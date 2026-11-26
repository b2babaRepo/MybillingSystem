import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useState } from 'react'
import { api, ApiError } from './api'
import { counterName } from './data'
import { uuidv7 } from './ids'

/**
 * Bills parked at the counter ("hold this, serve the next one"). Kept on the server so any till can pick one up;
 * picking it up takes it, so two tills can't both carry on the same bill. A bill held with no internet waits on this
 * device and goes up as soon as the server answers.
 */
export interface HeldBill<Cart> {
  id: string
  /** The app's own cart, kept as it was. */
  payload: Cart
  label: string | null
  total: number
  lines: number
  counter: string | null
  heldBy: string | null
  heldAt: string
  /** Still only on this device (held with no internet). */
  local?: boolean
}

/** Picked up at another till first. */
export class HeldBillGone extends Error {}

const key = (shopId: string) => `held-bills:${shopId}`

function readLocal<Cart>(shopId: string): HeldBill<Cart>[] {
  try {
    const raw = JSON.parse(localStorage.getItem(key(shopId)) ?? '[]') as unknown[]
    // Bills held on this device before held bills went to the server: { cart, customerId, at, delivery }.
    return raw.map((item) => {
      const old = item as { id?: string; cart?: unknown[]; customerId?: string; at?: number; delivery?: unknown }
      if (old.id) return { ...(item as HeldBill<Cart>), local: true }
      return {
        id: uuidv7(), payload: { cart: old.cart, customerId: old.customerId ?? '', delivery: old.delivery } as Cart,
        label: null, total: 0, lines: old.cart?.length ?? 0, counter: counterName() || null, heldBy: null,
        heldAt: new Date(old.at ?? Date.now()).toISOString(), local: true,
      }
    })
  } catch {
    return []
  }
}

function writeLocal<Cart>(shopId: string, list: HeldBill<Cart>[]) {
  try {
    if (list.length) localStorage.setItem(key(shopId), JSON.stringify(list))
    else localStorage.removeItem(key(shopId))
  } catch { /* private window: a bill held offline lasts until reload */ }
}

export function useHeldBills<Cart>(shopId: string) {
  const queryClient = useQueryClient()
  const queryKey = [shopId, 'held-bills']
  const remote = useQuery({ queryKey, queryFn: () => api<HeldBill<Cart>[]>('/v1/held-bills'), refetchInterval: 10_000, retry: false })
  const [local, setLocal] = useState<HeldBill<Cart>[]>(() => readLocal<Cart>(shopId))
  const keepLocal = useCallback((next: HeldBill<Cart>[]) => { setLocal(next); writeLocal(shopId, next) }, [shopId])

  // Send up what was held here with no internet, oldest first; whatever the server takes is no longer kept here.
  const upload = useCallback(async (list: HeldBill<Cart>[]) => {
    let rest = list
    for (const bill of list) {
      try {
        await api(`/v1/held-bills/${bill.id}`, 'PUT', { counter: bill.counter, label: bill.label, total: bill.total, lines: bill.lines, payload: bill.payload })
        rest = rest.filter((b) => b.id !== bill.id)
      } catch {
        break
      }
    }
    if (rest.length !== list.length) {
      keepLocal(rest)
      void queryClient.invalidateQueries({ queryKey })
    }
  }, [keepLocal, queryClient]) // eslint-disable-line react-hooks/exhaustive-deps

  // The server answered: anything still waiting here goes up now (syncing with the server, so an effect).
  useEffect(() => {
    // eslint-disable-next-line react/set-state-in-effect
    if (remote.isSuccess && local.length) void upload(local)
  }, [remote.dataUpdatedAt]) // eslint-disable-line react-hooks/exhaustive-deps

  const held = [...(remote.data ?? []).filter((r) => !local.some((l) => l.id === r.id)), ...local]
    .sort((a, b) => a.heldAt.localeCompare(b.heldAt))

  async function hold(payload: Cart, info: { label: string | null; total: number; lines: number }) {
    const bill: HeldBill<Cart> = { id: uuidv7(), payload, ...info, counter: counterName() || null, heldBy: null, heldAt: new Date().toISOString(), local: true }
    const next = [...local, bill]
    keepLocal(next)
    await upload(next)
  }

  /** Pick up a held bill: from this device, or from the server (taking it there). HeldBillGone if another till has it. */
  async function take(bill: HeldBill<Cart>): Promise<Cart> {
    if (bill.local) {
      keepLocal(local.filter((b) => b.id !== bill.id))
      return bill.payload
    }
    queryClient.setQueryData<HeldBill<Cart>[]>(queryKey, (list) => list?.filter((b) => b.id !== bill.id))
    try {
      return (await api<HeldBill<Cart>>(`/v1/held-bills/${bill.id}/take`, 'POST')).payload
    } catch (e) {
      if (e instanceof ApiError && e.status === 404) throw new HeldBillGone()
      throw e
    } finally {
      void queryClient.invalidateQueries({ queryKey })
    }
  }

  return { held, hold, take }
}

import { useQuery, type QueryClient } from '@tanstack/react-query'
import { useEffect, useMemo, useState, useSyncExternalStore } from 'react'
import { api, NetworkError } from './api'
import { readCache, writeCache } from './db'
import { uuidv7 } from './ids'
import { mergeCustomers, mergeProducts, mergeStatement, round2, type LocalCustomer } from './local'
import { cancelPending, enqueue, getSyncState, listOps, setSyncedHandler, subscribeOutbox, type OutboxOp } from './outbox'
import { getSession, subscribeSession } from './session'
import type { Customer, KhataEntryType, PaymentMode, Product, Statement, Unit } from './types'

export function useSession() {
  return useSyncExternalStore(subscribeSession, getSession)
}

/** This shop's outbox, kept current as ops are added and synced. */
export function useOutbox(shopId: string) {
  const [ops, setOps] = useState<OutboxOp[]>([])
  const state = useSyncExternalStore(subscribeOutbox, getSyncState)
  useEffect(() => {
    let active = true
    const load = () => void listOps(shopId).then((next) => active && setOps(next))
    load()
    const unsubscribe = subscribeOutbox(load)
    return () => {
      active = false
      unsubscribe()
    }
  }, [shopId])
  return { ops, state, pending: ops.filter((o) => o.status === 'pending').length, failed: ops.filter((o) => o.status === 'failed').length }
}

/** From the server when online (and saved for later); the saved copy when offline. */
async function cachedGet<T>(shopId: string, key: string, path: string): Promise<T> {
  const cacheKey = `${shopId}:${key}`
  try {
    const data = await api<T>(path)
    await writeCache(cacheKey, data)
    return data
  } catch (error) {
    if (error instanceof NetworkError) {
      const cached = await readCache<T>(cacheKey)
      if (cached !== undefined) return cached
    }
    throw error
  }
}

export function useCustomers(shopId: string) {
  const query = useQuery({
    queryKey: [shopId, 'customers'],
    queryFn: () => cachedGet<Customer[]>(shopId, 'customers', '/v1/customers'),
  })
  const { ops } = useOutbox(shopId)
  const customers = useMemo(() => mergeCustomers(query.data ?? [], ops), [query.data, ops])
  return { ...query, customers }
}

export function useProducts(shopId: string) {
  const query = useQuery({
    queryKey: [shopId, 'products'],
    queryFn: () => cachedGet<Product[]>(shopId, 'products', '/v1/products'),
  })
  const { ops } = useOutbox(shopId)
  const products = useMemo(() => mergeProducts(query.data ?? [], ops), [query.data, ops])
  return { ...query, products }
}

export function useStatement(shopId: string, customer: LocalCustomer | undefined) {
  const customerId = customer?.id
  const query = useQuery({
    queryKey: [shopId, 'statement', customerId],
    queryFn: () => cachedGet<Statement>(shopId, `statement:${customerId}`, `/v1/customers/${customerId}/statement`),
    // A customer added offline isn't on the server yet: nothing to fetch.
    enabled: customer !== undefined && !customer.isNew,
    retry: false,
  })
  const { ops } = useOutbox(shopId)
  return useMemo(() => mergeStatement(query.data, customer, ops), [query.data, customer, ops])
}

/**
 * After the server accepts an op, folds its reply into the cached lists, so the screen doesn't jump
 * while the fresh copy loads, then refetches.
 */
export function installSyncHandler(queryClient: QueryClient) {
  setSyncedHandler((op, reply) => {
    const customersKey = [op.shopId, 'customers']
    const balance = (reply as { customerBalance?: number } | null)?.customerBalance
    if (op.kind === 'customer') {
      queryClient.setQueryData<Customer[]>(customersKey, (list = []) =>
        list.some((c) => c.id === op.id) ? list : [...list, reply as Customer])
    } else if (op.customerId && typeof balance === 'number') {
      queryClient.setQueryData<Customer[]>(customersKey, (list = []) =>
        list.map((c) => (c.id === op.customerId ? { ...c, balance } : c)))
    } else if (op.kind === 'product') {
      queryClient.setQueryData<Product[]>([op.shopId, 'products'], (list = []) =>
        list.some((p) => p.id === op.id) ? list : [...list, reply as Product])
    }

    void writeCache(`${op.shopId}:customers`, queryClient.getQueryData(customersKey))
    void queryClient.invalidateQueries({ queryKey: [op.shopId] })
  })
}

// ---- Writes: all go through the outbox, so they work the same online and offline. ----

export async function addCustomer(shopId: string, name: string, phone: string) {
  const id = uuidv7()
  await enqueue({ id, shopId, kind: 'customer', path: '/v1/customers', body: { id, name, phone: phone || null }, label: `+ ${name}` })
  return id
}

export async function addKhataEntry(
  shopId: string,
  customer: { id: string; name: string },
  type: KhataEntryType,
  amount: number,
  paymentMode: PaymentMode,
  note: string,
) {
  const id = uuidv7()
  await enqueue({
    id,
    shopId,
    kind: 'khata',
    path: '/v1/khata-entries',
    body: {
      id,
      customerId: customer.id,
      type,
      amount,
      paymentMode: type === 'payment' ? paymentMode : null,
      note: note || null,
      occurredAt: new Date().toISOString(),
    },
    customerId: customer.id,
    delta: type === 'credit' ? amount : -amount,
    label: `${customer.name} ₹${amount} ${type === 'credit' ? 'udhaar' : 'jama'}`,
  })
  return id
}

export async function reverseEntry(shopId: string, entry: { id: string; amount: number }, customer: { id: string; name: string }) {
  const id = uuidv7()
  await enqueue({
    id,
    shopId,
    kind: 'reverse',
    path: `/v1/ledger-entries/${entry.id}/reverse`,
    body: { id },
    customerId: customer.id,
    delta: -entry.amount,
    reversesEntryId: entry.id,
    label: `${customer.name} ₹${Math.abs(entry.amount)} hataya`,
  })
}

/** Undo right after saving: drops the entry if it hasn't synced yet, otherwise posts a reversal. */
export async function undoEntry(shopId: string, entry: { id: string; amount: number }, customer: { id: string; name: string }) {
  if (!(await cancelPending(shopId, entry.id))) await reverseEntry(shopId, entry, customer)
}

export interface CartLine {
  productId: string | null
  name: string
  quantity: number
  unit: Unit
  unitPrice: number
}

export async function saveBill(
  shopId: string,
  lines: CartLine[],
  customer: { id: string; name: string } | null,
  paidAmount: number,
  paymentMode: PaymentMode,
) {
  const id = uuidv7()
  const total = round2(lines.reduce((sum, l) => sum + round2(l.quantity * l.unitPrice), 0))
  await enqueue({
    id,
    shopId,
    kind: 'bill',
    path: '/v1/bills',
    body: { id, customerId: customer?.id ?? null, lines, paidAmount, paymentMode, occurredAt: new Date().toISOString() },
    customerId: customer?.id,
    delta: customer ? round2(total - paidAmount) : undefined,
    label: `Bill ₹${total}${customer ? ` · ${customer.name}` : ''}`,
  })
  return { id, total }
}

export async function addProduct(shopId: string, name: string, unit: Unit, sellingPrice: number) {
  const id = uuidv7()
  await enqueue({
    id,
    shopId,
    kind: 'product',
    path: '/v1/products',
    body: { id, name, unit, sellingPrice, costPrice: null },
    label: `+ ${name}`,
  })
}

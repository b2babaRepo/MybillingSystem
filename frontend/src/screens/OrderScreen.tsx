import { btn } from '../components/ui'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api, NetworkError } from '../lib/api'
import { enqueue } from '../lib/outbox'
import { formatRupees } from '../lib/format'
import { useProducts } from '../lib/data'
import { round2 } from '../lib/local'
import type { Order } from '../lib/planner'
import { confirmAsk } from '../lib/ask'

/** AI-read lines: Ready (sure), Check (one part unsure), Not understood (read the customer's words below it). */
const AI_CHIP = { high: 'bg-[#e0efec] text-brand-strong', medium: 'bg-amber-100 text-warn-ink', low: 'bg-red-100 text-due' } as const

interface Draft {
  packedQuantity: string
  unitPrice: string
  removed: boolean
  shopNote: string
  /** The shop's option packed for this line (another brand or pack size than asked is allowed). */
  productId: string | null
}

/**
 * Packing a household's list: per line, which of the shop's options was packed (the household's pick, or the
 * shop's choice), how much, at what price, or "not available". A line that isn't what they asked for (when they
 * said "ask me"), or whose price moved a lot, waits for their OK before delivery. Then deliver, which makes the
 * bill and puts anything unpaid on the customer's khata. Online only.
 */
export function OrderScreen({ shopId, orderId }: { shopId: string; orderId: string }) {
  const { t } = useTranslation()
  const query = useQuery({ queryKey: [shopId, 'order', orderId], queryFn: () => api<Order>(`/v1/orders/${orderId}`) })
  if (!query.data) return <p className="p-4 text-muted">{query.isError ? query.error.message : t('syncing')}</p>
  // Keyed by status, so the form starts again from the server's figures after packing or delivering.
  return <OrderEditor key={`${query.data.id}:${query.data.status}`} shopId={shopId} order={query.data} />
}

function OrderEditor({ shopId, order }: { shopId: string; order: Order }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const orderId = order.id
  const [drafts, setDrafts] = useState<Record<string, Draft>>(() =>
    Object.fromEntries(order.lines.map((l) => [l.id, {
      packedQuantity: String(l.packedQuantity ?? l.quantity),
      unitPrice: l.unitPrice === null ? '' : String(l.unitPrice),
      removed: l.removed,
      shopNote: l.shopNote ?? '',
      productId: l.productId ?? null,
    }])))
  const { products } = useProducts(shopId)
  /** The shop's options for a line: its products for the same common-list item as the line's current one. */
  const optionsFor = (productId: string | null | undefined) => {
    const item = products.find((p) => p.id === productId)?.catalogueItemId
    return item ? products.filter((p) => p.catalogueItemId === item) : []
  }
  const waiting = order.status === 'packed' && order.lines.some((l) => l.approval === 'pending')
  const toCheck = order.lines.filter((l) => l.aiConfidence && l.aiConfidence !== 'high').length
  const [paid, setPaid] = useState<string | null>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)

  const toPack = order.status === 'placed' || order.status === 'accepted'
  const editable = toPack || order.status === 'packed'
  // Packed and (once nothing waits) ready at the counter or on the way: what remains is handing it over.
  const handing = order.status === 'packed' || order.status === 'ready_for_pickup' || order.status === 'out_for_delivery'
  // The delivery charge the customer saw at checkout is part of what they pay.
  const fee = order.deliveryFee ?? 0
  const total = round2(fee + order.lines.reduce((sum, l) => {
    const d = drafts[l.id]
    return d && !d.removed ? sum + round2((Number(d.packedQuantity) || 0) * (Number(d.unitPrice) || 0)) : sum
  }, 0))
  const allPriced = order.lines.every((l) => drafts[l.id]?.removed || Number(drafts[l.id]?.unitPrice) > 0)
  const set = (id: string, change: Partial<Draft>) => setDrafts((all) => ({ ...all, [id]: { ...all[id], ...change } }))

  /**
   * An order action that keeps working with no network: online it goes straight to the server; offline it waits in
   * the outbox (sent in order once back online; the server treats a repeat as already done) and the screen moves on
   * at once with the expected result.
   */
  async function act(path: string, body: unknown, expected: Partial<Order>, label: string, queueOnly = false): Promise<Order & { queued?: boolean }> {
    try {
      if (queueOnly) throw new NetworkError()
      return await api<Order>(path, 'POST', body)
    } catch (err) {
      if (!(err instanceof NetworkError)) throw err
      await enqueue({ id: crypto.randomUUID(), shopId, kind: 'orderAction', path, body, label })
      const current = queryClient.getQueryData<Order>([shopId, 'order', orderId]) ?? order
      return { ...current, ...expected, queued: true }
    }
  }

  async function run(action: () => Promise<Order>) {
    setBusy(true)
    setError('')
    try {
      const updated = await action() as Order & { queued?: boolean }
      queryClient.setQueryData([shopId, 'order', orderId], updated)
      // Waiting in the outbox: the server doesn't know yet, so re-reading it would undo what the screen shows.
      // The outbox refreshes everything once it syncs.
      if (!updated.queued) void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (err) {
      setError(err instanceof Error ? err.message : t('error'))
    } finally {
      setBusy(false)
    }
  }

  const packBody = () => ({
    lines: order.lines.map((l) => ({
      id: l.id,
      removed: drafts[l.id].removed,
      packedQuantity: Number(drafts[l.id].packedQuantity),
      unitPrice: drafts[l.id].removed ? null : Number(drafts[l.id].unitPrice),
      shopNote: drafts[l.id].shopNote || null,
      productId: drafts[l.id].productId !== l.productId ? drafts[l.id].productId : null,
    })),
  })
  const label = `${order.customerName ?? ''} · ${formatRupees(total)}`
  const pack = () => run(() => act(`/v1/orders/${orderId}/pack`, packBody(), { status: 'packed', finalAmount: total }, `${t('inbox.packedShort')} ${label}`))
  const defaultPaid = order.payment === 'khata' ? 0 : (order.finalAmount ?? 0)
  const deliver = () => run(() => act(`/v1/orders/${orderId}/deliver`, { paidAmount: paid === null ? defaultPaid : Number(paid) || 0 }, { status: 'delivered' }, `${t('inbox.deliveredShort')} ${label}`))
  // Pickup with nothing for the customer to approve (no item taken out or swapped): pack and hand over in one tap.
  const noApprovals = order.lines.every((l) => !drafts[l.id]?.removed && (drafts[l.id]?.productId ?? l.productId) === l.productId)
  const canHandOver = toPack && order.fulfilment === 'pickup' && allPriced && noApprovals
  const handOver = () => run(async () => {
    const packed = await act(`/v1/orders/${orderId}/pack`, packBody(), { status: 'packed', finalAmount: total }, `${t('inbox.packedShort')} ${label}`)
    // A pack waiting in the outbox must reach the server first, so the hand-over waits behind it.
    return act(`/v1/orders/${orderId}/deliver`, { paidAmount: order.payment === 'khata' ? 0 : total }, { status: 'delivered' }, `${t('inbox.deliveredShort')} ${label}`, packed.queued)
  })
  const accept = () => run(() => act(`/v1/orders/${orderId}/accept`, {}, { status: 'accepted' }, `${t('cust2.accept')} ${label}`))
  const ready = () => run(() => act(`/v1/orders/${orderId}/ready`, {}, { status: order.fulfilment === 'pickup' ? 'ready_for_pickup' : 'out_for_delivery' }, `${t(order.fulfilment === 'pickup' ? 'cust2.ready' : 'cust2.out')} ${label}`))
  const cancel = async () => (await confirmAsk(t('inbox.cancelConfirm'))) && void run(() => act(`/v1/orders/${orderId}/cancel`, {}, { status: 'cancelled' }, `${t('inbox.cancelledShort')} ${label}`))

  return (
    <div className="flex flex-col gap-3 p-4 pb-32 lg:pb-4">
      <section className="flex flex-col gap-1 rounded-card bg-white p-4 shadow-sm">
        <span className="text-xl font-bold">{order.customerName}</span>
        <span className="text-sm text-muted">
          {order.customerPhone} · {order.fulfilment === 'delivery' ? t('plan.delivery') : t('plan.pickup')} · {order.payment === 'khata' ? t('plan.onKhata') : t(order.payment)}
          {order.slot && ` · ${order.slot}`}
        </span>
        {order.address && <span className="mt-1 text-sm">{order.address}</span>}
        {order.note && <span className="mt-1 rounded-lg bg-warn-soft px-2 py-1 text-sm text-warn-ink">“{order.note}”</span>}
        <span className="mt-1 text-sm font-bold text-brand-strong">{t(`plan.status.${order.status}`)}</span>
      </section>

      {toCheck > 0 && (
        <p className="rounded-[14px] bg-warn-soft p-3 text-sm font-semibold text-amber-950"><svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="mr-1 inline align-[-3px]"><path d="M12 3l10 18H2zM12 10v4M12 17h.01" /></svg>{t('aiShop.summary', { count: toCheck })}</p>
      )}
      <ul className="flex flex-col gap-2">
        {order.lines.map((l) => {
          const d = drafts[l.id]
          if (!d) return null
          return (
            <li key={l.id} className={`flex flex-col gap-2 rounded-card bg-white p-3 ${d.removed ? 'opacity-60' : ''} shadow-sm`}>
              <div className="flex items-center justify-between gap-2">
                <span className="flex flex-col">
                  <span className="font-bold">{products.find((p) => p.id === d.productId)?.name ?? l.name}</span>
                  <span className="text-xs text-muted">
                    {l.requestedName ? t('options.askedFor', { asked: l.requestedName }) : `${l.quantity} ${t(`units.${l.unit}`, { count: l.quantity })}`}
                    {l.note && ` · ${l.note}`}
                  </span>
                  {l.aiConfidence && (
                    <span className={`mt-1 w-fit rounded-full px-2 py-0.5 text-[11px] font-bold ${AI_CHIP[l.aiConfidence]}`}>
                      <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="mr-1 inline align-[-3px]"><path d="M12 3l10 18H2zM12 10v4M12 17h.01" /></svg>{t(`aiShop.${l.aiConfidence}`)}
                    </span>
                  )}
                  {l.preference && (
                    <span className={`mt-1 w-fit rounded-full px-2 py-0.5 text-[11px] font-bold ${l.preference === 'shop_chooses' ? 'bg-[#e0efec] text-brand-strong' : 'bg-amber-100 text-warn-ink'}`}>
                      {t(`options.pref.${l.requestedProductId ? 'picked' : 'any'}.${l.preference}`)}
                    </span>
                  )}
                  {l.approval === 'pending' && <span className="mt-1 text-xs font-bold text-warn">⏳ {t('options.waitingShop')}</span>}
                  {l.approval === 'approved' && <span className="mt-1 text-xs font-bold text-brand-strong">✓ {t('options.approved')}</span>}
                  {l.approval === 'declined' && <span className="mt-1 text-xs font-bold text-due">✗ {t('options.declined')}</span>}
                </span>
                <label className="flex min-h-11 items-center gap-2 text-sm font-semibold">
                  <input type="checkbox" checked={d.removed} disabled={!editable} onChange={(e) => set(l.id, { removed: e.target.checked })} className="h-5 w-5" />
                  {t('inbox.remove')}
                </label>
              </div>
              {!d.removed && optionsFor(l.productId).length > 1 && (
                <label className="block">
                  <span className="mb-1 block text-xs font-semibold text-muted">{t('options.packedOption')}</span>
                  <select
                    value={d.productId ?? ''}
                    disabled={!editable}
                    onChange={(e) => {
                      const option = products.find((p) => p.id === e.target.value)
                      if (option) set(l.id, { productId: option.id, unitPrice: String(option.sellingPrice) })
                    }}
                    className="h-11 w-full rounded-xl border border-line bg-white px-2 outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10"
                  >
                    {optionsFor(l.productId).map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name} · {formatRupees(p.sellingPrice)}/{t(`units.${p.unit}`)}{p.id === l.requestedProductId ? ` · ${t('options.theirPick')}` : ''}{p.stock <= 0 ? ` · ${t('store.out')}` : ''}
                      </option>
                    ))}
                  </select>
                </label>
              )}
              {!d.removed && (
                <div className="grid grid-cols-[1fr_1fr_auto] items-end gap-2">
                  <label className="block">
                    <span className="mb-1 block text-xs font-semibold text-muted">{t('inbox.packedQty')} ({t(`units.${l.unit}`)})</span>
                    <input value={d.packedQuantity} disabled={!editable} onChange={(e) => set(l.id, { packedQuantity: e.target.value })} inputMode="decimal" className="h-11 w-full rounded-xl border border-line px-2 bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
                  </label>
                  <label className="block">
                    <span className="mb-1 block text-xs font-semibold text-muted">{t('inbox.price')}</span>
                    <input value={d.unitPrice} disabled={!editable} onChange={(e) => set(l.id, { unitPrice: e.target.value })} inputMode="decimal" placeholder="₹" className="h-11 w-full rounded-xl border border-line px-2 bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
                  </label>
                  <span className="w-20 pb-2.5 text-right font-bold">{formatRupees(round2((Number(d.packedQuantity) || 0) * (Number(d.unitPrice) || 0)))}</span>
                </div>
              )}
              <input value={d.shopNote} disabled={!editable} onChange={(e) => set(l.id, { shopNote: e.target.value })} maxLength={200} placeholder={t('inbox.shopNote')} className="h-11 rounded-xl border border-line px-2 text-sm bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
            </li>
          )
        })}
      </ul>

      {error && <p className="rounded-lg bg-due-soft p-3 text-due">{error}</p>}

      {fee > 0 && <p className="rounded-card bg-white p-3 text-sm shadow-sm">{t('cust2.delivery')}: <b>{formatRupees(fee)}</b> · {t('cust2.feeOnBill')}</p>}

      {handing && (
        <label className="block rounded-card bg-white p-3 shadow-sm">
          <span className="mb-1 block text-sm font-semibold text-muted">{t('inbox.paidNow')}</span>
          <input value={paid ?? String(defaultPaid)} onChange={(e) => setPaid(e.target.value)} inputMode="decimal" className="h-12 w-full rounded-xl border border-line px-3 text-lg bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
          {/* Cash received at the door: all of it, some (type it), or none (all on khata). */}
          <span className="mt-2 grid grid-cols-2 gap-2">
            <button type="button" onClick={() => setPaid(String(order.finalAmount ?? 0))} className="min-h-11 rounded-xl border border-line font-semibold transition hover:bg-[#f6faf9]">{t('print.paid')} · {formatRupees(order.finalAmount ?? 0)}</button>
            <button type="button" onClick={() => setPaid('0')} className="min-h-11 rounded-xl border border-line font-semibold transition hover:bg-[#f6faf9]">{t('print.onKhata')} · {formatRupees(order.finalAmount ?? 0)}</button>
          </span>
        </label>
      )}

      {editable && (
        <button onClick={cancel} disabled={busy} className={`min-h-11 text-sm ${btn.dangerOutline}`}>{t('inbox.cancel')}</button>
      )}

      {(editable || handing) && (
        <div className="fixed inset-x-0 bottom-[68px] z-10 mx-auto lg:sticky lg:bottom-0 lg:mx-0 lg:max-w-none lg:rounded-card lg:border lg:shadow-lg flex max-w-xl flex-col gap-2 border-t border-line bg-white px-4 py-3">
          {order.status === 'placed' && (
            <button onClick={accept} disabled={busy} className={`h-12 rounded-xl ${btn.outline}`}>✓ {t('cust2.accept')}</button>
          )}
          {canHandOver && (
            <button onClick={handOver} disabled={busy} className={`h-14 text-lg rounded-xl ${btn.solid}`}>
              {order.payment === 'khata' ? t('inbox.handOverKhata', { amount: formatRupees(total) }) : t('inbox.handOver', { amount: formatRupees(total) })}
            </button>
          )}
          {editable && (
            <button onClick={pack} disabled={busy || !allPriced} className={`h-12 rounded-2xl font-bold ${order.status === 'packed' || canHandOver ? 'border border-line bg-white' : 'bg-brand text-white'} disabled:opacity-40`}>
              {t('inbox.markPacked', { amount: formatRupees(total) })}
            </button>
          )}
          {waiting && <p className="text-center text-sm font-semibold text-warn">⏳ {t('options.waitingDeliver')}</p>}
          {order.status === 'packed' && !waiting && (
            <button onClick={ready} disabled={busy} className={`h-12 rounded-xl ${btn.outline}`}>{order.fulfilment === 'pickup' ? `🛍️ ${t('cust2.ready')}` : `🛵 ${t('cust2.out')}`}</button>
          )}
          {handing && order.finalAmount !== null && (
            <button onClick={deliver} disabled={busy || waiting} className={`h-14 text-lg rounded-xl ${btn.solid}`}>
              {t('inbox.deliver', { amount: formatRupees(order.finalAmount) })}
            </button>
          )}
        </div>
      )}
    </div>
  )
}

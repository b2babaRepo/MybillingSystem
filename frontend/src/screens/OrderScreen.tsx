import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { useProducts } from '../lib/data'
import { round2 } from '../lib/local'
import type { Order } from '../lib/planner'

/** AI-read lines: Ready (sure), Check (one part unsure), Not understood (read the customer's words below it). */
const AI_CHIP = { high: 'bg-[#e0efec] text-teal-800', medium: 'bg-amber-100 text-amber-900', low: 'bg-red-100 text-due' } as const

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

  const editable = order.status === 'placed' || order.status === 'packed'
  const total = round2(order.lines.reduce((sum, l) => {
    const d = drafts[l.id]
    return d && !d.removed ? sum + round2((Number(d.packedQuantity) || 0) * (Number(d.unitPrice) || 0)) : sum
  }, 0))
  const allPriced = order.lines.every((l) => drafts[l.id]?.removed || Number(drafts[l.id]?.unitPrice) > 0)
  const set = (id: string, change: Partial<Draft>) => setDrafts((all) => ({ ...all, [id]: { ...all[id], ...change } }))

  async function run(action: () => Promise<Order>) {
    setBusy(true)
    setError('')
    try {
      const updated = await action()
      queryClient.setQueryData([shopId, 'order', orderId], updated)
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (err) {
      setError(err instanceof Error ? err.message : t('error'))
    } finally {
      setBusy(false)
    }
  }

  const pack = () => run(() => api<Order>(`/v1/orders/${orderId}/pack`, 'POST', {
    lines: order.lines.map((l) => ({
      id: l.id,
      removed: drafts[l.id].removed,
      packedQuantity: Number(drafts[l.id].packedQuantity),
      unitPrice: drafts[l.id].removed ? null : Number(drafts[l.id].unitPrice),
      shopNote: drafts[l.id].shopNote || null,
      productId: drafts[l.id].productId !== l.productId ? drafts[l.id].productId : null,
    })),
  }))
  const defaultPaid = order.payment === 'khata' ? 0 : (order.finalAmount ?? 0)
  const deliver = () => run(() => api<Order>(`/v1/orders/${orderId}/deliver`, 'POST', { paidAmount: paid === null ? defaultPaid : Number(paid) || 0 }))
  const cancel = () => confirm(t('inbox.cancelConfirm')) && void run(() => api<Order>(`/v1/orders/${orderId}/cancel`, 'POST', {}))

  return (
    <div className="flex flex-col gap-3 p-4 pb-32">
      <section className="flex flex-col gap-1 rounded-[18px] bg-white p-4">
        <span className="text-xl font-bold">{order.customerName}</span>
        <span className="text-sm text-muted">
          {order.customerPhone} · {order.fulfilment === 'delivery' ? t('plan.delivery') : t('plan.pickup')} · {order.payment === 'khata' ? t('plan.onKhata') : t(order.payment)}
          {order.slot && ` · ${order.slot}`}
        </span>
        {order.address && <span className="mt-1 text-sm">{order.address}</span>}
        {order.note && <span className="mt-1 rounded-lg bg-amber-50 px-2 py-1 text-sm text-amber-900">“{order.note}”</span>}
        <span className="mt-1 text-sm font-bold text-teal-800">{t(`plan.status.${order.status}`)}</span>
      </section>

      {toCheck > 0 && (
        <p className="rounded-[14px] bg-amber-50 p-3 text-sm font-semibold text-amber-950">🤖 {t('aiShop.summary', { count: toCheck })}</p>
      )}
      <ul className="flex flex-col gap-2">
        {order.lines.map((l) => {
          const d = drafts[l.id]
          if (!d) return null
          return (
            <li key={l.id} className={`flex flex-col gap-2 rounded-[18px] bg-white p-3 ${d.removed ? 'opacity-60' : ''}`}>
              <div className="flex items-center justify-between gap-2">
                <span className="flex flex-col">
                  <span className="font-bold">{products.find((p) => p.id === d.productId)?.name ?? l.name}</span>
                  <span className="text-xs text-muted">
                    {l.requestedName ? t('options.askedFor', { asked: l.requestedName }) : `${l.quantity} ${t(`units.${l.unit}`)}`}
                    {l.note && ` · ${l.note}`}
                  </span>
                  {l.aiConfidence && (
                    <span className={`mt-1 w-fit rounded-full px-2 py-0.5 text-[11px] font-bold ${AI_CHIP[l.aiConfidence]}`}>
                      🤖 {t(`aiShop.${l.aiConfidence}`)}
                    </span>
                  )}
                  {l.preference && (
                    <span className={`mt-1 w-fit rounded-full px-2 py-0.5 text-[11px] font-bold ${l.preference === 'shop_chooses' ? 'bg-[#e0efec] text-teal-800' : 'bg-amber-100 text-amber-900'}`}>
                      {t(`options.pref.${l.requestedProductId ? 'picked' : 'any'}.${l.preference}`)}
                    </span>
                  )}
                  {l.approval === 'pending' && <span className="mt-1 text-xs font-bold text-amber-800">⏳ {t('options.waitingShop')}</span>}
                  {l.approval === 'approved' && <span className="mt-1 text-xs font-bold text-teal-800">✓ {t('options.approved')}</span>}
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
                    className="h-11 w-full rounded-xl border border-line bg-white px-2"
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
                    <input value={d.packedQuantity} disabled={!editable} onChange={(e) => set(l.id, { packedQuantity: e.target.value })} inputMode="decimal" className="h-11 w-full rounded-xl border border-line px-2" />
                  </label>
                  <label className="block">
                    <span className="mb-1 block text-xs font-semibold text-muted">{t('inbox.price')}</span>
                    <input value={d.unitPrice} disabled={!editable} onChange={(e) => set(l.id, { unitPrice: e.target.value })} inputMode="decimal" placeholder="₹" className="h-11 w-full rounded-xl border border-line px-2" />
                  </label>
                  <span className="w-20 pb-2.5 text-right font-bold">{formatRupees(round2((Number(d.packedQuantity) || 0) * (Number(d.unitPrice) || 0)))}</span>
                </div>
              )}
              <input value={d.shopNote} disabled={!editable} onChange={(e) => set(l.id, { shopNote: e.target.value })} maxLength={200} placeholder={t('inbox.shopNote')} className="h-11 rounded-xl border border-line px-2 text-sm" />
            </li>
          )
        })}
      </ul>

      {error && <p className="rounded-lg bg-red-50 p-3 text-due">{error}</p>}

      {order.status === 'packed' && (
        <label className="block rounded-[18px] bg-white p-3">
          <span className="mb-1 block text-sm font-semibold text-muted">{t('inbox.paidNow')}</span>
          <input value={paid ?? String(defaultPaid)} onChange={(e) => setPaid(e.target.value)} inputMode="decimal" className="h-12 w-full rounded-[14px] border border-line px-3 text-lg" />
          {/* Cash received at the door: all of it, some (type it), or none (all on khata). */}
          <span className="mt-2 grid grid-cols-2 gap-2">
            <button type="button" onClick={() => setPaid(String(order.finalAmount ?? 0))} className="min-h-11 rounded-xl border border-line font-semibold">{t('print.paid')} · {formatRupees(order.finalAmount ?? 0)}</button>
            <button type="button" onClick={() => setPaid('0')} className="min-h-11 rounded-xl border border-line font-semibold">{t('print.onKhata')} · {formatRupees(order.finalAmount ?? 0)}</button>
          </span>
        </label>
      )}

      {editable && (
        <button onClick={cancel} disabled={busy} className="min-h-11 text-sm font-semibold text-due underline">{t('inbox.cancel')}</button>
      )}

      {editable && (
        <div className="fixed inset-x-0 bottom-[68px] z-10 mx-auto flex max-w-xl flex-col gap-2 border-t border-line bg-white px-4 py-3">
          <button onClick={pack} disabled={busy || !allPriced} className={`h-12 rounded-2xl font-bold ${order.status === 'packed' ? 'border border-line bg-white' : 'bg-teal-700 text-white'} disabled:opacity-40`}>
            {t('inbox.markPacked', { amount: formatRupees(total) })}
          </button>
          {waiting && <p className="text-center text-sm font-semibold text-amber-800">⏳ {t('options.waitingDeliver')}</p>}
          {order.status === 'packed' && order.finalAmount !== null && (
            <button onClick={deliver} disabled={busy || waiting} className="h-14 rounded-2xl bg-teal-700 text-lg font-bold text-white disabled:opacity-40">
              {t('inbox.deliver', { amount: formatRupees(order.finalAmount) })}
            </button>
          )}
        </div>
      )}
    </div>
  )
}

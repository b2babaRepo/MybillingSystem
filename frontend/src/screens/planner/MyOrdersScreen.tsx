import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { PendingDot, btn } from '../../components/ui'
import { formatDateTime, formatRupees, whatsAppLink } from '../../lib/format'
import { api } from '../../lib/api'
import { confirmAsk } from '../../components/Ask'
import { navigate } from '../../router'
import { answerChangedLine, homeKey, useMyOrders, type Order, type OrderStatus } from '../../lib/planner'

const STEPS: OrderStatus[] = ['placed', 'packed', 'delivered']

/** Mockup 6: each order's progress, what the shop changed, and the final amount once packed. */
export function MyOrdersScreen({ userId }: { userId: string }) {
  const { t } = useTranslation()
  const { orders, isLoading } = useMyOrders(userId)

  return (
    <div className="flex flex-col gap-3 p-4">
      {!isLoading && orders.length === 0 && <p className="py-8 text-center text-muted">{t('plan.noOrders')}</p>}
      {orders.map((order) => (
        <OrderCard key={order.id} order={order} userId={userId} />
      ))}
    </div>
  )
}

function OrderCard({ order, userId }: { order: Order; userId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [busy, setBusy] = useState<string | null>(null)
  const waiting = order.status === 'packed' ? order.lines.filter((l) => l.approval === 'pending') : []

  async function answer(lineId: string, approve: boolean) {
    setBusy(lineId)
    try {
      await answerChangedLine(order.id, lineId, approve)
      await queryClient.invalidateQueries({ queryKey: [homeKey(userId), 'orders'] })
    } finally {
      setBusy(null)
    }
  }
  const reached = order.status === 'cancelled' ? -1 : STEPS.indexOf(order.status)
  const changed = order.lines.filter((l) => l.removed || l.shopNote || (l.packedQuantity !== null && l.packedQuantity !== l.quantity))

  return (
    <section className="flex flex-col gap-3 rounded-card bg-white p-4">
      <div className="flex items-start justify-between gap-2">
        <button onClick={() => navigate(`/orders/${order.id}`)} className="flex flex-col text-left">
          <span className="text-[17px] font-bold">{order.shopName}</span>
          <span className="text-[13px] text-muted">
            {order.source === 'store' ? t('orders2.online') : t(`plan.${order.listKind}List`)} · {t('plan.itemsCount', { count: order.lines.length })} · {formatDateTime(order.createdAt)}
          </span>
          <span className="text-[13px] font-bold text-brand">{t('cust.viewOrder')} ›</span>
        </button>
        <span className={`flex items-center gap-1.5 rounded-lg px-2 py-0.5 text-xs font-bold ${order.status === 'cancelled' ? 'bg-due-soft text-due' : 'bg-[#e0efec] text-brand-strong'}`}>
          {order.pending && <PendingDot />}
          {order.pending ? t('plan.sending') : t(`plan.status.${order.status}`)}
        </span>
      </div>

      {order.status !== 'cancelled' && (
        <ol className="grid grid-cols-3 gap-1" aria-label={t(`plan.status.${order.status}`)}>
          {STEPS.map((step, index) => (
            <li key={step} className="flex flex-col gap-1">
              <span className={`h-1.5 rounded-full ${index <= reached && !order.pending ? 'bg-brand' : 'bg-[#edf2f1]'}`} />
              <span className={`text-xs ${index <= reached && !order.pending ? 'font-bold text-brand-strong' : 'text-muted'}`}>{t(`plan.status.${step}`)}</span>
            </li>
          ))}
        </ol>
      )}

      {waiting.length > 0 && (
        <div className="flex flex-col gap-2 rounded-xl bg-warn-soft p-3 text-sm text-amber-950 ring-1 ring-amber-300">
          <span className="font-bold">⚠ {t('options.waitingTitle')}</span>
          {waiting.map((l) => (
            <div key={l.id} className="flex flex-col gap-2 rounded-lg bg-white p-2.5">
              <span>
                {t('options.askedFor', { asked: l.requestedName ?? '' })} → <b>{l.name}</b> · {l.packedQuantity ?? l.quantity} {t(`units.${l.unit}`, { count: l.packedQuantity ?? l.quantity })}
              </span>
              {l.expectedAmount !== null && l.expectedAmount !== undefined && l.amount !== null && (
                <span className="font-semibold">{formatRupees(l.expectedAmount)} → {formatRupees(l.amount)}</span>
              )}
              {l.shopNote && <span className="text-muted">{l.shopNote}</span>}
              <div className="grid grid-cols-2 gap-2">
                <button disabled={busy === l.id} onClick={() => void answer(l.id, true)} className={`min-h-11 rounded-xl ${btn.solid}`}>{t('options.ok')}</button>
                <button disabled={busy === l.id} onClick={() => void answer(l.id, false)} className={`min-h-11 ${btn.dangerOutline}`}>{t('options.leaveOut')}</button>
              </div>
            </div>
          ))}
        </div>
      )}

      {changed.length > 0 && (
        <div className="flex flex-col gap-1 rounded-xl bg-[#f1f5f4] p-3 text-sm">
          <span className="font-bold">{t('plan.shopChanged')}</span>
          {changed.map((l) => (
            <span key={l.id} className={l.removed ? 'text-due' : ''}>
              {l.name}: {l.removed ? t('plan.removedItem') : `${l.packedQuantity ?? l.quantity} ${t(`units.${l.unit}`, { count: l.packedQuantity ?? l.quantity })}`}
              {l.shopNote && ` · ${l.shopNote}`}
            </span>
          ))}
        </div>
      )}

      {order.finalAmount !== null && (
        <div className="flex justify-between text-lg font-extrabold">
          <span>{t('plan.finalAmount')}</span>
          <span>{formatRupees(order.finalAmount)}</span>
        </div>
      )}
    </section>
  )
}

/** One order in full: every line, the progress, what the shop changed, how and where, the bill, and Call / WhatsApp / Cancel. */
export function OrderDetail({ userId, orderId }: { userId: string; orderId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { orders, isLoading } = useMyOrders(userId)
  const [message, setMessage] = useState('')
  const order = orders.find((o) => o.id === orderId)
  if (!order) return <p className="p-6 text-center text-muted">{isLoading ? '…' : t('cust.orderGone')}</p>

  async function cancel() {
    if (!order || !(await confirmAsk(t('cust.cancelConfirm', { shop: order.shopName })))) return
    setMessage('')
    try {
      await api(`/v1/me/orders/${order.id}/cancel`, 'POST', {})
      await queryClient.invalidateQueries({ queryKey: [homeKey(userId), 'orders'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  const total = order.finalAmount ?? order.lines.reduce((s, l) => s + (l.amount ?? 0), 0)
  return (
    <div className="flex flex-col gap-3 p-4">
      <OrderCard order={order} userId={userId} />
      <section className="flex flex-col gap-1 rounded-card bg-white p-4">
        <h2 className="mb-1 font-bold">{t('cust.items')}</h2>
        {order.lines.map((l) => (
          <div key={l.id} className={`flex items-start justify-between gap-2 border-t border-[#edf2f1] py-2 text-sm first-of-type:border-t-0 ${l.removed ? 'text-muted line-through' : ''}`}>
            <span className="flex min-w-0 flex-col">
              <b className="truncate">{l.name}</b>
              <span className="text-xs text-muted">{l.packedQuantity ?? l.quantity} {t(`units.${l.unit}`, { count: l.packedQuantity ?? l.quantity })}{l.unitPrice != null ? ` × ${formatRupees(l.unitPrice)}` : ''}</span>
            </span>
            <span className="shrink-0 font-semibold">{l.amount != null ? formatRupees(l.amount) : '—'}</span>
          </div>
        ))}
        {total > 0 && <div className="mt-1 flex justify-between border-t border-line pt-2 text-lg font-extrabold"><span>{t('plan.finalAmount')}</span><span>{formatRupees(total)}</span></div>}
      </section>
      <section className="flex flex-col gap-1 rounded-card bg-white p-4 text-sm">
        <span><b>{t(`plan.${order.fulfilment}`)}</b>{order.slot ? ` · ${order.slot}` : ''}</span>
        {order.address && <span className="text-muted">📍 {order.address}</span>}
        <span className="text-muted">{t('cust.payment')}: {t(`cust.pay.${order.payment}`)}</span>
        {order.note && <span className="text-muted">“{order.note}”</span>}
      </section>
      <div className="grid grid-cols-2 gap-2">
        {order.shopPhone && <a href={`tel:${order.shopPhone}`} className="flex h-12 items-center justify-center rounded-xl border border-line font-bold">📞 {t('cust.callShop')}</a>}
        {order.shopPhone && <a href={whatsAppLink(order.shopPhone, t('cust.aboutOrder', { shop: order.shopName, id: order.id.slice(-4).toUpperCase() }))} target="_blank" rel="noreferrer" className="flex h-12 items-center justify-center rounded-xl bg-[#25D366] font-bold text-white">WhatsApp</a>}
        {order.shopSlug && <button onClick={() => navigate(`/s/${order.shopSlug}`)} className="col-span-2 h-12 rounded-xl border border-line font-semibold">{t('cust.openShop')}</button>}
        {order.status === 'placed' && !order.pending && <button onClick={() => void cancel()} className="col-span-2 h-12 rounded-xl border border-due font-bold text-due">{t('cust.cancelOrder')}</button>}
      </div>
      {message && <p className="text-sm text-due">{message}</p>}
    </div>
  )
}

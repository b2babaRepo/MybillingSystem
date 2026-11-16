import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { PendingDot } from '../../components/ui'
import { formatDateTime, formatRupees } from '../../lib/format'
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
    <section className="flex flex-col gap-3 rounded-[18px] bg-white p-4">
      <div className="flex items-start justify-between gap-2">
        <span className="flex flex-col">
          <span className="text-[17px] font-bold">{order.shopName}</span>
          <span className="text-[13px] text-muted">
            {t(`plan.${order.listKind}List`)} · {t('plan.itemsCount', { count: order.lines.length })} · {formatDateTime(order.createdAt)}
          </span>
        </span>
        <span className={`flex items-center gap-1.5 rounded-lg px-2 py-0.5 text-xs font-bold ${order.status === 'cancelled' ? 'bg-red-50 text-due' : 'bg-[#e0efec] text-teal-800'}`}>
          {order.pending && <PendingDot />}
          {order.pending ? t('plan.sending') : t(`plan.status.${order.status}`)}
        </span>
      </div>

      {order.status !== 'cancelled' && (
        <ol className="grid grid-cols-3 gap-1" aria-label={t(`plan.status.${order.status}`)}>
          {STEPS.map((step, index) => (
            <li key={step} className="flex flex-col gap-1">
              <span className={`h-1.5 rounded-full ${index <= reached && !order.pending ? 'bg-teal-700' : 'bg-[#edf2f1]'}`} />
              <span className={`text-xs ${index <= reached && !order.pending ? 'font-bold text-teal-800' : 'text-muted'}`}>{t(`plan.status.${step}`)}</span>
            </li>
          ))}
        </ol>
      )}

      {waiting.length > 0 && (
        <div className="flex flex-col gap-2 rounded-xl bg-amber-50 p-3 text-sm text-amber-950 ring-1 ring-amber-300">
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
                <button disabled={busy === l.id} onClick={() => void answer(l.id, true)} className="min-h-11 rounded-xl bg-teal-700 font-bold text-white disabled:opacity-40">{t('options.ok')}</button>
                <button disabled={busy === l.id} onClick={() => void answer(l.id, false)} className="min-h-11 rounded-xl border border-line font-semibold text-due disabled:opacity-40">{t('options.leaveOut')}</button>
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

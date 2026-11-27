import { btn } from '../lib/buttonStyles'
import { DeliveryRound } from '../components/DeliveryRound'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatDateTime } from '../lib/format'
import type { Order, OrderStatus } from '../lib/planner'
import { navigate } from '../router'

const icon = (d: string, size = 18) => <svg aria-hidden="true" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d={d} /></svg>
const initials = (name: string) => name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0]!.toUpperCase()).join('') || '#'

/**
 * Lists households and online shoppers sent to this shop, newest first, by stage. The stages are coloured cards with
 * their counts (as on the customer page), the chosen one filled. Needs the network.
 */
export function InboxScreen({ shopId, selectedId }: { shopId: string; selectedId?: string }) {
  const { t } = useTranslation()
  const [status, setStatus] = useState<OrderStatus>('placed')
  const fetchStage = (s: OrderStatus) => ({
    queryKey: [shopId, 'orders', s],
    queryFn: () => api<Order[]>(`/v1/orders?status=${s}`),
    refetchInterval: 120_000, // new orders arrive via the wait request in lib/orderAlerts.ts
  })
  const placed = useQuery(fetchStage('placed'))
  const packed = useQuery(fetchStage('packed'))
  const delivered = useQuery(fetchStage('delivered'))
  const orders = status === 'placed' ? placed : status === 'packed' ? packed : delivered

  const stages = [
    { status: 'placed' as const, label: t('inbox.new'), count: placed.data?.length, d: 'M3 13l3-9h12l3 9v7H3zM3 13h5l1 3h6l1-3h5', on: 'bg-[#0b5e4f] text-white shadow-lg shadow-[#0b5e4f]/30', off: 'bg-[#e3f2ee] text-[#0b5e4f] hover:bg-[#d2ebe4]' },
    { status: 'packed' as const, label: t('inbox.packed'), count: packed.data?.length, d: 'M3 7l9-4 9 4v10l-9 4-9-4zM3 7l9 4 9-4M12 11v10', on: 'bg-[#a1460a] text-white shadow-lg shadow-[#a1460a]/30', off: 'bg-[#fdf1e3] text-[#a1460a] hover:bg-[#fbe6cc]' },
    { status: 'delivered' as const, label: t('inbox.done'), count: delivered.data?.length, d: 'M4 12l5 5L20 6', on: 'bg-[#1d4ed8] text-white shadow-lg shadow-[#1d4ed8]/30', off: 'bg-[#e8effd] text-[#1d4ed8] hover:bg-[#dbe5fc]' },
  ]
  const shortcuts = [
    { to: '/sale-orders', label: t('saleOrder.link'), d: 'M9 3h6v3H9zM6 5h3M15 5h3v16H6V5M9 12h6M9 16h4', tone: 'bg-[#f1eafd] text-[#6d28d9] hover:bg-[#e7dcfb]' },
    { to: '/wa-inbox', label: t('waInbox.link'), d: 'M4 20l1.4-4A8 8 0 1112 20a8 8 0 01-4-1z', tone: 'bg-[#e7f8ee] text-[#0f6a3d] hover:bg-[#d3f2e0]' },
    { to: '/store', label: t('myStore.link'), d: 'M4 9l1-5h14l1 5M4 9h16v11H4zM9 20v-6h6v6', tone: 'bg-[#fde8ec] text-[#be123c] hover:bg-[#fbd5dd]' },
  ]

  return (
    <div className="flex flex-col gap-3 p-4">
      <h1 className="text-xl font-extrabold">{t('orders')}</h1>
      <DeliveryRound />

      <div role="tablist" aria-label={t('orders')} className="grid grid-cols-3 gap-2">
        {stages.map((s) => (
          <button key={s.status} role="tab" aria-selected={s.status === status} onClick={() => setStatus(s.status)}
            className={`flex min-h-[84px] flex-col justify-between rounded-[20px] p-3 text-left transition active:scale-[0.98] ${s.status === status ? s.on : s.off}`}>
            <span className={`flex h-[30px] w-[30px] items-center justify-center rounded-full ${s.status === status ? 'bg-white/20' : 'bg-white'}`}>{icon(s.d)}</span>
            <span className="flex items-baseline justify-between gap-1"><b className="text-[15px]">{s.label}</b><b className="text-xl">{s.count ?? '…'}</b></span>
          </button>
        ))}
      </div>

      <div className="grid grid-cols-3 gap-2">
        {shortcuts.map((x) => (
          <button key={x.to} onClick={() => navigate(x.to)} className={`flex min-h-12 items-center gap-2 rounded-2xl px-3 text-left text-sm font-bold transition active:scale-[0.98] ${x.tone}`}>
            <span aria-hidden className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-white">{icon(x.d, 16)}</span>
            <span className="leading-tight">{x.label}</span>
          </button>
        ))}
      </div>

      {orders.isError && <p className="text-due">{orders.error.message}</p>}
      {orders.data?.length === 0 && (
        <div className="flex flex-col items-center gap-2 rounded-card bg-white px-4 py-10 text-center shadow-sm">
          <span aria-hidden className="flex h-14 w-14 items-center justify-center rounded-full bg-brand-soft text-brand">{icon('M3 13l3-9h12l3 9v7H3zM3 13h5l1 3h6l1-3h5', 26)}</span>
          <p className="font-semibold">{t('inbox.empty')}</p>
          <p className="max-w-sm text-sm text-muted">{t('inbox3.emptyHint')}</p>
        </div>
      )}
      {orders.data?.map((order) => {
        const unsure = order.lines.filter((l) => l.aiConfidence && l.aiConfidence !== 'high').length
        return (
          <section key={order.id} onClick={() => navigate(`/o/${order.id}`)} aria-current={selectedId === order.id ? 'true' : undefined}
            className={`flex flex-col gap-3 rounded-card bg-white p-4 shadow-sm lg:cursor-pointer lg:hover:shadow-md ${selectedId === order.id ? 'ring-2 ring-brand' : ''}`}>
            <div className="flex items-start gap-3">
              <span aria-hidden className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-brand-soft font-bold text-brand">{initials(order.customerName)}</span>
              <span className="flex min-w-0 flex-1 flex-col">
                <span className="truncate text-[17px] font-bold">{order.customerName}</span>
                <span className="flex flex-wrap items-center gap-x-2 text-[13px] text-muted">
                  <span>{t('plan.itemsCount', { count: order.lines.length })}</span>
                  <span className="flex items-center gap-1">{icon(order.fulfilment === 'delivery' ? 'M2 6h13v11H2zM15 10h4l3 3v4h-7M6 20h.01M18 20h.01' : 'M4 9l1-5h14l1 5M4 9h16v11H4z', 14)}{order.fulfilment === 'delivery' ? t('plan.delivery') : t('plan.pickup')}</span>
                  <span>{order.payment === 'khata' ? t('plan.onKhata') : t(order.payment)}</span>
                </span>
              </span>
              <span className={`shrink-0 rounded-full px-2.5 py-0.5 text-xs font-bold ${order.source === 'store' ? 'bg-[#fde8ec] text-[#be123c]' : order.listKind === 'monthly' ? 'bg-[#e3f2ee] text-[#0b5e4f]' : 'bg-[#e8effd] text-[#1d4ed8]'}`}>
                {order.source === 'store' ? t('myStore.online') : t(`plan.${order.listKind}List`)}
              </span>
            </div>
            {unsure > 0 && (
              <span className="flex w-fit items-center gap-1.5 rounded-full bg-warn-soft px-2.5 py-0.5 text-xs font-bold text-warn-ink">
                {icon('M12 3l10 18H2zM12 10v4M12 17h.01', 14)}{t('aiShop.inbox', { count: unsure })}
              </span>
            )}
            <p className="rounded-xl bg-[#f6faf9] px-3 py-2 text-sm leading-snug">
              {order.lines.slice(0, 5).map((l) => `${l.name} ${l.quantity} ${t(`units.${l.unit}`, { count: l.quantity })}`).join(', ')}
              {order.lines.length > 5 && ` + ${order.lines.length - 5}`}
            </p>
            {order.address && <p className="flex items-start gap-1.5 text-sm"><span className="mt-0.5 text-muted">{icon('M12 21s7-6 7-11a7 7 0 10-14 0c0 5 7 11 7 11zM12 8a2 2 0 100 4 2 2 0 000-4', 16)}</span>{order.address}</p>}
            <div className="flex justify-between gap-2 text-sm text-muted">
              <span className="flex items-center gap-1.5">{icon('M12 3a9 9 0 100 18 9 9 0 000-18zM12 7v5l3 2', 14)}{order.slot ?? formatDateTime(order.createdAt)}</span>
              {order.note && <span className="truncate pl-2">“{order.note}”</span>}
            </div>
            <button onClick={(e) => { e.stopPropagation(); navigate(`/o/${order.id}`) }} className={`h-12 rounded-xl lg:h-10 ${btn.solid}`}>
              {order.status === 'placed' ? t('inbox.startPacking') : t('inbox.open')}
            </button>
          </section>
        )
      })}
    </div>
  )
}

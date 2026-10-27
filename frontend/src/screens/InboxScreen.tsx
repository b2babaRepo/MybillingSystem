import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatDateTime } from '../lib/format'
import type { Order, OrderStatus } from '../lib/planner'
import { navigate } from '../router'

/** Mockup 7: lists households sent to this shop, newest first, by stage. Needs the network. */
export function InboxScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const [status, setStatus] = useState<OrderStatus>('placed')
  const orders = useQuery({
    queryKey: [shopId, 'orders', status],
    queryFn: () => api<Order[]>(`/v1/orders?status=${status}`),
    refetchInterval: 30_000,
  })

  const tabs: { status: OrderStatus; label: string }[] = [
    { status: 'placed', label: t('inbox.new') },
    { status: 'packed', label: t('inbox.packed') },
    { status: 'delivered', label: t('inbox.done') },
  ]

  return (
    <div className="flex flex-col gap-3 p-4">
      <div role="tablist" className="grid grid-cols-3 rounded-[14px] bg-white p-1">
        {tabs.map((tab) => (
          <button
            key={tab.status}
            role="tab"
            aria-selected={tab.status === status}
            onClick={() => setStatus(tab.status)}
            className={`h-11 rounded-[10px] font-semibold ${tab.status === status ? 'bg-teal-700 text-white' : 'text-muted'}`}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {orders.isError && <p className="text-due">{orders.error.message}</p>}
      {orders.data?.length === 0 && <p className="py-8 text-center text-muted">{t('inbox.empty')}</p>}
      {orders.data?.map((order) => (
        <section key={order.id} className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
          <div className="flex items-start justify-between gap-2">
            <span className="flex flex-col">
              <span className="text-[17px] font-bold">{order.customerName}</span>
              <span className="text-[13px] text-muted">
                {t('plan.itemsCount', { count: order.lines.length })} · {order.fulfilment === 'delivery' ? t('plan.delivery') : t('plan.pickup')} ·{' '}
                {order.payment === 'khata' ? t('plan.onKhata') : t(order.payment)}
              </span>
            </span>
            <span className={`rounded-lg px-2 py-0.5 text-xs font-bold ${order.source === 'store' ? 'bg-orange-50 text-orange-800' : order.listKind === 'monthly' ? 'bg-[#e0efec] text-teal-800' : 'bg-blue-50 text-blue-800'}`}>
              {order.source === 'store' ? t('myStore.online') : t(`plan.${order.listKind}List`)}
            </span>
          </div>
          <p className="text-sm leading-snug">
            {order.lines.slice(0, 5).map((l) => `${l.name} ${l.quantity} ${t(`units.${l.unit}`)}`).join(', ')}
            {order.lines.length > 5 && ` + ${order.lines.length - 5}`}
          </p>
          {order.address && <p className="text-sm">{order.address}</p>}
          <div className="flex justify-between text-sm text-muted">
            <span>{order.slot ?? formatDateTime(order.createdAt)}</span>
            {order.note && <span className="truncate pl-2">“{order.note}”</span>}
          </div>
          <button onClick={() => navigate(`/o/${order.id}`)} className="h-12 rounded-2xl bg-teal-700 font-bold text-white">
            {order.status === 'placed' ? t('inbox.startPacking') : t('inbox.open')}
          </button>
        </section>
      ))}
    </div>
  )
}

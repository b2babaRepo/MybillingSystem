import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { confirmAsk } from '../components/Ask'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import type { Unit } from '../lib/types'
import { navigate } from '../router'

interface Line { productId: string | null; name: string; quantity: number; unit: Unit; unitPrice: number; delivered: number; pending: number; inStock: number | null; shortBy: number }
interface SaleOrder { id: string; number: number; customerId: string | null; party: string | null; lines: Line[]; total: number; pendingValue: number; dueOn: string | null; note: string | null; state: 'open' | 'part' | 'delivered' | 'closed' | 'cancelled'; bills: number; createdAt: string }

/** The bill screen reads this once and fills the cart with what the order still has to deliver. */
export const SALE_ORDER_KEY = 'gk-sale-order'

const TONE: Record<SaleOrder['state'], string> = {
  open: 'bg-brand-soft text-brand-strong', part: 'bg-warn-soft text-warn', delivered: 'bg-emerald-50 text-emerald-800', closed: 'bg-slate-100 text-muted', cancelled: 'bg-due-soft text-due',
}

/**
 * Customers' orders booked ahead: what each one is still owed, what is on the shelf for it (oldest order first),
 * and how much it is short. Deliver makes a bill of what is still to go; part deliveries count down.
 */
export function SaleOrdersScreen() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [all, setAll] = useState(false)
  const orders = useQuery({ queryKey: ['sale-orders', all], queryFn: () => api<SaleOrder[]>(`/v1/sale-orders${all ? '' : '?open=true'}`) })
  const [message, setMessage] = useState<string | null>(null)

  function deliver(o: SaleOrder) {
    const lines = o.lines.filter((l) => l.pending > 0).map((l) => ({ productId: l.productId, name: l.name, quantity: l.pending, unit: l.unit, unitPrice: l.unitPrice }))
    try { sessionStorage.setItem(SALE_ORDER_KEY, JSON.stringify({ id: o.id, number: o.number, customerId: o.customerId, lines })) } catch { /* no storage */ }
    navigate('/bill')
  }

  async function finish(o: SaleOrder, how: 'close' | 'cancel') {
    if (!(await confirmAsk(t(how === 'close' ? 'saleOrder.closeConfirm' : 'saleOrder.cancelConfirm', { number: o.number })))) return
    setMessage(null)
    try {
      await api(`/v1/sale-orders/${o.id}/${how}`, 'POST', {})
      await queryClient.invalidateQueries({ queryKey: ['sale-orders'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  const data = orders.data ?? []
  const shortOrders = data.filter((o) => o.lines.some((l) => l.shortBy > 0)).length
  return (
    <div className="flex flex-col gap-3 p-4">
      <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
        <h1 className="text-xl font-bold">📦 {t('saleOrder.title')}</h1>
        <p className="text-sm text-muted">{t('saleOrder.hint')}</p>
        <div className="grid grid-cols-2 gap-1 rounded-2xl bg-[#edf2f1] p-1 text-sm">
          {([false, true] as const).map((v) => (
            <button key={String(v)} onClick={() => setAll(v)} aria-pressed={all === v} className={`h-10 rounded-xl font-semibold transition ${all === v ? 'bg-white text-brand-strong shadow-sm ring-1 ring-brand/20' : 'text-muted hover:text-ink'}`}>{t(v ? 'saleOrder.all' : 'saleOrder.open')}</button>
          ))}
        </div>
        {!all && data.length > 0 && (
          <p className="text-sm">{t('saleOrder.summary', { count: data.length, value: formatRupees(data.reduce((s, o) => s + o.pendingValue, 0)) })}{shortOrders > 0 && <b className="text-due"> · {t('saleOrder.shortOrders', { count: shortOrders })}</b>}</p>
        )}
      </section>
      {message && <p className="text-sm text-due">{message}</p>}
      {orders.data && data.length === 0 && <p className="py-6 text-center text-muted">{t('saleOrder.none')}</p>}
      {data.map((o) => (
        <section key={o.id} className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
          <div className="flex flex-wrap items-start justify-between gap-2">
            <span className="flex flex-col">
              <b>{t('saleOrder.number', { number: o.number })} · {o.party ?? '—'}</b>
              <span className="text-xs text-muted">{new Date(o.createdAt).toLocaleDateString()}{o.dueOn && ` · ${t('saleOrder.due', { date: o.dueOn })}`}{o.bills > 0 && ` · ${t('saleOrder.bills', { count: o.bills })}`}</span>
            </span>
            <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${TONE[o.state]}`}>{t(`saleOrder.state.${o.state}`)}</span>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[420px] text-sm">
              <thead><tr className="text-right text-xs text-muted"><th className="py-1 text-left">{t('items2.colName')}</th><th>{t('saleOrder.ordered')}</th><th>{t('saleOrder.delivered')}</th><th>{t('saleOrder.toGo')}</th><th>{t('saleOrder.inStock')}</th></tr></thead>
              <tbody>
                {o.lines.map((l, i) => (
                  <tr key={i} className="border-t border-[#edf2f1] text-right">
                    <td className="py-1.5 text-left font-semibold">{l.name}{l.shortBy > 0 && <span className="ml-1 rounded-full bg-due-soft px-1.5 text-[11px] font-semibold text-due">{t('saleOrder.short', { qty: l.shortBy })}</span>}</td>
                    <td>{l.quantity}</td><td className="text-jama">{l.delivered || ''}</td><td className="font-bold">{l.pending || '—'}</td><td className="text-muted">{l.inStock ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {o.note && <p className="text-xs text-muted">{o.note}</p>}
          <p className="text-sm">{t('saleOrder.value', { total: formatRupees(o.total), pending: formatRupees(o.pendingValue) })}</p>
          {(o.state === 'open' || o.state === 'part') && (
            <div className="flex flex-wrap gap-2">
              <button onClick={() => deliver(o)} className="h-11 flex-1 rounded-xl bg-brand px-4 font-bold text-white shadow-sm hover:bg-brand-strong">🚚 {t('saleOrder.deliver')}</button>
              {o.state === 'part'
                ? <button onClick={() => void finish(o, 'close')} className="h-11 rounded-xl border border-line px-3 font-semibold hover:bg-[#f6faf9]">{t('saleOrder.close')}</button>
                : <button onClick={() => void finish(o, 'cancel')} className="h-11 rounded-xl border border-line px-3 font-semibold text-due hover:bg-due-soft">{t('saleOrder.cancel')}</button>}
            </div>
          )}
        </section>
      ))}
    </div>
  )
}

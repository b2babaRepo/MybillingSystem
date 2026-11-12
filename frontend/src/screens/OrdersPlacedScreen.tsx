import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'
import { isLate, type PurchaseOrder } from './SupplierOrders'

/** The suppliers screen opens this supplier once, when it is set. */
export const OPEN_SUPPLIER_KEY = 'gk-open-supplier'

type Filter = 'open' | 'late' | 'closed' | 'all'

const value = (o: PurchaseOrder) => o.lines.reduce((s, l) => s + (l.rate ?? 0) * l.quantity, 0)
const state = (o: PurchaseOrder) => o.status === 'open' ? (o.lines.some((l) => l.received > 0) ? 'part' : 'open') : o.status
const TONE: Record<string, string> = { open: 'bg-teal-50 text-teal-800', part: 'bg-amber-50 text-amber-800', closed: 'bg-slate-100 text-muted', cancelled: 'bg-red-50 text-due' }

/**
 * Every order to every supplier in one list: what came of each, what is still to come and what is late.
 * The search answers "who did I order sugar from?".
 */
export function OrdersPlacedScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const [filter, setFilter] = useState<Filter>('open')
  const [search, setSearch] = useState('')
  const orders = useQuery({ queryKey: [shopId, 'suppliers', 'orders', 'all'], queryFn: () => api<PurchaseOrder[]>('/v1/suppliers/orders?all=true') })
  const all = orders.data ?? []
  const open = all.filter((o) => o.status === 'open')
  const late = open.filter(isLate)
  const q = search.trim().toLowerCase()
  const shown = all
    .filter((o) => filter === 'all' || (filter === 'open' ? o.status === 'open' : filter === 'late' ? isLate(o) : o.status !== 'open'))
    .filter((o) => !q || o.lines.some((l) => l.name.toLowerCase().includes(q)) || o.supplierName.toLowerCase().includes(q))
    .sort((a, b) => Number(isLate(b)) - Number(isLate(a)) || b.createdAt.localeCompare(a.createdAt))

  function openSupplier(id: string) {
    try { sessionStorage.setItem(OPEN_SUPPLIER_KEY, id) } catch { /* no storage */ }
    navigate('/suppliers')
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm">
        <h1 className="text-xl font-bold">🧾 {t('po.title')}</h1>
        <p className="text-sm text-muted">{t('po.hint')}</p>
        {open.length > 0 && (
          <p className="text-sm font-semibold">
            {t('po.summary', { count: open.length, types: new Set(open.flatMap((o) => o.lines.filter((l) => l.received < l.quantity).map((l) => l.productId))).size, value: formatRupees(open.reduce((s, o) => s + value(o), 0)) })}
            {late.length > 0 && <span className="text-due"> · {t('po.lateCount', { count: late.length })}</span>}
          </p>
        )}
        <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('po.search')} aria-label={t('po.search')} className="h-12 rounded-xl border border-line bg-white px-3" />
        <div className="flex flex-wrap gap-1">
          {(['open', 'late', 'closed', 'all'] as const).map((f) => (
            <button key={f} onClick={() => setFilter(f)} aria-pressed={filter === f} className={`h-9 rounded-full px-3 text-sm font-semibold ${filter === f ? 'bg-teal-700 text-white' : 'bg-[#edf2f1] text-muted'}`}>
              {t(`po.f.${f}`)}{f === 'late' && late.length > 0 ? ` (${late.length})` : ''}
            </button>
          ))}
        </div>
      </section>
      {orders.isError && <p className="text-sm text-due">{t('error')}</p>}
      {orders.data && shown.length === 0 && <p className="py-6 text-center text-muted">{t('po.none')}</p>}
      {shown.map((o) => {
        const ordered = o.lines.reduce((s, l) => s + l.quantity, 0)
        const came = o.lines.reduce((s, l) => s + Math.min(l.received, l.quantity), 0)
        const s = state(o)
        return (
          <button key={o.id} onClick={() => openSupplier(o.supplierId)} className="flex flex-col gap-2 rounded-[18px] bg-white p-4 text-left shadow-sm hover:bg-[#f6faf9]">
            <span className="flex w-full items-start justify-between gap-2">
              <span className="flex flex-col">
                <b>{o.supplierName} · {t('supplier2.orderNo', { number: o.number })}</b>
                <span className={`text-xs ${isLate(o) ? 'font-semibold text-due' : 'text-muted'}`}>
                  {new Date(o.createdAt).toLocaleDateString('en-IN', { day: 'numeric', month: 'short' })}
                  {o.expectedOn && ` · ${t(isLate(o) ? 'po.late' : 'po.expected', { date: new Date(o.expectedOn).toLocaleDateString('en-IN', { day: 'numeric', month: 'short' }) })}`}
                  {` · ${t('po.types', { count: o.lines.length })}`}{value(o) > 0 && ` · ${formatRupees(value(o))}`}
                </span>
              </span>
              <span className={`shrink-0 rounded-full px-2.5 py-0.5 text-xs font-semibold ${TONE[s]}`}>{t(`po.state.${s}`)}</span>
            </span>
            <span className="h-2 w-full overflow-hidden rounded-full bg-[#edf2f1]"><span className="block h-full rounded-full bg-teal-600" style={{ width: `${ordered > 0 ? (came / ordered) * 100 : 0}%` }} /></span>
            <span className="text-xs text-muted">{t('po.progress', { came, ordered })}{o.pending > 0 && o.status === 'open' ? ` · ${t('supplier2.stillToCome', { qty: o.pending })}` : ''}</span>
            {q && <span className="text-xs">{o.lines.filter((l) => l.name.toLowerCase().includes(q)).map((l) => `${l.name}: ${t('supplier2.cameOf', { came: l.received, ordered: l.quantity })}`).join(' · ')}</span>}
            {o.note && <span className="text-xs text-muted">{o.note}</span>}
          </button>
        )
      })}
    </div>
  )
}

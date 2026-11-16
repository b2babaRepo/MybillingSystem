import { btn } from '../components/ui'
import { shownLocale } from '../i18n'
import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'
import { isLate, type PurchaseOrder } from './SupplierOrders'

/** The suppliers screen opens this supplier once, when it is set. */
export const OPEN_SUPPLIER_KEY = 'gk-open-supplier'
/** Set with OPEN_SUPPLIER_KEY: the supplier page opens with a new order ready to fill. */
export const NEW_ORDER_KEY = 'gk-new-order'

type Filter = 'open' | 'late' | 'closed' | 'all'

const value = (o: PurchaseOrder) => o.lines.reduce((s, l) => s + (l.rate ?? 0) * l.quantity, 0)
const state = (o: PurchaseOrder) => o.status === 'open' ? (o.lines.some((l) => l.received > 0) ? 'part' : 'open') : o.status
// Where an open order has got to, from Goods to come (placed, sent, confirmed, dispatched).
const stageLabel = (o: PurchaseOrder) => (o.status === 'open' && o.stage && !['placed', 'part'].includes(o.stage) ? o.stage : null)
const TONE: Record<string, string> = { open: 'bg-brand-soft text-brand-strong', part: 'bg-warn-soft text-warn', closed: 'bg-slate-100 text-muted', cancelled: 'bg-due-soft text-due', draft: 'bg-amber-100 text-warn' }

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
    .filter((o) => filter === 'all' || (filter === 'open' ? o.status === 'open' : filter === 'late' ? isLate(o) : o.status !== 'open' && o.status !== 'draft'))
    .filter((o) => !q || o.lines.some((l) => l.name.toLowerCase().includes(q)) || o.supplierName.toLowerCase().includes(q))
    .sort((a, b) => Number(isLate(b)) - Number(isLate(a)) || b.createdAt.localeCompare(a.createdAt))

  const suppliers = (useQuery({ queryKey: [shopId, 'suppliers'], queryFn: () => api<{ id: string; name: string; isActive?: boolean }[]>('/v1/suppliers') }).data ?? []).filter((s) => s.isActive !== false)
  const [choosing, setChoosing] = useState(false)
  function newOrder(id: string) {
    try { sessionStorage.setItem(NEW_ORDER_KEY, '1') } catch { /* no storage */ }
    openSupplier(id)
  }

  function openSupplier(id: string) {
    try { sessionStorage.setItem(OPEN_SUPPLIER_KEY, id) } catch { /* no storage */ }
    navigate('/suppliers')
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
        <div className="flex items-center justify-between gap-2">
          <h1 className="text-xl font-bold">🧾 {t('po.title')}</h1>
          <button onClick={() => (suppliers.length === 0 ? navigate('/suppliers') : setChoosing(!choosing))} className={`min-h-11 rounded-xl px-3 ${btn.solid}`}>+ {t('supplier2.newOrder')}</button>
        </div>
        {choosing && (
          <label className="flex flex-col gap-1 text-sm">{t('po.pickSupplier')}
            <select defaultValue="" onChange={(e) => e.target.value && newOrder(e.target.value)} className="h-12 rounded-xl border border-line bg-white px-3">
              <option value="" disabled>{t('po.pickSupplier')}</option>
              {suppliers.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
            </select>
          </label>
        )}
        <p className="text-sm text-muted">{t('po.hint')}</p>
        {open.length > 0 && (
          <p className="text-sm font-semibold">
            {t('po.summary', { count: open.length, types: new Set(open.flatMap((o) => o.lines.filter((l) => l.received < l.quantity).map((l) => l.productId))).size, value: formatRupees(open.reduce((s, o) => s + value(o), 0)) })}
            {late.length > 0 && <span className="text-due"> · {t('po.lateCount', { count: late.length })}</span>}
          </p>
        )}
        {late.length > 0 && <button onClick={() => navigate('/goods-to-come')} className={`self-start text-sm ${btn.danger}`}>⏳ {t('gtc.chaseLate', { count: late.length })}</button>}
        <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('po.search')} aria-label={t('po.search')} className="h-12 rounded-xl border border-line bg-white px-3" />
        <div className="flex flex-wrap gap-1">
          {(['open', 'late', 'closed', 'all'] as const).map((f) => (
            <button key={f} onClick={() => setFilter(f)} aria-pressed={filter === f} className={`h-9 rounded-full px-3 text-sm font-semibold ${filter === f ? 'bg-brand text-white' : 'bg-[#edf2f1] text-muted'}`}>
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
          <button key={o.id} onClick={() => openSupplier(o.supplierId)} className="flex flex-col gap-2 rounded-card bg-white p-4 text-left shadow-sm hover:bg-[#f6faf9]">
            <span className="flex w-full items-start justify-between gap-2">
              <span className="flex flex-col">
                <b>{o.supplierName} · {t('supplier2.orderNo', { number: o.number })}</b>
                <span className={`text-xs ${isLate(o) ? 'font-semibold text-due' : 'text-muted'}`}>
                  {new Date(o.createdAt).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })}
                  {o.expectedOn && ` · ${t(isLate(o) ? 'po.late' : 'po.expected', { date: new Date(o.expectedOn).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' }) })}`}
                  {` · ${t('po.types', { count: o.lines.length })}`}{value(o) > 0 && ` · ${formatRupees(value(o))}`}
                </span>
              </span>
              <span className="flex shrink-0 flex-col items-end gap-1">
                <span className={`rounded-full px-2.5 py-0.5 text-xs font-semibold ${TONE[s]}`}>{t(`po.state.${s}`)}</span>
                {stageLabel(o) && <span className="rounded-full bg-sky-50 px-2 py-0.5 text-[11px] font-semibold text-sky-800">{t(`gtc.stage.${stageLabel(o)}`)}</span>}
              </span>
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

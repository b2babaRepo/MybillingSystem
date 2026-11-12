import { Fragment, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { pct } from '../components/SupplierPanels'
import { api } from '../lib/api'
import { orderedNames, useCategories } from '../lib/categories'
import { saveCsv } from '../lib/csv'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'

interface Cell { supplierId: string; ordered: number; received: number }
interface Row { productId: string; name: string; category: string | null; unit: string; cells: Cell[]; received: number; pending: number }
interface Grid { from: string; to: string; suppliers: { id: string; name: string }[]; rows: Row[] }
interface Arrival { id: string; at: string; supplierId: string; supplier: string; received: number; damaged: number; rate: number | null; receiptNo: number | null; billNo: string | null; extra: boolean }
interface Score { supplierId: string; name: string; orders: number; ordered: number; received: number; fillRate: number | null; linesDue: number; onTimeRate: number | null; damageRate: number | null; leadDays: number | null; rateChange: number | null; valueReceived: number; deliveries: number }

const iso = (d: Date) => new Date(d.getTime() - d.getTimezoneOffset() * 60_000).toISOString().slice(0, 10)
const monthStart = () => { const d = new Date(); return iso(new Date(d.getFullYear(), d.getMonth(), 1)) }
const tone = (n: number | null, good: number, ok: number) => (n == null ? '' : n >= good ? 'text-emerald-700' : n >= ok ? 'text-amber-700' : 'text-due')

/**
 * Received by supplier: items down the side, suppliers across, "received of ordered" in each cell for the dates;
 * tapping a cell lists the deliveries behind it. The scorecard tab rates each supplier over the last 90 days.
 */
export function ReceivedBySupplierScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const [tab, setTab] = useState<'grid' | 'score'>('grid')
  const [range, setRange] = useState({ from: monthStart(), to: iso(new Date()) })
  const [category, setCategory] = useState('')
  const [open, setOpen] = useState<{ productId: string; supplierId: string } | null>(null)
  const categoryRows = useCategories(shopId).data
  const grid = useQuery({ queryKey: [shopId, 'received-by-supplier', range.from, range.to], queryFn: () => api<Grid>(`/v1/reports/received-by-supplier?from=${range.from}&to=${range.to}`), enabled: tab === 'grid' })
  const score = useQuery({ queryKey: [shopId, 'supplier-scorecard'], queryFn: () => api<Score[]>('/v1/reports/supplier-scorecard'), enabled: tab === 'score' })
  const deliveries = useQuery({
    queryKey: [shopId, 'arrivals', open?.productId, open?.supplierId],
    queryFn: () => api<Arrival[]>(`/v1/goods-in/arrivals?productId=${open!.productId}&supplierId=${open!.supplierId}`), enabled: !!open,
  })
  const g = grid.data
  const rows = (g?.rows ?? []).filter((r) => !category || r.category === category)
  const cell = (r: Row, s: string) => r.cells.find((c) => c.supplierId === s)

  function download() {
    if (!g) return
    saveCsv(`received-by-supplier-${g.from}-to-${g.to}.csv`, [
      [t('items2.colName'), t('stock.category'), ...g.suppliers.map((s) => s.name), t('rs.total'), t('rs.toCome')],
      ...rows.map((r) => [r.name, r.category ?? '', ...g.suppliers.map((s) => { const c = cell(r, s.id); return c ? `${c.received}/${c.ordered}` : '' }), r.received, r.pending]),
    ])
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm">
        <div className="flex items-center justify-between gap-2">
          <h1 className="text-xl font-bold">🚚 {t('rs.title')}</h1>
          {tab === 'grid' && <button disabled={!g} onClick={download} className="h-10 rounded-xl border border-line px-3 text-sm font-semibold hover:bg-[#f6faf9] disabled:opacity-40">⬇ {t('sl.csv')}</button>}
        </div>
        <div className="grid grid-cols-2 gap-1 rounded-2xl bg-[#edf2f1] p-1 text-sm">
          {(['grid', 'score'] as const).map((k) => (
            <button key={k} onClick={() => setTab(k)} aria-pressed={tab === k} className={`h-10 rounded-xl font-semibold ${tab === k ? 'bg-white text-teal-800 shadow-sm ring-1 ring-teal-700/20' : 'text-muted'}`}>{t(`rs.tab.${k}`)}</button>
          ))}
        </div>
        {tab === 'grid' ? (
          <>
            <p className="text-sm text-muted">{t('rs.hint')}</p>
            <div className="flex flex-wrap gap-2 text-sm">
              <label className="flex items-center gap-1.5">{t('sl.from')}<input type="date" value={range.from} max={range.to} onChange={(e) => e.target.value && setRange({ ...range, from: e.target.value })} className="h-10 rounded-xl border border-line bg-white px-2" /></label>
              <label className="flex items-center gap-1.5">{t('sl.to')}<input type="date" value={range.to} min={range.from} onChange={(e) => e.target.value && setRange({ ...range, to: e.target.value })} className="h-10 rounded-xl border border-line bg-white px-2" /></label>
              <select aria-label={t('stock.category')} value={category} onChange={(e) => setCategory(e.target.value)} className="h-10 rounded-xl border border-line bg-white px-2">
                <option value="">{t('cat.allCategories')}</option>
                {orderedNames(categoryRows, (g?.rows ?? []).map((r) => r.category)).map((c) => <option key={c} value={c}>{c}</option>)}
              </select>
            </div>
          </>
        ) : <p className="text-sm text-muted">{t('rs.scoreHint')}</p>}
      </section>

      {tab === 'grid' && g && (rows.length === 0 ? <p className="py-6 text-center text-muted">{t('rs.none')}</p> : (
        <div className="overflow-x-auto rounded-[18px] bg-white shadow-sm">
          <table className="w-full min-w-[480px] text-sm">
            <thead>
              <tr className="text-right text-xs text-muted">
                <th className="sticky left-0 bg-white p-2 text-left">{t('items2.colName')}</th>
                {g.suppliers.map((s) => <th key={s.id} className="p-2">{s.name}</th>)}
                <th className="p-2">{t('rs.total')}</th><th className="p-2">{t('rs.toCome')}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <Fragment key={r.productId}>
                  <tr className="border-t border-[#edf2f1] text-right">
                    <td className="sticky left-0 bg-white p-2 text-left"><button onClick={() => navigate(`/p/${r.productId}`)} className="font-semibold hover:underline">{r.name}</button></td>
                    {g.suppliers.map((s) => {
                      const c = cell(r, s.id)
                      const short = c && c.ordered > 0 && c.received < c.ordered
                      return (
                        <td key={s.id} className="p-2">
                          {c ? (
                            <button onClick={() => setOpen(open?.productId === r.productId && open.supplierId === s.id ? null : { productId: r.productId, supplierId: s.id })}
                              className={`rounded-lg px-1.5 py-0.5 hover:bg-[#edf2f1] ${short ? 'text-amber-800' : ''}`}>
                              {c.ordered > 0 ? t('rs.ofOrdered', { received: c.received, ordered: c.ordered }) : c.received}
                            </button>
                          ) : <span className="text-muted">·</span>}
                        </td>
                      )
                    })}
                    <td className="p-2 font-bold">{r.received}</td>
                    <td className={`p-2 ${r.pending > 0 ? 'font-semibold text-amber-800' : 'text-muted'}`}>{r.pending || '—'}</td>
                  </tr>
                  {open?.productId === r.productId && (
                    <tr><td colSpan={g.suppliers.length + 3} className="bg-[#f6faf9] p-2">
                      {deliveries.data?.length === 0 && <p className="text-xs text-muted">{t('rs.noDeliveries')}</p>}
                      {deliveries.data?.map((a) => (
                        <p key={a.id} className="text-xs">
                          {new Date(a.at).toLocaleDateString('en-IN', { day: 'numeric', month: 'short' })} · {a.supplier} · <b>{a.received} {t(`units.${r.unit}`)}</b>
                          {a.damaged > 0 && ` (${t('rs.damaged', { qty: a.damaged })})`}{a.rate != null && ` · ${formatRupees(a.rate)}`}
                          {a.receiptNo != null && ` · ${t('rs.receiptNo', { number: a.receiptNo })}`}{a.billNo && ` · ${t('rs.billNo', { no: a.billNo })}`}{a.extra && ` · ${t('rs.extra')}`}
                        </p>
                      ))}
                    </td></tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
      ))}

      {tab === 'score' && score.data && (score.data.length === 0 ? <p className="py-6 text-center text-muted">{t('rs.none')}</p> : score.data.map((s) => (
        <section key={s.supplierId} className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm">
          <div className="flex items-start justify-between gap-2">
            <b>{s.name}</b>
            <span className="text-sm font-semibold">{formatRupees(s.valueReceived)}</span>
          </div>
          <div className="grid grid-cols-2 gap-2 text-sm sm:grid-cols-5">
            <Metric label={t('rs.fill')} value={pct(s.fillRate)} cls={tone(s.fillRate, 0.95, 0.85)} hint={t('rs.fillHint', { received: s.received, ordered: s.ordered })} />
            <Metric label={t('rs.onTime')} value={pct(s.onTimeRate)} cls={tone(s.onTimeRate, 0.9, 0.7)} hint={t('rs.onTimeHint', { count: s.linesDue })} />
            <Metric label={t('rs.damage')} value={pct(s.damageRate)} cls={s.damageRate == null ? '' : s.damageRate <= 0.01 ? 'text-emerald-700' : s.damageRate <= 0.03 ? 'text-amber-700' : 'text-due'} />
            <Metric label={t('rs.lead')} value={s.leadDays == null ? '—' : t('orderToday.lead', { count: s.leadDays })} />
            <Metric label={t('rs.rateChange')} value={s.rateChange == null ? '—' : `${s.rateChange > 0 ? '+' : ''}${Math.round(s.rateChange * 1000) / 10}%`} cls={s.rateChange == null ? '' : s.rateChange <= 0 ? 'text-emerald-700' : 'text-due'} />
          </div>
          <span className="text-xs text-muted">{t('rs.counts', { orders: s.orders, deliveries: s.deliveries })}</span>
        </section>
      )))}
      {tab === 'score' && <p className="px-1 text-xs text-muted">{t('rs.rateChangeHint')}</p>}
    </div>
  )
}

function Metric({ label, value, cls = '', hint }: { label: string; value: string; cls?: string; hint?: string }) {
  return (
    <span className="flex flex-col rounded-xl bg-[#f6faf9] p-2">
      <span className="text-xs text-muted">{label}</span>
      <b className={`text-base ${cls}`}>{value}</b>
      {hint && <span className="text-[11px] text-muted">{hint}</span>}
    </span>
  )
}

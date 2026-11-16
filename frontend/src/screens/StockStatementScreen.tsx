import { useItemName } from '../lib/useItemName'
import { orderedNames, useCategories } from '../lib/categories'
import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { normBarcode } from '../lib/barcode'
import { coverDays, coverText, coverTone } from '../lib/cover'
import { monthToDate, saveCsv } from '../lib/csv'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'
import type { Unit } from '../lib/types'

interface Line {
  productId: string; name: string; category: string | null; unit: Unit; opening: number; purchased: number; sold: number; otherIn: number; otherOut: number; closing: number
  costPrice: number | null; value: number | null; lost: number; bagSize?: number | null; closingBags?: number | null
  inShop: number; reserved: number; promised: number; coming: number; dailySales: number; lateOrder: boolean; barcode: string | null; hsn: string | null; suppliers: string[]
}
interface Statement { value: number; unvalued: number; lostValue: number; items: Line[] }
interface Row extends Line { free: number; cover: number | null }

type Col = 'opening' | 'bought' | 'sold' | 'lost' | 'closing' | 'value' | 'inShop' | 'promised' | 'free' | 'coming' | 'cover'
const COLS: Col[] = ['opening', 'bought', 'sold', 'lost', 'closing', 'value', 'inShop', 'promised', 'free', 'coming', 'cover']
const DEFAULT: Col[] = ['opening', 'bought', 'sold', 'closing', 'value', 'free', 'coming', 'cover']
type State = 'all' | 'low' | 'negative' | 'noSale' | 'late' | 'short'
const COLS_KEY = 'gk-statement-cols'

const sortValue = (r: Row, c: Col) => ({ opening: r.opening, bought: r.purchased, sold: r.sold, lost: r.lost, closing: r.closing, value: r.value ?? -1, inShop: r.inShop, promised: r.promised, free: r.free, coming: r.coming, cover: r.cover ?? Number.MAX_VALUE })[c]

/**
 * The stock statement: for each item, opening, bought, sold, lost and closing for the dates, with the closing value at
 * average cost (the item-wise stock account GST rule 56 asks for), and today's picture beside it: in the shop,
 * promised, free to sell, coming on order, and how long it lasts. Filter, sort by any column, pick the columns.
 */
export function StockStatementScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const nm = useItemName(shopId)
  const [range, setRange] = useState(monthToDate)
  const [search, setSearch] = useState('')
  const [category, setCategory] = useState('')
  const [supplier, setSupplier] = useState('')
  const [state, setState] = useState<State>('all')
  const [sort, setSort] = useState<{ col: Col; desc: boolean } | null>(null)
  const [choosing, setChoosing] = useState(false)
  const [cols, setCols] = useState<Col[]>(() => {
    try { const saved = JSON.parse(localStorage.getItem(COLS_KEY) ?? 'null') as Col[] | null; return saved?.filter((c) => COLS.includes(c)) ?? DEFAULT } catch { return DEFAULT }
  })
  const categoryRows = useCategories(shopId).data
  const suppliers = useQuery({ queryKey: [shopId, 'suppliers'], queryFn: () => api<{ id: string; name: string }[]>('/v1/suppliers'), retry: false }).data ?? []
  const statement = useQuery({ queryKey: ['stock-statement', range.from, range.to], queryFn: () => api<Statement>(`/v1/reports/stock?from=${range.from}&to=${range.to}`) })
  const d = statement.data

  function toggleCol(c: Col) {
    const next = cols.includes(c) ? cols.filter((x) => x !== c) : COLS.filter((x) => x === c || cols.includes(x))
    setCols(next)
    try { localStorage.setItem(COLS_KEY, JSON.stringify(next)) } catch { /* no storage */ }
  }

  const rows: Row[] = useMemo(() => (d?.items ?? []).map((i) => {
    const free = Math.round((i.inShop - i.reserved - i.promised) * 1000) / 1000
    return { ...i, free, cover: coverDays(free, i.dailySales) }
  }), [d])
  const q = search.trim().toLowerCase()
  const code = normBarcode(search)
  const shown = useMemo(() => rows.filter((i) =>
    (i.opening || i.closing || i.purchased || i.sold || i.otherIn || i.otherOut || i.promised || i.coming)
    && (!q || i.name.toLowerCase().includes(q) || (!!code && normBarcode(i.barcode) === code) || (i.hsn ?? '').startsWith(q))
    && (!category || i.category === category) && (!supplier || i.suppliers.includes(supplier))
    && (state === 'all' || (state === 'low' ? i.cover !== null && i.cover < 7 : state === 'negative' ? i.inShop < 0 : state === 'noSale' ? i.inShop > 0 && !i.dailySales : state === 'late' ? i.lateOrder : i.free < 0)),
  ), [rows, q, code, category, supplier, state])
  const groups = useMemo(() => {
    const map = new Map<string, Row[]>()
    for (const i of shown) map.set(i.category ?? '', [...(map.get(i.category ?? '') ?? []), i])
    if (sort) for (const [k, v] of map) map.set(k, [...v].sort((a, b) => (sortValue(a, sort.col) - sortValue(b, sort.col)) * (sort.desc ? -1 : 1)))
    // Groups in the shop's own category order; items with no category last.
    const order = orderedNames(categoryRows, [...map.keys()])
    return [...map.entries()].sort(([a], [b]) => (a === '' ? 1 : b === '' ? -1 : order.indexOf(a) - order.indexOf(b)))
  }, [shown, categoryRows, sort])

  // What an owner acts on: money tied up in more than three months of stock, and items with under a week left.
  const overStock = rows.filter((r) => r.cover !== null && r.cover > 90).reduce((s, r) => s + (r.value ?? 0), 0)
  const underWeek = rows.filter((r) => r.cover !== null && r.cover < 7).length
  const states: [State, number][] = [
    ['all', 0], ['low', underWeek], ['short', rows.filter((r) => r.free < 0).length], ['negative', rows.filter((r) => r.inShop < 0).length],
    ['noSale', rows.filter((r) => r.inShop > 0 && !r.dailySales).length], ['late', rows.filter((r) => r.lateOrder).length],
  ]

  function download() {
    if (!d) return
    saveCsv(`stock-statement-${range.from}-to-${range.to}.csv`, [
      [t('items2.colName'), t('items2.colCategory'), t('unit'), t('sl.opening'), t('sl.bought'), t('sl.sold'), t('sl.lost'), t('sl.otherIn'), t('sl.otherOut'), t('sl.closing'), t('stock.costPrice'), t('sl.value'),
        t('st.col.inShop'), t('st.col.promised'), t('st.col.free'), t('st.col.coming'), t('st.col.cover')],
      ...rows.map((i) => [i.name, i.category, t(`units.${i.unit}`), i.opening, i.purchased, i.sold, i.lost, i.otherIn, i.otherOut - i.lost, i.closing, i.costPrice, i.value,
        i.inShop, i.promised + i.reserved, i.free, i.coming, i.cover === null ? '' : Math.floor(i.cover)]),
      ['', '', '', '', '', '', '', '', '', '', t('sl.totalValue'), d.value],
    ])
  }

  const cell = (i: Row, c: Col) => {
    switch (c) {
      case 'opening': return <td key={c} className="px-2">{i.opening}</td>
      case 'bought': return <td key={c} className="px-2 text-jama">{i.purchased || ''}</td>
      case 'sold': return <td key={c} className="px-2 text-udhaar">{i.sold || ''}</td>
      case 'lost': return <td key={c} className="px-2 text-due">{i.lost || ''}</td>
      case 'closing': return <td key={c} className={`px-2 font-bold ${i.closing < 0 ? 'text-due' : ''}`}>{i.closing}{i.closingBags != null && <span className="block text-xs font-normal text-muted">{t('bag.inBags', { count: i.closingBags })}</span>}</td>
      case 'value': return <td key={c} className="px-2 font-semibold">{i.value !== null ? formatRupees(i.value) : '—'}</td>
      case 'inShop': return <td key={c} className={`px-2 ${i.inShop < 0 ? 'text-due' : ''}`}>{i.inShop}</td>
      case 'promised': return <td key={c} className="px-2 text-warn">{i.promised + i.reserved || ''}</td>
      case 'free': return <td key={c} className={`px-2 font-semibold ${i.free < 0 ? 'text-due' : ''}`}>{i.free < 0 ? t('st.short', { qty: -i.free }) : i.free}</td>
      case 'coming': return <td key={c} className="px-2">{i.coming ? <span className={i.lateOrder ? 'font-semibold text-due' : 'text-brand-strong'}>{i.coming}{i.lateOrder && ' ⏰'}</span> : ''}</td>
      case 'cover': return <td key={c} className={`px-3 text-xs font-semibold ${coverTone(i.cover)}`}>{coverText(t, i.cover)}</td>
    }
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      <section className="flex flex-col gap-3 rounded-card bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h1 className="text-xl font-bold">📊 {t('sl.statement')}</h1>
          <span className="flex gap-2">
            <button onClick={() => setChoosing(!choosing)} aria-pressed={choosing} className="h-10 rounded-xl border border-line px-3 text-sm font-semibold hover:bg-[#f6faf9]">☰ {t('st.columns')}</button>
            <button onClick={download} disabled={!d} className="h-10 rounded-xl border border-line px-3 text-sm font-semibold transition hover:bg-[#f6faf9] disabled:opacity-40">⬇ {t('sl.csv')}</button>
          </span>
        </div>
        <p className="text-sm text-muted">{t('st.hint')}</p>
        {choosing && (
          <div className="flex flex-wrap gap-1 rounded-xl bg-[#f6faf9] p-2">
            {COLS.map((c) => <button key={c} onClick={() => toggleCol(c)} aria-pressed={cols.includes(c)} className={`h-8 rounded-full px-3 text-xs font-semibold ${cols.includes(c) ? 'bg-brand text-white' : 'bg-white text-muted ring-1 ring-black/5'}`}>{t(`st.col.${c}`)}</button>)}
          </div>
        )}
        <div className="flex flex-wrap gap-2 text-sm">
          <label className="flex items-center gap-1.5">{t('sl.from')}<input type="date" value={range.from} max={range.to} onChange={(e) => e.target.value && setRange({ ...range, from: e.target.value })} className="h-10 rounded-xl border border-line bg-white px-2 outline-none focus:border-brand-line" /></label>
          <label className="flex items-center gap-1.5">{t('sl.to')}<input type="date" value={range.to} min={range.from} onChange={(e) => e.target.value && setRange({ ...range, to: e.target.value })} className="h-10 rounded-xl border border-line bg-white px-2 outline-none focus:border-brand-line" /></label>
        </div>
        {d && (
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            <span className="flex flex-col rounded-2xl bg-brand-soft p-3 text-brand-deep"><span className="text-xs">{t('sl.totalValue')}</span><b className="text-xl">{formatRupees(d.value)}</b></span>
            <span className="flex flex-col rounded-2xl bg-due-soft p-3 text-red-800"><span className="text-xs">{t('sl.lostValue')}</span><b className="text-xl">{formatRupees(d.lostValue)}</b></span>
            <span className="flex flex-col rounded-2xl bg-warn-soft p-3 text-warn-ink"><span className="text-xs">{t('st.overStock')}</span><b className="text-xl">{formatRupees(overStock)}</b></span>
            <button onClick={() => setState('low')} className="flex flex-col rounded-2xl bg-[#f6faf9] p-3 text-left"><span className="text-xs text-muted">{t('st.underWeek')}</span><b className="text-xl text-due">{underWeek}</b></button>
            {d.unvalued > 0 && <span className="col-span-2 text-xs font-semibold text-warn sm:col-span-4">{t('sl.unvalued', { count: d.unvalued })}</span>}
          </div>
        )}
        <div className="flex flex-wrap gap-1">
          {states.map(([s, n]) => (
            <button key={s} onClick={() => setState(s)} aria-pressed={state === s} className={`h-9 rounded-full px-3 text-sm font-semibold ${state === s ? 'bg-brand text-white' : 'bg-[#edf2f1] text-muted'}`}>{t(`st.state.${s}`)}{s !== 'all' && n > 0 ? ` · ${n}` : ''}</button>
          ))}
        </div>
        <div className="grid grid-cols-2 gap-2">
          <select aria-label={t('stock.category')} value={category} onChange={(e) => setCategory(e.target.value)} className="h-11 rounded-2xl border border-line bg-white px-3 text-sm">
            <option value="">{t('cat.allCategories')}</option>
            {orderedNames(categoryRows, (d?.items ?? []).map((i) => i.category)).map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
          <select aria-label={t('st.supplier')} value={supplier} onChange={(e) => setSupplier(e.target.value)} className="h-11 rounded-2xl border border-line bg-white px-3 text-sm">
            <option value="">{t('st.allSuppliers')}</option>
            {suppliers.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select>
        </div>
        <span className="relative block">
          <span aria-hidden className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-muted">🔍</span>
          <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('st.search')} aria-label={t('st.search')} className="h-11 w-full rounded-2xl border border-line bg-white pl-10 pr-3 outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
        </span>
      </section>
      {statement.isError && <p className="text-due">{t('error')}</p>}
      {d && groups.length === 0 && <p className="py-6 text-center text-muted">{t('st.none')}</p>}
      {groups.map(([category, lines]) => (
        <section key={category} className="overflow-x-auto rounded-card bg-white shadow-sm">
          <h2 className="bg-[#f6faf9] px-4 py-2 text-sm font-bold text-muted">{category || t('sl.noCategory')} · {formatRupees(lines.reduce((s, l) => s + (l.value ?? 0), 0))}</h2>
          <table className="w-full min-w-[640px] text-sm">
            <thead><tr className="border-b border-line text-right text-xs text-muted">
              <th className="px-3 py-2 text-left">{t('items2.colName')}</th>
              {cols.map((c) => (
                <th key={c} className="px-2">
                  <button onClick={() => setSort(sort?.col === c ? (sort.desc ? null : { col: c, desc: true }) : { col: c, desc: false })} className="font-semibold hover:text-ink">
                    {t(`st.col.${c}`)}{sort?.col === c ? (sort.desc ? ' ▼' : ' ▲') : ''}
                  </button>
                </th>
              ))}
            </tr></thead>
            <tbody>
              {lines.map((i) => (
                <tr key={i.productId} onClick={() => navigate(`/p/${i.productId}`)} className="cursor-pointer border-b border-[#edf2f1] text-right last:border-0 hover:bg-[#f6faf9]">
                  <td className="px-3 py-2 text-left font-semibold">{nm(i)}<span className="block text-xs font-normal text-muted">{t(`units.${i.unit}`)}{i.costPrice !== null && ` · ${formatRupees(i.costPrice)}`}</span></td>
                  {cols.map((c) => cell(i, c))}
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      ))}
    </div>
  )
}

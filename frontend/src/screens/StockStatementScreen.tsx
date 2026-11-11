import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { monthToDate, saveCsv } from '../lib/csv'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'
import type { Unit } from '../lib/types'

interface Line { productId: string; name: string; category: string | null; unit: Unit; opening: number; purchased: number; sold: number; otherIn: number; otherOut: number; closing: number; costPrice: number | null; value: number | null; lost: number; bagSize?: number | null; closingBags?: number | null }
interface Statement { value: number; unvalued: number; lostValue: number; items: Line[] }

/**
 * The stock statement: for each item, opening, bought, sold, lost, other in and out, and closing, with the closing
 * value at average cost. The item-wise stock account GST rule 56 asks for, by category, for any dates.
 */
export function StockStatementScreen() {
  const { t } = useTranslation()
  const [range, setRange] = useState(monthToDate)
  const [search, setSearch] = useState('')
  const statement = useQuery({ queryKey: ['stock-statement', range.from, range.to], queryFn: () => api<Statement>(`/v1/reports/stock?from=${range.from}&to=${range.to}`) })
  const d = statement.data
  const shown = useMemo(() => (d?.items ?? []).filter((i) => (i.opening || i.closing || i.purchased || i.sold || i.otherIn || i.otherOut) && (!search || i.name.toLowerCase().includes(search.toLowerCase()))), [d, search])
  const groups = useMemo(() => {
    const map = new Map<string, Line[]>()
    for (const i of shown) map.set(i.category ?? '', [...(map.get(i.category ?? '') ?? []), i])
    return [...map.entries()]
  }, [shown])

  function download() {
    if (!d) return
    saveCsv(`stock-statement-${range.from}-to-${range.to}.csv`, [
      [t('items2.colName'), t('items2.colCategory'), t('unit'), t('sl.opening'), t('sl.bought'), t('sl.sold'), t('sl.lost'), t('sl.otherIn'), t('sl.otherOut'), t('sl.closing'), t('stock.costPrice'), t('sl.value')],
      ...d.items.map((i) => [i.name, i.category, t(`units.${i.unit}`), i.opening, i.purchased, i.sold, i.lost, i.otherIn, i.otherOut - i.lost, i.closing, i.costPrice, i.value]),
      ['', '', '', '', '', '', '', '', '', '', t('sl.totalValue'), d.value],
    ])
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      <section className="flex flex-col gap-3 rounded-[18px] bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h1 className="text-xl font-bold">📊 {t('sl.statement')}</h1>
          <button onClick={download} disabled={!d} className="h-10 rounded-xl border border-line px-3 text-sm font-semibold transition hover:bg-[#f6faf9] disabled:opacity-40">⬇ {t('sl.csv')}</button>
        </div>
        <p className="text-sm text-muted">{t('sl.statementHint')}</p>
        <div className="flex flex-wrap gap-2 text-sm">
          <label className="flex items-center gap-1.5">{t('sl.from')}<input type="date" value={range.from} max={range.to} onChange={(e) => e.target.value && setRange({ ...range, from: e.target.value })} className="h-10 rounded-xl border border-line bg-white px-2 outline-none focus:border-teal-600" /></label>
          <label className="flex items-center gap-1.5">{t('sl.to')}<input type="date" value={range.to} min={range.from} onChange={(e) => e.target.value && setRange({ ...range, to: e.target.value })} className="h-10 rounded-xl border border-line bg-white px-2 outline-none focus:border-teal-600" /></label>
        </div>
        {d && (
          <div className="grid grid-cols-2 gap-2">
            <span className="flex flex-col rounded-2xl bg-teal-50 p-3 text-teal-900"><span className="text-xs">{t('sl.totalValue')}</span><b className="text-xl">{formatRupees(d.value)}</b></span>
            <span className="flex flex-col rounded-2xl bg-red-50 p-3 text-red-800"><span className="text-xs">{t('sl.lostValue')}</span><b className="text-xl">{formatRupees(d.lostValue)}</b></span>
            {d.unvalued > 0 && <span className="col-span-2 text-xs font-semibold text-amber-800">{t('sl.unvalued', { count: d.unvalued })}</span>}
          </div>
        )}
        <span className="relative block">
          <span aria-hidden className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-muted">🔍</span>
          <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('sl.search')} aria-label={t('sl.search')} className="h-11 w-full rounded-2xl border border-line bg-white pl-10 pr-3 outline-none transition focus:border-teal-600 focus:ring-4 focus:ring-teal-600/10" />
        </span>
      </section>
      {statement.isError && <p className="text-due">{t('error')}</p>}
      {groups.map(([category, lines]) => (
        <section key={category} className="overflow-x-auto rounded-[18px] bg-white shadow-sm">
          <h2 className="bg-[#f6faf9] px-4 py-2 text-sm font-bold text-muted">{category || t('sl.noCategory')} · {formatRupees(lines.reduce((s, l) => s + (l.value ?? 0), 0))}</h2>
          <table className="w-full min-w-[640px] text-sm">
            <thead><tr className="border-b border-line text-right text-xs text-muted">
              <th className="px-3 py-2 text-left">{t('items2.colName')}</th><th className="px-2">{t('sl.opening')}</th><th className="px-2">{t('sl.bought')}</th><th className="px-2">{t('sl.sold')}</th><th className="px-2">{t('sl.lost')}</th><th className="px-2">{t('sl.closing')}</th><th className="px-3">{t('sl.value')}</th>
            </tr></thead>
            <tbody>
              {lines.map((i) => (
                <tr key={i.productId} onClick={() => navigate(`/p/${i.productId}`)} className="cursor-pointer border-b border-[#edf2f1] text-right last:border-0 hover:bg-[#f6faf9]">
                  <td className="px-3 py-2 text-left font-semibold">{i.name}<span className="block text-xs font-normal text-muted">{t(`units.${i.unit}`)}{i.costPrice !== null && ` · ${formatRupees(i.costPrice)}`}</span></td>
                  <td className="px-2">{i.opening}</td>
                  <td className="px-2 text-jama">{i.purchased || ''}</td>
                  <td className="px-2 text-udhaar">{i.sold || ''}</td>
                  <td className="px-2 text-due">{i.lost || ''}</td>
                  <td className={`px-2 font-bold ${i.closing < 0 ? 'text-due' : ''}`}>{i.closing}{i.closingBags != null && <span className="block text-xs font-normal text-muted">{t('bag.inBags', { count: i.closingBags })}</span>}</td>
                  <td className="px-3 font-semibold">{i.value !== null ? formatRupees(i.value) : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      ))}
    </div>
  )
}

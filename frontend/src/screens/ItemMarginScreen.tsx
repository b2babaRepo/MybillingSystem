import { useItemName } from '../lib/useItemName'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { localeOf } from '../i18n'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import type { Unit } from '../lib/types'
import { navigate } from '../router'
import { ProfitByList, type ProfitBy } from '../components/ProfitByList'

interface ItemMargin { productId: string | null; name: string; quantity: number; unit: Unit; sales: number; cost: number | null; profit: number | null; marginPercent: number | null }
interface ItemMarginReport { month: string; sales: number; profit: number; profitCoverage: number; items: ItemMargin[] }

function thisMonth() {
  return new Date(Date.now() + 5.5 * 3600_000).toISOString().slice(0, 7)
}

function shiftMonth(month: string, by: number) {
  const [y, m] = month.split('-').map(Number)
  const d = new Date(Date.UTC(y, m - 1 + by, 1))
  return d.toISOString().slice(0, 7)
}

type Sort = 'profit' | 'low' | 'noCost'

/**
 * Which items earn the money: each item's sales, profit and margin for a month. "Low margin" shows what is sold
 * too cheap (or at a loss); "No cost price" lists items whose profit can't be worked out until a cost is set.
 */
export function ItemMarginScreen({ shopId }: { shopId: string }) {
  const { t, i18n } = useTranslation()
  const nm = useItemName(shopId)
  const now = thisMonth()
  const [month, setMonth] = useState(now)
  const [sort, setSort] = useState<Sort>('profit')
  // Profit by item (this screen's own list), or by customer, bill or category.
  const [view, setView] = useState<'item' | ProfitBy>('item')
  const report = useQuery({ queryKey: [shopId, 'item-margins', month], queryFn: () => api<ItemMarginReport>(`/v1/reports/items?month=${month}`) })
  const data = report.data
  const label = new Date(`${month}-01T00:00:00Z`).toLocaleDateString(localeOf(i18n.language), { month: 'long', year: 'numeric', timeZone: 'UTC' })
  const items = (data?.items ?? []).filter((i) => (sort === 'noCost' ? i.profit === null : sort === 'low' ? i.marginPercent !== null : true))
  if (sort === 'low') items.sort((a, b) => (a.marginPercent ?? 0) - (b.marginPercent ?? 0))
  const noCostCount = data?.items.filter((i) => i.profit === null).length ?? 0
  const tone = (m: number | null) => (m === null ? 'bg-[#edf2f1] text-muted' : m < 0 ? 'bg-due-soft text-due' : m < 8 ? 'bg-warn-soft text-udhaar' : 'bg-brand-soft text-brand-strong')

  return (
    <div className="flex flex-col gap-3 p-4 pb-24">
      <div className="flex items-center justify-between rounded-card bg-white p-2 shadow-sm print:shadow-none">
        <button onClick={() => setMonth(shiftMonth(month, -1))} aria-label={t('book.previous')} className="h-11 w-11 rounded-xl text-xl">‹</button>
        <span className="text-lg font-bold">{label}</span>
        <button onClick={() => setMonth(shiftMonth(month, 1))} disabled={month >= now} aria-label={t('book.next')} className="h-11 w-11 rounded-xl text-xl disabled:opacity-30">›</button>
      </div>

      <div className="grid grid-cols-4 gap-1 rounded-[14px] bg-white p-1" role="tablist" aria-label={t('profitBy.title')}>
        {(['item', 'party', 'bill', 'category'] as const).map((v) => (
          <button key={v} role="tab" aria-selected={view === v} onClick={() => setView(v)}
            className={`min-h-11 rounded-[10px] text-sm font-semibold ${view === v ? 'bg-ink text-white' : 'text-muted'}`}>{t(`profitBy.tab.${v}`)}</button>
        ))}
      </div>
      {view !== 'item' && <ProfitByList shopId={shopId} month={month} by={view} />}

      {view === 'item' && report.isError && <p className="text-due">{report.error.message}</p>}
      {view === 'item' && data && (
        <>
          <div className="grid grid-cols-2 gap-2">
            <div className="flex flex-col rounded-card bg-white p-3 shadow-sm print:shadow-none"><span className="text-xs text-muted">{t('margin.sales')}</span><span className="text-xl font-extrabold">{formatRupees(data.sales)}</span></div>
            <div className="flex flex-col rounded-card bg-white p-3 shadow-sm print:shadow-none"><span className="text-xs text-muted">{t('margin.profit')}</span><span className="text-xl font-extrabold text-brand-strong">{formatRupees(data.profit)}</span></div>
          </div>
          {data.profitCoverage < 0.95 && data.sales > 0 && (
            <p className="rounded-[14px] bg-warn-soft p-3 text-sm text-warn-ink">{t('margin.coverage', { percent: Math.round(data.profitCoverage * 100) })}</p>
          )}

          <div className="grid grid-cols-3 gap-1 rounded-[14px] bg-white p-1">
            {(['profit', 'low', 'noCost'] as const).map((s) => (
              <button key={s} onClick={() => setSort(s)} aria-pressed={sort === s} className={`h-11 rounded-[10px] text-sm font-semibold ${sort === s ? 'bg-brand text-white' : 'text-muted'}`}>
                {s === 'noCost' ? t('margin.tab.noCost', { count: noCostCount }) : t(`margin.tab.${s}`)}
              </button>
            ))}
          </div>

          {items.length === 0 && <p className="py-6 text-center text-muted">{data.items.length === 0 ? t('margin.noSales') : t('margin.none')}</p>}
          {items.length > 0 && <p className="px-3 text-right text-xs font-semibold text-muted">{t('heads.marginRow')}</p>}
          <ul className="divide-y divide-[#edf2f1] rounded-card bg-white px-3 shadow-sm print:shadow-none">
            {items.map((i) => (
              <li key={`${i.productId ?? i.name}-${i.unit}`}>
                <button onClick={() => i.productId && navigate(`/p/${i.productId}`)} className="flex min-h-14 w-full items-center gap-3 py-2 text-left">
                  <span className="flex min-w-0 flex-1 flex-col">
                    <span className="truncate font-semibold">{nm(i)}</span>
                    <span className="truncate text-[13px] text-muted">{t('margin.line', { qty: i.quantity, unit: t(`units.${i.unit}`, { count: i.quantity }), sales: formatRupees(i.sales) })}</span>
                  </span>
                  <span className="flex shrink-0 flex-col items-end">
                    <span className={`font-bold ${i.profit !== null && i.profit < 0 ? 'text-due' : ''}`}>{i.profit === null ? t('margin.setCost') : formatRupees(i.profit)}</span>
                    {i.marginPercent !== null && <span className={`rounded-full px-2 text-xs font-bold ${tone(i.marginPercent)}`}>{i.marginPercent}%</span>}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  )
}

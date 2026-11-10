import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { localeOf } from '../i18n'
import { api } from '../lib/api'
import { useProducts } from '../lib/data'
import { groupStyles, variantLabel } from '../lib/styles'
import { navigate } from '../router'

interface ItemMargin { productId: string | null; quantity: number }

function thisMonth() {
  return new Date(Date.now() + 5.5 * 3600_000).toISOString().slice(0, 7)
}

function shiftMonth(month: string, by: number) {
  const [y, m] = month.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1 + by, 1)).toISOString().slice(0, 7)
}

/**
 * Sizes that sell and sizes that sit: for each style, what each size/colour sold this month and what is left. The
 * best seller is marked so it gets reordered first; a size with stock that sold nothing is marked as sitting.
 */
export function SizeReportScreen({ shopId }: { shopId: string }) {
  const { t, i18n } = useTranslation()
  const now = thisMonth()
  const [month, setMonth] = useState(now)
  const { products } = useProducts(shopId)
  const sales = useQuery({ queryKey: [shopId, 'item-margins', month], queryFn: () => api<{ items: ItemMargin[] }>(`/v1/reports/items?month=${month}`) }).data
  const sold = new Map((sales?.items ?? []).filter((i) => i.productId).map((i) => [i.productId!, i.quantity]))
  const styles = groupStyles(products).flatMap((e) => (e.kind === 'style' ? [e] : []))
  const label = new Date(`${month}-01T00:00:00Z`).toLocaleDateString(localeOf(i18n.language), { month: 'long', year: 'numeric', timeZone: 'UTC' })

  return (
    <div className="flex flex-col gap-3 p-4 pb-24">
      <div className="flex items-center justify-between rounded-[18px] bg-white p-2">
        <button onClick={() => setMonth(shiftMonth(month, -1))} aria-label={t('book.previous')} className="h-11 w-11 rounded-xl text-xl">‹</button>
        <span className="text-lg font-bold">{label}</span>
        <button onClick={() => setMonth(shiftMonth(month, 1))} disabled={month >= now} aria-label={t('book.next')} className="h-11 w-11 rounded-xl text-xl disabled:opacity-30">›</button>
      </div>
      {styles.length === 0 && <p className="py-6 text-center text-muted">{t('sizeReport.none')}</p>}
      {styles.map((s) => {
        const rows = s.items.map((p) => ({ p, sold: sold.get(p.id) ?? 0, left: Math.max(0, p.stock) }))
        const totalSold = rows.reduce((n, r) => n + r.sold, 0)
        const totalLeft = rows.reduce((n, r) => n + r.left, 0)
        const best = Math.max(0, ...rows.map((r) => r.sold))
        return (
          <section key={s.name} className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
            <div className="flex items-baseline justify-between">
              <h2 className="font-bold">{s.name}</h2>
              <span className="text-[13px] text-muted">{t('sizeReport.summary', { sold: totalSold, left: totalLeft })}</span>
            </div>
            <div className="grid grid-cols-[1fr_auto_auto] gap-x-4 gap-y-1 text-[15px]">
              <span className="text-xs font-semibold text-muted">{t('styles.size')}</span>
              <span className="text-right text-xs font-semibold text-muted">{t('sizeReport.sold')}</span>
              <span className="text-right text-xs font-semibold text-muted">{t('sizeReport.left')}</span>
              {rows.map(({ p, sold: n, left }) => (
                <button key={p.id} onClick={() => navigate(`/p/${p.id}`)} className="contents text-left">
                  <span className="min-h-9 py-1 font-semibold">
                    {variantLabel(p) || p.name}
                    {best > 0 && n === best && <span className="ml-2 rounded-full bg-teal-50 px-2 text-xs font-bold text-teal-800">{t('sizeReport.best')}</span>}
                    {n === 0 && left > 0 && <span className="ml-2 rounded-full bg-amber-50 px-2 text-xs font-bold text-amber-900">{t('sizeReport.sitting')}</span>}
                  </span>
                  <span className="py-1 text-right font-bold">{n}</span>
                  <span className={`py-1 text-right ${left === 0 ? 'font-bold text-due' : ''}`}>{left}</span>
                </button>
              ))}
            </div>
          </section>
        )
      })}
    </div>
  )
}

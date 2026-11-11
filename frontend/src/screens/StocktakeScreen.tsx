import { formatRupees } from '../lib/format'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { api } from '../lib/api'
import { useProducts } from '../lib/data'
import { uuidv7 } from '../lib/ids'

/**
 * Count the shelf, item by item. Only counted items are sent; each difference becomes a "count fix"
 * adjustment. The stocktake carries one ID, so pressing save twice corrects once.
 */
export function StocktakeScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { products } = useProducts(shopId)
  const [counts, setCounts] = useState<Record<string, string>>({})
  const [stocktakeId] = useState(uuidv7)
  const [result, setResult] = useState('')
  const [busy, setBusy] = useState(false)
  const counted = Object.entries(counts).filter(([, v]) => v.trim() !== '' && Number(v) >= 0)

  async function save() {
    setBusy(true)
    try {
      const done = await api<{ adjusted: number; unchanged: number }>('/v1/stocktakes', 'POST', {
        id: stocktakeId,
        counts: counted.map(([productId, value]) => ({ productId, counted: Number(value) })),
      })
      setResult(t('stock.stocktakeDone', done))
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (err) {
      setResult(err instanceof Error ? err.message : t('error'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <p className="text-muted">{t('stock.stocktakeHint')}</p>
      {result && <p className="rounded-xl bg-emerald-50 p-3 font-semibold text-emerald-800">{result}</p>}
      <ul className="divide-y divide-[#edf2f1] rounded-[18px] bg-white">
        {products.filter((p) => !p.uncounted).map((p) => (
          <li key={p.id} className="flex items-center gap-3 px-4 py-2.5 lg:py-1.5">
            <span className="flex min-w-0 flex-1 flex-col">
              <span className="truncate font-semibold">{p.name}</span>
              <span className="text-xs text-muted">{t('stock.books')}: {p.stock} {t(`units.${p.unit}`)}</span>
            </span>
            <input
              value={counts[p.id] ?? ''}
              onChange={(e) => setCounts({ ...counts, [p.id]: e.target.value })}
              inputMode="decimal"
              aria-label={`${t('stock.counted')}: ${p.name}`}
              placeholder={t('stock.counted')}
              className="h-11 w-24 rounded-xl border border-line px-2 text-right text-lg lg:h-9"
            />
            {(() => {
              // Desktop: the difference against the books, so a shortfall stands out while counting.
              const c = counts[p.id]?.trim()
              const diff = c ? Math.round((Number(c) - p.stock) * 1000) / 1000 : null
              return <span className={`hidden w-20 text-right font-bold lg:block ${diff == null || isNaN(diff) ? 'text-muted' : diff < 0 ? 'text-due' : diff > 0 ? 'text-teal-800' : 'text-muted'}`}>{diff == null || isNaN(diff) ? '—' : diff > 0 ? `+${diff}` : diff}</span>
            })()}
          </li>
        ))}
      </ul>
      <div className="fixed inset-x-0 bottom-[68px] z-10 mx-auto lg:bottom-0 lg:max-w-4xl max-w-xl border-t border-line bg-white px-4 py-3">
        <Button className="h-14 w-full lg:ml-auto lg:block lg:h-12 lg:w-auto lg:px-10" disabled={busy || counted.length === 0} onClick={save}>
          {t('stock.saveCount', { count: counted.length })}
        </Button>
      </div>
      <StocktakeHistory />
    </div>
  )
}

interface TakeSummary { id: string; number: number; takenAt: string; by: string; items: number; differences: number; valueDifference: number }
interface TakeLine { productId: string; name: string; books: number; counted: number; difference: number; costPrice: number | null }

/** Every saved count: when, who, how many items differed and what the difference was worth; tap one for its lines. */
function StocktakeHistory() {
  const { t } = useTranslation()
  const takes = useQuery({ queryKey: ['stocktakes'], queryFn: () => api<TakeSummary[]>('/v1/stocktakes') }).data
  const [open, setOpen] = useState<string | null>(null)
  const lines = useQuery({ queryKey: ['stocktake', open], queryFn: () => api<{ lines: TakeLine[] }>(`/v1/stocktakes/${open}`), enabled: !!open }).data
  return (
    <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm">
      <h2 className="font-bold">🗂️ {t('sl.takes')}</h2>
      {takes && takes.length === 0 && <p className="text-sm text-muted">{t('sl.noTakes')}</p>}
      {takes?.map((k) => (
        <div key={k.id} className="rounded-xl ring-1 ring-line">
          <button onClick={() => setOpen(open === k.id ? null : k.id)} className="flex w-full items-center justify-between gap-2 p-3 text-left">
            <span className="flex flex-col">
              <b>{t('sl.take', { number: k.number })}</b>
              <span className="text-xs text-muted">{t('sl.takeLine', { date: new Date(k.takenAt).toLocaleDateString(), by: k.by, items: k.items, diffs: k.differences })}</span>
            </span>
            <b className={k.valueDifference < 0 ? 'text-due' : k.valueDifference > 0 ? 'text-jama' : 'text-muted'}>{k.valueDifference > 0 ? '+' : ''}{formatRupees(k.valueDifference)}</b>
          </button>
          {open === k.id && lines && (
            <table className="w-full border-t border-line text-sm">
              <thead><tr className="text-right text-xs text-muted"><th className="px-3 py-1 text-left">{t('items2.colName')}</th><th className="px-2">{t('sl.books')}</th><th className="px-2">{t('sl.counted')}</th><th className="px-3">{t('sl.difference')}</th></tr></thead>
              <tbody>{lines.lines.filter((l) => l.difference !== 0).map((l) => (
                <tr key={l.productId} className="border-t border-[#edf2f1] text-right"><td className="px-3 py-1.5 text-left">{l.name}</td><td className="px-2">{l.books}</td><td className="px-2">{l.counted}</td><td className={`px-3 font-bold ${l.difference < 0 ? 'text-due' : 'text-jama'}`}>{l.difference > 0 ? '+' : ''}{l.difference}</td></tr>
              ))}</tbody>
            </table>
          )}
        </div>
      ))}
    </section>
  )
}


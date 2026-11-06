import { useQueryClient } from '@tanstack/react-query'
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
        {products.map((p) => (
          <li key={p.id} className="flex items-center gap-3 px-4 py-2.5">
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
              className="h-11 w-24 rounded-xl border border-line px-2 text-right text-lg"
            />
          </li>
        ))}
      </ul>
      <div className="fixed inset-x-0 bottom-[68px] z-10 mx-auto lg:bottom-0 lg:left-60 lg:max-w-4xl max-w-xl border-t border-line bg-white px-4 py-3">
        <Button className="h-14 w-full" disabled={busy || counted.length === 0} onClick={save}>
          {t('stock.saveCount', { count: counted.length })}
        </Button>
      </div>
    </div>
  )
}

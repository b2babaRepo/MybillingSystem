import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ItemPicker } from '../components/ItemPicker'
import { LotTable, type Lot } from '../components/LandedCost'
import { api } from '../lib/api'
import { useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'

/**
 * Landed cost by bill: pick an item and see every bill it came on, newest first, what it really cost each time with
 * freight and charges, and how each lot sold. Without an item: the latest bills with their totals.
 */
export function LotsScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const { products } = useProducts(shopId)
  const [text, setText] = useState('')
  const [productId, setProductId] = useState('')
  const lots = useQuery({ queryKey: [shopId, 'suppliers', 'lots', productId], queryFn: () => api<Lot[]>(`/v1/suppliers/lots${productId ? `?productId=${productId}` : ''}`) }).data ?? []
  const landed = lots.flatMap((l) => l.lines.map((x) => x.landedCost))
  const cheapest = productId && landed.length > 1 ? Math.min(...landed) : null
  return (
    <div className="flex flex-col gap-3 p-4">
      <header>
        <h1 className="text-xl font-bold">{t('landed.compareTitle')}</h1>
        <p className="text-sm text-muted">{t('landed.compareHint')}</p>
      </header>
      <ItemPicker products={products.filter((p) => !p.isService)} text={text} placeholder={t('supplier2.itemSearch')}
        onText={(v) => { setText(v); setProductId(products.find((p) => p.name.toLowerCase() === v.trim().toLowerCase())?.id ?? '') }}
        onPick={(p) => { setText(p.name); setProductId(p.id) }} />
      {lots.length === 0 && <p className="rounded-xl bg-white p-4 text-sm text-muted shadow-sm">{t('landed.noLots')}</p>}
      {lots.map((lot) => (
        <section key={lot.id} className="flex flex-col gap-1 rounded-[18px] bg-white p-4 shadow-sm">
          <p className="flex flex-wrap justify-between gap-2 font-semibold">
            <span>{lot.supplier}{lot.billNo ? ` · ${lot.billNo}` : ''}</span>
            <span className="text-sm text-muted">{new Date(lot.occurredAt).toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: '2-digit' })}</span>
          </p>
          {cheapest !== null && lot.lines.some((l) => l.landedCost === cheapest) && <p className="self-start rounded-full bg-teal-50 px-2 text-xs font-semibold text-teal-800">{t('landed.cheapest')}</p>}
          <LotTable lines={lot.lines} showName={!productId} />
          <p className="flex flex-wrap justify-between gap-x-3 text-sm">
            <span>{t('landed.total', { amount: formatRupees(lot.landedTotal) })}{lot.charges > 0 ? ` · ${t('landed.chargesIn', { amount: formatRupees(lot.charges) })}` : ''}</span>
            {lot.revenue > 0 && <span className={lot.profit < 0 ? 'text-due' : 'text-teal-800'}>{t('landed.soldProfit', { revenue: formatRupees(lot.revenue), profit: formatRupees(lot.profit) })}</span>}
          </p>
        </section>
      ))}
    </div>
  )
}

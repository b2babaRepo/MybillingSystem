import { useState, type FormEvent, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field, PendingDot, Sheet } from '../components/ui'
import { addProduct, useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'
import type { Product, Unit } from '../lib/types'
import { navigate } from '../router'
import { UnitSelect } from './BillScreen'

/** The catalogue with what's on the shelf: search, category chips, low-stock filter. */
export function ItemsScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const { products, isLoading } = useProducts(shopId)
  const [adding, setAdding] = useState(false)
  const [search, setSearch] = useState('')
  const [category, setCategory] = useState<string | null>(null)
  const [lowOnly, setLowOnly] = useState(false)

  const categories = [...new Set(products.map((p) => p.category).filter((c): c is string => !!c))].sort()
  const query = search.trim().toLowerCase()
  const shown = products.filter((p) =>
    (!query || p.name.toLowerCase().includes(query)) && (!category || p.category === category) && (!lowOnly || p.isLow))
  const lowCount = products.filter((p) => p.isLow).length

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <input
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder={t('search')}
        aria-label={t('search')}
        className="h-12 w-full rounded-[14px] border border-line bg-white px-4 text-[17px] outline-none focus:border-teal-600"
      />
      <div className="flex gap-2 overflow-x-auto pb-1">
        <Chip selected={lowOnly} onClick={() => setLowOnly(!lowOnly)} tone="warn">{t('stock.lowOnly')} · {lowCount}</Chip>
        <Chip selected={category === null} onClick={() => setCategory(null)}>{t('stock.all')}</Chip>
        {categories.map((c) => (
          <Chip key={c} selected={category === c} onClick={() => setCategory(category === c ? null : c)}>{c}</Chip>
        ))}
      </div>
      <div className="grid grid-cols-2 gap-2">
        <Button tone="plain" onClick={() => navigate('/stocktake')}>{t('stock.stocktake')}</Button>
        <Button tone="plain" onClick={() => navigate('/import')}>{t('stock.import')}</Button>
      </div>

      {!isLoading && products.length === 0 && <p className="py-8 text-center text-muted">{t('noItems')}</p>}
      <ul className="divide-y divide-[#edf2f1] rounded-[18px] bg-white">
        {shown.map((p) => (
          <li key={p.id}>
            <button onClick={() => navigate(`/p/${p.id}`)} className="flex min-h-16 w-full items-center gap-3 px-4 py-3 text-left active:bg-slate-50">
              <span className="flex min-w-0 flex-1 flex-col">
                <span className="flex items-center gap-2 font-semibold">
                  <span className="truncate">{p.name}</span>
                  {p.pending && <PendingDot />}
                </span>
                <span className="text-[13px] text-muted">
                  {p.sellingPrice > 0 ? `${formatRupees(p.sellingPrice)}/${t(`units.${p.unit}`)}` : t('stock.noPrice')}
                  {p.category && ` · ${p.category}`}
                </span>
              </span>
              <StockBadge product={p} />
            </button>
          </li>
        ))}
      </ul>
      <Button className="fixed right-4 bottom-24 h-14 rounded-full! px-6 shadow-[0_6px_16px_rgba(15,118,110,0.35)]" onClick={() => setAdding(true)}>
        + {t('addItem')}
      </Button>
      {adding && <AddProductSheet shopId={shopId} categories={categories} onClose={() => setAdding(false)} />}
    </div>
  )
}

/** Stock with words, not colour alone: "Out" (red), "Low" (orange), or "In stock". */
export function StockBadge({ product, large = false }: { product: Product; large?: boolean }) {
  const { t } = useTranslation()
  const out = product.stock <= 0
  const tone = out ? 'text-due' : product.isLow ? 'text-udhaar' : 'text-ink'
  return (
    <span className={`flex shrink-0 flex-col items-end ${tone}`}>
      <span className={`${large ? 'text-4xl' : 'text-lg'} font-bold`}>
        {product.stock} {t(`units.${product.unit}`)}
      </span>
      <span className="text-xs font-semibold">{out ? t('stock.out') : product.isLow ? t('stock.low') : t('stock.inStock')}</span>
    </span>
  )
}

function Chip({ selected, onClick, children, tone = 'plain' }: { selected: boolean; onClick: () => void; children: ReactNode; tone?: 'plain' | 'warn' }) {
  const on = tone === 'warn' ? 'bg-udhaar text-white' : 'bg-ink text-white'
  return (
    <button onClick={onClick} aria-pressed={selected} className={`h-11 shrink-0 rounded-full px-4 text-[15px] font-semibold ${selected ? on : 'border border-line bg-white text-ink'}`}>
      {children}
    </button>
  )
}

function AddProductSheet({ shopId, categories, onClose }: { shopId: string; categories: string[]; onClose: () => void }) {
  const { t } = useTranslation()
  const [name, setName] = useState('')
  const [unit, setUnit] = useState<Unit>('piece')
  const [price, setPrice] = useState('')
  const [category, setCategory] = useState('')
  const [opening, setOpening] = useState('')
  const [reorder, setReorder] = useState('')

  async function submit(e: FormEvent) {
    e.preventDefault()
    await addProduct(shopId, {
      name: name.trim(),
      unit,
      sellingPrice: Number(price),
      costPrice: null,
      category: category.trim() || null,
      openingStock: opening ? Number(opening) : null,
      reorderLevel: reorder ? Number(reorder) : null,
    })
    setName('')
    setPrice('')
    setOpening('')
  }

  return (
    <Sheet title={t('addItem')} onClose={onClose}>
      {/* Stays open after saving, so a shop can add many items in a row. */}
      <form onSubmit={submit} className="space-y-3">
        <Field label={t('itemName')} value={name} onChange={(e) => setName(e.target.value)} required autoFocus />
        <div className="grid grid-cols-2 gap-2">
          <UnitSelect value={unit} onChange={setUnit} />
          <Field label={t('price')} value={price} onChange={(e) => setPrice(e.target.value)} inputMode="decimal" required />
        </div>
        <Field label={t('stock.category')} value={category} onChange={(e) => setCategory(e.target.value)} list="item-categories" />
        <datalist id="item-categories">{categories.map((c) => <option key={c} value={c} />)}</datalist>
        <div className="grid grid-cols-2 gap-2">
          <Field label={t('stock.openingStock')} value={opening} onChange={(e) => setOpening(e.target.value)} inputMode="decimal" />
          <Field label={t('stock.reorderLevel')} value={reorder} onChange={(e) => setReorder(e.target.value)} inputMode="decimal" />
        </div>
        <Button type="submit" className="w-full" disabled={!name.trim() || !(Number(price) > 0)}>
          {t('save')}
        </Button>
      </form>
    </Sheet>
  )
}

import { useShopSettings } from '../lib/reports'
import { useState, type FormEvent, type ReactNode } from 'react'
import { useGstBilling } from '../lib/billing'
import { GST_RATES } from '../lib/gst'
import { useTranslation } from 'react-i18next'
import { Button, Field, PendingDot, Sheet } from '../components/ui'
import { addProduct, useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'
import type { Product, Unit } from '../lib/types'
import { navigate } from '../router'
import { imageUrl } from '../lib/images'
import { itemArt } from '../lib/itemArt'
import { UnitSelect } from './BillScreen'

/** The catalogue with what's on the shelf: search, category chips, low-stock filter. */
export function ItemsScreen({ shopId }: { shopId: string }) {
  const isKirana = (useShopSettings(shopId).data?.type ?? 'kirana') === 'kirana'
  const { t } = useTranslation()
  const { products, isLoading } = useProducts(shopId)
  const [adding, setAdding] = useState(false)
  const [search, setSearch] = useState('')
  const [category, setCategory] = useState<string | null>(null)
  const [filter, setFilter] = useState<'all' | 'low' | 'out'>('all')
  const [zoom, setZoom] = useState<{ src: string; name: string } | null>(null)

  const categories = [...new Set(products.map((p) => p.category).filter((c): c is string => !!c))].sort()
  const query = search.trim().toLowerCase()
  const isOut = (p: Product) => !p.isService && p.stock <= 0
  const shown = products.filter((p) =>
    (!query || p.name.toLowerCase().includes(query)) && (!category || p.category === category)
    && (filter === 'all' || (filter === 'out' ? isOut(p) : p.isLow && !isOut(p))))
  const lowCount = products.filter((p) => p.isLow && !isOut(p)).length
  const outCount = products.filter(isOut).length

  // The ways to fill the list, as equal picture tiles instead of a banner and three plain buttons.
  const actions = [
    ...(isKirana ? [{ icon: '🛒', label: t('items2.common'), to: '/items/common', tint: 'bg-amber-50' }] : []),
    { icon: '📋', label: t('items2.master'), to: '/items/pick', tint: 'bg-teal-50' },
    { icon: '📦', label: t('items2.stocktake'), to: '/stocktake', tint: 'bg-sky-50' },
    { icon: '📥', label: t('items2.import'), to: '/import', tint: 'bg-violet-50' },
  ]

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <div className={`grid gap-2 ${actions.length === 4 ? 'grid-cols-4' : 'grid-cols-3'}`}>
        {actions.map((a) => (
          <button key={a.to} onClick={() => navigate(a.to)} className={`flex min-h-[84px] flex-col items-center justify-center gap-1 rounded-2xl border border-line ${a.tint} px-1 py-2 text-center active:scale-95`}>
            <span aria-hidden className="text-[28px] leading-none">{a.icon}</span>
            <span className="text-[13px] leading-tight font-semibold">{a.label}</span>
          </button>
        ))}
      </div>

      <div className="relative">
        <span aria-hidden className="pointer-events-none absolute top-1/2 left-4 -translate-y-1/2 text-lg text-muted">🔍</span>
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('items2.search', { count: products.length })}
          aria-label={t('search')}
          className="h-12 w-full rounded-[14px] border border-line bg-white pr-10 pl-11 text-[17px] outline-none focus:border-teal-600"
        />
        {search && <button onClick={() => setSearch('')} aria-label={t('cancel')} className="absolute top-1/2 right-2 h-9 w-9 -translate-y-1/2 rounded-full text-muted">✕</button>}
      </div>

      <div className="flex gap-2 overflow-x-auto pb-1">
        <Chip selected={filter === 'all' && category === null} onClick={() => { setFilter('all'); setCategory(null) }}>{t('stock.all')} · {products.length}</Chip>
        {lowCount > 0 && <Chip selected={filter === 'low'} onClick={() => setFilter(filter === 'low' ? 'all' : 'low')} tone="warn">⚠ {t('stock.low')} · {lowCount}</Chip>}
        {outCount > 0 && <Chip selected={filter === 'out'} onClick={() => setFilter(filter === 'out' ? 'all' : 'out')} tone="due">✕ {t('stock.out')} · {outCount}</Chip>}
        {categories.map((c) => (
          <Chip key={c} selected={category === c} onClick={() => setCategory(category === c ? null : c)}>{c}</Chip>
        ))}
      </div>

      {!isLoading && products.length === 0 && <p className="py-8 text-center text-muted">{t('noItems')}</p>}
      {!isLoading && products.length > 0 && shown.length === 0 && <p className="py-8 text-center text-muted">{t('items2.none')}</p>}
      <ul className="divide-y divide-[#edf2f1] overflow-hidden rounded-[18px] bg-white">
        {shown.map((p) => {
          const thumb = imageUrl(shopId, p.id, p.imageAt, 'thumb')
          const art = itemArt({ name: p.name })
          return (
            <li key={p.id} className="flex items-center gap-3 px-3 py-2.5">
              {thumb ? (
                <button onClick={() => setZoom({ src: imageUrl(shopId, p.id, p.imageAt)!, name: p.name })} aria-label={t('pick.zoom', { name: p.name })} title={t('pick.zoom', { name: p.name })}
                  className="shrink-0 cursor-zoom-in rounded-xl transition-transform hover:scale-110">
                  <img src={thumb} alt="" loading="lazy" width={52} height={52} className="h-[52px] w-[52px] rounded-xl border border-line bg-white object-contain" />
                </button>
              ) : (
                <span aria-hidden className="flex h-[52px] w-[52px] shrink-0 items-center justify-center rounded-xl text-[26px]" style={{ background: art.tint }}>{art.emoji}</span>
              )}
              <button onClick={() => navigate(`/p/${p.id}`)} className="flex min-h-14 min-w-0 flex-1 items-center gap-2 text-left">
                <span className="flex min-w-0 flex-1 flex-col">
                  <span className="flex items-center gap-2 text-[16px] font-semibold">
                    <span className="truncate">{p.name}</span>
                    {p.pending && <PendingDot />}
                  </span>
                  <span className="truncate text-[13px] text-muted">
                    {p.sellingPrice > 0 ? <b className="font-bold text-ink">{formatRupees(p.sellingPrice)}</b> : <span className="font-semibold text-udhaar">{t('stock.noPrice')}</span>}
                    {p.sellingPrice > 0 && `/${t(`units.${p.unit}`)}`}
                    {p.category && ` · ${p.category}`}
                  </span>
                </span>
                <StockPill product={p} />
              </button>
            </li>
          )
        })}
      </ul>
      <Button className="fixed right-[max(1rem,calc(50vw-17rem))] bottom-24 h-14 rounded-full! px-6 shadow-[0_6px_16px_rgba(15,118,110,0.35)]" onClick={() => setAdding(true)}>
        + {t('addItem')}
      </Button>
      {adding && <AddProductSheet shopId={shopId} categories={categories} onClose={() => setAdding(false)} />}
      {zoom && (
        <div role="dialog" aria-modal="true" aria-label={zoom.name} onClick={() => setZoom(null)} onKeyDown={(e) => e.key === 'Escape' && setZoom(null)} tabIndex={-1} ref={(el) => el?.focus()}
          className="fixed inset-0 z-50 flex cursor-zoom-out flex-col items-center justify-center gap-3 bg-black/80 p-4">
          <img src={zoom.src} alt={zoom.name} className="max-h-[75dvh] max-w-full rounded-2xl bg-white object-contain p-2" />
          <p className="text-center text-lg font-semibold text-white">{zoom.name}</p>
          <button type="button" onClick={() => setZoom(null)} className="h-11 rounded-xl bg-white px-6 font-bold text-ink">{t('pick.close')}</button>
        </div>
      )}
    </div>
  )
}

/** The list's stock: a coloured pill with words, so it reads at a glance and without colour too. */
function StockPill({ product }: { product: Product }) {
  const { t } = useTranslation()
  if (product.isService) return <StockBadge product={product} />
  const out = product.stock <= 0
  const tone = out ? 'bg-red-50 text-due' : product.isLow ? 'bg-amber-50 text-udhaar' : 'bg-teal-50 text-teal-800'
  return (
    <span className={`flex shrink-0 flex-col items-center rounded-xl px-2.5 py-1 ${tone}`}>
      <span className="text-[17px] leading-tight font-bold">{product.stock} {t(`units.${product.unit}`)}</span>
      <span className="text-[11px] font-semibold">{out ? t('stock.out') : product.isLow ? t('stock.low') : t('stock.inStock')}</span>
    </span>
  )
}

/** Stock with words, not colour alone: "Out" (red), "Low" (orange), or "In stock". */
export function StockBadge({ product, large = false }: { product: Product; large?: boolean }) {
  const { t } = useTranslation()
  if (product.isService) {
    return <span className="shrink-0 text-right text-sm font-semibold text-teal-800">{t('salon.serviceBadge')}{product.durationMinutes ? ` · ${t('salon.minutesShort', { count: product.durationMinutes })}` : ''}</span>
  }
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

function Chip({ selected, onClick, children, tone = 'plain' }: { selected: boolean; onClick: () => void; children: ReactNode; tone?: 'plain' | 'warn' | 'due' }) {
  const on = tone === 'warn' ? 'bg-udhaar text-white' : tone === 'due' ? 'bg-due text-white' : 'bg-ink text-white'
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
  const [cost, setCost] = useState('')
  const [category, setCategory] = useState('')
  const [opening, setOpening] = useState('')
  const [reorder, setReorder] = useState('')
  const gstBilling = useGstBilling(shopId)
  const [gst, setGst] = useState('')
  const [hsn, setHsn] = useState('')
  const [service, setService] = useState(false)
  const [minutes, setMinutes] = useState('')

  async function submit(e: FormEvent) {
    e.preventDefault()
    await addProduct(shopId, {
      name: name.trim(),
      unit,
      sellingPrice: Number(price),
      costPrice: cost ? Number(cost) : null,
      category: category.trim() || null,
      openingStock: opening ? Number(opening) : null,
      reorderLevel: reorder ? Number(reorder) : null,
      gstRate: gst === '' ? null : Number(gst),
      hsnCode: hsn.trim() || null,
      isService: service,
      durationMinutes: service && Number(minutes) > 0 ? Number(minutes) : null,
    })
    setName('')
    setPrice('')
    setCost('')
    setOpening('')
    setHsn('')
  }

  return (
    <Sheet title={t('addItem')} onClose={onClose}>
      {/* Stays open after saving, so a shop can add many items in a row. */}
      <form onSubmit={submit} className="space-y-3">
        <Field label={t('itemName')} value={name} onChange={(e) => setName(e.target.value)} required autoFocus />
        <div className="grid grid-cols-2 gap-2">
          <UnitSelect value={unit} onChange={setUnit} />
          <Field label={t('stock.sellingPrice')} value={price} onChange={(e) => setPrice(e.target.value)} inputMode="decimal" required />
        </div>
        <Field label={t('stock.buyingCost')} value={cost} onChange={(e) => setCost(e.target.value)} inputMode="decimal" />
        <Field label={t('stock.category')} value={category} onChange={(e) => setCategory(e.target.value)} list="item-categories" />
        <datalist id="item-categories">{categories.map((c) => <option key={c} value={c} />)}</datalist>
        <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={service} onChange={(e) => setService(e.target.checked)} className="h-5 w-5" />{t('salon.isService')}</label>
        {service && <Field label={t('salon.min')} value={minutes} onChange={(e) => setMinutes(e.target.value)} inputMode="numeric" />}
        {!service && <div className="grid grid-cols-2 gap-2">
          <Field label={t('stock.openingStock')} value={opening} onChange={(e) => setOpening(e.target.value)} inputMode="decimal" />
          <Field label={t('stock.reorderLevel')} value={reorder} onChange={(e) => setReorder(e.target.value)} inputMode="decimal" />
        </div>}
        {gstBilling && <div className="grid grid-cols-2 gap-2">
          <label className="flex flex-col gap-1">
            <span className="text-sm font-medium text-muted">{t('stock.gst')}</span>
            <select value={gst} onChange={(e) => setGst(e.target.value)} className="h-12 rounded-xl border border-line bg-white px-3 text-lg">
              <option value="">{t('stock.gstNotSet')}</option>
              {GST_RATES.map((rate) => <option key={rate} value={rate}>{rate}%</option>)}
            </select>
          </label>
          <Field label={t('stock.hsn')} value={hsn} onChange={(e) => setHsn(e.target.value)} inputMode="numeric" maxLength={8} />
        </div>}
        <div className="grid grid-cols-[1fr_2fr] gap-2">
          <Button type="button" tone="plain" onClick={onClose}>{t('cancel')}</Button>
          <Button type="submit" disabled={!name.trim() || !(Number(price) > 0)}>{t('save')}</Button>
        </div>
      </form>
    </Sheet>
  )
}

import { itemName, nameMatches } from '../lib/itemName'
import { CategoryInput } from '../components/CategoryInput'
import { orderedNames, useCategories } from '../lib/categories'
import { useShopSettings } from '../lib/reports'
import { groupStyles, sortVariants, variantLabel } from '../lib/styles'
import { useState, type FormEvent, type ReactNode } from 'react'
import { GST_RATES } from '../lib/gst'
import { useTranslation } from 'react-i18next'
import { Button, Field, PendingDot, Select, Sheet } from '../components/ui'
import { btn } from '../lib/buttonStyles'
import { addProduct, addStockMovement, useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'
import type { Product, Unit } from '../lib/types'
import { navigate } from '../router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '../lib/api'
import { confirmAsk } from '../lib/ask'
import { isOutToday } from '../lib/local'
import { imageUrl } from '../lib/images'
import { itemArt } from '../lib/itemArt'
import { UnitSelect } from './BillScreen'
import { DataTable } from '../components/DataTable'
import { shownError } from '../lib/notify'

interface SuggestedItem { id: number; trade: string; category: string; name: string; nameHi: string | null; packSizes: string[]; imageAt?: string | null; popular?: boolean }

/** The catalogue with what's on the shelf: search, category chips, low-stock filter. */
export function ItemsScreen({ shopId, isOwner = false }: { shopId: string; isOwner?: boolean }) {
  const shopType = useShopSettings(shopId).data?.type ?? 'kirana'
  const isKirana = shopType === 'kirana'
  const { t } = useTranslation()
  const { products, isLoading } = useProducts(shopId)
  const [adding, setAdding] = useState(false)
  const [search, setSearch] = useState('')
  const [category, setCategory] = useState<string | null>(null)
  const [filter, setFilter] = useState<'all' | 'low' | 'out' | 'suggested'>('all')
  const [zoom, setZoom] = useState<{ src: string; name: string } | null>(null)
  // The style whose new stock is being entered size by size.
  const [receiving, setReceiving] = useState<string | null>(null)

  async function stopCounting() {
    if (!(await confirmAsk(t('stock.stopCountingConfirm')))) return
    await api('/v1/products/stop-counting-empty', 'POST')
    await queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
    setFilter('all')
  }

  const categoryRows = useCategories(shopId).data
  const categories = orderedNames(categoryRows, products.map((p) => p.category))
  const query = search.trim().toLowerCase()
  const isOut = (p: Product) => !p.isService && !p.uncounted && p.stock <= 0
  const shown = products.filter((p) =>
    (!query || nameMatches(p, query)) && (!category || p.category === category)
    && (filter === 'all' || (filter === 'out' ? isOut(p) : p.isLow && !isOut(p))))
  const lowCount = products.filter((p) => p.isLow && !isOut(p)).length
  // Suggested: the trade's popular master items this shop doesn't have yet (matched by name, as the picker does).
  const queryClient = useQueryClient()
  const master = useQuery({ queryKey: ['master-items'], queryFn: () => api<SuggestedItem[]>('/v1/master-items'), enabled: isOwner, staleTime: 600_000 }).data ?? []
  const owned = new Set(products.map((p) => p.name.trim().toLowerCase()))
  const has = (m: SuggestedItem) => [m.nameHi ?? m.name, m.name].some((n) => owned.has(n.toLowerCase()) || (m.packSizes.length > 0 && owned.has(`${n} ${m.packSizes[0]}`.toLowerCase())))
  const trade = shopType
  const suggested = master.filter((m) => m.popular && m.trade === trade && !has(m)).slice(0, 30)
  const [added, setAdded] = useState<Set<number>>(new Set())
  async function addSuggested(m: SuggestedItem) {
    setAdded((s) => new Set(s).add(m.id))
    await api('/v1/products/from-master', 'POST', { ids: [m.id], sizes: {} }).catch(() => setAdded((s) => { const n = new Set(s); n.delete(m.id); return n }))
    void queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
  }
  const outCount = products.filter(isOut).length

  const icon = (d: string, size = 18) => <svg aria-hidden="true" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d={d} /></svg>
  // The ways to fill the list, each in its own colour like the customer and supplier pages, with a line icon on a white dot.
  const actions = [
    ...(isKirana ? [{ d: 'M3 4h2l2.5 11h11L21 7H6M9 20h.01M18 20h.01', label: t('items2.common'), to: '/items/common', tone: 'bg-[#fdf1e3] text-[#a1460a] hover:bg-[#fbe6cc]' }] : []),
    { d: 'M9 5h11M9 12h11M9 19h11M4 5h.01M4 12h.01M4 19h.01', label: t('items2.master'), to: '/items/pick', tone: 'bg-[#e3f2ee] text-[#0b5e4f] hover:bg-[#d2ebe4]' },
    { d: 'M4 8V4h4M16 4h4v4M20 16v4h-4M8 20H4v-4M8 12h8', label: t('items2.stocktake'), to: '/stocktake', tone: 'bg-[#e8effd] text-[#1d4ed8] hover:bg-[#dbe5fc]' },
    { d: 'M12 15V3M7 8l5-5 5 5M4 15v5h16v-5', label: t('items2.import'), to: '/import', tone: 'bg-[#f1eafd] text-[#6d28d9] hover:bg-[#e7dcfb]' },
  ]
  // Pages about the stock, as small links instead of four big cards.
  const reports = [
    { d: 'M5 3h10l4 4v14H5zM8 11h8M8 15h8', label: t('sl.statement'), to: '/stock-statement' },
    { d: 'M12 3v9h9A9 9 0 1112 3z', label: t('sl.salesByShort'), to: '/sales-by' },
    ...(isOwner ? [{ d: 'M3 6h6l2 2h10v12H3z', label: t('cat.title'), to: '/categories' }] : []),
    { d: 'M3 12h4l3-8 4 16 3-8h4', label: t('sh.title'), to: '/stock-health' },
  ]
  // Shelf value at cost: what the stock cost the shop.
  const value = products.reduce((n, p) => n + (!p.isService && !p.uncounted && p.stock > 0 && p.costPrice ? p.stock * p.costPrice : 0), 0)
  const card = (on: boolean) => `flex min-h-[84px] flex-col justify-between rounded-card p-3 text-left shadow-sm transition ${on ? 'ring-2 ring-brand' : 'ring-1 ring-black/5 hover:ring-brand-line'}`

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="mr-auto text-xl font-extrabold">{t('items')}</h1>
        <Button className="hidden h-12 px-5 text-base lg:block" onClick={() => setAdding(true)}>+ {t('addItem')}</Button>
      </div>

      <div className={`grid grid-cols-2 gap-2 ${actions.length === 4 ? 'sm:grid-cols-4' : 'sm:grid-cols-3'}`}>
        {actions.map((a) => (
          <button key={a.to} onClick={() => navigate(a.to)} className={`flex min-h-[52px] items-center gap-2.5 rounded-2xl px-3 text-left text-sm font-bold transition active:scale-[0.98] ${a.tone}`}>
            <span aria-hidden className="flex h-[30px] w-[30px] shrink-0 items-center justify-center rounded-full bg-white">{icon(a.d)}</span>
            <span className="leading-tight">{a.label}</span>
          </button>
        ))}
      </div>

      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        <button onClick={() => { setFilter('all'); setCategory(null) }} className={`${card(filter === 'all' && category === null)} bg-white`}>
          <span className="text-xs font-semibold text-muted">{t('items3.total')}</span><b className="text-2xl">{products.length}</b>
        </button>
        <button onClick={() => setFilter(filter === 'low' ? 'all' : 'low')} className={`${card(filter === 'low')} bg-warn-soft`}>
          <span className="text-xs font-semibold text-warn-ink">{t('stock.low')}</span><b className="text-2xl text-udhaar">{lowCount}</b>
        </button>
        <button onClick={() => setFilter(filter === 'out' ? 'all' : 'out')} className={`${card(filter === 'out')} bg-due-soft`}>
          <span className="text-xs font-semibold text-due">{t('stock.out')}</span><b className="text-2xl text-due">{outCount}</b>
        </button>
        <div className={`${card(false)} bg-white`}>
          <span className="text-xs font-semibold text-muted">{t('items3.value')}</span><b className="text-xl">{formatRupees(Math.round(value))}</b>
        </div>
      </div>

      <div className="flex flex-wrap gap-2">
        {reports.map((r) => (
          <button key={r.to} onClick={() => navigate(r.to)} className="flex min-h-10 items-center gap-1.5 rounded-full border border-line bg-white px-3.5 text-sm font-semibold text-ink hover:bg-[#f6faf9]">
            <span className="text-brand">{icon(r.d, 16)}</span>{r.label}
          </button>
        ))}
      </div>

      <div className="relative">
        <span aria-hidden className="pointer-events-none absolute top-1/2 left-4 -translate-y-1/2 text-muted">{icon('M11 4a7 7 0 100 14 7 7 0 000-14zM20 20l-3.5-3.5')}</span>
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('items2.search', { count: products.length })}
          aria-label={t('search')}
          className="h-12 w-full rounded-2xl border border-line bg-white pr-10 pl-11 text-[17px] shadow-sm outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10"
        />
        {search && <button onClick={() => setSearch('')} aria-label={t('cancel')} className="absolute top-1/2 right-2 h-9 w-9 -translate-y-1/2 rounded-full text-muted">✕</button>}
      </div>

      <div className="flex gap-2 overflow-x-auto pb-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
        <Chip selected={filter === 'all' && category === null} onClick={() => { setFilter('all'); setCategory(null) }}>{t('stock.all')} · {products.length}</Chip>
        {isOwner && suggested.length > 0 && <Chip selected={filter === 'suggested'} onClick={() => setFilter(filter === 'suggested' ? 'all' : 'suggested')} tone="warn">{t('items2.suggested')} · {suggested.length}</Chip>}
        {categories.map((c) => (
          <Chip key={c} selected={category === c} onClick={() => setCategory(category === c ? null : c)}>{c}</Chip>
        ))}
      </div>

      {isOwner && lowCount + outCount > 0 && (
        <button onClick={() => navigate('/reorder')} className="flex min-h-14 items-center gap-3 rounded-2xl bg-[#fdf1e3] px-4 text-left font-bold text-[#a1460a] shadow-sm ring-1 ring-amber-200 hover:bg-[#fbe6cc]">
          <span aria-hidden className="flex h-[30px] w-[30px] shrink-0 items-center justify-center rounded-full bg-white">{icon('M3 4h2l2.5 11h11L21 7H6M9 20h.01M18 20h.01')}</span>
          <span className="flex-1">{t('reorder.linkAll', { count: lowCount + outCount })}</span><span aria-hidden>›</span>
        </button>
      )}
      {!isLoading && products.length === 0 && <p className="py-8 text-center text-muted">{t('noItems')}</p>}
      {filter === 'suggested' && (
        <section className="flex flex-col gap-2">
          <p className="px-1 text-sm text-muted">{t('items2.suggestedHint')}</p>
          <ul className="divide-y divide-[#edf2f1] overflow-hidden rounded-card bg-white">
            {suggested.map((m) => {
              const art = itemArt({ name: m.name })
              const photo = m.imageAt ? `/api/v1/master-images/${m.id}?v=${Date.parse(m.imageAt)}&size=thumb` : null
              return (
                <li key={m.id} className="flex items-center gap-3 px-3 py-2.5">
                  {photo
                    ? <img src={photo} alt="" loading="lazy" width={52} height={52} className="h-[52px] w-[52px] shrink-0 rounded-xl border border-line bg-white object-contain" />
                    : <span aria-hidden className="art-tint flex h-[52px] w-[52px] shrink-0 items-center justify-center rounded-xl text-[26px]" style={{ background: art.tint }}>{art.emoji}</span>}
                  <span className="flex min-w-0 flex-1 flex-col">
                    <span className="truncate text-[16px] font-semibold">{m.name}</span>
                    <span className="truncate text-[13px] text-muted">{m.nameHi ?? m.category}</span>
                  </span>
                  <button onClick={() => void addSuggested(m)} disabled={added.has(m.id)}
                    className={`h-10 shrink-0 rounded-xl px-4 font-bold ${added.has(m.id) ? 'bg-brand text-white' : 'border-2 border-brand text-brand-strong'}`}>
                    {added.has(m.id) ? `✓ ${t('pick.picked')}` : `+ ${t('pick.addOne')}`}
                  </button>
                </li>
              )
            })}
          </ul>
        </section>
      )}
      {filter !== 'suggested' && !isLoading && products.length > 0 && shown.length === 0 && <p className="py-8 text-center text-muted">{t('items2.none')}</p>}
      {filter !== 'suggested' && (
        <span className="hidden text-sm text-muted lg:block">{t('items2.count', { count: shown.length })}</span>
      )}
      {isOwner && filter === 'out' && <button onClick={() => void stopCounting()} className="min-h-11 rounded-xl border border-line bg-white px-3 text-left text-sm font-semibold text-brand-strong">{t('stock.stopCountingEmpty')}</button>}
      {/* A computer: every item as a row to sort, filter and download. */}
      {filter !== 'suggested' && shown.length > 0 && (() => {
        const stockState = (p: Product) => (p.isService || p.uncounted ? 'uncounted' : p.stock <= 0 ? 'out' : p.isLow ? 'low' : 'ok')
        const margin = (p: Product) => (p.costPrice && p.sellingPrice > 0 ? Math.round(((p.sellingPrice - p.costPrice) / p.sellingPrice) * 1000) / 10 : null)
        const daysLeft = (p: Product) => ((p.dailySales ?? 0) > 0 && p.stock > 0 ? Math.floor(p.stock / p.dailySales!) : null)
        return (
          <div className="hidden lg:block">
            <DataTable label={t('items2.colName')} rows={shown} rowKey={(p) => p.id} download="items" onRowClick={(p) => navigate(`/p/${p.id}`)}
              columns={[
                { key: 'pic', header: '', width: 'w-12', render: (p) => { const th = imageUrl(shopId, p.id, p.imageAt, 'thumb'); const art = itemArt({ name: p.name })
                  return th ? <img src={th} alt="" loading="lazy" width={36} height={36} className="h-9 w-9 rounded-lg border border-line bg-white object-contain" />
                    : <span aria-hidden className="art-tint flex h-9 w-9 items-center justify-center rounded-lg text-lg" style={{ background: art.tint }}>{art.emoji}</span> } },
                { key: 'name', header: t('items2.colName'), sortValue: (p) => itemName(p), filter: { kind: 'text', value: (p) => `${itemName(p)} ${p.barcode ?? ''}` },
                  render: (p) => <span className="flex items-center gap-2 font-semibold">{itemName(p)}{p.pending && <PendingDot />}</span>, csv: (p) => itemName(p) },
                { key: 'cat', header: t('items2.colCategory'), width: 'w-40', sortValue: (p) => p.category ?? '',
                  filter: { kind: 'select', value: (p) => p.category ?? '', options: categories.map((c) => ({ value: c, label: c })) },
                  render: (p) => <span className="text-muted">{p.category ?? '—'}</span>, csv: (p) => p.category ?? '' },
                { key: 'price', header: t('items2.colPrice'), align: 'right', width: 'w-28', sortValue: (p) => p.sellingPrice,
                  render: (p) => (p.sellingPrice > 0 ? <><b>{formatRupees(p.sellingPrice)}</b><span className="text-xs text-muted">/{t(`units.${p.unit}`)}</span></> : <span className="text-xs font-semibold text-udhaar">{t('stock.noPrice')}</span>), csv: (p) => p.sellingPrice },
                { key: 'cost', header: t('itemTable.cost'), align: 'right', width: 'w-24', sortValue: (p) => p.costPrice ?? null, render: (p) => (p.costPrice ? formatRupees(p.costPrice) : '—'), csv: (p) => p.costPrice ?? '' },
                { key: 'margin', header: t('itemTable.margin'), align: 'right', width: 'w-24', sortValue: (p) => margin(p),
                  render: (p) => { const m = margin(p); return m == null ? '—' : <span className={m < 0 ? 'font-semibold text-due' : m < 5 ? 'text-warn' : ''}>{m}%</span> }, csv: (p) => margin(p) ?? '' },
                { key: 'stock', header: t('items2.colStock'), align: 'right', width: 'w-32', sortValue: (p) => (p.uncounted || p.isService ? null : p.stock),
                  filter: { kind: 'select', value: stockState, options: (['out', 'low', 'ok', 'uncounted'] as const).map((st) => ({ value: st, label: t(`itemTable.stock.${st}`) })) },
                  render: (p) => <span className="flex justify-end"><StockPill product={p} /></span>, csv: (p) => (p.uncounted ? '' : p.stock) },
                { key: 'days', header: t('reorderTable.daysLeft'), align: 'right', width: 'w-24', sortValue: (p) => daysLeft(p),
                  render: (p) => { const d = daysLeft(p); return d == null ? '—' : <span className={d <= 3 ? 'font-semibold text-due' : d <= 10 ? 'text-warn' : ''}>{d}</span> }, csv: (p) => daysLeft(p) ?? '' },
                { key: 'barcode', header: t('itemTable.barcode'), width: 'w-36', render: (p) => <span className="font-mono text-xs text-muted">{p.barcode ?? '—'}</span>, csv: (p) => p.barcode ?? '' },
              ]} />
          </div>
        )
      })()}
      <ul className={`divide-y divide-[#edf2f1] overflow-hidden rounded-card bg-white shadow-sm lg:hidden ${filter === 'suggested' ? 'hidden' : ''}`}>
        {shown.length > 0 && (
          <li aria-hidden className="hidden items-center gap-3 bg-[#f6faf9] px-3 py-2 text-[13px] font-semibold text-muted lg:flex">
            <span className="w-[52px] shrink-0" />
            <span className="flex-1">{t('items2.colName')}</span>
            <span className="w-28 text-right">{t('items2.colPrice')}</span>
            <span className="w-44">{t('items2.colCategory')}</span>
            <span className="w-28 text-right">{t('items2.colStock')}</span>
          </li>
        )}
        {groupStyles(shown).map((entry) => entry.kind === 'style' ? (
          <li key={`style-${entry.name}`} className="flex items-start gap-3 px-3 py-2.5">
            {(() => { const first = entry.items.find((x) => x.imageAt) ?? entry.items[0]; const th = imageUrl(shopId, first.id, first.imageAt, 'thumb'); const art = itemArt({ name: entry.name })
              return th ? <img src={th} alt="" loading="lazy" width={52} height={52} className="h-[52px] w-[52px] shrink-0 rounded-xl border border-line bg-white object-contain" />
                : <span aria-hidden className="art-tint flex h-[52px] w-[52px] shrink-0 items-center justify-center rounded-xl text-[26px]" style={{ background: art.tint }}>{art.emoji}</span> })()}
            <div className="flex min-w-0 flex-1 flex-col gap-1.5">
              <span className="flex items-baseline justify-between gap-2">
                <span className="truncate text-[16px] font-semibold">{entry.name}</span>
                <span className="shrink-0 text-[13px] text-muted">{t('styles.total', { count: entry.items.reduce((n, x) => n + Math.max(0, x.stock), 0) })}</span>
                {isOwner && <button onClick={() => navigate(`/tags/${entry.items.map((x) => x.id).join(',')}`)} className="min-h-9 shrink-0 rounded-lg border border-line px-2 text-[13px] font-semibold">{t('tags.short')}</button>}
                {isOwner && <button onClick={() => setReceiving(entry.name)} className={`min-h-9 shrink-0 px-2.5 text-[13px] rounded-xl ${btn.solid}`}>{products.some((p) => p.styleName?.trim() === entry.name && p.sellingPrice <= 0) ? t('styles.setPrice') : t('styles.receive')}</button>}
              </span>
              <span className="flex flex-wrap gap-1.5">
                {entry.items.map((x) => (
                  <button key={x.id} onClick={() => navigate(`/p/${x.id}`)}
                    className={`min-h-9 rounded-lg border px-2 text-[13px] font-semibold ${!x.uncounted && x.stock <= 0 ? 'border-red-200 bg-due-soft text-due' : x.isLow ? 'border-amber-200 bg-warn-soft text-udhaar' : 'border-line bg-white'}`}>
                    {variantLabel(x) || x.name} <span className="font-normal">· {x.stock}</span>
                  </button>
                ))}
              </span>
            </div>
          </li>
        ) : ((p) => {
          const thumb = imageUrl(shopId, p.id, p.imageAt, 'thumb')
          const art = itemArt({ name: p.name })
          return (
            <li key={p.id} className="flex items-center gap-3 px-3 py-2.5 lg:py-1.5 lg:hover:bg-[#f6faf9]">
              {thumb ? (
                <button onClick={() => setZoom({ src: imageUrl(shopId, p.id, p.imageAt)!, name: p.name })} aria-label={t('pick.zoom', { name: p.name })} title={t('pick.zoom', { name: p.name })}
                  className="shrink-0 cursor-zoom-in rounded-xl transition-transform hover:scale-110">
                  <img src={thumb} alt="" loading="lazy" width={52} height={52} className="h-[52px] w-[52px] rounded-xl border border-line bg-white object-contain" />
                </button>
              ) : (
                <span aria-hidden className="art-tint flex h-[52px] w-[52px] shrink-0 items-center justify-center rounded-xl text-[26px]" style={{ background: art.tint }}>{art.emoji}</span>
              )}
              <button onClick={() => navigate(`/p/${p.id}`)} className="flex min-h-14 min-w-0 flex-1 items-center gap-2 text-left">
                <span className="flex min-w-0 flex-1 flex-col">
                  <span className="flex items-center gap-2 text-[16px] font-semibold">
                    <span className="truncate">{itemName(p)}</span>
                    {p.pending && <PendingDot />}
                  </span>
                  {(p.dailySales ?? 0) > 0 && p.stock > 0 && p.stock / p.dailySales! <= 10 && !p.isService && (
                    <span className={`w-fit rounded-full px-2 text-[11px] font-semibold ${p.stock / p.dailySales! <= 3 ? 'bg-due-soft text-due' : 'bg-warn-soft text-warn'}`}>⏳ {t('sl.daysLeft', { count: Math.floor(p.stock / p.dailySales!) })}</span>
                  )}
                  <span className="truncate text-[13px] text-muted lg:hidden">
                    {p.sellingPrice > 0 ? <b className="font-bold text-ink">{formatRupees(p.sellingPrice)}</b> : <span className="font-semibold text-udhaar">{t('stock.noPrice')}</span>}
                    {p.sellingPrice > 0 && `/${t(`units.${p.unit}`)}`}
                    {p.category && ` · ${p.category}`}
                  </span>
                </span>
                <span className="hidden w-28 shrink-0 text-right lg:block">
                  {p.sellingPrice > 0 ? <><b>{formatRupees(p.sellingPrice)}</b><span className="text-[13px] text-muted">/{t(`units.${p.unit}`)}</span></> : <span className="text-[13px] font-semibold text-udhaar">{t('stock.noPrice')}</span>}
                </span>
                <span className="hidden w-44 shrink-0 truncate text-[14px] text-muted lg:block">{p.category ?? '—'}</span>
                <span className="flex shrink-0 justify-end lg:w-28"><StockPill product={p} /></span>
                <span aria-hidden className="text-muted lg:hidden">›</span>
              </button>
            </li>
          )
        })(entry.product))}
      </ul>
      <Button className="fixed right-[max(1rem,calc(50vw-17rem))] bottom-24 h-14 lg:hidden rounded-full! px-6 shadow-[0_6px_16px_rgba(15,118,110,0.35)]" onClick={() => setAdding(true)}>
        + {t('addItem')}
      </Button>
      {adding && <AddProductSheet shopId={shopId} onClose={() => setAdding(false)} />}
      {receiving && <ReceiveSizesSheet shopId={shopId} styleName={receiving} items={sortVariants(products.filter((p) => p.styleName?.trim() === receiving))} onClose={() => setReceiving(null)} />}
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
  if (product.isService || product.uncounted || isOutToday(product.outOn)) return <StockBadge product={product} />
  const out = product.stock <= 0
  const tone = out ? 'bg-due-soft text-due' : product.isLow ? 'bg-warn-soft text-udhaar' : 'bg-brand-soft text-brand-strong'
  return (
    <span className={`flex shrink-0 flex-col items-center rounded-xl px-2.5 py-1 ${tone}`}>
      <span className="text-[17px] leading-tight font-bold">{product.stock} {t(`units.${product.unit}`, { count: product.stock })}</span>
      <span className="text-[11px] font-semibold">{out ? t('stock.out') : product.isLow ? t('stock.low') : t('stock.inStock')}</span>
    </span>
  )
}

/** Stock with words, not colour alone: "Out" (red), "Low" (orange), or "In stock". */
export function StockBadge({ product, large = false }: { product: Product; large?: boolean }) {
  const { t } = useTranslation()
  if (product.isService) {
    return <span className="shrink-0 text-right text-sm font-semibold text-brand-strong">{t('salon.serviceBadge')}{product.durationMinutes ? ` · ${t('salon.minutesShort', { count: product.durationMinutes })}` : ''}</span>
  }
  if (isOutToday(product.outOn)) return <span className="shrink-0 text-right text-sm font-semibold text-due">{t('stock.outToday')}</span>
  if (product.uncounted) return <span className="shrink-0 text-right text-sm font-semibold text-muted">{t('stock.notCounted')}</span>
  const out = product.stock <= 0
  const tone = out ? 'text-due' : product.isLow ? 'text-udhaar' : 'text-ink'
  return (
    <span className={`flex shrink-0 flex-col items-end ${tone}`}>
      <span className={`${large ? 'text-4xl' : 'text-lg'} font-bold`}>
        {product.stock} {t(`units.${product.unit}`, { count: product.stock })}
      </span>
      <span className="text-xs font-semibold">{out ? t('stock.out') : product.isLow ? t('stock.low') : t('stock.inStock')}</span>
    </span>
  )
}

function Chip({ selected, onClick, children, tone = 'plain' }: { selected: boolean; onClick: () => void; children: ReactNode; tone?: 'plain' | 'warn' | 'due' }) {
  const on = tone === 'warn' ? 'bg-udhaar text-white' : tone === 'due' ? 'bg-due text-white' : 'bg-brand text-white shadow-sm'
  return (
    <button onClick={onClick} aria-pressed={selected} className={`h-11 shrink-0 rounded-full px-4 text-[15px] font-semibold ${selected ? on : 'border border-line bg-white text-ink'}`}>
      {children}
    </button>
  )
}

function AddProductSheet({ shopId, onClose }: { shopId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const [name, setName] = useState('')
  const [unit, setUnit] = useState<Unit>('piece')
  const [price, setPrice] = useState('')
  const [cost, setCost] = useState('')
  const [category, setCategory] = useState('')
  const [opening, setOpening] = useState('')
  const [reorder, setReorder] = useState('')
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
        <CategoryInput shopId={shopId} value={category} onChange={setCategory} onPick={(c) => {
          // The category's defaults fill what is still empty.
          if (c.defaultUnit && !name) setUnit(c.defaultUnit)
          if (c.defaultGstRate != null && gst === '') setGst(String(c.defaultGstRate))
          if (c.defaultHsn && hsn === '') setHsn(c.defaultHsn)
        }} />
        <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={service} onChange={(e) => setService(e.target.checked)} className="h-5 w-5" />{t('salon.isService')}</label>
        {service && <Field label={t('salon.min')} value={minutes} onChange={(e) => setMinutes(e.target.value)} inputMode="numeric" />}
        {!service && <div className="grid grid-cols-2 gap-2">
          <Field label={t('stock.openingStock')} value={opening} onChange={(e) => setOpening(e.target.value)} inputMode="decimal" />
          <Field label={t('stock.reorderLevel')} value={reorder} onChange={(e) => setReorder(e.target.value)} inputMode="decimal" />
        </div>}
        <div className="grid grid-cols-2 gap-2">
          <label className="flex flex-col gap-1">
            <span className="text-sm font-medium text-muted">{t('stock.gst')}</span>
            <select value={gst} onChange={(e) => setGst(e.target.value)} className="h-12 rounded-xl border border-line bg-white px-3 text-lg">
              <option value="">{t('stock.gstNotSet')}</option>
              {GST_RATES.map((rate) => <option key={rate} value={rate}>{rate}%</option>)}
            </select>
          </label>
          <Field label={t('stock.hsn')} value={hsn} onChange={(e) => setHsn(e.target.value)} inputMode="numeric" maxLength={8} />
        </div>
        <div className="grid grid-cols-[1fr_2fr] gap-2">
          <Button type="button" tone="plain" onClick={onClose}>{t('cancel')}</Button>
          <Button type="submit" disabled={!name.trim() || !(Number(price) > 0)}>{t('save')}</Button>
        </div>
      </form>
    </Sheet>
  )
}

/**
 * New stock for one style, size by size: a box per size/colour, saved in one go as "goods in" for each (works
 * offline like any stock entry). An optional buying price per piece is kept on each as its cost.
 */
function ReceiveSizesSheet({ shopId, styleName, items, onClose }: { shopId: string; styleName: string; items: Product[]; onClose: () => void }) {
  const { t } = useTranslation()
  const [qty, setQty] = useState<Record<string, string>>({})
  const [cost, setCost] = useState('')
  // One selling price for every size (sizes from the suggested list come in with none).
  const [price, setPrice] = useState(items.every((p) => p.sellingPrice === items[0]?.sellingPrice) && (items[0]?.sellingPrice ?? 0) > 0 ? String(items[0].sellingPrice) : '')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const queryClient = useQueryClient()
  const [supplierId, setSupplierId] = useState('')
  // Owner only: helpers get an empty list and stock goes in by hand.
  const suppliers = useQuery({ queryKey: [shopId, 'suppliers', 'pick'], queryFn: () => api<{ id: string; name: string; isActive?: boolean }[]>('/v1/suppliers'), retry: false }).data ?? []
  const category = items[0]?.category
  const total = Object.values(qty).reduce((n, v) => n + (Number(v) || 0), 0)
  const priceChanged = Number(price) > 0 && items.some((p) => p.sellingPrice !== Number(price))
  async function save() {
    setBusy(true)
    setError('')
    if (priceChanged) {
      try {
        await api('/v1/products/style-price', 'POST', { styleName, sellingPrice: Number(price) })
        await queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
      } catch (e) {
        setError(shownError(e))
        setBusy(false)
        return
      }
    }

    if (supplierId && total > 0) {
      try {
        await api('/v1/goods-receipts', 'POST', { id: crypto.randomUUID(), supplierId, note: null,
          lines: items.filter((p) => Number(qty[p.id]) > 0).map((p) => ({ productId: p.id, quantity: Number(qty[p.id]), bags: null })) })
        await queryClient.invalidateQueries({ queryKey: [shopId] })
      } catch (e) {
        setError(shownError(e))
        setBusy(false)
        return
      }
      onClose()
      return
    }
    for (const p of items) {
      const n = Number(qty[p.id]) || 0
      if (n > 0) await addStockMovement(shopId, { id: p.id, name: p.name }, 'purchase', n, null, Number(cost) > 0 ? Number(cost) : null, null)
    }
    onClose()
  }
  return (
    <Sheet title={t('styles.receiveTitle', { name: styleName })} onClose={onClose}>
      <p className="-mt-1 mb-3 text-sm text-muted">{t('styles.receiveHint')}</p>
      <div className="grid grid-cols-3 gap-2">
        {items.map((p) => (
          <label key={p.id} className="flex flex-col gap-1 rounded-xl border border-line p-2">
            <span className="text-center font-extrabold">{variantLabel(p) || p.name}</span>
            <span className="text-center text-xs text-muted">{t('styles.left', { count: p.stock })}</span>
            <input value={qty[p.id] ?? ''} onChange={(e) => setQty({ ...qty, [p.id]: e.target.value })} inputMode="numeric" placeholder="0"
              aria-label={`${t('styles.receive')} ${variantLabel(p)}`} className="h-11 rounded-lg border border-line text-center text-lg" />
          </label>
        ))}
      </div>
      <div className="mt-3 flex flex-wrap gap-3">
        <label className="block">
          <span className="mb-1 block text-sm font-semibold text-muted">{t('styles.priceAll')}</span>
          <input value={price} onChange={(e) => setPrice(e.target.value)} inputMode="decimal" placeholder="₹" className="h-12 w-40 rounded-xl border border-line px-3 text-lg" />
        </label>
        {!supplierId && (
          <label className="block">
            <span className="mb-1 block text-sm font-semibold text-muted">{t('styles.costEach')}</span>
            <input value={cost} onChange={(e) => setCost(e.target.value)} inputMode="decimal" className="h-12 w-40 rounded-xl border border-line px-3 text-lg" />
          </label>
        )}
      </div>
      <p className="mt-3 text-sm"><span className="text-muted">{t('stock.inCategory')}: </span><b>{category || t('stock.noCategory')}</b></p>
      {suppliers.length > 0 && (
        <label className="mt-2 block">
          <span className="mb-1 block text-sm font-semibold text-muted">{t('stock.fromSupplier')}</span>
          <Select value={supplierId} onChange={(e) => setSupplierId(e.target.value)} className="w-full text-base">
            <option value="">{t('stock.noSupplier')}</option>
            {suppliers.filter((x) => x.isActive !== false).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
          </Select>
          <span className="mt-1 block text-xs text-muted">{t(supplierId ? 'stock.supplierHint' : 'stock.byHandHint')}</span>
        </label>
      )}
      {error && <p className="mt-2 text-sm text-due">{error}</p>}
      <div className="mt-4 grid grid-cols-[1fr_2fr] gap-2">
        <Button type="button" tone="plain" onClick={onClose}>{t('cancel')}</Button>
        <Button type="button" onClick={() => void save()} disabled={busy || (total <= 0 && !priceChanged)}>{total > 0 ? t('styles.receiveSave', { count: total }) : t('styles.savePrice')}</Button>
      </div>
    </Sheet>
  )
}

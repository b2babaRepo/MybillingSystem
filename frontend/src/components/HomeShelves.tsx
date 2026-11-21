import { useQueries, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { addToCart, type SavedShop } from '../lib/customer'
import { formatRupees } from '../lib/format'
import { imageUrl } from '../lib/images'
import { itemArt } from '../lib/itemArt'
import type { Order } from '../lib/planner'
import { useSavedItems } from '../lib/savedItems'
import { navigate } from '../router'
import { shopTint } from './CustIcons'
import { MetalRates, type StoreRate } from './MetalRates'

interface ShelfItem { id: string; name: string; price: number; regularPrice?: number | null; available: number; imageAt?: string | null; catalogueItemId?: number | null; addedAt?: string | null }
interface ShelfStore { shopId: string; name: string; slug: string; items: ShelfItem[]; rates?: StoreRate[] | null }

/** Shop kinds whose customers mostly rebuy the same things: their shelf is the usual items, not offers or new arrivals. */
const REBUY_TRADES = new Set(['kirana', 'dairy', 'medical', 'water_supply'])
const NEW_DAYS = 21

/**
 * Home's shelves from the customer's own shops, for every trade: what they usually buy (one tap back into that
 * shop's cart), what they saved, and one card per saved shop that suits it (new arrivals for clothes, toys or
 * jewellery; offers where there are some).
 */
export function HomeShelves({ saved, orders }: { saved: SavedShop[]; orders: Order[] }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const stores = useQueries({ queries: saved.map((s) => ({ queryKey: ['store', s.slug], queryFn: () => fetch(`/api/v1/store/${s.slug}`).then((r) => (r.ok ? (r.json() as Promise<ShelfStore>) : null)), staleTime: 60_000 })) })
  const hearts = useSavedItems().data ?? []
  const [added, setAdded] = useState<Record<string, boolean>>({})

  // Usual items: what this customer ordered most, across the shops, that is in stock now; last quantity ordered.
  const counts = new Map<string, { n: number; quantity: number }>()
  for (const o of [...orders].sort((a, b) => a.createdAt.localeCompare(b.createdAt))) {
    if (o.status === 'cancelled') continue
    for (const l of o.lines) {
      const id = l.requestedProductId ?? l.productId
      if (!id || l.removed) continue
      counts.set(id, { n: (counts.get(id)?.n ?? 0) + 1, quantity: l.quantity })
    }
  }
  const loaded = stores.map((r) => r.data).filter((s): s is ShelfStore => !!s)
  const usual = loaded.flatMap((s) => s.items.filter((i) => counts.has(i.id) && i.available > 0).map((i) => ({ item: i, store: s, n: counts.get(i.id)!.n, quantity: counts.get(i.id)!.quantity })))
    .sort((a, b) => b.n - a.n).slice(0, 12)

  async function add(store: ShelfStore, item: ShelfItem, quantity: number) {
    setAdded((a) => ({ ...a, [item.id]: true }))
    try {
      await addToCart(store.shopId, { [item.id]: Math.min(quantity, item.available) })
      await queryClient.invalidateQueries({ queryKey: ['me', 'carts'] })
    } catch {
      setAdded((a) => ({ ...a, [item.id]: false }))
    }
  }

  const picture = (shopId: string, item: { id: string; name: string; imageAt?: string | null; catalogueItemId?: number | null }) => {
    const src = imageUrl(shopId, item.id, item.imageAt, 'thumb')
    if (src) return <img src={src} alt="" loading="lazy" className="h-full w-full object-contain" />
    const art = itemArt({ catalogueItemId: item.catalogueItemId, name: item.name })
    return <span aria-hidden className="flex h-full w-full items-center justify-center text-3xl" style={{ background: art.tint }}>{art.emoji}</span>
  }
  const tile = (key: string, shopId: string, item: ShelfItem | { id: string; name: string; price: number; imageAt?: string | null }, under: string | null, action: React.ReactNode, onOpen: () => void) => (
    <div key={key} className="flex w-32 shrink-0 flex-col gap-1 rounded-2xl bg-white p-2 shadow-[0_1px_4px_rgba(16,32,29,0.06)]">
      <button onClick={onOpen} aria-label={item.name} className="aspect-square overflow-hidden rounded-xl bg-[#f6f8f7]">{picture(shopId, item)}</button>
      <span className="line-clamp-2 min-h-[2.4em] text-xs font-semibold">{item.name}</span>
      <span className="flex items-baseline gap-1.5">
        <b className="text-sm">{formatRupees(item.price)}</b>
        {'regularPrice' in item && item.regularPrice && item.regularPrice > item.price && <s className="text-[11px] text-muted">{formatRupees(item.regularPrice)}</s>}
      </span>
      {under && <span className="truncate text-[11px] text-muted">{under}</span>}
      {action}
    </div>
  )
  const shelf = (title: string, children: React.ReactNode, more?: { label: string; to: string }) => (
    <section className="flex flex-col gap-2" aria-label={title}>
      <div className="flex items-baseline justify-between">
        <h2 className="text-[17px] font-extrabold">{title}</h2>
        {more && <button onClick={() => navigate(more.to)} className="min-h-11 text-sm font-bold text-brand">{more.label}</button>}
      </div>
      <div className="-mx-4 flex gap-2.5 overflow-x-auto px-4 pb-1">{children}</div>
    </section>
  )

  // One card per saved shop that isn't a rebuy trade: its new arrivals, else its offers.
  const shopCards = saved.map((s) => {
    const store = loaded.find((x) => x.slug === s.slug)
    if (!store || REBUY_TRADES.has(s.type)) return null
    const fresh = store.items.filter((i) => i.addedAt && Date.now() - Date.parse(i.addedAt) < NEW_DAYS * 86_400_000 && i.available > 0)
      .sort((a, b) => Date.parse(b.addedAt!) - Date.parse(a.addedAt!))
    const offers = store.items.filter((i) => i.regularPrice && i.regularPrice > i.price && i.available > 0)
    const [label, items] = fresh.length > 0 ? [t('cust4.newArrivals'), fresh] : offers.length > 0 ? [t('cust4.offers'), offers] : [store.rates ? t('cust5.todayRate') : null, []]
    if (!label) return null
    return { s, store, label, items: items.slice(0, 10) }
  }).filter((x) => !!x)

  return (
    <>
      {usual.length > 0 && shelf(t('cust4.usual'), usual.map(({ item, store, quantity }) => tile(`u-${item.id}`, store.shopId, item, store.name,
        <button onClick={() => void add(store, item, quantity)} disabled={added[item.id]} className={`h-9 rounded-lg text-sm font-bold ${added[item.id] ? 'bg-brand-soft text-brand-deep' : 'border-2 border-brand text-brand'}`}>
          {added[item.id] ? `✓ ${t('cust5.added')}` : t('store.add')}
        </button>, () => navigate(`/s/${store.slug}`))))}

      {shopCards.map(({ s, store, label, items }) => (
        <section key={s.id} className="flex flex-col gap-2 rounded-card bg-white p-3 shadow-[0_1px_4px_rgba(16,32,29,0.06)]" aria-label={`${s.name}: ${label}`}>
          <button onClick={() => navigate(`/s/${s.slug}`)} className="flex min-h-11 items-center gap-2.5 text-left">
            <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl font-extrabold" style={shopTint(s.id)}>{s.name.charAt(0)}</span>
            <span className="flex min-w-0 flex-1 flex-col">
              <span className="truncate font-extrabold">{s.name}</span>
              {store.rates ? <MetalRates rates={store.rates} compact /> : <span className="text-xs font-semibold text-brand">{label}</span>}
            </span>
            <span className="shrink-0 text-sm font-bold text-brand">{t('cust.openShop')} ›</span>
          </button>
          {items.length > 0 && <div className="-mx-3 flex gap-2 overflow-x-auto px-3 pb-1">
            {items.map((i) => (
              <button key={i.id} onClick={() => navigate(`/s/${s.slug}`)} className="flex w-24 shrink-0 flex-col gap-1 text-left">
                <span className="aspect-square overflow-hidden rounded-xl bg-[#f6f8f7]">{picture(store.shopId, i)}</span>
                <span className="line-clamp-1 text-xs font-semibold">{i.name}</span>
                <b className="text-xs">{formatRupees(i.price)}</b>
              </button>
            ))}
          </div>}
        </section>
      ))}

      {hearts.length > 0 && shelf(t('cust4.savedItems'), hearts.slice(0, 12).map((h) => tile(`h-${h.shopId}-${h.productId}`, h.shopId, { id: h.productId, name: h.name, price: h.price, imageAt: h.imageAt }, h.available > 0 ? h.shopName : t('store.out'),
        null, () => navigate(`/s/${h.shopSlug}`))), { label: t('cust.seeAll'), to: '/saved' })}
    </>
  )
}

import { CustIcon } from '../../components/CustIcons'
import { SalonBooking } from '../../components/SalonBooking'
import { StartDeliverySheet } from '../../components/DeliveryPlans'
import { DELIVERY_TRADES } from '../../lib/deliveries'
import { takeBookAgain } from '../../lib/salon'
import { MetalRates, type StoreRate } from '../../components/MetalRates'
import { StorePieces, type StorePiece } from '../../components/StorePieces'
import { putCart, useCarts, useSavedShops, useSaveShop } from '../../lib/customer'
import { api } from '../../lib/api'
import { aisleName } from '../../lib/aisleNames'
import { btn } from '../../lib/buttonStyles'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useAisles } from '../../lib/aisles'
import { ItemCard } from '../../components/ItemCard'
import { allPhotos, imageUrl } from '../../lib/images'
import { PhotoGallery } from '../../components/PhotoGallery'
import { aisleOf } from '../../lib/itemArt'
import { categoryOf, nameIn } from '../../lib/categoryTree'
import { useMenuTree } from '../../lib/menuConfig'
import { nextLanguage, setLanguage } from '../../i18n'
import { formatRupees, whatsAppLink } from '../../lib/format'
import { uuidv7 } from '../../lib/ids'
import { round2 } from '../../lib/local'
import type { Order } from '../../lib/planner'
import type { ProblemDetails, Unit } from '../../lib/types'
import { navigate } from '../../router'
import { getSession } from '../../lib/session'
import { AlertsPrompt, BillSummary, OrderSteps, PaymentPicker, SlotChips, UpiPay } from '../../components/OrderPay'
import { billOf, isOpen, noSettings, useCheckoutInfo, type KhataAtCheckout, type StoreSettings } from '../../lib/orderFlow'
import { ProductSheet } from '../../components/ProductSheet'
import { update, usePwa } from '../../lib/pwa'
import { fullPageTrades } from '../../lib/productSheet'
import { Drawer } from '../../components/ui'
import { rank, rememberSearch } from '../../lib/search'
import { useHeart, useSavedItems } from '../../lib/savedItems'
import { api as apiCall } from '../../lib/api'
import { festive } from '../../lib/festive'
import type { Fulfilment, OrderPayment } from '../../lib/planner'
import { useNow } from '../../lib/useNow'

// The shop's public online store at #/s/{slug}: no account, no app install. Guests browse, fill a cart, check
// out with name, phone and (for delivery) an address, then follow the order by its private link.

interface StoreItem {
  id: string
  name: string
  category: string | null
  unit: Unit
  price: number
  /** Before the shop's sale offer, when one is on; then price is the offer price. */
  regularPrice?: number | null
  offerName?: string | null
  available: number
  catalogueItemId?: number | null
  imageAt?: string | null
  mrp?: number | null
  packSize?: number | null
  packUnit?: Unit | null
  imageCredit?: string | null
  extraPhotos?: string[] | null
  /** When the shop added it: "New arrivals". */
  addedAt?: string | null
  /** A salon service: how long it takes (set only on services). */
  serviceMinutes?: number | null
}

interface Store {
  name: string
  slug: string
  area: string | null
  address: string | null
  phone: string | null
  shopId: string
  items: StoreItem[]
  /** How the shop takes online orders: charge, free above, minimum, time chips. */
  settings?: StoreSettings | null
  upiId?: string | null
  /** The shop's trade: kirana browses by a left rail, clothes and jewellery by chips and filters and a full product page. */
  type?: string | null
  /** A jewellery shop's gold and silver rates of the day. */
  rates?: StoreRate[] | null
  /** Tagged pieces in stock with today's price, when the shop shows them online. */
  pieces?: StorePiece[] | null
}

/** What a line saves against MRP or the price before the shop's offer. */
const wasOf = (i: StoreItem) => (i.regularPrice && i.regularPrice > i.price ? i.regularPrice : i.mrp && i.mrp > i.price ? i.mrp : null)

interface Placed {
  order: Order
  statusToken: string
}

type Cart = Record<string, number>
interface MyOrder { id: string; token: string; at: string }

const STEP: Record<Unit, number> = { piece: 1, packet: 1, kg: 0.5, litre: 0.5, g: 100, ml: 100, bag: 1, tonne: 0.5, brass: 0.5, cft: 10, metre: 1, pair: 1, dozen: 1, box: 1, jar: 1, sq_ft: 10, running_ft: 10, quintal: 1, carton: 1, bottle: 1, set: 1, strip: 1 }

/** Per-store memory on this phone: the cart, the customer's details, and their orders' private links. */
function remembered<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key)
    return raw ? (JSON.parse(raw) as T) : fallback
  } catch {
    return fallback
  }
}

function remember(key: string, value: unknown) {
  try {
    localStorage.setItem(key, JSON.stringify(value))
  } catch {
    // Private mode: works for this visit only.
  }
}

async function storeApi<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
  const response = await fetch(`/api/v1/store/${path}`, {
    method,
    // A signed-in customer's order goes on their account too, so it shows under their own orders.
    headers: { ...(body === undefined ? {} : { 'Content-Type': 'application/json' }), ...(getSession()?.accessToken ? { Authorization: `Bearer ${getSession()!.accessToken}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as ProblemDetails | null
    const first = problem?.errors ? Object.values(problem.errors)[0]?.[0] : undefined
    throw new Error(first ?? problem?.title ?? `Error ${response.status}`)
  }
  return (await response.json()) as T
}

/** Counts a step for the platform's totals (no items or details), at most once a day per shop on this phone. */
function seen(slug: string, kind: 'cart' | 'checkout') {
  const key = `seen.${kind}.${slug}`
  const today = new Date().toDateString()
  try {
    if (localStorage.getItem(key) === today) return
    localStorage.setItem(key, today)
  } catch {
    // Storage off: still counted, maybe twice.
  }
  const token = getSession()?.accessToken
  void fetch(`/api/v1/store/${slug}/seen`, { method: 'POST', headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: JSON.stringify({ kind }) }).catch(() => {})
}

/** path: "/s/{slug}", "/s/{slug}/cart" (straight to checkout) or "/s/{slug}/o/{orderId}?t={token}". */
export function StoreApp({ path }: { path: string }) {
  const match = path.match(/^\/s\/([a-z0-9-]+)(\/cart)?(?:\/o\/([0-9a-f-]+))?(?:\?t=([A-Za-z0-9_-]+))?$/)
  if (!match) return <NotFound />
  const [, slug, toCart, orderId, token] = match
  return orderId && token ? <OrderStatus slug={slug} orderId={orderId} token={token} /> : <Catalogue key={slug} slug={slug} openCheckout={!!toCart} />
}

/**
 * A shop's page takes a new app version by itself: it shows no "Update" banner, and customers keep it open for days.
 * The cart and the customer's details are kept on the phone, so the reload loses nothing; not while checkout is open.
 */
function useAutoUpdate(paused: boolean) {
  const { updateReady } = usePwa()
  useEffect(() => { if (updateReady && !paused) update() }, [updateReady, paused])
}

function NotFound() {
  const { t } = useTranslation()
  return <p className="p-8 text-center text-muted">{t('store.notFound')}</p>
}

function Header({ title, subtitle, shopId, theme }: { title: string; subtitle?: string | null; shopId?: string; theme?: string | null }) {
  const { t, i18n } = useTranslation()
  const signedIn = !!getSession()
  const saved = useSavedShops(signedIn).data?.some((s) => s.id === shopId) ?? false
  const save = useSaveShop()
  return (
    <header className={`sticky top-0 z-10 flex items-center gap-2 px-4 py-3 text-white lg:rounded-b-2xl lg:px-6 ${festive(theme)?.header ?? 'bg-brand'}`}>
      {/* Home on every shop page: a signed-in customer goes back to their shops, anyone else to the app's start. */}
      <button onClick={() => navigate('/')} aria-label={t('store.home')} className="-ml-1 flex h-11 shrink-0 items-center gap-1 rounded-full bg-white/15 px-3 text-sm font-semibold hover:bg-white/25">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" aria-hidden><path d="M3 11l9-8 9 8M5 10v10h5v-6h4v6h5V10" /></svg>
        {t('store.home')}
      </button>
      <div className="flex min-w-0 flex-1 flex-col">
        <h1 className="truncate text-xl font-bold">{festive(theme) ? `${festive(theme)!.emoji} ` : ''}{title}</h1>
        {subtitle && <span className="truncate text-sm opacity-85">{subtitle}</span>}
      </div>
      {signedIn && shopId && (
        <button onClick={() => void save({ id: shopId }, !saved)} aria-pressed={saved} aria-label={t(saved ? 'cust.unsave' : 'cust.save')} className="h-11 w-11 shrink-0 rounded-full bg-white/20 text-xl">{saved ? '♥' : '♡'}</button>
      )}
      <button onClick={() => setLanguage(nextLanguage(i18n.language).code)} className="h-11 rounded-full bg-white/20 px-3 text-sm font-semibold">
        {nextLanguage(i18n.language).short}
      </button>
    </header>
  )
}

/** Who is signed in on a shop's page: a customer, or the shop's own people looking at it as customers do. */
function SignedInAs({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const session = getSession()
  if (!session) return null
  const own = session.memberships?.some((m) => m.shopId === shopId)
  return (
    <p className="mx-4 mt-2 flex min-h-9 items-center justify-between gap-2 rounded-xl bg-brand-soft px-3 text-sm">
      <span className="min-w-0 truncate">{own ? t('storeWho.yourShop') : t('storeWho.customer', { name: session.user.fullName })}</span>
      {own && <button onClick={() => navigate('/')} className="shrink-0 font-semibold text-brand underline">{t('storeWho.openShop')}</button>}
    </p>
  )
}

function Catalogue({ slug, openCheckout = false }: { slug: string; openCheckout?: boolean }) {
  // The category tree as the platform admin has set it.
  const tree = useMenuTree()
  const nowMs = useNow()
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const store = useQuery({ queryKey: ['store', slug], queryFn: () => storeApi<Store>(slug), retry: false })
  const [cart, setCartState] = useState<Cart>(() => remembered(`store.${slug}.cart`, {}))
  const [category, setCategory] = useState('all')
  const [search, setSearch] = useState('')
  const [gallery, setGallery] = useState<StoreItem | null>(null)
  const [open, setOpen] = useState<StoreItem | null>(null)
  const [filters, setFilters] = useState({ open: false, sort: 'relevance' as 'relevance' | 'low' | 'high', inStock: false, max: '' })
  const [popped, setPopped] = useState<string | null>(null)
  const aisles = useAisles().data ?? []
  const [checkingOut, setCheckingOut] = useState(openCheckout)
  useEffect(() => { if (checkingOut) seen(slug, 'checkout') }, [checkingOut, slug])
  useAutoUpdate(checkingOut)
  const myOrders = remembered<MyOrder[]>(`store.${slug}.orders`, [])
  const signedIn = !!getSession()
  const accountCarts = useCarts(signedIn)
  const [merged, setMerged] = useState(false)
  const shopId = store.data?.shopId
  // Saved this shop: a good moment to offer order notifications (asked once per phone).
  const savedHere = useSavedShops(signedIn).data?.some((s) => s.id === shopId) ?? false
  const hearts = useSavedItems(signedIn).data ?? []
  const heart = useHeart()
  // "Your usual items": what this customer ordered here before, most often first.
  const pastOrders = useQuery({ queryKey: ['me', 'orders-lite'], queryFn: () => apiCall<Order[]>('/v1/me/orders'), enabled: signedIn, retry: false, staleTime: 60_000 }).data ?? []
  // Signed in: the cart lives on the account. What was added on this phone before signing in joins it once.
  useEffect(() => {
    if (!signedIn || merged || !shopId || !accountCarts.data) return
    const server: Cart = Object.fromEntries((accountCarts.data.find((c) => c.shopId === shopId)?.items ?? []).map((i) => [i.productId, i.quantity]))
    const local = remembered<Cart>(`store.${slug}.cart`, {})
    const joined: Cart = { ...server }
    for (const [id, q] of Object.entries(local)) joined[id] = Math.max(joined[id] ?? 0, q)
    // Syncs with the server and this phone's storage (below), which is what effects are for.
    // oxlint-disable-next-line react/set-state-in-effect
    setCartState(joined)
    setMerged(true)
    if (Object.keys(local).length > 0) { void putCart(shopId, joined).catch(() => {}); remember(`store.${slug}.cart`, {}) }
  }, [signedIn, merged, shopId, accountCarts.data, slug])
  const pushTimer = useRef<number | undefined>(undefined)
  // Signed in: whether this customer can put an order on khata here (shown with the shop's terms).
  const khataHere = useCheckoutInfo(shopId, signedIn).data?.khata
  // A salon: book a visit; "book again" from Home opens it with the last visit's services ticked.
  const [bookAgain] = useState(() => takeBookAgain(slug))
  const [booking, setBooking] = useState(!!bookAgain)
  const [planning, setPlanning] = useState(false)

  if (store.isError) return <NotFound />
  const data = store.data
  // Grey cards while the shop loads, instead of a blank page.
  if (!data) return (
    <div className="mx-auto max-w-xl p-3 lg:max-w-6xl" aria-busy="true">
      <div className="mb-3 h-14 animate-pulse rounded-xl bg-[#e3ebe9]" />
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-5">
        {Array.from({ length: 10 }, (_, i) => <div key={i} className="flex flex-col gap-2 rounded-card bg-white p-2"><div className="aspect-square animate-pulse rounded-xl bg-[#edf2f1]" /><div className="h-3 w-3/4 animate-pulse rounded bg-[#edf2f1]" /><div className="h-3 w-1/3 animate-pulse rounded bg-[#edf2f1]" /><div className="h-10 animate-pulse rounded-xl bg-[#edf2f1]" /></div>)}
      </div>
    </div>
  )

  const setCart = (next: Cart) => {
    if (Object.keys(cart).length === 0 && Object.keys(next).length > 0) seen(slug, 'cart')
    setCartState(next)
    if (!signedIn) { remember(`store.${slug}.cart`, next); return }
    // A moment after the last tap, the account's cart is updated.
    window.clearTimeout(pushTimer.current)
    pushTimer.current = window.setTimeout(() => void putCart(data.shopId, next).then(() => queryClient.invalidateQueries({ queryKey: ['me', 'carts'] })).catch(() => {}), 600)
  }
  /** A typed quantity, capped at what the shop has. */
  const setQuantity = (item: StoreItem, quantity: number) => {
    const updated = { ...cart }
    if (quantity <= 0) delete updated[item.id]
    else updated[item.id] = Math.min(round2(quantity), Math.max(item.available, 0))
    setCart(updated)
  }
  const change = (item: StoreItem, direction: 1 | -1) => {
    const next = round2((cart[item.id] ?? 0) + direction * STEP[item.unit])
    const updated = { ...cart }
    if (next <= 0) delete updated[item.id]
    else updated[item.id] = Math.min(next, Math.max(item.available, 0))
    setCart(updated)
    // A tick you can feel and see on Add.
    if (direction === 1) {
      try { navigator.vibrate?.(12) } catch { /* not on this phone */ }
      setPopped(item.id)
      window.setTimeout(() => setPopped((p) => (p === item.id ? null : p)), 350)
    }
  }
  const hearted = (id: string) => hearts.some((h) => h.shopId === data.shopId && h.productId === id)
  const toggleHeart = (id: string) => void heart(data.shopId, id, !hearted(id))
  const kirana = !data.type || data.type === 'kirana'
  const full = !!data.type && fullPageTrades.includes(data.type)

  // A kirana store browses by the same category tree as the shop's bill and order menus (lib/categoryTree): one chip
  // per category in department order, so "Atta & rice" and "आटा, चावल, दाल" no longer sit side by side. Other trades,
  // and a store whose items mostly fall outside the tree, browse by aisle (the common grocery list), then the shop's
  // own categories, and "Other" for the rest.
  const treeKey = (i: StoreItem) => { const c = categoryOf({ name: i.name, category: i.category }, tree); return c.key === 'other' ? 'other' : `tree:${c.key}` }
  const byTree = kirana && data.items.filter((i) => treeKey(i) !== 'other').length >= data.items.length / 2
  const aisleOfItem = (i: StoreItem) => (byTree ? treeKey(i) : aisleOf(i.catalogueItemId) ?? (i.category ? `cat:${i.category}` : 'other'))
  const stocked = new Set(data.items.map(aisleOfItem))
  const ownCategories = [...stocked].filter((a) => a.startsWith('cat:')).sort().map((a) => ({ code: a, name: a.slice(4), count: data.items.filter((i) => aisleOfItem(i) === a).length }))
  // Like the big shopping apps: every item shows at once as a photo card; chips narrow it, search finds by name.
  const treeChips = tree.flatMap((d) => d.categories).map((c) => ({ code: `tree:${c.key}`, name: nameIn(c, i18n.language), count: data.items.filter((i) => aisleOfItem(i) === `tree:${c.key}`).length })).filter((c) => c.count > 0)
  const chips = byTree ? [
    { code: 'all', name: t('store.all'), count: data.items.length },
    ...treeChips,
    ...(stocked.has('other') ? [{ code: 'other', name: t('store.otherItems'), count: data.items.filter((i) => aisleOfItem(i) === 'other').length }] : []),
  ] : [
    { code: 'all', name: t('store.all'), count: data.items.length },
    ...aisles.filter((a) => stocked.has(a.code)).map((a) => ({ code: a.code, name: aisleName(a, i18n.language), count: data.items.filter((i) => aisleOfItem(i) === a.code).length })),
    ...ownCategories,
    ...(stocked.has('other') ? [{ code: 'other', name: t('store.otherItems'), count: data.items.filter((i) => aisleOfItem(i) === 'other').length }] : []),
  ]
  const q = search.trim()
  // Search forgives: doodh finds milk, a letter wrong still matches (lib/search.ts).
  // The tree's names in three scripts go in too, so "dal" finds "तूर दाल" and "chai" the teas.
  const treeWords = (i: StoreItem) => { const c = categoryOf({ name: i.name, category: i.category }, tree); return c.key === 'other' ? '' : `${c.hi} ${c.en} ${c.gu}` }
  const found = q ? rank(data.items, q, (i) => `${i.name} ${i.category ?? ''} ${treeWords(i)}`) : data.items
  const max = Number(filters.max) || Infinity
  const shown = found
    .filter((i) => (category === 'all' || aisleOfItem(i) === category) && (!filters.inStock || i.available > 0) && i.price <= max)
    .sort((a, b) => Number(b.available > 0) - Number(a.available > 0) || (filters.sort === 'low' ? a.price - b.price : filters.sort === 'high' ? b.price - a.price : 0))
  // Shelves on the shop's front page: your usual items, today's offers, new arrivals.
  const counts = new Map<string, number>()
  for (const o of pastOrders.filter((o) => o.shopId === data.shopId)) for (const l of o.lines) if (l.productId) counts.set(l.productId, (counts.get(l.productId) ?? 0) + 1)
  const usual = data.items.filter((i) => counts.has(i.id) && i.available > 0).sort((a, b) => counts.get(b.id)! - counts.get(a.id)!).slice(0, 12)
  const offers = data.items.filter((i) => i.regularPrice && i.regularPrice > i.price && i.available > 0).slice(0, 12)
  const fresh = data.items.filter((i) => i.addedAt && nowMs - Date.parse(i.addedAt) < 21 * 86_400_000 && i.available > 0)
    .sort((a, b) => Date.parse(b.addedAt!) - Date.parse(a.addedAt!)).slice(0, 12)
  const front = category === 'all' && !q
  const rail = (title: string, items: StoreItem[]) => items.length > 0 && (
    <section className="mx-4 mt-3" aria-label={title}>
      <h2 className="mb-2 font-bold">{title}</h2>
      <div className="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1">
        {items.map((i) => (
          <div key={i.id} className="flex w-32 shrink-0 flex-col gap-1 rounded-2xl bg-white p-2 shadow-sm">
            <button onClick={() => setOpen(i)} className="flex aspect-square items-center justify-center overflow-hidden rounded-xl bg-[#f6f8f7]" aria-label={i.name}>
              {i.imageAt ? <img src={imageUrl(data.shopId, i.id, i.imageAt, 'thumb')} alt="" loading="lazy" className="h-full w-full object-contain" /> : <span aria-hidden className="text-3xl">🛍️</span>}
            </button>
            <span className="line-clamp-2 min-h-[2.4em] text-xs font-semibold">{i.name}</span>
            <b className="text-sm">{formatRupees(i.price)}</b>
            <button onClick={() => change(i, 1)} disabled={(cart[i.id] ?? 0) >= i.available} className={`h-9 rounded-lg text-sm ${btn.outline} ${popped === i.id ? 'scale-110' : ''} transition-transform`}>
              {(cart[i.id] ?? 0) > 0 ? `${cart[i.id]} ✓` : t('store.add')}
            </button>
          </div>
        ))}
      </div>
    </section>
  )
  const lines = data.items.filter((i) => cart[i.id] > 0).map((i) => ({ item: i, quantity: cart[i.id] }))
  const total = round2(lines.reduce((sum, l) => sum + round2(l.quantity * l.item.price), 0))
  const settings = data.settings ?? noSettings
  const salonServices = data.type === 'beauty' ? data.items.filter((i) => i.serviceMinutes).map((i) => ({ id: i.id, name: i.name, price: i.price, minutes: i.serviceMinutes! })) : []
  const planItems = data.items.filter((i) => !i.serviceMinutes && i.available > 0).map((i) => ({ id: i.id, name: i.name, unit: i.unit, price: i.price }))
  const toFree = settings.deliveryFee && settings.freeDeliveryAbove ? Math.max(0, round2(settings.freeDeliveryAbove - total)) : 0

  return (
    <div className="mx-auto min-h-dvh max-w-xl bg-ground pb-28 lg:max-w-6xl lg:px-4">
      <Header title={data.name} subtitle={[data.area, data.address].filter(Boolean).join(' · ') || t('store.title')} shopId={data.shopId} theme={settings.theme} />
      <SignedInAs shopId={data.shopId} />
      {festive(settings.theme) && <p className={`mx-4 mt-3 rounded-2xl p-3 text-center font-bold text-white ${festive(settings.theme)!.header}`}>{festive(settings.theme)!.emoji} {t(`cust4.festive.${settings.theme}`)}</p>}

      {data.rates && <MetalRates rates={data.rates} />}
      {data.pieces && <StorePieces pieces={data.pieces} phone={data.phone} />}
      <ShopTerms settings={settings} khata={khataHere} />
      {salonServices.length > 0 && (signedIn
        ? <button onClick={() => setBooking(true)} className={`mx-4 mt-3 flex h-12 w-[calc(100%-2rem)] items-center justify-center gap-2 rounded-xl text-base ${btn.solid}`}>{t('cust5.bookTitle')}</button>
        : <p className="mx-4 mt-3 rounded-xl bg-white p-3 text-center text-sm text-muted">{t('cust5.signInToBook')}</p>)}
      {signedIn && DELIVERY_TRADES.has(data.type ?? '') && planItems.length > 0 && (
        <button onClick={() => setPlanning(true)} className={`mx-4 mt-3 flex h-12 w-[calc(100%-2rem)] items-center justify-center rounded-xl text-base ${btn.outline}`}>{t('cust5.dailyTitle')}</button>
      )}
      {planning && <StartDeliverySheet shopId={data.shopId} shopName={data.name} items={planItems} onClose={() => setPlanning(false)} />}
      {booking && signedIn && salonServices.length > 0 && <SalonBooking shopId={data.shopId} shopName={data.name} services={salonServices} preselect={bookAgain} onClose={() => setBooking(false)} />}
      {signedIn && savedHere && <div className="mx-4 mt-3"><AlertsPrompt show /></div>}
      {myOrders.length > 0 && (
        <div className="mx-4 mt-3 flex flex-wrap gap-2">
          <span className="text-sm text-muted">{t('store.myOrders')}:</span>
          {myOrders.slice(0, 3).map((o) => (
            <button key={o.id} onClick={() => navigate(`/s/${slug}/o/${o.id}?t=${o.token}`)} className="min-h-11 rounded-full border border-line bg-white px-3 text-sm font-semibold">
              #{o.id.slice(-4).toUpperCase()}
            </button>
          ))}
        </div>
      )}

      <div className="sticky top-[60px] z-[5] flex flex-col gap-2 bg-ground px-4 pt-3 pb-2 lg:top-[64px]">
        <div className="flex gap-2">
          <input value={search} onChange={(e) => setSearch(e.target.value)} onBlur={() => rememberSearch(search)} placeholder={t('store.search', { name: data.name })} aria-label={t('store.search', { name: data.name })}
            className="h-12 min-w-0 flex-1 rounded-xl border border-line bg-white px-4 shadow-sm outline-none focus:border-brand-line" />
          <button onClick={() => setFilters({ ...filters, open: true })} className="h-12 shrink-0 rounded-xl border border-line bg-white px-3 font-semibold">⚙ {t('cust4.filters')}</button>
        </div>
        <div className={`-mx-4 flex gap-2 overflow-x-auto px-4 pb-1 ${kirana ? 'lg:hidden' : ''}`} role="tablist">
          {chips.map((c) => (
            <button key={c.code} role="tab" aria-selected={category === c.code} onClick={() => setCategory(c.code)}
              className={`flex h-10 shrink-0 items-center gap-1.5 rounded-full px-4 text-sm font-semibold ${category === c.code ? 'bg-brand text-white' : 'border border-line bg-white text-ink'}`}>
              {c.name}<span className={category === c.code ? 'opacity-80' : 'text-muted'}>{c.count}</span>
            </button>
          ))}
        </div>
      </div>

      {front && rail(t('cust4.usual'), usual)}
      {front && rail(t('cust4.offers'), offers)}
      {front && rail(t('cust4.newArrivals'), fresh)}

      {data.items.length > 0 && shown.length === 0 && (
        <div className="m-4 flex flex-col items-center gap-2 rounded-card bg-white p-6 text-center">
          <p className="text-muted">{t('store.noMatch')}</p>
          {data.phone && <a href={whatsAppLink(data.phone, t('cust4.askForItem', { item: q, shop: data.name }))} target="_blank" rel="noreferrer" className="flex h-11 items-center rounded-xl bg-[#25D366] px-4 font-bold text-white">{t('cust4.askShop')}</a>}
        </div>
      )}
      {data.items.length === 0 && <p className="p-8 text-center text-muted">{t('store.empty')}</p>}
      <div className={kirana ? 'lg:grid lg:grid-cols-[180px_1fr] lg:gap-2' : ''}>
      {/* Kirana: a left rail of aisles on wide screens (many aisles, few items each); chips on phones. */}
      {kirana && (
        <nav aria-label={t('cust4.aisles')} className="sticky top-[140px] hidden max-h-[calc(100dvh-160px)] flex-col gap-1 self-start overflow-y-auto pl-3 lg:flex">
          {chips.map((c) => (
            <button key={c.code} onClick={() => setCategory(c.code)} aria-current={category === c.code ? 'true' : undefined}
              className={`flex min-h-11 items-center justify-between rounded-xl px-3 text-left text-sm font-semibold ${category === c.code ? 'bg-brand text-white' : 'hover:bg-white'}`}>
              <span className="truncate">{c.name}</span><span className="opacity-70">{c.count}</span>
            </button>
          ))}
        </nav>
      )}
      <ul className={`mx-3 mt-2 grid grid-cols-2 gap-2 sm:grid-cols-3 md:grid-cols-4 ${kirana ? 'lg:grid-cols-4' : 'lg:grid-cols-5'}`}>
        {shown.map((item) => {
          const inCart = cart[item.id] ?? 0
          const out = item.available <= 0
          return (
            <li key={item.id} className="item-card">
              <ItemCard item={item} square imageUrl={imageUrl(data.shopId, item.id, item.imageAt, 'thumb')} title={item.name} badge={inCart > 0 ? inCart : undefined} selected={inCart > 0}
                photos={allPhotos(data.shopId, item).map((p) => p.thumb)} onPhotoClick={() => setOpen(item)}>
              <div className="mt-0.5 text-[11px] text-muted">{item.packSize && item.packUnit ? `${item.packSize} ${t(`units.${item.packUnit}`, { count: item.packSize })}` : `/${t(`units.${item.unit}`)}`}</div>
              <div className="mt-1 flex flex-wrap items-baseline gap-x-1.5">
                <span className="text-[15px] font-bold">{formatRupees(item.price)}</span>
                {signedIn && <button onClick={() => toggleHeart(item.id)} aria-pressed={hearted(item.id)} aria-label={t(hearted(item.id) ? 'cust4.unheart' : 'cust4.heart')} className={`ml-auto h-8 w-8 rounded-full text-lg ${hearted(item.id) ? 'text-due' : 'text-muted'}`}>{hearted(item.id) ? '♥' : '♡'}</button>}
                {item.regularPrice != null && item.regularPrice > item.price ? (
                  <>
                    <span className="text-[11px] text-muted line-through">{formatRupees(item.regularPrice)}</span>
                    <span className="rounded-full bg-amber-100 px-1.5 text-[11px] font-bold text-warn-ink">🏷 {item.offerName} · {Math.round((1 - item.price / item.regularPrice) * 100)}%</span>
                  </>
                ) : item.mrp != null && item.mrp > item.price && (
                  <>
                    <span className="text-[11px] text-muted line-through">{formatRupees(item.mrp)}</span>
                    <span className="text-[11px] font-semibold text-green-700">{t('store.off', { percent: Math.round((1 - item.price / item.mrp) * 100) })}</span>
                  </>
                )}
              </div>
              <div className="mt-auto pt-2">
              {out ? (
                <span className="text-sm font-semibold text-due">{t('store.out')}</span>
              ) : inCart === 0 ? (
                <button onClick={() => change(item, 1)} aria-label={`${t('store.add')} ${item.name}`} className={`h-10 w-full rounded-xl ${btn.outline}`}>{t('store.add')}</button>
              ) : (
                <span className="flex items-center gap-1">
                  <button aria-label={t('a11y.less')} onClick={() => change(item, -1)} className="h-11 w-11 rounded-xl bg-[#edf2f1] text-xl">−</button>
                  <span className={`min-w-14 text-center font-bold transition-transform ${popped === item.id ? 'scale-125 text-brand' : ''}`}>{inCart} {t(`units.${item.unit}`, { count: inCart })}</span>
                  <button aria-label={t('a11y.more')} onClick={() => change(item, 1)} disabled={inCart >= item.available} className="h-11 w-11 rounded-xl bg-[#edf2f1] text-xl disabled:opacity-30">+</button>
                </span>
              )}
              </div>
              </ItemCard>
            </li>
          )
        })}
      </ul>
      </div>

      {open && (
        <ProductSheet shopId={data.shopId} shopName={data.name} shopPhone={data.phone} item={open} all={data.items} inCart={(id) => cart[id] ?? 0} full={full}
          hearted={signedIn ? hearted(open.id) : undefined} onHeart={signedIn ? () => toggleHeart(open.id) : undefined}
          onChange={(i, d) => change(i as StoreItem, d)} onPick={(i) => setOpen(i as StoreItem)} onClose={() => setOpen(null)}
          deliveryText={settings.deliveryFee ? `${t('cust4.homeDelivery')} · ${t('cust2.delivery')} ${formatRupees(settings.deliveryFee)}${settings.freeDeliveryAbove ? ` · ${t('cust4.freeAbove', { amount: formatRupees(settings.freeDeliveryAbove) })}` : ''} · ${t('cust4.orPickup')}` : `${t('cust4.homeDelivery')} · ${t('cust4.orPickup')}`} />
      )}
      {filters.open && (
        <Drawer title={t('cust4.filters')} onClose={() => setFilters({ ...filters, open: false })}>
          <div className="flex flex-col gap-3">
            <span className="text-sm font-semibold text-muted">{t('cust4.sort')}</span>
            <div className="flex flex-wrap gap-2">
              {(['relevance', 'low', 'high'] as const).map((s) => (
                <button key={s} onClick={() => setFilters({ ...filters, sort: s })} aria-pressed={filters.sort === s} className={`min-h-11 rounded-full px-4 font-semibold ${filters.sort === s ? 'bg-brand text-white' : 'border border-line bg-white'}`}>{t(`cust4.sort_${s}`)}</button>
              ))}
            </div>
            <label className="flex flex-col gap-1"><span className="text-sm font-semibold text-muted">{t('cust4.maxPrice')}</span>
              <input value={filters.max} onChange={(e) => setFilters({ ...filters, max: e.target.value.replace(/[^\d]/g, '') })} inputMode="numeric" placeholder="₹" className="h-12 rounded-xl border border-line bg-white px-3 text-lg" /></label>
            <label className="flex min-h-11 items-center gap-2 font-semibold"><input type="checkbox" checked={filters.inStock} onChange={(e) => setFilters({ ...filters, inStock: e.target.checked })} className="h-5 w-5" />{t('cust4.inStockOnly')}</label>
            <button onClick={() => setFilters({ ...filters, open: false })} className={`h-12 rounded-xl ${btn.solid}`}>{t('cust4.showItems', { count: shown.length })}</button>
          </div>
        </Drawer>
      )}

      {gallery && <PhotoGallery title={gallery.name} photos={allPhotos(data.shopId, gallery)} onClose={() => setGallery(null)}
        footer={gallery.imageAt ? <PhotoNote shopId={data.shopId} item={gallery} /> : undefined} />}

      {lines.length > 0 && !checkingOut && (
        <div className={`fixed inset-x-4 ${signedIn ? "bottom-[80px]" : "bottom-4"} z-10 mx-auto max-w-[544px] lg:inset-x-auto lg:right-6 lg:bottom-6 lg:w-[440px]`}>
          {/* One dark bar: what's in the cart, and what still stands between the customer and a cheaper or allowed order. */}
          <button onClick={() => setCheckingOut(true)} className="flex min-h-[58px] w-full items-center justify-between gap-3 rounded-2xl bg-ink px-4 py-2 text-left text-white shadow-[0_8px_24px_rgba(16,32,29,0.25)]">
            <span className="flex min-w-0 flex-col">
              <b className="text-[15px]">{t('store.cart', { count: lines.length, total: formatRupees(total) })}</b>
              <span className={`truncate text-xs ${settings.minOrder && total < settings.minOrder ? 'font-bold text-amber-300' : 'opacity-80'}`}>
                {settings.minOrder && total < settings.minOrder ? t('cust2.belowMin', { amount: formatRupees(round2(settings.minOrder - total)), min: formatRupees(settings.minOrder) })
                  : toFree > 0 ? t('cust2.addMoreFree', { amount: formatRupees(toFree) })
                  : settings.deliveryFee && settings.freeDeliveryAbove ? t('cust5.freeDelivery') : t('store.checkout')}
              </span>
            </span>
            <span className="flex shrink-0 items-center gap-1 text-[15px] font-extrabold">{t('cust5.viewCart')}<CustIcon name="chevron" size={18} /></span>
          </button>
        </div>
      )}

      {checkingOut && (!signedIn || merged) && (
        <Checkout store={data} lines={lines} onClose={() => setCheckingOut(false)}
          onStep={change} onSet={setQuantity} onRemove={(item) => { const rest = { ...cart }; delete rest[item.id]; setCart(rest); if (lines.length <= 1) setCheckingOut(false) }}
          onPlaced={(placed) => {
            setCart({})
            remember(`store.${slug}.orders`, [{ id: placed.order.id, token: placed.statusToken, at: placed.order.createdAt }, ...myOrders].slice(0, 10))
            navigate(`/s/${slug}/o/${placed.order.id}?t=${placed.statusToken}`)
          }} />
      )}
    </div>
  )
}

/** Units sold in any amount (typed as 250 g or 1.5 kg); the rest count whole pieces, packets, boxes. */
const LOOSE = new Set<Unit>(['kg', 'g', 'litre', 'ml', 'tonne', 'brass', 'cft', 'metre', 'sq_ft', 'running_ft', 'quintal'])

/** The quantity in checkout: typed (a payal's weight, 250 g of dal), or nudged with − and +. */
function QuantityBox({ value, unit, max, onSet }: { value: number; unit: Unit; max: number; onSet: (quantity: number) => void }) {
  const { t } = useTranslation()
  const [text, setText] = useState(String(value))
  const [shown, setShown] = useState(value)
  if (shown !== value) { setShown(value); setText(String(value)) }
  const commit = () => {
    const typed = Number(text)
    if (!text.trim() || !Number.isFinite(typed) || typed <= 0) { setText(String(value)); return }
    const quantity = Math.min(LOOSE.has(unit) ? round2(typed) : Math.max(1, Math.round(typed)), Math.max(max, 0))
    setText(String(quantity))
    if (quantity !== value) onSet(quantity)
  }
  return (
    <label className="flex min-w-14 items-center justify-center gap-1 text-xs font-bold">
      <input value={text} inputMode={LOOSE.has(unit) ? 'decimal' : 'numeric'} aria-label={t('a11y.quantity')}
        onChange={(e) => setText(e.target.value.replace(LOOSE.has(unit) ? /[^0-9.]/g : /[^0-9]/g, ''))}
        onBlur={commit} onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); commit() } }} onFocus={(e) => e.target.select()}
        className="w-12 rounded-md bg-transparent py-1 text-center text-sm font-bold outline-none focus:bg-brand-soft" />
      <span>{t(`units.${unit}`, { count: value })}</span>
    </label>
  )
}

function Checkout({ store, lines, onClose, onPlaced, onStep, onSet, onRemove }: {
  store: Store
  lines: { item: StoreItem; quantity: number }[]
  onClose: () => void
  /** Change a line by one step (−1 or +1), or remove it, from the checkout itself. */
  onStep: (item: StoreItem, direction: 1 | -1) => void
  onSet: (item: StoreItem, quantity: number) => void
  onRemove: (item: StoreItem) => void
  onPlaced: (placed: Placed) => void
}) {
  const { t } = useTranslation()
  // Signed in: their own name and mobile to start with; a guest gets what they typed last time.
  const me = getSession()?.user
  const customerKey = `store.customer.${me?.id ?? 'guest'}`
  const saved = remembered(customerKey, { name: me?.fullName ?? '', phone: me?.phone?.replace(/^\+91/, '') ?? '', address: '' })
  const addresses = useQuery({ queryKey: ['addresses'], queryFn: () => api<{ id: string; label: string; text: string; landmark: string | null }[]>('/v1/addresses'), enabled: !!me, retry: false }).data ?? []
  const [form, setForm] = useState({ ...saved, fulfilment: 'delivery' as Fulfilment, slot: '', note: '' })
  const [payment, setPayment] = useState<OrderPayment>('cash')
  const settings = store.settings ?? noSettings
  const info = useCheckoutInfo(store.shopId, !!me)
  const bill = billOf(lines.map((l) => ({ price: l.item.price, quantity: l.quantity, was: wasOf(l.item) })), settings, form.fulfilment)
  const [orderId] = useState(uuidv7)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value })

  async function place(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError('')
    try {
      remember(customerKey, { name: form.name, phone: form.phone, address: form.address })
      const placed = await storeApi<Placed>(`${store.slug}/orders`, 'POST', {
        id: orderId,
        name: form.name,
        phone: form.phone,
        fulfilment: form.fulfilment,
        address: form.fulfilment === 'delivery' ? form.address : null,
        slot: form.slot || null,
        note: form.note || null,
        payment,
        lines: lines.map((l) => ({ productId: l.item.id, quantity: l.quantity })),
      })
      onPlaced(placed)
    } catch (err) {
      setError(err instanceof Error ? err.message : t('error'))
      setBusy(false)
    }
  }

  const input = 'h-12 w-full rounded-[14px] border-[1.5px] border-line bg-white px-3 text-lg outline-none focus:border-brand-line'
  return (
    <div className="fixed inset-0 z-30 flex items-end bg-black/40 lg:items-center lg:justify-center lg:p-6" onClick={onClose}>
      <form onSubmit={place} onClick={(e) => e.stopPropagation()} className="flex max-h-[92dvh] w-full flex-col gap-3 overflow-y-auto rounded-t-3xl bg-ground p-4 pb-8 lg:max-w-lg lg:rounded-3xl lg:p-6">
        <h2 className="text-xl font-bold">{t('store.checkout')}</h2>
        <ul className="rounded-[14px] bg-white px-3 text-sm">
          {lines.map((l) => (
            <li key={l.item.id} className="flex items-center gap-2 border-b border-[#edf2f1] py-2 last:border-0">
              <span className="min-w-0 flex-1">{l.item.name}<span className="block text-xs text-muted">{formatRupees(round2(l.quantity * l.item.price))}</span></span>
              <span className="flex h-9 shrink-0 items-center rounded-lg border border-line">
                <button type="button" onClick={() => onStep(l.item, -1)} aria-label={t('a11y.less')} className="h-full w-8 text-lg font-bold">−</button>
                <QuantityBox value={l.quantity} unit={l.item.unit} max={l.item.available} onSet={(q) => onSet(l.item, q)} />
                <button type="button" onClick={() => onStep(l.item, 1)} disabled={l.quantity >= l.item.available} aria-label={t('a11y.more')} className="h-full w-8 text-lg font-bold disabled:opacity-40">+</button>
              </span>
              <button type="button" onClick={() => onRemove(l.item)} aria-label={`${t('remove')} ${l.item.name}`} className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-due hover:bg-due-soft">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden><path d="M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13M10 11v6M14 11v6" /></svg>
              </button>
            </li>
          ))}
        </ul>
        <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('store.yourName')}</span>
          <input value={form.name} onChange={set('name')} required autoComplete="name" className={input} /></label>
        <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('store.phone')}</span>
          <input value={form.phone} onChange={set('phone')} required inputMode="tel" autoComplete="tel" className={input} /></label>
        <div className="grid grid-cols-2 gap-2">
          {(['delivery', 'pickup'] as const).map((f) => (
            <button type="button" key={f} onClick={() => setForm({ ...form, fulfilment: f })} aria-pressed={form.fulfilment === f}
              className={`min-h-12 rounded-[14px] font-semibold ${form.fulfilment === f ? 'border-2 border-brand bg-brand-soft text-brand-strong' : 'border border-line bg-white'}`}>
              {t(`plan.${f}`)}
            </button>
          ))}
        </div>
        {form.fulfilment === 'delivery' && (<>
          {addresses.length > 0 && (
            <select aria-label={t('orders2.savedAddress')} value="" onChange={(e) => { const a = addresses.find((x) => x.id === e.target.value); if (a) setForm({ ...form, address: [a.text, a.landmark].filter(Boolean).join(', ') }) }}
              className="h-12 w-full rounded-[14px] border-[1.5px] border-line bg-white px-3">
              <option value="">{t('orders2.savedAddress')}</option>
              {addresses.map((a) => <option key={a.id} value={a.id}>{a.label}: {a.text}</option>)}
            </select>
          )}
          <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('store.address')}</span>
            <textarea value={form.address} onChange={set('address')} required rows={2} maxLength={300} autoComplete="street-address" className="w-full rounded-[14px] border-[1.5px] border-line bg-white p-3 text-lg outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /></label>
        </>)}
        <SlotChips slots={settings.slots} value={form.slot} onChange={(slot) => setForm({ ...form, slot })} placeholder={t('plan.whenPlaceholder')} />
        <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('store.note')}</span>
          <input value={form.note} onChange={set('note')} maxLength={300} className={input} /></label>
        <PaymentPicker value={payment} onChange={setPayment} total={bill.total} upi={!!store.upiId} khata={me ? (info.data ? info.data.khata : undefined) : undefined} signedIn={!!me} />
        <BillSummary bill={bill} minOrder={settings.minOrder} />
        {error && <p className="rounded-lg bg-due-soft p-3 text-due">{error}</p>}
        <button type="submit" disabled={busy || bill.short > 0} className={`h-14 text-lg rounded-xl ${btn.solid} disabled:opacity-50`}>
          {t('store.place', { total: formatRupees(bill.total) })}
        </button>
      </form>
    </div>
  )
}

function OrderStatus({ slug, orderId, token }: { slug: string; orderId: string; token: string }) {
  const { t } = useTranslation()
  useAutoUpdate(false)
  const query = useQuery({
    queryKey: ['store', slug, 'order', orderId],
    queryFn: () => storeApi<Order>(`${slug}/orders/${orderId}?token=${token}`),
    refetchInterval: 30_000,
    retry: false,
  })
  const store = useQuery({ queryKey: ['store', slug], queryFn: () => storeApi<Store>(slug), retry: false })
  if (query.isError) return <NotFound />
  const order = query.data
  if (!order) return <p className="p-8 text-center text-muted">…</p>

  const code = `#${order.id.slice(-4).toUpperCase()}`
  const estimate = round2((order.deliveryFee ?? 0) + order.lines.reduce((sum, l) => sum + (l.unitPrice ?? 0) * l.quantity, 0))
  const amount = order.finalAmount ?? estimate
  const shopPhone = store.data?.phone ?? null
  const upiId = store.data?.upiId ?? null

  return (
    <div className="mx-auto min-h-dvh max-w-xl bg-ground pb-8 lg:max-w-2xl">
      <Header title={order.shopName} subtitle={`${t('store.placedTitle')} ${code}`} />
      <div className="flex flex-col gap-3 p-4">
        <section className="flex flex-col gap-3 rounded-[20px] bg-white p-[18px]">
          <span className={`text-2xl font-extrabold ${order.status === 'cancelled' ? 'text-due' : ''}`}>{t(`store.status.${order.status}`)}</span>
          <OrderSteps order={order} />
          <p className="text-sm text-muted">{t('store.keepLink')}</p>
        </section>

        <ul className="rounded-card bg-white px-4 shadow-sm">
          {order.lines.map((l) => (
            <li key={l.id} className={`flex justify-between gap-2 border-b border-[#edf2f1] py-2.5 last:border-0 ${l.removed ? 'text-due line-through' : ''}`}>
              <span className="flex flex-col">
                <span className="font-semibold">{l.name}</span>
                <span className="text-xs text-muted">{l.packedQuantity ?? l.quantity} {t(`units.${l.unit}`, { count: l.packedQuantity ?? l.quantity })}{l.shopNote && ` · ${l.shopNote}`}</span>
              </span>
              <span className="font-semibold">{formatRupees(l.amount ?? round2((l.unitPrice ?? 0) * l.quantity))}</span>
            </li>
          ))}
        </ul>

        <div className="flex flex-col gap-1 rounded-card bg-white p-4 shadow-sm">
          {(order.deliveryFee ?? 0) > 0 && <p className="flex justify-between text-sm"><span>{t('cust2.delivery')}</span><span>{formatRupees(order.deliveryFee!)}</span></p>}
          <p className="flex justify-between text-xl font-extrabold">
            <span>{order.finalAmount === null ? t('store.estimate') : t('plan.finalAmount')}</span>
            <span>{formatRupees(amount)}</span>
          </p>
          <p className="text-sm text-muted">{t('cust.payment')}: {t(`cust.pay.${order.payment}`)}</p>
        </div>

        {order.payment === 'upi' && upiId && isOpen(order.status) && <UpiPay upiId={upiId} shopName={order.shopName} amount={amount} note={`${order.shopName} ${code}`} />}
        {getSession() && <AlertsPrompt show />}

        {shopPhone && (
          <a href={whatsAppLink(shopPhone, t('store.whatsappText', { code, shop: order.shopName, total: formatRupees(amount) }))} target="_blank" rel="noreferrer"
            className="flex h-12 items-center justify-center rounded-2xl bg-[#15803d] font-bold text-white">
            {t('store.whatsappShop')}
          </a>
        )}
        <button onClick={() => navigate(`/s/${slug}`)} className="min-h-11 font-semibold text-brand underline">← {order.shopName}</button>
      </div>
    </div>
  )
}

/** Under a card's photo: where it is from (Open Food Facts needs the credit), and a way to say it's the wrong photo. */
function PhotoNote({ shopId, item }: { shopId: string; item: StoreItem }) {
  const { t } = useTranslation()
  const [sent, setSent] = useState(false)
  async function report() {
    // No pop-up asking why (some phone browsers block them): one tap reports it, and the shop takes a look.
    await fetch(`/api/v1/images/${shopId}/${item.id}/report`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({}) })
    setSent(true)
  }
  return (
    <div className="mt-0.5 flex items-center justify-between gap-1 text-[10px] text-muted">
      <span className="truncate">{item.imageCredit ? t('store.photoFrom', { source: item.imageCredit }) : ''}</span>
      {sent ? <span>✓ {t('store.reported')}</span> : <button onClick={(e) => { e.stopPropagation(); void report() }} className="shrink-0 underline">{t('store.reportPhoto')}</button>}
    </div>
  )
}

/** The shop's terms before shopping: delivery charge, minimum order, and whether this customer can put it on khata. */
function ShopTerms({ settings, khata }: { settings: StoreSettings; khata: KhataAtCheckout | null | undefined }) {
  const { t } = useTranslation()
  const boxes: [string, string][] = [
    [settings.deliveryFee ? t('cust5.delivery', { amount: formatRupees(settings.deliveryFee) }) : t('cust5.freeDelivery'),
      settings.deliveryFee && settings.freeDeliveryAbove ? t('cust5.freeAboveOrPickup', { amount: formatRupees(settings.freeDeliveryAbove) }) : t('cust4.orPickup')],
    [settings.minOrder ? t('cust5.minOrder', { amount: formatRupees(settings.minOrder) }) : t('cust5.noMin'), settings.minOrder ? t('cust5.ofItems') : t('cust5.anySize')],
  ]
  if (khata?.allowed) boxes.push([t('cust5.khataOk'), khata.limit ? t('cust5.khataLimit', { amount: formatRupees(khata.limit) }) : t('cust5.khataOpen')])
  return (
    <div className="mx-4 mt-3 grid gap-2" style={{ gridTemplateColumns: `repeat(${boxes.length}, minmax(0, 1fr))` }}>
      {boxes.map(([title, hint]) => (
        <span key={title} className="flex flex-col rounded-xl bg-white px-2.5 py-2 text-xs leading-snug text-muted shadow-[0_1px_4px_rgba(16,32,29,0.06)]">
          <b className="text-[13px] text-ink">{title}</b>{hint}
        </span>
      ))}
    </div>
  )
}

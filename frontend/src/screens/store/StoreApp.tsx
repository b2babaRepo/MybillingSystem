import { putCart, useCarts, useSavedShops, useSaveShop } from '../../lib/customer'
import { api } from '../../lib/api'
import { aisleName } from '../../lib/aisleNames'
import { btn } from '../../components/ui'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useAisles } from '../../components/AisleTiles'
import { ItemCard } from '../../components/ItemCard'
import { allPhotos, imageUrl } from '../../lib/images'
import { PhotoGallery } from '../../components/PhotoGallery'
import { aisleOf } from '../../lib/itemArt'
import { nextLanguage, setLanguage } from '../../i18n'
import { formatRupees, whatsAppLink } from '../../lib/format'
import { uuidv7 } from '../../lib/ids'
import { round2 } from '../../lib/local'
import type { Order } from '../../lib/planner'
import type { ProblemDetails, Unit } from '../../lib/types'
import { navigate } from '../../router'
import { getSession } from '../../lib/session'
import { AlertsPrompt, BillSummary, OrderSteps, PaymentPicker, SlotChips, UpiPay } from '../../components/OrderPay'
import { billOf, isOpen, noSettings, useCheckoutInfo, type StoreSettings } from '../../lib/orderFlow'
import type { Fulfilment, OrderPayment } from '../../lib/planner'

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

/** path: "/s/{slug}", "/s/{slug}/cart" (straight to checkout) or "/s/{slug}/o/{orderId}?t={token}". */
export function StoreApp({ path }: { path: string }) {
  const match = path.match(/^\/s\/([a-z0-9-]+)(\/cart)?(?:\/o\/([0-9a-f-]+))?(?:\?t=([A-Za-z0-9_-]+))?$/)
  if (!match) return <NotFound />
  const [, slug, toCart, orderId, token] = match
  return orderId && token ? <OrderStatus slug={slug} orderId={orderId} token={token} /> : <Catalogue key={slug} slug={slug} openCheckout={!!toCart} />
}

function NotFound() {
  const { t } = useTranslation()
  return <p className="p-8 text-center text-muted">{t('store.notFound')}</p>
}

function Header({ title, subtitle, shopId }: { title: string; subtitle?: string | null; shopId?: string }) {
  const { t, i18n } = useTranslation()
  const signedIn = !!getSession()
  const saved = useSavedShops(signedIn).data?.some((s) => s.id === shopId) ?? false
  const save = useSaveShop()
  return (
    <header className="sticky top-0 z-10 flex items-center gap-2 bg-brand px-4 py-3 text-white lg:rounded-b-2xl lg:px-6">
      {/* Home on every shop page: a signed-in customer goes back to their shops, anyone else to the app's start. */}
      <button onClick={() => navigate('/')} aria-label={t('store.home')} className="-ml-1 flex h-11 shrink-0 items-center gap-1 rounded-full bg-white/15 px-3 text-sm font-semibold hover:bg-white/25">
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" aria-hidden><path d="M3 11l9-8 9 8M5 10v10h5v-6h4v6h5V10" /></svg>
        {t('store.home')}
      </button>
      <div className="flex min-w-0 flex-1 flex-col">
        <h1 className="truncate text-xl font-bold">{title}</h1>
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

function Catalogue({ slug, openCheckout = false }: { slug: string; openCheckout?: boolean }) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const store = useQuery({ queryKey: ['store', slug], queryFn: () => storeApi<Store>(slug), retry: false })
  const [cart, setCartState] = useState<Cart>(() => remembered(`store.${slug}.cart`, {}))
  const [category, setCategory] = useState('all')
  const [search, setSearch] = useState('')
  const [gallery, setGallery] = useState<StoreItem | null>(null)
  const aisles = useAisles().data ?? []
  const [checkingOut, setCheckingOut] = useState(openCheckout)
  const myOrders = remembered<MyOrder[]>(`store.${slug}.orders`, [])
  const signedIn = !!getSession()
  const accountCarts = useCarts(signedIn)
  const [merged, setMerged] = useState(false)
  const shopId = store.data?.shopId
  // Saved this shop: a good moment to offer order notifications (asked once per phone).
  const savedHere = useSavedShops(signedIn).data?.some((s) => s.id === shopId) ?? false
  // Signed in: the cart lives on the account. What was added on this phone before signing in joins it once.
  useEffect(() => {
    if (!signedIn || merged || !shopId || !accountCarts.data) return
    const server: Cart = Object.fromEntries((accountCarts.data.find((c) => c.shopId === shopId)?.items ?? []).map((i) => [i.productId, i.quantity]))
    const local = remembered<Cart>(`store.${slug}.cart`, {})
    const joined: Cart = { ...server }
    for (const [id, q] of Object.entries(local)) joined[id] = Math.max(joined[id] ?? 0, q)
    setCartState(joined)
    setMerged(true)
    if (Object.keys(local).length > 0) { void putCart(shopId, joined).catch(() => {}); remember(`store.${slug}.cart`, {}) }
  }, [signedIn, merged, shopId, accountCarts.data, slug])
  const pushTimer = useRef<number | undefined>(undefined)

  if (store.isError) return <NotFound />
  const data = store.data
  if (!data) return <p className="p-8 text-center text-muted">…</p>

  const setCart = (next: Cart) => {
    setCartState(next)
    if (!signedIn) { remember(`store.${slug}.cart`, next); return }
    // A moment after the last tap, the account's cart is updated.
    window.clearTimeout(pushTimer.current)
    pushTimer.current = window.setTimeout(() => void putCart(data.shopId, next).then(() => queryClient.invalidateQueries({ queryKey: ['me', 'carts'] })).catch(() => {}), 600)
  }
  const change = (item: StoreItem, direction: 1 | -1) => {
    const next = round2((cart[item.id] ?? 0) + direction * STEP[item.unit])
    const updated = { ...cart }
    if (next <= 0) delete updated[item.id]
    else updated[item.id] = Math.min(next, Math.max(item.available, 0))
    setCart(updated)
  }

  // Browse by aisle (the common grocery tree). Items not on it (a clothes or jewellery shop's goods, or items added by
  // hand) sit under the shop's own categories, and those with none under "Other".
  const aisleOfItem = (i: StoreItem) => aisleOf(i.catalogueItemId) ?? (i.category ? `cat:${i.category}` : 'other')
  const stocked = new Set(data.items.map(aisleOfItem))
  const ownCategories = [...stocked].filter((a) => a.startsWith('cat:')).sort().map((a) => ({ code: a, name: a.slice(4), count: data.items.filter((i) => aisleOfItem(i) === a).length }))
  // Like the big shopping apps: every item shows at once as a photo card; chips narrow it, search finds by name.
  const chips = [
    { code: 'all', name: t('store.all'), count: data.items.length },
    ...aisles.filter((a) => stocked.has(a.code)).map((a) => ({ code: a.code, name: aisleName(a, i18n.language), count: data.items.filter((i) => aisleOfItem(i) === a.code).length })),
    ...ownCategories,
    ...(stocked.has('other') ? [{ code: 'other', name: t('store.otherItems'), count: data.items.filter((i) => aisleOfItem(i) === 'other').length }] : []),
  ]
  const q = search.trim().toLowerCase()
  const shown = data.items
    .filter((i) => (category === 'all' || aisleOfItem(i) === category) && (!q || i.name.toLowerCase().includes(q) || (i.category ?? '').toLowerCase().includes(q)))
    .sort((a, b) => Number(b.available > 0) - Number(a.available > 0))
  const lines = data.items.filter((i) => cart[i.id] > 0).map((i) => ({ item: i, quantity: cart[i.id] }))
  const total = round2(lines.reduce((sum, l) => sum + round2(l.quantity * l.item.price), 0))
  const settings = data.settings ?? noSettings

  return (
    <div className="mx-auto min-h-dvh max-w-xl bg-ground pb-28 lg:max-w-6xl lg:px-4">
      <Header title={data.name} subtitle={[data.area, data.address].filter(Boolean).join(' · ') || t('store.title')} shopId={data.shopId} />

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
        <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('store.search', { name: data.name })} aria-label={t('store.search', { name: data.name })}
          className="h-12 w-full rounded-xl border border-line bg-white px-4 shadow-sm outline-none focus:border-brand-line" />
        <div className="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1" role="tablist">
          {chips.map((c) => (
            <button key={c.code} role="tab" aria-selected={category === c.code} onClick={() => setCategory(c.code)}
              className={`flex h-10 shrink-0 items-center gap-1.5 rounded-full px-4 text-sm font-semibold ${category === c.code ? 'bg-brand text-white' : 'border border-line bg-white text-ink'}`}>
              {c.name}<span className={category === c.code ? 'opacity-80' : 'text-muted'}>{c.count}</span>
            </button>
          ))}
        </div>
      </div>

      {data.items.length > 0 && shown.length === 0 && <p className="p-8 text-center text-muted">{t('store.noMatch')}</p>}
      {data.items.length === 0 && <p className="p-8 text-center text-muted">{t('store.empty')}</p>}
      <ul className="mx-3 mt-2 grid grid-cols-2 gap-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5">
        {shown.map((item) => {
          const inCart = cart[item.id] ?? 0
          const out = item.available <= 0
          return (
            <li key={item.id} className="item-card">
              <ItemCard item={item} square imageUrl={imageUrl(data.shopId, item.id, item.imageAt, 'thumb')} title={item.name} badge={inCart > 0 ? inCart : undefined} selected={inCart > 0}
                photos={allPhotos(data.shopId, item).map((p) => p.thumb)} onPhotoClick={allPhotos(data.shopId, item).length > 0 ? () => setGallery(item) : undefined}>
              <div className="mt-0.5 text-[11px] text-muted">{item.packSize && item.packUnit ? `${item.packSize} ${t(`units.${item.packUnit}`, { count: item.packSize })}` : `/${t(`units.${item.unit}`)}`}</div>
              <div className="mt-1 flex flex-wrap items-baseline gap-x-1.5">
                <span className="text-[15px] font-bold">{formatRupees(item.price)}</span>
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
                  <span className="min-w-14 text-center font-bold">{inCart} {t(`units.${item.unit}`, { count: inCart })}</span>
                  <button aria-label={t('a11y.more')} onClick={() => change(item, 1)} disabled={inCart >= item.available} className="h-11 w-11 rounded-xl bg-[#edf2f1] text-xl disabled:opacity-30">+</button>
                </span>
              )}
              </div>
              </ItemCard>
            </li>
          )
        })}
      </ul>

      {gallery && <PhotoGallery title={gallery.name} photos={allPhotos(data.shopId, gallery)} onClose={() => setGallery(null)}
        footer={gallery.imageAt ? <PhotoNote shopId={data.shopId} item={gallery} /> : undefined} />}

      {lines.length > 0 && !checkingOut && (
        <div className="fixed inset-x-0 bottom-0 z-10 mx-auto max-w-xl border-t border-line bg-white px-4 py-3 lg:inset-x-auto lg:right-6 lg:bottom-6 lg:w-[440px] lg:rounded-2xl lg:border lg:shadow-xl">
          <button onClick={() => setCheckingOut(true)} className={`flex h-14 w-full items-center justify-between px-5 text-lg rounded-xl ${btn.solid}`}>
            <span>{t('store.cart', { count: lines.length, total: formatRupees(total) })}</span>
            <span>{t('store.checkout')} →</span>
          </button>
          {settings.minOrder && total < settings.minOrder ? <p className="mt-1 text-center text-xs font-semibold text-warn-ink">{t('cust2.belowMin', { amount: formatRupees(round2(settings.minOrder - total)), min: formatRupees(settings.minOrder) })}</p>
            : settings.deliveryFee ? <p className="mt-1 text-center text-xs text-muted">{t('cust2.delivery')} {formatRupees(settings.deliveryFee)}{settings.freeDeliveryAbove ? ` · ${t('cust2.addMoreFree', { amount: formatRupees(Math.max(0, round2(settings.freeDeliveryAbove - total))) })}` : ''}</p> : null}
        </div>
      )}

      {checkingOut && (!signedIn || merged) && (
        <Checkout store={data} lines={lines} onClose={() => setCheckingOut(false)}
          onPlaced={(placed) => {
            setCart({})
            remember(`store.${slug}.orders`, [{ id: placed.order.id, token: placed.statusToken, at: placed.order.createdAt }, ...myOrders].slice(0, 10))
            navigate(`/s/${slug}/o/${placed.order.id}?t=${placed.statusToken}`)
          }} />
      )}
    </div>
  )
}

function Checkout({ store, lines, onClose, onPlaced }: {
  store: Store
  lines: { item: StoreItem; quantity: number }[]
  onClose: () => void
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
            <li key={l.item.id} className="flex justify-between border-b border-[#edf2f1] py-2 last:border-0">
              <span>{l.item.name} · {l.quantity} {t(`units.${l.item.unit}`, { count: l.quantity })}</span>
              <span className="font-semibold">{formatRupees(round2(l.quantity * l.item.price))}</span>
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

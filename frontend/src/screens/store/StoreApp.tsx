import { useQuery } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { AisleTiles } from '../../components/AisleTiles'
import { ItemCard } from '../../components/ItemCard'
import { imageUrl } from '../../lib/images'
import { aisleOf } from '../../lib/itemArt'
import { setLanguage } from '../../i18n'
import { formatRupees, whatsAppLink } from '../../lib/format'
import { uuidv7 } from '../../lib/ids'
import { round2 } from '../../lib/local'
import type { Order } from '../../lib/planner'
import type { ProblemDetails, Unit } from '../../lib/types'
import { navigate } from '../../router'

// The shop's public online store at #/s/{slug}: no account, no app install. Guests browse, fill a cart, check
// out with name, phone and (for delivery) an address, then follow the order by its private link.

interface StoreItem {
  id: string
  name: string
  category: string | null
  unit: Unit
  price: number
  available: number
  catalogueItemId?: number | null
  imageAt?: string | null
}

interface Store {
  name: string
  slug: string
  area: string | null
  address: string | null
  phone: string | null
  shopId: string
  items: StoreItem[]
}

interface Placed {
  order: Order
  statusToken: string
}

type Cart = Record<string, number>
interface MyOrder { id: string; token: string; at: string }

const STEP: Record<Unit, number> = { piece: 1, packet: 1, kg: 0.5, litre: 0.5, g: 100, ml: 100 }

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
    headers: body === undefined ? {} : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as ProblemDetails | null
    const first = problem?.errors ? Object.values(problem.errors)[0]?.[0] : undefined
    throw new Error(first ?? problem?.title ?? `Error ${response.status}`)
  }
  return (await response.json()) as T
}

/** path: "/s/{slug}" or "/s/{slug}/o/{orderId}?t={token}". */
export function StoreApp({ path }: { path: string }) {
  const match = path.match(/^\/s\/([a-z0-9-]+)(?:\/o\/([0-9a-f-]+))?(?:\?t=([A-Za-z0-9_-]+))?$/)
  if (!match) return <NotFound />
  const [, slug, orderId, token] = match
  return orderId && token ? <OrderStatus slug={slug} orderId={orderId} token={token} /> : <Catalogue slug={slug} />
}

function NotFound() {
  const { t } = useTranslation()
  return <p className="p-8 text-center text-muted">{t('store.notFound')}</p>
}

function Header({ title, subtitle }: { title: string; subtitle?: string | null }) {
  const { i18n } = useTranslation()
  return (
    <header className="sticky top-0 z-10 flex items-center gap-2 bg-teal-700 px-4 py-3 text-white">
      <div className="flex min-w-0 flex-1 flex-col">
        <h1 className="truncate text-xl font-bold">{title}</h1>
        {subtitle && <span className="truncate text-sm opacity-85">{subtitle}</span>}
      </div>
      <button onClick={() => setLanguage(i18n.language === 'hi' ? 'en' : 'hi')} className="h-11 rounded-full bg-white/20 px-3 text-sm font-semibold">
        {i18n.language === 'hi' ? 'EN' : 'हि'}
      </button>
    </header>
  )
}

function Catalogue({ slug }: { slug: string }) {
  const { t } = useTranslation()
  const store = useQuery({ queryKey: ['store', slug], queryFn: () => storeApi<Store>(slug), retry: false })
  const [cart, setCartState] = useState<Cart>(() => remembered(`store.${slug}.cart`, {}))
  const [category, setCategory] = useState<string | null>(null)
  const [checkingOut, setCheckingOut] = useState(false)
  const myOrders = remembered<MyOrder[]>(`store.${slug}.orders`, [])

  if (store.isError) return <NotFound />
  const data = store.data
  if (!data) return <p className="p-8 text-center text-muted">…</p>

  const setCart = (next: Cart) => {
    setCartState(next)
    remember(`store.${slug}.cart`, next)
  }
  const change = (item: StoreItem, direction: 1 | -1) => {
    const next = round2((cart[item.id] ?? 0) + direction * STEP[item.unit])
    const updated = { ...cart }
    if (next <= 0) delete updated[item.id]
    else updated[item.id] = Math.min(next, Math.max(item.available, 0))
    setCart(updated)
  }

  // Browse by aisle (the common tree); items the shop added by hand sit under "Other".
  const aisleOfItem = (i: StoreItem) => aisleOf(i.catalogueItemId) ?? 'other'
  const stocked = new Set(data.items.map(aisleOfItem))
  const shown = category ? data.items.filter((i) => aisleOfItem(i) === category) : []
  const lines = data.items.filter((i) => cart[i.id] > 0).map((i) => ({ item: i, quantity: cart[i.id] }))
  const total = round2(lines.reduce((sum, l) => sum + round2(l.quantity * l.item.price), 0))

  return (
    <div className="mx-auto min-h-dvh max-w-xl bg-ground pb-28">
      <Header title={data.name} subtitle={[data.area, data.address].filter(Boolean).join(' · ') || t('store.title')} />

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

      {category ? (
        <button onClick={() => setCategory(null)} className="mx-4 mt-3 flex min-h-11 items-center gap-1 text-lg font-bold text-teal-800">
          ‹ {t('store.allAisles')}
        </button>
      ) : (
        <div className="mx-4 mt-3 rounded-[18px] bg-white p-3">
          <AisleTiles available={stocked} onPick={(a) => setCategory(a.code)} />
          {stocked.has('other') && (
            <button onClick={() => setCategory('other')} className="mt-3 min-h-11 w-full rounded-xl bg-[#edf2f1] font-semibold">{t('store.otherItems')}</button>
          )}
        </div>
      )}

      {data.items.length === 0 && <p className="p-8 text-center text-muted">{t('store.empty')}</p>}
      <ul className="mx-4 mt-2 grid grid-cols-2 gap-3 sm:grid-cols-3">
        {shown.map((item) => {
          const inCart = cart[item.id] ?? 0
          const out = item.available <= 0
          return (
            <li key={item.id} className="item-card">
              <ItemCard item={item} imageUrl={imageUrl(data.shopId, item.id, item.imageAt)} title={item.name} subtitle={`${formatRupees(item.price)}/${t(`units.${item.unit}`)}`} badge={inCart > 0 ? inCart : undefined} selected={inCart > 0}>
              <div className="mt-2">
              {out ? (
                <span className="text-sm font-semibold text-due">{t('store.out')}</span>
              ) : inCart === 0 ? (
                <button onClick={() => change(item, 1)} className="h-11 rounded-xl bg-teal-700 px-4 font-bold text-white">{t('store.add')}</button>
              ) : (
                <span className="flex items-center gap-1">
                  <button aria-label={t('a11y.less')} onClick={() => change(item, -1)} className="h-11 w-11 rounded-xl bg-[#edf2f1] text-xl">−</button>
                  <span className="min-w-14 text-center font-bold">{inCart} {t(`units.${item.unit}`)}</span>
                  <button aria-label={t('a11y.more')} onClick={() => change(item, 1)} disabled={inCart >= item.available} className="h-11 w-11 rounded-xl bg-[#edf2f1] text-xl disabled:opacity-30">+</button>
                </span>
              )}
              </div>
              </ItemCard>
            </li>
          )
        })}
      </ul>

      {lines.length > 0 && !checkingOut && (
        <div className="fixed inset-x-0 bottom-0 z-10 mx-auto max-w-xl border-t border-line bg-white px-4 py-3">
          <button onClick={() => setCheckingOut(true)} className="flex h-14 w-full items-center justify-between rounded-2xl bg-teal-700 px-5 text-lg font-bold text-white">
            <span>{t('store.cart', { count: lines.length, total: formatRupees(total) })}</span>
            <span>{t('store.checkout')} →</span>
          </button>
        </div>
      )}

      {checkingOut && (
        <Checkout store={data} lines={lines} total={total} onClose={() => setCheckingOut(false)}
          onPlaced={(placed) => {
            setCart({})
            remember(`store.${slug}.orders`, [{ id: placed.order.id, token: placed.statusToken, at: placed.order.createdAt }, ...myOrders].slice(0, 10))
            navigate(`/s/${slug}/o/${placed.order.id}?t=${placed.statusToken}`)
          }} />
      )}
    </div>
  )
}

function Checkout({ store, lines, total, onClose, onPlaced }: {
  store: Store
  lines: { item: StoreItem; quantity: number }[]
  total: number
  onClose: () => void
  onPlaced: (placed: Placed) => void
}) {
  const { t } = useTranslation()
  const saved = remembered(`store.customer`, { name: '', phone: '', address: '' })
  const [form, setForm] = useState({ ...saved, fulfilment: 'delivery' as 'delivery' | 'pickup', slot: '', note: '' })
  const [orderId] = useState(uuidv7)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value })

  async function place(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError('')
    try {
      remember('store.customer', { name: form.name, phone: form.phone, address: form.address })
      const placed = await storeApi<Placed>(`${store.slug}/orders`, 'POST', {
        id: orderId,
        name: form.name,
        phone: form.phone,
        fulfilment: form.fulfilment,
        address: form.fulfilment === 'delivery' ? form.address : null,
        slot: form.slot || null,
        note: form.note || null,
        lines: lines.map((l) => ({ productId: l.item.id, quantity: l.quantity })),
      })
      onPlaced(placed)
    } catch (err) {
      setError(err instanceof Error ? err.message : t('error'))
      setBusy(false)
    }
  }

  const input = 'h-12 w-full rounded-[14px] border-[1.5px] border-line bg-white px-3 text-lg outline-none focus:border-teal-600'
  return (
    <div className="fixed inset-0 z-20 flex items-end bg-black/40" onClick={onClose}>
      <form onSubmit={place} onClick={(e) => e.stopPropagation()} className="flex max-h-[92dvh] w-full flex-col gap-3 overflow-y-auto rounded-t-3xl bg-ground p-4 pb-8">
        <h2 className="text-xl font-bold">{t('store.checkout')}</h2>
        <ul className="rounded-[14px] bg-white px-3 text-sm">
          {lines.map((l) => (
            <li key={l.item.id} className="flex justify-between border-b border-[#edf2f1] py-2 last:border-0">
              <span>{l.item.name} · {l.quantity} {t(`units.${l.item.unit}`)}</span>
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
              className={`min-h-12 rounded-[14px] font-semibold ${form.fulfilment === f ? 'border-2 border-teal-700 bg-teal-50 text-teal-800' : 'border border-line bg-white'}`}>
              {t(`plan.${f}`)}
            </button>
          ))}
        </div>
        {form.fulfilment === 'delivery' && (
          <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('store.address')}</span>
            <textarea value={form.address} onChange={set('address')} required rows={2} maxLength={300} autoComplete="street-address" className="w-full rounded-[14px] border-[1.5px] border-line bg-white p-3 text-lg" /></label>
        )}
        <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('store.when')}</span>
          <input value={form.slot} onChange={set('slot')} maxLength={80} placeholder={t('plan.whenPlaceholder')} className={input} /></label>
        <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('store.note')}</span>
          <input value={form.note} onChange={set('note')} maxLength={300} className={input} /></label>
        <p className="text-sm text-muted">{t('store.priceNote')}</p>
        {error && <p className="rounded-lg bg-red-50 p-3 text-due">{error}</p>}
        <button type="submit" disabled={busy} className="h-14 rounded-2xl bg-teal-700 text-lg font-bold text-white disabled:opacity-40">
          {t('store.place', { total: formatRupees(total) })}
        </button>
      </form>
    </div>
  )
}

const STEPS = ['placed', 'packed', 'delivered'] as const

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
  const estimate = round2(order.lines.reduce((sum, l) => sum + (l.unitPrice ?? 0) * l.quantity, 0))
  const amount = order.finalAmount ?? estimate
  const reached = order.status === 'cancelled' ? -1 : STEPS.indexOf(order.status as (typeof STEPS)[number])
  const shopPhone = store.data?.phone ?? null

  return (
    <div className="mx-auto min-h-dvh max-w-xl bg-ground pb-8">
      <Header title={order.shopName} subtitle={`${t('store.placedTitle')} ${code}`} />
      <div className="flex flex-col gap-3 p-4">
        <section className="flex flex-col gap-3 rounded-[20px] bg-white p-[18px]">
          <span className={`text-2xl font-extrabold ${order.status === 'cancelled' ? 'text-due' : ''}`}>{t(`store.status.${order.status}`)}</span>
          {order.status !== 'cancelled' && (
            <ol className="grid grid-cols-3 gap-1">
              {STEPS.map((step, i) => (
                <li key={step} className="flex flex-col gap-1">
                  <span className={`h-1.5 rounded-full ${i <= reached ? 'bg-teal-700' : 'bg-[#edf2f1]'}`} />
                  <span className={`text-xs ${i <= reached ? 'font-bold text-teal-800' : 'text-muted'}`}>{t(`store.status.${step}`)}</span>
                </li>
              ))}
            </ol>
          )}
          <p className="text-sm text-muted">{t('store.keepLink')}</p>
        </section>

        <ul className="rounded-[18px] bg-white px-4">
          {order.lines.map((l) => (
            <li key={l.id} className={`flex justify-between gap-2 border-b border-[#edf2f1] py-2.5 last:border-0 ${l.removed ? 'text-due line-through' : ''}`}>
              <span className="flex flex-col">
                <span className="font-semibold">{l.name}</span>
                <span className="text-xs text-muted">{l.packedQuantity ?? l.quantity} {t(`units.${l.unit}`)}{l.shopNote && ` · ${l.shopNote}`}</span>
              </span>
              <span className="font-semibold">{formatRupees(l.amount ?? round2((l.unitPrice ?? 0) * l.quantity))}</span>
            </li>
          ))}
        </ul>

        <p className="flex justify-between rounded-[18px] bg-white p-4 text-xl font-extrabold">
          <span>{order.finalAmount === null ? t('store.estimate') : t('plan.finalAmount')}</span>
          <span>{formatRupees(amount)}</span>
        </p>

        {shopPhone && (
          <a href={whatsAppLink(shopPhone, t('store.whatsappText', { code, shop: order.shopName, total: formatRupees(amount) }))} target="_blank" rel="noreferrer"
            className="flex h-12 items-center justify-center rounded-2xl bg-[#15803d] font-bold text-white">
            {t('store.whatsappShop')}
          </a>
        )}
        <button onClick={() => navigate(`/s/${slug}`)} className="min-h-11 font-semibold text-teal-700 underline">← {order.shopName}</button>
      </div>
    </div>
  )
}

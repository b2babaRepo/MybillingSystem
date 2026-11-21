import { CustIcon, shopTint, type CustIconName } from '../../components/CustIcons'
import { useEffect, useState } from 'react'
import { BillSummary } from '../../components/OrderPay'
import { canListen, clearSearches, dataSaver, listen, rank, recentSearches, rememberSearch, setDataSaver } from '../../lib/search'
import { useHeart, useSavedItems } from '../../lib/savedItems'
import { getSession, setSession } from '../../lib/session'
import { alertsOn, alertsPossible, billOf, noSettings, turnOffAlerts, turnOnAlerts, type StoreSettings } from '../../lib/orderFlow'
import { useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ConnectShop } from '../../components/ConnectShop'
import { api } from '../../lib/api'
import { asShopEntry, useCarts, useSavedShops, useSaveShop, type Cart, type SavedShop } from '../../lib/customer'
import { formatRupees, whatsAppLink } from '../../lib/format'
import { imageUrl } from '../../lib/images'
import { useShopSearch, type ShopEntry } from '../../lib/planner'
import { useSession } from '../../lib/data'
import { CUSTOMER_LANGUAGES, setLanguage, type Language } from '../../i18n'
import { navigate } from '../../router'
import { confirmAsk } from '../../components/Ask'

const card = 'flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm'
const input = 'h-11 min-w-0 rounded-xl border border-line bg-white px-3'

/** Every shop's cart: what is in it, the total, open the shop or go to checkout. */
export function CartScreen() {
  const { t } = useTranslation()
  const carts = useCarts().data
  return (
    <div className="flex flex-col gap-3 p-4">
      {carts?.length === 0 && (
        <div className={`${card} items-center py-8 text-center`}>
          <span className="text-4xl" aria-hidden>🛒</span>
          <b>{t('cust.cartEmpty')}</b>
          <button onClick={() => navigate('/')} className="mt-2 h-11 rounded-xl bg-brand px-5 font-bold text-white">{t('cust.startShopping')}</button>
        </div>
      )}
      {carts?.map((c) => (
        <section key={c.shopId} className={card}>
          <div className="flex items-center justify-between gap-2">
            <b className="truncate text-lg">{c.shopName}</b>
            <span className="shrink-0 text-sm text-muted">{t('plan.itemsCount', { count: c.items.length })}</span>
          </div>
          <div className="flex gap-2 overflow-x-auto">
            {c.items.slice(0, 6).map((i) => (
              <span key={i.productId} className="flex w-16 shrink-0 flex-col items-center gap-1 text-center">
                <span className="flex h-14 w-14 items-center justify-center overflow-hidden rounded-xl bg-[#f1f5f4]">
                  {i.imageAt ? <img src={imageUrl(c.shopId, i.productId, i.imageAt, 'thumb')} alt="" className="h-full w-full object-contain" /> : <span aria-hidden>🛍️</span>}
                </span>
                <span className="line-clamp-2 text-[11px] leading-tight">{i.quantity} × {i.name}</span>
              </span>
            ))}
          </div>
          <CartBill cart={c} />
          <div className="flex items-center justify-end gap-2">
            <span className="flex gap-2">
              <button onClick={() => navigate(`/s/${c.shopSlug}`)} className="h-11 rounded-xl border border-line px-3 font-semibold">{t('cust.openShop')}</button>
              <button onClick={() => navigate(`/s/${c.shopSlug}/cart`)} className="h-11 rounded-xl bg-brand px-4 font-bold text-white">{t('store.checkout')} →</button>
            </span>
          </div>
        </section>
      ))}
    </div>
  )
}

interface StoreBits { settings?: StoreSettings | null; items: { id: string; price: number; mrp?: number | null; regularPrice?: number | null }[] }

/** The cart's honest bill, before checkout: item total, saving, the shop's delivery charge and minimum, the total. */
function CartBill({ cart }: { cart: Cart }) {
  const store = useQuery({ queryKey: ['store', cart.shopSlug], queryFn: () => fetch(`/api/v1/store/${cart.shopSlug}`).then((r) => (r.ok ? (r.json() as Promise<StoreBits>) : null)), staleTime: 60_000 })
  const settings = store.data?.settings ?? noSettings
  const was = (id: string) => {
    const i = store.data?.items.find((x) => x.id === id)
    return i ? (i.regularPrice && i.regularPrice > i.price ? i.regularPrice : i.mrp ?? null) : null
  }
  const bill = billOf(cart.items.map((i) => ({ price: i.price, quantity: i.quantity, was: was(i.productId) })), settings, 'delivery')
  return <BillSummary bill={bill} minOrder={settings.minOrder} />
}

/** Order notifications on this phone, and WhatsApp updates on the account. */
function AlertSettings() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [push, setPush] = useState<boolean | null>(null)
  const [message, setMessage] = useState('')
  const whatsapp = useQuery({ queryKey: ['me', 'order-alerts'], queryFn: () => api<{ whatsApp: boolean }>('/v1/me/order-alerts'), retry: false }).data?.whatsApp ?? false
  useEffect(() => { void alertsOn().then(setPush).catch(() => setPush(false)) }, [])
  const blocked = alertsPossible() && Notification.permission === 'denied'
  async function togglePush() {
    setMessage('')
    try {
      if (push) { await turnOffAlerts(); setPush(false) } else { const on = await turnOnAlerts(); setPush(on); if (!on) setMessage(t('cust2.alertsBlocked')) }
    } catch (e) { setMessage(e instanceof Error ? e.message : t('error')) }
  }
  async function toggleWhatsApp() {
    await api('/v1/me/order-alerts', 'PUT', { whatsApp: !whatsapp })
    await queryClient.invalidateQueries({ queryKey: ['me', 'order-alerts'] })
  }
  const toggle = (label: string, hint: string, on: boolean, onClick: () => void, disabled = false) => (
    <button onClick={onClick} disabled={disabled} role="switch" aria-checked={on} className="flex min-h-14 w-full items-center gap-3 border-t border-[#edf2f1] px-1 text-left first:border-t-0 disabled:opacity-50">
      <span className="flex flex-1 flex-col"><span className="font-semibold">{label}</span><span className="text-[13px] text-muted">{hint}</span></span>
      <span className={`relative h-7 w-12 shrink-0 rounded-full transition ${on ? 'bg-brand' : 'bg-[#cfd8d6]'}`}><span className={`absolute top-1 h-5 w-5 rounded-full bg-white shadow transition-all ${on ? 'left-6' : 'left-1'}`} /></span>
    </button>
  )
  return (
    <section className={card}>
      {alertsPossible() && toggle(`🔔 ${t('cust2.alertsOn')}`, blocked ? t('cust2.alertsBlocked') : t('cust2.alertsAsk'), !!push, () => void togglePush(), blocked || push === null)}
      {toggle(`💬 ${t('cust2.whatsapp')}`, t('cust2.whatsappHint'), whatsapp, () => void toggleWhatsApp())}
      {message && <p className="text-sm text-due">{message}</p>}
    </section>
  )
}

interface Khata { shopId: string; shopName: string; customerId: string; balance: number; confirmed: boolean }

/** The customer's account: who they are, their orders, carts, khata, shops, lists, addresses, language, sign out. */
export function AccountScreen({ onSignOut, onSwitchToShop }: { onSignOut: () => void; onSwitchToShop?: () => void }) {
  const { t, i18n } = useTranslation()
  const session = useSession()
  const carts = useCarts().data ?? []
  const shops = useSavedShops().data ?? []
  const khata = useQuery({ queryKey: ['my-khata'], queryFn: () => api<Khata[]>('/v1/me/khata'), retry: false }).data ?? []
  const due = khata.reduce((s, k) => s + Math.max(0, k.balance), 0)
  const user = session?.user
  const savedItems = useSavedItems().data?.length ?? 0
  const tiles: { label: string; count?: number; to: string; icon: CustIconName }[] = [
    { label: t('orders'), to: '/orders', icon: 'orders' },
    { label: t('cust.tab.cart'), count: carts.length, to: '/cart', icon: 'cart' },
    { label: t('cust5.saved'), count: savedItems, to: '/saved', icon: 'heart' },
    { label: t('cust.shops'), count: shops.length, to: '/shops', icon: 'shop' },
  ]
  const row = (icon: CustIconName, label: string, to: () => void, hint?: string) => (
    <button onClick={to} className="flex min-h-14 w-full items-center gap-3 border-t border-[#edf2f1] px-1 text-left first:border-t-0">
      <CustIcon name={icon} className="shrink-0 text-[#3d4c48]" />
      <span className="flex-1 font-semibold">{label}</span>
      {hint && <span className="text-sm text-muted">{hint}</span>}
      <CustIcon name="chevron" size={16} className="shrink-0 text-[#9aa8a4]" />
    </button>
  )
  return (
    <div className="flex flex-col gap-4 pb-4">
      <section className="flex items-center gap-3.5 bg-brand px-4 pt-3 pb-14 text-white">
        <span className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-white text-2xl font-extrabold text-brand">{user?.fullName.charAt(0)}</span>
        <span className="flex min-w-0 flex-1 flex-col">
          <b className="truncate text-xl">{user?.fullName}</b>
          <span className="text-sm opacity-90">{user?.phone?.replace(/^\+91/, '')}</span>
        </span>
      </section>
      <div className="-mt-14 grid grid-cols-4 gap-2 px-4">
        {tiles.map((x) => (
          <button key={x.to} onClick={() => navigate(x.to)} className="relative flex h-[78px] flex-col items-center justify-center gap-1.5 rounded-2xl bg-white text-xs font-bold shadow-[0_6px_18px_rgba(16,32,29,0.08)]">
            <CustIcon name={x.icon} size={22} className="text-brand" />
            <span className="truncate px-1">{x.label}</span>
            {!!x.count && <span className="absolute right-2 top-2 min-w-[18px] rounded-full bg-brand px-1 text-center text-[10px] leading-[18px] text-white">{x.count}</span>}
          </button>
        ))}
      </div>
      <section className="flex flex-col gap-2 px-4">
        <div className="flex items-baseline justify-between">
          <h2 className="text-[15px] font-extrabold">{t('myKhata.title')}</h2>
          {due > 0 && <span className="text-sm font-bold text-udhaar">{t('cust5.owe', { amount: formatRupees(due) })}</span>}
        </div>
        <div className={card}>
          {khata.filter((k) => k.confirmed).length === 0 && <button onClick={() => navigate('/khata')} className="min-h-12 w-full text-left text-sm text-muted">{t('myKhata.none')}</button>}
          {khata.filter((k) => k.confirmed).map((k) => (
            <button key={k.customerId} onClick={() => navigate(`/khata/${k.shopId}/${k.customerId}`)} className="flex min-h-[60px] w-full items-center gap-3 border-t border-[#edf2f1] px-1 text-left first:border-t-0">
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl font-extrabold" style={shopTint(k.shopId)}>{k.shopName.charAt(0)}</span>
              <span className="min-w-0 flex-1 truncate font-bold">{k.shopName}</span>
              <b className={k.balance > 0 ? 'text-udhaar' : 'text-muted'}>{k.balance > 0 ? formatRupees(k.balance) : t('settled')}</b>
            </button>
          ))}
        </div>
      </section>
      <section className="flex flex-col gap-2 px-4">
        <h2 className="text-[15px] font-extrabold">{t('home2.myLists')}</h2>
        <div className={card}>
          {row('list', t('plan.monthlyList'), () => navigate('/list/monthly'))}
          {row('calendar', t('plan.weeklyList'), () => navigate('/list/weekly'))}
          {row('sparkle', t('ai.title'), () => navigate('/ai'))}
        </div>
      </section>
      <div className="px-4"><AlertSettings /></div>
      <section className="flex flex-col gap-2 px-4">
        <h2 className="text-[15px] font-extrabold">{t('cust5.settings')}</h2>
        <div className={card}>
          {row('pin', t('cust.addresses'), () => navigate('/addresses'))}
          {row('globe', t('cust.language'), () => navigate('/language'), CUSTOMER_LANGUAGES.find((l) => l.code === i18n.language)?.name)}
          <DataSaverRow />
          {row('lock', t('cust4.privacy'), () => navigate('/privacy'))}
          {row('team', t('teamLink'), () => navigate('/team'))}
          {onSwitchToShop && row('store', t('plan.iAmShop'), onSwitchToShop)}
        </div>
      </section>
      <div className="px-4"><button onClick={onSignOut} className="h-12 w-full rounded-xl border-2 border-line font-bold text-due">{t('signOut')}</button></div>
    </div>
  )
}

interface Address { id: string; label: string; text: string; landmark: string | null; pincode: string | null; isDefault: boolean }

/** Saved addresses: add, edit, delete, and choose the default checkouts pick first. */
export function AddressesScreen() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const addresses = useQuery({ queryKey: ['addresses'], queryFn: () => api<Address[]>('/v1/addresses') }).data ?? []
  const blank = { label: '', text: '', landmark: '', pincode: '' }
  const [editing, setEditing] = useState<{ id: string | null } & typeof blank | null>(null)
  const [message, setMessage] = useState('')

  async function run(action: () => Promise<unknown>) {
    setMessage('')
    try { await action(); await queryClient.invalidateQueries({ queryKey: ['addresses'] }); return true } catch (e) { setMessage(e instanceof Error ? e.message : t('error')); return false }
  }
  async function save() {
    if (!editing) return
    const body = { label: editing.label, text: editing.text, landmark: editing.landmark || null, pincode: editing.pincode || null }
    const ok = await run(() => editing.id ? api(`/v1/addresses/${editing.id}`, 'PUT', body) : api('/v1/addresses', 'POST', { id: crypto.randomUUID(), ...body }))
    if (ok) setEditing(null)
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      {addresses.map((a) => (
        <section key={a.id} className={card}>
          <div className="flex items-center gap-2">
            <b>{a.label}</b>
            {a.isDefault && <span className="rounded-full bg-brand-soft px-2 text-xs font-bold text-brand-strong">{t('cust.default')}</span>}
          </div>
          <span className="text-sm">{[a.text, a.landmark, a.pincode].filter(Boolean).join(', ')}</span>
          <div className="flex flex-wrap gap-3 text-sm font-semibold">
            {!a.isDefault && <button onClick={() => void run(() => api(`/v1/addresses/${a.id}/default`, 'POST', {}))} className="text-brand">{t('cust.makeDefault')}</button>}
            <button onClick={() => setEditing({ id: a.id, label: a.label, text: a.text, landmark: a.landmark ?? '', pincode: a.pincode ?? '' })} className="text-brand">{t('cust.edit')}</button>
            <button onClick={() => void (async () => { if (await confirmAsk(t('cust.deleteAddress'))) void run(() => api(`/v1/addresses/${a.id}`, 'DELETE')) })()} className="text-due">{t('remove')}</button>
          </div>
        </section>
      ))}
      {editing ? (
        <section className={card}>
          <input aria-label={t('cust.label')} placeholder={t('cust.label')} value={editing.label} maxLength={30} onChange={(e) => setEditing({ ...editing, label: e.target.value })} className={input} />
          <textarea aria-label={t('store.address')} placeholder={t('store.address')} value={editing.text} maxLength={300} rows={2} onChange={(e) => setEditing({ ...editing, text: e.target.value })} className="rounded-xl border border-line p-3" />
          <div className="grid grid-cols-2 gap-2">
            <input aria-label={t('cust.landmark')} placeholder={t('cust.landmark')} value={editing.landmark} maxLength={120} onChange={(e) => setEditing({ ...editing, landmark: e.target.value })} className={input} />
            <input aria-label={t('cust.pincode')} placeholder={t('cust.pincode')} value={editing.pincode} inputMode="numeric" maxLength={6} onChange={(e) => setEditing({ ...editing, pincode: e.target.value })} className={input} />
          </div>
          <div className="grid grid-cols-2 gap-2">
            <button onClick={() => void save()} className="h-11 rounded-xl bg-brand font-bold text-white">{t('save')}</button>
            <button onClick={() => setEditing(null)} className="h-11 rounded-xl border border-line font-semibold">{t('cancel')}</button>
          </div>
        </section>
      ) : (
        <button onClick={() => setEditing({ id: null, ...blank })} className="h-12 rounded-xl border-2 border-dashed border-brand-line font-bold text-brand">+ {t('cust.addAddress')}</button>
      )}
      {message && <p className="text-sm text-due">{message}</p>}
    </div>
  )
}

/** Every language written in its own script; the choice is saved to the account. */
export function LanguageScreen() {
  const { i18n } = useTranslation()
  return (
    <div className="flex flex-col gap-2 p-4">
      {CUSTOMER_LANGUAGES.map((l) => (
        <button key={l.code} onClick={() => void setLanguage(l.code as Language)} aria-pressed={i18n.language === l.code}
          className={`flex min-h-16 items-center justify-between rounded-2xl bg-white px-4 text-xl font-bold shadow-sm ${i18n.language === l.code ? 'ring-2 ring-brand' : ''}`}>
          {l.name}
          {i18n.language === l.code && <span className="text-brand" aria-hidden>✓</span>}
        </button>
      ))}
    </div>
  )
}

/** One shop row: open its items, save or remove it. */
function ShopRow({ shop, saved }: { shop: ShopEntry; saved: boolean }) {
  const { t } = useTranslation()
  const save = useSaveShop()
  return (
    <div className="flex items-center gap-3 rounded-2xl bg-white p-3 shadow-sm">
      <button onClick={() => shop.slug && navigate(`/s/${shop.slug}`)} className="flex min-w-0 flex-1 items-center gap-3 text-left">
        <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-brand font-extrabold text-white">{shop.name.charAt(0)}</span>
        <span className="flex min-w-0 flex-col">
          <b className="truncate">{shop.name}</b>
          <span className="truncate text-[13px] text-muted">{shop.area ?? ''}</span>
        </span>
      </button>
      <button onClick={() => void save(shop, !saved)} aria-pressed={saved} aria-label={t(saved ? 'cust.unsave' : 'cust.save')} className={`h-10 w-10 shrink-0 rounded-full text-xl ${saved ? 'text-due' : 'text-muted'}`}>{saved ? '♥' : '♡'}</button>
    </div>
  )
}

/** The customer's saved shops, and connecting to a new one. */
export function SavedShopsScreen() {
  const { t } = useTranslation()
  const shops = useSavedShops().data
  const save = useSaveShop()
  return (
    <div className="flex flex-col gap-3 p-4">
      <ConnectShop onPick={(s) => void save(s, true).then(() => s.slug && navigate(`/s/${s.slug}`))} />
      {shops?.length === 0 && <p className="py-4 text-center text-muted">{t('cust.noSaved')}</p>}
      {shops?.map((s: SavedShop) => <ShopRow key={s.id} shop={asShopEntry(s)} saved />)}
    </div>
  )
}

interface StoreLite { name: string; slug: string; shopId: string; phone: string | null; items: { id: string; name: string; price: number; imageAt?: string | null; category: string | null; available: number }[] }

/** Search: items across your saved shops (forgiving: doodh finds milk), recent searches, voice, and shops by name or area. */
export function SearchScreen() {
  const { t, i18n } = useTranslation()
  const [q, setQ] = useState('')
  const [recent, setRecent] = useState(recentSearches)
  const [hearing, setHearing] = useState(false)
  const saved = useSavedShops().data ?? []
  const savedIds = new Set(saved.map((s) => s.id))
  const term = q.trim()
  const stores = useQueries({ queries: saved.map((s) => ({ queryKey: ['store', s.slug], queryFn: () => fetch(`/api/v1/store/${s.slug}`).then((r) => (r.ok ? (r.json() as Promise<StoreLite>) : null)), staleTime: 60_000 })) })
  const all = stores.flatMap((r) => (r.data ? r.data.items.map((i) => ({ ...i, store: r.data! })) : []))
  const items = term.length < 2 ? [] : rank(all, term, (i) => `${i.name} ${i.category ?? ''}`).slice(0, 40)
  const shops = useShopSearch(term.length >= 2 ? q : '').data ?? []
  // Popular in your shops: what is on offer or most common across them, as tap-to-search chips.
  const trending = [...new Set(all.filter((i) => i.available > 0).map((i) => (i.category ?? '').trim()).filter(Boolean))].slice(0, 8)
  const pick = (text: string) => { setQ(text); rememberSearch(text); setRecent(recentSearches()) }
  const voice = () => {
    setHearing(true)
    if (!listen(i18n.language, (text) => pick(text), () => setHearing(false))) setHearing(false)
  }
  // Opened from the mic on Home: start listening straight away.
  useEffect(() => {
    try { if (sessionStorage.getItem('search.voice') === '1') { sessionStorage.removeItem('search.voice'); if (canListen()) voice() } } catch { /* storage off */ }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])
  const chip = (text: string) => <button key={text} onClick={() => pick(text)} className="min-h-10 rounded-full border border-line bg-white px-3 text-sm font-semibold">{text}</button>
  const ask = saved.length > 0 && stores.find((r) => r.data?.phone)?.data
  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex gap-2">
        <input value={q} onChange={(e) => setQ(e.target.value)} onBlur={() => { rememberSearch(q); setRecent(recentSearches()) }} autoFocus placeholder={t('cust.searchPlaceholder')} aria-label={t('cust.searchPlaceholder')} className="h-12 min-w-0 flex-1 rounded-xl border border-line bg-white px-4 shadow-sm" />
        {canListen() && <button onClick={voice} aria-label={t('cust4.voice')} aria-pressed={hearing} className={`h-12 w-12 shrink-0 rounded-xl text-xl ${hearing ? 'animate-pulse bg-due text-white' : 'border border-line bg-white'}`}>🎙️</button>}
      </div>
      {term.length < 2 && recent.length > 0 && (
        <div className="flex flex-col gap-2">
          <div className="flex items-center justify-between"><h2 className="font-bold">{t('cust4.recent')}</h2><button onClick={() => { clearSearches(); setRecent([]) }} className="text-sm font-semibold text-brand">{t('cust4.clear')}</button></div>
          <div className="flex flex-wrap gap-2">{recent.map(chip)}</div>
        </div>
      )}
      {term.length < 2 && trending.length > 0 && (
        <div className="flex flex-col gap-2"><h2 className="font-bold">{t('cust4.trending')}</h2><div className="flex flex-wrap gap-2">{trending.map(chip)}</div></div>
      )}
      {items.length > 0 && <h2 className="font-bold">{t('cust.itemsInShops')}</h2>}
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
        {items.map((i) => (
          <button key={`${i.store.slug}-${i.id}`} onClick={() => navigate(`/s/${i.store.slug}`)} className="flex flex-col gap-1 rounded-2xl bg-white p-2 text-left shadow-sm">
            <span className="flex aspect-square items-center justify-center overflow-hidden rounded-xl bg-[#f1f5f4]">
              {i.imageAt ? <img src={imageUrl(i.store.shopId, i.id, i.imageAt, 'thumb')} alt="" loading="lazy" className="h-full w-full object-contain" /> : <span aria-hidden className="text-3xl">🛍️</span>}
            </span>
            <span className="line-clamp-2 text-sm font-semibold">{i.name}</span>
            <b>{formatRupees(i.price)}</b>
            <span className="truncate text-xs text-muted">{i.store.name}</span>
          </button>
        ))}
      </div>
      {/* Nothing found: a local shop can get it. Ask on WhatsApp. */}
      {term.length >= 2 && items.length === 0 && ask && (
        <div className="flex flex-col items-center gap-2 rounded-card bg-white p-5 text-center">
          <p className="text-muted">{t('cust4.noItems', { q: term })}</p>
          <a href={whatsAppLink(ask.phone!, t('cust4.askForItem', { item: term, shop: ask.name }))} target="_blank" rel="noreferrer" className="flex h-11 items-center rounded-xl bg-[#25D366] px-4 font-bold text-white">{t('cust4.askNamed', { shop: ask.name })}</a>
        </div>
      )}
      {term.length >= 2 && shops.length > 0 && <h2 className="font-bold">{t('cust.shops')}</h2>}
      {term.length >= 2 && shops.map((s) => <ShopRow key={s.id} shop={s} saved={savedIds.has(s.id)} />)}
      {term.length < 2 && <ConnectShop onPick={(s) => s.slug && navigate(`/s/${s.slug}`)} />}
    </div>
  )
}

/** Hearted items across shops: open the shop, or remove the heart. Out of stock ones say an alert comes when they are back. */
export function SavedItemsScreen() {
  const { t } = useTranslation()
  const items = useSavedItems().data
  const heart = useHeart()
  return (
    <div className="flex flex-col gap-2 p-4">
      {items?.length === 0 && <p className="py-8 text-center text-muted">{t('cust4.noSavedItems')}</p>}
      {items?.map((i) => (
        <div key={`${i.shopId}-${i.productId}`} className="flex items-center gap-3 rounded-2xl bg-white p-3 shadow-sm">
          <button onClick={() => navigate(`/s/${i.shopSlug}`)} className="flex min-w-0 flex-1 items-center gap-3 text-left">
            <span className="flex h-14 w-14 shrink-0 items-center justify-center overflow-hidden rounded-xl bg-[#f1f5f4]">
              {i.imageAt ? <img src={imageUrl(i.shopId, i.productId, i.imageAt, 'thumb')} alt="" className="h-full w-full object-contain" /> : <span aria-hidden>🛍️</span>}
            </span>
            <span className="flex min-w-0 flex-col">
              <b className="truncate">{i.name}</b>
              <span className="text-sm text-muted">{i.shopName} · {formatRupees(i.price)}</span>
              {i.available <= 0 && <span className="text-xs font-semibold text-warn-ink">{t('cust4.outAlert')}</span>}
            </span>
          </button>
          <button onClick={() => void heart(i.shopId, i.productId, false)} aria-label={t('cust4.unheart')} className="h-11 w-11 shrink-0 rounded-full text-2xl text-due">♥</button>
        </div>
      ))}
    </div>
  )
}

function DataSaverRow() {
  const { t } = useTranslation()
  const [on, setOn] = useState(dataSaver)
  return (
    <button onClick={() => { setDataSaver(!on); setOn(!on) }} role="switch" aria-checked={on} className="flex min-h-14 w-full items-center gap-3 border-t border-[#edf2f1] px-1 text-left">
      <span aria-hidden className="text-xl">📶</span>
      <span className="flex flex-1 flex-col"><span className="font-semibold">{t('cust4.dataSaver')}</span><span className="text-[13px] text-muted">{t('cust4.dataSaverHint')}</span></span>
      <span className={`relative h-7 w-12 shrink-0 rounded-full transition ${on ? 'bg-brand' : 'bg-[#cfd8d6]'}`}><span className={`absolute top-1 h-5 w-5 rounded-full bg-white shadow transition-all ${on ? 'left-6' : 'left-1'}`} /></span>
    </button>
  )
}

/** Notifications and privacy: what the app keeps, a copy of all of it, and closing the account. */
export function PrivacyScreen() {
  const { t } = useTranslation()
  const [message, setMessage] = useState('')
  async function download() {
    setMessage('')
    try {
      const r = await fetch('/api/v1/me/export', { headers: { Authorization: `Bearer ${getSession()?.accessToken}` } })
      if (!r.ok) throw new Error(String(r.status))
      const url = URL.createObjectURL(await r.blob())
      const a = document.createElement('a')
      a.href = url
      a.download = 'grocery-khata-my-data.json'
      a.click()
      URL.revokeObjectURL(url)
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  async function close() {
    if (!(await confirmAsk(t('cust4.deleteConfirm')))) return
    setMessage('')
    try {
      await api('/v1/me/delete-account', 'POST', {})
      setSession(null)
      navigate('/')
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  return (
    <div className="flex flex-col gap-3 p-4">
      <AlertSettings />
      <section className={card}>
        <h2 className="font-bold">{t('cust4.whatWeKeep')}</h2>
        <p className="text-sm text-muted">{t('cust4.whatWeKeepHint')}</p>
        <button onClick={() => void download()} className="h-12 rounded-xl border border-line font-bold">⬇ {t('cust4.download')}</button>
      </section>
      <section className={card}>
        <h2 className="font-bold text-due">{t('cust4.deleteTitle')}</h2>
        <p className="text-sm text-muted">{t('cust4.deleteHint')}</p>
        <button onClick={() => void close()} className="h-12 rounded-xl border border-due font-bold text-due">{t('cust4.delete')}</button>
      </section>
      {message && <p className="text-sm text-due">{message}</p>}
    </div>
  )
}

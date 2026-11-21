import { useState } from 'react'
import { useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ConnectShop } from '../../components/ConnectShop'
import { api } from '../../lib/api'
import { asShopEntry, useCarts, useSavedShops, useSaveShop, type SavedShop } from '../../lib/customer'
import { formatRupees } from '../../lib/format'
import { imageUrl } from '../../lib/images'
import { useShopSearch, type ShopEntry } from '../../lib/planner'
import { useSession } from '../../lib/data'
import { LANGUAGES, setLanguage, type Language } from '../../i18n'
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
          <div className="flex items-center justify-between gap-2">
            <b className="text-xl">{formatRupees(c.total)}</b>
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

interface Khata { shopId: string; balance: number }

/** The customer's account: who they are, their orders, carts, khata, shops, lists, addresses, language, sign out. */
export function AccountScreen({ onSignOut, onSwitchToShop }: { onSignOut: () => void; onSwitchToShop?: () => void }) {
  const { t, i18n } = useTranslation()
  const session = useSession()
  const carts = useCarts().data ?? []
  const shops = useSavedShops().data ?? []
  const khata = useQuery({ queryKey: ['my-khata'], queryFn: () => api<Khata[]>('/v1/me/khata'), retry: false }).data ?? []
  const due = khata.reduce((s, k) => s + Math.max(0, k.balance), 0)
  const user = session?.user
  const tiles = [
    { label: t('orders'), value: '›', to: '/orders', icon: '📦' },
    { label: t('cust.myCarts'), value: String(carts.length), to: '/cart', icon: '🛒' },
    { label: t('myKhata.title'), value: formatRupees(due), to: '/khata', icon: '📒' },
    { label: t('cust.savedShops'), value: String(shops.length), to: '/shops', icon: '🏪' },
  ]
  const row = (icon: string, label: string, to: () => void, hint?: string) => (
    <button onClick={to} className="flex min-h-14 w-full items-center gap-3 border-t border-[#edf2f1] px-1 text-left first:border-t-0">
      <span aria-hidden className="text-xl">{icon}</span>
      <span className="flex-1 font-semibold">{label}</span>
      {hint && <span className="text-sm text-muted">{hint}</span>}
      <span className="text-muted" aria-hidden>›</span>
    </button>
  )
  return (
    <div className="flex flex-col gap-3 p-4">
      <section className="flex items-center gap-3 rounded-card bg-brand p-4 text-white">
        <span className="flex h-14 w-14 items-center justify-center rounded-full bg-amber-400 text-2xl font-extrabold text-amber-950">{user?.fullName.charAt(0)}</span>
        <span className="flex min-w-0 flex-col">
          <b className="truncate text-lg">{user?.fullName}</b>
          <span className="text-sm opacity-85">{user?.phone?.replace(/^\+91/, '')}</span>
        </span>
      </section>
      <div className="grid grid-cols-2 gap-2">
        {tiles.map((x) => (
          <button key={x.to} onClick={() => navigate(x.to)} className="flex flex-col items-start gap-1 rounded-2xl bg-white p-3 text-left shadow-sm">
            <span aria-hidden className="text-2xl">{x.icon}</span>
            <span className="text-sm text-muted">{x.label}</span>
            <b className="text-lg">{x.value}</b>
          </button>
        ))}
      </div>
      <section className={card}>
        {row('📝', t('plan.monthlyList'), () => navigate('/list/monthly'))}
        {row('🗓️', t('plan.weeklyList'), () => navigate('/list/weekly'))}
        {row('🎙️', t('ai.title'), () => navigate('/ai'))}
      </section>
      <section className={card}>
        {row('📍', t('cust.addresses'), () => navigate('/addresses'))}
        {row('🌐', t('cust.language'), () => navigate('/language'), LANGUAGES.find((l) => l.code === i18n.language)?.name)}
        {row('👥', t('teamLink'), () => navigate('/team'))}
        {onSwitchToShop && row('🏬', t('plan.iAmShop'), onSwitchToShop)}
      </section>
      <button onClick={onSignOut} className="h-12 rounded-xl border border-due font-bold text-due">{t('signOut')}</button>
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
      {LANGUAGES.map((l) => (
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

interface StoreLite { name: string; slug: string; shopId: string; items: { id: string; name: string; price: number; imageAt?: string | null; category: string | null }[] }

/** Search: items in your saved shops, and shops by name or area (or connect by ID, mobile, QR). */
export function SearchScreen() {
  const { t } = useTranslation()
  const [q, setQ] = useState('')
  const saved = useSavedShops().data ?? []
  const savedIds = new Set(saved.map((s) => s.id))
  const term = q.trim().toLowerCase()
  const stores = useQueries({ queries: saved.map((s) => ({ queryKey: ['store', s.slug], queryFn: () => fetch(`/api/v1/store/${s.slug}`).then((r) => (r.ok ? (r.json() as Promise<StoreLite>) : null)), staleTime: 60_000 })) })
  const items = term.length < 2 ? [] : stores.flatMap((r) => (r.data ? r.data.items.filter((i) => i.name.toLowerCase().includes(term) || (i.category ?? '').toLowerCase().includes(term)).map((i) => ({ ...i, store: r.data! })) : [])).slice(0, 30)
  const shops = useShopSearch(term.length >= 2 ? q : '').data ?? []
  return (
    <div className="flex flex-col gap-3 p-4">
      <input value={q} onChange={(e) => setQ(e.target.value)} autoFocus placeholder={t('cust.searchPlaceholder')} aria-label={t('cust.searchPlaceholder')} className="h-12 rounded-xl border border-line bg-white px-4 shadow-sm" />
      {items.length > 0 && <h2 className="font-bold">{t('cust.itemsInShops')}</h2>}
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
        {items.map((i) => (
          <button key={`${i.store.slug}-${i.id}`} onClick={() => navigate(`/s/${i.store.slug}`)} className="flex flex-col gap-1 rounded-2xl bg-white p-2 text-left shadow-sm">
            <span className="flex aspect-square items-center justify-center overflow-hidden rounded-xl bg-[#f1f5f4]">
              {i.imageAt ? <img src={imageUrl(i.store.shopId, i.id, i.imageAt, 'thumb')} alt="" className="h-full w-full object-contain" /> : <span aria-hidden className="text-3xl">🛍️</span>}
            </span>
            <span className="line-clamp-2 text-sm font-semibold">{i.name}</span>
            <b>{formatRupees(i.price)}</b>
            <span className="truncate text-xs text-muted">{i.store.name}</span>
          </button>
        ))}
      </div>
      {term.length >= 2 && shops.length > 0 && <h2 className="font-bold">{t('cust.shops')}</h2>}
      {term.length >= 2 && shops.map((s) => <ShopRow key={s.id} shop={s} saved={savedIds.has(s.id)} />)}
      {term.length < 2 && <ConnectShop onPick={(s) => s.slug && navigate(`/s/${s.slug}`)} />}
    </div>
  )
}

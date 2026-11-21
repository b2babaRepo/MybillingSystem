import { ConnectShop } from '../../components/ConnectShop'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { itemArt } from '../../lib/itemArt'
import { formatRupees } from '../../lib/format'
import { confirmAsk } from '../../components/Ask'
import { sendList, useCatalogue, useLists, useMyOrders, useShopSearch, type CatalogueItem, type ListItem, type ShopEntry, type ShoppingList } from '../../lib/planner'
import { navigate } from '../../router'

/** Rough cost of a list from the catalogue's typical prices; items typed by hand or in another unit don't count. */
export function listEstimate(items: ListItem[], catalogue: CatalogueItem[]) {
  const byId = new Map(catalogue.map((c) => [c.id, c]))
  return Math.round(items.reduce((sum, i) => {
    const c = i.catalogueItemId ? byId.get(i.catalogueItemId) : undefined
    return c && c.unit === i.unit ? sum + c.typicalPrice * i.quantity : sum
  }, 0))
}

/** A list item's photo: the master-list photo of its catalogue item, when there is one. */
export function listItemPhoto(item: ListItem, catalogue: CatalogueItem[]) {
  const c = item.catalogueItemId ? catalogue.find((x) => x.id === item.catalogueItemId) : undefined
  return c?.photoMasterId && c.photoAt ? `/api/v1/master-images/${c.photoMasterId}?v=${Date.parse(c.photoAt)}&size=thumb` : null
}

/** The shop the customer is ordering from: kept on this phone until they change or clear it. */
export function getChosenShop(userId: string): ShopEntry | null {
  try {
    const raw = localStorage.getItem(`chosenShop:${userId}`)
    return raw ? (JSON.parse(raw) as ShopEntry) : null
  } catch { return null }
}

export function setChosenShop(userId: string, shop: ShopEntry | null) {
  try {
    if (shop) localStorage.setItem(`chosenShop:${userId}`, JSON.stringify(shop))
    else localStorage.removeItem(`chosenShop:${userId}`)
  } catch { /* storage blocked: the choice lasts until the page closes */ }
}

/** The shop tapped on the home screen, picked already when the send screen opens. */
export function rememberShop(shop: ShopEntry) {
  try { sessionStorage.setItem('chosenShop', JSON.stringify(shop)) } catch { /* private mode: the customer picks it again */ }
}

export function takeRememberedShop(): ShopEntry | null {
  try {
    const raw = sessionStorage.getItem('chosenShop')
    sessionStorage.removeItem('chosenShop')
    return raw ? (JSON.parse(raw) as ShopEntry) : null
  } catch { return null }
}

/** The customer's home: both lists with a picture and rough cost, a quick way to add, and the shops nearby. */
export function PlanHomeScreen({ userId, userName = '' }: { userId: string; userName?: string }) {
  const { t } = useTranslation()
  const { lists } = useLists(userId)
  const catalogue = useCatalogue(userId).data ?? []
  const monthly = lists.find((l) => l.kind === 'monthly')!
  const weekly = lists.find((l) => l.kind === 'weekly')!
  // The last list sent to a shop (not cancelled): repeated in one tap with the same shop and options.
  const orders = useMyOrders(userId).data ?? []
  const last = [...orders].filter((o) => o.source !== 'store' && o.status !== 'cancelled' && !o.pending)
    .sort((a, b) => b.createdAt.localeCompare(a.createdAt))[0]
  const lastList = last ? lists.find((l) => l.kind === last.listKind) : undefined
  const [sending, setSending] = useState(false)
  async function orderAgain() {
    if (!last || !lastList || lastList.items.length === 0) return
    const ok = await confirmAsk(t('again.confirm', {
      list: t(last.listKind === 'monthly' ? 'plan.monthlyList' : 'plan.weeklyList'), count: lastList.items.length, shop: last.shopName,
      how: last.fulfilment === 'delivery' ? t('plan.delivery') : t('plan.pickup'),
    }))
    if (!ok) return
    setSending(true)
    await sendList(userId, userName, lastList.items, {
      shop: { id: last.shopId, name: last.shopName, area: null, address: null }, kind: last.listKind, fulfilment: last.fulfilment,
      slot: '', payment: last.payment, note: '', address: last.address ?? null,
    })
    navigate('/orders')
  }
  const [near, setNear] = useState<{ lat: number; lng: number } | null>(null)
  const [locating, setLocating] = useState(false)
  const shops = useShopSearch('', '', near)
  const findNear = () => {
    setLocating(true)
    navigator.geolocation.getCurrentPosition(
      (p) => { setNear({ lat: p.coords.latitude, lng: p.coords.longitude }); setLocating(false) },
      () => setLocating(false),
      { timeout: 15_000 },
    )
  }

  return (
    <div className="flex flex-col gap-4 pb-6">
      <div className="flex flex-col gap-3 bg-brand px-4 pt-1 pb-5 text-white">
        <button onClick={findNear} className="flex min-h-11 items-center gap-1.5 self-start text-[17px] font-bold">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" aria-hidden><path d="M12 21s-7-6.5-7-11a7 7 0 0 1 14 0c0 4.5-7 11-7 11z" /><circle cx="12" cy="10" r="2.5" /></svg>
          {locating ? t('home2.locating') : near ? t('home2.nearYou') : t('home2.setLocation')}
        </button>
      </div>

      <section className="-mt-4 flex flex-col gap-3 px-4">
        {last && lastList && lastList.items.length > 0 && (
          <button onClick={() => void orderAgain()} disabled={sending} className="flex items-center gap-3 rounded-card bg-white p-3 text-left shadow-[0_1px_4px_rgba(16,32,29,0.06)] disabled:opacity-50">
            <span aria-hidden className="flex h-12 w-12 shrink-0 items-center justify-center rounded-[14px] bg-amber-100 text-2xl">🔁</span>
            <span className="flex min-w-0 flex-1 flex-col">
              <span className="font-extrabold">{t('again.title')}</span>
              <span className="truncate text-[13px] text-muted">{t('again.line', { list: t(last.listKind === 'monthly' ? 'plan.monthlyList' : 'plan.weeklyList'), count: lastList.items.length, shop: last.shopName })}</span>
            </span>
            <span className="shrink-0 rounded-xl bg-brand px-3 py-2 text-sm font-bold text-white">{t('again.send')}</span>
          </button>
        )}
        <h2 className="text-[17px] font-extrabold">{t('home2.myLists')}</h2>
        <div className="grid grid-cols-2 gap-2.5">
          <ListCard list={monthly} title={t('plan.monthlyList')} catalogue={catalogue} />
          <ListCard list={weekly} title={t('plan.weeklyList')} catalogue={catalogue} />
        </div>
        <button onClick={() => navigate('/ai')} className="flex min-h-14 items-center gap-3 rounded-2xl border border-dashed border-brand-line/50 bg-white px-4 text-left">
          <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="#0f6b5c" strokeWidth="2" aria-hidden><rect x="9" y="3" width="6" height="11" rx="3" /><path d="M5 11a7 7 0 0 0 14 0M12 18v3" /></svg>
          <span className="flex-1 font-bold">{t('ai.title')}</span>
          <span className="text-xl font-extrabold text-brand">+</span>
        </button>
      </section>

      <section className="flex flex-col gap-2 px-4">
        <div className="flex items-baseline justify-between">
          <h2 className="text-[17px] font-extrabold">{near ? t('home2.shopsNear') : t('home2.shops')}</h2>
          {!near && <button onClick={findNear} className="min-h-11 font-bold text-brand">{t('plan.nearMe')}</button>}
        </div>
        <ConnectShop onPick={(s) => { setChosenShop(userId, s); navigate('/list/monthly') }} current={getChosenShop(userId)?.id} />
        {shops.data?.length === 0 && <p className="text-sm text-muted">{t('plan.noShops')}</p>}
        {shops.data?.slice(0, 5).map((s) => (
          <button key={s.id} onClick={() => { setChosenShop(userId, s); navigate('/list/monthly') }} className="flex items-center gap-3 rounded-2xl bg-white p-3 text-left">
            <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-[14px] bg-brand text-lg font-extrabold text-white">{s.name.charAt(0)}</span>
            <span className="flex min-w-0 flex-1 flex-col">
              <span className="truncate font-bold">{s.name}</span>
              <span className="truncate text-[13px] text-muted">{[s.distanceKm != null ? t('plan.km', { km: s.distanceKm }) : null, s.area].filter(Boolean).join(' · ')}</span>
            </span>
            <span className="shrink-0 text-sm font-bold text-brand">{t('home2.order')} ›</span>
          </button>
        ))}
      </section>
    </div>
  )
}

function ListCard({ list, title, catalogue }: { list: ShoppingList; title: string; catalogue: CatalogueItem[] }) {
  const { t } = useTranslation()
  const count = list.items.length
  const estimate = listEstimate(list.items, catalogue)
  return (
    <button onClick={() => navigate(`/list/${list.kind}`)} className="flex flex-col gap-1.5 rounded-card bg-white p-3 text-left shadow-[0_1px_4px_rgba(16,32,29,0.06)]">
      <span className="flex h-8">
        {list.items.slice(0, 4).map((i, n) => {
          const art = itemArt({ catalogueItemId: i.catalogueItemId, name: i.name })
          const photo = listItemPhoto(i, catalogue)
          if (photo) return <img key={i.id} src={photo} alt="" className="h-8 w-8 rounded-lg border-2 border-white bg-white object-contain" style={{ marginLeft: n ? -8 : 0 }} />
          return <span key={i.id} aria-hidden className="flex h-8 w-8 items-center justify-center rounded-lg border-2 border-white text-base" style={{ background: art.tint, marginLeft: n ? -8 : 0 }}>{art.emoji}</span>
        })}
        {count === 0 && <span aria-hidden className="flex h-8 w-8 items-center justify-center rounded-lg bg-[#edf2f1] text-base">🛒</span>}
      </span>
      <span className="font-extrabold">{title}</span>
      <span className="text-[13px] text-muted">{count === 0 ? t('home2.empty') : estimate > 0 ? t('home2.countAbout', { count, amount: formatRupees(estimate) }) : t('plan.itemsCount', { count })}</span>
      <span className="text-[13px] font-bold text-brand">{count === 0 ? t('home2.start') : t('home2.order')} ›</span>
    </button>
  )
}

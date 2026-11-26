import { btn } from '../../components/ui'
import { SHOP_TYPES } from '../../lib/reports'
import { useQuery } from '@tanstack/react-query'
import { api } from '../../lib/api'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatRupees } from '../../lib/format'
import { estimateAtShop, optionFor, quantityIn, sendList, useLists, useShopOptions, useShopSearch, type Fulfilment, type LineChoice, type ListKind, type OrderPayment, type ReplacePreference, type ShopEntry } from '../../lib/planner'
import { navigate } from '../../router'
import { confirmAsk } from '../../lib/ask'
import { getChosenShop } from './PlanHomeScreen'
import { PaymentPicker, SlotChips } from '../../components/OrderPay'
import { noSettings, useCheckoutInfo } from '../../lib/orderFlow'

/** Mockups 4 and 5 on one screen: pick the shop, then how and when, then send. */
export function SendScreen({ userId, userName, kind }: { userId: string; userName: string; kind: ListKind }) {
  const { t } = useTranslation()
  const { lists } = useLists(userId)
  const items = lists.find((l) => l.kind === kind)!.items
  const [search, setSearch] = useState('')
  const [type, setType] = useState('')
  const [near, setNear] = useState<{ lat: number; lng: number } | null>(null)
  const shops = useShopSearch(search, type, near)
  const findNear = () => navigator.geolocation.getCurrentPosition((p) => setNear({ lat: p.coords.latitude, lng: p.coords.longitude }), () => setNear(null), { timeout: 15_000 })
  const [shop, setShop] = useState<ShopEntry | null>(() => getChosenShop(userId))
  const [fulfilment, setFulfilment] = useState<Fulfilment>('delivery')
  const addresses = useQuery({ queryKey: ['addresses'], queryFn: () => api<{ id: string; label: string; text: string; landmark: string | null; pincode: string | null }[]>('/v1/addresses'), retry: false })
  const [addressId, setAddressId] = useState('')
  const [newAddress, setNewAddress] = useState({ label: '', text: '', landmark: '', pincode: '' })
  const chosenAddress = addresses.data?.find((a) => a.id === addressId) ?? addresses.data?.[0]
  const addressText = chosenAddress ? [chosenAddress.text, chosenAddress.landmark, chosenAddress.pincode].filter(Boolean).join(', ') : null
  async function saveAddress() {
    const id = crypto.randomUUID()
    await api('/v1/addresses', 'POST', { id, ...newAddress })
    setNewAddress({ label: '', text: '', landmark: '', pincode: '' })
    await addresses.refetch()
    setAddressId(id)
  }
  const [slot, setSlot] = useState('')
  const [payment, setPayment] = useState<OrderPayment>('cash')
  const [note, setNote] = useState('')
  const [busy, setBusy] = useState(false)
  const options = useShopOptions(shop?.id)
  const [choices, setChoices] = useState<Record<string, LineChoice>>({})
  const estimate = options.data ? estimateAtShop(items, options.data, choices) : null
  // The shop's time chips and UPI ID, and this person's khata there (its balance, whether the shop allows app orders on it).
  const info = useCheckoutInfo(shop?.id).data
  // How many list items this shop has in stock (matched by the common catalogue), so the customer can compare shops.
  const available = options.data ? items.filter((i) => options.data.some((o) => o.catalogueItemId === i.catalogueItemId && o.inStock)).length : 0
  const choose = (itemId: string, change: Partial<LineChoice>) =>
    setChoices((current) => ({ ...current, [itemId]: { ...{ productId: null, preference: 'shop_chooses' as const }, ...current[itemId], ...change } }))

  async function send() {
    if (!shop) return
    setBusy(true)
    await sendList(userId, userName, items, { shop, kind, fulfilment, slot: slot.trim(), payment, note: note.trim(), address: addressText }, choices)
    navigate('/orders')
  }

  const chip = (selected: boolean) =>
    `min-h-12 rounded-[14px] px-3 font-semibold ${selected ? 'border-2 border-brand bg-brand-soft text-brand-strong' : 'border border-line bg-white text-ink'}`

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <section className="flex flex-col gap-2 rounded-card bg-white p-3">
        <h2 className="text-base font-bold">{t('plan.chooseShop')}</h2>
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('plan.searchShop')}
          aria-label={t('plan.searchShop')}
          className="h-12 rounded-[14px] border border-line px-3 text-[17px] outline-none focus:border-brand-line"
        />
        <div className="grid grid-cols-2 gap-2">
          <select aria-label={t('shopType.label')} value={type} onChange={(e) => setType(e.target.value)} className="h-12 rounded-[14px] border border-line bg-white px-2">
            <option value="">{t('plan.allTypes')}</option>
            {SHOP_TYPES.map((x) => <option key={x} value={x}>{t(`shopType.${x}`)}</option>)}
          </select>
          <button onClick={() => (near ? setNear(null) : findNear())} aria-pressed={!!near} className={`h-12 rounded-[14px] font-semibold ${near ? 'bg-brand text-white' : 'border border-line'}`}>{t('plan.nearMe')}</button>
        </div>
        {shops.isError && <p className="text-sm text-due">{shops.error.message}</p>}
        {shops.data?.length === 0 && <p className="text-sm text-muted">{t('plan.noShops')}</p>}
        <ul className="flex max-h-64 flex-col gap-2 overflow-y-auto">
          {shops.data?.map((s) => (
            <li key={s.id}>
              <button
                onClick={() => setShop(s)}
                aria-pressed={shop?.id === s.id}
                className={`flex w-full items-center gap-3 rounded-[14px] p-3 text-left ${shop?.id === s.id ? 'border-2 border-brand bg-brand-soft' : 'border border-line'}`}
              >
                <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-[#e0efec] text-lg font-extrabold text-brand">
                  {s.name.charAt(0)}
                </span>
                <span className="flex min-w-0 flex-col">
                  <span className="truncate font-bold">{s.name}</span>
                  {(s.area || s.address || s.distanceKm != null) && <span className="truncate text-sm text-muted">{[s.distanceKm != null ? t('plan.km', { km: s.distanceKm }) : null, s.area, s.address].filter(Boolean).join(' · ')}</span>}
                </span>
              </button>
            </li>
          ))}
        </ul>
        {shop && estimate && (
          <div className="rounded-[14px] bg-brand-soft p-3">
            <div className="mb-2 grid grid-cols-2 gap-2 text-center">
              <span className="rounded-xl bg-white p-2"><b className="block text-lg text-brand-strong">{available} / {items.length}</b><span className="text-xs text-muted">{t('home2.itemsThere')}</span></span>
              <span className="rounded-xl bg-white p-2"><b className="block text-lg">~{formatRupees(estimate.total)}</b><span className="text-xs text-muted">{t('home2.estimateShort')}</span></span>
            </div>
            <p className="text-lg font-bold text-brand-strong">{t('plan.estimate', { amount: formatRupees(estimate.total) })}</p>
            {estimate.unpriced > 0 && <p className="text-sm text-muted">{t('plan.estimateUnpriced', { count: estimate.unpriced })}</p>}
          </div>
        )}
      </section>

      {shop && options.data && items.some((i) => options.data.some((o) => o.catalogueItemId === i.catalogueItemId)) && (
        <section className="flex flex-col gap-3 rounded-card bg-white p-3">
          <h2 className="text-base font-bold">{t('options.title')}</h2>
          <p className="-mt-2 text-sm text-muted">{t('options.hint')}</p>
          {items.map((item) => {
            const mine = options.data.filter((o) => o.catalogueItemId === item.catalogueItemId)
            if (mine.length === 0) return null
            const choice = choices[item.id]
            const current = optionFor(item, options.data, choice)
            const prefs: ReplacePreference[] = choice?.productId ? ['shop_chooses', 'ask_me', 'dont_replace'] : ['shop_chooses', 'ask_me']
            return (
              <div key={item.id} className="flex flex-col gap-2 border-t border-[#edf2f1] pt-3 first:border-0 first:pt-0">
                <div className="flex justify-between font-semibold">
                  <span>{item.name} · {item.quantity} {t(`units.${item.unit}`, { count: item.quantity })}</span>
                  {current && quantityIn(item.quantity, item.unit, current) !== null && (
                    <span className="text-brand-strong">{formatRupees(Math.round(current.price * quantityIn(item.quantity, item.unit, current)! * 100) / 100)}</span>
                  )}
                </div>
                <div className="-mx-1 flex gap-2 overflow-x-auto px-1 pb-1">
                  <button onClick={() => choose(item.id, { productId: null, preference: choice?.preference === 'dont_replace' ? 'shop_chooses' : choice?.preference })}
                    aria-pressed={!choice?.productId}
                    className={`min-h-11 shrink-0 rounded-xl px-3 text-sm font-semibold ${!choice?.productId ? 'bg-brand text-white' : 'border border-line'}`}>
                    {t('options.shopChooses')}
                  </button>
                  {mine.map((o) => (
                    <button key={o.productId} onClick={() => choose(item.id, { productId: o.productId })} aria-pressed={choice?.productId === o.productId}
                      className={`flex min-h-11 shrink-0 flex-col items-start justify-center rounded-xl px-3 text-left text-sm ${choice?.productId === o.productId ? 'bg-brand text-white' : 'border border-line'} ${o.inStock ? '' : 'opacity-50'}`}>
                      <span className="font-semibold">{o.name}</span>
                      <span className={choice?.productId === o.productId ? 'opacity-90' : 'text-muted'}>
                        {formatRupees(o.price)}/{t(`units.${o.unit}`)}{o.isDefault ? ` · ${t('options.usual')}` : ''}{o.inStock ? '' : ` · ${t('store.out')}`}
                      </span>
                    </button>
                  ))}
                </div>
                <div className="flex flex-wrap gap-1.5">
                  {prefs.map((pref) => (
                    <button key={pref} onClick={() => choose(item.id, { preference: pref })} aria-pressed={(choice?.preference ?? 'shop_chooses') === pref}
                      className={`min-h-9 rounded-full px-3 text-xs font-semibold ${(choice?.preference ?? 'shop_chooses') === pref ? 'bg-amber-100 text-amber-950 ring-1 ring-amber-400' : 'bg-[#edf2f1] text-muted'}`}>
                      {t(`options.pref.${choice?.productId ? 'picked' : 'any'}.${pref}`)}
                    </button>
                  ))}
                </div>
              </div>
            )
          })}
        </section>
      )}

      <section className="flex flex-col gap-2 rounded-card bg-white p-3">
        <h2 className="text-base font-bold">{t('plan.howGet')}</h2>
        <div className="grid grid-cols-2 gap-2">
          <button onClick={() => setFulfilment('delivery')} aria-pressed={fulfilment === 'delivery'} className={chip(fulfilment === 'delivery')}>{t('plan.delivery')}</button>
          <button onClick={() => setFulfilment('pickup')} aria-pressed={fulfilment === 'pickup'} className={chip(fulfilment === 'pickup')}>{t('plan.pickup')}</button>
        </div>
        {fulfilment === 'delivery' && (
          <div className="flex flex-col gap-2">
            <span className="text-base font-bold">{t('plan.deliverTo')}</span>
            {(addresses.data?.length ?? 0) > 0 && (
              <select aria-label={t('plan.deliverTo')} value={chosenAddress?.id ?? ''} onChange={(e) => setAddressId(e.target.value)} className="h-12 rounded-[14px] border border-line bg-white px-3">
                {addresses.data!.map((a) => <option key={a.id} value={a.id}>{a.label}: {a.text}</option>)}
              </select>
            )}
            {chosenAddress && (
              <button onClick={async () => { if ((await confirmAsk(t('plan.removeAddress')))) void api(`/v1/addresses/${chosenAddress.id}`, 'DELETE').then(() => { setAddressId(''); void addresses.refetch() }) }}
                className={`min-h-11 self-start text-sm ${btn.dangerOutline}`}>{t('plan.removeAddress')}</button>
            )}
            <details className="rounded-[14px] border border-line p-2">
              <summary className="min-h-11 cursor-pointer font-semibold text-brand">{t('plan.addAddress')}</summary>
              <div className="mt-2 flex flex-col gap-2">
                <input aria-label={t('plan.addressLabel')} placeholder={t('plan.addressLabel')} value={newAddress.label} onChange={(e) => setNewAddress({ ...newAddress, label: e.target.value })} maxLength={30} className="h-12 rounded-[14px] border border-line px-3" />
                <input aria-label={t('plan.addressText')} placeholder={t('plan.addressText')} value={newAddress.text} onChange={(e) => setNewAddress({ ...newAddress, text: e.target.value })} maxLength={300} className="h-12 rounded-[14px] border border-line px-3" />
                <div className="grid grid-cols-2 gap-2">
                  <input aria-label={t('plan.landmark')} placeholder={t('plan.landmark')} value={newAddress.landmark} onChange={(e) => setNewAddress({ ...newAddress, landmark: e.target.value })} maxLength={120} className="h-12 min-w-0 rounded-[14px] border border-line px-3" />
                  <input aria-label={t('plan.pincode')} placeholder={t('plan.pincode')} value={newAddress.pincode} onChange={(e) => setNewAddress({ ...newAddress, pincode: e.target.value })} inputMode="numeric" maxLength={6} className="h-12 min-w-0 rounded-[14px] border border-line px-3" />
                </div>
                <button disabled={!newAddress.label.trim() || newAddress.text.trim().length < 5} onClick={() => void saveAddress()} className="min-h-11 rounded-[14px] bg-brand font-semibold text-white disabled:opacity-40">{t('save')}</button>
              </div>
            </details>
          </div>
        )}
        <SlotChips key={shop?.id ?? ''} slots={(info?.settings ?? noSettings).slots} value={slot} onChange={setSlot} placeholder={t('plan.whenPlaceholder')} />
        <PaymentPicker value={payment} onChange={setPayment} total={estimate?.total ?? 0} upi={!!info?.upiId} khata={info ? info.khata : undefined} signedIn />
        <label className="mt-1 block">
          <span className="mb-1 block text-sm font-semibold text-muted">{t('plan.noteForShop')}</span>
          <input value={note} onChange={(e) => setNote(e.target.value)} maxLength={300} className="h-12 w-full rounded-[14px] border border-line px-3 text-[16px]" />
        </label>
      </section>

      <div className="fixed inset-x-0 bottom-[68px] z-10 mx-auto lg:bottom-0 lg:max-w-4xl max-w-xl border-t border-line bg-white px-4 py-3">
        <button onClick={send} disabled={!shop || busy || items.length === 0} className={`h-14 w-full text-lg rounded-xl ${btn.solid}`}>
          {shop ? `${t('plan.send')} · ${shop.name}` : t('plan.pickShopFirst')}
        </button>
      </div>
    </div>
  )
}

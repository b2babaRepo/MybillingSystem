import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { sendList, useLists, useShopSearch, type Fulfilment, type ListKind, type OrderPayment, type ShopEntry } from '../../lib/planner'
import { navigate } from '../../router'

/** Mockups 4 and 5 on one screen: pick the shop, then how and when, then send. */
export function SendScreen({ userId, userName, kind }: { userId: string; userName: string; kind: ListKind }) {
  const { t } = useTranslation()
  const { lists } = useLists(userId)
  const items = lists.find((l) => l.kind === kind)!.items
  const [search, setSearch] = useState('')
  const shops = useShopSearch(search)
  const [shop, setShop] = useState<ShopEntry | null>(null)
  const [fulfilment, setFulfilment] = useState<Fulfilment>('delivery')
  const [slot, setSlot] = useState('')
  const [payment, setPayment] = useState<OrderPayment>('cash')
  const [note, setNote] = useState('')
  const [busy, setBusy] = useState(false)

  async function send() {
    if (!shop) return
    setBusy(true)
    await sendList(userId, userName, items, { shop, kind, fulfilment, slot: slot.trim(), payment, note: note.trim() })
    navigate('/orders')
  }

  const chip = (selected: boolean) =>
    `min-h-12 rounded-[14px] px-3 font-semibold ${selected ? 'border-2 border-teal-700 bg-teal-50 text-teal-800' : 'border border-line bg-white text-ink'}`

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
        <h2 className="text-base font-bold">{t('plan.chooseShop')}</h2>
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('plan.searchShop')}
          aria-label={t('plan.searchShop')}
          className="h-12 rounded-[14px] border border-line px-3 text-[17px] outline-none focus:border-teal-600"
        />
        {shops.isError && <p className="text-sm text-due">{shops.error.message}</p>}
        {shops.data?.length === 0 && <p className="text-sm text-muted">{t('plan.noShops')}</p>}
        <ul className="flex max-h-64 flex-col gap-2 overflow-y-auto">
          {shops.data?.map((s) => (
            <li key={s.id}>
              <button
                onClick={() => setShop(s)}
                aria-pressed={shop?.id === s.id}
                className={`flex w-full items-center gap-3 rounded-[14px] p-3 text-left ${shop?.id === s.id ? 'border-2 border-teal-700 bg-teal-50' : 'border border-line'}`}
              >
                <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-[#e0efec] text-lg font-extrabold text-teal-700">
                  {s.name.charAt(0)}
                </span>
                <span className="flex min-w-0 flex-col">
                  <span className="truncate font-bold">{s.name}</span>
                  {(s.area || s.address) && <span className="truncate text-sm text-muted">{[s.area, s.address].filter(Boolean).join(' · ')}</span>}
                </span>
              </button>
            </li>
          ))}
        </ul>
      </section>

      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
        <h2 className="text-base font-bold">{t('plan.howGet')}</h2>
        <div className="grid grid-cols-2 gap-2">
          <button onClick={() => setFulfilment('delivery')} aria-pressed={fulfilment === 'delivery'} className={chip(fulfilment === 'delivery')}>{t('plan.delivery')}</button>
          <button onClick={() => setFulfilment('pickup')} aria-pressed={fulfilment === 'pickup'} className={chip(fulfilment === 'pickup')}>{t('plan.pickup')}</button>
        </div>
        <label className="mt-1 block">
          <span className="mb-1 block text-base font-bold">{t('plan.when')}</span>
          <input value={slot} onChange={(e) => setSlot(e.target.value)} maxLength={80} placeholder={t('plan.whenPlaceholder')} className="h-12 w-full rounded-[14px] border border-line px-3 text-[17px]" />
        </label>
        <h2 className="mt-1 text-base font-bold">{t('plan.payment')}</h2>
        <div className="grid grid-cols-3 gap-2">
          {(['cash', 'upi', 'khata'] as const).map((p) => (
            <button key={p} onClick={() => setPayment(p)} aria-pressed={payment === p} className={chip(payment === p)}>
              {p === 'khata' ? t('plan.onKhata') : t(p)}
            </button>
          ))}
        </div>
        <label className="mt-1 block">
          <span className="mb-1 block text-sm font-semibold text-muted">{t('plan.noteForShop')}</span>
          <input value={note} onChange={(e) => setNote(e.target.value)} maxLength={300} className="h-12 w-full rounded-[14px] border border-line px-3 text-[16px]" />
        </label>
      </section>

      <div className="fixed inset-x-0 bottom-[68px] z-10 mx-auto max-w-xl border-t border-line bg-white px-4 py-3">
        <button onClick={send} disabled={!shop || busy || items.length === 0} className="h-14 w-full rounded-2xl bg-teal-700 text-lg font-bold text-white disabled:opacity-40">
          {shop ? `${t('plan.send')} · ${shop.name}` : t('plan.pickShopFirst')}
        </button>
      </div>
    </div>
  )
}

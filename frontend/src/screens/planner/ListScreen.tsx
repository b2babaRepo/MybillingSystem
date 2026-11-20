import { btn } from '../../components/ui'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { AisleTiles, type Aisle } from '../../components/AisleTiles'
import { ItemCard } from '../../components/ItemCard'
import { PendingDot } from '../../components/ui'
import { round2 } from '../../lib/local'
import { formatRupees } from '../../lib/format'
import { addItem, estimateAtShop, removeItem, saveItem, searchCatalogue, useCatalogue, useLists, useShopOptions, useShopSearch, type CatalogueItem, type ListItem, type ListKind, type ShopEntry, type ShopOption } from '../../lib/planner'
import { imageUrl } from '../../lib/images'
import type { Unit } from '../../lib/types'
import { navigate } from '../../router'
import { itemArt } from '../../lib/itemArt'
import { getChosenShop, listEstimate, listItemPhoto, setChosenShop } from './PlanHomeScreen'
import { UnitSelect } from '../BillScreen'

/**
 * Mockup 2: one list. Pick items from the common catalogue (search or browse by category) or type any item;
 * tick, change quantity, remove. Every change is saved on the phone at once.
 */
export function ListScreen({ userId, kind }: { userId: string; kind: ListKind }) {
  const { t, i18n } = useTranslation()
  const { lists } = useLists(userId)
  const list = lists.find((l) => l.kind === kind)!
  const [name, setName] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [unit, setUnit] = useState<Unit>('kg')
  const catalogue = useCatalogue(userId).data ?? []
  const [aisle, setAisle] = useState<Aisle | null>(null)
  const onList = new Set(list.items.map((i) => i.catalogueItemId).filter(Boolean))
  // Once a shop is chosen the list becomes "at that shop": its items, prices and stock. The list itself never loses items.
  const [shop, setShop] = useState<ShopEntry | null>(() => getChosenShop(userId))
  const [changingShop, setChangingShop] = useState(false)
  const shopSearch = useShopSearch('', '', null)
  const options = useShopOptions(shop?.id).data
  const atShop = shop && options ? options : null
  const sold = atShop ? new Set(atShop.map((o) => o.catalogueItemId)) : null
  const inStock = atShop ? new Set(atShop.filter((o) => o.inStock).map((o) => o.catalogueItemId)) : null
  const status = (i: ListItem): 'here' | 'out' | 'notSold' =>
    !sold || !i.catalogueItemId || !sold.has(i.catalogueItemId) ? 'notSold' : inStock!.has(i.catalogueItemId) ? 'here' : 'out'
  // Adding at a shop offers only what it sells, in-stock first; without a shop, the whole common catalogue.
  const shelf = sold ? catalogue.filter((c) => sold.has(c.id)).sort((a, b) => Number(inStock!.has(b.id)) - Number(inStock!.has(a.id))) : catalogue
  const suggestions = searchCatalogue(shelf, name)
  const hereItems = atShop ? list.items.filter((i) => status(i) === 'here') : list.items
  const estimate = atShop ? Math.round(estimateAtShop(hereItems, atShop).total) : listEstimate(list.items, catalogue)
  const chooseShop = (s: ShopEntry | null) => { setChosenShop(userId, s); setShop(s); setChangingShop(false) }
  const optionOf = (i: ListItem): ShopOption | undefined => atShop?.find((o) => o.catalogueItemId === i.catalogueItemId && o.isDefault) ?? atShop?.find((o) => o.catalogueItemId === i.catalogueItemId)

  /** One tap adds a catalogue item with a sensible first quantity (adjust with − and +). */
  async function pick(item: CatalogueItem) {
    const typed = Number(quantity)
    const qty = name.trim() && typed > 0 ? typed : item.unit === 'g' || item.unit === 'ml' ? 100 : 1
    await addItem(userId, kind, item.name, qty, item.unit, item.id)
    setName('')
    setQuantity('1')
  }

  async function add(e: FormEvent) {
    e.preventDefault()
    const qty = Number(quantity)
    if (!name.trim() || !(qty > 0)) return
    await addItem(userId, kind, name.trim(), qty, unit)
    setName('')
    setQuantity('1')
  }

  const update = (item: ListItem, change: Partial<ListItem>) => void saveItem(userId, kind, { ...item, ...change })
  const step = (item: ListItem, direction: 1 | -1) => {
    const size = item.unit === 'g' || item.unit === 'ml' ? 100 : item.unit === 'kg' || item.unit === 'litre' ? 0.5 : 1
    const next = round2(item.quantity + direction * size)
    if (next > 0) update(item, { quantity: next })
  }

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <section className="flex flex-col gap-2 rounded-card bg-white p-3">
        <div className="flex items-center gap-3">
          <span aria-hidden className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-brand text-lg font-extrabold text-white">{shop ? shop.name.charAt(0) : '🏪'}</span>
          <span className="flex min-w-0 flex-1 flex-col">
            <span className="truncate font-bold">{shop ? t('shopFirst.at', { name: shop.name }) : t('shopFirst.none')}</span>
            <span className="truncate text-[13px] text-muted">{shop ? (atShop ? t('shopFirst.summary', { here: hereItems.length, count: list.items.length }) : t('shopFirst.loading')) : t('shopFirst.noneHint')}</span>
          </span>
          <button onClick={() => setChangingShop(!changingShop)} className="min-h-11 shrink-0 rounded-xl border border-line px-3 font-bold text-brand">{shop ? t('shopFirst.change') : t('shopFirst.pick')}</button>
        </div>
        {changingShop && (
          <ul className="flex max-h-64 flex-col gap-1.5 overflow-y-auto">
            {shopSearch.data?.map((s) => (
              <li key={s.id}>
                <button onClick={() => chooseShop(s)} aria-pressed={shop?.id === s.id} className={`flex min-h-12 w-full items-center gap-2 rounded-xl px-3 text-left ${shop?.id === s.id ? 'bg-brand-soft font-bold text-brand-strong' : 'border border-line'}`}>
                  <span className="flex-1 truncate">{s.name}</span>
                  {s.area && <span className="truncate text-sm text-muted">{s.area}</span>}
                </button>
              </li>
            ))}
            {shop && <li><button onClick={() => chooseShop(null)} className="min-h-11 w-full text-sm font-semibold text-muted">{t('shopFirst.clear')}</button></li>}
          </ul>
        )}
      </section>
      <div role="tablist" className="grid grid-cols-2 rounded-[14px] bg-white p-1">
        {(['monthly', 'weekly'] as const).map((k) => (
          <button
            key={k}
            role="tab"
            aria-selected={k === kind}
            onClick={() => navigate(`/list/${k}`)}
            className={`h-11 rounded-[10px] font-semibold ${k === kind ? 'bg-brand text-white' : 'text-muted'}`}
          >
            {t(`plan.${k}`)}
          </button>
        ))}
      </div>

      <form onSubmit={add} className="flex flex-col gap-2 rounded-card bg-white p-3">
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder={t('plan.addPlaceholder')}
          aria-label={t('plan.addPlaceholder')}
          className="h-12 rounded-[14px] border border-line px-3 text-[17px] outline-none focus:border-brand-line"
        />
        {suggestions.length > 0 && (
          <ul className="flex flex-col divide-y divide-[#edf2f1] rounded-[14px] border border-line">
            {suggestions.map((item) => (
              <li key={item.id}>
                <button type="button" onClick={() => void pick(item)} className="flex min-h-12 w-full items-center gap-2 px-3 text-left">
                  <span className="flex-1 font-semibold">{item.name} <span className="font-normal text-muted">· {item.nameEn}</span></span>
                  <span className="text-sm text-muted">~{formatRupees(item.typicalPrice)}/{t(`units.${item.unit}`)}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
        <div className="grid grid-cols-[1fr_1fr_auto] items-end gap-2">
          <label className="block">
            <span className="mb-1 block text-sm font-semibold text-muted">{t('quantity')}</span>
            <input value={quantity} onChange={(e) => setQuantity(e.target.value)} inputMode="decimal" className="h-12 w-full rounded-[14px] border border-line px-3 text-lg" />
          </label>
          <UnitSelect value={unit} onChange={setUnit} />
          <button type="submit" disabled={!name.trim()} className={`h-12 px-5 text-lg rounded-xl ${btn.solid}`}>
            {t('plan.add')}
          </button>
        </div>
      </form>

      {shelf.length > 0 && !name.trim() && (
        <>{atShop && <p className="-mb-1 px-1 text-sm text-muted">{t('shopFirst.shelfHint', { name: shop!.name })}</p>}</>
      )}
      {shelf.length > 0 && !name.trim() && (
        <section className="flex flex-col gap-2 rounded-card bg-white p-3">
          {aisle ? (
            <button onClick={() => setAisle(null)} className="flex min-h-11 items-center gap-1 self-start font-bold text-brand-strong">
              ‹ {i18n.language === 'hi' ? aisle.name : aisle.nameEn}
            </button>
          ) : (
            <h2 className="text-base font-bold">{t('plan.pickItems')}</h2>
          )}
          {!aisle && <AisleTiles available={new Set(shelf.map((i) => i.aisle))} onPick={setAisle} />}
          {aisle && (
            <div className="grid grid-cols-2 gap-2">
              {shelf.filter((i) => i.aisle === aisle.code).map((item) => {
                const added = onList.has(item.id)
                return (
                  <ItemCard
                    key={item.id}
                    item={{ catalogueItemId: item.id, aisle: item.aisle }}
                    title={item.name}
                    subtitle={`${item.nameEn} · ~${formatRupees(item.typicalPrice)}/${t(`units.${item.unit}`)}`}
                    badge={added ? `✓ ${t('plan.onList')}` : '+'}
                    selected={added}
                    onClick={added ? undefined : () => void pick(item)}
                  />
                )
              })}
            </div>
          )}
        </section>
      )}

      {list.items.length === 0 ? (
        <p className="py-6 text-center text-muted">{t('plan.emptyList')}</p>
      ) : (
        (atShop ? (['here', 'out', 'notSold'] as const) : (['all'] as const)).map((group) => {
          const items = group === 'all' ? list.items : list.items.filter((i) => status(i) === group)
          if (items.length === 0) return null
          return (
            <section key={group} className="flex flex-col gap-1.5">
              {group !== 'all' && (
                <h2 className={`px-1 text-[15px] font-extrabold ${group === 'here' ? 'text-brand-strong' : group === 'out' ? 'text-udhaar' : 'text-muted'}`}>
                  {t(`shopFirst.group.${group}`, { count: items.length, name: shop!.name })}
                </h2>
              )}
              {group === 'notSold' && <p className="-mt-1 px-1 text-[13px] text-muted">{t('shopFirst.notSoldHint')}</p>}
              <ul className={`divide-y divide-[#edf2f1] rounded-card bg-white px-3 ${group === 'out' || group === 'notSold' ? 'opacity-80' : ''}`}>
                {items.map((item) => {
                  const option = group === 'all' ? undefined : optionOf(item)
                  const photo = option?.imageAt && shop ? imageUrl(shop.id, option.productId, option.imageAt, 'thumb') : listItemPhoto(item, catalogue)
                  const art = itemArt({ catalogueItemId: item.catalogueItemId, name: item.name })
                  return (
                    <li key={item.id} className="flex items-center gap-2 py-2.5">
                      {photo
                        ? <img src={photo} alt="" loading="lazy" width={44} height={44} className={`h-11 w-11 shrink-0 rounded-xl border border-line bg-white object-contain ${group === 'out' ? 'grayscale' : ''}`} />
                        : <span aria-hidden className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl text-xl" style={{ background: art.tint }}>{art.emoji}</span>}
                      <span className="flex min-w-0 flex-1 flex-col">
                        <span className="flex items-center gap-1.5 text-base font-semibold">
                          <span className="truncate">{item.name}</span>
                          {item.pending && <PendingDot />}
                        </span>
                        {option && <span className="truncate text-[13px] text-muted">{option.name} · {formatRupees(option.price)}/{t(`units.${option.unit}`)}{group === 'out' ? ` · ${t('store.out')}` : ''}</span>}
                      </span>
                      <button aria-label={t('a11y.less')} onClick={() => step(item, -1)} className="h-11 w-10 rounded-[10px] bg-[#edf2f1] text-lg">−</button>
                      <span className="min-w-12 text-center text-[15px] font-bold">{item.quantity} {t(`units.${item.unit}`, { count: item.quantity })}</span>
                      <button aria-label={t('a11y.more')} onClick={() => step(item, 1)} className="h-11 w-10 rounded-[10px] bg-[#edf2f1] text-lg">+</button>
                      <button aria-label={t('remove')} onClick={() => void removeItem(userId, kind, item)} className="h-11 w-8 text-xl text-muted">×</button>
                    </li>
                  )
                })}
              </ul>
            </section>
          )
        })
      )}

      {list.items.length > 0 && (
        <div className="fixed inset-x-0 bottom-[68px] z-10 mx-auto lg:bottom-0 lg:max-w-4xl flex max-w-xl items-center gap-3 border-t border-line bg-white px-4 py-3">
          <span className="flex min-w-0 flex-1 flex-col">
            <span className="text-[13px] text-muted">{atShop ? t('shopFirst.totalAt', { here: hereItems.length, name: shop!.name }) : <>{t('plan.itemsCount', { count: list.items.length })}{estimate > 0 ? ` · ${t('home2.about')}` : ''}</>}</span>
            {estimate > 0 && <span className="text-[22px] leading-tight font-extrabold">{formatRupees(estimate)}</span>}
          </span>
          <button onClick={() => navigate(`/send/${kind}`)} className={`h-14 shrink-0 px-6 text-lg rounded-xl ${btn.solid}`}>
            {shop ? t('plan.send') : t('home2.chooseShop')} ›
          </button>
        </div>
      )}
    </div>
  )
}

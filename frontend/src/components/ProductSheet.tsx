import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatRupees, whatsAppLink } from '../lib/format'
import { allPhotos } from '../lib/images'
import { itemArt } from '../lib/itemArt'
import { dataSaver } from '../lib/search'
import type { Unit } from '../lib/types'
import { btn } from '../lib/buttonStyles'

// Customer app phase 3: the product page. A half-sheet for groceries, a full page for clothes and jewellery (where
// the photo matters most). Swipeable, zoomable photos with a 1/5 counter and no auto-slide; each option with its own
// price; the real MRP; plain delivery words; more from the shop; and a sticky Add beside Ask on WhatsApp.

export interface SheetItem {
  id: string
  name: string
  category: string | null
  unit: Unit
  price: number
  regularPrice?: number | null
  offerName?: string | null
  available: number
  catalogueItemId?: number | null
  imageAt?: string | null
  mrp?: number | null
  packSize?: number | null
  packUnit?: Unit | null
  extraPhotos?: string[] | null
}

export function ProductSheet({ shopId, shopName, shopPhone, item, all, inCart, full, hearted, onHeart, onChange, onPick, onClose, deliveryText }: {
  shopId: string
  shopName: string
  shopPhone: string | null
  item: SheetItem
  all: SheetItem[]
  inCart: (id: string) => number
  full: boolean
  hearted?: boolean
  onHeart?: () => void
  onChange: (item: SheetItem, direction: 1 | -1) => void
  onPick: (item: SheetItem) => void
  onClose: () => void
  deliveryText: string
}) {
  const { t } = useTranslation()
  const saver = dataSaver()
  const photos = allPhotos(shopId, item)
  const strip = useRef<HTMLDivElement>(null)
  const [at, setAt] = useState(0)
  const [zoom, setZoom] = useState(false)
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])
  const [shownId, setShownId] = useState(item.id)
  if (shownId !== item.id) { setShownId(item.id); setAt(0); setZoom(false) }
  useEffect(() => { strip.current?.scrollTo({ left: 0 }) }, [item.id])

  // Options: the shop's other items for the same everyday thing (another brand or pack size), each with its price.
  const options = item.catalogueItemId ? all.filter((x) => x.catalogueItemId === item.catalogueItemId) : [item]
  const more = all.filter((x) => x.id !== item.id && x.available > 0 && (x.category ?? '') === (item.category ?? '') && !options.includes(x)).slice(0, 10)
  const was = item.regularPrice && item.regularPrice > item.price ? item.regularPrice : item.mrp && item.mrp > item.price ? item.mrp : null
  const count = inCart(item.id)
  const out = item.available <= 0
  const art = itemArt(item)
  const size = item.packSize && item.packUnit ? `${item.packSize} ${t(`units.${item.packUnit}`, { count: item.packSize })}` : `/${t(`units.${item.unit}`)}`

  return (
    <div className={`fixed inset-0 z-40 flex ${full ? '' : 'items-end lg:items-center'} justify-center bg-black/40`} onClick={onClose}>
      <div role="dialog" aria-modal="true" aria-label={item.name} onClick={(e) => e.stopPropagation()}
        className={`flex w-full flex-col overflow-hidden bg-ground ${full ? 'h-full max-w-3xl' : 'max-h-[88dvh] max-w-xl rounded-t-3xl lg:rounded-3xl'}`}>
        <div className="flex items-center gap-2 bg-white px-3 py-2">
          <button onClick={onClose} aria-label={t('a11y.close')} className="h-11 w-11 rounded-full text-xl hover:bg-[#f1f5f4]">{full ? '‹' : '✕'}</button>
          <span className="min-w-0 flex-1 truncate font-bold">{full ? item.name : shopName}</span>
          {onHeart && <button onClick={onHeart} aria-pressed={hearted} aria-label={t(hearted ? 'cust4.unheart' : 'cust4.heart')} className={`h-11 w-11 rounded-full text-2xl ${hearted ? 'text-due' : 'text-muted'}`}>{hearted ? '♥' : '♡'}</button>}
        </div>
        <div className="flex-1 overflow-y-auto">
          {/* Photos: swipe, or double-tap to zoom; a counter, not an auto-slide. */}
          <div className={`relative bg-white ${full ? 'aspect-[3/4] max-h-[70dvh]' : 'aspect-[4/3]'}`}>
            {photos.length > 0 ? (
              <div ref={strip} onScroll={(e) => setAt(Math.round(e.currentTarget.scrollLeft / e.currentTarget.clientWidth))}
                className="flex h-full snap-x snap-mandatory overflow-x-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
                {photos.map((p, i) => (
                  <div key={p.full} className="flex h-full w-full shrink-0 snap-center items-center justify-center overflow-hidden" onDoubleClick={() => setZoom(!zoom)}>
                    <img src={saver ? p.thumb : p.full} alt={`${item.name} ${i + 1}`} style={{ touchAction: 'pinch-zoom' }}
                      className={`max-h-full max-w-full object-contain transition-transform duration-200 ${zoom && i === at ? 'scale-[2] cursor-zoom-out' : 'cursor-zoom-in'}`} />
                  </div>
                ))}
              </div>
            ) : (
              <div className="flex h-full items-center justify-center text-7xl" style={{ background: `linear-gradient(135deg, ${art.from}, ${art.to})` }} aria-hidden>{art.emoji}</div>
            )}
            {photos.length > 1 && <span className="absolute right-3 bottom-3 rounded-full bg-black/60 px-2.5 py-0.5 text-sm font-semibold text-white">{at + 1}/{photos.length}</span>}
          </div>

          <div className="flex flex-col gap-3 p-4">
            <h2 className="text-xl font-bold leading-tight">{item.name}</h2>
            <div className="flex flex-wrap items-baseline gap-x-2">
              <span className="text-2xl font-extrabold">{formatRupees(item.price)}</span>
              <span className="text-sm text-muted">{size}</span>
              {was && <span className="text-muted line-through">{item.mrp && was === item.mrp ? `${t('cust4.mrp')} ` : ''}{formatRupees(was)}</span>}
              {was && <span className="font-bold text-green-700">{Math.round((1 - item.price / was) * 100)}% {t('cust4.off')}</span>}
            </div>
            {item.offerName && <span className="w-fit rounded-full bg-amber-100 px-2 py-0.5 text-sm font-bold text-warn-ink">🏷 {item.offerName}</span>}
            {item.available > 0 && item.available <= 5 && <span className="text-sm font-semibold text-warn-ink">{t('cust4.onlyLeft', { count: Math.floor(item.available) })}</span>}

            {options.length > 1 && (
              <div className="flex flex-col gap-2">
                <span className="text-sm font-semibold text-muted">{t('cust4.options')}</span>
                <div className="flex gap-2 overflow-x-auto pb-1">
                  {options.map((o) => (
                    <button key={o.id} onClick={() => onPick(o)} aria-pressed={o.id === item.id} disabled={o.available <= 0}
                      className={`flex shrink-0 flex-col items-start rounded-xl px-3 py-2 text-left disabled:opacity-50 ${o.id === item.id ? 'border-2 border-brand bg-brand-soft' : 'border border-line bg-white'}`}>
                      <span className="max-w-40 truncate text-sm font-semibold">{o.name}</span>
                      <b>{formatRupees(o.price)}</b>
                    </button>
                  ))}
                </div>
              </div>
            )}

            <p className="rounded-xl bg-white p-3 text-sm">🛵 {deliveryText}</p>

            {more.length > 0 && (
              <div className="flex flex-col gap-2">
                <span className="font-bold">{t('cust4.moreFromShop')}</span>
                <div className="flex gap-2 overflow-x-auto pb-1">
                  {more.map((m) => (
                    <button key={m.id} onClick={() => onPick(m)} className="flex w-28 shrink-0 flex-col gap-1 rounded-xl bg-white p-2 text-left shadow-sm">
                      <span className="flex aspect-square items-center justify-center overflow-hidden rounded-lg bg-[#f6f8f7]">
                        {allPhotos(shopId, m)[0] ? <img src={allPhotos(shopId, m)[0].thumb} alt="" loading="lazy" className="h-full w-full object-contain" /> : <span aria-hidden className="text-3xl">{itemArt(m).emoji}</span>}
                      </span>
                      <span className="line-clamp-2 text-xs font-semibold">{m.name}</span>
                      <b className="text-sm">{formatRupees(m.price)}</b>
                    </button>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Sticky: Add (or the stepper) beside Ask on WhatsApp. */}
        <div className="grid grid-cols-2 gap-2 border-t border-line bg-white p-3 pb-[max(12px,env(safe-area-inset-bottom))]">
          {shopPhone ? (
            <a href={whatsAppLink(shopPhone, t('cust4.askText', { item: item.name, shop: shopName }))} target="_blank" rel="noreferrer"
              className="flex h-12 items-center justify-center rounded-xl border border-[#25D366] font-bold text-[#128C7E]">{t('cust4.askWhatsApp')}</a>
          ) : <span />}
          {out ? (
            <span className="flex h-12 items-center justify-center rounded-xl bg-due-soft font-bold text-due">{t('store.out')}</span>
          ) : count === 0 ? (
            <button onClick={() => onChange(item, 1)} className={`h-12 rounded-xl text-lg ${btn.solid}`}>{t('store.add')}</button>
          ) : (
            <span className="flex h-12 items-center justify-between rounded-xl bg-brand px-1 text-white">
              <button onClick={() => onChange(item, -1)} aria-label={t('a11y.less')} className="h-10 w-10 rounded-lg text-2xl">−</button>
              <b>{count} {t(`units.${item.unit}`, { count })}</b>
              <button onClick={() => onChange(item, 1)} disabled={count >= item.available} aria-label={t('a11y.more')} className="h-10 w-10 rounded-lg text-2xl disabled:opacity-40">+</button>
            </span>
          )}
        </div>
      </div>
    </div>
  )
}

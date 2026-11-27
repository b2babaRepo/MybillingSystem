import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { buildMenu, ICONS, kindOf, nameIn, subName, type Category, type MenuCategory } from '../lib/categoryTree'
import { formatRupees } from '../lib/format'
import { itemName } from '../lib/itemName'
import type { Product } from '../lib/types'

const Icon = ({ name, size = 22, stroke = 'currentColor', width = 1.8 }: { name: string; size?: number; stroke?: string; width?: number }) => (
  <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke={stroke} strokeWidth={width} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d={ICONS[name as keyof typeof ICONS] ?? ICONS.box} />
  </svg>
)

/**
 * "सब विभाग": the shop's items by department and category, over the bill (reports/Category menu for item ordering.md).
 * A rail of departments, tiles per category with a count, and inside a tile its items with kind chips, an in-stock
 * filter and a stepper that puts them straight on the bill. The bill bar at the bottom closes it.
 * mode "order": the same menu for a supplier order (unpriced items too), with a Low stock filter, low items first,
 * stock and the last buying rate on each card, and taps adding to the order's rows.
 */
export function CategoryMenu({ products, rank, quantities, onAdd, onLess, billItems, billTotal = 0, onClose, mode = 'bill' }: {
  products: Product[]
  rank: Map<string, number>
  quantities: Map<string, number>
  onAdd: (p: Product) => void
  onLess: (p: Product) => void
  billItems: number
  billTotal?: number
  onClose: () => void
  mode?: Mode
}) {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  const sellable = useMemo(() => products.filter((p) => (mode === 'order' ? !p.isService : p.sellingPrice > 0)), [products, mode])
  const menu = useMemo(() => buildMenu(sellable, rank), [sellable, rank])
  const usual = useMemo(() => sellable.filter((p) => rank.has(p.id)).sort((a, b) => rank.get(a.id)! - rank.get(b.id)!).slice(0, 12), [sellable, rank])
  const [open, setOpen] = useState<MenuCategory | null>(null)
  const [active, setActive] = useState(menu[0]?.department.key ?? '')
  const sections = useRef<Record<string, HTMLElement | null>>({})

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key !== 'Escape') return; if (open) setOpen(null); else onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, onClose])

  const usualCategory: MenuCategory | null = usual.length > 0
    ? { category: { key: 'usual', hi: '★ रोज़ के', en: '★ Your usual', gu: '★ રોજના', icon: 'star', tint: '#fff4e5', ink: '#9a5300' }, items: usual }
    : null

  return (
    <div role="dialog" aria-modal="true" aria-label={t('catMenu.title')} className="fixed inset-0 z-50 flex flex-col bg-ground text-ink lg:inset-y-4 lg:left-1/2 lg:w-[760px] lg:-translate-x-1/2 lg:rounded-3xl lg:shadow-2xl lg:overflow-hidden">
      {open
        ? <Listing entry={open} lang={lang} mode={mode} quantities={quantities} onAdd={onAdd} onLess={onLess} onBack={() => setOpen(null)} />
        : <>
            <header className="flex items-center gap-2 border-b border-line bg-white py-3 pl-4 pr-3">
              <div className="flex flex-1 flex-col">
                <span className="text-xl font-bold leading-tight">{t('catMenu.title')}</span>
                <span className="text-[13px] text-muted">{t('catMenu.count', { count: sellable.length })}</span>
              </div>
              <button onClick={onClose} aria-label={t('catMenu.close')} className="flex h-11 w-11 items-center justify-center rounded-xl bg-line/50">
                <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true"><path d="M6 6l12 12M18 6L6 18" /></svg>
              </button>
            </header>
            <div className="flex min-h-0 flex-1">
              <nav aria-label={t('catMenu.departments')} className="flex w-[92px] shrink-0 flex-col overflow-y-auto border-r border-line bg-white pb-24">
                {usualCategory && (
                  <button onClick={() => setOpen(usualCategory)} className="flex flex-col items-center gap-[3px] border-l-4 border-transparent px-1 py-2.5">
                    <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-warn-soft text-warn"><Icon name="star" /></span>
                    <span className="text-center text-[13px] font-semibold leading-[1.15]">{nameIn(usualCategory.category, lang)}</span>
                  </button>
                )}
                {menu.map(({ department: d }) => {
                  const on = d.key === active
                  return (
                    <button key={d.key} aria-current={on || undefined}
                      onClick={() => { setActive(d.key); sections.current[d.key]?.scrollIntoView({ behavior: 'smooth', block: 'start' }) }}
                      className={`flex flex-col items-center gap-[3px] border-l-4 px-1 py-2.5 ${on ? 'border-brand bg-brand-soft text-brand' : 'border-transparent bg-white'}`}>
                      <span className={`flex h-10 w-10 items-center justify-center rounded-xl ${on ? 'bg-white' : 'bg-ground'}`}><Icon name={d.icon} /></span>
                      <span className="text-center text-[13px] font-semibold leading-[1.15]">{nameIn(d, lang)}</span>
                      <span className="text-center text-[10.5px] leading-[1.1] text-muted">{subName(d, lang)}</span>
                    </button>
                  )
                })}
              </nav>
              <main className="flex min-w-0 flex-1 flex-col gap-[18px] overflow-y-auto px-3 pb-24 pt-3.5">
                {menu.length === 0 && <p className="text-muted">{t('catMenu.empty')}</p>}
                {menu.map(({ department: d, categories }) => (
                  <section key={d.key} ref={(el) => { sections.current[d.key] = el }} className="flex scroll-mt-3 flex-col gap-2.5">
                    <h2 className="m-0 flex items-baseline gap-1.5">
                      <span className="text-[17px] font-bold">{nameIn(d, lang)}</span>
                      <span className="text-[12.5px] font-normal text-muted">{subName(d, lang)}</span>
                    </h2>
                    <div className="grid grid-cols-3 gap-2.5 lg:grid-cols-5">
                      {categories.map((entry) => <Tile key={entry.category.key} c={entry.category} count={entry.items.length} lang={lang} onOpen={() => setOpen(entry)} />)}
                    </div>
                  </section>
                ))}
              </main>
            </div>
          </>}
      <button onClick={onClose} className="relative mx-3 -mt-[76px] mb-3 flex h-[60px] items-center justify-between rounded-2xl bg-brand px-4 text-white shadow-[0_6px_16px_rgba(15,95,88,0.28)]">
        <span className="flex flex-col text-left leading-[1.15]">
          <span className="text-[13px] opacity-90">{t(mode === 'order' ? 'catMenu.order' : 'catMenu.bill', { count: billItems })}</span>
          {mode === 'bill' && <span className="text-[19px] font-bold">{formatRupees(billTotal)}</span>}
        </span>
        <span className="text-base font-bold">{t(mode === 'order' ? 'catMenu.backToOrder' : 'catMenu.viewBill')}</span>
      </button>
    </div>
  )
}

function Tile({ c, count, lang, onOpen }: { c: Category; count: number; lang: string; onOpen: () => void }) {
  return (
    <button onClick={onOpen} className="flex flex-col items-center gap-1 text-ink">
      <span data-light className="relative flex aspect-square w-full items-center justify-center rounded-2xl" style={{ background: c.tint }}>
        <Icon name={c.icon} size={34} stroke={c.ink} width={1.6} />
        <span className="absolute right-1 top-1 rounded-lg bg-white px-[5px] text-[10.5px] font-semibold text-muted">{count}</span>
      </span>
      <span className="text-center text-[13px] font-semibold leading-[1.1]">{nameIn(c, lang)}</span>
      <span className="text-center text-[11px] leading-[1.1] text-muted">{subName(c, lang)}</span>
    </button>
  )
}

type Mode = 'bill' | 'order'

/** Days of stock left at the last month's selling rate; unknown (Infinity) when it doesn't sell or isn't counted. */
const daysLeft = (p: Product) => (p.uncounted || !p.dailySales ? Infinity : p.stock / p.dailySales)

function Listing({ entry, lang, mode, quantities, onAdd, onLess, onBack }: {
  entry: MenuCategory; lang: string; mode: Mode; quantities: Map<string, number>; onAdd: (p: Product) => void; onLess: (p: Product) => void; onBack: () => void
}) {
  const { t } = useTranslation()
  const { category: c, items } = entry
  const [kind, setKind] = useState<string | null>(null)
  const [inStock, setInStock] = useState(false)
  const [onOffer, setOnOffer] = useState(false)
  const [low, setLow] = useState(false)
  const kinds = (c.kinds ?? []).map((kd) => ({ kd, count: items.filter((p) => kindOf(p, c) === kd.key).length })).filter((x) => x.count > 0)
  const shown = items.filter((p) => (!kind || kindOf(p, c) === kind) && (!inStock || p.uncounted || p.isService || p.stock > 0) && (!onOffer || (p.mrp ?? 0) > p.sellingPrice) && (!low || p.isLow))
  // Ordering: what runs out first comes first.
  if (mode === 'order') shown.sort((a, b) => Number(b.isLow) - Number(a.isLow) || daysLeft(a) - daysLeft(b))
  const chip = (on: boolean) => `h-9 shrink-0 rounded-[18px] px-3.5 text-sm font-semibold ${on ? 'bg-brand text-white' : 'bg-line/50 text-ink'}`
  const filter = (on: boolean) => `h-9 shrink-0 rounded-[18px] border px-3 text-[13px] ${on ? 'border-brand bg-brand-soft font-semibold text-brand-strong' : 'border-line bg-white'}`

  return <>
    <header className="flex items-center gap-1.5 bg-white pb-2 pl-1.5 pr-3 pt-3">
      <button onClick={onBack} aria-label={t('catMenu.back')} className="flex h-11 w-11 items-center justify-center">
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d="M15 5l-7 7 7 7" /></svg>
      </button>
      <div className="flex flex-1 flex-col">
        <span className="text-xl font-bold leading-tight">{nameIn(c, lang)}</span>
        <span className="text-[13px] text-muted">{subName(c, lang)} · {t('catMenu.inShop', { count: items.length })}</span>
      </div>
    </header>
    {kinds.length > 0 && (
      <div role="tablist" aria-label={nameIn(c, lang)} className="flex gap-2 overflow-x-auto bg-white px-3 pb-2.5 pt-1">
        <button role="tab" aria-selected={!kind} onClick={() => setKind(null)} className={chip(!kind)}>{t('catMenu.all')} <span className="font-normal opacity-70">{items.length}</span></button>
        {kinds.map(({ kd, count }) => (
          <button key={kd.key} role="tab" aria-selected={kind === kd.key} onClick={() => setKind(kind === kd.key ? null : kd.key)} className={chip(kind === kd.key)}>
            {nameIn(kd, lang)} <span className="font-normal opacity-70">{count}</span>
          </button>
        ))}
      </div>
    )}
    <div className="flex gap-2 overflow-x-auto border-y border-line bg-ground px-3 py-2.5">
      {mode === 'order'
        ? <button aria-pressed={low} onClick={() => setLow(!low)} className={filter(low)}>{t('catMenu.lowStock', { count: items.filter((p) => p.isLow).length })}{low && ' ✕'}</button>
        : <>
            <button aria-pressed={inStock} onClick={() => setInStock(!inStock)} className={filter(inStock)}>{t('catMenu.inStock')}{inStock && ' ✕'}</button>
            <button aria-pressed={onOffer} onClick={() => setOnOffer(!onOffer)} className={filter(onOffer)}>{t('catMenu.offer')}{onOffer && ' ✕'}</button>
          </>}
    </div>
    <main className="grid min-h-0 flex-1 grid-cols-2 content-start gap-2.5 overflow-y-auto px-3 pb-24 pt-3 sm:grid-cols-3 lg:grid-cols-4">
      {shown.length === 0 && <p className="col-span-full text-muted">{t('catMenu.none')}</p>}
      {shown.map((p) => {
        const qty = quantities.get(p.id) ?? 0
        const off = (p.mrp ?? 0) > p.sellingPrice ? p.mrp! - p.sellingPrice : 0
        return (
          <article key={p.id} className="flex flex-col gap-1 rounded-2xl border border-line bg-white p-2">
            <div data-light className="relative flex aspect-[4/3] items-center justify-center rounded-xl" style={{ background: c.tint }}>
              <Icon name={c.icon} size={40} stroke={c.ink} width={1.5} />
              {off > 0 && <span className="absolute left-1.5 top-1.5 rounded-md bg-[#b45309] px-1.5 text-[11px] font-bold text-white">{t('catMenu.off', { amount: formatRupees(off) })}</span>}
            </div>
            <span className="text-sm font-semibold leading-[1.15]">{itemName(p, lang)}</span>
            <span className={`text-xs ${mode === 'order' && p.isLow ? 'font-semibold text-due' : 'text-muted'}`}>{t(`units.${p.unit}`)}{!p.uncounted && !p.isService && <> · {t('catMenu.stock', { count: p.stock })}</>}{mode === 'order' && Number.isFinite(daysLeft(p)) && <> · {t('catMenu.daysLeft', { count: Math.floor(daysLeft(p)) })}</>}</span>
            <div className="mt-0.5 flex items-center justify-between gap-1.5">
              <span className="flex flex-col leading-[1.1]">
                {mode === 'order'
                  ? <span className="text-sm text-muted">{p.costPrice ? t('catMenu.cost', { amount: formatRupees(p.costPrice) }) : ''}</span>
                  : <span className="text-base font-bold">{formatRupees(p.sellingPrice)}</span>}
                {mode === 'bill' && off > 0 && <span className="text-[11.5px] text-muted line-through">{formatRupees(p.mrp!)}</span>}
              </span>
              {qty > 0
                ? <div className="flex h-9 items-center rounded-[10px] bg-brand text-white">
                    <button aria-label={t('catMenu.less')} onClick={() => onLess(p)} className="h-9 w-8 text-xl font-bold">−</button>
                    <span className="min-w-[18px] text-center font-bold">{qty}</span>
                    <button aria-label={t('catMenu.more')} onClick={() => onAdd(p)} className="h-9 w-8 text-xl font-bold">+</button>
                  </div>
                : <button onClick={() => onAdd(p)} className="h-9 rounded-[10px] border-[1.5px] border-brand bg-white px-3.5 text-sm font-bold text-brand-strong">{t('catMenu.add')}</button>}
            </div>
          </article>
        )
      })}
    </main>
  </>
}

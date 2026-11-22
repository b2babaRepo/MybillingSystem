import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Sheet, btn } from '../components/ui'
import { api } from '../lib/api'
import { useProducts } from '../lib/data'
import { itemArt } from '../lib/itemArt'
import { imageUrl } from '../lib/images'
import type { Unit } from '../lib/types'

interface MasterChange { productId: string; productName: string; gstFrom: number | null; gstTo: number | null; hsnFrom: string | null; hsnTo: string | null }

interface MasterItem { id: number; trade: string; category: string; name: string; nameHi: string | null; unit: Unit; gstRate: number | null; packSizes: string[]; imageAt?: string | null; popular?: boolean }

/** Bulk "select all" only once a search or filter has narrowed the list this far: never a thousand items by mistake. */
const SELECT_ALL_LIMIT = 50

/** The popular starter bundle's chip, kept apart from real category names. */
const POPULAR = '\u0000popular'

/**
 * Spelling that doesn't matter when searching: "aata" finds "Atta", "chaval" finds "Chawal", "daal" finds "Dal".
 * Hindi typed in Hindi letters is matched against the Hindi name as it is.
 */
function loose(text: string) {
  return text.toLowerCase().normalize('NFC')
    .replace(/aa/g, 'a').replace(/ee/g, 'i').replace(/oo/g, 'u').replace(/w/g, 'v').replace(/z/g, 'j')
    .replace(/ph/g, 'f').replace(/sh/g, 's').replace(/([bcdgjkpt])h/g, '$1').replace(/y\b/g, 'i').replace(/([a-z])\1+/g, '$1').replace(/[^a-z0-9\u0900-\u097f]+/g, '')
}

/** Each typed word must start one of the item's words (Hindi or English): "aata" finds Atta, not "matar". */
function matches(item: { name: string; nameHi: string | null }, query: string) {
  const words = `${item.name} ${item.nameHi ?? ''}`.split(/[\s()/,.-]+/).map(loose).filter(Boolean)
  return query.split(/\s+/).map(loose).filter(Boolean).every((q) => words.some((w) => w.startsWith(q)))
}

/** Soft backgrounds for picture tiles, picked by the category's name so a category always gets the same one. */
const TINTS = ['#faeeda', '#faece7', '#e1f5ee', '#eeedfe', '#fbeaf0', '#eaf3de', '#e6f1fb']

/**
 * Pick items for the shop from its trades' master lists, like walking a shop aisle: grouped by category, a tap on a
 * row adds it, a whole group can be added at once (with undo), and a tray at the bottom counts what is chosen.
 */
export function PickItemsScreen({ shopId }: { shopId: string }) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const list = useQuery({ queryKey: ['master-items'], queryFn: () => api<MasterItem[]>('/v1/master-items') })
  const updates = useQuery({ queryKey: [shopId, 'master-updates'], queryFn: () => api<{ changes: MasterChange[]; newItems: MasterItem[] }>('/v1/master-items/updates') })
  const newIds = new Set((updates.data?.newItems ?? []).map((i) => i.id))
  const changes = updates.data?.changes ?? []
  const items = useMemo(() => list.data ?? [], [list.data])
  const trades = [...new Set(items.map((i) => i.trade))]
  const [trade, setTrade] = useState('')
  const [category, setCategory] = useState('')
  const [q, setQ] = useState('')
  // The photo opened large: tapping a row's photo shows it big, so the owner can tell packs apart before adding.
  const [zoom, setZoom] = useState<{ src: string; name: string } | null>(null)
  const [chosen, setChosen] = useState<Set<number>>(new Set())
  // Pack sizes ticked per item; an item chosen with none ticked adds every size it comes in.
  const [sizes, setSizes] = useState<Record<number, string[]>>({})
  const [message, setMessage] = useState('')
  const [idea, setIdea] = useState({ name: '', category: '', unit: 'piece' as Unit })
  const [showChanges, setShowChanges] = useState(false)
  const [undo, setUndo] = useState<{ before: Set<number>; count: number } | null>(null)
  const { products } = useProducts(shopId)
  // Items picked before, by the names picking gives them ("आटा", "Bhujia 1 kg"), so the list can say "already in shop".
  const owned = useMemo(() => new Set(products.map((p) => p.name.trim().toLowerCase())), [products])
  // The shop's own photos, by item name, so an item it already photographed shows that photo here too.
  const shopPhotos = useMemo(() => new Map(products.filter((p) => p.imageAt).map((p) => [p.name.trim().toLowerCase(), imageUrl(shopId, p.id, p.imageAt, 'thumb')!])), [products, shopId])
  const shopPhoto = (i: MasterItem) => [i.nameHi ?? i.name, i.name].flatMap((n) => [n, ...i.packSizes.map((s) => `${n} ${s}`)])
    .map((n) => shopPhotos.get(n.toLowerCase())).find(Boolean)
  // Your own photo of the item first, else the master list's, else its drawn picture (🛒 when there's none).
  const photoFor = (i: MasterItem) => shopPhoto(i) ?? (i.imageAt ? `/api/v1/master-images/${i.id}?v=${Date.parse(i.imageAt)}&size=thumb` : undefined)
  const activeTrade = trade || trades[0] || ''
  // The hint shows names from this list, so a clothes shop isn't told to search for aata.
  const searchHint = (([first, second]) => first ? [first.name, first.nameHi, second?.name].filter(Boolean).join(', ') : '')(items.filter((i) => i.trade === activeTrade))
  const categories = [...new Set(items.filter((i) => i.trade === activeTrade).map((i) => i.category))]
  const label = (i: MasterItem) => (i18n.language === 'hi' && i.nameHi ? i.nameHi : i.name)
  const needle = loose(q.trim())
  const tradeItems = items.filter((i) => i.trade === activeTrade)
  const popularCount = tradeItems.filter((i) => i.popular).length
  const shown = tradeItems.filter((i) => (!category || (category === POPULAR ? i.popular : i.category === category))
    && (!needle || matches(i, q)))
  // A picture for each category: the first item in it with a photo, else its first letter on a soft tile.
  const tiles = categories.map((c, n) => ({ name: c, count: tradeItems.filter((i) => i.category === c).length, photo: tradeItems.find((i) => i.category === c && photoFor(i)), first: tradeItems.find((i) => i.category === c), tint: TINTS[n % TINTS.length] }))
  const inShop = (i: MasterItem) => [i.nameHi ?? i.name, i.name].some((n) => owned.has(n.toLowerCase()) || (i.packSizes.length > 0 && owned.has(`${n} ${i.packSizes[0]}`.toLowerCase())))
  const allShown = shown.length > 0 && shown.every((i) => chosen.has(i.id))
  const narrowed = shown.length <= SELECT_ALL_LIMIT
  const groups = useMemo(() => {
    const map = new Map<string, MasterItem[]>()
    if (category === POPULAR) return [[t('pick.popularGroup', { count: shown.length }), shown] as [string, MasterItem[]]]
    for (const i of shown) map.set(i.category, [...(map.get(i.category) ?? []), i])
    return [...map.entries()]
  }, [shown, category, t])

  const toggle = (id: number) => setChosen((c) => { const next = new Set(c); if (next.has(id)) { next.delete(id); setSizes((s) => { const { [id]: _, ...rest } = s; return rest }) } else next.add(id); return next })
  // Tapping a size chooses the item with that size; taking off its last size takes the item off too.
  const toggleSize = (id: number, size: string) => {
    const now = sizes[id] ?? []
    const next = now.includes(size) ? now.filter((s) => s !== size) : [...now, size]
    setSizes((s) => ({ ...s, [id]: next }))
    setChosen((c) => { const set = new Set(c); if (next.length) set.add(id); else set.delete(id); return set })
  }
  const toggleAll = () => setChosen((c) => { const next = new Set(c); shown.forEach((i) => (allShown ? next.delete(i.id) : next.add(i.id))); return next })
  // A whole group at once, skipping what the shop already has; undo puts the choice back as it was.
  const addGroup = (rows: MasterItem[]) => {
    const fresh = rows.filter((i) => !inShop(i) && !chosen.has(i.id))
    if (fresh.length === 0) return
    setUndo({ before: new Set(chosen), count: fresh.length })
    setChosen((c) => new Set([...c, ...fresh.map((i) => i.id)]))
  }
  const thumb = (i: MasterItem) => photoFor(i)
    ? (
      // Inside the row's button, so not a button itself: it stops the tap from also picking the item.
      <span role="button" tabIndex={0} aria-label={t('pick.zoom', { name: label(i) })} title={t('pick.zoom', { name: label(i) })}
        onClick={(e) => { e.stopPropagation(); setZoom({ src: photoFor(i)!.replace(/([?&])size=thumb&?/, '$1').replace(/[?&]$/, ''), name: label(i) }) }}
        onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); e.stopPropagation(); setZoom({ src: photoFor(i)!.replace(/([?&])size=thumb&?/, '$1').replace(/[?&]$/, ''), name: label(i) }) } }}
        className="shrink-0 cursor-zoom-in rounded-[10px] transition-transform hover:scale-110 focus-visible:outline-2 focus-visible:outline-teal-700">
        <img src={photoFor(i)} alt="" loading="lazy" width={44} height={44} className="h-11 w-11 rounded-[10px] bg-white object-contain" />
      </span>
    )
    : <span aria-hidden className="flex h-11 w-11 shrink-0 items-center justify-center rounded-[10px] text-[22px]" style={{ background: itemArt({ name: i.name }).tint }}>{itemArt({ name: i.name }).emoji}</span>

  async function add() {
    setMessage('')
    try {
      const picked = Object.fromEntries([...chosen].filter((id) => sizes[id]?.length).map((id) => [id, sizes[id]]))
      const result = await api<{ created: number; skipped: number }>('/v1/products/from-master', 'POST', { ids: [...chosen], sizes: picked })
      setMessage(t('pick.added', result))
      setChosen(new Set())
      setSizes({})
      setUndo(null)
      void queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  async function applyChanges() {
    setMessage('')
    try {
      const r = await api<{ applied: number }>('/v1/master-items/updates/apply', 'POST', { productIds: changes.map((c) => c.productId) })
      setMessage(t('pick.applied', r))
      setShowChanges(false)
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  async function suggest() {
    setMessage('')
    try {
      await api('/v1/master-items/suggest', 'POST', { trade: activeTrade || null, name: idea.name.trim(), category: idea.category.trim() || 'Other', unit: idea.unit })
      setIdea({ name: '', category: '', unit: 'piece' })
      setMessage(t('pick.suggested'))
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  const side = (on: boolean) => `flex min-h-10 items-center justify-between gap-2 rounded-xl px-3 text-left text-[15px] ${on ? 'bg-brand-soft font-bold text-brand-strong' : 'font-semibold hover:bg-[#f1f5f4]'}`
  const chip = (on: boolean) => `min-h-11 shrink-0 rounded-full px-3 text-sm font-semibold ${on ? 'bg-brand text-white' : 'border border-line bg-white'}`
  return (
    <div className="p-4 pb-28 lg:grid lg:grid-cols-[220px_minmax(0,1fr)] lg:items-start lg:gap-5">
      <aside className="hidden lg:sticky lg:top-20 lg:flex lg:max-h-[calc(100dvh-6rem)] lg:flex-col lg:gap-0.5 lg:overflow-y-auto lg:rounded-card lg:bg-white lg:p-2" aria-label={t('pick.suggestCategory')}>
        {popularCount > 0 && <button onClick={() => setCategory(POPULAR)} className={side(category === POPULAR)}>★ {t('pick.popular', { count: popularCount })}</button>}
        <button onClick={() => setCategory('')} className={side(!category)}><span>{t('pick.all')}</span><span className="text-xs text-muted">{tradeItems.length}</span></button>
        {tiles.map((c) => <button key={c.name} onClick={() => setCategory(c.name)} className={side(c.name === category)}><span className="truncate">{c.name}</span><span className="text-xs text-muted">{c.count}</span></button>)}
      </aside>
      <div className="flex min-w-0 flex-col gap-3">
      <div className="-mx-4 -mt-4 lg:mx-0 lg:mt-0 lg:rounded-[22px] lg:pt-3 flex items-center justify-between gap-2 rounded-b-[22px] bg-brand px-4 pb-4 pt-1 text-white">
        <span className="flex flex-col">
          <h1 className="text-xl font-extrabold leading-tight">{t('pick.titleShort')}</h1>
          <span className="text-sm opacity-90">{t('pick.listCount', { trade: t(`shopType.${activeTrade}`), count: tradeItems.length })}</span>
        </span>
        {changes.length > 0 && (
          <button onClick={() => setShowChanges(true)} className="flex min-h-9 shrink-0 items-center gap-1 rounded-full bg-white/20 px-3 text-sm font-semibold text-white">
            <svg aria-hidden width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9" /><path d="M10 21a2 2 0 0 0 4 0" /></svg>
            {t('pick.changesPill', { count: changes.length })}
          </button>
        )}
      </div>
      {showChanges && (
        <Sheet title={t('pick.changes', { count: changes.length })} onClose={() => setShowChanges(false)}>
          <p className="text-sm text-muted">{t('pick.changesHint')}</p>
          <ul className="my-3 flex flex-col gap-2 text-sm">
            {changes.map((c) => (
              <li key={c.productId} className="rounded-xl bg-white p-2"><b>{c.productName}</b><br /> {c.gstFrom !== c.gstTo ? `GST ${c.gstFrom ?? '–'}% → ${c.gstTo}%` : ''}{c.gstFrom !== c.gstTo && c.hsnFrom !== c.hsnTo ? ' · ' : ''}{c.hsnFrom !== c.hsnTo ? `HSN ${c.hsnFrom ?? '–'} → ${c.hsnTo}` : ''}</li>
            ))}
          </ul>
          <Button className="w-full" onClick={() => void applyChanges()}>{t('pick.apply', { count: changes.length })}</Button>
        </Sheet>
      )}
      {newIds.size > 0 && <p className="rounded-xl bg-brand-soft p-3 text-sm font-semibold text-brand-deep">{t('pick.newCount', { count: newIds.size })}</p>}
      <p className="text-sm text-muted">{t('pick.hint2')}</p>
      {trades.length > 1 && (
        <div className="flex gap-2 overflow-x-auto">
          {trades.map((x) => <button key={x} onClick={() => { setTrade(x); setCategory('') }} className={chip(x === activeTrade)}>{t(`shopType.${x}`)}</button>)}
        </div>
      )}
      <input value={q} onChange={(e) => setQ(e.target.value)} type="search" placeholder={searchHint ? t('pick.searchAny', { hint: searchHint }) : t('pick.search')} aria-label={t('pick.search')} className="h-12 rounded-xl border border-line bg-white px-4 text-[16px]" />
      <div className="flex gap-2 overflow-x-auto lg:hidden">
        {popularCount > 0 && <button onClick={() => setCategory(POPULAR)} className={chip(category === POPULAR)}>★ {t('pick.popular', { count: popularCount })}</button>}
        <button onClick={() => setCategory('')} className={chip(!category)}>{t('pick.all')}</button>
        {categories.map((c) => <button key={c} onClick={() => setCategory(c)} className={chip(c === category)}>{c}</button>)}
      </div>
      {!category && !needle && tiles.length > 1 && (
        <div className="-mx-4 flex gap-3 overflow-x-auto px-4 pb-1 lg:hidden">
          {tiles.map((c) => (
            <button key={c.name} onClick={() => setCategory(c.name)} className="flex w-[76px] shrink-0 flex-col items-center gap-1 text-center">
              <span className="flex h-[68px] w-[68px] items-center justify-center overflow-hidden rounded-2xl shadow-[0_1px_3px_rgba(16,32,29,0.1)]" style={{ background: c.tint }}>
                {c.photo
                  ? <img src={photoFor(c.photo)} alt="" loading="lazy" width={68} height={68} className="h-full w-full object-cover" />
                  : <span aria-hidden className="text-[30px]">{c.first ? itemArt({ name: c.first.name }).emoji : '🛒'}</span>}
              </span>
              <span className="line-clamp-2 text-[12px] font-semibold leading-tight">{c.name}</span>
              <span className="text-[11px] text-muted">{c.count}</span>
            </button>
          ))}
        </div>
      )}
      {list.isLoading && <p className="text-muted">…</p>}
      {shown.length > 0 && narrowed && (q.trim() || category) && (
        <label className="flex min-h-11 items-center gap-2 font-semibold">
          <input type="checkbox" checked={allShown} onChange={toggleAll} className="h-5 w-5" />{t('pick.chooseAll', { count: shown.length })}
        </label>
      )}
      {groups.map(([group, rows]) => (
        <section key={group} className="[content-visibility:auto] [contain-intrinsic-size:auto_600px]">
          <div className="sticky top-0 z-[5] -mx-4 flex items-center justify-between bg-ground/95 px-4 py-2 backdrop-blur-sm lg:top-16 lg:mx-0 lg:px-1">
            <h2 className="text-base font-bold">{group}{category === POPULAR ? '' : <span className="text-sm font-semibold text-muted"> · {rows.length}</span>}</h2>
            <button onClick={() => addGroup(rows)} className={`min-h-9 px-3 text-sm rounded-xl ${btn.outline}`}>{category === POPULAR ? t('pick.addAllCount', { count: rows.length }) : t('pick.addGroup')}</button>
          </div>
          <ul className="overflow-hidden rounded-card bg-white shadow-[0_1px_3px_rgba(16,32,29,0.08)] lg:grid lg:grid-cols-2 lg:gap-px lg:bg-line xl:grid-cols-3">
            {rows.map((i) => {
              const on = chosen.has(i.id)
              const have = inShop(i)
              const other = i18n.language === 'hi' ? (i.nameHi ? i.name : null) : i.nameHi
              return (
                <li key={i.id} className="border-b border-line last:border-b-0 lg:border-b-0 lg:bg-white">
                  <button onClick={() => !have && toggle(i.id)} aria-pressed={on} disabled={have}
                    className={`flex min-h-16 w-full items-center gap-3 py-2 pr-3 pl-3 text-left transition-colors ${on ? 'bg-brand-soft shadow-[inset_4px_0_0_#0f6b5c]' : ''}`}>
                    {thumb(i)}
                    <span className="flex min-w-0 flex-1 flex-col leading-[1.45]">
                      <span className="truncate text-[16px] font-semibold">{label(i)}{newIds.has(i.id) && <span className="ml-2 rounded-full bg-brand px-2 align-middle text-[11px] font-bold text-white">{t('pick.new')}</span>}</span>
                      <span className="truncate text-[13px] text-muted">{other ?? t(`units.${i.unit}`)}</span>
                    </span>
                    {have
                      ? <span className="shrink-0 rounded-full bg-[#edf2f1] px-2 py-1 text-xs font-semibold text-muted">{t('pick.inShop')}</span>
                      : <span className={`flex h-9 min-w-[76px] shrink-0 items-center justify-center rounded-xl border-2 border-brand text-sm font-bold transition-colors ${on ? 'bg-brand text-white' : 'text-brand-strong'}`}>{on ? (i.packSizes.length > 0 ? `✓ ${(sizes[i.id]?.length || i.packSizes.length)}` : `✓ ${t('pick.picked')}`) : `+ ${t('pick.addOne')}`}</span>}
                  </button>
                  {!have && (i.packSizes.length > 0 || i.gstRate != null) && (
                    <div className={`-mt-1 flex flex-wrap items-center gap-1.5 pb-2.5 pl-[68px] pr-3 ${on ? 'bg-brand-soft' : ''}`}>
                      {i.packSizes.map((s) => {
                        const ticked = (sizes[i.id] ?? []).includes(s) || (on && !(sizes[i.id]?.length))
                        return (
                          <button key={s} onClick={() => toggleSize(i.id, s)} aria-pressed={ticked}
                            className={`min-h-8 rounded-full border px-3 text-[13px] font-semibold ${ticked ? 'border-brand bg-brand text-white' : 'border-line bg-white text-ink'}`}>{s}</button>
                        )
                      })}
                      {i.gstRate != null && <span className="text-[12px] text-muted">GST {i.gstRate}%</span>}
                    </div>
                  )}
                </li>
              )
            })}
          </ul>
        </section>
      ))}
      <details className="rounded-card bg-white p-3">
        <summary className="min-h-11 cursor-pointer font-semibold">{t('pick.suggest')}</summary>
        <p className="text-xs text-muted">{t('pick.suggestHint')}</p>
        <div className="mt-2 grid grid-cols-2 gap-2">
          <input value={idea.name} onChange={(e) => setIdea({ ...idea, name: e.target.value })} placeholder={t('pick.suggestName')} aria-label={t('pick.suggestName')} className="col-span-2 h-12 rounded-xl border border-line px-3" />
          <input value={idea.category} onChange={(e) => setIdea({ ...idea, category: e.target.value })} placeholder={t('pick.suggestCategory')} aria-label={t('pick.suggestCategory')} className="h-12 rounded-xl border border-line px-3" />
          <select value={idea.unit} onChange={(e) => setIdea({ ...idea, unit: e.target.value as Unit })} aria-label={t('pick.suggestUnit')} className="h-12 rounded-xl border border-line px-3">
            {(['piece', 'kg', 'g', 'litre', 'ml', 'packet', 'bag'] as Unit[]).map((u) => <option key={u} value={u}>{t(`units.${u}`)}</option>)}
          </select>
          <Button className="col-span-2" disabled={idea.name.trim().length < 2} onClick={() => void suggest()}>{t('pick.suggestSend')}</Button>
        </div>
      </details>
      {message && <p className="rounded-xl bg-brand-soft p-3 text-sm font-semibold text-brand-deep">{message}</p>}
      <div className="fixed inset-x-0 bottom-[68px] z-10 mx-auto lg:bottom-0 lg:left-auto lg:right-6 lg:w-[440px] lg:max-w-none flex max-w-xl flex-col gap-2 px-3 pb-3">
        {undo && (
          <div role="status" className="flex items-center justify-between rounded-xl bg-ink px-4 py-2 text-sm text-white">
            <span>{t('pick.groupAdded', { count: undo.count })}</span>
            <button onClick={() => { setChosen(undo.before); setUndo(null) }} className="min-h-9 px-2 font-bold text-amber-300">{t('pick.undo')}</button>
          </div>
        )}
        {chosen.size === 0
          ? <p className="rounded-xl bg-white/95 px-4 py-3 text-center text-sm text-muted shadow-[0_-1px_6px_rgba(16,32,29,0.08)]">{t('pick.tapHint')}</p>
          : (
            <div className="flex items-center justify-between gap-3 rounded-2xl bg-[#085041] px-4 py-3 text-white shadow-lg">
              <span className="text-base"><b className="text-xl">{chosen.size}</b> {t('pick.chosen')}</span>
              <button onClick={() => void add()} className="min-h-11 rounded-xl bg-white px-4 font-bold text-[#085041]">{t('pick.addToShop')} →</button>
            </div>
          )}
      </div>
      {zoom && (
        <div role="dialog" aria-modal="true" aria-label={zoom.name} onClick={() => setZoom(null)} onKeyDown={(e) => e.key === 'Escape' && setZoom(null)} tabIndex={-1} ref={(el) => el?.focus()}
          className="fixed inset-0 z-50 flex cursor-zoom-out flex-col items-center justify-center gap-3 bg-black/80 p-4">
          <img src={zoom.src} alt={zoom.name} className="max-h-[75dvh] max-w-full rounded-2xl bg-white object-contain p-2" />
          <p className="text-center text-lg font-semibold text-white">{zoom.name}</p>
          <button type="button" onClick={() => setZoom(null)} className="h-11 rounded-xl bg-white px-6 font-bold text-ink">{t('pick.close')}</button>
        </div>
      )}
      </div>
    </div>
  )
}

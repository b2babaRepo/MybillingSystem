import { formatRupees } from '../lib/format'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState, type KeyboardEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { api } from '../lib/api'
import { useProducts } from '../lib/data'
import { uuidv7 } from '../lib/ids'
import { shownLocale } from '../i18n'
import { itemName } from '../lib/itemName'
import { SortHead } from '../components/TableHead'
import { compareCells, filterBox, nextSort, type Sort } from '../lib/tableSort'
import type { Product } from '../lib/types'
import { markCountDone, takeCountCategory } from '../lib/dailyCount'
import { packGroups, sizeOf } from '../lib/packGroups'

/** What one item's count holds: the total in its unit, and for items kept in bags the bags and the loose part. */
interface Count { bags?: string; loose?: string; quintal?: boolean; cost?: string; price?: string }

/**
 * Buying cost and selling price beside the count, for an item that has none yet (a shop just set up from the common
 * list): stock and prices in one pass. Saved with the count.
 */
function PriceBoxes({ p, parts, onChange }: { p: Product; parts: Count; onChange: (next: Count) => void }) {
  const { t } = useTranslation()
  if (p.isService || (p.sellingPrice > 0 && p.costPrice != null)) return null
  const box = 'h-9 w-20 rounded-lg border border-line px-2 text-right'
  return (
    <span className="mt-1 flex flex-wrap items-center gap-1.5 text-xs text-muted">
      <label className="flex items-center gap-1">{t('opening.cost')} ₹
        <input value={parts.cost ?? (p.costPrice != null ? String(p.costPrice) : '')} onChange={(e) => onChange({ ...parts, cost: e.target.value })} inputMode="decimal"
          aria-label={`${t('opening.cost')}: ${p.name}`} className={box} /></label>
      <label className="flex items-center gap-1">{t('opening.price')} ₹
        <input value={parts.price ?? (p.sellingPrice > 0 ? String(p.sellingPrice) : '')} onChange={(e) => onChange({ ...parts, price: e.target.value })} inputMode="decimal"
          aria-label={`${t('opening.price')}: ${p.name}`} className={box} /></label>
    </span>
  )
}

/**
 * One item's count box, in the units the shelf is counted in: a bag item takes "3 बोरी + 20 kg" (saved as 170 kg and
 * 3 sealed bags); a kg item can be typed in quintal (1.5 → 150 kg); anything else is one number in its own unit.
 */
function CountBox({ p, value, parts, onChange, big }: {
  p: Product; value: string; parts: Count; onChange: (total: string, parts: Count) => void; big?: boolean
}) {
  const { t } = useTranslation()
  const box = `rounded-xl border border-line px-2 text-right ${big ? 'h-11 text-lg lg:h-9' : 'h-9'}`
  const unit = t(`units.${p.unit}`, { count: 2 })
  if (p.bagSize) {
    const size = p.bagSize
    const set = (next: Count) => {
      const bags = Number(next.bags || 0)
      const loose = Number(next.loose || 0)
      const filled = (next.bags ?? '').trim() !== '' || (next.loose ?? '').trim() !== ''
      onChange(filled && !isNaN(bags) && !isNaN(loose) ? String(Math.round((bags * size + loose) * 1000) / 1000) : '', next)
    }
    return (
      <span className="flex flex-wrap items-center justify-end gap-1 text-sm">
        <input value={parts.bags ?? ''} onChange={(e) => set({ ...parts, bags: e.target.value.replace(/[^\d]/g, '') })} inputMode="numeric"
          aria-label={`${t('opening.bags')}: ${p.name}`} placeholder="0" className={`${box} w-14`} />
        <span className="text-muted">{t('opening.bags')} +</span>
        <input value={parts.loose ?? ''} onChange={(e) => set({ ...parts, loose: e.target.value })} inputMode="decimal"
          aria-label={`${t('opening.loose')}: ${p.name}`} placeholder="0" className={`${box} w-16`} />
        <span className="text-muted">{unit}</span>
        {value && <span className="text-xs font-semibold text-brand-strong">= {value} {unit}</span>}
      </span>
    )
  }
  if (p.unit === 'kg') {
    const shown = parts.quintal ? (value ? String(Math.round((Number(value) / 100) * 1000) / 1000) : '') : value
    return (
      <span className="flex items-center gap-1 text-sm">
        <input value={shown} onChange={(e) => { const typed = e.target.value; onChange(parts.quintal && typed.trim() !== '' && !isNaN(Number(typed)) ? String(Math.round(Number(typed) * 100 * 1000) / 1000) : typed, parts) }}
          inputMode="decimal" aria-label={`${t('stock.counted')}: ${p.name}`} placeholder={t('stock.counted')} className={`${box} w-20`} />
        <button type="button" aria-pressed={!!parts.quintal} onClick={() => onChange(value, { ...parts, quintal: !parts.quintal })}
          className={`h-9 rounded-lg border px-2 text-xs font-bold ${parts.quintal ? 'border-brand bg-brand-soft text-brand-strong' : 'border-line text-muted'}`}>{parts.quintal ? t('opening.quintal') : 'kg'}</button>
      </span>
    )
  }
  return <input value={value} onChange={(e) => onChange(e.target.value, parts)} inputMode="decimal" aria-label={`${t('stock.counted')}: ${p.name}`}
    placeholder={t('stock.counted')} className={`${box} w-24`} />
}

interface LastIn { productId: string; at: string; quantity: number; supplier: string | null; byHand: boolean }

/**
 * Count the shelf, item by item. Only counted items are sent; each difference becomes a "count fix"
 * adjustment. The stocktake carries one ID, so pressing save twice corrects once.
 */
export function StocktakeScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { products } = useProducts(shopId)
  const draftKey = `gk-count-draft:${shopId}`
  // Read once, when the screen opens.
  const [draft] = useState(() => { try { return JSON.parse(localStorage.getItem(draftKey) ?? 'null') as { counts: Record<string, string>; parts: Record<string, Count> } | null } catch { return null } })
  const [counts, setCounts] = useState<Record<string, string>>(() => draft?.counts ?? {})
  // How each count was typed (bags + loose, quintal), kept beside the total.
  const [parts, setParts] = useState<Record<string, Count>>(() => draft?.parts ?? {})
  useEffect(() => {
    try {
      if (Object.values(counts).some((v) => v.trim() !== '')) localStorage.setItem(draftKey, JSON.stringify({ counts, parts }))
      else localStorage.removeItem(draftKey)
    } catch { /* storage off: the count lasts while the screen is open */ }
  }, [counts, parts, draftKey])
  const resumed = !!draft && Object.values(draft.counts).some((v) => v.trim() !== '')
  const setCount = (id: string, total: string, next: Count) => { setCounts((c) => ({ ...c, [id]: total })); setParts((x) => ({ ...x, [id]: next })) }
  const [stocktakeId, setStocktakeId] = useState(uuidv7)
  // Opened from the home card: today's category only, until "all" is tapped.
  const [only, setOnly] = useState<string | null>(() => takeCountCategory(shopId))
  // When each item's stock last came in and from whom, so a count can tell where stock came from.
  const lastIn = new Map((useQuery({ queryKey: [shopId, 'stock-last-in'], queryFn: () => api<LastIn[]>('/v1/stock/last-in') }).data ?? []).map((l) => [l.productId, l]))
  // Counted under their category, as the shelves are kept.
  const groups = Object.entries(products.filter((p) => !p.uncounted).reduce<Record<string, typeof products>>((g, p) => {
    const key = p.category?.trim() || ''
    ;(g[key] ??= []).push(p)
    return g
  }, {})).sort(([a], [b]) => (a === '' ? 1 : b === '' ? -1 : a.localeCompare(b)))
  // Desktop count sheet: sort by a column, filter by name and by what has been counted.
  type ColKey = 'item' | 'books' | 'diff' | 'value'
  const [sort, setSort] = useState<Sort<ColKey>>({ key: 'item', up: true })
  const [filter, setFilter] = useState({ item: '', show: '' })
  const diffOf = (p: (typeof products)[number]) => { const c = counts[p.id]?.trim(); return c && !isNaN(Number(c)) ? Math.round((Number(c) - p.stock) * 1000) / 1000 : null }
  const [result, setResult] = useState('')
  const [busy, setBusy] = useState(false)
  const counted = Object.entries(counts).filter(([, v]) => v.trim() !== '' && Number(v) >= 0)
  // The shop's stock at cost as it is being counted: counted quantities where typed, the books elsewhere.
  const stockValue = products.filter((p) => !p.isService && !p.uncounted).reduce((sum, p) => {
    const c = (counts[p.id] ?? '').trim()
    const qty = c !== '' && !isNaN(Number(c)) ? Number(c) : p.stock
    const typed = (parts[p.id]?.cost ?? '').trim()
    const cost = typed !== '' && !isNaN(Number(typed)) ? Number(typed) : p.costPrice ?? 0
    return sum + Math.max(0, qty) * cost
  }, 0)
  const pricesTyped = products.filter((p) => (parts[p.id]?.price ?? '').trim() !== '' || (parts[p.id]?.cost ?? '').trim() !== '').length

  async function save() {
    setBusy(true)
    try {
      const priced = products.filter((p) => {
        const x = parts[p.id]
        return x && ((x.price ?? '').trim() !== '' || (x.cost ?? '').trim() !== '')
      }).map((p) => {
        const x = parts[p.id]
        const price = (x.price ?? '').trim() !== '' ? Number(x.price) : p.sellingPrice
        const cost = (x.cost ?? '').trim() !== '' ? Number(x.cost) : null
        return { productId: p.id, sellingPrice: Math.round(price * 100) / 100, costPrice: cost == null || isNaN(cost) ? null : Math.round(cost * 100) / 100 }
      }).filter((r) => !isNaN(r.sellingPrice) && r.sellingPrice >= 0)
      if (priced.length > 0) await api('/v1/products/rates', 'PUT', { rates: priced })
      const done = counted.length === 0 ? { adjusted: 0, unchanged: 0 } : await api<{ adjusted: number; unchanged: number }>('/v1/stocktakes', 'POST', {
        id: stocktakeId,
        counts: counted.map(([productId, value]) => ({ productId, counted: Number(value), bags: parts[productId]?.bags?.trim() ? Number(parts[productId].bags) : null })),
      })
      setResult(t('stock.stocktakeDone', done))
      void queryClient.invalidateQueries({ queryKey: [shopId] })
      // Saved: the next count starts clean, with its own id.
      setCounts({}); setParts({}); setStocktakeId(uuidv7())
      markCountDone(shopId)
      try { localStorage.removeItem(draftKey) } catch { /* storage off */ }
    } catch (err) {
      setResult(err instanceof Error ? err.message : t('error'))
    } finally {
      setBusy(false)
    }
  }

  function nextBox(e: KeyboardEvent<HTMLDivElement>) {
    if (e.key !== 'Enter' || !(e.target instanceof HTMLInputElement)) return
    const boxes = [...e.currentTarget.querySelectorAll<HTMLInputElement>('input[inputmode]')].filter((b) => b.offsetParent !== null)
    const next = boxes[boxes.indexOf(e.target) + 1]
    if (next) { e.preventDefault(); next.focus(); next.select() }
  }

  return (
    <div className="flex flex-col gap-3 p-4 pb-28" onKeyDown={nextBox}>
      <p className="text-muted">{t('stock.stocktakeHint')}</p>
      {resumed && !result && <p className="rounded-xl bg-brand-soft p-3 text-sm font-semibold text-brand-deep">{t('opening.resumed')}</p>}
      {result && <p className="rounded-xl bg-emerald-50 p-3 font-semibold text-emerald-800">{result}</p>}
      {only && (
        <p className="flex items-center justify-between gap-2 rounded-xl bg-brand-soft p-3 text-sm font-semibold text-brand-deep">
          <span>{t('opening.onlyToday', { name: only })}</span>
          <button onClick={() => setOnly(null)} className="shrink-0 underline">{t('opening.showAll')}</button>
        </p>
      )}
      {groups.filter(([category]) => !only || category === only).map(([category, items]) => (
      <section key={category} className="flex flex-col gap-1">
      {(() => {
        const done = items.filter((p) => (counts[p.id] ?? '').trim() !== '').length
        return (
          <h2 className="flex items-center justify-between px-1 text-sm font-bold text-muted">
            <span>{category || t('stock.noCategory')}</span>
            <span className={done === items.length ? 'text-jama' : done > 0 ? 'text-brand-strong' : ''}>{done === items.length ? '✓ ' : ''}{t('opening.progress', { done, count: items.length })}</span>
          </h2>
        )
      })()}
      {/* A computer: a count sheet, one row per item, the difference and what it is worth as you type. */}
      <table className="hidden w-full table-fixed border-collapse overflow-hidden rounded-card bg-white text-sm lg:table">
        <thead className="bg-brand-soft text-left text-xs tracking-wide">
          <tr className="align-top">
            <th className="w-10 px-3 py-2 uppercase">#</th>
            <th className="px-2 py-2"><SortHead label={t('stock.colItem')} active={sort.key === 'item'} up={sort.up} onClick={() => setSort(nextSort(sort, 'item', true))} sortLabel={t('billList.sortBy', { col: t('stock.colItem') })}>
              <input value={filter.item} onChange={(e) => setFilter({ ...filter, item: e.target.value })} aria-label={t('billList.filterBy', { col: t('stock.colItem') })} className={filterBox} /></SortHead></th>
            <th className="w-28 px-2 py-2 text-right"><SortHead right label={t('stockTable.books')} active={sort.key === 'books'} up={sort.up} onClick={() => setSort(nextSort(sort, 'books', false))} sortLabel={t('billList.sortBy', { col: t('stockTable.books') })} /></th>
            <th className="w-56 px-2 py-2 uppercase">{t('stockTable.lastIn')}</th>
            <th className="w-32 px-2 py-2 text-right uppercase">{t('stock.counted')}
              <select value={filter.show} onChange={(e) => setFilter({ ...filter, show: e.target.value })} aria-label={t('billList.filterBy', { col: t('stock.counted') })} className={filterBox}>
                <option value="">{t('billList.status.all')}</option><option value="done">{t('stockTable.done')}</option><option value="todo">{t('stockTable.todo')}</option><option value="differs">{t('stockTable.differs')}</option>
              </select></th>
            <th className="w-24 px-2 py-2 text-right"><SortHead right label={t('stock.colDiff')} active={sort.key === 'diff'} up={sort.up} onClick={() => setSort(nextSort(sort, 'diff', false))} sortLabel={t('billList.sortBy', { col: t('stock.colDiff') })} /></th>
            <th className="w-32 px-3 py-2 text-right"><SortHead right label={t('stockTable.value')} active={sort.key === 'value'} up={sort.up} onClick={() => setSort(nextSort(sort, 'value', false))} sortLabel={t('billList.sortBy', { col: t('stockTable.value') })} /></th>
          </tr>
        </thead>
        <tbody className="divide-y divide-line">
          {items.every((p) => !((!filter.item || itemName(p).toLowerCase().includes(filter.item.toLowerCase()))
            && (!filter.show || (filter.show === 'done' ? diffOf(p) != null : filter.show === 'todo' ? diffOf(p) == null : (diffOf(p) ?? 0) !== 0))))
            && <tr><td colSpan={7} className="px-3 py-2 text-center text-xs text-muted">{t('stockTable.noMatch')}</td></tr>}
          {items
            .filter((p) => (!filter.item || itemName(p).toLowerCase().includes(filter.item.toLowerCase()))
              && (!filter.show || (filter.show === 'done' ? diffOf(p) != null : filter.show === 'todo' ? diffOf(p) == null : (diffOf(p) ?? 0) !== 0)))
            .sort((a, b) => {
              const val = (p: (typeof items)[number]) => sort.key === 'item' ? itemName(p) : sort.key === 'books' ? p.stock
                : sort.key === 'diff' ? diffOf(p) : diffOf(p) != null && p.costPrice ? diffOf(p)! * p.costPrice : null
              const c = compareCells(val(a), val(b))
              return sort.up ? c : -c
            })
            .map((p, n) => {
            const c = counts[p.id]?.trim()
            const diff = c && !isNaN(Number(c)) ? Math.round((Number(c) - p.stock) * 1000) / 1000 : null
            const value = diff != null && p.costPrice ? Math.round(diff * p.costPrice * 100) / 100 : null
            const l = lastIn.get(p.id)
            const when = l ? new Date(l.at).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' }) : null
            return (
              <tr key={p.id} className={diff != null && diff !== 0 ? (diff < 0 ? 'bg-due-soft/40' : 'bg-green-50/60') : ''}>
                <td className="px-3 py-1.5 text-muted">{n + 1}</td>
                <td className="truncate px-2 py-1.5 font-semibold" title={itemName(p)}>{itemName(p)}</td>
                <td className={`whitespace-nowrap px-2 py-1.5 text-right ${p.stock < 0 ? 'font-semibold text-due' : ''}`}>{p.stock} {t(`units.${p.unit}`, { count: p.stock })}</td>
                <td className="px-2 py-1.5 text-xs text-muted">{l && when ? (l.byHand ? <span className="text-warn">{t('stock.lastInHand', { date: when, qty: l.quantity })}</span> : t('stock.lastIn', { date: when, qty: l.quantity, supplier: l.supplier })) : '—'}</td>
                <td className="px-2 py-1 text-right">
                  <CountBox p={p} value={counts[p.id] ?? ''} parts={parts[p.id] ?? {}} onChange={(total, next) => setCount(p.id, total, next)} />
                </td>
                <td className={`px-2 py-1.5 text-right font-bold ${diff == null ? 'text-muted' : diff < 0 ? 'text-due' : diff > 0 ? 'text-brand-strong' : 'text-muted'}`}>{diff == null ? '—' : diff > 0 ? `+${diff}` : diff}</td>
                <td className={`px-3 py-1.5 text-right ${value == null ? 'text-muted' : value < 0 ? 'text-due' : value > 0 ? 'text-jama' : 'text-muted'}`}>{value == null ? '—' : `${value > 0 ? '+' : ''}${formatRupees(value)}`}</td>
              </tr>
            )
          })}
        </tbody>
        <tfoot>
          {(() => {
            const done = items.filter((p) => counts[p.id]?.trim() && !isNaN(Number(counts[p.id])))
            const worth = done.reduce((sum, p) => sum + (p.costPrice ? (Number(counts[p.id]) - p.stock) * p.costPrice : 0), 0)
            return (
              <tr className="border-t-2 border-ink font-bold">
                <td colSpan={4} className="px-3 py-2">{t('stockTable.counted', { done: done.length, count: items.length })}</td>
                <td /><td />
                <td className={`px-3 py-2 text-right ${worth < 0 ? 'text-due' : worth > 0 ? 'text-jama' : ''}`}>{worth > 0 ? '+' : ''}{formatRupees(Math.round(worth * 100) / 100)}</td>
              </tr>
            )
          })()}
        </tfoot>
      </table>
      <ul className="divide-y divide-[#edf2f1] rounded-card bg-white lg:hidden">
        <li aria-hidden className="flex items-center gap-3 px-4 py-1.5 text-xs font-semibold text-muted">
          <span className="flex-1">{t('stock.colItem')}</span><span className="w-24 text-right">{t('stock.counted')}</span><span className="hidden w-20 text-right lg:block">{t('stock.colDiff')}</span>
        </li>
        {/* Sizes of one item under one heading: "मैगी केचप" with its 1 kg, 200 g and 500 g rows. */}
        {packGroups(items).flatMap((g) => [
          ...(g.items.length > 1 ? [<li key={'pack-' + g.stem} className="bg-ground px-4 pb-1 pt-2.5 text-sm font-bold">{g.stem} <span className="font-normal text-muted">· {t('opening.sizes', { count: g.items.length })}</span></li>] : []),
          ...g.items.map((p) => (
          <li key={p.id} className={`flex items-center gap-3 py-2.5 pr-4 lg:py-1.5 ${g.items.length > 1 ? 'pl-7' : 'pl-4'}`}>
            <span className="flex min-w-0 flex-1 flex-col">
              <span className="truncate font-semibold">{g.items.length > 1 ? sizeOf(itemName(p), g.stem) : itemName(p)}</span>
              <PriceBoxes p={p} parts={parts[p.id] ?? {}} onChange={(next) => setCount(p.id, counts[p.id] ?? '', next)} />
              <span className="text-xs text-muted">{t('stock.books')}: {p.stock} {t(`units.${p.unit}`, { count: p.stock })}
                {(() => {
                  const l = lastIn.get(p.id)
                  if (!l) return null
                  const when = new Date(l.at).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })
                  return <> · {l.byHand ? <span className="text-warn">{t('stock.lastInHand', { date: when, qty: l.quantity })}</span> : t('stock.lastIn', { date: when, qty: l.quantity, supplier: l.supplier })}</>
                })()}
              </span>
            </span>
            <CountBox big p={p} value={counts[p.id] ?? ''} parts={parts[p.id] ?? {}} onChange={(total, next) => setCount(p.id, total, next)} />
            {(() => {
              // Desktop: the difference against the books, so a shortfall stands out while counting.
              const c = counts[p.id]?.trim()
              const diff = c ? Math.round((Number(c) - p.stock) * 1000) / 1000 : null
              return <span className={`hidden w-20 text-right font-bold lg:block ${diff == null || isNaN(diff) ? 'text-muted' : diff < 0 ? 'text-due' : diff > 0 ? 'text-brand-strong' : 'text-muted'}`}>{diff == null || isNaN(diff) ? '—' : diff > 0 ? `+${diff}` : diff}</span>
            })()}
          </li>
        ))])}
      </ul>
      </section>
      ))}
      <div className="fixed inset-x-0 bottom-[68px] z-10 mx-auto lg:bottom-0 lg:max-w-4xl max-w-xl border-t border-line bg-white px-4 py-3">
        {stockValue > 0 && <p className="mb-2 text-center text-sm font-semibold text-brand-deep lg:text-right">{t('opening.worth', { amount: formatRupees(Math.round(stockValue)) })}</p>}
        <Button className="h-14 w-full lg:ml-auto lg:block lg:h-12 lg:w-auto lg:px-10" disabled={busy || (counted.length === 0 && pricesTyped === 0)} onClick={save}>
          {t('stock.saveCount', { count: counted.length })}
        </Button>
      </div>
      <StocktakeHistory />
    </div>
  )
}

interface TakeSummary { id: string; number: number; takenAt: string; by: string; items: number; differences: number; valueDifference: number }
interface TakeLine { productId: string; name: string; books: number; counted: number; difference: number; costPrice: number | null }

/** Every saved count: when, who, how many items differed and what the difference was worth; tap one for its lines. */
function StocktakeHistory() {
  const { t } = useTranslation()
  const takes = useQuery({ queryKey: ['stocktakes'], queryFn: () => api<TakeSummary[]>('/v1/stocktakes') }).data
  const [open, setOpen] = useState<string | null>(null)
  const lines = useQuery({ queryKey: ['stocktake', open], queryFn: () => api<{ lines: TakeLine[] }>(`/v1/stocktakes/${open}`), enabled: !!open }).data
  return (
    <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
      <h2 className="font-bold">🗂️ {t('sl.takes')}</h2>
      {takes && takes.length === 0 && <p className="text-sm text-muted">{t('sl.noTakes')}</p>}
      {takes?.map((k) => (
        <div key={k.id} className="rounded-xl ring-1 ring-line">
          <button onClick={() => setOpen(open === k.id ? null : k.id)} className="flex w-full items-center justify-between gap-2 p-3 text-left">
            <span className="flex flex-col">
              <b>{t('sl.take', { number: k.number })}</b>
              <span className="text-xs text-muted">{t('sl.takeLine', { date: new Date(k.takenAt).toLocaleDateString(), by: k.by, items: k.items, diffs: k.differences })}</span>
            </span>
            <b className={k.valueDifference < 0 ? 'text-due' : k.valueDifference > 0 ? 'text-jama' : 'text-muted'}>{k.valueDifference > 0 ? '+' : ''}{formatRupees(k.valueDifference)}</b>
          </button>
          {open === k.id && lines && (
            <table className="w-full border-t border-line text-sm">
              <thead><tr className="text-right text-xs text-muted"><th className="px-3 py-1 text-left">{t('items2.colName')}</th><th className="px-2">{t('sl.books')}</th><th className="px-2">{t('sl.counted')}</th><th className="px-3">{t('sl.difference')}</th></tr></thead>
              <tbody>{lines.lines.filter((l) => l.difference !== 0).map((l) => (
                <tr key={l.productId} className="border-t border-[#edf2f1] text-right"><td className="px-3 py-1.5 text-left">{l.name}</td><td className="px-2">{l.books}</td><td className="px-2">{l.counted}</td><td className={`px-3 font-bold ${l.difference < 0 ? 'text-due' : 'text-jama'}`}>{l.difference > 0 ? '+' : ''}{l.difference}</td></tr>
              ))}</tbody>
            </table>
          )}
        </div>
      ))}
    </section>
  )
}


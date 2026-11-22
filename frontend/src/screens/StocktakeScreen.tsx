import { formatRupees } from '../lib/format'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { api } from '../lib/api'
import { useProducts } from '../lib/data'
import { uuidv7 } from '../lib/ids'
import { shownLocale } from '../i18n'
import { itemName } from '../lib/itemName'
import { compareCells, filterBox, nextSort, SortHead, type Sort } from '../components/TableHead'

interface LastIn { productId: string; at: string; quantity: number; supplier: string | null; byHand: boolean }

/**
 * Count the shelf, item by item. Only counted items are sent; each difference becomes a "count fix"
 * adjustment. The stocktake carries one ID, so pressing save twice corrects once.
 */
export function StocktakeScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { products } = useProducts(shopId)
  const [counts, setCounts] = useState<Record<string, string>>({})
  const [stocktakeId] = useState(uuidv7)
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

  async function save() {
    setBusy(true)
    try {
      const done = await api<{ adjusted: number; unchanged: number }>('/v1/stocktakes', 'POST', {
        id: stocktakeId,
        counts: counted.map(([productId, value]) => ({ productId, counted: Number(value) })),
      })
      setResult(t('stock.stocktakeDone', done))
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (err) {
      setResult(err instanceof Error ? err.message : t('error'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <p className="text-muted">{t('stock.stocktakeHint')}</p>
      {result && <p className="rounded-xl bg-emerald-50 p-3 font-semibold text-emerald-800">{result}</p>}
      {groups.map(([category, items]) => (
      <section key={category} className="flex flex-col gap-1">
      <h2 className="px-1 text-sm font-bold text-muted">{category || t('stock.noCategory')} · {items.length}</h2>
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
                  <input value={counts[p.id] ?? ''} onChange={(e) => setCounts({ ...counts, [p.id]: e.target.value })} inputMode="decimal"
                    aria-label={`${t('stock.counted')}: ${p.name}`} className="h-9 w-24 rounded-lg border border-line px-2 text-right" />
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
        {items.map((p) => (
          <li key={p.id} className="flex items-center gap-3 px-4 py-2.5 lg:py-1.5">
            <span className="flex min-w-0 flex-1 flex-col">
              <span className="truncate font-semibold">{itemName(p)}</span>
              <span className="text-xs text-muted">{t('stock.books')}: {p.stock} {t(`units.${p.unit}`, { count: p.stock })}
                {(() => {
                  const l = lastIn.get(p.id)
                  if (!l) return null
                  const when = new Date(l.at).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })
                  return <> · {l.byHand ? <span className="text-warn">{t('stock.lastInHand', { date: when, qty: l.quantity })}</span> : t('stock.lastIn', { date: when, qty: l.quantity, supplier: l.supplier })}</>
                })()}
              </span>
            </span>
            <input
              value={counts[p.id] ?? ''}
              onChange={(e) => setCounts({ ...counts, [p.id]: e.target.value })}
              inputMode="decimal"
              aria-label={`${t('stock.counted')}: ${p.name}`}
              placeholder={t('stock.counted')}
              className="h-11 w-24 rounded-xl border border-line px-2 text-right text-lg lg:h-9"
            />
            {(() => {
              // Desktop: the difference against the books, so a shortfall stands out while counting.
              const c = counts[p.id]?.trim()
              const diff = c ? Math.round((Number(c) - p.stock) * 1000) / 1000 : null
              return <span className={`hidden w-20 text-right font-bold lg:block ${diff == null || isNaN(diff) ? 'text-muted' : diff < 0 ? 'text-due' : diff > 0 ? 'text-brand-strong' : 'text-muted'}`}>{diff == null || isNaN(diff) ? '—' : diff > 0 ? `+${diff}` : diff}</span>
            })()}
          </li>
        ))}
      </ul>
      </section>
      ))}
      <div className="fixed inset-x-0 bottom-[68px] z-10 mx-auto lg:bottom-0 lg:max-w-4xl max-w-xl border-t border-line bg-white px-4 py-3">
        <Button className="h-14 w-full lg:ml-auto lg:block lg:h-12 lg:w-auto lg:px-10" disabled={busy || counted.length === 0} onClick={save}>
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


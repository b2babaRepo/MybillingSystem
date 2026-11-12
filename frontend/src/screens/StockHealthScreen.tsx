import { orderedNames, useCategories } from '../lib/categories'
import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'

type Movement = 'dead' | 'slow' | 'new' | 'fast' | 'none'
interface Item { productId: string; name: string; category: string | null; unit: string; stock: number; value: number | null; sales90: number; sold90: number; daysSinceSale: number | null; daysSinceIn: number | null; movement: Movement; rank: 'A' | 'B' | 'C'; daysOfCover: number | null }
interface Health { value: number; deadValue: number; deadItems: number; slowValue: number; slowItems: number; ageing: { label: string; value: number; items: number }[]; rankA: number; rankB: number; rankC: number; turnover: number; items: Item[] }

const TONE: Record<Movement, string> = { dead: 'bg-red-50 text-due', slow: 'bg-amber-50 text-amber-800', new: 'bg-sky-50 text-sky-800', fast: 'bg-emerald-50 text-emerald-800', none: 'bg-slate-100 text-muted' }

/** Where the shop's money sits: dead and slow stock, how old the stock is, and A/B/C items by sales. */
export function StockHealthScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const [category, setCategory] = useState('')
  const categoryRows = useCategories(shopId).data
  const health = useQuery({ queryKey: ['stock-health'], queryFn: () => api<Health>('/v1/reports/stock-health') })
  const [filter, setFilter] = useState<'all' | 'stuck' | 'A' | 'B' | 'C'>('stuck')
  const h = health.data
  const items = (h?.items ?? []).filter((i) => i.movement !== 'none' && (!category || i.category === category) && (filter === 'all' || (filter === 'stuck' ? i.movement === 'dead' || i.movement === 'slow' : i.rank === filter)))
  const max = Math.max(1, ...(h?.ageing ?? []).map((a) => a.value))

  return (
    <div className="flex flex-col gap-3 p-4">
      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm">
        <h1 className="text-xl font-bold">🩺 {t('sh.title')}</h1>
        <p className="text-sm text-muted">{t('sh.hint')}</p>
        {h && (
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            <Tile label={t('sh.value')} value={formatRupees(h.value)} />
            <Tile label={t('sh.dead', { count: h.deadItems })} value={formatRupees(h.deadValue)} tone="text-due" />
            <Tile label={t('sh.slow', { count: h.slowItems })} value={formatRupees(h.slowValue)} tone="text-amber-700" />
            <Tile label={t('sh.turnover')} value={t('sh.times', { n: h.turnover })} />
          </div>
        )}
      </section>
      {h && (
        <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm">
          <h2 className="font-bold">{t('sh.ageing')}</h2>
          {h.ageing.map((a) => (
            <div key={a.label} className="flex items-center gap-2 text-sm">
              <span className="w-20 shrink-0 text-muted">{t('sh.days', { range: a.label })}</span>
              <span className="h-3 flex-1 overflow-hidden rounded-full bg-[#edf2f1]"><span className="block h-full rounded-full bg-teal-600" style={{ width: `${(a.value / max) * 100}%` }} /></span>
              <b className="w-24 shrink-0 text-right">{formatRupees(a.value)}</b>
            </div>
          ))}
          <p className="text-xs text-muted">{t('sh.ranks', { a: h.rankA, b: h.rankB, c: h.rankC })}</p>
        </section>
      )}
      <div className="flex flex-wrap gap-1">
        <select aria-label={t('stock.category')} value={category} onChange={(e) => setCategory(e.target.value)} className="h-9 rounded-full border border-line bg-white px-3 text-sm">
          <option value="">{t('cat.allCategories')}</option>
          {orderedNames(categoryRows, (h?.items ?? []).map((i) => i.category)).map((c) => <option key={c} value={c}>{c}</option>)}
        </select>
        {(['stuck', 'all', 'A', 'B', 'C'] as const).map((f) => (
          <button key={f} onClick={() => setFilter(f)} aria-pressed={filter === f} className={`h-9 rounded-full px-3 text-sm font-semibold ${filter === f ? 'bg-teal-700 text-white' : 'bg-white text-muted ring-1 ring-black/5'}`}>{t(`sh.f.${f}`)}</button>
        ))}
      </div>
      {health.isError && <p className="text-sm text-due">{t('error')}</p>}
      {h && items.length === 0 && <p className="py-6 text-center text-muted">{t('sh.none')}</p>}
      {items.map((i) => (
        <button key={i.productId} onClick={() => navigate(`/p/${i.productId}`)} className="flex items-center justify-between gap-2 rounded-2xl bg-white p-3 text-left shadow-sm hover:bg-[#f6faf9]">
          <span className="flex min-w-0 flex-col">
            <b className="truncate">{i.name} <span className="ml-1 rounded bg-slate-100 px-1 text-[11px] text-muted">{i.rank}</span></b>
            <span className="text-xs text-muted">
              {i.stock} {i.unit} · {i.daysSinceSale == null ? t('sh.neverSold') : t('sh.lastSold', { n: i.daysSinceSale })}{i.daysOfCover != null && ` · ${t('sh.cover', { n: i.daysOfCover })}`}
            </span>
          </span>
          <span className="flex shrink-0 flex-col items-end gap-1">
            <b>{i.value == null ? '—' : formatRupees(i.value)}</b>
            <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${TONE[i.movement]}`}>{t(`sh.m.${i.movement}`)}</span>
          </span>
        </button>
      ))}
    </div>
  )
}

function Tile({ label, value, tone }: { label: string; value: string; tone?: string }) {
  return (
    <div className="flex flex-col rounded-2xl bg-[#f6faf9] p-3">
      <span className="text-xs text-muted">{label}</span>
      <b className={`text-lg ${tone ?? ''}`}>{value}</b>
    </div>
  )
}

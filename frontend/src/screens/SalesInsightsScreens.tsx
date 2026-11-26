import { useItemName } from '../lib/useItemName'
import { shownLocale } from '../i18n'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees, indiaToday, whatsAppLink } from '../lib/format'
import { downloadCsv } from '../lib/reports'
import type { Unit } from '../lib/types'
import { navigate } from '../router'
import { presets, weekSummary, label, change } from '../lib/salesInsights'

interface RangeItem { productId: string | null; name: string; unit: Unit; quantity: number; sales: number; bills: number; previousQuantity: number; previousSales: number }
export interface SalesRange {
  from: string; to: string; sales: number; bills: number; cash: number; digital: number; previousSales: number; previousBills: number
  days: { date: string; sales: number; received: number }[]
  items: RangeItem[]
  notSold: { productId: string; name: string; unit: Unit; stock: number; lastSold: string | null }[]
  namedCustomers: number; walkInBills: number
}
interface CustomerPattern {
  customerId: string; name: string; phone: string | null; bills: number; spend: number; firstVisit: string; lastVisit: string
  gapDays: number | null; daysSince: number; habit: 'regular' | 'due' | 'stopped' | 'new'; usualItems: string[]
}

/** Sales over the days chosen (a week by default): against the same days before, each day, best sellers, and what sat. */
export function SalesWeekScreen({ shopId, shopName, isOwner }: { shopId: string; shopName: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const nm = useItemName(shopId)
  const p = presets()
  const [range, setRange] = useState(() => {
    try { const pick = sessionStorage.getItem('gk-week'); sessionStorage.removeItem('gk-week'); if (pick === 'lastWeek') return p.lastWeek } catch { /* no storage */ }
    return p.thisWeek
  })
  const [by, setBy] = useState<'sales' | 'quantity'>('sales')
  const [error, setError] = useState('')
  const query = useQuery({ queryKey: [shopId, 'range', range.from, range.to], queryFn: () => api<SalesRange>(`/v1/reports/range?from=${range.from}&to=${range.to}`) })
  const r = query.data
  const peak = Math.max(1, ...(r?.days.map((d) => d.sales) ?? [1]))
  const items = r ? [...r.items].sort((a, b) => (by === 'sales' ? b.sales - a.sales : b.quantity - a.quantity)) : []
  const pct = r ? change(r.sales, r.previousSales) : null

  return (
    <div className="flex flex-col gap-3 p-4 pb-24">
      <div className="flex flex-wrap gap-2">
        {(Object.keys(p) as (keyof typeof p)[]).map((k) => (
          <button key={k} onClick={() => setRange(p[k])} aria-pressed={range.from === p[k].from && range.to === p[k].to}
            className={`min-h-11 rounded-full px-4 text-sm font-semibold ${range.from === p[k].from && range.to === p[k].to ? 'bg-brand text-white' : 'border border-line bg-white'}`}>
            {t(`insights.preset.${k}`)}
          </button>
        ))}
      </div>
      <div className="grid grid-cols-2 gap-2">
        <label className="flex flex-col gap-1 text-sm"><span className="text-muted">{t('insights.from')}</span>
          <input type="date" value={range.from} max={range.to} onChange={(e) => e.target.value && setRange({ ...range, from: e.target.value })} className="h-11 rounded-xl border border-line bg-white px-2 outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /></label>
        <label className="flex flex-col gap-1 text-sm"><span className="text-muted">{t('insights.to')}</span>
          <input type="date" value={range.to} max={indiaToday()} onChange={(e) => e.target.value && setRange({ ...range, to: e.target.value })} className="h-11 rounded-xl border border-line bg-white px-2 outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /></label>
      </div>
      {query.isError && <p className="text-sm text-due">{query.error.message}</p>}
      {!r && !query.isError && <p className="text-muted">…</p>}
      {r && <>
        <section className="flex flex-col gap-1 rounded-card bg-white p-4 shadow-sm print:shadow-none">
          <span className="text-sm text-muted">{label(r.from)} – {label(r.to)} · {t('insights.bills', { count: r.bills })}</span>
          <span className="text-3xl font-extrabold">{formatRupees(r.sales)}</span>
          {pct !== null && <span className={`text-sm font-semibold ${pct >= 0 ? 'text-brand-strong' : 'text-due'}`}>{pct >= 0 ? '▲' : '▼'} {Math.abs(pct)}% {t('insights.vsBeforeLong', { amount: formatRupees(r.previousSales) })}</span>}
          <span className="text-sm text-muted">{t('insights.split', { cash: formatRupees(r.cash), digital: formatRupees(r.digital), named: r.namedCustomers, walkIn: r.walkInBills })}</span>
          <div role="img" aria-label={t('reports.daily')} className="mt-2 flex h-24 items-end gap-1">
            {r.days.map((d) => (
              <span key={d.date} title={`${label(d.date)}: ${formatRupees(d.sales)}`} className={`flex-1 rounded-t-sm ${d.sales > 0 ? 'bg-brand' : 'bg-[#edf2f1]'}`} style={{ height: `${Math.max(3, (d.sales / peak) * 100)}%` }} />
            ))}
          </div>
          {r.days.length <= 14 && (
            <div className="flex gap-1 text-[10px] text-muted">{r.days.map((d) => <span key={d.date} className="flex-1 text-center">{new Date(`${d.date}T00:00:00Z`).toLocaleDateString(shownLocale(), { weekday: 'narrow', timeZone: 'UTC' })}</span>)}</div>
          )}
        </section>

        <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm print:shadow-none">
          <div className="flex items-center justify-between gap-2">
            <h2 className="font-bold">{t('insights.best')}</h2>
            <div className="flex rounded-full bg-ground p-0.5 text-xs font-semibold">
              {(['sales', 'quantity'] as const).map((k) => (
                <button key={k} onClick={() => setBy(k)} aria-pressed={by === k} className={`min-h-9 rounded-full px-3 ${by === k ? 'bg-white shadow' : 'text-muted'}`}>{t(`insights.by.${k}`)}</button>
              ))}
            </div>
          </div>
          {items.length === 0 && <p className="text-sm text-muted">{t('insights.noSales')}</p>}
          <ol className="divide-y divide-[#edf2f1] text-sm">
            {items.slice(0, 20).map((i, n) => {
              const was = by === 'sales' ? i.previousSales : i.previousQuantity
              const is = by === 'sales' ? i.sales : i.quantity
              const up = change(is, was)
              return (
                <li key={`${i.productId ?? i.name}-${i.unit}`} className="flex items-center justify-between gap-2 py-2">
                  <span className="min-w-0 truncate"><span className="text-muted">{n + 1}.</span> {nm(i)}</span>
                  <span className="shrink-0 text-right">
                    <b>{i.quantity} {t(`units.${i.unit}`, { count: i.quantity })} · {formatRupees(i.sales)}</b>
                    <span className={`block text-xs ${up === null ? 'text-muted' : up >= 0 ? 'text-brand-strong' : 'text-due'}`}>{up === null ? t('insights.newSeller') : `${up >= 0 ? '▲' : '▼'} ${Math.abs(up)}%`}</span>
                  </span>
                </li>
              )
            })}
          </ol>
        </section>

        {r.notSold.length > 0 && (
          <section className="flex flex-col gap-1 rounded-card bg-white p-4 text-sm shadow-sm print:shadow-none">
            <h2 className="font-bold">{t('insights.notSold', { count: r.notSold.length })}</h2>
            <p className="text-muted">{t('insights.notSoldHint')}</p>
            <ul className="divide-y divide-[#edf2f1]">
              {r.notSold.slice(0, 15).map((u) => (
                <li key={u.productId}>
                  <button onClick={() => navigate(`/p/${u.productId}`)} className="flex min-h-11 w-full items-center justify-between gap-2 text-left">
                    <span className="truncate">{nm(u)}</span>
                    <span className="shrink-0 text-muted">{u.stock} {t(`units.${u.unit}`, { count: u.stock })} · {u.lastSold ? t('insights.lastSold', { date: new Date(u.lastSold).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' }) }) : t('insights.neverSold')}</span>
                  </button>
                </li>
              ))}
            </ul>
          </section>
        )}

        {isOwner && (
          <div className="grid grid-cols-2 gap-2">
            <a href={whatsAppLink(null, weekSummary(r, shopName, t))} target="_blank" rel="noreferrer" className="flex min-h-12 items-center justify-center rounded-xl bg-[#15803d] font-bold text-white">{t('insights.share')}</a>
            <button onClick={() => void downloadCsv(`/v1/reports/items.csv?from=${r.from}&to=${r.to}`, `items-${r.from}-to-${r.to}.csv`).catch((e: Error) => setError(e.message))} className="min-h-12 rounded-xl border border-line bg-white font-bold transition hover:bg-[#f6faf9]">{t('insights.csv')}</button>
          </div>
        )}
        {error && <p className="text-sm text-due">{error}</p>}
      </>}
    </div>
  )
}

/**
 * How named customers buy: spend, how often they come, their usual items, and who is past their usual gap or has
 * stopped coming, with a WhatsApp message to bring them back. Walk-in bills have no name, so they aren't here.
 */
export function CustomerPatternsScreen({ shopId, shopName }: { shopId: string; shopName: string }) {
  const { t } = useTranslation()
  const [days, setDays] = useState(90)
  const [filter, setFilter] = useState<'all' | CustomerPattern['habit']>('all')
  const query = useQuery({ queryKey: [shopId, 'customer-patterns', days], queryFn: () => api<{ days: number; customers: CustomerPattern[] }>(`/v1/reports/customers?days=${days}`) })
  const all = query.data?.customers ?? []
  const shown = filter === 'all' ? all : all.filter((c) => c.habit === filter)
  const count = (h: CustomerPattern['habit']) => all.filter((c) => c.habit === h).length

  return (
    <div className="flex flex-col gap-3 p-4 pb-24">
      <p className="text-sm text-muted">{t('insights.customersHint')}</p>
      <div className="flex flex-wrap items-center gap-2">
        <select aria-label={t('insights.lookBack')} value={days} onChange={(e) => setDays(Number(e.target.value))} className="h-11 rounded-xl border border-line bg-white px-2 text-sm font-semibold transition hover:bg-[#f6faf9]">
          {[30, 90, 180, 365].map((d) => <option key={d} value={d}>{t('insights.lastDays', { days: d })}</option>)}
        </select>
        {(['all', 'regular', 'due', 'stopped', 'new'] as const).map((h) => (
          <button key={h} onClick={() => setFilter(h)} aria-pressed={filter === h}
            className={`min-h-11 rounded-full px-3 text-sm font-semibold ${filter === h ? 'bg-brand text-white' : 'border border-line bg-white'}`}>
            {t(`insights.habit.${h}`)}{h === 'all' ? ` · ${all.length}` : ` · ${count(h)}`}
          </button>
        ))}
      </div>
      {query.isError && <p className="text-sm text-due">{query.error.message}</p>}
      {query.data && shown.length === 0 && <p className="rounded-card bg-white p-4 text-sm text-muted shadow-sm print:shadow-none">{t('insights.noCustomers')}</p>}
      {shown.map((c, n) => (
        <section key={c.customerId} className="flex flex-col gap-1 rounded-card bg-white p-4 text-sm shadow-sm print:shadow-none">
          <p className="flex items-center justify-between gap-2">
            <button onClick={() => navigate(`/c/${c.customerId}`)} className="min-h-9 truncate text-left text-base font-bold">{filter === 'all' ? `${n + 1}. ` : ''}{c.name}</button>
            <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-semibold ${c.habit === 'regular' ? 'bg-brand-soft text-brand-strong' : c.habit === 'due' ? 'bg-warn-soft text-warn-ink' : c.habit === 'stopped' ? 'bg-due-soft text-due' : 'bg-ground text-muted'}`}>{t(`insights.habit.${c.habit}`)}</span>
          </p>
          <p>{t('insights.spend', { spend: formatRupees(c.spend), count: c.bills })}{c.gapDays ? ` · ${t('insights.every', { days: c.gapDays })}` : ''}</p>
          <p className="text-muted">{c.daysSince === 0 ? t('insights.today') : t('insights.since', { days: c.daysSince })}{c.usualItems.length > 0 ? ` · ${t('insights.usual', { items: c.usualItems.join(', ') })}` : ''}</p>
          {(c.habit === 'due' || c.habit === 'stopped') && c.phone && (
            <a href={whatsAppLink(c.phone, t('insights.comeBack', { name: c.name, shop: shopName, items: c.usualItems.join(', ') || t('insights.yourItems') }))} target="_blank" rel="noreferrer"
              className="mt-1 flex min-h-11 items-center justify-center self-start rounded-xl bg-[#15803d] px-4 font-semibold text-white">{t('insights.message')}</a>
          )}
        </section>
      ))}
    </div>
  )
}

/**
 * On Monday and Tuesday, the owner's home screen offers last week in one message: open the week report, or send it
 * on WhatsApp (to themselves or a partner). Nothing is sent by itself.
 */
export function WeekSummaryCard({ shopId, shopName }: { shopId: string; shopName: string }) {
  const { t } = useTranslation()
  const weekday = (new Date(`${indiaToday()}T00:00:00Z`).getUTCDay() + 6) % 7
  const last = presets().lastWeek
  const show = weekday <= 1
  const r = useQuery({ queryKey: [shopId, 'range', last.from, last.to], queryFn: () => api<SalesRange>(`/v1/reports/range?from=${last.from}&to=${last.to}`), enabled: show, retry: false }).data
  if (!show || !r || r.bills === 0) return null
  const pct = change(r.sales, r.previousSales)
  return (
    <section className="mx-4 mt-4 flex flex-col gap-2 rounded-card bg-white p-4 text-sm shadow-sm print:shadow-none">
      <p className="font-bold">{t('insights.cardTitle')}</p>
      <p>{t('insights.waSales', { sales: formatRupees(r.sales), bills: r.bills })}{pct === null ? '' : ` · ${pct >= 0 ? '▲' : '▼'} ${Math.abs(pct)}%`}</p>
      {r.items[0] && <p className="text-muted">{t('insights.cardTop', { name: r.items[0].name })}</p>}
      <div className="grid grid-cols-2 gap-2">
        <button onClick={() => { try { sessionStorage.setItem('gk-week', 'lastWeek') } catch { /* opens this week */ } navigate('/reports/week') }} className="min-h-11 rounded-xl border border-line font-semibold transition hover:bg-[#f6faf9]">{t('insights.open')}</button>
        <a href={whatsAppLink(null, weekSummary(r, shopName, t))} target="_blank" rel="noreferrer" className="flex min-h-11 items-center justify-center rounded-xl bg-[#15803d] font-semibold text-white">{t('insights.share')}</a>
      </div>
    </section>
  )
}

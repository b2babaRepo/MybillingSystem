import { useItemName } from '../lib/useItemName'
import { getSession } from '../lib/session'
import { shownLocale } from '../i18n'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees, indiaToday, whatsAppLink } from '../lib/format'
import type { Unit } from '../lib/types'
import { navigate } from '../router'

interface DayBill {
  id: string; billNo: string | null; at: string; customerId: string | null; customer: string | null; items: number; itemsText: string
  total: number; paid: number; mode: 'cash' | 'upi' | 'bank' | 'card' | null; onKhata: number; by: string | null; cancelledAt: string | null; cancelReason: string | null
  parts?: { mode: 'cash' | 'upi' | 'bank' | 'card'; amount: number }[] | null
}
interface DayTotals { sales: number; bills: number; cash: number; upi: number; bank: number; card?: number; onKhata: number; khataCollected: number; spent: number }
interface DayReport {
  date: string; totals: DayTotals; yesterday: DayTotals; bills: DayBill[]; cancelled: DayBill[]
  items: { productId: string | null; name: string; unit: Unit; quantity: number; sales: number }[]
  galla: { opening: number; expected: number; counted: number | null; difference: number | null } | null
  tills?: { counter: string | null; by: string | null; bills: number; sales: number; cash: number; upi: number; card: number; bank: number; onKhata: number; cancelled: number; cancelledValue: number }[]
}

const shift = (date: string, days: number) => new Date(Date.parse(`${date}T00:00:00Z`) + days * 86_400_000).toISOString().slice(0, 10)
const time = (iso: string) => new Date(iso).toLocaleTimeString(shownLocale(), { hour: 'numeric', minute: '2-digit' })

/**
 * The day book: every bill of a day with its items, how it was paid and what went on khata; cancelled bills apart (never
 * hidden); money by mode against the day before; items sold; and the drawer's close. Any past day, not only today.
 */
export function DayReportScreen({ shopName }: { shopName: string }) {
  const { t } = useTranslation()
  const nm = useItemName(getSession()?.shopId)
  const today = indiaToday()
  const [date, setDate] = useState(today)
  const [showItems, setShowItems] = useState(false)
  const query = useQuery({ queryKey: ['day-report', date], queryFn: () => api<DayReport>(`/v1/reports/day?date=${date}`) })
  const r = query.data
  const modeLabel = (b: DayBill) => ((b.parts?.length ?? 0) > 1 ? b.parts!.map((p) => `${t(p.mode)} ${formatRupees(p.amount)}`).join(' + ') : b.paid > 0 && b.mode ? t(b.mode) : '') + (b.onKhata > 0 ? `${b.paid > 0 ? ' + ' : ''}${t('day.khata')}` : '')
  const change = r && r.yesterday.sales > 0 ? Math.round(((r.totals.sales - r.yesterday.sales) / r.yesterday.sales) * 100) : null
  const share = r ? [
    t('day.waTitle', { shop: shopName, date: new Date(`${date}T00:00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' }) }),
    t('day.waTotals', { sales: formatRupees(r.totals.sales), bills: r.totals.bills }),
    t('day.waModes', { cash: formatRupees(r.totals.cash), upi: formatRupees(r.totals.upi), khata: formatRupees(r.totals.onKhata) }),
    ...r.bills.map((b) => `${time(b.at)} · ${b.billNo ?? ''} · ${b.customer ?? t('hold.walkIn')} · ${formatRupees(b.total)} · ${modeLabel(b)}`),
    r.cancelled.length > 0 ? t('day.waCancelled', { count: r.cancelled.length }) : '',
  ].filter(Boolean).join('\n') : ''

  return (
    <div className="flex flex-col gap-3 p-4 pb-24">
      <div className="flex items-center justify-between rounded-card bg-white p-2 shadow-sm">
        <button onClick={() => setDate(shift(date, -1))} aria-label={t('book.previous')} className="h-11 w-11 rounded-xl text-xl">‹</button>
        <input type="date" value={date} max={today} onChange={(e) => e.target.value && setDate(e.target.value)} aria-label={t('day.date')} className="h-11 rounded-xl border border-line px-2 font-bold" />
        <button onClick={() => setDate(shift(date, 1))} disabled={date >= today} aria-label={t('book.next')} className="h-11 w-11 rounded-xl text-xl disabled:opacity-30">›</button>
      </div>
      {query.isError && <p className="text-sm text-due">{query.error.message}</p>}
      {!r && !query.isError && <p className="text-muted">…</p>}
      {r && <>
        <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
          <div className="flex items-baseline justify-between">
            <span className="text-sm font-semibold text-muted">{t('day.sales', { count: r.totals.bills })}</span>
            {change !== null && <span className={`text-sm font-semibold ${change >= 0 ? 'text-brand-strong' : 'text-due'}`}>{change >= 0 ? '▲' : '▼'} {Math.abs(change)}% {t('day.vsYesterday')}</span>}
          </div>
          <span className="text-3xl font-extrabold">{formatRupees(r.totals.sales)}</span>
          <div className="grid grid-cols-2 gap-2 text-sm sm:grid-cols-4">
            <span className="rounded-xl bg-emerald-50 p-2 text-emerald-800">{t('cash')}<b className="block">{formatRupees(r.totals.cash)}</b></span>
            <span className="rounded-xl bg-blue-50 p-2 text-blue-800">{t('upi')}<b className="block">{formatRupees(r.totals.upi)}</b></span>
            {(r.totals.card ?? 0) > 0 && <span className="rounded-xl bg-indigo-50 p-2 text-indigo-800">{t('card')}<b className="block">{formatRupees(r.totals.card!)}</b></span>}
            <span className="rounded-xl bg-orange-50 p-2 text-orange-800">{t('day.khata')}<b className="block">{formatRupees(r.totals.onKhata)}</b></span>
            <span className="rounded-xl bg-[#f1f5f4] p-2 text-muted">{t('day.collected')}<b className="block text-ink">{formatRupees(r.totals.khataCollected)}</b></span>
          </div>
          {r.totals.bank > 0 && <p className="text-xs text-muted">{t('bank')} {formatRupees(r.totals.bank)}</p>}
        </section>

        <section className="rounded-card bg-white p-4 shadow-sm">
          <h2 className="mb-1 font-bold">{t('day.bills', { count: r.bills.length })}</h2>
          {r.bills.length === 0 && <p className="text-sm text-muted">{t('day.noBills')}</p>}
          <ul className="divide-y divide-[#edf2f1] text-sm">
            {r.bills.map((b) => (
              <li key={b.id}>
                <button onClick={() => navigate(`/print/bill/${b.id}`)} className="flex w-full flex-col gap-0.5 py-2 text-left">
                  <span className="flex justify-between gap-2">
                    <b className="truncate">{time(b.at)} · {b.billNo ?? ''} · {b.customer ?? t('hold.walkIn')}</b>
                    <b className="shrink-0">{formatRupees(b.total)}</b>
                  </span>
                  <span className="flex justify-between gap-2 text-xs text-muted">
                    <span className="truncate">{b.itemsText}</span>
                    <span className={`shrink-0 font-semibold ${b.onKhata > 0 ? 'text-orange-800' : ''}`}>{modeLabel(b)}{b.onKhata > 0 ? ` ${formatRupees(b.onKhata)}` : ''}</span>
                  </span>
                  {b.by && <span className="text-xs text-muted">{t('day.by', { name: b.by })}</span>}
                </button>
              </li>
            ))}
          </ul>
        </section>

        {(r.tills?.length ?? 0) > 1 && (
          <section className="overflow-x-auto rounded-card bg-white p-4 text-sm shadow-sm">
            <h2 className="mb-1 font-bold">{t('day.tills')}</h2>
            <table className="w-full">
              <thead><tr className="text-left text-xs text-muted"><th className="py-1">{t('day.till')}</th><th className="text-right">{t('day.tillBills')}</th><th className="text-right">{t('day.tillSales')}</th><th className="text-right">{t('day.tillCash')}</th><th className="text-right">UPI</th><th className="text-right">{t('day.tillKhata')}</th></tr></thead>
              <tbody>
                {r.tills!.map((x, i) => (
                  <tr key={i} className="border-t border-line">
                    <td className="py-1.5">{x.counter ?? t('day.noCounter')}<span className="block text-xs text-muted">{x.by ?? ''}{x.cancelled > 0 ? ` · ${t('day.tillCancelled', { count: x.cancelled, amount: formatRupees(x.cancelledValue) })}` : ''}</span></td>
                    <td className="text-right tabular-nums">{x.bills}</td><td className="text-right tabular-nums">{formatRupees(x.sales)}</td>
                    <td className="text-right tabular-nums">{formatRupees(x.cash)}</td><td className="text-right tabular-nums">{formatRupees(x.upi)}</td><td className="text-right tabular-nums">{formatRupees(x.onKhata)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </section>
        )}

        {r.cancelled.length > 0 && (
          <section className="rounded-card bg-white p-4 text-sm shadow-sm">
            <h2 className="mb-1 font-bold text-due">{t('day.cancelled', { count: r.cancelled.length })}</h2>
            {r.cancelled.map((b) => (
              <button key={b.id} onClick={() => navigate(`/print/bill/${b.id}`)} className="flex w-full justify-between gap-2 py-1.5 text-left">
                <span className="line-through">{time(b.at)} · {b.billNo ?? ''} · {formatRupees(b.total)}</span>
                <span className="text-xs text-muted">{b.cancelReason}</span>
              </button>
            ))}
          </section>
        )}

        <section className="rounded-card bg-white p-4 text-sm shadow-sm">
          <button onClick={() => setShowItems(!showItems)} aria-expanded={showItems} className="flex min-h-11 w-full items-center justify-between font-bold">
            <span>{t('day.items', { count: r.items.length })}</span><span>{showItems ? '▴' : '▾'}</span>
          </button>
          {showItems && r.items.map((i) => (
            <p key={`${i.productId ?? i.name}-${i.unit}`} className="flex justify-between gap-2 py-1"><span className="truncate">{nm(i)}</span><span className="shrink-0">{i.quantity} {t(`units.${i.unit}`, { count: i.quantity })} · {formatRupees(i.sales)}</span></p>
          ))}
        </section>

        <section className="grid grid-cols-2 gap-2 rounded-card bg-white p-4 text-sm shadow-sm">
          <span className="text-muted">{t('day.spent')}<b className="block text-ink">{formatRupees(r.totals.spent)}</b></span>
          {r.galla && (
            <span className="text-muted">{t('day.drawer')}<b className="block text-ink">{formatRupees(r.galla.expected)}</b>
              {r.galla.counted != null && <span className={`block text-xs ${r.galla.difference === 0 ? 'text-brand-strong' : 'text-due'}`}>{t('day.counted', { amount: formatRupees(r.galla.counted), diff: formatRupees(r.galla.difference ?? 0) })}</span>}
            </span>
          )}
        </section>

        <a href={whatsAppLink(null, share)} target="_blank" rel="noreferrer" className="flex min-h-12 items-center justify-center rounded-xl bg-[#15803d] font-bold text-white">{t('day.share')}</a>
      </>}
    </div>
  )
}

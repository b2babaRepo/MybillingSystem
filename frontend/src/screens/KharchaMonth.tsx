import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees, indiaToday, whatsAppLink } from '../lib/format'
import { navigate } from '../router'
import { OUTSIDE_PROFIT, type ExpenseCategory } from './ExpensesScreen'
import { Page } from '../components/PrintPage'
import { addText, shownLocale } from '../i18n'
import { kharchaText } from '../i18n/kharcha'

addText(kharchaText)

export interface ExpenseMonth {
  month: string
  complete: boolean
  total: number
  cash: number
  upi: number
  takenHome: number
  shopKharcha: number
  count: number
  byCategory: { category: ExpenseCategory; total: number; count: number }[]
  byDay: { date: string; total: number; count: number }[]
  lines: { id: string; occurredAt: string; amount: number; category: ExpenseCategory; note: string | null; mode: 'cash' | 'upi' | 'bank'; by: string | null }[]
  previousMonthShopKharcha: number | null
  /** Stock paid for and the out-of-profit headings (not money taken home). */
  outsideProfit: number
  /** Credit-note refunds: counted as returns, not kharcha. */
  refunds: number
}

/** "2026-10" ± months. */
export function shiftMonth(month: string, by: number) {
  const [y, m] = month.split('-').map(Number)
  const d = new Date(Date.UTC(y, m - 1 + by, 1))
  return `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}`
}

/** The India-time month now, "2026-10". */
export const thisMonth = () => indiaToday().slice(0, 7)

const monthLabel = (month: string) => new Date(`${month}-01T00:00:00`).toLocaleDateString(shownLocale(), { month: 'long', year: 'numeric' })

export function useExpenseMonth(shopId: string, month: string) {
  return useQuery({ queryKey: [shopId, 'expenses', 'month', month], queryFn: () => api<ExpenseMonth>(`/v1/expenses/month?month=${month}`) })
}

/** Paid by bank or cheque this month, added to the cash and UPI line when there is any. */
function withBank(m: ExpenseMonth, text: string, t: (k: string) => string) {
  const bank = m.lines.filter((l) => l.mode === 'bank').reduce((sum, l) => sum + l.amount, 0)
  return bank > 0 ? `${text} · ${t('bank')} ${formatRupees(bank)}` : text
}

/** The month's kharcha as a WhatsApp message: totals and the headings. */
function shareText(m: ExpenseMonth, shopName: string, t: (k: string, o?: Record<string, unknown>) => string) {
  return [
    t('kharchaMonth.waTitle', { shop: shopName, month: monthLabel(m.month) }),
    t('kharchaMonth.waTotal', { amount: formatRupees(m.shopKharcha) }),
    ...m.byCategory.filter((c) => !OUTSIDE_PROFIT.includes(c.category)).map((c) => `• ${t(`expense.cat.${c.category}`)}: ${formatRupees(c.total)}`),
    m.outsideProfit > 0 ? t('kharchaMonth.outside', { amount: formatRupees(m.outsideProfit) }) : '',
    m.takenHome > 0 ? t('kharchaMonth.waHome', { amount: formatRupees(m.takenHome) }) : '',
    withBank(m, t('kharchaMonth.waModes', { cash: formatRupees(m.cash), upi: formatRupees(m.upi) }), t),
  ].filter(Boolean).join('\n')
}

/**
 * The month's kharcha, rolled up from the daily pages: totals (shop kharcha apart from money taken home), each
 * heading with a bar, each day, and last month to compare. Print or save as PDF, or share on WhatsApp.
 */
export function KharchaMonth({ shopId, shopName }: { shopId: string; shopName: string }) {
  const { t } = useTranslation()
  const [month, setMonth] = useState(thisMonth())
  const query = useExpenseMonth(shopId, month)
  const m = query.data
  const top = Math.max(1, ...(m?.byCategory.map((c) => c.total) ?? [1]))
  const change = m && m.previousMonthShopKharcha ? m.shopKharcha - m.previousMonthShopKharcha : null

  return (
    <>
      <div className="flex items-center justify-between rounded-card bg-white p-2 shadow-sm">
        <button onClick={() => setMonth(shiftMonth(month, -1))} aria-label={t('book.previous')} className="h-11 w-11 rounded-xl text-xl">‹</button>
        <span className="text-lg font-bold">{monthLabel(month)}</span>
        <button onClick={() => setMonth(shiftMonth(month, 1))} disabled={month >= thisMonth()} aria-label={t('book.next')} className="h-11 w-11 rounded-xl text-xl disabled:opacity-30">›</button>
      </div>
      {!m && <p className="p-4 text-center text-muted">…</p>}
      {m && <>
        <section className="flex flex-col gap-1 rounded-[20px] bg-white p-4">
          <span className="text-sm font-semibold text-muted">{m.complete ? t('kharchaMonth.closed') : t('kharchaMonth.soFar')}</span>
          <span className="text-3xl font-extrabold">{formatRupees(m.shopKharcha)}</span>
          <span className="text-sm">{withBank(m, t('kharchaMonth.modes', { cash: formatRupees(m.cash), upi: formatRupees(m.upi), count: m.count }), t)}</span>
          {m.outsideProfit > 0 && <span className="text-sm text-muted">{t('kharchaMonth.outside', { amount: formatRupees(m.outsideProfit) })}</span>}
          {m.takenHome > 0 && <span className="text-sm text-muted">{t('kharchaMonth.home', { amount: formatRupees(m.takenHome) })}</span>}
          {m.refunds > 0 && <span className="text-sm text-muted">{t('kharchaMonth.refunds', { amount: formatRupees(m.refunds) })}</span>}
          {change !== null && (
            <span className={`text-sm font-semibold ${change > 0 ? 'text-due' : 'text-brand-strong'}`}>
              {change > 0 ? t('kharchaMonth.more', { amount: formatRupees(change) }) : t('kharchaMonth.less', { amount: formatRupees(-change) })}
            </span>
          )}
        </section>

        {m.count > 0 && <>
          <div className="grid grid-cols-2 gap-2">
            <button onClick={() => navigate(`/print/kharcha/${m.month}`)} className="min-h-12 rounded-xl border border-line bg-white font-bold transition hover:bg-[#f6faf9]">{t('kharchaMonth.pdf')}</button>
            <a href={whatsAppLink(null, shareText(m, shopName, t))} target="_blank" rel="noreferrer" className="flex min-h-12 items-center justify-center rounded-xl bg-[#15803d] font-bold text-white">{t('kharchaMonth.share')}</a>
          </div>

          <section className="flex flex-col gap-2 rounded-[20px] bg-white p-4">
            <h2 className="font-bold">{t('kharchaMonth.byHeading')}</h2>
            {m.byCategory.map((c) => (
              <div key={c.category} className="flex flex-col gap-1">
                <span className="flex justify-between text-sm"><span>{t(`expense.cat.${c.category}`)} <span className="text-muted">×{c.count}</span>{OUTSIDE_PROFIT.includes(c.category) && <span className="ml-1 rounded-full bg-ground px-2 text-xs text-muted">{t('kharchaMonth.outsideTag')}</span>}</span><b>{formatRupees(c.total)}</b></span>
                <span className="h-2 rounded-full bg-ground"><span className={`block h-2 rounded-full ${OUTSIDE_PROFIT.includes(c.category) ? 'bg-amber-400' : 'bg-teal-600'}`} style={{ width: `${(c.total / top) * 100}%` }} /></span>
              </div>
            ))}
          </section>

          <section className="flex flex-col gap-1 rounded-[20px] bg-white p-4">
            <h2 className="font-bold">{t('kharchaMonth.byDay')}</h2>
            <ul className="divide-y divide-[#edf2f1] text-sm">
              {m.byDay.map((d) => (
                <li key={d.date} className="flex justify-between py-2">
                  <span>{new Date(`${d.date}T00:00:00`).toLocaleDateString(shownLocale(), { weekday: 'short', day: 'numeric', month: 'short' })} <span className="text-muted">×{d.count}</span></span>
                  <b>{formatRupees(d.total)}</b>
                </li>
              ))}
            </ul>
          </section>
        </>}
        {m.count === 0 && <p className="rounded-[20px] bg-white p-6 text-center text-muted">{t('kharchaMonth.empty')}</p>}
      </>}
    </>
  )
}

/** The month's kharcha on paper (/print/kharcha/2026-10): every line, then running costs and what stays out of profit. A4 or a receipt roll. */
export function KharchaPrint({ shopId, shopName, month }: { shopId: string; shopName: string; month: string }) {
  const { t } = useTranslation()
  const m = useExpenseMonth(shopId, month).data
  if (!m) return <p className="p-6">…</p>
  const running = m.byCategory.filter((c) => !OUTSIDE_PROFIT.includes(c.category))
  const outside = m.byCategory.filter((c) => OUTSIDE_PROFIT.includes(c.category) && c.category !== 'home')
  return (
    <Page title={shopName} subtitle={`${t('kharchaMonth.printTitle', { month: monthLabel(m.month) })}${m.complete ? '' : ` · ${t('kharchaMonth.soFar')}`}`} shareText={shareText(m, shopName, t)} phone={null}>
      <table className="kharcha-lines w-full border-collapse text-[14px]">
        <thead><tr className="border-b border-black text-left"><th className="py-1">{t('kharchaMonth.date')}</th><th>{t('kharchaMonth.heading')}</th><th>{t('kharchaMonth.mode')}</th><th className="text-right">{t('kharchaMonth.amount')}</th></tr></thead>
        <tbody>
          {m.lines.map((l) => (
            <tr key={l.id} className="border-b border-gray-300">
              <td className="py-1 pr-2 whitespace-nowrap">{new Date(l.occurredAt).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })}</td>
              <td className="pr-2">{t(`expense.cat.${l.category}`)}{l.note && <span className="block text-xs text-gray-600">{l.note}</span>}</td>
              <td className="pr-2">{t(l.mode)}</td>
              <td className="text-right whitespace-nowrap">{formatRupees(l.amount)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <table className="mt-4 w-full border-collapse text-[14px]">
        <tbody>
          {running.map((c) => (
            <tr key={c.category}><td className="py-0.5">{t(`expense.cat.${c.category}`)}</td><td className="text-right">{formatRupees(c.total)}</td></tr>
          ))}
          <tr className="border-t-2 border-black text-lg font-extrabold"><td className="pt-1">{t('kharchaMonth.shopTotal')}</td><td className="text-right">{formatRupees(m.shopKharcha)}</td></tr>
          {m.refunds > 0 && <tr><td colSpan={2} className="text-gray-600">{t('kharchaMonth.refunds', { amount: formatRupees(m.refunds) })}</td></tr>}
          {outside.length > 0 && <tr><td colSpan={2} className="pt-3 font-bold">{t('kharchaMonth.outsideShort')}</td></tr>}
          {outside.map((c) => (
            <tr key={c.category}><td className="py-0.5">{t(`expense.cat.${c.category}`)}</td><td className="text-right">{formatRupees(c.total)}</td></tr>
          ))}
          {m.takenHome > 0 && <tr><td>{t('kharchaMonth.homeShort')}</td><td className="text-right">{formatRupees(m.takenHome)}</td></tr>}
          <tr><td colSpan={2} className="pt-2 text-gray-600">{withBank(m, t('kharchaMonth.modesShort', { cash: formatRupees(m.cash), upi: formatRupees(m.upi) }), t)}</td></tr>
        </tbody>
      </table>
    </Page>
  )
}

const REGULAR: ExpenseCategory[] = ['rent', 'salary', 'electricity']

/**
 * Rent, salary and bijli come every month. Any written last month but not yet this month is shown, with last month's
 * amount ready to write again (it goes to the check step first, never straight to the book). Owners only, from the
 * 5th, since these are often paid in the first days.
 */
export function MissingRegular({ shopId, skip, onRepeat }: { shopId: string; skip: ExpenseCategory[]; onRepeat: (line: { amount: number; category: ExpenseCategory; note: string | null; mode: 'cash' | 'upi' }) => void }) {
  const { t } = useTranslation()
  const now = thisMonth()
  const last = useExpenseMonth(shopId, shiftMonth(now, -1)).data
  const current = useExpenseMonth(shopId, now).data
  const dayOfMonth = Number(indiaToday().slice(8, 10))
  if (!last || !current || dayOfMonth < 5) return null
  const written = new Set(current.lines.map((l) => l.category))
  const missing = last.lines.filter((l) => REGULAR.includes(l.category) && !written.has(l.category) && !skip.includes(l.category))
  if (missing.length === 0) return null
  return (
    <section className="flex flex-col gap-2 rounded-[20px] bg-warn-soft p-4 text-sm">
      <p className="font-bold">{t('kharchaMonth.missingTitle', { month: monthLabel(now) })}</p>
      <p className="text-muted">{t('kharchaMonth.missingWhy')}</p>
      {missing.map((l) => (
        <div key={l.id} className="flex items-center justify-between gap-2">
          <span>{t('kharchaMonth.missingLine', { heading: l.note || t(`expense.cat.${l.category}`), amount: formatRupees(l.amount) })}</span>
          <button onClick={() => onRepeat({ amount: l.amount, category: l.category, note: l.note, mode: l.mode === 'bank' ? 'upi' : l.mode })} className="min-h-11 shrink-0 rounded-xl border border-amber-700 bg-white px-3 font-semibold text-warn-ink">
            {t('kharchaMonth.repeat')}
          </button>
        </div>
      ))}
    </section>
  )
}

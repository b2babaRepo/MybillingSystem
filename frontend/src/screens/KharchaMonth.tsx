import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees, whatsAppLink } from '../lib/format'
import { navigate } from '../router'
import type { ExpenseCategory } from './ExpensesScreen'
import { addText } from '../i18n'
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
  lines: { id: string; occurredAt: string; amount: number; category: ExpenseCategory; note: string | null; mode: 'cash' | 'upi'; by: string | null }[]
  previousMonthShopKharcha: number | null
}

/** "2026-10" ± months. */
export function shiftMonth(month: string, by: number) {
  const [y, m] = month.split('-').map(Number)
  const d = new Date(Date.UTC(y, m - 1 + by, 1))
  return `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}`
}

/** The India-time month now, "2026-10". */
export const thisMonth = () => new Date(Date.now() + 5.5 * 3600_000).toISOString().slice(0, 7)

const monthLabel = (month: string) => new Date(`${month}-01T00:00:00`).toLocaleDateString('en-IN', { month: 'long', year: 'numeric' })

export function useExpenseMonth(shopId: string, month: string) {
  return useQuery({ queryKey: [shopId, 'expenses', 'month', month], queryFn: () => api<ExpenseMonth>(`/v1/expenses/month?month=${month}`) })
}

/** The month's kharcha as a WhatsApp message: totals and the headings. */
function shareText(m: ExpenseMonth, shopName: string, t: (k: string, o?: Record<string, unknown>) => string) {
  return [
    t('kharchaMonth.waTitle', { shop: shopName, month: monthLabel(m.month) }),
    t('kharchaMonth.waTotal', { amount: formatRupees(m.shopKharcha) }),
    ...m.byCategory.filter((c) => c.category !== 'home').map((c) => `• ${t(`expense.cat.${c.category}`)}: ${formatRupees(c.total)}`),
    m.takenHome > 0 ? t('kharchaMonth.waHome', { amount: formatRupees(m.takenHome) }) : '',
    t('kharchaMonth.waModes', { cash: formatRupees(m.cash), upi: formatRupees(m.upi) }),
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
      <div className="flex items-center justify-between rounded-[18px] bg-white p-2">
        <button onClick={() => setMonth(shiftMonth(month, -1))} aria-label={t('book.previous')} className="h-11 w-11 rounded-xl text-xl">‹</button>
        <span className="text-lg font-bold">{monthLabel(month)}</span>
        <button onClick={() => setMonth(shiftMonth(month, 1))} disabled={month >= thisMonth()} aria-label={t('book.next')} className="h-11 w-11 rounded-xl text-xl disabled:opacity-30">›</button>
      </div>
      {!m && <p className="p-4 text-center text-muted">…</p>}
      {m && <>
        <section className="flex flex-col gap-1 rounded-[20px] bg-white p-4">
          <span className="text-sm font-semibold text-muted">{m.complete ? t('kharchaMonth.closed') : t('kharchaMonth.soFar')}</span>
          <span className="text-3xl font-extrabold">{formatRupees(m.shopKharcha)}</span>
          <span className="text-sm">{t('kharchaMonth.modes', { cash: formatRupees(m.cash), upi: formatRupees(m.upi), count: m.count })}</span>
          {m.takenHome > 0 && <span className="text-sm text-muted">{t('kharchaMonth.home', { amount: formatRupees(m.takenHome) })}</span>}
          {change !== null && (
            <span className={`text-sm font-semibold ${change > 0 ? 'text-due' : 'text-teal-800'}`}>
              {change > 0 ? t('kharchaMonth.more', { amount: formatRupees(change) }) : t('kharchaMonth.less', { amount: formatRupees(-change) })}
            </span>
          )}
        </section>

        {m.count > 0 && <>
          <div className="grid grid-cols-2 gap-2">
            <button onClick={() => navigate(`/print/kharcha/${m.month}`)} className="min-h-12 rounded-xl border border-line bg-white font-bold">{t('kharchaMonth.pdf')}</button>
            <a href={whatsAppLink(null, shareText(m, shopName, t))} target="_blank" rel="noreferrer" className="flex min-h-12 items-center justify-center rounded-xl bg-[#15803d] font-bold text-white">{t('kharchaMonth.share')}</a>
          </div>

          <section className="flex flex-col gap-2 rounded-[20px] bg-white p-4">
            <h2 className="font-bold">{t('kharchaMonth.byHeading')}</h2>
            {m.byCategory.map((c) => (
              <div key={c.category} className="flex flex-col gap-1">
                <span className="flex justify-between text-sm"><span>{t(`expense.cat.${c.category}`)} <span className="text-muted">×{c.count}</span></span><b>{formatRupees(c.total)}</b></span>
                <span className="h-2 rounded-full bg-ground"><span className={`block h-2 rounded-full ${c.category === 'home' ? 'bg-amber-400' : 'bg-teal-600'}`} style={{ width: `${(c.total / top) * 100}%` }} /></span>
              </div>
            ))}
          </section>

          <section className="flex flex-col gap-1 rounded-[20px] bg-white p-4">
            <h2 className="font-bold">{t('kharchaMonth.byDay')}</h2>
            <ul className="divide-y divide-[#edf2f1] text-sm">
              {m.byDay.map((d) => (
                <li key={d.date} className="flex justify-between py-2">
                  <span>{new Date(`${d.date}T00:00:00`).toLocaleDateString('en-IN', { weekday: 'short', day: 'numeric', month: 'short' })} <span className="text-muted">×{d.count}</span></span>
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

/** The month's kharcha on paper (/print/kharcha/2026-10): every line, totals by heading. Print or save as PDF. */
export function KharchaPrint({ shopId, shopName, month }: { shopId: string; shopName: string; month: string }) {
  const { t } = useTranslation()
  const m = useExpenseMonth(shopId, month).data
  if (!m) return <p className="p-6">…</p>
  return (
    <div className="mx-auto max-w-2xl bg-white p-6 text-[14px] text-black print:p-0">
      <button onClick={() => navigate('/expenses')} className="mb-3 font-semibold text-teal-700 print:hidden">← {t('kharchaMonth.back')}</button>
      <header className="border-b-2 border-black pb-2">
        <h1 className="text-2xl font-extrabold">{shopName}</h1>
        <p className="font-semibold">{t('kharchaMonth.printTitle', { month: monthLabel(m.month) })}{m.complete ? '' : ` · ${t('kharchaMonth.soFar')}`}</p>
      </header>
      <table className="mt-3 w-full border-collapse">
        <thead><tr className="border-b border-black text-left"><th className="py-1">{t('kharchaMonth.date')}</th><th>{t('kharchaMonth.heading')}</th><th>{t('kharchaMonth.note')}</th><th>{t('kharchaMonth.mode')}</th><th className="text-right">{t('kharchaMonth.amount')}</th></tr></thead>
        <tbody>
          {m.lines.map((l) => (
            <tr key={l.id} className="border-b border-gray-300">
              <td className="py-1 pr-2 whitespace-nowrap">{new Date(l.occurredAt).toLocaleDateString('en-IN', { day: 'numeric', month: 'short' })}</td>
              <td className="pr-2">{t(`expense.cat.${l.category}`)}</td>
              <td className="pr-2">{l.note}</td>
              <td className="pr-2 uppercase">{l.mode}</td>
              <td className="text-right whitespace-nowrap">{formatRupees(l.amount)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <table className="mt-4 w-full border-collapse">
        <tbody>
          {m.byCategory.map((c) => (
            <tr key={c.category}><td className="py-0.5">{t(`expense.cat.${c.category}`)}</td><td className="text-right">{formatRupees(c.total)}</td></tr>
          ))}
          <tr className="border-t-2 border-black text-lg font-extrabold"><td className="pt-1">{t('kharchaMonth.shopTotal')}</td><td className="text-right">{formatRupees(m.shopKharcha)}</td></tr>
          {m.takenHome > 0 && <tr><td>{t('kharchaMonth.homeShort')}</td><td className="text-right">{formatRupees(m.takenHome)}</td></tr>}
          <tr><td className="text-gray-600">{t('kharchaMonth.modesShort', { cash: formatRupees(m.cash), upi: formatRupees(m.upi) })}</td><td /></tr>
        </tbody>
      </table>
      <button onClick={() => window.print()} className="mt-6 w-full rounded-xl bg-teal-700 py-3 font-bold text-white print:hidden">{t('planBill.print')}</button>
    </div>
  )
}

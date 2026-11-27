import { shownLocale } from '../i18n'
import { formatRupees, indiaToday } from './format'
import type { SalesRange } from '../screens/SalesInsightsScreens'

const iso = (d: Date) => d.toISOString().slice(0, 10)

const shift = (date: string, days: number) => iso(new Date(Date.parse(`${date}T00:00:00Z`) + days * 86_400_000))

export const label = (date: string) => new Date(`${date}T00:00:00Z`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', timeZone: 'UTC' })

/** The quick choices: this week (Monday to today), last week, the last 7 days, this month so far. */
export function presets() {
  const today = indiaToday()
  const weekday = (new Date(`${today}T00:00:00Z`).getUTCDay() + 6) % 7
  const monday = shift(today, -weekday)
  return {
    thisWeek: { from: monday, to: today },
    lastWeek: { from: shift(monday, -7), to: shift(monday, -1) },
    last7: { from: shift(today, -6), to: today },
    thisMonth: { from: `${today.slice(0, 8)}01`, to: today },
  }
}

/** "+12%" against the period before; nothing when there was nothing before. */
export function change(now: number, before: number) {
  if (before <= 0) return null
  return Math.round(((now - before) / before) * 100)
}

/** The week (or any days) as a WhatsApp message for the owner: sales against before, best sellers, what didn't sell. */
export function weekSummary(r: SalesRange, shop: string, t: (k: string, o?: Record<string, unknown>) => string) {
  const pct = change(r.sales, r.previousSales)
  return [
    t('insights.waTitle', { shop, from: label(r.from), to: label(r.to) }),
    t('insights.waSales', { sales: formatRupees(r.sales), bills: r.bills }) + (pct === null ? '' : ` (${pct >= 0 ? '+' : ''}${pct}% ${t('insights.vsBefore')})`),
    t('insights.waTop'),
    ...r.items.slice(0, 5).map((i, n) => `${n + 1}. ${i.name}: ${i.quantity} ${t(`units.${i.unit}`, { count: i.quantity })} · ${formatRupees(i.sales)}`),
    r.notSold.length > 0 ? t('insights.waNotSold', { count: r.notSold.length, names: r.notSold.slice(0, 3).map((u) => u.name).join(', ') }) : '',
  ].filter(Boolean).join('\n')
}

import { shownLocale } from '../i18n'

/** The last 12 months as "yyyy-mm", newest first, in India's calendar. */
export function lastMonths(count = 12) {
  const now = new Date(Date.now() + (330 + new Date().getTimezoneOffset()) * 60_000)
  return Array.from({ length: count }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() - i, 1)
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
  })
}

export const monthLabel = (m: string) => new Date(`${m}-01T00:00`).toLocaleDateString(shownLocale(), { month: 'long', year: 'numeric' })

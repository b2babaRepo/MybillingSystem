import { useTranslation } from 'react-i18next'
import { shownLocale } from '../i18n'
import { whatsAppLink } from '../lib/format'

/** The last 12 months as "yyyy-mm", newest first, in India's calendar. */
export function lastMonths(count = 12) {
  const now = new Date(Date.now() + (330 + new Date().getTimezoneOffset()) * 60_000)
  return Array.from({ length: count }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() - i, 1)
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
  })
}

export const monthLabel = (m: string) => new Date(`${m}-01T00:00`).toLocaleDateString(shownLocale(), { month: 'long', year: 'numeric' })

/** A statement's period: everything, or one of the last 12 months. */
export function MonthSelect({ value, onChange, allLabel }: { value: string; onChange: (month: string) => void; allLabel: string }) {
  const { t } = useTranslation()
  return (
    <select value={value} onChange={(e) => onChange(e.target.value)} aria-label={t('cust7.period')} className="h-11 min-w-0 flex-1 rounded-xl border border-line bg-white px-3 font-semibold">
      <option value="">{allLabel}</option>
      {lastMonths().map((m) => <option key={m} value={m}>{monthLabel(m)}</option>)}
    </select>
  )
}

/** Print (or save as PDF from the print dialog) and send a short summary on WhatsApp. */
export function PrintShare({ text }: { text: string }) {
  const { t } = useTranslation()
  return (
    <div className="flex shrink-0 gap-2 print:hidden">
      <button onClick={() => window.print()} className="h-11 rounded-xl border border-line bg-white px-3 text-sm font-bold">{t('cust7.print')}</button>
      <a href={whatsAppLink(null, text)} target="_blank" rel="noreferrer" className="flex h-11 items-center rounded-xl bg-[#e3f2ee] px-3 text-sm font-bold text-brand-deep">WhatsApp</a>
    </div>
  )
}

import { useTranslation } from 'react-i18next'

import { whatsAppLink } from '../lib/format'


export interface DueItem { key: string; kind: 'girvi' | 'scheme' | 'order' | 'approval'; title: string; who: string; phone: string | null; message: string; overdue: boolean }

/** The "Due today" tab: one row per customer to remind, with a WhatsApp message ready to send. */
export function JewelleryTodayPanel({ items }: { items: DueItem[] }) {
  const { t } = useTranslation()
  return (
    <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
      <p className="text-sm text-muted">{t('jwl5.todayHint')}</p>
      {items.length === 0 && <p className="py-6 text-center text-muted">{t('jwl5.todayNone')}</p>}
      {items.map((i) => (
        <div key={i.key} className="flex flex-wrap items-center gap-2 border-b border-line py-2 last:border-0">
          <div className="min-w-0 flex-1">
            <b>{i.who}</b>
            <p className={`text-sm ${i.overdue ? 'font-semibold text-due' : 'text-muted'}`}>{i.title}</p>
          </div>
          {i.phone
            ? <a href={whatsAppLink(i.phone, i.message)} target="_blank" rel="noreferrer" className="flex min-h-11 items-center rounded-xl bg-[#1f9d55] px-4 text-sm font-bold text-white">{t('jwl5.remind')}</a>
            : <span className="text-xs text-muted">{t('jwl5.noPhone')}</span>}
        </div>
      ))}
    </section>
  )
}

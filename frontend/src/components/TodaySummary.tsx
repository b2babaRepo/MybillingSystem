import { useTranslation } from 'react-i18next'
import { formatRupees } from '../lib/format'
import type { Today } from '../lib/types'
import { navigate } from '../router'

/**
 * The end-of-day summary: what came in, what went on udhaar, what was collected, what is still owed and what to
 * reorder. "Send on WhatsApp" opens WhatsApp with it written out, so the owner can keep it or send it to a partner.
 */
export function TodaySummary({ today, shopName, lowCount }: { today: Today | undefined; shopName: string; lowCount: number }) {
  const { t, i18n } = useTranslation()
  if (!today) return null
  const collected = today.collected ?? 0
  const rows: { label: string; value: string; tone?: string; to?: string }[] = [
    { label: t('summary.sales', { count: today.bills }), value: formatRupees(today.sales) },
    { label: t('cash'), value: formatRupees(today.cash), tone: 'text-emerald-800' },
    { label: t('upi'), value: formatRupees(today.upi), tone: 'text-blue-800' },
    { label: t('summary.udhaarGiven'), value: formatRupees(today.udhaar), tone: 'text-udhaar' },
    { label: t('summary.collected'), value: formatRupees(collected), tone: 'text-jama' },
    { label: t('summary.outstanding', { count: today.customersOwing }), value: formatRupees(today.outstanding), tone: 'text-due', to: '/dues' },
    ...(lowCount > 0 ? [{ label: t('summary.lowStock'), value: String(lowCount), tone: 'text-udhaar', to: '/reorder' }] : []),
  ]
  const day = new Date(today.date).toLocaleDateString(i18n.language === 'hi' ? 'hi-IN' : 'en-IN', { day: 'numeric', month: 'short' })
  const message = [`*${shopName} · ${t('summary.title')} · ${day}*`, ...rows.map((r) => `${r.label}: ${r.value}`), t('summary.footer')].join('\n')

  function share() {
    // The phone's share sheet where there is one (it offers WhatsApp too), else WhatsApp's own link.
    if (navigator.share) {
      void navigator.share({ text: message }).catch(() => undefined)
      return
    }
    window.open(`https://wa.me/?text=${encodeURIComponent(message)}`, '_blank', 'noopener')
  }

  return (
    <section className="mx-4 mt-4 flex flex-col gap-1 rounded-[18px] bg-white p-4">
      <div className="mb-1 flex items-baseline justify-between">
        <h2 className="font-bold">{t('summary.title')}</h2>
        <span className="text-[13px] text-muted">{day}</span>
      </div>
      {rows.map((r) => {
        const body = (
          <>
            <span className="text-[15px]">{r.label}</span>
            <span className={`text-[15px] font-bold ${r.tone ?? ''}`}>{r.value}</span>
          </>
        )
        return r.to
          ? <button key={r.label} onClick={() => navigate(r.to!)} className="flex min-h-10 items-center justify-between border-t border-[#edf2f1] text-left">{body}</button>
          : <div key={r.label} className="flex min-h-10 items-center justify-between border-t border-[#edf2f1] first-of-type:border-0">{body}</div>
      })}
      <button onClick={share} className="mt-2 flex min-h-12 items-center justify-center gap-2 rounded-xl bg-[#1f9d55] font-bold text-white">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor" aria-hidden><path d="M12 2a10 10 0 0 0-8.6 15.1L2 22l5-1.3A10 10 0 1 0 12 2zm0 18.2a8.2 8.2 0 0 1-4.2-1.2l-.3-.2-3 .8.8-2.9-.2-.3A8.2 8.2 0 1 1 12 20.2zm4.5-6.1c-.2-.1-1.5-.7-1.7-.8s-.4-.1-.6.1-.7.8-.8 1-.3.2-.5.1a6.7 6.7 0 0 1-3.3-2.9c-.2-.4.2-.4.7-1.3.1-.2 0-.3 0-.4l-.8-1.8c-.2-.5-.4-.4-.6-.4h-.5a1 1 0 0 0-.7.3 3 3 0 0 0-.9 2.2 5.2 5.2 0 0 0 1.1 2.7 11.8 11.8 0 0 0 4.5 4 5.2 5.2 0 0 0 3.2.7 2.7 2.7 0 0 0 1.8-1.3 2.2 2.2 0 0 0 .2-1.3c-.1-.1-.3-.2-.5-.3z" /></svg>
        {t('summary.send')}
      </button>
    </section>
  )
}

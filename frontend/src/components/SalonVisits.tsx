import { useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { shownLocale } from '../i18n'
import { cancelVisit, hhmm, setBookAgain, useVisits } from '../lib/salon'
import { navigate } from '../router'
import { confirmAsk } from './Ask'

/** Home: the customer's coming salon visits (with cancel), and "book again" for the last visit at each salon. */
export function SalonVisits() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const visits = useVisits().data ?? []
  const upcomingAt = new Set(visits.filter((v) => v.upcoming).map((v) => v.shopId))
  const shown = visits.filter((v) => v.upcoming || !upcomingAt.has(v.shopId)).slice(0, 3)
  if (shown.length === 0) return null
  const day = (iso: string) => new Date(`${iso}T00:00`).toLocaleDateString(shownLocale(), { weekday: 'short', day: 'numeric', month: 'short' })

  async function cancel(shopId: string, id: string, shopName: string) {
    if (!(await confirmAsk(t('cust5.cancelVisit', { shop: shopName })))) return
    await cancelVisit(shopId, id).catch(() => {})
    await queryClient.invalidateQueries({ queryKey: ['me', 'salon-visits'] })
  }

  return (
    <section aria-label={t('cust5.visits')} className="flex flex-col gap-2 rounded-card border border-[#ead9f5] bg-[#faf5ff] p-3.5">
      <h2 className="font-extrabold text-[#5b21b6]">{t('cust5.visits')}</h2>
      {shown.map((v) => (
        <div key={v.id} className="flex items-center gap-3 rounded-xl bg-white p-2.5">
          <span className="flex min-w-0 flex-1 flex-col">
            <b className="truncate">{v.upcoming ? `${day(v.date)} · ${hhmm(v.start)}` : t('cust5.lastVisit', { day: day(v.date) })}</b>
            <span className="truncate text-xs text-muted">{v.services.map((s) => s.name).join(', ')} · {v.shopName}</span>
          </span>
          {v.upcoming
            ? <button onClick={() => void cancel(v.shopId, v.id, v.shopName)} className="h-10 shrink-0 rounded-xl border-2 border-line px-3 text-sm font-bold text-due">{t('cancel')}</button>
            : v.shopSlug && <button onClick={() => { setBookAgain(v.shopSlug!, v.services.map((s) => s.productId)); navigate(`/s/${v.shopSlug}`) }} className="h-10 shrink-0 rounded-xl bg-[#6d28d9] px-3 text-sm font-bold text-white">{t('cust5.bookAgain')}</button>}
        </div>
      ))}
    </section>
  )
}

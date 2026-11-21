import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { shownLocale } from '../i18n'
import { api } from '../lib/api'
import { indiaDay } from '../lib/deliveries'
import { formatPhone } from '../lib/format'
import type { Unit } from '../lib/types'

interface Round { date: string; deliveries: { planId: string; name: string; phone: string | null; address: string | null; productName: string; unit: Unit; quantity: number }[]; totals: { productName: string; unit: Unit; quantity: number }[] }

/** The shop's daily round from customers' standing orders: what to take, and who gets what, today or tomorrow. */
export function DeliveryRound() {
  const { t } = useTranslation()
  const [offset, setOffset] = useState(1)
  const date = indiaDay(offset)
  const round = useQuery({ queryKey: ['deliveries', date], queryFn: () => api<Round>(`/v1/deliveries?date=${date}`), retry: false }).data
  const any = useQuery({ queryKey: ['deliveries', indiaDay(1)], queryFn: () => api<Round>(`/v1/deliveries?date=${indiaDay(1)}`), retry: false }).data
  const [open, setOpen] = useState(false)
  if (!round || (!any?.deliveries.length && !round.deliveries.length)) return null
  const unit = (u: Unit, q: number) => t(`units.${u}`, { count: q })

  return (
    <section className="flex flex-col gap-2 rounded-2xl border border-[#cde9e1] bg-[#f0faf7] p-3" aria-label={t('cust5.roundTitle')}>
      <div className="flex items-center justify-between gap-2">
        <h2 className="font-extrabold text-brand-deep">{t('cust5.roundTitle')}</h2>
        <div className="flex gap-1">
          {[0, 1].map((n) => (
            <button key={n} onClick={() => setOffset(n)} aria-pressed={offset === n} className={`h-9 rounded-full px-3 text-sm font-bold ${offset === n ? 'bg-brand text-white' : 'bg-white'}`}>
              {n === 0 ? t('cust5.today') : t('cust5.tomorrow')}
            </button>
          ))}
        </div>
      </div>
      <span className="text-xs text-muted">{new Date(`${round.date}T00:00`).toLocaleDateString(shownLocale(), { weekday: 'long', day: 'numeric', month: 'short' })} · {t('cust5.roundCount', { count: round.deliveries.length })}</span>
      {round.totals.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {round.totals.map((x) => <span key={x.productName} className="rounded-full bg-white px-3 py-1 text-sm font-bold">{x.quantity} {unit(x.unit, x.quantity)} · {x.productName}</span>)}
        </div>
      )}
      {round.deliveries.length > 0 && <button onClick={() => setOpen(!open)} aria-expanded={open} className="min-h-10 self-start text-sm font-bold text-brand">{open ? t('a11y.less') : t('cust5.roundWho')}</button>}
      {open && (
        <ul className="flex flex-col divide-y divide-[#e2efeb] rounded-xl bg-white">
          {round.deliveries.map((d) => (
            <li key={d.planId} className="flex flex-col px-3 py-2 text-sm">
              <span className="flex justify-between gap-2"><b className="truncate">{d.name}</b><b className="shrink-0">{d.quantity} {unit(d.unit, d.quantity)} {d.productName}</b></span>
              <span className="text-muted">{d.address}{d.phone ? ` · ${formatPhone(d.phone)}` : ''}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

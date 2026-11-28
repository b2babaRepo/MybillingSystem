import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { shownLocale } from '../i18n'
import { api } from '../lib/api'
import { indiaDay } from '../lib/deliveries'
import { formatPhone } from '../lib/format'
import type { Unit } from '../lib/types'
import { shownError } from '../lib/notify'

interface Round { date: string; deliveries: { planId: string; name: string; phone: string | null; address: string | null; productName: string; unit: Unit; quantity: number; onKhata: boolean; posted: boolean }[]; totals: { productName: string; unit: Unit; quantity: number }[] }

/** The shop's daily round from customers' standing orders: what to take, and who gets what, today or tomorrow. */
export function DeliveryRound() {
  const { t } = useTranslation()
  const [offset, setOffset] = useState(1)
  const date = indiaDay(offset)
  const round = useQuery({ queryKey: ['deliveries', date], queryFn: () => api<Round>(`/v1/deliveries?date=${date}`), retry: false }).data
  // Shown while today or tomorrow has deliveries (today's may still need putting on the khatas).
  const todays = useQuery({ queryKey: ['deliveries', indiaDay(0)], queryFn: () => api<Round>(`/v1/deliveries?date=${indiaDay(0)}`), retry: false }).data
  const tomorrows = useQuery({ queryKey: ['deliveries', indiaDay(1)], queryFn: () => api<Round>(`/v1/deliveries?date=${indiaDay(1)}`), retry: false }).data
  const [open, setOpen] = useState(false)
  const queryClient = useQueryClient()
  const [posting, setPosting] = useState(false)
  const [message, setMessage] = useState('')
  if (!round || (!todays?.deliveries.length && !tomorrows?.deliveries.length)) return null
  const unit = (u: Unit, q: number) => t(`units.${u}`, { count: q })
  const toPost = round.deliveries.filter((d) => d.onKhata && !d.posted).length
  const noKhata = round.deliveries.filter((d) => !d.onKhata).length

  // Today's round onto the khatas: one bill per delivery, at today's price; pressing again never bills twice.
  async function post() {
    setPosting(true)
    setMessage('')
    try {
      const r = await api<{ posted: number; alreadyPosted: number; noKhata: string[]; failed: string[] }>(`/v1/deliveries/${round!.date}/post`, 'POST', {})
      setMessage([t('cust5.postedCount', { count: r.posted }), r.noKhata.length ? t('cust5.collectFrom', { names: r.noKhata.join(', ') }) : '', ...r.failed].filter(Boolean).join(' · '))
      await queryClient.invalidateQueries({ queryKey: ['deliveries'] })
    } catch (e) {
      setMessage(shownError(e))
    } finally {
      setPosting(false)
    }
  }

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
      {offset === 0 && toPost > 0 && (
        <button onClick={() => void post()} disabled={posting} className="h-11 rounded-xl bg-brand px-4 font-bold text-white disabled:opacity-50">{t('cust5.postToKhata', { count: toPost })}</button>
      )}
      {offset === 0 && toPost === 0 && round.deliveries.some((d) => d.posted) && <span className="text-sm font-semibold text-brand-deep">✓ {t('cust5.allPosted')}</span>}
      {offset === 0 && noKhata > 0 && <span className="text-xs text-muted">{t('cust5.noKhataCount', { count: noKhata })}</span>}
      {message && <p className="text-sm font-semibold text-brand-deep">{message}</p>}
      {round.deliveries.length > 0 && <button onClick={() => setOpen(!open)} aria-expanded={open} className="min-h-10 self-start text-sm font-bold text-brand">{open ? t('a11y.less') : t('cust5.roundWho')}</button>}
      {open && (
        <ul className="flex flex-col divide-y divide-[#e2efeb] rounded-xl bg-white">
          {round.deliveries.map((d) => (
            <li key={d.planId} className="flex flex-col px-3 py-2 text-sm">
              <span className="flex justify-between gap-2"><b className="truncate">{d.name}</b><b className="shrink-0">{d.quantity} {unit(d.unit, d.quantity)} {d.productName}</b></span>
              <span className="text-muted">{d.address}{d.phone ? ` · ${formatPhone(d.phone)}` : ''}</span>
              {offset === 0 && <span className={`text-xs font-semibold ${d.posted ? 'text-brand' : d.onKhata ? 'text-muted' : 'text-udhaar'}`}>{d.posted ? `✓ ${t('cust5.onKhataNow')}` : d.onKhata ? t('cust5.notPosted') : t('cust5.noKhata')}</span>}
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

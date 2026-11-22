import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { shownLocale } from '../i18n'
import { formatRupees } from '../lib/format'
import { bookVisit } from '../lib/salon'
import { btn } from './ui'

export interface SalonService { id: string; name: string; price: number; minutes: number }

const pad = (n: number) => String(n).padStart(2, '0')
/** India's date and time now, whatever the phone's own time zone. */
const indiaNow = () => new Date(Date.now() + (330 + new Date().getTimezoneOffset()) * 60_000)
const isoDay = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`

/** Book a salon visit: services, a day this week, a time; it lands in the salon's day for any stylist. */
export function SalonBooking({ shopId, shopName, services, preselect, onClose }: {
  shopId: string; shopName: string; services: SalonService[]; preselect?: string[] | null; onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const now = indiaNow()
  const days = Array.from({ length: 7 }, (_, i) => { const d = new Date(now); d.setDate(d.getDate() + i); return d })
  const [picked, setPicked] = useState<string[]>((preselect ?? []).filter((id) => services.some((s) => s.id === id)))
  const [day, setDay] = useState(isoDay(days[0]))
  const [time, setTime] = useState('')
  const [note, setNote] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [done, setDone] = useState(false)
  // Half-hour slots from 9 to 8:30; today only from the next slot that is still ahead.
  const nowMinutes = now.getHours() * 60 + now.getMinutes()
  const slots = Array.from({ length: 24 }, (_, i) => 9 * 60 + i * 30).filter((m) => day !== isoDay(days[0]) || m > nowMinutes + 15)
    .map((m) => `${pad(Math.floor(m / 60))}:${pad(m % 60)}`)
  const chosen = services.filter((s) => picked.includes(s.id))
  const total = chosen.reduce((s, x) => s + x.price, 0)
  const minutes = chosen.reduce((s, x) => s + x.minutes, 0)

  async function book() {
    setBusy(true)
    setError('')
    try {
      await bookVisit(shopId, { id: crypto.randomUUID(), date: day, start: time, serviceIds: picked, note: note.trim() })
      await queryClient.invalidateQueries({ queryKey: ['me', 'salon-visits'] })
      setDone(true)
    } catch (e) {
      setError(e instanceof Error ? e.message : t('error'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="fixed inset-0 z-30 flex items-end bg-black/40 lg:items-center lg:justify-center lg:p-6" onClick={onClose}>
      <div role="dialog" aria-label={t('cust5.bookTitle')} onClick={(e) => e.stopPropagation()} className="flex max-h-[90dvh] w-full flex-col gap-3 overflow-y-auto rounded-t-3xl bg-white p-4 lg:max-w-lg lg:rounded-3xl">
        <h2 className="text-lg font-extrabold">{t('cust5.bookTitle')} · {shopName}</h2>
        {done ? (
          <>
            <p className="rounded-xl bg-brand-soft p-3 font-semibold text-brand-deep">{t('cust5.booked', { day: new Date(`${day}T00:00`).toLocaleDateString(shownLocale(), { weekday: 'short', day: 'numeric', month: 'short' }), time })}</p>
            <button onClick={onClose} className={`h-12 rounded-xl ${btn.solid}`}>{t('a11y.close')}</button>
          </>
        ) : <>
          <fieldset className="flex flex-col gap-1.5">
            <legend className="mb-1 text-sm font-semibold text-muted">{t('cust5.services')}</legend>
            {services.map((s) => (
              <label key={s.id} className={`flex min-h-12 items-center gap-3 rounded-xl px-3 ${picked.includes(s.id) ? 'border-2 border-brand bg-brand-soft' : 'border-2 border-line'}`}>
                <input type="checkbox" checked={picked.includes(s.id)} onChange={() => setPicked(picked.includes(s.id) ? picked.filter((x) => x !== s.id) : [...picked, s.id])} className="h-5 w-5 accent-[#0b6e5c]" />
                <span className="flex-1 font-semibold">{s.name}</span>
                <span className="text-sm text-muted">{t('cust5.minutes', { count: s.minutes })}</span>
                <b>{formatRupees(s.price)}</b>
              </label>
            ))}
          </fieldset>
          <fieldset className="flex flex-col gap-1.5">
            <legend className="mb-1 text-sm font-semibold text-muted">{t('cust5.day')}</legend>
            <div className="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1">
              {days.map((d) => (
                <button key={isoDay(d)} type="button" onClick={() => { setDay(isoDay(d)); setTime('') }} aria-pressed={day === isoDay(d)}
                  className={`flex h-14 w-16 shrink-0 flex-col items-center justify-center rounded-xl text-sm font-bold ${day === isoDay(d) ? 'bg-brand text-white' : 'border-2 border-line'}`}>
                  <span className="text-xs font-semibold opacity-80">{d.toLocaleDateString(shownLocale(), { weekday: 'short' })}</span>{d.getDate()}
                </button>
              ))}
            </div>
          </fieldset>
          <label className="flex flex-col gap-1.5">
            <span className="text-sm font-semibold text-muted">{t('cust5.time')}</span>
            <select value={time} onChange={(e) => setTime(e.target.value)} className="h-12 rounded-xl border-2 border-line bg-white px-3">
              <option value="">{slots.length === 0 ? t('cust5.noSlots') : t('cust5.pickTime')}</option>
              {slots.map((s) => <option key={s} value={s}>{s}</option>)}
            </select>
          </label>
          <label className="flex flex-col gap-1.5">
            <span className="text-sm font-semibold text-muted">{t('store.note')}</span>
            <input value={note} onChange={(e) => setNote(e.target.value)} maxLength={150} className="h-12 rounded-xl border-2 border-line px-3" />
          </label>
          {error && <p className="text-sm text-due">{error}</p>}
          <button onClick={() => void book()} disabled={busy || picked.length === 0 || !time} className={`h-14 rounded-xl text-lg disabled:opacity-50 ${btn.solid}`}>
            {picked.length === 0 ? t('cust5.pickService') : t('cust5.bookFor', { total: formatRupees(total), minutes })}
          </button>
          <p className="text-center text-xs text-muted">{t('cust5.bookNote')}</p>
        </>}
      </div>
    </div>
  )
}

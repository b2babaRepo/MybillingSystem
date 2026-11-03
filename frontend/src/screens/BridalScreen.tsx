import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { useCustomers } from '../lib/data'
import { formatRupees, whatsAppLink } from '../lib/format'

interface BridalEvent { id: string; name: string; date: string; start: string; end: string; price: number; stylistName: string | null }
interface Booking {
  id: string; number: number; customerName: string; customerPhone: string | null; title: string; atHome: boolean; address: string | null; travelCharge: number
  status: 'booked' | 'done' | 'cancelled'; events: BridalEvent[]; total: number; advance: number
}
interface EventDraft { name: string; date: string; start: string; minutes: string; price: string; stylistId: string }

const day = (iso: string) => new Date(`${iso}T00:00`).toLocaleDateString('en-IN', { weekday: 'short', day: 'numeric', month: 'short' })
const hm = (t: string) => t.slice(0, 5)

/** Bridal and home bookings: several events on their own dates, a travel charge, trial fee and advances, then one final bill. */
export function BridalScreen({ shopId, shopName, isOwner }: { shopId: string; shopName: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const bookings = useQuery({ queryKey: [shopId, 'bridal'], queryFn: () => api<Booking[]>('/v1/bridal') }).data ?? []
  const [message, setMessage] = useState('')
  const [pay, setPay] = useState<{ id: string; amount: string; mode: string; trial: boolean } | null>(null)

  async function run(path: string, body: unknown, done: string) {
    setMessage('')
    try {
      await api(path, 'POST', body)
      setMessage(done)
      setPay(null)
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  const select = 'h-12 w-full rounded-xl border border-line bg-white px-3'
  const reminder = (b: Booking) => t('bridal.reminder', {
    name: b.customerName, shop: shopName, events: b.events.map((e) => `${e.name} ${day(e.date)} ${hm(e.start)}`).join(', '), due: formatRupees(Math.max(0, b.total - b.advance)),
  })

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <p className="text-sm text-muted">{t('bridal.hint')}</p>
      {message && <p className="rounded-xl bg-teal-50 p-3 text-sm font-semibold text-teal-900">{message}</p>}
      <NewBooking shopId={shopId} onDone={setMessage} />

      {bookings.length === 0 && <p className="text-sm text-muted">{t('bridal.none')}</p>}
      {bookings.map((b) => (
        <section key={b.id} className="flex flex-col gap-1 rounded-[18px] bg-white p-3">
          <h3 className="font-bold">#{b.number} {b.title} · {b.customerName}</h3>
          {b.atHome && <p className="text-xs text-muted">{t('bridal.atHome')}{b.address ? `: ${b.address}` : ''}</p>}
          {b.events.map((e) => (
            <p key={e.id} className="flex justify-between text-sm"><span>{e.name} · {day(e.date)} {hm(e.start)}–{hm(e.end)}{e.stylistName ? ` · ${e.stylistName}` : ''}</span><span>{formatRupees(e.price)}</span></p>
          ))}
          {b.travelCharge > 0 && <p className="flex justify-between text-sm"><span>{t('bridal.travel')}</span><span>{formatRupees(b.travelCharge)}</span></p>}
          <p className="flex justify-between font-bold"><span>{t('total')}</span><span>{formatRupees(b.total)}</span></p>
          <p className="flex justify-between text-sm text-teal-900"><span>{t('bridal.received')}</span><span>{formatRupees(b.advance)}</span></p>
          <p className="flex justify-between text-sm font-semibold text-due"><span>{t('bridal.balance')}</span><span>{formatRupees(Math.max(0, b.total - b.advance))}</span></p>

          {pay?.id === b.id ? (
            <div className="mt-1 grid grid-cols-2 gap-2">
              <Field label={t('bridal.amount')} value={pay.amount} onChange={(e) => setPay({ ...pay, amount: e.target.value })} inputMode="decimal" />
              <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('jobs.mode')}</span>
                <select value={pay.mode} onChange={(e) => setPay({ ...pay, mode: e.target.value })} className={select}><option value="upi">UPI</option><option value="cash">{t('cash')}</option></select>
              </label>
              <label className="col-span-2 flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={pay.trial} onChange={(e) => setPay({ ...pay, trial: e.target.checked })} />{t('bridal.isTrial')}</label>
              <Button disabled={!(Number(pay.amount) > 0)} onClick={() => void run(`/v1/bridal/${b.id}/payment`, { amount: Number(pay.amount), mode: pay.mode, trial: pay.trial }, t('bridal.paid'))}>{t('save')}</Button>
              <Button tone="plain" onClick={() => setPay(null)}>{t('cancel')}</Button>
            </div>
          ) : (
            <div className="mt-1 flex flex-wrap gap-2">
              <Button tone="plain" className="h-10 px-3 text-sm" onClick={() => setPay({ id: b.id, amount: String(Math.round(b.total * 0.4)), mode: 'upi', trial: false })}>{t('bridal.takePayment')}</Button>
              {b.customerPhone && <a href={whatsAppLink(b.customerPhone, reminder(b))} target="_blank" rel="noreferrer" className="flex h-10 items-center rounded-xl bg-[#15803d] px-3 text-sm font-semibold text-white">{t('tables.remind')}</a>}
              {isOwner && <Button className="h-10 px-3 text-sm" onClick={() => { if (confirm(t('bridal.completeConfirm', { due: formatRupees(Math.max(0, b.total - b.advance)) }))) void run(`/v1/bridal/${b.id}/complete`, { billId: crypto.randomUUID(), paidAmount: 0 }, t('bridal.completed')) }}>{t('bridal.complete')}</Button>}
              {isOwner && <Button tone="plain" className="h-10 px-3 text-sm" onClick={() => {
                const keep = prompt(t('bridal.cancelKeep'), '0')
                if (keep !== null) void run(`/v1/bridal/${b.id}/cancel`, { reason: '', keep: Number(keep) || 0 }, t('bridal.cancelled'))
              }}>{t('bridal.cancel')}</Button>}
            </div>
          )}
        </section>
      ))}
    </div>
  )
}

function NewBooking({ shopId, onDone }: { shopId: string; onDone: (message: string) => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { customers } = useCustomers(shopId)
  const stylists = useQuery({ queryKey: [shopId, 'appointments', 'stylists'], queryFn: () => api<{ stylists: { id: string; name: string }[] }>('/v1/appointments') }).data?.stylists ?? []
  const blank: EventDraft = { name: '', date: '', start: '08:00', minutes: '180', price: '', stylistId: '' }
  const [d, setD] = useState({ customerId: '', title: '', atHome: true, address: '', travel: '0' })
  const [events, setEvents] = useState<EventDraft[]>([{ ...blank, name: t('bridal.wedding') }])
  const [error, setError] = useState('')
  const setEvent = (i: number, key: keyof EventDraft) => (e: { target: { value: string } }) => setEvents(events.map((x, j) => (j === i ? { ...x, [key]: e.target.value } : x)))

  async function save() {
    setError('')
    try {
      await api('/v1/bridal', 'POST', {
        id: crypto.randomUUID(), customerId: d.customerId, title: d.title.trim(), atHome: d.atHome, address: d.address, travelCharge: Number(d.travel) || 0,
        events: events.map((e) => ({ name: e.name.trim(), date: e.date, start: e.start, minutes: Number(e.minutes) || 0, price: Number(e.price) || 0, stylistId: e.stylistId || null })),
      })
      setD({ customerId: '', title: '', atHome: true, address: '', travel: '0' })
      setEvents([{ ...blank, name: t('bridal.wedding') }])
      onDone(t('bridal.booked'))
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setError(e instanceof Error ? e.message : t('error'))
    }
  }
  const select = 'h-12 w-full rounded-xl border border-line bg-white px-3'
  return (
    <details className="rounded-[18px] bg-white p-3">
      <summary className="min-h-11 cursor-pointer font-bold">{t('bridal.add')}</summary>
      <div className="mt-2 flex flex-col gap-2">
        <select value={d.customerId} onChange={(e) => setD({ ...d, customerId: e.target.value })} aria-label={t('jobs.customer')} className={select}>
          <option value="">{t('packages.chooseCustomer')}</option>{customers.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
        <Field label={t('bridal.title')} value={d.title} onChange={(e) => setD({ ...d, title: e.target.value })} placeholder={t('bridal.titleHint')} />
        <label className="flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={d.atHome} onChange={(e) => setD({ ...d, atHome: e.target.checked })} />{t('bridal.atHome')}</label>
        {d.atHome && (
          <div className="grid grid-cols-[2fr_1fr] gap-2">
            <Field label={t('bridal.address')} value={d.address} onChange={(e) => setD({ ...d, address: e.target.value })} />
            <Field label={t('bridal.travel')} value={d.travel} onChange={(e) => setD({ ...d, travel: e.target.value })} inputMode="decimal" />
          </div>
        )}
        {events.map((e, i) => (
          <div key={i} className="grid grid-cols-2 gap-2 rounded-xl bg-ground p-2">
            <Field label={t('bridal.event')} value={e.name} onChange={setEvent(i, 'name')} placeholder={t('bridal.eventHint')} />
            <Field label={t('bridal.price')} value={e.price} onChange={setEvent(i, 'price')} inputMode="decimal" />
            <Field label={t('bridal.date')} type="date" value={e.date} onChange={setEvent(i, 'date')} />
            <Field label={t('bridal.start')} type="time" value={e.start} onChange={setEvent(i, 'start')} />
            <Field label={t('bridal.minutes')} value={e.minutes} onChange={setEvent(i, 'minutes')} inputMode="numeric" />
            <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('bridal.stylist')}</span>
              <select value={e.stylistId} onChange={setEvent(i, 'stylistId')} className={select}><option value="">—</option>{stylists.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select>
            </label>
            {events.length > 1 && <Button tone="plain" className="col-span-2 h-10 text-sm" onClick={() => setEvents(events.filter((_, j) => j !== i))}>{t('bridal.removeEvent')}</Button>}
          </div>
        ))}
        {events.length < 10 && <Button tone="plain" onClick={() => setEvents([...events, { ...blank }])}>{t('bridal.addEvent')}</Button>}
        {error && <p className="text-sm text-due">{error}</p>}
        <Button disabled={!d.customerId || d.title.trim().length < 2 || events.some((e) => e.name.trim().length < 2 || !e.date)} onClick={() => void save()}>{t('bridal.book')}</Button>
      </div>
    </details>
  )
}

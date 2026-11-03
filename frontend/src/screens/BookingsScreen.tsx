import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { useCustomers } from '../lib/data'
import { formatRupees } from '../lib/format'
import { JobPage } from './JobsScreen'

type Slot = 'morning' | 'evening' | 'full_day'
type Occasion = 'wedding' | 'reception' | 'engagement' | 'birthday' | 'anniversary' | 'other'
interface Venue { id: string; name: string; capacity: number | null }
interface Booking {
  id: string; number: number; venueId: string; venueName: string; date: string; slot: Slot; status: 'hold' | 'expired' | 'confirmed' | 'cancelled' | 'settled'
  holdUntil: string | null; customerId: string | null; customerName: string | null; occasion: Occasion; guests: number; plateRate: number; hallRent: number
  estimate: number; deposit: number; depositTaken: number; depositReturned: number | null; actualGuests: number | null; jobId: string | null; advance: number
  note: string | null; cancelReason: string | null
}

const SLOTS: Slot[] = ['morning', 'evening', 'full_day']
const OCCASIONS: Occasion[] = ['wedding', 'reception', 'engagement', 'birthday', 'anniversary', 'other']
const monthOf = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
const STATUS_TONE: Record<Booking['status'], string> = {
  hold: 'bg-amber-100 text-amber-900', expired: 'bg-slate-100 text-muted', confirmed: 'bg-teal-100 text-teal-900', cancelled: 'bg-red-50 text-due', settled: 'bg-slate-200 text-ink',
}

/** Hall and lawn bookings for weddings, receptions, birthdays and anniversaries: a month at a time, each date and slot booked once. */
export function BookingsScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const [month, setMonth] = useState(monthOf(new Date()))
  const [openId, setOpenId] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)
  const venues = useQuery({ queryKey: [shopId, 'venues'], queryFn: () => api<Venue[]>('/v1/venues') }).data ?? []
  const bookings = useQuery({ queryKey: [shopId, 'bookings', month], queryFn: () => api<Booking[]>(`/v1/bookings?month=${month}`) }).data ?? []

  if (openId) return <BookingPage shopId={shopId} id={openId} isOwner={isOwner} onBack={() => setOpenId(null)} />
  const shift = (n: number) => { const [y, m] = month.split('-').map(Number); setMonth(monthOf(new Date(y, m - 1 + n, 1))) }
  const label = new Date(`${month}-01T00:00`).toLocaleDateString('en-IN', { month: 'long', year: 'numeric' })
  const byDate = bookings.reduce<Record<string, Booking[]>>((acc, b) => ({ ...acc, [b.date]: [...(acc[b.date] ?? []), b] }), {})

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <p className="text-sm text-muted">{t('bookings.hint')}</p>
      {venues.length === 0 ? <AddVenue shopId={shopId} first /> : (
        <>
          <div className="flex items-center justify-between rounded-[18px] bg-white p-2">
            <button onClick={() => shift(-1)} aria-label={t('bookings.prev')} className="h-11 w-11 text-xl">‹</button>
            <span className="font-bold">{label}</span>
            <button onClick={() => shift(1)} aria-label={t('bookings.next')} className="h-11 w-11 text-xl">›</button>
          </div>
          {adding ? <NewBooking shopId={shopId} venues={venues} onDone={(id) => { setAdding(false); if (id) setOpenId(id) }} /> : <Button onClick={() => setAdding(true)}>{t('bookings.new')}</Button>}
          {Object.keys(byDate).length === 0 && <p className="rounded-[18px] bg-white p-4 text-center text-muted">{t('bookings.none')}</p>}
          {Object.entries(byDate).map(([date, list]) => (
            <section key={date} className="rounded-[18px] bg-white p-3">
              <h3 className="mb-1 font-bold">{new Date(`${date}T00:00`).toLocaleDateString('en-IN', { weekday: 'short', day: 'numeric', month: 'short' })}</h3>
              <ul className="flex flex-col gap-1">
                {list.map((b) => (
                  <li key={b.id}>
                    <button onClick={() => setOpenId(b.id)} className="flex min-h-12 w-full items-center gap-2 rounded-xl px-2 text-left active:bg-slate-50">
                      <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-semibold ${STATUS_TONE[b.status]}`}>{t(`bookings.status.${b.status}`)}</span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate font-semibold">{t(`bookings.occasion.${b.occasion}`)} · {b.customerName ?? '—'}</span>
                        <span className="block text-xs text-muted">{b.venueName} · {t(`bookings.slot.${b.slot}`)} · {t('bookings.guests', { count: b.guests })}</span>
                      </span>
                      <span className="text-sm font-bold">{formatRupees(b.estimate)}</span>
                    </button>
                  </li>
                ))}
              </ul>
            </section>
          ))}
          {isOwner && <AddVenue shopId={shopId} />}
        </>
      )}
    </div>
  )
}

function AddVenue({ shopId, first = false }: { shopId: string; first?: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [d, setD] = useState({ name: '', capacity: '' })
  const [open, setOpen] = useState(first)
  if (!open) return <button onClick={() => setOpen(true)} className="min-h-11 text-sm font-semibold text-teal-700">+ {t('bookings.addHall')}</button>
  return (
    <div className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
      <h3 className="font-bold">{t(first ? 'bookings.firstHall' : 'bookings.addHall')}</h3>
      <div className="grid grid-cols-[2fr_1fr] gap-2">
        <Field label={t('bookings.hallName')} value={d.name} onChange={(e) => setD({ ...d, name: e.target.value })} placeholder={t('bookings.hallHint')} />
        <Field label={t('bookings.capacity')} value={d.capacity} onChange={(e) => setD({ ...d, capacity: e.target.value })} inputMode="numeric" />
      </div>
      <Button disabled={d.name.trim().length < 2} onClick={() => void api('/v1/venues', 'POST', { id: crypto.randomUUID(), name: d.name.trim(), capacity: Number(d.capacity) || null })
        .then(() => { setD({ name: '', capacity: '' }); setOpen(first); void queryClient.invalidateQueries({ queryKey: [shopId, 'venues'] }) })}>{t('save')}</Button>
    </div>
  )
}

function NewBooking({ shopId, venues, onDone }: { shopId: string; venues: Venue[]; onDone: (id?: string) => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { customers } = useCustomers(shopId)
  const [d, setD] = useState({ venueId: venues[0]?.id ?? '', date: '', slot: 'evening' as Slot, occasion: 'wedding' as Occasion, customerId: '', guests: '', plateRate: '', hallRent: '', deposit: '', note: '', hold: false })
  const [message, setMessage] = useState('')
  const estimate = (Number(d.hallRent) || 0) + (Number(d.guests) || 0) * (Number(d.plateRate) || 0)
  const select = 'h-12 w-full rounded-xl border border-line bg-white px-3'
  const set = (key: keyof typeof d) => (e: { target: { value: string } }) => setD({ ...d, [key]: e.target.value })

  async function save() {
    setMessage('')
    const id = crypto.randomUUID()
    try {
      await api('/v1/bookings', 'POST', {
        id, venueId: d.venueId, date: d.date, slot: d.slot, occasion: d.occasion, customerId: d.customerId || null, guests: Number(d.guests) || 0,
        plateRate: Number(d.plateRate) || 0, hallRent: Number(d.hallRent) || 0, deposit: Number(d.deposit) || 0, note: d.note.trim() || null, hold: d.hold,
      })
      void queryClient.invalidateQueries({ queryKey: [shopId, 'bookings'] })
      onDone(id)
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  const labelled = (label: string, node: React.ReactNode) => <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{label}</span>{node}</label>
  return (
    <div className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
      <div className="grid grid-cols-2 gap-2">
        {labelled(t('bookings.hall'), <select value={d.venueId} onChange={set('venueId')} className={select}>{venues.map((v) => <option key={v.id} value={v.id}>{v.name}</option>)}</select>)}
        <Field label={t('bookings.date')} type="date" value={d.date} onChange={set('date')} />
        {labelled(t('bookings.slotLabel'), <select value={d.slot} onChange={set('slot')} className={select}>{SLOTS.map((s) => <option key={s} value={s}>{t(`bookings.slot.${s}`)}</option>)}</select>)}
        {labelled(t('bookings.occasionLabel'), <select value={d.occasion} onChange={set('occasion')} className={select}>{OCCASIONS.map((o) => <option key={o} value={o}>{t(`bookings.occasion.${o}`)}</option>)}</select>)}
      </div>
      {labelled(t('jobs.customer'), (
        <select value={d.customerId} onChange={set('customerId')} className={select}>
          <option value="">{t('bookings.noCustomer')}</option>
          {customers.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
      ))}
      <div className="grid grid-cols-2 gap-2">
        <Field label={t('bookings.guaranteed')} value={d.guests} onChange={set('guests')} inputMode="numeric" />
        <Field label={t('bookings.plateRate')} value={d.plateRate} onChange={set('plateRate')} inputMode="decimal" />
        <Field label={t('bookings.hallRent')} value={d.hallRent} onChange={set('hallRent')} inputMode="decimal" />
        <Field label={t('bookings.deposit')} value={d.deposit} onChange={set('deposit')} inputMode="decimal" />
      </div>
      <Field label={t('bookings.note')} value={d.note} onChange={set('note')} placeholder={t('bookings.noteHint')} />
      <p className="font-bold">{t('bookings.estimate', { amount: formatRupees(estimate) })}</p>
      <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={d.hold} onChange={(e) => setD({ ...d, hold: e.target.checked })} className="h-5 w-5" />{t('bookings.holdOnly')}</label>
      {message && <p className="text-sm text-due">{message}</p>}
      <div className="grid grid-cols-[1fr_2fr] gap-2">
        <Button tone="plain" onClick={() => onDone()}>{t('cancel')}</Button>
        <Button disabled={!d.venueId || !d.date || estimate <= 0 || (!d.hold && !d.customerId)} onClick={() => void save()}>{t(d.hold ? 'bookings.saveHold' : 'bookings.saveBooking')}</Button>
      </div>
    </div>
  )
}

function BookingPage({ shopId, id, isOwner, onBack }: { shopId: string; id: string; isOwner: boolean; onBack: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const booking = useQuery({ queryKey: [shopId, 'booking', id], queryFn: () => api<Booking>(`/v1/bookings/${id}`) }).data
  const [form, setForm] = useState<'deposit' | 'settle' | 'cancel' | null>(null)
  const [f, setF] = useState({ amount: '', guests: '', extras: [{ name: '', amount: '' }], damage: '', paid: '', mode: 'cash', reason: '', keep: '' })
  const [message, setMessage] = useState('')
  if (!booking) return <p className="p-4 text-muted">…</p>

  async function run(path: string, body: unknown) {
    setMessage('')
    try {
      await api(`/v1/bookings/${id}${path}`, 'POST', body)
      setForm(null)
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  const b = booking
  const plates = Math.max(Number(f.guests) || 0, b.guests)
  const extrasTotal = f.extras.reduce((s, e) => s + (Number(e.amount) || 0), 0)
  const finalBill = b.hallRent + plates * b.plateRate + extrasTotal + (Number(f.damage) || 0)
  const line = (label: string, value: string, strong = false) => <div className={`flex justify-between ${strong ? 'font-bold' : ''}`}><span>{label}</span><span>{value}</span></div>

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <button onClick={onBack} className="min-h-11 self-start font-semibold text-teal-700">← {t('bookings.all')}</button>
      <div className="rounded-[18px] bg-white p-4">
        <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${STATUS_TONE[b.status]}`}>{t(`bookings.status.${b.status}`)}</span>
        <h2 className="mt-1 text-lg font-bold">#{b.number} {t(`bookings.occasion.${b.occasion}`)} · {b.customerName ?? '—'}</h2>
        <p className="text-sm text-muted">{b.venueName} · {new Date(`${b.date}T00:00`).toLocaleDateString('en-IN', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })} · {t(`bookings.slot.${b.slot}`)}</p>
        {b.status === 'hold' && b.holdUntil && <p className="text-sm font-semibold text-amber-800">{t('bookings.heldUntil', { date: new Date(b.holdUntil).toLocaleString('en-IN', { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' }) })}</p>}
        <div className="mt-3 flex flex-col gap-1 text-sm">
          {line(t('bookings.hallRent'), formatRupees(b.hallRent))}
          {line(t('bookings.platesLine', { count: b.guests, rate: formatRupees(b.plateRate) }), formatRupees(b.guests * b.plateRate))}
          {line(t('bookings.estimateLabel'), formatRupees(b.estimate), true)}
          {line(t('jobs.advance'), formatRupees(b.advance))}
          {b.deposit > 0 && line(t('bookings.depositLine'), `${formatRupees(b.depositTaken)} / ${formatRupees(b.deposit)}`)}
          {b.depositReturned != null && line(t('bookings.depositBack'), formatRupees(b.depositReturned))}
          {b.actualGuests != null && line(t('bookings.came'), String(b.actualGuests))}
        </div>
        {b.note && <p className="mt-2 text-sm">{b.note}</p>}
        {b.cancelReason && <p className="mt-2 text-sm text-due">{b.cancelReason}</p>}
      </div>

      {(b.status === 'hold' || b.status === 'expired') && (
        <Button disabled={!b.customerId} onClick={() => void run('/confirm', {})}>{t(b.customerId ? 'bookings.confirm' : 'bookings.confirmNeedsCustomer')}</Button>
      )}
      {b.status === 'confirmed' && (
        <div className="grid grid-cols-2 gap-2">
          {b.deposit > b.depositTaken && <Button tone="plain" onClick={() => setForm('deposit')}>{t('bookings.takeDeposit')}</Button>}
          {isOwner && <Button onClick={() => { setForm('settle'); setF({ ...f, guests: String(b.guests) }) }}>{t('bookings.settle')}</Button>}
        </div>
      )}

      {form === 'deposit' && (
        <div className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
          <h3 className="font-bold">{t('bookings.takeDeposit')}</h3>
          <p className="text-xs text-muted">{t('bookings.depositHint')}</p>
          <Field label={t('jobs.amount')} value={f.amount} onChange={(e) => setF({ ...f, amount: e.target.value })} inputMode="decimal" />
          <Button disabled={!(Number(f.amount) > 0)} onClick={() => void run('/deposit', { amount: Number(f.amount) })}>{t('save')}</Button>
        </div>
      )}

      {form === 'settle' && (
        <div className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
          <h3 className="font-bold">{t('bookings.settle')}</h3>
          <Field label={t('bookings.cameField')} value={f.guests} onChange={(e) => setF({ ...f, guests: e.target.value })} inputMode="numeric" />
          <p className="text-xs text-muted">{t('bookings.guaranteeHint', { count: b.guests })}</p>
          {f.extras.map((x, i) => (
            <div key={i} className="grid grid-cols-[2fr_1fr] gap-2">
              <Field label={t('bookings.extra')} value={x.name} onChange={(e) => setF({ ...f, extras: f.extras.map((y, j) => (j === i ? { ...y, name: e.target.value } : y)) })} placeholder={t('bookings.extraHint')} />
              <Field label={t('jobs.amount')} value={x.amount} onChange={(e) => setF({ ...f, extras: f.extras.map((y, j) => (j === i ? { ...y, amount: e.target.value } : y)) })} inputMode="decimal" />
            </div>
          ))}
          <button onClick={() => setF({ ...f, extras: [...f.extras, { name: '', amount: '' }] })} className="min-h-11 self-start text-sm font-semibold text-teal-700">+ {t('bookings.addExtra')}</button>
          <div className="grid grid-cols-2 gap-2">
            <Field label={t('bookings.damage')} value={f.damage} onChange={(e) => setF({ ...f, damage: e.target.value })} inputMode="decimal" />
            <Field label={t('jobs.paidNow')} value={f.paid} onChange={(e) => setF({ ...f, paid: e.target.value })} inputMode="decimal" />
          </div>
          <p className="font-bold">{t('bookings.finalBill', { amount: formatRupees(finalBill) })}</p>
          <p className="text-xs text-muted">{t('bookings.settleHint')}</p>
          {message && <p className="text-sm text-due">{message}</p>}
          <Button onClick={() => void run('/settle', {
            billId: crypto.randomUUID(), actualGuests: Number(f.guests) || 0, damage: Number(f.damage) || 0, paidAmount: Number(f.paid) || 0, mode: f.mode,
            extras: f.extras.filter((x) => x.name.trim() && Number(x.amount) > 0).map((x) => ({ name: x.name.trim(), amount: Number(x.amount) })),
          })}>{t('bookings.makeBill')}</Button>
        </div>
      )}

      {b.status === 'confirmed' && b.jobId && (
        <section className="flex flex-col gap-2">
          <h3 className="font-bold">{t('bookings.moneyAndCosts')}</h3>
          <JobPage shopId={shopId} id={b.jobId} embedded />
        </section>
      )}

      {isOwner && (b.status === 'hold' || b.status === 'expired' || b.status === 'confirmed') && (form === 'cancel' ? (
        <div className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
          <h3 className="font-bold">{t('bookings.cancel')}</h3>
          <Field label={t('bookings.reason')} value={f.reason} onChange={(e) => setF({ ...f, reason: e.target.value })} />
          {b.advance > 0 && <Field label={t('bookings.keep', { advance: formatRupees(b.advance) })} value={f.keep} onChange={(e) => setF({ ...f, keep: e.target.value })} inputMode="decimal" />}
          <p className="text-xs text-muted">{t('bookings.cancelHint')}</p>
          {message && <p className="text-sm text-due">{message}</p>}
          <Button tone="credit" onClick={() => void run('/cancel', { reason: f.reason.trim() || null, keep: Number(f.keep) || 0 })}>{t('bookings.cancel')}</Button>
        </div>
      ) : <button onClick={() => setForm('cancel')} className="min-h-11 text-sm font-semibold text-due">{t('bookings.cancel')}</button>)}
      {!form && message && <p className="text-sm text-due">{message}</p>}
    </div>
  )
}

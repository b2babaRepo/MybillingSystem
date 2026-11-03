import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { useCustomers, useProducts } from '../lib/data'
import { formatRupees, whatsAppLink } from '../lib/format'

type Status = 'booked' | 'arrived' | 'done' | 'no_show' | 'cancelled'
interface Appointment {
  id: string; number: number; date: string; start: string; end: string; stylistId: string | null; stylistName: string | null; customerId: string | null
  name: string; phone: string | null; status: Status; note: string | null; billId: string | null; services: { productId: string; name: string; price: number; minutes: number }[]; total: number
}
interface Day { date: string; stylists: { id: string; name: string }[]; appointments: Appointment[] }

const isoDay = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
const time12 = (t: string) => new Date(`2000-01-01T${t}`).toLocaleTimeString('en-IN', { hour: 'numeric', minute: '2-digit' })
const TONE: Record<Status, string> = { booked: 'border-teal-200 bg-white', arrived: 'border-amber-300 bg-amber-50', done: 'border-line bg-slate-50 text-muted', no_show: 'border-line bg-slate-50 text-muted line-through', cancelled: 'border-line bg-slate-50 text-muted line-through' }

/** The salon's day: a column for each stylist (and "any stylist"), each appointment by time, from booking to bill. */
export function AppointmentsScreen({ shopId, shopName }: { shopId: string; shopName: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [date, setDate] = useState(isoDay(new Date()))
  const [form, setForm] = useState<false | 'book' | 'walk-in'>(false)
  const [paying, setPaying] = useState<string | null>(null)
  const [pay, setPay] = useState({ amount: '', mode: 'upi', stylistId: '', tip: '' })
  const [message, setMessage] = useState('')
  const day = useQuery({ queryKey: [shopId, 'appointments', date], queryFn: () => api<Day>(`/v1/appointments?date=${date}`) }).data

  async function run(path: string, body: unknown) {
    setMessage('')
    try {
      await api(`/v1/appointments/${path}`, 'POST', body)
      setPaying(null)
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  const shift = (n: number) => { const d = new Date(`${date}T00:00`); d.setDate(d.getDate() + n); setDate(isoDay(d)) }
  const columns = [...(day?.stylists ?? []).map((s) => ({ id: s.id as string | null, name: s.name })), { id: null, name: t('appointments.anyStylist') }]
  const reminder = (a: Appointment) => t('appointments.reminder', { name: a.name, shop: shopName, date: new Date(`${a.date}T00:00`).toLocaleDateString('en-IN', { day: 'numeric', month: 'short' }), time: time12(a.start), services: a.services.map((s) => s.name).join(', ') })

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <div className="flex items-center justify-between rounded-[18px] bg-white p-2">
        <button onClick={() => shift(-1)} aria-label={t('tables.prev')} className="h-11 w-11 text-xl">‹</button>
        <input type="date" value={date} onChange={(e) => e.target.value && setDate(e.target.value)} aria-label={t('tables.date')} className="h-11 rounded-xl border border-line px-2 font-bold" />
        <button onClick={() => shift(1)} aria-label={t('tables.next')} className="h-11 w-11 text-xl">›</button>
      </div>
      {form ? <NewAppointment shopId={shopId} date={date} walkIn={form === 'walk-in'} stylists={day?.stylists ?? []} onDone={() => setForm(false)} /> : (
        <div className="grid grid-cols-2 gap-2">
          <Button onClick={() => setForm('book')}>{t('appointments.book')}</Button>
          <Button tone="plain" onClick={() => setForm('walk-in')}>{t('appointments.walkIn')}</Button>
        </div>
      )}
      {message && <p className="text-sm text-due">{message}</p>}
      <div className="flex gap-3 overflow-x-auto pb-2">
        {columns.map((c) => {
          const list = (day?.appointments ?? []).filter((a) => a.stylistId === c.id)
          if (c.id === null && list.length === 0) return null
          return (
            <section key={c.id ?? 'any'} className="flex w-72 shrink-0 flex-col gap-2">
              <h3 className="font-bold">{c.name} <span className="text-sm font-normal text-muted">({list.filter((a) => a.status === 'booked' || a.status === 'arrived').length})</span></h3>
              {list.length === 0 && <p className="rounded-xl bg-white p-3 text-sm text-muted">{t('appointments.free')}</p>}
              {list.map((a) => (
                <article key={a.id} className={`rounded-xl border p-3 ${TONE[a.status]}`}>
                  <p className="text-sm font-bold">{time12(a.start)} – {time12(a.end)}</p>
                  <p className="font-semibold">{a.name}</p>
                  <p className="text-xs">{a.services.map((s) => s.name).join(' + ')} · {formatRupees(a.total)}</p>
                  {a.note && <p className="text-xs">{a.note}</p>}
                  <p className="text-xs font-semibold">{t(`appointments.status.${a.status}`)}</p>
                  {(a.status === 'booked' || a.status === 'arrived') && (
                    <div className="mt-2 flex flex-wrap gap-2">
                      {a.status === 'booked' && <Button className="h-10 px-3 text-sm" onClick={() => void run(`${a.id}/status`, { status: 'arrived' })}>{t('appointments.arrived')}</Button>}
                      <Button className="h-10 px-3 text-sm" tone={a.status === 'arrived' ? 'primary' : 'plain'} onClick={() => { setPaying(a.id); setPay({ amount: String(a.total), mode: 'upi', stylistId: '', tip: '' }) }}>{t('appointments.bill')}</Button>
                      {a.status === 'booked' && <button onClick={() => void run(`${a.id}/status`, { status: 'no_show' })} className="h-10 px-2 text-sm font-semibold">{t('appointments.noShow')}</button>}
                      {a.phone && a.status === 'booked' && <a href={whatsAppLink(a.phone, reminder(a))} target="_blank" rel="noreferrer" className="flex h-10 items-center rounded-xl bg-[#15803d] px-3 text-sm font-semibold text-white">{t('tables.remind')}</a>}
                      <button onClick={() => { if (confirm(t('appointments.cancelConfirm'))) void run(`${a.id}/status`, { status: 'cancelled' }) }} className="h-10 px-2 text-sm font-semibold text-due">{t('cancel')}</button>
                    </div>
                  )}
                  {paying === a.id && (
                    <div className="mt-2 grid grid-cols-[1fr_1fr_auto] gap-2">
                      {!a.stylistId && (
                        <select value={pay.stylistId} onChange={(e) => setPay({ ...pay, stylistId: e.target.value })} aria-label={t('appointments.whoDid')} className="col-span-3 h-10 rounded-xl border border-line bg-white px-2">
                          <option value="">{t('appointments.whoDid')}</option>{(day?.stylists ?? []).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
                        </select>
                      )}
                      <input value={pay.tip} onChange={(e) => setPay({ ...pay, tip: e.target.value })} inputMode="decimal" placeholder={t('appointments.tip')} aria-label={t('appointments.tip')} className="col-span-3 h-10 rounded-xl border border-line px-2" />
                      <input value={pay.amount} onChange={(e) => setPay({ ...pay, amount: e.target.value })} inputMode="decimal" aria-label={t('jobs.paidNow')} className="h-10 rounded-xl border border-line px-2" />
                      <select value={pay.mode} onChange={(e) => setPay({ ...pay, mode: e.target.value })} aria-label={t('jobs.mode')} className="h-10 rounded-xl border border-line bg-white px-2"><option value="upi">UPI</option><option value="cash">{t('cash')}</option></select>
                      <Button className="h-10 px-3" onClick={() => void run(`${a.id}/bill`, { billId: crypto.randomUUID(), paidAmount: Number(pay.amount) || 0, mode: pay.mode, stylistId: pay.stylistId || null, tip: Number(pay.tip) || 0 })}>{t('save')}</Button>
                      {!a.customerId && Number(pay.amount) < a.total && <p className="col-span-3 text-xs text-due">{t('appointments.payAll')}</p>}
                    </div>
                  )}
                </article>
              ))}
            </section>
          )
        })}
      </div>
    </div>
  )
}

function NewAppointment({ shopId, date, walkIn, stylists, onDone }: { shopId: string; date: string; walkIn: boolean; stylists: { id: string; name: string }[]; onDone: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { customers } = useCustomers(shopId)
  const services = useProducts(shopId).products.filter((p) => p.isService)
  const [d, setD] = useState({ date, start: '11:00', stylistId: '', customerId: '', name: '', phone: '', note: '' })
  const [chosen, setChosen] = useState<string[]>([])
  const [message, setMessage] = useState('')
  const set = (key: keyof typeof d) => (e: { target: { value: string } }) => setD({ ...d, [key]: e.target.value })
  const picked = services.filter((s) => chosen.includes(s.id))
  const minutes = picked.reduce((m, s) => m + (s.durationMinutes ?? 30), 0)
  const total = picked.reduce((m, s) => m + s.sellingPrice, 0)
  const select = 'h-12 w-full rounded-xl border border-line bg-white px-3'

  async function save() {
    setMessage('')
    try {
      await api('/v1/appointments', 'POST', {
        id: crypto.randomUUID(), date: d.date, start: walkIn ? null : d.start, walkIn, stylistId: d.stylistId || null, customerId: d.customerId || null,
        name: d.name.trim() || null, phone: d.phone.trim() || null, serviceIds: chosen, note: d.note.trim() || null,
      })
      void queryClient.invalidateQueries({ queryKey: [shopId, 'appointments'] })
      onDone()
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  return (
    <div className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
      <h3 className="font-bold">{t(walkIn ? 'appointments.walkIn' : 'appointments.book')}</h3>
      {!walkIn && <div className="grid grid-cols-2 gap-2"><Field label={t('tables.date')} type="date" value={d.date} onChange={set('date')} /><Field label={t('tables.time')} type="time" value={d.start} onChange={set('start')} /></div>}
      <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('appointments.stylist')}</span>
        <select value={d.stylistId} onChange={set('stylistId')} className={select}><option value="">{t('appointments.anyStylist')}</option>{stylists.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select>
      </label>
      <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('jobs.customer')}</span>
        <select value={d.customerId} onChange={set('customerId')} className={select}><option value="">{t('tables.notOnKhata')}</option>{customers.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}</select>
      </label>
      {!d.customerId && <div className="grid grid-cols-2 gap-2"><Field label={t('tables.name')} value={d.name} onChange={set('name')} placeholder={walkIn ? t('appointments.walkInName') : ''} /><Field label={t('tables.phone')} value={d.phone} onChange={set('phone')} inputMode="tel" /></div>}
      <p className="text-sm font-semibold text-muted">{t('appointments.services')}</p>
      {services.length === 0 && <p className="text-sm text-due">{t('appointments.noServices')}</p>}
      <div className="flex flex-wrap gap-2">
        {services.map((s) => (
          <button key={s.id} type="button" onClick={() => setChosen((c) => (c.includes(s.id) ? c.filter((x) => x !== s.id) : [...c, s.id]))}
            className={`min-h-11 rounded-full px-3 text-sm font-semibold ${chosen.includes(s.id) ? 'bg-teal-700 text-white' : 'border border-line bg-white'}`}>
            {s.name}{s.sellingPrice > 0 ? ` · ${formatRupees(s.sellingPrice)}` : ''}
          </button>
        ))}
      </div>
      {picked.length > 0 && <p className="font-bold">{t('appointments.summary', { minutes, amount: formatRupees(total) })}</p>}
      <Field label={t('bookings.note')} value={d.note} onChange={set('note')} />
      {message && <p className="text-sm text-due">{message}</p>}
      <div className="grid grid-cols-[1fr_2fr] gap-2">
        <Button tone="plain" onClick={onDone}>{t('cancel')}</Button>
        <Button disabled={chosen.length === 0 || (!walkIn && (!d.date || !d.start)) || (!walkIn && !d.customerId && d.name.trim().length < 2)} onClick={() => void save()}>{t(walkIn ? 'appointments.startNow' : 'appointments.book')}</Button>
      </div>
    </div>
  )
}

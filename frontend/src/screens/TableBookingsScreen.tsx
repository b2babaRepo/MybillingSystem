import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { btn } from '../lib/buttonStyles'
import { api } from '../lib/api'
import { useCustomers } from '../lib/data'
import { formatPhone, formatRupees, whatsAppLink } from '../lib/format'
import { confirmAsk } from '../lib/ask'

type Occasion = 'none' | 'birthday' | 'anniversary' | 'other'
type Status = 'booked' | 'seated' | 'no_show' | 'cancelled'
interface TableBooking {
  id: string; number: number; date: string; time: string; people: number; customerId: string | null; name: string; phone: string | null
  occasion: Occasion; cake: string | null; decoration: boolean; table: string | null; note: string | null; status: Status; deposit: number
}
interface Day { date: string; people: number; bookings: TableBooking[] }

const OCCASIONS: Occasion[] = ['none', 'birthday', 'anniversary', 'other']
const isoDay = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
const TONE: Record<Status, string> = { booked: 'bg-teal-100 text-brand-deep', seated: 'bg-slate-200 text-ink', no_show: 'bg-amber-100 text-warn-ink', cancelled: 'bg-due-soft text-due' }
const ICON: Record<Occasion, string> = { none: '', birthday: '🎂 ', anniversary: '💐 ', other: '🎉 ' }

/** A day's table bookings by time: family dinners, and birthday or anniversary parties with cake and decoration. */
export function TableBookingsScreen({ shopId, shopName }: { shopId: string; shopName: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [date, setDate] = useState(() => isoDay(new Date()))
  const [adding, setAdding] = useState(false)
  const [depositFor, setDepositFor] = useState<string | null>(null)
  const [deposit, setDeposit] = useState({ amount: '', mode: 'upi' })
  const [message, setMessage] = useState('')
  const day = useQuery({ queryKey: [shopId, 'table-bookings', date], queryFn: () => api<Day>(`/v1/table-bookings?date=${date}`) }).data

  async function run(path: string, body: unknown) {
    setMessage('')
    try {
      await api(`/v1/table-bookings/${path}`, 'POST', body)
      setDepositFor(null)
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  const shift = (n: number) => { const d = new Date(`${date}T00:00`); d.setDate(d.getDate() + n); setDate(isoDay(d)) }
  const time12 = (time: string) => new Date(`2000-01-01T${time}`).toLocaleTimeString(shownLocale(), { hour: 'numeric', minute: '2-digit' })
  const reminder = (b: TableBooking) => t('tables.reminder', { name: b.name, shop: shopName, date: new Date(`${b.date}T00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' }), time: time12(b.time), people: b.people })

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <p className="text-sm text-muted">{t('tables.hint')}</p>
      <div className="flex items-center justify-between rounded-card bg-white p-2">
        <button onClick={() => shift(-1)} aria-label={t('tables.prev')} className="h-11 w-11 text-xl">‹</button>
        <input type="date" value={date} onChange={(e) => e.target.value && setDate(e.target.value)} aria-label={t('tables.date')} className="h-11 rounded-xl border border-line px-2 font-bold" />
        <button onClick={() => shift(1)} aria-label={t('tables.next')} className="h-11 w-11 text-xl">›</button>
      </div>
      {day && <p className="font-semibold">{t('tables.expected', { count: day.people })}</p>}
      {adding ? <NewTableBooking shopId={shopId} date={date} onDone={() => setAdding(false)} /> : <Button onClick={() => setAdding(true)}>{t('tables.new')}</Button>}
      {message && <p className="text-sm text-due">{message}</p>}
      {day?.bookings.length === 0 && <p className="rounded-card bg-white p-4 text-center text-muted">{t('tables.none')}</p>}
      <ul className="flex flex-col gap-2">
        {day?.bookings.map((b) => (
          <li key={b.id} className="rounded-card bg-white p-3">
            <div className="flex items-start gap-2">
              <span className="w-16 shrink-0 font-bold">{time12(b.time)}</span>
              <span className="min-w-0 flex-1">
                <span className="block font-semibold">{ICON[b.occasion]}{b.name} · {t('tables.people', { count: b.people })}</span>
                <span className="block text-xs text-muted">
                  {[b.table, b.phone && formatPhone(b.phone), b.occasion !== 'none' && t(`tables.occasion.${b.occasion}`), b.cake && `${t('tables.cake')}: ${b.cake}`, b.decoration && t('tables.decoration'), b.note].filter(Boolean).join(' · ')}
                </span>
                {b.deposit > 0 && <span className="block text-xs font-semibold text-brand-strong">{t('tables.depositTaken', { amount: formatRupees(b.deposit) })}</span>}
              </span>
              <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-semibold ${TONE[b.status]}`}>{t(`tables.status.${b.status}`)}</span>
            </div>
            {b.status === 'booked' && (
              <div className="mt-2 flex flex-wrap gap-2">
                <Button className="h-11 px-3 text-sm" onClick={() => void run(`${b.id}/status`, { status: 'seated' })}>{t('tables.seated')}</Button>
                <Button tone="plain" className="h-11 px-3 text-sm" onClick={() => void run(`${b.id}/status`, { status: 'no_show' })}>{t('tables.noShow')}</Button>
                {b.customerId && <Button tone="plain" className="h-11 px-3 text-sm" onClick={() => setDepositFor(depositFor === b.id ? null : b.id)}>{t('tables.takeDeposit')}</Button>}
                {b.phone && <a href={whatsAppLink(b.phone, reminder(b))} target="_blank" rel="noreferrer" className="flex h-11 items-center rounded-xl bg-[#15803d] px-3 text-sm font-semibold text-white">{t('tables.remind')}</a>}
                <button onClick={async () => { if ((await confirmAsk(t('tables.cancelConfirm')))) void run(`${b.id}/status`, { status: 'cancelled' }) }} className={`h-11 text-sm ${btn.dangerOutline}`}>{t('tables.cancel')}</button>
              </div>
            )}
            {depositFor === b.id && (
              <div className="mt-2 grid grid-cols-[1fr_1fr_auto] gap-2">
                <input value={deposit.amount} onChange={(e) => setDeposit({ ...deposit, amount: e.target.value })} inputMode="decimal" placeholder="₹" aria-label={t('jobs.amount')} className="h-11 rounded-xl border border-line px-3" />
                <select value={deposit.mode} onChange={(e) => setDeposit({ ...deposit, mode: e.target.value })} aria-label={t('jobs.mode')} className="h-11 rounded-xl border border-line bg-white px-2">
                  <option value="upi">UPI</option><option value="cash">{t('cash')}</option>
                </select>
                <Button className="h-11 px-3" disabled={!(Number(deposit.amount) > 0)} onClick={() => void run(`${b.id}/deposit`, { amount: Number(deposit.amount), mode: deposit.mode })}>{t('save')}</Button>
              </div>
            )}
          </li>
        ))}
      </ul>
    </div>
  )
}

function NewTableBooking({ shopId, date, onDone }: { shopId: string; date: string; onDone: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { customers } = useCustomers(shopId)
  const [d, setD] = useState({ date, time: '20:00', people: '', customerId: '', name: '', phone: '', occasion: 'none' as Occasion, cake: '', decoration: false, table: '', note: '' })
  const [message, setMessage] = useState('')
  const set = (key: keyof typeof d) => (e: { target: { value: string } }) => setD({ ...d, [key]: e.target.value })
  const select = 'h-12 w-full rounded-xl border border-line bg-white px-3'
  const party = d.occasion !== 'none'

  async function save() {
    setMessage('')
    try {
      await api('/v1/table-bookings', 'POST', {
        id: crypto.randomUUID(), date: d.date, time: d.time, people: Number(d.people) || 0, customerId: d.customerId || null, name: d.name.trim() || null,
        phone: d.phone.trim() || null, occasion: d.occasion, cake: party ? d.cake.trim() || null : null, decoration: party && d.decoration, table: d.table.trim() || null, note: d.note.trim() || null,
      })
      void queryClient.invalidateQueries({ queryKey: [shopId, 'table-bookings'] })
      onDone()
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  return (
    <div className="flex flex-col gap-2 rounded-card bg-white p-3">
      <div className="grid grid-cols-3 gap-2">
        <Field label={t('tables.date')} type="date" value={d.date} onChange={set('date')} />
        <Field label={t('tables.time')} type="time" value={d.time} onChange={set('time')} />
        <Field label={t('tables.peopleField')} value={d.people} onChange={set('people')} inputMode="numeric" />
      </div>
      <label className="block">
        <span className="mb-1 block text-sm font-semibold text-muted">{t('jobs.customer')}</span>
        <select value={d.customerId} onChange={set('customerId')} className={select}>
          <option value="">{t('tables.notOnKhata')}</option>
          {customers.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
      </label>
      {!d.customerId && (
        <div className="grid grid-cols-2 gap-2">
          <Field label={t('tables.name')} value={d.name} onChange={set('name')} />
          <Field label={t('tables.phone')} value={d.phone} onChange={set('phone')} inputMode="tel" />
        </div>
      )}
      <div className="grid grid-cols-2 gap-2">
        <label className="block">
          <span className="mb-1 block text-sm font-semibold text-muted">{t('tables.occasionLabel')}</span>
          <select value={d.occasion} onChange={set('occasion')} className={select}>{OCCASIONS.map((o) => <option key={o} value={o}>{t(`tables.occasion.${o}`)}</option>)}</select>
        </label>
        <Field label={t('tables.table')} value={d.table} onChange={set('table')} placeholder={t('tables.tableHint')} />
      </div>
      {party && (
        <>
          <Field label={t('tables.cake')} value={d.cake} onChange={set('cake')} placeholder={t('tables.cakeHint')} />
          <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={d.decoration} onChange={(e) => setD({ ...d, decoration: e.target.checked })} className="h-5 w-5" />{t('tables.decoration')}</label>
        </>
      )}
      <Field label={t('bookings.note')} value={d.note} onChange={set('note')} />
      {message && <p className="text-sm text-due">{message}</p>}
      <div className="grid grid-cols-[1fr_2fr] gap-2">
        <Button tone="plain" onClick={onDone}>{t('cancel')}</Button>
        <Button disabled={!d.date || !d.time || !(Number(d.people) > 0) || (!d.customerId && d.name.trim().length < 2)} onClick={() => void save()}>{t('tables.book')}</Button>
      </div>
    </div>
  )
}

import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { btn } from '../lib/buttonStyles'
import { api } from '../lib/api'
import { useCustomers } from '../lib/data'
import { formatPhone, formatRupees } from '../lib/format'
import { navigate } from '../router'
import { confirmAsk } from '../lib/ask'
import { useNow } from '../lib/useNow'

type Source = 'direct' | 'walk_in' | 'make_my_trip' | 'goibibo' | 'booking_com' | 'oyo' | 'airbnb' | 'other'
type Status = 'booked' | 'checked_in' | 'checked_out' | 'cancelled'
interface Room { id: string; name: string; kind: string | null; rate: number; icalImportUrl?: string | null; icalExportPath?: string | null }
interface RatePeriod { id: string; name: string; from: string; to: string; roomId: string | null; rate: number }
interface Stay {
  id: string; number: number; roomId: string; roomName: string; checkIn: string; checkOut: string; nights: number; status: Status; customerId: string | null
  guestName: string; phone: string | null; guests: number; idType: string | null; idLast4: string | null; foreigner: boolean; formCFiledAt: string | null; formCDue: boolean
  rate: number; source: Source; sourceRef: string | null; commission: number; roomTotal: number; charges: { id: string; name: string; amount: number }[]; total: number
  billId: string | null; note: string | null; bookingId?: string | null
}
interface BoardRow { room: Room; state: 'free' | 'arriving' | 'occupied' | 'leaving'; stay: Stay | null }

const SOURCES: Source[] = ['direct', 'walk_in', 'make_my_trip', 'goibibo', 'booking_com', 'oyo', 'airbnb', 'other']
const STATE_TONE: Record<BoardRow['state'], string> = { free: 'bg-white', arriving: 'bg-warn-soft', occupied: 'bg-brand-soft', leaving: 'bg-due-soft' }
const day = (iso: string) => new Date(`${iso}T00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })
const todayIso = () => { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}` }

/** Rooms today (free, arriving, guest in, leaving), the coming stays, and each stay from booking to checkout. */
export function NewStayInline(props: { shopId: string; bookingId: string; date: string; onDone: () => void }) {
  const rooms = useQuery({ queryKey: [props.shopId, 'rooms'], queryFn: () => api<Room[]>('/v1/rooms') }).data ?? []
  if (rooms.length === 0) return null
  return <NewStay shopId={props.shopId} rooms={rooms} roomId={null} bookingId={props.bookingId} startDate={props.date} onDone={() => props.onDone()} />
}

export function RoomsScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const [openId, setOpenId] = useState<string | null>(null)
  const [adding, setAdding] = useState<string | null | false>(false)
  const rooms = useQuery({ queryKey: [shopId, 'rooms'], queryFn: () => api<Room[]>('/v1/rooms') }).data ?? []
  const board = useQuery({ queryKey: [shopId, 'rooms', 'board'], queryFn: () => api<BoardRow[]>('/v1/rooms/board') }).data ?? []
  const stays = useQuery({ queryKey: [shopId, 'stays'], queryFn: () => api<Stay[]>('/v1/stays') }).data ?? []

  if (openId) return <StayPage shopId={shopId} id={openId} onBack={() => setOpenId(null)} />
  const formCDue = stays.filter((s) => s.formCDue)
  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <p className="text-sm text-muted">{t('rooms.hint')}</p>
      {rooms.length === 0 ? (isOwner ? <AddRoom shopId={shopId} first /> : <p className="text-muted">{t('rooms.noRooms')}</p>) : (
        <>
          {formCDue.map((s) => (
            <button key={s.id} onClick={() => setOpenId(s.id)} className="rounded-xl bg-due-soft p-3 text-left text-sm font-semibold text-due">{t('rooms.formCDue', { name: s.guestName, room: s.roomName })} →</button>
          ))}
          <h3 className="font-bold">{t('rooms.today')}</h3>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
            {board.map((r) => (
              <button key={r.room.id} onClick={() => (r.stay ? setOpenId(r.stay.id) : setAdding(r.room.id))} className={`flex min-h-20 flex-col rounded-card border border-line p-3 text-left ${STATE_TONE[r.state]}`}>
                <span className="font-bold">{r.room.name}{r.room.kind ? ` · ${r.room.kind}` : ''}</span>
                <span className="text-xs font-semibold">{t(`rooms.state.${r.state}`)}</span>
                <span className="truncate text-xs text-muted">{r.stay ? `${r.stay.guestName} · ${t('rooms.until', { date: day(r.stay.checkOut) })}` : formatRupees(r.room.rate)}</span>
              </button>
            ))}
          </div>
          {adding !== false ? <NewStay shopId={shopId} rooms={rooms} roomId={adding} onDone={(id) => { setAdding(false); if (id) setOpenId(id) }} /> : <Button onClick={() => setAdding(null)}>{t('rooms.newStay')}</Button>}
          <h3 className="font-bold">{t('rooms.coming')}</h3>
          {stays.filter((s) => s.status === 'booked').length === 0 && <p className="rounded-card bg-white p-3 text-center text-muted">{t('rooms.noneComing')}</p>}
          <ul className="flex flex-col gap-1">
            {stays.filter((s) => s.status === 'booked').map((s) => (
              <li key={s.id}>
                <button onClick={() => setOpenId(s.id)} className="flex min-h-12 w-full items-center gap-2 rounded-xl bg-white px-3 text-left">
                  <span className="min-w-0 flex-1"><span className="block font-semibold">{s.guestName} · {s.roomName}</span><span className="block text-xs text-muted">{day(s.checkIn)} → {day(s.checkOut)} · {t('rooms.nights', { count: s.nights })} · {t(`rooms.source.${s.source}`)}</span></span>
                  <span className="text-sm font-bold">{formatRupees(s.roomTotal)}</span>
                </button>
              </li>
            ))}
          </ul>
          {isOwner && <AddRoom shopId={shopId} />}
          {isOwner && <SeasonPrices shopId={shopId} rooms={rooms} />}
          {isOwner && <Calendars shopId={shopId} />}
        </>
      )}
    </div>
  )
}

function AddRoom({ shopId, first = false }: { shopId: string; first?: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [d, setD] = useState({ name: '', kind: '', rate: '' })
  const [open, setOpen] = useState(first)
  const [message, setMessage] = useState('')
  if (!open) return <button onClick={() => setOpen(true)} className={`min-h-11 text-sm self-start ${btn.add}`}>+ {t('rooms.addRoom')}</button>
  const save = () => api('/v1/rooms', 'POST', { id: crypto.randomUUID(), name: d.name.trim(), kind: d.kind.trim() || null, rate: Number(d.rate) || 0 })
    .then(() => { setD({ name: '', kind: '', rate: '' }); setMessage(t('saved')); void queryClient.invalidateQueries({ queryKey: [shopId, 'rooms'] }) })
    .catch((e: unknown) => setMessage(e instanceof Error ? e.message : t('error')))
  return (
    <div className="flex flex-col gap-2 rounded-card bg-white p-3">
      <h3 className="font-bold">{t(first ? 'rooms.firstRoom' : 'rooms.addRoom')}</h3>
      <div className="grid grid-cols-3 gap-2">
        <Field label={t('rooms.roomName')} value={d.name} onChange={(e) => setD({ ...d, name: e.target.value })} placeholder="101" />
        <Field label={t('rooms.kind')} value={d.kind} onChange={(e) => setD({ ...d, kind: e.target.value })} placeholder={t('rooms.kindHint')} />
        <Field label={t('rooms.rate')} value={d.rate} onChange={(e) => setD({ ...d, rate: e.target.value })} inputMode="decimal" />
      </div>
      {message && <p className="text-sm text-muted">{message}</p>}
      <Button disabled={!d.name.trim() || !(Number(d.rate) > 0)} onClick={() => void save()}>{t('save')}</Button>
      {Number(d.rate) > 7500 && <p className="text-xs font-semibold text-due">{t('rooms.gstHint')}</p>}
    </div>
  )
}

function NewStay({ shopId, rooms, roomId, onDone, bookingId, startDate }: { shopId: string; rooms: Room[]; roomId: string | null; onDone: (id?: string) => void; bookingId?: string; startDate?: string }) {
  const nowMs = useNow()
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { customers } = useCustomers(shopId)
  const tomorrow = (() => { const d = new Date(nowMs); d.setDate(d.getDate() + 1); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}` })()
  const firstRoom = rooms.find((r) => r.id === roomId) ?? rooms[0]
  const next = (iso: string) => { const x = new Date(`${iso}T00:00`); x.setDate(x.getDate() + 1); return `${x.getFullYear()}-${String(x.getMonth() + 1).padStart(2, '0')}-${String(x.getDate()).padStart(2, '0')}` }
  const [d, setD] = useState({ roomId: firstRoom?.id ?? '', checkIn: startDate ?? todayIso(), checkOut: startDate ? next(startDate) : tomorrow, customerId: '', guestName: '', phone: '', guests: '2', rate: '', source: 'direct' as Source, sourceRef: '', commission: '', foreigner: false, note: '' })
  const [message, setMessage] = useState('')
  const set = (key: keyof typeof d) => (e: { target: { value: string } }) => setD({ ...d, [key]: e.target.value })
  const select = 'h-12 w-full rounded-xl border border-line bg-white px-3'
  const nights = Math.max(0, Math.round((new Date(`${d.checkOut}T00:00`).getTime() - new Date(`${d.checkIn}T00:00`).getTime()) / 86_400_000))
  const site = !['direct', 'walk_in'].includes(d.source)
  const quote = useQuery({ queryKey: [shopId, 'quote', d.roomId, d.checkIn, d.checkOut], queryFn: () => api<{ nights: number; total: number; nightRates: number[] }>(`/v1/rooms/${d.roomId}/quote?checkIn=${d.checkIn}&checkOut=${d.checkOut}`), enabled: !!d.roomId && nights > 0 }).data
  const seasonal = quote && new Set(quote.nightRates).size > 1
  const estimate = d.rate ? nights * Number(d.rate) : quote?.total ?? 0

  async function save() {
    setMessage('')
    const id = crypto.randomUUID()
    try {
      await api('/v1/stays', 'POST', {
        id, roomId: d.roomId, checkIn: d.checkIn, checkOut: d.checkOut, customerId: d.customerId || null, guestName: d.guestName.trim() || null, phone: d.phone.trim() || null,
        guests: Number(d.guests) || 1, rate: Number(d.rate) || null, source: d.source, bookingId: bookingId ?? null, sourceRef: site ? d.sourceRef.trim() || null : null,
        commission: site ? Number(d.commission) || 0 : 0, foreigner: d.foreigner, note: d.note.trim() || null,
      })
      void queryClient.invalidateQueries({ queryKey: [shopId] })
      onDone(id)
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  const labelled = (label: string, node: React.ReactNode) => <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{label}</span>{node}</label>
  return (
    <div className="flex flex-col gap-2 rounded-card bg-white p-3">
      <div className="grid grid-cols-3 gap-2">
        {labelled(t('rooms.room'), <select value={d.roomId} onChange={(e) => setD({ ...d, roomId: e.target.value })} className={select}>{rooms.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}</select>)}
        <Field label={t('rooms.checkIn')} type="date" value={d.checkIn} onChange={set('checkIn')} />
        <Field label={t('rooms.checkOut')} type="date" value={d.checkOut} onChange={set('checkOut')} />
      </div>
      {labelled(t('jobs.customer'), (
        <select value={d.customerId} onChange={set('customerId')} className={select}>
          <option value="">{t('tables.notOnKhata')}</option>
          {customers.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
      ))}
      {!d.customerId && <div className="grid grid-cols-2 gap-2"><Field label={t('rooms.guestName')} value={d.guestName} onChange={set('guestName')} /><Field label={t('tables.phone')} value={d.phone} onChange={set('phone')} inputMode="tel" /></div>}
      <div className="grid grid-cols-3 gap-2">
        <Field label={t('rooms.guests')} value={d.guests} onChange={set('guests')} inputMode="numeric" />
        <Field label={t('rooms.ownRate')} value={d.rate} onChange={set('rate')} inputMode="decimal" placeholder={quote ? formatRupees(Math.round(quote.total / Math.max(quote.nights, 1))) : ''} />
        {labelled(t('rooms.sourceLabel'), <select value={d.source} onChange={set('source')} className={select}>{SOURCES.map((s) => <option key={s} value={s}>{t(`rooms.source.${s}`)}</option>)}</select>)}
      </div>
      {site && <div className="grid grid-cols-2 gap-2"><Field label={t('rooms.sourceRef')} value={d.sourceRef} onChange={set('sourceRef')} /><Field label={t('rooms.commission')} value={d.commission} onChange={set('commission')} inputMode="decimal" /></div>}
      <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={d.foreigner} onChange={(e) => setD({ ...d, foreigner: e.target.checked })} className="h-5 w-5" />{t('rooms.foreigner')}</label>
      <Field label={t('bookings.note')} value={d.note} onChange={set('note')} />
      <p className="font-bold">{t('rooms.estimate', { count: nights, amount: formatRupees(estimate) })}</p>
      {seasonal && !d.rate && <p className="text-xs text-muted">{t('rooms.seasonal', { rates: quote!.nightRates.map((r) => formatRupees(r)).join(' + ') })}</p>}
      {message && <p className="text-sm text-due">{message}</p>}
      <div className="grid grid-cols-[1fr_2fr] gap-2">
        <Button tone="plain" onClick={() => onDone()}>{t('cancel')}</Button>
        <Button disabled={!d.roomId || nights < 1 || !(estimate > 0) || (!d.customerId && d.guestName.trim().length < 2)} onClick={() => void save()}>{t('rooms.book')}</Button>
      </div>
    </div>
  )
}

function StayPage({ shopId, id, onBack }: { shopId: string; id: string; onBack: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const stay = useQuery({ queryKey: [shopId, 'stay', id], queryFn: () => api<Stay>(`/v1/stays/${id}`) }).data
  const [form, setForm] = useState<'checkin' | 'charge' | 'advance' | 'checkout' | null>(null)
  const [f, setF] = useState({ idType: 'Aadhaar', idNumber: '', name: '', amount: '', mode: 'upi' })
  const [message, setMessage] = useState('')
  if (!stay) return <p className="p-4 text-muted">…</p>
  const s = stay

  async function run(path: string, body: unknown) {
    setMessage('')
    try {
      await api(`/v1/stays/${id}/${path}`, 'POST', body)
      setForm(null)
      setF({ ...f, idNumber: '', name: '', amount: '' })
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  const line = (label: string, value: string, strong = false) => <div className={`flex justify-between ${strong ? 'font-bold' : ''}`}><span>{label}</span><span>{value}</span></div>
  const live = s.status === 'booked' || s.status === 'checked_in'

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <button onClick={onBack} className="min-h-11 self-start font-semibold text-brand">← {t('rooms.all')}</button>
      <div className="rounded-card bg-white p-4">
        <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs font-semibold">{t(`rooms.status.${s.status}`)}</span>
        <h2 className="mt-1 text-lg font-bold">#{s.number} {s.guestName} · {t('rooms.roomLabel', { room: s.roomName })}</h2>
        <p className="text-sm text-muted">{day(s.checkIn)} → {day(s.checkOut)} · {t('rooms.nights', { count: s.nights })} · {t('rooms.guestsCount', { count: s.guests })} · {t(`rooms.source.${s.source}`)}{s.sourceRef ? ` (${s.sourceRef})` : ''}</p>
        {s.phone && <p className="text-sm text-muted">{formatPhone(s.phone)}</p>}
        {s.idLast4 && <p className="text-sm text-muted">{t('rooms.idSeen', { type: s.idType, last4: s.idLast4 })}</p>}
        {s.formCDue && (
          <div className="mt-2 rounded-xl bg-due-soft p-3 text-sm">
            <p className="font-semibold text-due">{t('rooms.formCText')}</p>
            <Button tone="plain" className="mt-2 h-11" onClick={() => void run('form-c', {})}>{t('rooms.formCFiled')}</Button>
          </div>
        )}
        <div className="mt-3 flex flex-col gap-1 text-sm">
          {line(t('rooms.roomLine', { count: s.nights, rate: formatRupees(s.rate) }), formatRupees(s.roomTotal))}
          {s.charges.map((c) => line(c.name, formatRupees(c.amount)))}
          {line(t('total'), formatRupees(s.total), true)}
          {s.commission > 0 && line(t('rooms.commissionLine'), formatRupees(s.commission))}
        </div>
      </div>

      {live && (
        <div className="grid grid-cols-2 gap-2">
          {s.status === 'booked' && <Button onClick={() => setForm('checkin')}>{t('rooms.checkInButton')}</Button>}
          <Button tone="plain" onClick={() => setForm('charge')}>{t('rooms.addCharge')}</Button>
          {s.customerId && <Button tone="plain" onClick={() => setForm('advance')}>{t('rooms.takeAdvance')}</Button>}
          {s.status === 'checked_in' && <Button onClick={() => { setForm('checkout'); setF({ ...f, amount: s.customerId ? '' : String(s.total) }) }}>{t('rooms.checkoutButton')}</Button>}
        </div>
      )}
      {s.billId && <Button tone="plain" onClick={() => navigate(`/print/bill/${s.billId}`)}>{t('print.viewBill')}</Button>}

      {form && (
        <div className="flex flex-col gap-2 rounded-card bg-white p-3">
          <h3 className="font-bold">{t(`rooms.form.${form}`)}</h3>
          {form === 'checkin' && (
            <>
              <div className="grid grid-cols-[1fr_2fr] gap-2">
                <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('rooms.idType')}</span>
                  <select value={f.idType} onChange={(e) => setF({ ...f, idType: e.target.value })} className="h-12 w-full rounded-xl border border-line bg-white px-3">
                    {['Aadhaar', 'Passport', 'Driving licence', 'Voter ID', 'PAN'].map((x) => <option key={x}>{x}</option>)}
                  </select>
                </label>
                <Field label={t('rooms.idNumber')} value={f.idNumber} onChange={(e) => setF({ ...f, idNumber: e.target.value })} autoComplete="off" />
              </div>
              <p className="text-xs text-muted">{t('rooms.idHint')}</p>
            </>
          )}
          {form === 'charge' && <Field label={t('rooms.chargeName')} value={f.name} onChange={(e) => setF({ ...f, name: e.target.value })} placeholder={t('rooms.chargeHint')} />}
          {(form === 'charge' || form === 'advance' || form === 'checkout') && (
            <div className="grid grid-cols-2 gap-2">
              <Field label={t(form === 'checkout' ? 'jobs.paidNow' : 'jobs.amount')} value={f.amount} onChange={(e) => setF({ ...f, amount: e.target.value })} inputMode="decimal" />
              {form !== 'charge' && (
                <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('jobs.mode')}</span>
                  <select value={f.mode} onChange={(e) => setF({ ...f, mode: e.target.value })} className="h-12 w-full rounded-xl border border-line bg-white px-3"><option value="upi">UPI</option><option value="cash">{t('cash')}</option></select>
                </label>
              )}
            </div>
          )}
          {form === 'checkout' && <p className="text-xs text-muted">{t(s.customerId ? 'rooms.checkoutKhata' : 'rooms.checkoutCash', { amount: formatRupees(s.total) })}</p>}
          {message && <p className="text-sm text-due">{message}</p>}
          <div className="grid grid-cols-[1fr_2fr] gap-2">
            <Button tone="plain" onClick={() => setForm(null)}>{t('cancel')}</Button>
            <Button onClick={() => void (form === 'checkin' ? run('check-in', { idType: f.idType, idNumber: f.idNumber })
              : form === 'charge' ? run('charges', { id: crypto.randomUUID(), name: f.name.trim(), amount: Number(f.amount) })
              : form === 'advance' ? run('advance', { amount: Number(f.amount), mode: f.mode })
              : run('checkout', { billId: crypto.randomUUID(), paidAmount: Number(f.amount) || 0, mode: f.mode }))}>{t('save')}</Button>
          </div>
        </div>
      )}
      {!form && message && <p className="text-sm text-due">{message}</p>}
      {s.status === 'booked' && <button onClick={async () => { if ((await confirmAsk(t('rooms.cancelConfirm')))) void run('cancel', {}) }} className={`min-h-11 text-sm ${btn.dangerOutline}`}>{t('rooms.cancel')}</button>}
    </div>
  )
}

/** Season and date prices: from a date to a date, one room or all rooms go for a different rate a night. */
function SeasonPrices({ shopId, rooms }: { shopId: string; rooms: Room[] }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const periods = useQuery({ queryKey: [shopId, 'room-rates'], queryFn: () => api<RatePeriod[]>('/v1/room-rates') }).data ?? []
  const [d, setD] = useState({ name: '', from: '', to: '', roomId: '', rate: '' })
  const [message, setMessage] = useState('')
  const run = (path: string, body: unknown) => api(path, 'POST', body)
    .then(() => { setD({ name: '', from: '', to: '', roomId: '', rate: '' }); setMessage(''); void queryClient.invalidateQueries({ queryKey: [shopId] }) })
    .catch((e: unknown) => setMessage(e instanceof Error ? e.message : t('error')))
  const roomName = (id: string | null) => (id ? rooms.find((r) => r.id === id)?.name ?? '' : t('rooms.allRooms'))
  return (
    <details className="rounded-card bg-white p-3">
      <summary className="min-h-11 cursor-pointer font-bold">{t('rooms.seasonTitle', { count: periods.length })}</summary>
      <p className="text-xs text-muted">{t('rooms.seasonHint')}</p>
      <ul className="my-2 flex flex-col gap-1">
        {periods.map((p) => (
          <li key={p.id} className="flex min-h-11 items-center gap-2 text-sm">
            <span className="min-w-0 flex-1"><span className="font-semibold">{p.name}</span> · {day(p.from)} – {day(p.to)} · {roomName(p.roomId)}</span>
            <span className="font-bold">{formatRupees(p.rate)}</span>
            <button onClick={() => void run(`/v1/room-rates/${p.id}/remove`, {})} className={`min-h-11 ${btn.dangerOutline}`}>{t('rooms.remove')}</button>
          </li>
        ))}
      </ul>
      <div className="grid grid-cols-2 gap-2">
        <Field label={t('rooms.seasonName')} value={d.name} onChange={(e) => setD({ ...d, name: e.target.value })} placeholder={t('rooms.seasonNameHint')} />
        <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('rooms.room')}</span>
          <select value={d.roomId} onChange={(e) => setD({ ...d, roomId: e.target.value })} className="h-12 w-full rounded-xl border border-line bg-white px-3">
            <option value="">{t('rooms.allRooms')}</option>{rooms.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
          </select>
        </label>
        <Field label={t('rooms.firstNight')} type="date" value={d.from} onChange={(e) => setD({ ...d, from: e.target.value })} />
        <Field label={t('rooms.lastNight')} type="date" value={d.to} onChange={(e) => setD({ ...d, to: e.target.value })} />
        <Field label={t('rooms.rate')} value={d.rate} onChange={(e) => setD({ ...d, rate: e.target.value })} inputMode="decimal" />
      </div>
      {message && <p className="text-sm text-due">{message}</p>}
      <Button className="mt-2 w-full" disabled={d.name.trim().length < 2 || !d.from || !d.to || !(Number(d.rate) > 0)}
        onClick={() => void run('/v1/room-rates', { id: crypto.randomUUID(), name: d.name.trim(), from: d.from, to: d.to, roomId: d.roomId || null, rate: Number(d.rate) })}>{t('rooms.addSeason')}</Button>
    </details>
  )
}

/**
 * Booking-site calendars (iCal): paste the site's link for a room to bring its bookings in, and give the site this
 * room's own link so nights booked here show as taken there. Real two-way links with the sites need their partner
 * programmes or a channel manager; calendar links are what every site offers to small properties.
 */
function Calendars({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const rooms = useQuery({ queryKey: [shopId, 'room-calendars'], queryFn: () => api<Room[]>('/v1/rooms/calendars') }).data ?? []
  const [urls, setUrls] = useState<Record<string, string>>({})
  const [message, setMessage] = useState<Record<string, string>>({})
  async function save(room: Room, sync: boolean) {
    try {
      if (urls[room.id] !== undefined) await api(`/v1/rooms/${room.id}/ical`, 'PUT', { importUrl: urls[room.id].trim() || null })
      if (sync) {
        const r = await api<{ added: number; skipped: number; problems: string[] }>(`/v1/rooms/${room.id}/ical/sync`, 'POST', {})
        setMessage({ ...message, [room.id]: [t('rooms.synced', r), ...r.problems].join('\n') })
      } else setMessage({ ...message, [room.id]: t('saved') })
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setMessage({ ...message, [room.id]: e instanceof Error ? e.message : t('error') })
    }
  }
  return (
    <details className="rounded-card bg-white p-3">
      <summary className="min-h-11 cursor-pointer font-bold">{t('rooms.calendarsTitle')}</summary>
      <p className="text-xs text-muted">{t('rooms.calendarsHint')}</p>
      <ul className="mt-2 flex flex-col gap-3">
        {rooms.map((r) => (
          <li key={r.id} className="flex flex-col gap-2 border-t border-line pt-2">
            <p className="font-semibold">{t('rooms.roomLabel', { room: r.name })}</p>
            <Field label={t('rooms.siteLink')} value={urls[r.id] ?? r.icalImportUrl ?? ''} onChange={(e) => setUrls({ ...urls, [r.id]: e.target.value })} placeholder="https://www.airbnb.co.in/calendar/ical/…" />
            <div className="grid grid-cols-2 gap-2">
              <Button tone="plain" onClick={() => void save(r, false)}>{t('save')}</Button>
              <Button disabled={!(urls[r.id] ?? r.icalImportUrl)} onClick={() => void save(r, true)}>{t('rooms.bringIn')}</Button>
            </div>
            {r.icalExportPath && (
              <div className="rounded-xl bg-ground p-2 text-xs">
                <p className="font-semibold">{t('rooms.ourLink')}</p>
                <p className="break-all font-mono">{`${window.location.origin}${r.icalExportPath}`}</p>
                <button onClick={() => void navigator.clipboard?.writeText(`${window.location.origin}${r.icalExportPath}`)} className="min-h-11 font-semibold text-brand">{t('payPage.copy')}</button>
              </div>
            )}
            {message[r.id] && <p className="whitespace-pre-line text-sm text-muted">{message[r.id]}</p>}
          </li>
        ))}
      </ul>
    </details>
  )
}

import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api, request } from '../lib/api'
import { useCustomers } from '../lib/data'
import { shrinkPhoto } from '../lib/images'
import { formatRupees } from '../lib/format'

type Kind = 'advance' | 'travel' | 'food' | 'stay' | 'other' | 'collected'
interface Trip {
  id: string; number: number; technician: string; place: string; startedAt: string; advance: number; spent: number; collected: number
  broughtForward: number; holding: number; returned: number | null; settledAt: string | null
  entries: { id: string; kind: Kind; description: string; amount: number; customerName: string | null; hasPhoto: boolean }[]; jobs: { id: string; number: number; title: string; price: number }[]
}
const KINDS: Kind[] = ['advance', 'travel', 'food', 'stay', 'other', 'collected']

/**
 * Technicians' field trips: the advance, travel, food and stay bills, cash collected on site, and the jobs done. At
 * settlement the bills go into the kharcha book, their cost is shared across the jobs, and the cash still held carries on.
 */
export function TripsScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const trips = useQuery({ queryKey: [shopId, 'trips'], queryFn: () => api<Trip[]>('/v1/trips') }).data ?? []
  const jobs = useQuery({ queryKey: [shopId, 'jobs', false], queryFn: () => api<{ id: string; number: number; title: string }[]>('/v1/jobs') }).data ?? []
  const [d, setD] = useState({ technician: '', place: '' })
  const [message, setMessage] = useState('')

  async function run(path: string, body: unknown) {
    setMessage('')
    try {
      await api(path, 'POST', body)
      void queryClient.invalidateQueries({ queryKey: [shopId] })
      return true
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
      return false
    }
  }

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <p className="text-sm text-muted">{t('trips.hint')}</p>
      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
        <div className="grid grid-cols-2 gap-2">
          <Field label={t('trips.technician')} value={d.technician} onChange={(e) => setD({ ...d, technician: e.target.value })} />
          <Field label={t('trips.place')} value={d.place} onChange={(e) => setD({ ...d, place: e.target.value })} />
        </div>
        <Button disabled={d.technician.trim().length < 2 || d.place.trim().length < 2}
          onClick={() => void run('/v1/trips', { id: crypto.randomUUID(), technician: d.technician.trim(), place: d.place.trim() }).then((ok) => ok && setD({ technician: '', place: '' }))}>
          {t('trips.open')}
        </Button>
      </section>
      {message && <p className="text-sm text-due">{message}</p>}
      {trips.length === 0 && <p className="rounded-[18px] bg-white p-4 text-center text-muted">{t('trips.empty')}</p>}
      {trips.map((trip) => <TripCard key={trip.id} shopId={shopId} trip={trip} jobs={jobs} isOwner={isOwner} run={run} />)}
    </div>
  )
}

function TripCard({ shopId, trip, jobs, isOwner, run }: {
  shopId: string; trip: Trip; jobs: { id: string; number: number; title: string }[]; isOwner: boolean; run: (path: string, body: unknown) => Promise<boolean>
}) {
  const { t } = useTranslation()
  const { customers } = useCustomers(shopId)
  const [f, setF] = useState({ kind: 'travel' as Kind, amount: '', text: '', jobId: '', returned: '', customerId: '', split: 'equal', photo: '' })
  const [km, setKm] = useState<Record<string, string>>({})
  async function pickPhoto(file: File | undefined) {
    if (file) setF({ ...f, photo: await shrinkPhoto(file, 1280, 380_000, false) })
  }
  // The bill photo needs the sign-in, so it is fetched and opened as a local file.
  async function openPhoto(entryId: string) {
    const response = await request(`/v1/trips/photos/${entryId}`, 'GET')
    if (response.ok) window.open(URL.createObjectURL(await response.blob()), '_blank')
  }
  const open = !trip.settledAt
  const line = (label: string, value: number, strong = false) => (
    <div className={`flex justify-between ${strong ? 'font-bold' : ''}`}><span>{label}</span><span>{formatRupees(value)}</span></div>
  )
  const select = 'h-12 rounded-xl border border-line bg-white px-3'

  return (
    <section className="flex flex-col gap-2 rounded-[18px] bg-white p-3 text-sm">
      <h3 className="font-bold">#{trip.number} {trip.technician} · {trip.place}{open ? '' : ` · ${t('trips.settled')}`}</h3>
      {trip.broughtForward !== 0 && line(t('trips.broughtForward'), trip.broughtForward)}
      {line(t('trips.advance'), trip.advance)}
      {line(t('trips.collected'), trip.collected)}
      {line(t('trips.spent'), trip.spent)}
      {trip.returned != null && line(t('trips.returned'), trip.returned)}
      {line(t(open ? 'trips.holding' : 'trips.carried'), trip.holding, true)}
      <ul className="border-t border-line pt-1">
        {trip.entries.map((e) => (
          <li key={e.id} className="flex justify-between py-0.5">
            <span>{t(`trips.kind.${e.kind}`)}{e.description.toLowerCase() !== e.kind ? ` · ${e.description}` : ''}{e.customerName ? ` · ${e.customerName}` : ''}
              {e.hasPhoto && <button onClick={() => void openPhoto(e.id)} className="ml-1 font-semibold text-teal-700">📷 {t('trips.viewBill')}</button>}</span>
            <span>{formatRupees(e.amount)}</span>
          </li>
        ))}
      </ul>
      {trip.jobs.length > 0 && <p className="text-xs text-muted">{t('trips.jobs')}: {trip.jobs.map((j) => `#${j.number} ${j.title}`).join(', ')}</p>}
      {open && (
        <>
          <div className="grid grid-cols-[1fr_1fr] gap-2">
            <select value={f.kind} onChange={(e) => setF({ ...f, kind: e.target.value as Kind })} aria-label={t('trips.what')} className={select}>
              {KINDS.map((k) => <option key={k} value={k}>{t(`trips.kind.${k}`)}</option>)}
            </select>
            <Field label={t('trips.amount')} value={f.amount} onChange={(e) => setF({ ...f, amount: e.target.value })} inputMode="decimal" />
          </div>
          <Field label={t('trips.note')} value={f.text} onChange={(e) => setF({ ...f, text: e.target.value })} placeholder={t('trips.noteHint')} />
          {f.kind === 'collected' && (
            <select value={f.customerId} onChange={(e) => setF({ ...f, customerId: e.target.value })} aria-label={t('trips.fromCustomer')} className={select}>
              <option value="">{t('trips.noCustomer')}</option>{customers.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
          )}
          {['travel', 'food', 'stay', 'other'].includes(f.kind) && (
            <label className="flex min-h-11 cursor-pointer items-center justify-center rounded-xl border border-dashed border-line font-semibold text-teal-700">
              {f.photo ? `✓ ${t('trips.photoAdded')}` : `📷 ${t('trips.addPhoto')}`}
              <input type="file" accept="image/*" capture="environment" onChange={(e) => void pickPhoto(e.target.files?.[0])} className="sr-only" />
            </label>
          )}
          <Button tone="plain" disabled={!(Number(f.amount) > 0)}
            onClick={() => void run(`/v1/trips/${trip.id}/entries`, {
              id: crypto.randomUUID(), kind: f.kind, amount: Number(f.amount), description: f.text.trim() || null,
              customerId: f.kind === 'collected' && f.customerId ? f.customerId : null, photoDataUrl: f.photo && f.kind !== 'collected' && f.kind !== 'advance' ? f.photo : null,
            }).then((ok) => ok && setF({ ...f, amount: '', text: '', customerId: '', photo: '' }))}>{t('trips.add')}</Button>
          <div className="grid grid-cols-[2fr_1fr] gap-2">
            <select value={f.jobId} onChange={(e) => setF({ ...f, jobId: e.target.value })} aria-label={t('trips.addJob')} className={select}>
              <option value="">{t('trips.addJob')}</option>
              {jobs.filter((j) => !trip.jobs.some((x) => x.id === j.id)).map((j) => <option key={j.id} value={j.id}>#{j.number} {j.title}</option>)}
            </select>
            <Button tone="plain" disabled={!f.jobId} onClick={() => void run(`/v1/trips/${trip.id}/jobs`, { jobId: f.jobId }).then((ok) => ok && setF({ ...f, jobId: '' }))}>{t('trips.attach')}</Button>
          </div>
          {isOwner && (
            <div className="flex flex-col gap-2 border-t border-line pt-2">
              {trip.jobs.length > 1 && (
                <select value={f.split} onChange={(e) => setF({ ...f, split: e.target.value })} aria-label={t('trips.split')} className={select}>
                  {(['equal', 'value', 'distance'] as const).map((s) => <option key={s} value={s}>{t(`trips.splitBy.${s}`)}</option>)}
                </select>
              )}
              {f.split === 'distance' && trip.jobs.map((j) => (
                <Field key={j.id} label={t('trips.kmFor', { job: `#${j.number} ${j.title}` })} value={km[j.id] ?? ''} onChange={(e) => setKm({ ...km, [j.id]: e.target.value })} inputMode="decimal" />
              ))}
              <div className="grid grid-cols-[2fr_1fr] items-end gap-2">
                <Field label={t('trips.handBack', { amount: formatRupees(trip.holding) })} value={f.returned} onChange={(e) => setF({ ...f, returned: e.target.value })} inputMode="decimal" placeholder={String(trip.holding)} />
                <Button className="h-12" onClick={() => void run(`/v1/trips/${trip.id}/settle`, {
                  returned: f.returned === '' ? trip.holding : Number(f.returned), split: f.split,
                  km: f.split === 'distance' ? Object.fromEntries(trip.jobs.map((j) => [j.id, Number(km[j.id]) || 0])) : null,
                })}>{t('trips.settle')}</Button>
              </div>
              <p className="text-xs text-muted">{t('trips.cashHint')}</p>
            </div>
          )}
        </>
      )}
    </section>
  )
}

import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'

type Kind = 'advance' | 'travel' | 'food' | 'stay' | 'other' | 'collected'
interface Trip {
  id: string; number: number; technician: string; place: string; startedAt: string; advance: number; spent: number; collected: number
  broughtForward: number; holding: number; returned: number | null; settledAt: string | null
  entries: { id: string; kind: Kind; description: string; amount: number }[]; jobs: { id: string; number: number; title: string }[]
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
      {trips.map((trip) => <TripCard key={trip.id} trip={trip} jobs={jobs} isOwner={isOwner} run={run} />)}
    </div>
  )
}

function TripCard({ trip, jobs, isOwner, run }: {
  trip: Trip; jobs: { id: string; number: number; title: string }[]; isOwner: boolean; run: (path: string, body: unknown) => Promise<boolean>
}) {
  const { t } = useTranslation()
  const [f, setF] = useState({ kind: 'travel' as Kind, amount: '', text: '', jobId: '', returned: '' })
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
          <li key={e.id} className="flex justify-between py-0.5"><span>{t(`trips.kind.${e.kind}`)}{e.description.toLowerCase() !== e.kind ? ` · ${e.description}` : ''}</span><span>{formatRupees(e.amount)}</span></li>
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
          <Button tone="plain" disabled={!(Number(f.amount) > 0)}
            onClick={() => void run(`/v1/trips/${trip.id}/entries`, { id: crypto.randomUUID(), kind: f.kind, amount: Number(f.amount), description: f.text.trim() || null })
              .then((ok) => ok && setF({ ...f, amount: '', text: '' }))}>{t('trips.add')}</Button>
          <div className="grid grid-cols-[2fr_1fr] gap-2">
            <select value={f.jobId} onChange={(e) => setF({ ...f, jobId: e.target.value })} aria-label={t('trips.addJob')} className={select}>
              <option value="">{t('trips.addJob')}</option>
              {jobs.filter((j) => !trip.jobs.some((x) => x.id === j.id)).map((j) => <option key={j.id} value={j.id}>#{j.number} {j.title}</option>)}
            </select>
            <Button tone="plain" disabled={!f.jobId} onClick={() => void run(`/v1/trips/${trip.id}/jobs`, { jobId: f.jobId }).then((ok) => ok && setF({ ...f, jobId: '' }))}>{t('trips.attach')}</Button>
          </div>
          {isOwner && (
            <div className="grid grid-cols-[2fr_1fr] items-end gap-2 border-t border-line pt-2">
              <Field label={t('trips.handBack', { amount: formatRupees(trip.holding) })} value={f.returned} onChange={(e) => setF({ ...f, returned: e.target.value })} inputMode="decimal" placeholder={String(trip.holding)} />
              <Button className="h-12" onClick={() => void run(`/v1/trips/${trip.id}/settle`, { returned: f.returned === '' ? trip.holding : Number(f.returned) })}>{t('trips.settle')}</Button>
            </div>
          )}
        </>
      )}
    </section>
  )
}

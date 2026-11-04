import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'

interface Claim {
  id: string; number: number; supplierId: string; supplierName: string; jobId: string | null; jobNumber: number | null; serialNo: string | null
  description: string; parts: number; labour: number; amount: number; raisedAt: string; daysOpen: number; dispatchNo: string | null
  creditAmount: number | null; creditNoteNo: string | null; rejectReason: string | null; settledAt: string | null; loss: number
}

const BANDS = [[0, 30], [31, 60], [61, Infinity]] as const

/**
 * Warranty claims on companies: raised from a service job, owed to the shop until credited or rejected. Open claims
 * are aged (0–30, 31–60, 60+ days) by company, because old claims are the ones companies refuse.
 */
export function ClaimsScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [all, setAll] = useState(false)
  const [adding, setAdding] = useState(false)
  const [message, setMessage] = useState('')
  const claims = useQuery({ queryKey: [shopId, 'claims', all], queryFn: () => api<Claim[]>(`/v1/claims${all ? '?all=true' : ''}`) }).data ?? []
  const suppliers = useQuery({ queryKey: [shopId, 'suppliers'], queryFn: () => api<{ id: string; name: string }[]>('/v1/suppliers') }).data ?? []
  const jobs = useQuery({ queryKey: [shopId, 'jobs', true], queryFn: () => api<{ id: string; number: number; title: string; serialNo: string | null }[]>('/v1/jobs?done=true') }).data ?? []
  const [d, setD] = useState({ supplierId: '', jobId: '', description: '', parts: '', labour: '' })

  async function run(work: () => Promise<unknown>) {
    setMessage('')
    try {
      await work()
      void queryClient.invalidateQueries({ queryKey: [shopId, 'claims'] })
      void queryClient.invalidateQueries({ queryKey: [shopId, 'suppliers'] })
      return true
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
      return false
    }
  }
  const raise = () => run(() => api('/v1/claims', 'POST', {
    id: crypto.randomUUID(), supplierId: d.supplierId, jobId: d.jobId || null, description: d.description.trim() || null,
    parts: d.parts ? Number(d.parts) : null, labour: Number(d.labour) || 0,
  })).then((ok) => { if (ok) { setAdding(false); setD({ supplierId: '', jobId: '', description: '', parts: '', labour: '' }) } })

  // Ageing of open claims, by company.
  const open = claims.filter((c) => !c.settledAt)
  const companies = [...new Set(open.map((c) => c.supplierName))]
  const band = (name: string, [lo, hi]: readonly [number, number]) => open.filter((c) => c.supplierName === name && c.daysOpen >= lo && c.daysOpen <= hi).reduce((s, c) => s + c.amount, 0)
  const chip = (on: boolean) => `min-h-11 flex-1 rounded-full text-sm font-semibold ${on ? 'bg-teal-700 text-white' : 'border border-line bg-white'}`
  const select = 'h-12 w-full rounded-xl border border-line bg-white px-3'

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <p className="text-sm text-muted">{t('claims.hint')}</p>
      {!all && open.length > 0 && (
        <section className="rounded-[18px] bg-white p-3 text-sm">
          <h3 className="mb-2 font-bold">{t('claims.ageing', { amount: formatRupees(open.reduce((s, c) => s + c.amount, 0)) })}</h3>
          <div className="grid grid-cols-[2fr_1fr_1fr_1fr] gap-1">
            <span /><b>0–30</b><b>31–60</b><b className="text-due">60+</b>
            {companies.map((name) => [
              <span key={name}>{name}</span>,
              ...BANDS.map((b, i) => <span key={name + i} className={i === 2 && band(name, b) > 0 ? 'font-semibold text-due' : ''}>{formatRupees(band(name, b))}</span>),
            ])}
          </div>
        </section>
      )}
      <div className="flex gap-2">
        <button onClick={() => setAll(false)} className={chip(!all)}>{t('claims.open')}</button>
        <button onClick={() => setAll(true)} className={chip(all)}>{t('claims.all')}</button>
      </div>
      {!all && (adding ? (
        <section className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
          <select value={d.supplierId} onChange={(e) => setD({ ...d, supplierId: e.target.value })} aria-label={t('claims.company')} className={select}>
            <option value="">{t('claims.company')}</option>{suppliers.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
          </select>
          {suppliers.length === 0 && <p className="text-xs text-muted">{t('claims.noCompany')}</p>}
          <select value={d.jobId} onChange={(e) => setD({ ...d, jobId: e.target.value })} aria-label={t('claims.job')} className={select}>
            <option value="">{t('claims.noJob')}</option>{jobs.map((j) => <option key={j.id} value={j.id}>#{j.number} {j.title}{j.serialNo ? ` · ${j.serialNo}` : ''}</option>)}
          </select>
          <Field label={t('claims.what')} value={d.description} onChange={(e) => setD({ ...d, description: e.target.value })} placeholder={d.jobId ? t('claims.fromJob') : ''} />
          <div className="grid grid-cols-2 gap-2">
            <Field label={t('claims.parts')} value={d.parts} onChange={(e) => setD({ ...d, parts: e.target.value })} inputMode="decimal" placeholder={d.jobId ? t('claims.fromJob') : ''} />
            <Field label={t('claims.labour')} value={d.labour} onChange={(e) => setD({ ...d, labour: e.target.value })} inputMode="decimal" />
          </div>
          <div className="flex gap-2">
            <Button tone="plain" className="flex-1" onClick={() => setAdding(false)}>{t('cancel')}</Button>
            <Button className="flex-1" disabled={!d.supplierId} onClick={() => void raise()}>{t('claims.raise')}</Button>
          </div>
        </section>
      ) : <Button onClick={() => setAdding(true)}>{t('claims.new')}</Button>)}
      {message && <p className="text-sm text-due">{message}</p>}
      {claims.length === 0 && <p className="rounded-[18px] bg-white p-4 text-center text-muted">{t('claims.empty')}</p>}
      {claims.map((c) => <ClaimCard key={c.id} claim={c} run={run} />)}
    </div>
  )
}

function ClaimCard({ claim: c, run }: { claim: Claim; run: (work: () => Promise<unknown>) => Promise<boolean> }) {
  const { t } = useTranslation()
  const [form, setForm] = useState<'dispatch' | 'settle' | null>(null)
  const [f, setF] = useState({ text: '', amount: '', reason: '' })
  const save = () => run(() => form === 'dispatch'
    ? api(`/v1/claims/${c.id}/dispatch`, 'POST', { dispatchNo: f.text.trim() || null })
    : api(`/v1/claims/${c.id}/settle`, 'POST', { amount: Number(f.amount) || 0, creditNoteNo: f.text.trim() || null, rejectReason: f.reason.trim() || null }))
    .then((ok) => { if (ok) setForm(null) })

  return (
    <div className="flex flex-col gap-1 rounded-[18px] bg-white p-3 text-sm">
      <p className="flex justify-between gap-2"><b>#{c.number} {c.supplierName}</b><b>{formatRupees(c.amount)}</b></p>
      <p>{c.description}{c.serialNo ? ` · S/N ${c.serialNo}` : ''}{c.jobNumber ? ` · ${t('claims.jobNo', { number: c.jobNumber })}` : ''}</p>
      <p className="text-xs text-muted">{t('claims.split', { parts: formatRupees(c.parts), labour: formatRupees(c.labour) })}</p>
      {c.settledAt
        ? <p className={c.creditAmount ? 'text-teal-800' : 'text-due'}>{c.creditAmount ? t('claims.credited', { amount: formatRupees(c.creditAmount), loss: formatRupees(c.loss) }) : t('claims.rejected', { reason: c.rejectReason ?? '' })}</p>
        : <p className={c.daysOpen > 60 ? 'font-semibold text-due' : 'text-muted'}>{t('claims.daysOpen', { days: c.daysOpen })}{c.dispatchNo ? ` · ${t('claims.sent', { no: c.dispatchNo })}` : ` · ${t('claims.notSent')}`}</p>}
      {!c.settledAt && !form && (
        <div className="flex gap-2">
          {!c.dispatchNo && <Button tone="plain" className="h-9 flex-1 text-xs" onClick={() => setForm('dispatch')}>{t('claims.markSent')}</Button>}
          <Button tone="plain" className="h-9 flex-1 text-xs" onClick={() => { setForm('settle'); setF({ text: '', amount: String(c.amount), reason: '' }) }}>{t('claims.settle')}</Button>
        </div>
      )}
      {form && (
        <div className="flex flex-col gap-2">
          {form === 'settle' && <Field label={t('claims.creditAmount')} value={f.amount} onChange={(e) => setF({ ...f, amount: e.target.value })} inputMode="decimal" />}
          <Field label={t(form === 'dispatch' ? 'claims.docket' : 'claims.creditNote')} value={f.text} onChange={(e) => setF({ ...f, text: e.target.value })} />
          {form === 'settle' && Number(f.amount) === 0 && <Field label={t('claims.reason')} value={f.reason} onChange={(e) => setF({ ...f, reason: e.target.value })} />}
          <div className="flex gap-2">
            <Button tone="plain" className="flex-1" onClick={() => setForm(null)}>{t('cancel')}</Button>
            <Button className="flex-1" onClick={() => void save()}>{t('save')}</Button>
          </div>
        </div>
      )}
    </div>
  )
}

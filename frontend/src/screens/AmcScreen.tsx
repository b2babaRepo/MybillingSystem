import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { useCustomers } from '../lib/data'
import { formatRupees, whatsAppLink } from '../lib/format'
import { navigate } from '../router'

interface Amc {
  id: string; number: number; customerId: string; customerName: string; customerPhone: string | null; title: string; serialNo: string | null
  startOn: string; endsOn: string; months: number; visits: number; visitsDone: number; nextDue: string | null; fee: number; billId: string
}

const day = (iso: string) => new Date(`${iso}T00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric' })
const daysFrom = (iso: string) => Math.round((new Date(`${iso}T00:00`).getTime() - new Date(new Date().toDateString()).getTime()) / 86_400_000)
const EMPTY = { customerId: '', title: '', serialNo: '', months: '12', visits: '4', fee: '', paid: '', mode: 'cash' }

/**
 * AMC contracts: the fee is billed once, each visit opens a free service job, the next visit's date is shown (overdue
 * in red), and contracts ending within 30 days get a renewal reminder on WhatsApp.
 */
export function AmcScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { customers } = useCustomers(shopId)
  const amcs = useQuery({ queryKey: [shopId, 'amc'], queryFn: () => api<Amc[]>('/v1/amc') }).data ?? []
  const [adding, setAdding] = useState(false)
  const [d, setD] = useState(EMPTY)
  const [message, setMessage] = useState('')

  async function run(path: string, body: unknown, done = '') {
    setMessage('')
    try {
      await api(path, 'POST', body)
      void queryClient.invalidateQueries({ queryKey: [shopId] })
      setMessage(done)
      return true
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
      return false
    }
  }
  const save = () => run('/v1/amc', {
    id: crypto.randomUUID(), customerId: d.customerId, title: d.title.trim(), serialNo: d.serialNo.trim() || null, months: Number(d.months), visits: Number(d.visits),
    fee: Number(d.fee), paidAmount: Number(d.paid) || 0, mode: d.mode,
  }, t('saved')).then((ok) => { if (ok) { setAdding(false); setD(EMPTY) } })

  const renewing = amcs.filter((a) => daysFrom(a.endsOn) <= 30)
  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <p className="text-sm text-muted">{t('amc.hint')}</p>
      {renewing.length > 0 && <p className="rounded-[18px] bg-amber-50 p-3 text-sm font-semibold text-amber-900">{t('amc.renewSoon', { count: renewing.length })}</p>}
      {adding ? (
        <section className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
          <select value={d.customerId} onChange={(e) => setD({ ...d, customerId: e.target.value })} aria-label={t('amc.customer')} className="h-12 rounded-xl border border-line bg-white px-3">
            <option value="">{t('amc.customer')}</option>{customers.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
          <Field label={t('amc.covers')} value={d.title} onChange={(e) => setD({ ...d, title: e.target.value })} placeholder={t('amc.coversHint')} />
          <Field label={t('amc.serial')} value={d.serialNo} onChange={(e) => setD({ ...d, serialNo: e.target.value })} />
          <div className="grid grid-cols-3 gap-2">
            <Field label={t('amc.months')} value={d.months} onChange={(e) => setD({ ...d, months: e.target.value })} inputMode="numeric" />
            <Field label={t('amc.visits')} value={d.visits} onChange={(e) => setD({ ...d, visits: e.target.value })} inputMode="numeric" />
            <Field label={t('amc.fee')} value={d.fee} onChange={(e) => setD({ ...d, fee: e.target.value })} inputMode="decimal" />
          </div>
          <div className="grid grid-cols-2 gap-2">
            <Field label={t('amc.paid')} value={d.paid} onChange={(e) => setD({ ...d, paid: e.target.value })} inputMode="decimal" />
            <select value={d.mode} onChange={(e) => setD({ ...d, mode: e.target.value })} aria-label={t('amc.mode')} className="h-12 self-end rounded-xl border border-line bg-white px-3">
              <option value="cash">{t('cash')}</option><option value="upi">UPI</option>
            </select>
          </div>
          <div className="flex gap-2">
            <Button tone="plain" className="flex-1" onClick={() => setAdding(false)}>{t('cancel')}</Button>
            <Button className="flex-1" disabled={!d.customerId || d.title.trim().length < 2 || !(Number(d.fee) > 0)} onClick={() => void save()}>{t('amc.sign')}</Button>
          </div>
        </section>
      ) : <Button onClick={() => setAdding(true)}>{t('amc.new')}</Button>}
      {message && <p className="text-sm font-semibold text-teal-900">{message}</p>}
      {amcs.length === 0 && <p className="rounded-[18px] bg-white p-4 text-center text-muted">{t('amc.empty')}</p>}
      {amcs.map((a) => <AmcCard key={a.id} amc={a} run={run} />)}
    </div>
  )
}

function AmcCard({ amc: a, run }: { amc: Amc; run: (path: string, body: unknown, done?: string) => Promise<boolean> }) {
  const { t } = useTranslation()
  const [fee, setFee] = useState<string | null>(null)
  const left = daysFrom(a.endsOn)
  const due = a.nextDue ? daysFrom(a.nextDue) : null
  const reminder = t('amc.reminder', { name: a.customerName, title: a.title, date: day(a.endsOn), fee: formatRupees(a.fee) })

  return (
    <section className="flex flex-col gap-1 rounded-[18px] bg-white p-3 text-sm">
      <p className="flex justify-between gap-2"><b>#{a.number} {a.customerName}</b><b>{formatRupees(a.fee)}</b></p>
      <p>{a.title}{a.serialNo ? ` · S/N ${a.serialNo}` : ''}</p>
      <p className="text-xs text-muted">{t('amc.period', { from: day(a.startOn), to: day(a.endsOn) })} · {t('amc.used', { done: a.visitsDone, total: a.visits })}</p>
      {a.nextDue && due != null && <p className={due < 0 ? 'font-semibold text-due' : 'text-teal-800'}>{t(due < 0 ? 'amc.overdue' : 'amc.nextDue', { date: day(a.nextDue) })}</p>}
      {left <= 30 && <p className="font-semibold text-amber-800">{t(left < 0 ? 'amc.ended' : 'amc.endsIn', { days: left })}</p>}
      <div className="flex flex-wrap gap-2 pt-1">
        {a.nextDue && left >= 0 && (
          <Button tone="plain" className="h-9 px-3 text-xs" onClick={() => void run(`/v1/amc/${a.id}/visit`, { jobId: crypto.randomUUID() }).then((ok) => ok && navigate('/jobs'))}>{t('amc.visit')}</Button>
        )}
        {left <= 30 && a.customerPhone && (
          <a href={whatsAppLink(a.customerPhone, reminder)} target="_blank" rel="noreferrer" className="inline-flex h-9 items-center rounded-xl border border-line px-3 text-xs font-semibold text-teal-700">💬 {t('amc.remind')}</a>
        )}
        {left <= 30 && fee == null && <Button tone="plain" className="h-9 px-3 text-xs" onClick={() => setFee(String(a.fee))}>{t('amc.renew')}</Button>}
      </div>
      {fee != null && (
        <div className="grid grid-cols-[1fr_auto_auto] items-end gap-2">
          <Field label={t('amc.fee')} value={fee} onChange={(e) => setFee(e.target.value)} inputMode="decimal" />
          <Button tone="plain" className="h-12 px-3" onClick={() => setFee(null)}>{t('cancel')}</Button>
          <Button className="h-12 px-3" onClick={() => void run(`/v1/amc/${a.id}/renew`, { id: crypto.randomUUID(), fee: Number(fee), paidAmount: 0 }, t('saved')).then((ok) => ok && setFee(null))}>{t('amc.renew')}</Button>
        </div>
      )}
    </section>
  )
}

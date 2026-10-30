import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { isValidAadhaar, type FieldAgent, type FieldAgentStatus } from '../lib/agents'
import { formatDateTime, formatPhone } from '../lib/format'

const TABS: FieldAgentStatus[] = ['applied', 'approved', 'blocked', 'rejected']

/**
 * Field agents (super and agent-manager admins): applications to check, approved agents with their codes, blocked and
 * rejected ones. Approving asks the manager to confirm they saw the Aadhaar card; the app only checks the number's form.
 */
export function AgentsPanel() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [tab, setTab] = useState<FieldAgentStatus>('applied')
  const agents = useQuery({ queryKey: ['platform', 'agents', tab], queryFn: () => api<FieldAgent[]>(`/v1/platform/agents?status=${tab}`), enabled: open })
  const waiting = useQuery({ queryKey: ['platform', 'agents', 'applied'], queryFn: () => api<FieldAgent[]>('/v1/platform/agents?status=applied') }).data?.length ?? 0
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)
  const empty = { fullName: '', phone: '', address: '', market: '', aadhaar: '', upiId: '' }
  const [form, setForm] = useState(empty)

  async function run(action: () => Promise<unknown>, done?: string) {
    setBusy(true)
    setMessage(null)
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: ['platform', 'agents'] })
      if (done) setMessage(done)
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  const box = 'h-11 rounded-xl border border-line bg-white px-2'
  return (
    <section className="flex flex-col gap-3 rounded-2xl bg-white p-4">
      <button onClick={() => setOpen(!open)} aria-expanded={open} className="flex min-h-11 items-center justify-between text-left">
        <h2 className="text-lg font-bold">{t('agentsAdmin.title', { count: waiting })}</h2>
        <span aria-hidden className="text-xl">{open ? '−' : '+'}</span>
      </button>
      {open && <>
        <p className="text-sm text-muted">{t('agentsAdmin.hint')}</p>
        <div role="tablist" className="grid grid-cols-4 gap-1 rounded-2xl bg-ground p-1 text-xs font-semibold">
          {TABS.map((s) => (
            <button key={s} role="tab" aria-selected={tab === s} onClick={() => setTab(s)}
              className={`min-h-11 rounded-xl ${tab === s ? 'bg-teal-700 text-white' : ''}`}>{t(`fieldAgent.status.${s}`)}</button>
          ))}
        </div>

        {agents.data?.length === 0 && <p className="text-sm text-muted">{t('agentsAdmin.none')}</p>}
        <ul className="flex flex-col gap-2">
          {agents.data?.map((a) => (
            <li key={a.id} className="flex flex-col gap-1 rounded-xl border border-line p-3 text-sm">
              <span className="flex items-baseline justify-between gap-2">
                <b className="text-base">{a.fullName}</b>
                {a.code && <span className="font-mono font-bold text-teal-800">{a.code}</span>}
              </span>
              <span>{formatPhone(a.phone)} · <a href={`tel:${a.phone}`} className="text-teal-700 underline">{t('agentsAdmin.call')}</a></span>
              <span>{a.market}</span>
              <span className="text-muted">{a.address}</span>
              <span className="text-muted">
                {t('agentsAdmin.aadhaarEnds', { last4: a.aadhaarLast4 })}{a.kycChecked ? ` · ✓ ${t('agentsAdmin.kycDone')}` : ''}{a.upiId ? ` · UPI ${a.upiId}` : ''}
              </span>
              <span className="text-xs text-muted">
                {t('agentsAdmin.applied', { date: formatDateTime(a.appliedAt) })}{a.approvedBy ? ` · ${t('agentsAdmin.approvedBy', { name: a.approvedBy })}` : ''}
              </span>
              {a.statusReason && <span className="font-semibold text-red-800">{a.statusReason}</span>}

              <div className="mt-1 grid grid-cols-2 gap-2">
                {a.status === 'applied' && <>
                  <button disabled={busy} className="min-h-11 rounded-xl bg-teal-700 font-bold text-white"
                    onClick={() => confirm(t('agentsAdmin.kycAsk', { name: a.fullName, last4: a.aadhaarLast4 }))
                      && void run(() => api(`/v1/platform/agents/${a.id}/approve`, 'POST', { kycChecked: true }), t('agentsAdmin.approved', { name: a.fullName }))}>
                    {t('agentsAdmin.approve')}</button>
                  <button disabled={busy} className="min-h-11 rounded-xl border border-line font-semibold text-red-700"
                    onClick={() => { const reason = prompt(t('agentsAdmin.rejectAsk')); if (reason) void run(() => api(`/v1/platform/agents/${a.id}/reject`, 'POST', { reason })) }}>
                    {t('agentsAdmin.reject')}</button>
                </>}
                {a.status === 'approved' && <>
                  <button disabled={busy} className="min-h-11 rounded-xl border border-line font-semibold"
                    onClick={() => { const market = prompt(t('agentsAdmin.marketAsk'), a.market); if (market) void run(() => api(`/v1/platform/agents/${a.id}/market`, 'PUT', { market })) }}>
                    {t('agentsAdmin.changeMarket')}</button>
                  <button disabled={busy} className="min-h-11 rounded-xl border border-line font-semibold text-red-700"
                    onClick={() => { const reason = prompt(t('agentsAdmin.blockAsk')); if (reason) void run(() => api(`/v1/platform/agents/${a.id}/block`, 'POST', { reason })) }}>
                    {t('agentsAdmin.block')}</button>
                </>}
                {a.status === 'blocked' && (
                  <button disabled={busy} className="col-span-2 min-h-11 rounded-xl bg-teal-700 font-bold text-white"
                    onClick={() => void run(() => api(`/v1/platform/agents/${a.id}/unblock`, 'POST', {}))}>{t('agentsAdmin.unblock')}</button>
                )}
              </div>
            </li>
          ))}
        </ul>

        {adding ? (
          <div className="flex flex-col gap-2 rounded-xl bg-ground p-3 text-sm">
            <span className="font-bold">{t('agentsAdmin.addTitle')}</span>
            <input aria-label={t('agentsAdmin.name')} placeholder={t('agentsAdmin.name')} value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} className={box} />
            <input aria-label={t('agentsAdmin.phone')} placeholder={t('agentsAdmin.phone')} value={form.phone} inputMode="tel" onChange={(e) => setForm({ ...form, phone: e.target.value })} className={box} />
            <input aria-label={t('fieldAgent.address')} placeholder={t('fieldAgent.address')} value={form.address} onChange={(e) => setForm({ ...form, address: e.target.value })} className={box} />
            <input aria-label={t('fieldAgent.market')} placeholder={t('fieldAgent.market')} value={form.market} onChange={(e) => setForm({ ...form, market: e.target.value })} className={box} />
            <input aria-label={t('fieldAgent.aadhaar')} placeholder={t('fieldAgent.aadhaar')} value={form.aadhaar} inputMode="numeric"
              onChange={(e) => setForm({ ...form, aadhaar: e.target.value.replace(/[^\d ]/g, '').slice(0, 14) })} className={`${box} font-mono`} />
            {form.aadhaar.replace(/\s/g, '').length === 12 && !isValidAadhaar(form.aadhaar) && <span className="font-semibold text-red-700">{t('fieldAgent.aadhaarWrong')}</span>}
            <input aria-label={t('fieldAgent.upi')} placeholder={t('fieldAgent.upi')} value={form.upiId} onChange={(e) => setForm({ ...form, upiId: e.target.value.trim() })} className={box} />
            <div className="grid grid-cols-2 gap-2">
              <button disabled={busy || !isValidAadhaar(form.aadhaar) || form.fullName.trim().length < 2 || form.phone.trim().length < 10}
                className="min-h-11 rounded-xl bg-teal-700 font-bold text-white disabled:opacity-40"
                onClick={() => void run(async () => {
                  const result = await api<{ agent: FieldAgent; tempPassword: string | null }>('/v1/platform/agents', 'POST', { ...form, upiId: form.upiId || null })
                  setForm(empty)
                  setAdding(false)
                  setTab('applied')
                  if (result.tempPassword) setMessage(t('agentsAdmin.addedLogin', { phone: result.agent.phone, password: result.tempPassword }))
                })}>{t('agentsAdmin.add')}</button>
              <button disabled={busy} className="min-h-11 rounded-xl border border-line font-semibold" onClick={() => setAdding(false)}>{t('plans.cancel')}</button>
            </div>
          </div>
        ) : (
          <button onClick={() => setAdding(true)} className="min-h-11 rounded-xl border-2 border-dashed border-line font-semibold">+ {t('agentsAdmin.addTitle')}</button>
        )}
        {message && <p className="rounded-xl bg-teal-50 p-3 text-sm font-semibold text-teal-900">{message}</p>}
      </>}
    </section>
  )
}

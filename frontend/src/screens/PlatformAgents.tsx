import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api, request } from '../lib/api'
import { BADGE_ICON, isValidAadhaar, type AgentPerformanceRow, type FieldAgent, type FieldAgentStatus, type Registration } from '../lib/agents'
import { formatDateTime, formatPhone, formatRupees } from '../lib/format'
import type { AdminRole } from '../lib/types'
import { addText } from '../i18n'
import { agentText } from '../i18n/agent'
import { ask, confirmAsk } from '../components/Ask'

addText(agentText)

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
                    onClick={async () => (await confirmAsk(t('agentsAdmin.kycAsk', { name: a.fullName, last4: a.aadhaarLast4 })))
                      && void run(() => api(`/v1/platform/agents/${a.id}/approve`, 'POST', { kycChecked: true }), t('agentsAdmin.approved', { name: a.fullName }))}>
                    {t('agentsAdmin.approve')}</button>
                  <button disabled={busy} className="min-h-11 rounded-xl border border-line font-semibold text-red-700"
                    onClick={async () => { const reason = (await ask(t('agentsAdmin.rejectAsk'))); if (reason) void run(() => api(`/v1/platform/agents/${a.id}/reject`, 'POST', { reason })) }}>
                    {t('agentsAdmin.reject')}</button>
                </>}
                {a.status === 'approved' && <>
                  <button disabled={busy} className="min-h-11 rounded-xl border border-line font-semibold"
                    onClick={async () => { const market = (await ask(t('agentsAdmin.marketAsk'), a.market)); if (market) void run(() => api(`/v1/platform/agents/${a.id}/market`, 'PUT', { market })) }}>
                    {t('agentsAdmin.changeMarket')}</button>
                  <button disabled={busy} className="min-h-11 rounded-xl border border-line font-semibold text-red-700"
                    onClick={async () => { const reason = (await ask(t('agentsAdmin.blockAsk'))); if (reason) void run(() => api(`/v1/platform/agents/${a.id}/block`, 'POST', { reason })) }}>
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

/** A photo the API serves only with the admin's token: fetched, then shown from memory. */
function AuthPhoto({ path, alt }: { path: string; alt: string }) {
  const photo = useQuery({
    queryKey: ['platform', 'photo', path],
    queryFn: async () => URL.createObjectURL(await (await request(path)).blob()),
    staleTime: Infinity,
  }).data
  return photo ? <a href={photo} target="_blank" rel="noreferrer"><img src={photo} alt={alt} className="aspect-square w-full rounded-lg object-cover" /></a> : <div className="aspect-square rounded-lg bg-ground" />
}

const REG_TABS = ['submitted', 'payment', 'approved', 'rejected'] as const

/**
 * Shops agents signed up (approvers, agent managers, super admins; billing admins see the payments tab): photos, map
 * pin, owner, plan and money, with warnings for likely duplicates. Approve opens the shop under the owner's number;
 * confirming the payment starts their plan.
 */
export function RegistrationsPanel({ role }: { role: AdminRole }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const review = role !== 'billing'
  const money = role === 'super' || role === 'billing'
  const first = review ? 'submitted' : 'payment'
  const [open, setOpen] = useState(false)
  const [tab, setTab] = useState<(typeof REG_TABS)[number]>(first)
  const list = useQuery({ queryKey: ['platform', 'registrations', tab], queryFn: () => api<Registration[]>(`/v1/platform/registrations?status=${tab}`), enabled: open })
  const waiting = useQuery({ queryKey: ['platform', 'registrations', first], queryFn: () => api<Registration[]>(`/v1/platform/registrations?status=${first}`) }).data?.length ?? 0
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  async function run(action: () => Promise<unknown>, done?: string) {
    setBusy(true)
    setMessage(null)
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: ['platform'] })
      if (done) setMessage(done)
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="flex flex-col gap-3 rounded-2xl bg-white p-4">
      <button onClick={() => setOpen(!open)} aria-expanded={open} className="flex min-h-11 items-center justify-between text-left">
        <h2 className="text-lg font-bold">{t('registrations.title', { count: waiting })}</h2>
        <span aria-hidden className="text-xl">{open ? '−' : '+'}</span>
      </button>
      {open && <>
        <div role="tablist" className="grid grid-cols-4 gap-1 rounded-2xl bg-ground p-1 text-xs font-semibold">
          {REG_TABS.filter((x) => review || x === 'payment').map((x) => (
            <button key={x} role="tab" aria-selected={tab === x} onClick={() => setTab(x)} className={`min-h-11 rounded-xl ${tab === x ? 'bg-teal-700 text-white' : ''}`}>{t(`registrations.tab.${x}`)}</button>
          ))}
        </div>
        {list.data?.length === 0 && <p className="text-sm text-muted">{t('agentsAdmin.none')}</p>}
        <ul className="flex flex-col gap-3">
          {list.data?.map((r) => (
            <li key={r.id} className="flex flex-col gap-2 rounded-xl border border-line p-3 text-sm">
              <span className="flex items-baseline justify-between gap-2"><b className="text-base">{r.shopName}</b><span className="font-mono text-xs">{r.agentCode} · {r.agentName}</span></span>
              {r.warnings.map((w) => <span key={w} className="rounded-lg bg-red-50 px-2 py-1 font-semibold text-red-800">⚠ {w}</span>)}
              <div className="grid grid-cols-3 gap-2">
                {r.photos.map((p) => <AuthPhoto key={p.id} path={`/v1/platform/registrations/${r.id}/photos/${p.id}`} alt={p.kind} />)}
              </div>
              <span>{t(`shopType.${r.type}`)} · {r.address} · {r.market}</span>
              <a href={`https://www.google.com/maps?q=${r.latitude},${r.longitude}`} target="_blank" rel="noreferrer" className="text-teal-700 underline">
                {t('registrations.map')}{r.accuracyMetres ? ` (±${Math.round(r.accuracyMetres)} m)` : ''}</a>
              <span>{r.ownerName} · {formatPhone(r.ownerPhone)}{r.altPhone ? ` / ${formatPhone(r.altPhone)}` : ''}{r.gstin ? ` · GSTIN ${r.gstin}` : ''}</span>
              <span className="text-xs text-muted">{t('registrations.consent', { date: formatDateTime(r.consentAt) })}</span>
              <span className="font-semibold">{r.planCode} · {t(`plan2.term.${r.term}`)} · {formatRupees(r.amount)} · {t(`agentShops.pay.${r.paymentMode}`)}{r.paymentRef ? ` ${r.paymentRef}` : ''} · {t(`agentShops.payStatus.${r.paymentStatus}`)}</span>
              {r.rejectReason && <span className="font-semibold text-red-800">{r.rejectReason}</span>}
              <div className="grid grid-cols-2 gap-2">
                {review && r.status === 'submitted' && <>
                  <button disabled={busy} className="min-h-11 rounded-xl bg-teal-700 font-bold text-white"
                    onClick={() => void run(() => api(`/v1/platform/registrations/${r.id}/approve`, 'POST', {}), t('registrations.approved', { name: r.shopName }))}>{t('registrations.approve')}</button>
                  <button disabled={busy} className="min-h-11 rounded-xl border border-line font-semibold text-red-700"
                    onClick={async () => { const reason = (await ask(t('registrations.rejectAsk'))); if (reason) void run(() => api(`/v1/platform/registrations/${r.id}/reject`, 'POST', { reason })) }}>{t('agentsAdmin.reject')}</button>
                </>}
                {money && r.status === 'approved' && r.paymentStatus === 'received' && (
                  <button disabled={busy} className="col-span-2 min-h-11 rounded-xl bg-teal-700 font-bold text-white"
                    onClick={async () => (await confirmAsk(t('registrations.confirmAsk', { amount: formatRupees(r.amount), how: t(`agentShops.pay.${r.paymentMode}`) })))
                      && void run(() => api(`/v1/platform/registrations/${r.id}/confirm-payment`, 'POST', {}), t('registrations.paid'))}>{t('registrations.confirmPay', { amount: formatRupees(r.amount) })}</button>
                )}
              </div>
            </li>
          ))}
        </ul>
        {message && <p className="rounded-xl bg-teal-50 p-3 text-sm font-semibold text-teal-900">{message}</p>}
      </>}
    </section>
  )
}

/**
 * Agent performance this month (super, agent-manager and billing admins): sign-ups, paid shops, cash each agent holds
 * for the company, and commission. Billing and super admins record commission paid, up to what is due.
 */
export function PerformancePanel({ role }: { role: AdminRole }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const rows = useQuery({ queryKey: ['platform', 'agent-performance'], queryFn: () => api<AgentPerformanceRow[]>('/v1/platform/agent-performance'), enabled: open })
  const pays = role === 'super' || role === 'billing'
  const [message, setMessage] = useState<string | null>(null)

  async function pay(r: AgentPerformanceRow) {
    const amount = (await ask(t('performance.payAsk', { name: r.name, due: formatRupees(r.commissionDue) }), String(r.commissionDue)))
    if (!amount) return
    const reference = (await ask(t('performance.refAsk', { upi: r.upiId ?? '–' }))) ?? ''
    setMessage(null)
    try {
      await api(`/v1/platform/agent-performance/${r.agentId}/payouts`, 'POST', { amount: Number(amount), reference })
      await queryClient.invalidateQueries({ queryKey: ['platform', 'agent-performance'] })
      setMessage(t('performance.paid', { name: r.name, amount: formatRupees(Number(amount)) }))
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    }
  }

  return (
    <section className="flex flex-col gap-3 rounded-2xl bg-white p-4">
      <button onClick={() => setOpen(!open)} aria-expanded={open} className="flex min-h-11 items-center justify-between text-left">
        <h2 className="text-lg font-bold">{t('performance.title')}</h2>
        <span aria-hidden className="text-xl">{open ? '−' : '+'}</span>
      </button>
      {open && <>
        <p className="text-sm text-muted">{t('performance.hint')}</p>
        {rows.data?.length === 0 && <p className="text-sm text-muted">{t('agentsAdmin.none')}</p>}
        <ul className="flex flex-col gap-2">
          {rows.data?.map((r) => (
            <li key={r.agentId} className="flex flex-col gap-1 rounded-xl border border-line p-3 text-sm">
              <span className="flex items-baseline justify-between gap-2">
                <b>{r.rankOverall > 0 ? `#${r.rankOverall} ` : ''}{r.name} {BADGE_ICON[r.thisMonth.badge]}</b>
                <span className="font-mono text-xs">{r.code}</span>
              </span>
              <span className="text-muted">{r.market} · {formatPhone(r.phone)}</span>
              <span>{t('performance.month', { submitted: r.thisMonth.submitted, approved: r.thisMonth.approved, rejected: r.thisMonth.rejected, paid: r.thisMonth.paid })}</span>
              <span className="text-muted">{t('agentHome.allTime', { shops: r.shopsAllTime, paid: r.paidShopsAllTime })}</span>
              {r.cashToDeposit > 0 && <span className="font-semibold text-amber-800">{t('performance.cash', { amount: formatRupees(r.cashToDeposit) })}</span>}
              <span>{t('performance.money', { due: formatRupees(r.commissionDue), waiting: formatRupees(r.commissionWaiting), paid: formatRupees(r.commissionPaid) })}</span>
              {pays && r.commissionDue > 0 && (
                <button onClick={() => void pay(r)} className="mt-1 min-h-11 rounded-xl bg-teal-700 font-bold text-white">{t('performance.pay', { amount: formatRupees(r.commissionDue) })}</button>
              )}
            </li>
          ))}
        </ul>
        {message && <p className="rounded-xl bg-teal-50 p-3 text-sm font-semibold text-teal-900">{message}</p>}
      </>}
    </section>
  )
}

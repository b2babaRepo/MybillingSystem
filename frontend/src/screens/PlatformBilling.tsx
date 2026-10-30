import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { FEATURES, priceFor, usePlans, type Claim, type Payment, type PlanInfo, type PlanTerm, type SubscriptionStatus } from '../lib/billing'
import { formatDateTime, formatPhone, formatRupees, whatsAppLink } from '../lib/format'
import type { AdminRole } from '../lib/types'

interface Overview { byStatus: Record<SubscriptionStatus, number>; trialsEndingThisWeek: number; revenueThisMonth: number; revenueThisYear: number; pendingClaims: Claim[] }
interface Offer { code: string; title: string; planCode: string; term: PlanTerm; price: number; newShopsOnly: boolean; validUntil: string | null; maxUses: number | null; uses: number; isActive: boolean }
interface Suggestion { id: string; shopId: string; shopName: string; kind: 'follow_up' | 'extend_trial'; reason: string; days: number | null; createdAt: string }
interface Briefing { newShopsThisWeek: number; trials: number; trialsEndingThisWeek: number; paying: number; inGrace: number; onHold: number; complimentary: number; free: number; revenueThisMonth: number; pendingClaims: number; pendingSuggestions: number; notes: string[] }
interface ShopBilling { status: SubscriptionStatus; planCode: string; endsAt: string | null; trialEndsAt: string; paidUntil: string | null; compPlanCode: string | null; compUntil: string | null; compReason: string | null; holdSince: string | null; holdReason: string | null; payments: Payment[]; claims: Claim[] }

const STATUSES: SubscriptionStatus[] = ['trial', 'active', 'grace', 'complimentary', 'on_hold', 'free']

function useRun() {
  const queryClient = useQueryClient()
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
  return { busy, message, run }
}

function Collapsible({ title, children, defaultOpen = false }: { title: string; children: React.ReactNode; defaultOpen?: boolean }) {
  const [open, setOpen] = useState(defaultOpen)
  return (
    <section className="flex flex-col gap-3 rounded-2xl bg-white p-4">
      <button onClick={() => setOpen(!open)} aria-expanded={open} className="flex min-h-11 items-center justify-between text-left">
        <h2 className="text-lg font-bold">{title}</h2>
        <span aria-hidden className="text-xl">{open ? '−' : '+'}</span>
      </button>
      {open && children}
    </section>
  )
}

/**
 * The AI admin agent's page: the week in numbers with what needs doing, and its suggestions. It only suggests;
 * approving one carries it out under your own role (extending a trial needs an approver), rejecting keeps the record.
 */
export function AgentPanel({ role }: { role: AdminRole }) {
  const { t } = useTranslation()
  const briefing = useQuery({ queryKey: ['platform', 'briefing'], queryFn: () => api<Briefing>('/v1/platform/agent/briefing') }).data
  const suggestions = useQuery({ queryKey: ['platform', 'suggestions'], queryFn: () => api<Suggestion[]>('/v1/platform/agent/suggestions') }).data ?? []
  const { busy, message, run } = useRun()

  return (
    <Collapsible title={t('agent.title', { count: suggestions.length })} defaultOpen={suggestions.length > 0}>
      {briefing && (
        <div className="flex flex-col gap-1 rounded-xl bg-ground p-3 text-sm">
          <span className="font-semibold">{t('agent.week', { newShops: briefing.newShopsThisWeek, paying: briefing.paying, trials: briefing.trials, revenue: formatRupees(briefing.revenueThisMonth) })}</span>
          {briefing.notes.map((n) => <span key={n}>• {n}</span>)}
        </div>
      )}
      {suggestions.length === 0 && <p className="text-sm text-muted">{t('agent.none')}</p>}
      <ul className="flex flex-col gap-2">
        {suggestions.map((s) => (
          <li key={s.id} className="flex flex-col gap-2 rounded-xl border border-line p-3 text-sm">
            <span><b>{s.shopName}</b> · {t(`agent.kind.${s.kind}`)}</span>
            <span>{s.reason}</span>
            <span className="text-xs text-muted">{formatDateTime(s.createdAt)}</span>
            <div className="grid grid-cols-2 gap-2">
              <button disabled={busy || (s.kind === 'extend_trial' && role !== 'super' && role !== 'approver')}
                onClick={() => void run(() => api(`/v1/platform/agent/suggestions/${s.id}/approve`, 'POST', {}), t('agent.done'))}
                className="min-h-11 rounded-xl bg-teal-700 font-bold text-white disabled:opacity-40">{s.kind === 'extend_trial' ? t('agent.extend', { days: s.days }) : t('agent.markDone')}</button>
              <button disabled={busy} onClick={() => void run(() => api(`/v1/platform/agent/suggestions/${s.id}/reject`, 'POST', {}))}
                className="min-h-11 rounded-xl border border-line font-semibold">{t('agent.dismiss')}</button>
            </div>
          </li>
        ))}
      </ul>
      {role === 'super' && (
        <button disabled={busy} onClick={() => void run(() => api('/v1/platform/agent/run', 'POST', {}), t('agent.ran'))} className="min-h-11 self-start rounded-xl border border-line px-3 text-sm font-semibold">
          {t('agent.runNow')}
        </button>
      )}
      {message && <p className="text-sm font-semibold text-teal-900">{message}</p>}
    </Collapsible>
  )
}

/** Money (billing and super admins): shops by plan status, money in, UPI payments to check, offers. */
export function BillingPanel() {
  const { t } = useTranslation()
  const overview = useQuery({ queryKey: ['platform', 'billing'], queryFn: () => api<Overview>('/v1/platform/billing') }).data
  const offers = useQuery({ queryKey: ['platform', 'offers'], queryFn: () => api<Offer[]>('/v1/platform/offers') }).data ?? []
  const plans = usePlans().data ?? []
  const { busy, message, run } = useRun()
  const [newOffer, setNewOffer] = useState({ code: '', title: '', planCode: 'basic', term: 'year1' as PlanTerm, price: '', newShopsOnly: true })

  return (
    <Collapsible title={t('billing.title', { count: overview?.pendingClaims.length ?? 0 })} defaultOpen={(overview?.pendingClaims.length ?? 0) > 0}>
      {overview && (
        <>
          <div className="grid grid-cols-3 gap-2 text-center sm:grid-cols-6">
            {STATUSES.map((s) => (
              <div key={s} className="rounded-xl bg-ground p-2"><div className="text-xl font-extrabold">{overview.byStatus[s] ?? 0}</div><div className="text-xs text-muted">{t(`plan2.status.${s}`)}</div></div>
            ))}
          </div>
          <p className="text-sm">{t('billing.money', { month: formatRupees(overview.revenueThisMonth), year: formatRupees(overview.revenueThisYear), ending: overview.trialsEndingThisWeek })}</p>

          <h3 className="font-bold">{t('billing.claims')}</h3>
          {overview.pendingClaims.length === 0 && <p className="text-sm text-muted">{t('billing.noClaims')}</p>}
          {overview.pendingClaims.map((c) => (
            <div key={c.id} className="flex flex-col gap-2 rounded-xl border border-line p-3 text-sm">
              <span><b>{c.shopName}</b> · {c.planCode} {t(`plan2.term.${c.term}`)} · <b>{formatRupees(c.amount)}</b>{c.offerCode ? ` · ${c.offerCode}` : ''}</span>
              <span className="font-mono">UTR {c.upiRef} · {formatDateTime(c.createdAt)}</span>
              <div className="grid grid-cols-2 gap-2">
                <button disabled={busy} onClick={() => void run(() => api(`/v1/platform/claims/${c.id}/confirm`, 'POST', {}), t('billing.confirmed'))}
                  className="min-h-11 rounded-xl bg-teal-700 font-bold text-white">{t('billing.confirm')}</button>
                <button disabled={busy} onClick={() => { const note = prompt(t('billing.rejectAsk')); if (note !== null) void run(() => api(`/v1/platform/claims/${c.id}/reject`, 'POST', { note })) }}
                  className="min-h-11 rounded-xl border border-line font-semibold text-red-700">{t('billing.reject')}</button>
              </div>
            </div>
          ))}
        </>
      )}

      <h3 className="font-bold">{t('billing.offers')}</h3>
      <ul className="flex flex-col gap-1 text-sm">
        {offers.map((o) => (
          <li key={o.code} className={`flex items-center justify-between gap-2 ${o.isActive ? '' : 'text-muted line-through'}`}>
            <span><b>{o.code}</b> · {o.title} · {formatRupees(o.price)} · {t('billing.used', { count: o.uses })}</span>
            <button disabled={busy} onClick={() => void run(() => api(`/v1/platform/offers/${o.code}/active?active=${!o.isActive}`, 'POST', {}))}
              className="min-h-11 rounded-xl border border-line px-3 text-xs font-semibold">{o.isActive ? t('billing.stop') : t('billing.restart')}</button>
          </li>
        ))}
      </ul>
      <div className="grid grid-cols-2 gap-2 rounded-xl bg-ground p-3 text-sm">
        <input aria-label={t('billing.code')} placeholder={t('billing.code')} value={newOffer.code} onChange={(e) => setNewOffer({ ...newOffer, code: e.target.value.toUpperCase() })} className="h-11 rounded-xl border border-line bg-white px-2" />
        <input aria-label={t('billing.price')} placeholder={t('billing.price')} value={newOffer.price} inputMode="numeric" onChange={(e) => setNewOffer({ ...newOffer, price: e.target.value.replace(/\D/g, '') })} className="h-11 rounded-xl border border-line bg-white px-2" />
        <input aria-label={t('billing.offerTitle')} placeholder={t('billing.offerTitle')} value={newOffer.title} onChange={(e) => setNewOffer({ ...newOffer, title: e.target.value })} className="col-span-2 h-11 rounded-xl border border-line bg-white px-2" />
        <select aria-label={t('billing.plan')} value={newOffer.planCode} onChange={(e) => setNewOffer({ ...newOffer, planCode: e.target.value })} className="h-11 rounded-xl border border-line bg-white px-2">
          {plans.filter((p) => p.code !== 'free' && p.isActive !== false).map((p) => <option key={p.code} value={p.code}>{p.name}</option>)}
        </select>
        <select aria-label={t('plan2.period')} value={newOffer.term} onChange={(e) => setNewOffer({ ...newOffer, term: e.target.value as PlanTerm })} className="h-11 rounded-xl border border-line bg-white px-2">
          {(['monthly', 'year1', 'year3', 'year5'] as const).map((x) => <option key={x} value={x}>{t(`plan2.term.${x}`)}</option>)}
        </select>
        <label className="col-span-2 flex min-h-11 items-center gap-2"><input type="checkbox" checked={newOffer.newShopsOnly} onChange={(e) => setNewOffer({ ...newOffer, newShopsOnly: e.target.checked })} className="h-5 w-5" />{t('billing.newOnly')}</label>
        <button disabled={busy || newOffer.code.length < 4 || !newOffer.title || !newOffer.price} className="col-span-2 min-h-11 rounded-xl bg-teal-700 font-bold text-white disabled:opacity-40"
          onClick={() => void run(() => api('/v1/platform/offers', 'POST', { ...newOffer, price: Number(newOffer.price) }), t('billing.offerAdded'))}>{t('billing.addOffer')}</button>
      </div>

      {message && <p className="text-sm font-semibold text-teal-900">{message}</p>}
    </Collapsible>
  )
}

/**
 * One shop's plan in its admin detail. Where it stands (plan, status, until when), then taking a payment as steps:
 * plan → period (with the price) → how it was paid → confirm → done, with the bill to send on WhatsApp. Rarer actions
 * (move a paid shop to another plan, free access, hold) sit under "More".
 */
export function ShopBillingPanel({ shopId, role, shopName, ownerPhone }: { shopId: string; role: AdminRole; shopName: string; ownerPhone: string | null }) {
  const { t } = useTranslation()
  const view = useQuery({ queryKey: ['platform', 'shop-billing', shopId], queryFn: () => api<ShopBilling>(`/v1/platform/shops/${shopId}/billing`) }).data
  const { busy, message, run } = useRun()
  const plans = (usePlans().data ?? []).filter((p) => p.code !== 'free' && p.isActive !== false)
  const [step, setStep] = useState<0 | 1 | 2 | 3 | 4 | 5>(0)
  const [pay, setPay] = useState({ planCode: '', term: 'year1' as PlanTerm, method: 'cash', reference: '' })
  const [done, setDone] = useState<Payment | null>(null)
  const [more, setMore] = useState(false)
  const [moveTo, setMoveTo] = useState('')
  const [compPlan, setCompPlan] = useState('pro')
  if (!view) return null
  const money = role === 'super' || role === 'billing'
  const approve = role === 'super' || role === 'approver'
  const planName = (code: string) => plans.find((p) => p.code === code)?.name ?? code
  const chosen = plans.find((p) => p.code === pay.planCode)
  const amount = chosen ? priceFor(chosen, pay.term) : null
  const paid = view.status === 'active' || view.status === 'grace'
  const date = (iso: string | null) => (iso ? new Date(iso).toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' }) : '')
  const tone: Record<string, string> = {
    active: 'bg-emerald-50 text-emerald-900', grace: 'bg-amber-50 text-amber-900', trial: 'bg-teal-50 text-teal-900',
    complimentary: 'bg-violet-50 text-violet-900', on_hold: 'bg-red-50 text-red-900', free: 'bg-white text-ink',
  }
  const stepTitle = (n: number, label: string) => (
    <span className="flex items-center gap-2 font-bold"><span className="flex h-7 w-7 items-center justify-center rounded-full bg-teal-700 text-sm text-white">{n}</span>{label}</span>
  )
  const billText = (p: Payment) => t('billing.waBill', {
    shop: shopName, invoice: p.invoiceNo, plan: planName(p.planCode), term: t(`plan2.term.${p.term}`), amount: formatRupees(p.amount),
    method: t(`billing.methods.${p.method}`), until: date(p.periodEnd),
  })

  async function record() {
    await run(async () => {
      const payment = await api<Payment>(`/v1/platform/shops/${shopId}/payments`, 'POST', { ...pay, reference: pay.reference.trim() || null })
      setDone(payment)
      setStep(5)
    })
  }

  return (
    <div className="flex flex-col gap-3 rounded-xl bg-ground p-3 text-sm">
      {/* Where the shop stands */}
      <div className={`flex flex-col gap-0.5 rounded-xl p-3 ${tone[view.status] ?? ''}`}>
        <span className="text-xs font-semibold">{t(`plan2.status.${view.status}`)}</span>
        <span className="text-xl font-extrabold">{planName(view.planCode)}</span>
        {view.endsAt && <span>{t(paid ? 'billing.paidUntil' : 'billing.until', { date: date(view.endsAt) })}</span>}
        {view.compReason && <span>{t('billing.compBy', { reason: view.compReason })}</span>}
        {view.holdReason && <span className="font-semibold">{t('plan2.holdReason', { reason: view.holdReason })}</span>}
      </div>

      {view.payments.length > 0 && (
        <ul className="flex flex-col divide-y divide-line rounded-xl bg-white px-3">
          {view.payments.map((p) => (
            <li key={p.id} className={`flex flex-wrap items-center justify-between gap-2 py-2 ${p.refundedAt ? 'text-muted line-through' : ''}`}>
              <span><b>{p.invoiceNo}</b> · {planName(p.planCode)} · {t(`plan2.term.${p.term}`)} · {t(`billing.methods.${p.method}`)}<br />
                <span className="text-xs text-muted">{date(p.paidAt)} → {date(p.periodEnd)}</span></span>
              <span className="flex items-center gap-2">
                <b>{formatRupees(p.amount)}</b>
                {!p.refundedAt && <a href={whatsAppLink(ownerPhone, billText(p))} target="_blank" rel="noreferrer" className="flex min-h-11 items-center rounded-xl bg-[#15803d] px-3 text-xs font-semibold text-white">WhatsApp</a>}
                {money && !p.refundedAt && p.term !== 'monthly' && (
                  <button disabled={busy} onClick={() => confirm(t('billing.refundAsk')) && void run(() => api(`/v1/platform/payments/${p.id}/refund`, 'POST', {}), t('billing.refunded'))}
                    className="min-h-11 rounded-xl border border-line px-2 text-xs font-semibold">{t('billing.refund')}</button>
                )}
              </span>
            </li>
          ))}
        </ul>
      )}

      {/* Taking a payment, one step at a time */}
      {money && step === 0 && (
        <button onClick={() => { setPay({ planCode: '', term: 'year1', method: 'cash', reference: '' }); setDone(null); setStep(1) }}
          className="min-h-12 rounded-xl bg-teal-700 text-base font-bold text-white">{paid ? t('billing.renew') : t('billing.takePayment')}</button>
      )}

      {money && step >= 1 && step <= 4 && (
        <div className="flex flex-col gap-3 rounded-xl bg-white p-3">
          <div className="flex gap-1" aria-hidden="true">{[1, 2, 3, 4].map((n) => <span key={n} className={`h-1.5 flex-1 rounded-full ${n <= step ? 'bg-teal-700' : 'bg-line'}`} />)}</div>

          {step === 1 && <>
            {stepTitle(1, t('billing.step.plan'))}
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
              {plans.map((p) => (
                <button key={p.code} onClick={() => { setPay({ ...pay, planCode: p.code }); setStep(2) }}
                  className={`flex flex-col items-start gap-1 rounded-xl border-2 p-3 text-left ${pay.planCode === p.code ? 'border-teal-700 bg-teal-50' : 'border-line'}`}>
                  <span className="text-lg font-extrabold">{p.name}</span>
                  <span className="font-semibold">{formatRupees(p.priceYear1 ?? 0)}/{t('plan2.year')}</span>
                  <span className="text-xs text-muted">{p.features.map((f) => t(`plan2.feature.${f}`)).join(' · ')}</span>
                </button>
              ))}
            </div>
          </>}

          {step === 2 && chosen && <>
            {stepTitle(2, t('billing.step.period', { plan: chosen.name }))}
            <div className="grid grid-cols-3 gap-2">
              {(['year1', 'year3', 'year5'] as const).map((x) => {
                const price = priceFor(chosen, x)
                return price === null ? null : (
                  <button key={x} onClick={() => { setPay({ ...pay, term: x }); setStep(3) }}
                    className={`flex min-h-16 flex-col items-center justify-center rounded-xl border-2 font-semibold ${pay.term === x ? 'border-teal-700 bg-teal-50' : 'border-line'}`}>
                    <span>{t(`plan2.term.${x}`)}</span>
                    <span className="text-lg font-extrabold">{formatRupees(price)}</span>
                    {x === 'year5' && <span className="text-xs text-muted">{t('billing.perYear5')}</span>}
                  </button>
                )
              })}
            </div>
          </>}

          {step === 3 && <>
            {stepTitle(3, t('billing.step.how', { amount: amount !== null ? formatRupees(amount) : '' }))}
            <div className="grid grid-cols-3 gap-2">
              {['cash', 'upi', 'bank'].map((m) => (
                <button key={m} onClick={() => setPay({ ...pay, method: m })}
                  className={`min-h-12 rounded-xl border-2 font-semibold ${pay.method === m ? 'border-teal-700 bg-teal-50' : 'border-line'}`}>{t(`billing.methods.${m}`)}</button>
              ))}
            </div>
            <label className="flex flex-col gap-1"><span className="text-xs text-muted">{pay.method === 'cash' ? t('billing.receiptOptional') : t('billing.utrNeeded')}</span>
              <input value={pay.reference} onChange={(e) => setPay({ ...pay, reference: e.target.value })} className="h-12 rounded-xl border border-line px-3 text-base" /></label>
            <button disabled={pay.method !== 'cash' && pay.reference.trim().length < 6} onClick={() => setStep(4)}
              className="min-h-12 rounded-xl bg-teal-700 font-bold text-white disabled:opacity-40">{t('billing.next')}</button>
          </>}

          {step === 4 && chosen && amount !== null && <>
            {stepTitle(4, t('billing.step.confirm'))}
            <dl className="grid grid-cols-2 gap-x-3 gap-y-1 rounded-xl bg-ground p-3">
              <dt className="text-muted">{t('billing.shop')}</dt><dd className="font-semibold">{shopName}</dd>
              <dt className="text-muted">{t('billing.plan')}</dt><dd className="font-semibold">{chosen.name} · {t(`plan2.term.${pay.term}`)}</dd>
              <dt className="text-muted">{t('billing.method')}</dt><dd className="font-semibold">{t(`billing.methods.${pay.method}`)}{pay.reference.trim() ? ` · ${pay.reference.trim()}` : ''}</dd>
              <dt className="text-muted">{t('plan2.toPay')}</dt><dd className="text-xl font-extrabold">{formatRupees(amount)}</dd>
            </dl>
            <p className="text-xs text-muted">{paid ? t('billing.addsOn', { date: date(view.endsAt) }) : t('billing.startsToday')}</p>
            <button disabled={busy} onClick={() => void record()} className="min-h-12 rounded-xl bg-teal-700 text-base font-bold text-white">{t('billing.recordAmount', { amount: formatRupees(amount) })}</button>
          </>}

          <div className="flex justify-between">
            <button onClick={() => setStep(step === 1 ? 0 : ((step - 1) as 1 | 2 | 3))} className="min-h-11 px-2 font-semibold text-teal-700">{step === 1 ? t('plans.cancel') : t('billing.back')}</button>
          </div>
        </div>
      )}

      {step === 5 && done && (
        <div className="flex flex-col gap-2 rounded-xl bg-emerald-50 p-3 text-emerald-900">
          {stepTitle(5, t('billing.step.done'))}
          <span>{t('billing.doneLine', { invoice: done.invoiceNo, plan: planName(done.planCode), amount: formatRupees(done.amount), until: date(done.periodEnd) })}</span>
          <a href={whatsAppLink(ownerPhone, billText(done))} target="_blank" rel="noreferrer"
            className="flex min-h-12 items-center justify-center rounded-xl bg-[#15803d] font-bold text-white">{t('billing.sendWa', { phone: ownerPhone ? formatPhone(ownerPhone) : '' })}</a>
          <button onClick={() => setStep(0)} className="min-h-11 font-semibold">{t('billing.close')}</button>
        </div>
      )}

      {/* Rarer actions */}
      {(money || approve) && (
        <button onClick={() => setMore(!more)} aria-expanded={more} className="min-h-11 self-start font-semibold text-teal-700">{more ? '−' : '+'} {t('billing.more')}</button>
      )}
      {more && (
        <div className="grid grid-cols-2 gap-2">
          {money && paid && <>
            <select aria-label={t('plans.moveTo')} value={moveTo} onChange={(e) => setMoveTo(e.target.value)} className="h-11 rounded-xl border border-line bg-white px-2">
              <option value="">{t('plans.moveTo')}</option>
              {plans.filter((p) => p.code !== view.planCode).map((p) => <option key={p.code} value={p.code}>{p.name}</option>)}
            </select>
            <button disabled={busy || !moveTo} className="min-h-11 rounded-xl border border-line bg-white font-semibold disabled:opacity-40"
              onClick={() => void run(async () => { await api(`/v1/platform/shops/${shopId}/plan`, 'POST', { planCode: moveTo }); setMoveTo('') }, t('plans.moved'))}>{t('plans.move')}</button>
          </>}
          {approve && <>
            <select aria-label={t('plans.freePlan')} value={compPlan} onChange={(e) => setCompPlan(e.target.value)} className="col-span-2 h-11 rounded-xl border border-line bg-white px-2">
              {plans.map((p) => <option key={p.code} value={p.code}>{t('plans.freeOn', { plan: p.name })}</option>)}
            </select>
            <button disabled={busy} className="min-h-11 rounded-xl border border-line bg-white font-semibold" onClick={() => {
              const months = prompt(t('billing.compAsk', { max: role === 'super' ? t('billing.anyLength') : 3 }))
              if (months === null) return
              const reason = prompt(t('billing.compReasonAsk'))
              if (reason) void run(() => api(`/v1/platform/shops/${shopId}/complimentary`, 'POST', { planCode: compPlan, months: months.trim() ? Number(months) : null, reason }), t('billing.compGiven'))
            }}>{t('billing.giveFree')}</button>
            {view.holdSince
              ? <button disabled={busy} className="min-h-11 rounded-xl bg-teal-700 font-bold text-white" onClick={() => void run(() => api(`/v1/platform/shops/${shopId}/release`, 'POST', {}))}>{t('billing.release')}</button>
              : <button disabled={busy} className="min-h-11 rounded-xl border border-line bg-white font-semibold text-red-700" onClick={() => { const reason = prompt(t('billing.holdAsk')); if (reason) void run(() => api(`/v1/platform/shops/${shopId}/hold`, 'POST', { reason })) }}>{t('billing.hold')}</button>}
            {view.compPlanCode && <button disabled={busy} className="col-span-2 min-h-11 rounded-xl border border-line bg-white text-xs font-semibold" onClick={() => void run(() => api(`/v1/platform/shops/${shopId}/complimentary`, 'DELETE'))}>{t('billing.endFree')}</button>}
          </>}
        </div>
      )}
      {message && <p className="font-semibold text-teal-900">{message}</p>}
    </div>
  )
}

interface Activity { id: string; at: string; admin: string; action: string; shopId: string | null; shopName: string | null; detail: Record<string, unknown> }

/** Route → what it means, for the activity list ("POST /shops/{id:guid}/block" → "Blocked shop"). */
const ACTIONS: [RegExp, string][] = [
  [/suggestions.*approve$/, 'agentApprove'], [/suggestions.*reject$/, 'agentReject'], [/agent\/run$/, 'agentRun'],
  [/^POST \/shops$/, 'openShop'], [/approve$/, 'approve'], [/\/block$/, 'block'], [/unblock$/, 'unblock'], [/note$/, 'note'],
  [/unlock$/, 'unlock'], [/^POST \/admins/, 'addAdmin'], [/^PUT \/admins/, 'changeAdmin'], [/^DELETE \/admins/, 'removeAdmin'],
  [/confirm$/, 'confirmClaim'], [/claims.*reject$/, 'rejectClaim'], [/payments$/, 'recordPayment'], [/refund$/, 'refund'],
  [/^POST .*complimentary$/, 'giveFree'], [/^DELETE .*complimentary$/, 'endFree'], [/hold$/, 'hold'], [/release$/, 'release'],
  [/plans/, 'planPrice'], [/^POST \/offers$/, 'addOffer'], [/offers.*active$/, 'offerOnOff'],
]

/** Super admins: everything any admin changed, newest first. Written by the server; nobody can edit it. */
export function ActivityPanel() {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const rows = useQuery({ queryKey: ['platform', 'activity'], queryFn: () => api<Activity[]>('/v1/platform/activity'), enabled: open })
  const label = (action: string) => {
    // Unblock and unlock also end in "block"/"lock": match the longer ones first.
    const key = action.endsWith('unblock') ? 'unblock' : action.endsWith('unlock') ? 'unlock' : ACTIONS.find(([re]) => re.test(action))?.[1]
    return key ? t(`activity.actions.${key}`) : action
  }
  const said = (d: Record<string, unknown>) => {
    const body = (d.body ?? {}) as Record<string, unknown>
    return Object.entries(body).filter(([, v]) => v !== null && v !== '' && typeof v !== 'object').map(([k, v]) => `${k}: ${String(v)}`).join(' · ')
  }

  return (
    <section className="flex flex-col gap-3 rounded-2xl bg-white p-4">
      <button onClick={() => setOpen(!open)} aria-expanded={open} className="flex min-h-11 items-center justify-between text-left">
        <h2 className="text-lg font-bold">{t('activity.title')}</h2>
        <span aria-hidden className="text-xl">{open ? '−' : '+'}</span>
      </button>
      {open && <>
        <p className="text-sm text-muted">{t('activity.hint')}</p>
        {rows.data?.length === 0 && <p className="text-sm text-muted">{t('activity.none')}</p>}
        <ul className="flex flex-col divide-y divide-[#edf2f1] text-sm">
          {rows.data?.map((r) => (
            <li key={r.id} className="flex flex-col py-2">
              <span className="font-semibold">{label(r.action)}{r.shopName ? ` · ${r.shopName}` : ''}</span>
              <span className="text-xs text-muted">{r.admin} · {formatDateTime(r.at)}</span>
              {said(r.detail) && <span className="text-xs">{said(r.detail)}</span>}
            </li>
          ))}
        </ul>
      </>}
    </section>
  )
}

type PlanDraft = { name: string; priceMonthly: string; priceYear1: string; priceYear3: string; priceYear5: string; maxCustomers: string; maxStaff: string; whatsAppPerMonth: string; features: string[] }
const PRICE_KEYS = ['priceMonthly', 'priceYear1', 'priceYear3', 'priceYear5'] as const
const blank: PlanDraft = { name: '', priceMonthly: '', priceYear1: '', priceYear3: '', priceYear5: '', maxCustomers: '', maxStaff: '0', whatsAppPerMonth: '0', features: [] }
const toDraft = (p: PlanInfo): PlanDraft => ({
  name: p.name, priceMonthly: String(p.priceMonthly ?? ''), priceYear1: String(p.priceYear1 ?? ''), priceYear3: String(p.priceYear3 ?? ''), priceYear5: String(p.priceYear5 ?? ''),
  maxCustomers: String(p.maxCustomers ?? ''), maxStaff: String(p.maxStaff), whatsAppPerMonth: String(p.whatsAppPerMonth), features: p.features,
})
const toBody = (d: PlanDraft) => {
  const num = (v: string) => (v.trim() === '' ? null : Number(v))
  return {
    name: d.name.trim(), priceMonthly: num(d.priceMonthly), priceYear1: num(d.priceYear1), priceYear3: num(d.priceYear3), priceYear5: num(d.priceYear5),
    maxCustomers: num(d.maxCustomers), maxStaff: Number(d.maxStaff || 0), whatsAppPerMonth: Number(d.whatsAppPerMonth || 0), features: d.features,
  }
}

/** The fields of one plan: name, prices for each period, limits and what it opens. */
function PlanFields({ draft, onChange, disabled, free }: { draft: PlanDraft; onChange: (d: PlanDraft) => void; disabled: boolean; free?: boolean }) {
  const { t } = useTranslation()
  const digits = (k: keyof PlanDraft) => (e: { target: { value: string } }) => onChange({ ...draft, [k]: e.target.value.replace(/\D/g, '') })
  const box = 'h-11 rounded-xl border border-line bg-white px-2 text-sm text-ink'
  return (
    <div className="flex flex-col gap-2">
      <label className="flex flex-col text-xs text-muted">{t('plans.name')}
        <input value={draft.name} disabled={disabled} onChange={(e) => onChange({ ...draft, name: e.target.value })} className={box} /></label>
      {!free && (
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          {PRICE_KEYS.map((k) => (
            <label key={k} className="flex flex-col text-xs text-muted">{t(`billing.${k}`)} ₹
              <input value={draft[k]} disabled={disabled} onChange={digits(k)} inputMode="numeric" className={box} /></label>
          ))}
        </div>
      )}
      <div className="grid grid-cols-3 gap-2">
        <label className="flex flex-col text-xs text-muted">{t('plans.customers')}
          <input value={draft.maxCustomers} disabled={disabled} onChange={digits('maxCustomers')} inputMode="numeric" placeholder={t('plans.noLimit')} className={box} /></label>
        <label className="flex flex-col text-xs text-muted">{t('plans.helpers')}
          <input value={draft.maxStaff} disabled={disabled} onChange={digits('maxStaff')} inputMode="numeric" className={box} /></label>
        <label className="flex flex-col text-xs text-muted">{t('plans.whatsapp')}
          <input value={draft.whatsAppPerMonth} disabled={disabled} onChange={digits('whatsAppPerMonth')} inputMode="numeric" className={box} /></label>
      </div>
      {!free && (
        <div className="flex flex-wrap gap-2">
          {FEATURES.map((f) => {
            const on = draft.features.includes(f)
            return (
              <label key={f} className={`flex min-h-11 items-center gap-2 rounded-xl border px-3 text-sm ${on ? 'border-teal-700 bg-teal-50 font-semibold' : 'border-line'}`}>
                <input type="checkbox" checked={on} disabled={disabled} className="h-5 w-5"
                  onChange={() => onChange({ ...draft, features: on ? draft.features.filter((x) => x !== f) : [...draft.features, f] })} />
                {t(`plan2.feature.${f}`)}
              </label>
            )
          })}
        </div>
      )}
    </div>
  )
}

/**
 * Plans (super and billing admins): Basic, Silver, Gold and any added later. Change prices, limits and what each
 * opens; take a plan off sale (shops on it keep it until their period ends); add a new one. Prices apply to payments
 * from now on; past payments don't change.
 */
export function PlansPanel() {
  const { t } = useTranslation()
  const plans = usePlans().data ?? []
  const { busy, message, run } = useRun()
  const [drafts, setDrafts] = useState<Record<string, PlanDraft>>({})
  const [adding, setAdding] = useState<PlanDraft | null>(null)

  return (
    <Collapsible title={t('plans.title')}>
      <p className="text-sm text-muted">{t('plans.hint')}</p>
      {plans.map((p) => {
        const draft = drafts[p.code]
        return (
          <div key={p.code} className={`flex flex-col gap-2 rounded-xl border p-3 ${p.isActive === false ? 'border-dashed border-line opacity-70' : 'border-line'}`}>
            <div className="flex items-center justify-between gap-2">
              <span className="text-lg font-extrabold">{p.name}{p.code !== 'free' && p.priceYear1 ? ` · ${formatRupees(p.priceYear1)}/${t('plan2.year')}` : ''}</span>
              <span className="text-xs text-muted">{t('plans.shops', { count: p.shops ?? 0 })}{p.isActive === false ? ` · ${t('plans.offSale')}` : ''}</span>
            </div>
            {draft ? (
              <>
                <PlanFields draft={draft} onChange={(d) => setDrafts({ ...drafts, [p.code]: d })} disabled={busy} free={p.code === 'free'} />
                <div className="grid grid-cols-2 gap-2">
                  <button disabled={busy} className="min-h-11 rounded-xl bg-teal-700 font-bold text-white" onClick={() => void run(async () => {
                    await api(`/v1/platform/plans/${p.code}`, 'PUT', toBody(draft))
                    const next = { ...drafts }
                    delete next[p.code]
                    setDrafts(next)
                  }, t('billing.planSaved'))}>{t('billing.savePlan')}</button>
                  <button disabled={busy} className="min-h-11 rounded-xl border border-line font-semibold" onClick={() => { const next = { ...drafts }; delete next[p.code]; setDrafts(next) }}>{t('plans.cancel')}</button>
                </div>
              </>
            ) : (
              <>
                <span className="text-sm">
                  {p.code === 'free' ? t('plan2.freeIncludes') : p.features.map((f) => t(`plan2.feature.${f}`)).join(' · ')}
                </span>
                <span className="text-xs text-muted">
                  {p.maxCustomers ? t('plan2.customersUpTo', { count: p.maxCustomers }) : t('plan2.customersUnlimited')}
                  {` · ${t('plan2.staff', { count: p.maxStaff })} · ${t('plan2.whatsapp', { count: p.whatsAppPerMonth })}`}
                </span>
                <div className="grid grid-cols-2 gap-2">
                  <button disabled={busy} className="min-h-11 rounded-xl border border-line font-semibold" onClick={() => setDrafts({ ...drafts, [p.code]: toDraft(p) })}>{t('plans.edit')}</button>
                  {p.code !== 'free' && (
                    <button disabled={busy} className="min-h-11 rounded-xl border border-line font-semibold"
                      onClick={() => void run(() => api(`/v1/platform/plans/${p.code}/active?active=${p.isActive === false}`, 'POST', {}))}>
                      {p.isActive === false ? t('plans.putOnSale') : t('plans.takeOff')}
                    </button>
                  )}
                </div>
              </>
            )}
          </div>
        )
      })}

      {adding ? (
        <div className="flex flex-col gap-2 rounded-xl bg-ground p-3">
          <span className="font-bold">{t('plans.new')}</span>
          <PlanFields draft={adding} onChange={setAdding} disabled={busy} />
          <div className="grid grid-cols-2 gap-2">
            <button disabled={busy || adding.name.trim().length < 2 || !adding.priceYear1} className="min-h-11 rounded-xl bg-teal-700 font-bold text-white disabled:opacity-40"
              onClick={() => void run(async () => { await api('/v1/platform/plans', 'POST', toBody(adding)); setAdding(null) }, t('plans.added'))}>{t('plans.add')}</button>
            <button disabled={busy} className="min-h-11 rounded-xl border border-line font-semibold" onClick={() => setAdding(null)}>{t('plans.cancel')}</button>
          </div>
        </div>
      ) : (
        <button onClick={() => setAdding(blank)} className="min-h-11 rounded-xl border-2 border-dashed border-line font-semibold">+ {t('plans.new')}</button>
      )}
      {message && <p className="text-sm font-semibold text-teal-900">{message}</p>}
    </Collapsible>
  )
}

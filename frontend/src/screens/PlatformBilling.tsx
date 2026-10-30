import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import type { Claim, Payment, PlanInfo, PlanTerm, SubscriptionStatus } from '../lib/billing'
import { formatDateTime, formatRupees } from '../lib/format'
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

/** Money (billing and super admins): shops by plan status, money in, UPI payments to check, offers, plan prices. */
export function BillingPanel({ role }: { role: AdminRole }) {
  const { t } = useTranslation()
  const overview = useQuery({ queryKey: ['platform', 'billing'], queryFn: () => api<Overview>('/v1/platform/billing') }).data
  const offers = useQuery({ queryKey: ['platform', 'offers'], queryFn: () => api<Offer[]>('/v1/platform/offers') }).data ?? []
  const plans = useQuery({ queryKey: ['platform', 'plans'], queryFn: () => api<PlanInfo[]>('/v1/platform/plans') }).data ?? []
  const { busy, message, run } = useRun()
  const [newOffer, setNewOffer] = useState({ code: '', title: '', planCode: 'basic', term: 'year1' as PlanTerm, price: '', newShopsOnly: true })
  const [editing, setEditing] = useState<Record<string, Record<string, string>>>({})

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
          <option value="basic">Basic</option><option value="pro">Pro</option>
        </select>
        <select aria-label={t('plan2.period')} value={newOffer.term} onChange={(e) => setNewOffer({ ...newOffer, term: e.target.value as PlanTerm })} className="h-11 rounded-xl border border-line bg-white px-2">
          {(['monthly', 'year1', 'year3', 'year5'] as const).map((x) => <option key={x} value={x}>{t(`plan2.term.${x}`)}</option>)}
        </select>
        <label className="col-span-2 flex min-h-11 items-center gap-2"><input type="checkbox" checked={newOffer.newShopsOnly} onChange={(e) => setNewOffer({ ...newOffer, newShopsOnly: e.target.checked })} className="h-5 w-5" />{t('billing.newOnly')}</label>
        <button disabled={busy || newOffer.code.length < 4 || !newOffer.title || !newOffer.price} className="col-span-2 min-h-11 rounded-xl bg-teal-700 font-bold text-white disabled:opacity-40"
          onClick={() => void run(() => api('/v1/platform/offers', 'POST', { ...newOffer, price: Number(newOffer.price) }), t('billing.offerAdded'))}>{t('billing.addOffer')}</button>
      </div>

      <h3 className="font-bold">{t('billing.plans')}</h3>
      {plans.map((p) => {
        const draft = editing[p.code]
        const field = (key: keyof PlanInfo) => draft?.[key] ?? String(p[key] ?? '')
        const set = (key: string, value: string) => setEditing({ ...editing, [p.code]: { ...(editing[p.code] ?? {}), [key]: value.replace(/\D/g, '') } })
        return (
          <div key={p.code} className="flex flex-col gap-2 rounded-xl border border-line p-3 text-sm">
            <span className="font-bold">{p.name}</span>
            <div className="grid grid-cols-4 gap-2">
              {(['priceMonthly', 'priceYear1', 'priceYear3', 'priceYear5'] as const).map((k) => (
                <label key={k} className="flex flex-col text-xs text-muted">{t(`billing.${k}`)}
                  <input value={field(k)} disabled={role !== 'super' || p.code === 'free'} onChange={(e) => set(k, e.target.value)} inputMode="numeric" className="h-11 rounded-xl border border-line px-2 text-sm text-ink" />
                </label>
              ))}
            </div>
            {role === 'super' && draft && (
              <button disabled={busy} className="min-h-11 rounded-xl bg-teal-700 font-bold text-white" onClick={() => void run(async () => {
                const num = (k: keyof PlanInfo) => { const v = field(k); return v === '' ? null : Number(v) }
                await api(`/v1/platform/plans/${p.code}`, 'PUT', {
                  name: p.name, priceMonthly: num('priceMonthly'), priceYear1: num('priceYear1'), priceYear3: num('priceYear3'), priceYear5: num('priceYear5'),
                  features: p.features, maxCustomers: p.maxCustomers, maxStaff: p.maxStaff, whatsAppPerMonth: p.whatsAppPerMonth,
                })
                setEditing({ ...editing, [p.code]: undefined as never })
              }, t('billing.planSaved'))}>{t('billing.savePlan')}</button>
            )}
          </div>
        )
      })}
      {message && <p className="text-sm font-semibold text-teal-900">{message}</p>}
    </Collapsible>
  )
}

/** One shop's plan in its admin detail: status and dates, payments, and what this admin's role may do. */
export function ShopBillingPanel({ shopId, role }: { shopId: string; role: AdminRole }) {
  const { t } = useTranslation()
  const view = useQuery({ queryKey: ['platform', 'shop-billing', shopId], queryFn: () => api<ShopBilling>(`/v1/platform/shops/${shopId}/billing`) }).data
  const { busy, message, run } = useRun()
  const [pay, setPay] = useState({ planCode: 'basic', term: 'year1' as PlanTerm, method: 'cash', reference: '' })
  if (!view) return null
  const money = role === 'super' || role === 'billing'
  const approve = role === 'super' || role === 'approver'

  return (
    <div className="flex flex-col gap-2 rounded-xl bg-ground p-3 text-sm">
      <span className="font-bold">{t('billing.shopPlan')}: {t(`plan2.status.${view.status}`)} · {view.planCode}{view.endsAt ? ` · ${t('billing.until', { date: new Date(view.endsAt).toLocaleDateString('en-IN') })}` : ''}</span>
      {view.compReason && <span>{t('billing.compBy', { reason: view.compReason })}</span>}
      {view.holdReason && <span className="text-red-800">{t('plan2.holdReason', { reason: view.holdReason })}</span>}
      {view.payments.map((p) => (
        <div key={p.id} className={`flex items-center justify-between gap-2 ${p.refundedAt ? 'text-muted line-through' : ''}`}>
          <span>{p.invoiceNo} · {formatRupees(p.amount)} · {t(`plan2.term.${p.term}`)} · {p.method}</span>
          {money && !p.refundedAt && p.term !== 'monthly' && (
            <button disabled={busy} onClick={() => confirm(t('billing.refundAsk')) && void run(() => api(`/v1/platform/payments/${p.id}/refund`, 'POST', {}), t('billing.refunded'))}
              className="min-h-11 rounded-xl border border-line px-2 text-xs font-semibold">{t('billing.refund')}</button>
          )}
        </div>
      ))}

      {money && (
        <div className="grid grid-cols-2 gap-2">
          <select aria-label={t('billing.plan')} value={pay.planCode} onChange={(e) => setPay({ ...pay, planCode: e.target.value })} className="h-11 rounded-xl border border-line bg-white px-2"><option value="basic">Basic</option><option value="pro">Pro</option></select>
          <select aria-label={t('plan2.period')} value={pay.term} onChange={(e) => setPay({ ...pay, term: e.target.value as PlanTerm })} className="h-11 rounded-xl border border-line bg-white px-2">
            {(['monthly', 'year1', 'year3', 'year5'] as const).map((x) => <option key={x} value={x}>{t(`plan2.term.${x}`)}</option>)}
          </select>
          <select aria-label={t('billing.method')} value={pay.method} onChange={(e) => setPay({ ...pay, method: e.target.value })} className="h-11 rounded-xl border border-line bg-white px-2">
            {['cash', 'upi', 'bank'].map((m) => <option key={m} value={m}>{t(`billing.methods.${m}`)}</option>)}
          </select>
          <input aria-label={t('billing.reference')} placeholder={t('billing.reference')} value={pay.reference} onChange={(e) => setPay({ ...pay, reference: e.target.value })} className="h-11 rounded-xl border border-line bg-white px-2" />
          <button disabled={busy} className="col-span-2 min-h-11 rounded-xl bg-teal-700 font-bold text-white"
            onClick={() => void run(() => api(`/v1/platform/shops/${shopId}/payments`, 'POST', pay), t('billing.recorded'))}>{t('billing.record')}</button>
        </div>
      )}

      {approve && (
        <div className="grid grid-cols-2 gap-2">
          <button disabled={busy} className="min-h-11 rounded-xl border border-line font-semibold" onClick={() => {
            const months = prompt(t('billing.compAsk', { max: role === 'super' ? t('billing.anyLength') : 3 }))
            if (months === null) return
            const reason = prompt(t('billing.compReasonAsk'))
            if (reason) void run(() => api(`/v1/platform/shops/${shopId}/complimentary`, 'POST', { planCode: 'pro', months: months.trim() ? Number(months) : null, reason }), t('billing.compGiven'))
          }}>{t('billing.giveFree')}</button>
          {view.holdSince
            ? <button disabled={busy} className="min-h-11 rounded-xl bg-teal-700 font-bold text-white" onClick={() => void run(() => api(`/v1/platform/shops/${shopId}/release`, 'POST', {}))}>{t('billing.release')}</button>
            : <button disabled={busy} className="min-h-11 rounded-xl border border-line font-semibold text-red-700" onClick={() => { const reason = prompt(t('billing.holdAsk')); if (reason) void run(() => api(`/v1/platform/shops/${shopId}/hold`, 'POST', { reason })) }}>{t('billing.hold')}</button>}
          {view.compPlanCode && <button disabled={busy} className="col-span-2 min-h-11 rounded-xl border border-line text-xs font-semibold" onClick={() => void run(() => api(`/v1/platform/shops/${shopId}/complimentary`, 'DELETE'))}>{t('billing.endFree')}</button>}
        </div>
      )}
      {message && <p className="font-semibold text-teal-900">{message}</p>}
    </div>
  )
}

import { btn } from '../lib/buttonStyles'
import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { FEATURES, priceFor, usePlans, type Claim, type Payment, type PlanInfo, type PlanTerm, type SubscriptionStatus } from '../lib/billing'
import { formatDateTime, formatPhone, formatRupees, whatsAppLink } from '../lib/format'
import type { Can } from '../lib/platformAccess'
import { planBillLink } from '../lib/planBill'
import { ask, confirmAsk } from '../lib/ask'

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

/** An admin section: open when the page opens (the section is the page now), and can be folded away. */
function Collapsible({ title, children }: { title: string; children: React.ReactNode }) {
  const [open, setOpen] = useState(true)
  return (
    <section className="flex flex-col gap-3 rounded-2xl bg-white p-4">
      <button onClick={() => setOpen(!open)} aria-expanded={open} className="flex min-h-11 items-center justify-between text-left">
        <h2 className="text-lg font-bold">{title}</h2>
        <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={`shrink-0 text-muted transition ${open ? 'rotate-180' : ''}`}><path d="M6 9l6 6 6-6" /></svg>
      </button>
      {open && children}
    </section>
  )
}

interface CustomerUsage { days: number; cartsStarted: number; checkoutsOpened: number; storeOrders: number; listOrders: number; cartToOrderPercent: number | null; checkoutToOrderPercent: number | null; changesAnswered: number; medianAnswerMinutes: number | null; languages: { en: number; hi: number; gu: number; hn: number } }

/** How customers use the shopping app: where they stop between cart and order, how fast they answer a shop's change, which language they read. */
export function CustomerUsagePanel() {
  const { t } = useTranslation()
  const [days, setDays] = useState(30)
  const data = useQuery({ queryKey: ['platform', 'customer-usage', days], queryFn: () => api<CustomerUsage>(`/v1/platform/customer-usage?days=${days}`) }).data
  const pct = (n: number | null) => (n === null ? '–' : `${n}%`)
  const tile = (label: string, value: string | number, hint?: string) => (
    <div className="flex min-h-[84px] flex-col justify-between rounded-[18px] bg-[#f1f5f4] p-3">
      <span className="text-[13px] font-bold text-muted">{label}</span>
      <b className="text-2xl tabular-nums">{value}</b>
      {hint && <span className="text-xs text-muted">{hint}</span>}
    </div>
  )
  const langs = data ? [['English', data.languages.en], ['हिंदी', data.languages.hi], ['ગુજરાતી', data.languages.gu], ['Hinglish', data.languages.hn]] as const : []
  const totalLang = langs.reduce((s, [, n]) => s + n, 0)
  return (
    <Collapsible title={t('usage.title')}>
      <div className="flex gap-2">
        {[7, 30, 90].map((d) => (
          <button key={d} onClick={() => setDays(d)} aria-pressed={days === d} className={`min-h-10 rounded-full px-4 text-sm font-bold ${days === d ? 'bg-brand text-white' : 'bg-[#f1f5f4]'}`}>{t('usage.days', { count: d })}</button>
        ))}
      </div>
      {!data ? <p className="text-muted">…</p> : <>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          {tile(t('usage.carts'), data.cartsStarted)}
          {tile(t('usage.checkouts'), data.checkoutsOpened)}
          {tile(t('usage.storeOrders'), data.storeOrders, t('usage.listOrders', { count: data.listOrders }))}
          {tile(t('usage.conversion'), pct(data.cartToOrderPercent), t('usage.fromCheckout', { pct: pct(data.checkoutToOrderPercent) }))}
          {tile(t('usage.answers'), data.changesAnswered)}
          {tile(t('usage.answerTime'), data.medianAnswerMinutes === null ? '–' : t('usage.minutes', { count: data.medianAnswerMinutes }))}
        </div>
        <div className="flex flex-col gap-1.5">
          <h3 className="text-sm font-bold text-muted">{t('usage.languages')}</h3>
          {langs.map(([name, n]) => (
            <div key={name} className="flex items-center gap-2 text-sm">
              <span className="w-20 shrink-0">{name}</span>
              <span className="h-3 flex-1 overflow-hidden rounded-full bg-[#f1f5f4]"><span className="block h-full rounded-full bg-brand" style={{ width: `${totalLang ? (100 * n) / totalLang : 0}%` }} /></span>
              <span className="w-10 text-right tabular-nums">{n}</span>
            </div>
          ))}
        </div>
        <p className="text-xs text-muted">{t('usage.note')}</p>
      </>}
    </Collapsible>
  )
}

/**
 * The AI admin agent's page: the week in numbers with what needs doing, and its suggestions. It only suggests;
 * approving one carries it out under your own role (extending a trial needs an approver), rejecting keeps the record.
 */
export function AgentPanel({ can }: { can: Can }) {
  const { t } = useTranslation()
  const briefing = useQuery({ queryKey: ['platform', 'briefing'], queryFn: () => api<Briefing>('/v1/platform/agent/briefing') }).data
  const suggestions = useQuery({ queryKey: ['platform', 'suggestions'], queryFn: () => api<Suggestion[]>('/v1/platform/agent/suggestions') }).data ?? []
  const { busy, message, run } = useRun()

  return (
    <Collapsible title={t('agent.title', { count: suggestions.length })}>
      {can('admins.manage') && (
        <button disabled={busy} onClick={() => void run(() => api('/v1/platform/agent/run', 'POST', {}), t('agent.ran'))} className={`flex min-h-11 items-center gap-2 self-end rounded-xl px-4 -mt-1 ${btn.outline}`}>
          <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M4 12a8 8 0 0114-5l2 2M20 4v5h-5M20 12a8 8 0 01-14 5l-2-2M4 20v-5h5" /></svg>{t('agent.runNow')}
        </button>
      )}
      {briefing && (
        <div className="flex flex-col gap-3 rounded-[20px] bg-[#13241f] p-4 text-white">
          <span className="flex items-center gap-2 font-bold"><span aria-hidden className="flex h-8 w-8 items-center justify-center rounded-full bg-white/10"><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M12 3l2 5 5 2-5 2-2 5-2-5-5-2 5-2z" /></svg></span>{t('plat5.week')}</span>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            {([[t('plat5.newShops'), briefing.newShopsThisWeek], [t('plat5.paying'), briefing.paying], [t('plat5.onTrial'), briefing.trials], [t('plat5.thisMonth'), formatRupees(briefing.revenueThisMonth)]] as [string, string | number][]).map(([label, value]) => (
              <div key={label} className="rounded-2xl bg-white/10 p-3"><span className="text-xs text-[#9fbab2]">{label}</span><b className="block text-2xl">{value}</b></div>
            ))}
          </div>
          {briefing.notes.map((n) => <p key={n} className="flex items-center gap-2 rounded-xl bg-amber-400 px-3 py-2 text-sm font-bold text-amber-950"><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M5 4h4l2 5-2.5 1.5a11 11 0 005 5L15 13l5 2v4a2 2 0 01-2 2A16 16 0 013 6a2 2 0 012-2" /></svg>{n}</p>)}
        </div>
      )}
      {suggestions.length === 0 && <p className="text-sm text-muted">{t('agent.none')}</p>}
      <ul className="flex flex-col gap-2">
        {suggestions.map((s) => (
          <li key={s.id} className="flex flex-wrap items-start gap-3 rounded-2xl border border-line bg-white p-4 text-sm shadow-sm">
            <span aria-hidden className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-[#fdf1e3] text-[#a1460a]"><svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M5 4h4l2 5-2.5 1.5a11 11 0 005 5L15 13l5 2v4a2 2 0 01-2 2A16 16 0 013 6a2 2 0 012-2" /></svg></span>
            <span className="flex min-w-0 flex-1 basis-64 flex-col gap-1">
              <span className="flex flex-wrap items-center gap-2"><b className="text-base">{s.shopName}</b><span className="rounded-full bg-[#fdf1e3] px-2.5 py-0.5 text-xs font-bold text-[#a1460a]">{t(`agent.kind.${s.kind}`)}</span></span>
              <span>{s.reason}</span>
              <span className="text-xs text-muted">{formatDateTime(s.createdAt)}</span>
            </span>
            <div className="grid grid-cols-2 gap-2 sm:w-auto sm:min-w-72">
              <button disabled={busy || (s.kind === 'extend_trial' && !can('shops.approve'))}
                onClick={() => void run(() => api(`/v1/platform/agent/suggestions/${s.id}/approve`, 'POST', {}), t('agent.done'))}
                className={`min-h-11 rounded-xl ${btn.solid}`}>{s.kind === 'extend_trial' ? t('agent.extend', { days: s.days }) : t('agent.markDone')}</button>
              <button disabled={busy} onClick={() => void run(() => api(`/v1/platform/agent/suggestions/${s.id}/reject`, 'POST', {}))}
                className="min-h-11 rounded-xl border border-line font-semibold">{t('agent.dismiss')}</button>
            </div>
          </li>
        ))}
      </ul>
      {message && <p className="text-sm font-semibold text-brand-deep">{message}</p>}
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
    <Collapsible title={t('billing.title', { count: overview?.pendingClaims.length ?? 0 })}>
      {overview && (
        <>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
            <div className="flex min-h-[84px] flex-col justify-between rounded-[18px] bg-[#e3f2ee] p-3 text-[#0b5e4f]"><span className="text-[13px] font-bold">{t('plat4.month')}</span><b className="text-2xl">{formatRupees(overview.revenueThisMonth)}</b></div>
            <div className="flex min-h-[84px] flex-col justify-between rounded-[18px] bg-white p-3 ring-1 ring-line"><span className="text-[13px] font-bold text-muted">{t('plat4.year')}</span><b className="text-2xl">{formatRupees(overview.revenueThisYear)}</b></div>
            <div className={`flex min-h-[84px] flex-col justify-between rounded-[18px] p-3 ${overview.trialsEndingThisWeek > 0 ? 'bg-[#b45309] text-white shadow-lg shadow-[#b45309]/30' : 'bg-[#fdf1e3] text-[#a1460a]'}`}><span className="text-[13px] font-bold">{t('plat4.ending')}</span><b className="text-2xl">{overview.trialsEndingThisWeek}</b></div>
          </div>
          <div className="flex flex-wrap gap-2">
            {STATUSES.map((s) => (
              <span key={s} className="rounded-full border border-line bg-white px-3 py-1 text-[13px] font-semibold">{t(`plan2.status.${s}`)} · <b>{overview.byStatus[s] ?? 0}</b></span>
            ))}
          </div>

          <h3 className="font-bold">{t('billing.claims')}</h3>
          {overview.pendingClaims.length === 0 && <p className="text-sm text-muted">{t('billing.noClaims')}</p>}
          {overview.pendingClaims.map((c) => (
            <div key={c.id} className="flex flex-col gap-2 rounded-xl border border-line p-3 text-sm">
              <span><b>{c.shopName}</b> · {c.planCode} {t(`plan2.term.${c.term}`)} · <b>{formatRupees(c.amount)}</b>{c.offerCode ? ` · ${c.offerCode}` : ''}</span>
              <span className="font-mono">UTR {c.upiRef} · {formatDateTime(c.createdAt)}</span>
              <div className="grid grid-cols-2 gap-2">
                <button disabled={busy} onClick={() => void run(() => api(`/v1/platform/claims/${c.id}/confirm`, 'POST', {}), t('billing.confirmed'))}
                  className={`min-h-11 rounded-xl ${btn.solid}`}>{t('billing.confirm')}</button>
                <button disabled={busy} onClick={async () => { const note = (await ask(t('billing.rejectAsk'))); if (note !== null) void run(() => api(`/v1/platform/claims/${c.id}/reject`, 'POST', { note })) }}
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
        <button disabled={busy || newOffer.code.length < 4 || !newOffer.title || !newOffer.price} className={`col-span-2 min-h-11 rounded-xl ${btn.solid}`}
          onClick={() => void run(() => api('/v1/platform/offers', 'POST', { ...newOffer, price: Number(newOffer.price) }), t('billing.offerAdded'))}>{t('billing.addOffer')}</button>
      </div>

      {message && <p className="text-sm font-semibold text-brand-deep">{message}</p>}
    </Collapsible>
  )
}

/**
 * One shop's plan in its admin detail. Where it stands (plan, status, until when), then taking a payment as steps:
 * plan → period (with the price) → how it was paid → confirm → done, with the bill to send on WhatsApp. Rarer actions
 * (move a paid shop to another plan, free access, hold) sit under "More".
 */
export function ShopBillingPanel({ shopId, can, shopName, ownerPhone }: { shopId: string; can: Can; shopName: string; ownerPhone: string | null }) {
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
  const money = can('billing')
  const approve = can('shops.approve')
  const planName = (code: string) => plans.find((p) => p.code === code)?.name ?? code
  const chosen = plans.find((p) => p.code === pay.planCode)
  const amount = chosen ? priceFor(chosen, pay.term) : null
  const paid = view.status === 'active' || view.status === 'grace'
  const date = (iso: string | null) => (iso ? new Date(iso).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric' }) : '')
  const tone: Record<string, string> = {
    active: 'bg-emerald-50 text-emerald-900', grace: 'bg-warn-soft text-warn-ink', trial: 'bg-brand-soft text-brand-deep',
    complimentary: 'bg-violet-50 text-violet-900', on_hold: 'bg-due-soft text-red-900', free: 'bg-white text-ink',
  }
  const stepTitle = (n: number, label: string) => (
    <span className="flex items-center gap-2 font-bold"><span className="flex h-7 w-7 items-center justify-center rounded-full bg-brand text-sm text-white">{n}</span>{label}</span>
  )
  const billText = (p: Payment) => t('billing.waBill', {
    shop: shopName, invoice: p.invoiceNo, plan: planName(p.planCode), term: t(`plan2.term.${p.term}`), amount: formatRupees(p.amount),
    method: t(`billing.methods.${p.method}`), until: date(p.periodEnd),
  }) + (p.billToken ? `\n${planBillLink(p.id, p.billToken)}` : '')

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
                {p.billToken && <a href={planBillLink(p.id, p.billToken)} target="_blank" rel="noreferrer" className="flex min-h-11 items-center rounded-xl border border-line bg-white px-3 text-xs font-semibold">{t('billing.bill')}</a>}
                {!p.refundedAt && <a href={whatsAppLink(ownerPhone, billText(p))} target="_blank" rel="noreferrer" className="flex min-h-11 items-center rounded-xl bg-[#15803d] px-3 text-xs font-semibold text-white">WhatsApp</a>}
                {money && !p.refundedAt && p.term !== 'monthly' && (
                  <button disabled={busy} onClick={async () => (await confirmAsk(t('billing.refundAsk'))) && void run(() => api(`/v1/platform/payments/${p.id}/refund`, 'POST', {}), t('billing.refunded'))}
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
          className={`min-h-12 text-base rounded-xl ${btn.solid}`}>{paid ? t('billing.renew') : t('billing.takePayment')}</button>
      )}

      {money && step >= 1 && step <= 4 && (
        <div className="flex flex-col gap-3 rounded-xl bg-white p-3">
          <div className="flex gap-1" aria-hidden="true">{[1, 2, 3, 4].map((n) => <span key={n} className={`h-1.5 flex-1 rounded-full ${n <= step ? 'bg-brand' : 'bg-line'}`} />)}</div>

          {step === 1 && <>
            {stepTitle(1, t('billing.step.plan'))}
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
              {plans.map((p) => (
                <button key={p.code} onClick={() => { setPay({ ...pay, planCode: p.code }); setStep(2) }}
                  className={`flex flex-col items-start gap-1 rounded-xl border-2 p-3 text-left ${pay.planCode === p.code ? 'border-brand bg-brand-soft' : 'border-line'}`}>
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
                    className={`flex min-h-16 flex-col items-center justify-center rounded-xl border-2 font-semibold ${pay.term === x ? 'border-brand bg-brand-soft' : 'border-line'}`}>
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
                  className={`min-h-12 rounded-xl border-2 font-semibold ${pay.method === m ? 'border-brand bg-brand-soft' : 'border-line'}`}>{t(`billing.methods.${m}`)}</button>
              ))}
            </div>
            <label className="flex flex-col gap-1"><span className="text-xs text-muted">{pay.method === 'cash' ? t('billing.receiptOptional') : t('billing.utrNeeded')}</span>
              <input value={pay.reference} onChange={(e) => setPay({ ...pay, reference: e.target.value })} className="h-12 rounded-xl border border-line px-3 text-base" /></label>
            <button disabled={pay.method !== 'cash' && pay.reference.trim().length < 6} onClick={() => setStep(4)}
              className="min-h-12 rounded-xl bg-brand font-bold text-white disabled:opacity-40">{t('billing.next')}</button>
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
            <button disabled={busy} onClick={() => void record()} className={`min-h-12 text-base rounded-xl ${btn.solid}`}>{t('billing.recordAmount', { amount: formatRupees(amount) })}</button>
          </>}

          <div className="flex justify-between">
            <button onClick={() => setStep(step === 1 ? 0 : ((step - 1) as 1 | 2 | 3))} className="min-h-11 px-2 font-semibold text-brand">{step === 1 ? t('plans.cancel') : t('billing.back')}</button>
          </div>
        </div>
      )}

      {step === 5 && done && (
        <div className="flex flex-col gap-2 rounded-xl bg-emerald-50 p-3 text-emerald-900">
          {stepTitle(5, t('billing.step.done'))}
          <span>{t('billing.doneLine', { invoice: done.invoiceNo, plan: planName(done.planCode), amount: formatRupees(done.amount), until: date(done.periodEnd) })}</span>
          <a href={whatsAppLink(ownerPhone, billText(done))} target="_blank" rel="noreferrer"
            className="flex min-h-12 items-center justify-center rounded-xl bg-[#15803d] font-bold text-white">{t('billing.sendWa', { phone: ownerPhone ? formatPhone(ownerPhone) : '' })}</a>
          {done.billToken && <a href={planBillLink(done.id, done.billToken)} target="_blank" rel="noreferrer" className="flex min-h-12 items-center justify-center rounded-xl border border-emerald-700 bg-white font-bold">{t('billing.printBill')}</a>}
          <button onClick={() => setStep(0)} className="min-h-11 font-semibold">{t('billing.close')}</button>
        </div>
      )}

      {/* Rarer actions */}
      {(money || approve) && (
        <button onClick={() => setMore(!more)} aria-expanded={more} className="min-h-11 self-start font-semibold text-brand">{more ? '−' : '+'} {t('billing.more')}</button>
      )}
      {more && (
        <div className="grid grid-cols-2 gap-2">
          {money && paid && <>
            <select aria-label={t('plans.moveTo')} value={moveTo} onChange={(e) => setMoveTo(e.target.value)} className="h-11 rounded-xl border border-line bg-white px-2">
              <option value="">{t('plans.moveTo')}</option>
              {plans.filter((p) => p.code !== view.planCode).map((p) => <option key={p.code} value={p.code}>{p.name}</option>)}
            </select>
            <button disabled={busy || !moveTo} className="min-h-11 rounded-xl border border-line bg-white font-semibold disabled:opacity-40"
              onClick={async () => { const reason = (await ask(t('plans.moveReason')))?.trim(); if (reason) void run(async () => { await api(`/v1/platform/shops/${shopId}/plan`, 'POST', { planCode: moveTo, reason }); setMoveTo('') }, t('plans.moved')) }}>{t('plans.move')}</button>
          </>}
          {approve && <>
            <select aria-label={t('plans.freePlan')} value={compPlan} onChange={(e) => setCompPlan(e.target.value)} className="col-span-2 h-11 rounded-xl border border-line bg-white px-2">
              {plans.map((p) => <option key={p.code} value={p.code}>{t('plans.freeOn', { plan: p.name })}</option>)}
            </select>
            <button disabled={busy} className="min-h-11 rounded-xl border border-line bg-white font-semibold" onClick={async () => {
              const months = (await ask(t('billing.compAsk', { max: can('admins.manage') ? t('billing.anyLength') : 3 })))
              if (months === null) return
              const reason = (await ask(t('billing.compReasonAsk')))
              if (reason) void run(() => api(`/v1/platform/shops/${shopId}/complimentary`, 'POST', { planCode: compPlan, months: months.trim() ? Number(months) : null, reason }), t('billing.compGiven'))
            }}>{t('billing.giveFree')}</button>
            {view.holdSince
              ? <button disabled={busy} className={`min-h-11 rounded-xl ${btn.solid}`} onClick={() => void run(() => api(`/v1/platform/shops/${shopId}/release`, 'POST', {}))}>{t('billing.release')}</button>
              : <button disabled={busy} className="min-h-11 rounded-xl border border-line bg-white font-semibold text-red-700" onClick={async () => { const reason = (await ask(t('billing.holdAsk'))); if (reason) void run(() => api(`/v1/platform/shops/${shopId}/hold`, 'POST', { reason })) }}>{t('billing.hold')}</button>}
            {view.compPlanCode && <button disabled={busy} className="col-span-2 min-h-11 rounded-xl border border-line bg-white text-xs font-semibold" onClick={() => void run(() => api(`/v1/platform/shops/${shopId}/complimentary`, 'DELETE'))}>{t('billing.endFree')}</button>}
          </>}
        </div>
      )}
      {message && <p className="font-semibold text-brand-deep">{message}</p>}
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
  const [open, setOpen] = useState(true)
  const rows = useQuery({ queryKey: ['platform', 'activity'], queryFn: () => api<Activity[]>('/v1/platform/activity'), enabled: open })
  const label = (action: string) => {
    // Unblock and unlock also end in "block"/"lock": match the longer ones first.
    const key = /\/image\/?$/.test(action) ? 'photo' : action.endsWith('unblock') ? 'unblock' : action.endsWith('unlock') ? 'unlock' : ACTIONS.find(([re]) => re.test(action))?.[1]
    return key ? t(`activity.actions.${key}`) : action
  }
  const said = (d: Record<string, unknown>) => {
    const body = (d.body ?? {}) as Record<string, unknown>
    // A photo or file travels as a long data string: say what it was instead of printing it.
    return Object.entries(body).filter(([, v]) => v !== null && v !== '' && typeof v !== 'object')
      .map(([k, v]) => (typeof v === 'string' && (v.startsWith('data:') || v.length > 120) ? `${k}: ${t('activity.longValue')}` : `${k}: ${String(v)}`)).join(' · ')
  }

  // Each change with a small coloured icon by its kind, and filters by admin and kind.
  const kindOf = (action: string) => (/\/image\/?$|master-items/.test(action) ? 'items' : /admins/.test(action) ? 'admins' : /plans|offers|claims|payments|refund|complimentary|hold|release/.test(action) ? 'money' : /agent/.test(action) ? 'agents' : 'shops')
  const KIND: Record<string, [string, string]> = {
    shops: ['M4 9l1-5h14l1 5M4 9h16v11H4zM9 20v-6h6v6', 'bg-[#e8effd] text-[#1d4ed8]'],
    money: ['M3 6h18v12H3zM3 10h18M7 15h3', 'bg-[#fdf1e3] text-[#a1460a]'],
    items: ['M3 4h18v16H3zM3 16l5-5 5 5 3-3 5 5M15 9h.01', 'bg-[#e3f2ee] text-[#0b5e4f]'],
    admins: ['M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z', 'bg-[#f1eafd] text-[#6d28d9]'],
    agents: ['M9 11a4 4 0 100-8 4 4 0 000 8zM2 21v-1a7 7 0 0114 0v1', 'bg-[#e0f2fe] text-[#0369a1]'],
  }
  const [who, setWho] = useState('')
  const [kind, setKind] = useState('')
  const shown = (rows.data ?? []).filter((r) => (!who || r.admin === who) && (!kind || kindOf(r.action) === kind))

  return (
    <section className="flex flex-col gap-3 rounded-2xl bg-white p-4">
      <button onClick={() => setOpen(!open)} aria-expanded={open} className="flex min-h-11 items-center justify-between text-left">
        <h2 className="text-lg font-bold">{t('activity.title')}</h2>
        <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={`shrink-0 text-muted transition ${open ? 'rotate-180' : ''}`}><path d="M6 9l6 6 6-6" /></svg>
      </button>
      {open && <>
        <div className="flex flex-wrap items-center gap-2">
          <p className="mr-auto text-sm text-muted">{t('activity.hint')}</p>
          <select aria-label={t('plat6.byAdmin')} value={who} onChange={(e) => setWho(e.target.value)} className="h-10 rounded-xl border border-line bg-white px-2 text-sm">
            <option value="">{t('plat6.allAdmins')}</option>{[...new Set((rows.data ?? []).map((r) => r.admin))].map((a) => <option key={a} value={a}>{a}</option>)}
          </select>
          <select aria-label={t('plat6.byKind')} value={kind} onChange={(e) => setKind(e.target.value)} className="h-10 rounded-xl border border-line bg-white px-2 text-sm">
            <option value="">{t('plat6.allChanges')}</option>{Object.keys(KIND).map((k) => <option key={k} value={k}>{t(`plat6.kind.${k}`)}</option>)}
          </select>
        </div>
        {shown.length === 0 && <p className="text-sm text-muted">{t('activity.none')}</p>}
        <ul className="flex flex-col divide-y divide-[#edf2f1] text-sm">
          {shown.map((r) => {
            const [d, tone] = KIND[kindOf(r.action)]
            return (
              <li key={r.id} className="flex items-start gap-3 py-2.5">
                <span aria-hidden className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-full ${tone}`}><svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d={d} /></svg></span>
                <span className="flex min-w-0 flex-col">
                  <span className="font-semibold">{label(r.action)}{r.shopName ? ` · ${r.shopName}` : ''}</span>
                  <span className="text-xs text-muted">{r.admin} · {formatDateTime(r.at)}</span>
                  {said(r.detail) && <span className="text-xs break-words">{said(r.detail)}</span>}
                </span>
              </li>
            )
          })}
        </ul>
      </>}
    </section>
  )
}

type PlanDraft = { name: string; priceMonthly: string; priceYear1: string; priceYear3: string; priceYear5: string; maxCustomers: string; maxOwners: string; maxStaff: string; whatsAppPerMonth: string; features: string[] }
const PRICE_KEYS = ['priceMonthly', 'priceYear1', 'priceYear3', 'priceYear5'] as const
const blank: PlanDraft = { name: '', priceMonthly: '', priceYear1: '', priceYear3: '', priceYear5: '', maxCustomers: '', maxOwners: '1', maxStaff: '0', whatsAppPerMonth: '0', features: [] }
const toDraft = (p: PlanInfo): PlanDraft => ({
  name: p.name, priceMonthly: String(p.priceMonthly ?? ''), priceYear1: String(p.priceYear1 ?? ''), priceYear3: String(p.priceYear3 ?? ''), priceYear5: String(p.priceYear5 ?? ''),
  maxCustomers: String(p.maxCustomers ?? ''), maxOwners: String(p.maxOwners ?? 1), maxStaff: String(p.maxStaff), whatsAppPerMonth: String(p.whatsAppPerMonth), features: p.features,
})
const toBody = (d: PlanDraft) => {
  const num = (v: string) => (v.trim() === '' ? null : Number(v))
  return {
    name: d.name.trim(), priceMonthly: num(d.priceMonthly), priceYear1: num(d.priceYear1), priceYear3: num(d.priceYear3), priceYear5: num(d.priceYear5),
    maxCustomers: num(d.maxCustomers), maxOwners: Math.max(1, Number(d.maxOwners || 1)), maxStaff: Number(d.maxStaff || 0), whatsAppPerMonth: Number(d.whatsAppPerMonth || 0), features: d.features,
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
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        <label className="flex flex-col text-xs text-muted">{t('plans.owners')}
          <input value={draft.maxOwners} disabled={disabled} onChange={digits('maxOwners')} inputMode="numeric" className={box} /></label>
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
              <label key={f} className={`flex min-h-11 items-center gap-2 rounded-xl border px-3 text-sm ${on ? 'border-brand bg-brand-soft font-semibold' : 'border-line'}`}>
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
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
      {plans.map((p, i) => {
        const draft = drafts[p.code]
        // Each plan in its own colour: free grey, then green, blue, amber, purple.
        const tone = p.code === 'free' ? ['bg-[#edf2f1]', 'text-[#33423d]'] : [['bg-[#e3f2ee]', 'text-[#0b5e4f]'], ['bg-[#e8effd]', 'text-[#1d4ed8]'], ['bg-[#fdf1e3]', 'text-[#a1460a]'], ['bg-[#f1eafd]', 'text-[#6d28d9]']][(i - 1 + 4) % 4]
        return (
          <div key={p.code} className={`flex flex-col gap-2.5 rounded-[18px] p-4 ${draft ? 'col-span-full bg-white ring-2 ring-brand' : tone[0]} ${p.isActive === false ? 'opacity-70 outline-2 outline-dashed outline-line' : ''}`}>
            <div className="flex items-center gap-2.5">
              <span aria-hidden className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-white ${tone[1]}`}><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M12 3l2.8 5.7 6.2.9-4.5 4.4 1 6.2L12 17.3 6.5 20.2l1-6.2L3 9.6l6.2-.9z" /></svg></span>
              <b className="text-lg">{p.name}</b>
              <span className="ml-auto rounded-full bg-white px-2.5 py-0.5 text-xs font-bold">{t('plans.shops', { count: p.shops ?? 0 })}{p.isActive === false ? ` · ${t('plans.offSale')}` : ''}</span>
            </div>
            <p className="text-2xl font-extrabold">{p.code === 'free' ? formatRupees(0) : p.priceYear1 ? formatRupees(p.priceYear1) : '—'}{p.code !== 'free' && p.priceYear1 ? <span className="text-sm font-semibold text-muted"> / {t('plan2.year')}</span> : null}</p>
            {draft ? (
              <>
                <PlanFields draft={draft} onChange={(d) => setDrafts({ ...drafts, [p.code]: d })} disabled={busy} free={p.code === 'free'} />
                <div className="grid grid-cols-2 gap-2">
                  <button disabled={busy} className={`min-h-11 rounded-xl ${btn.solid}`} onClick={() => void run(async () => {
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
                <span className="text-sm leading-snug">
                  {p.code === 'free' ? t('plan2.freeIncludes') : p.features.map((f) => t(`plan2.feature.${f}`)).join(' · ')}
                </span>
                <span className="text-xs text-muted">
                  {p.maxCustomers ? t('plan2.customersUpTo', { count: p.maxCustomers }) : t('plan2.customersUnlimited')}
                  {` · ${t('plans.ownersCount', { count: p.maxOwners ?? 1 })} · ${t('plan2.staff', { count: p.maxStaff })} · ${t('plan2.whatsapp', { count: p.whatsAppPerMonth })}`}
                </span>
                <div className="mt-auto grid grid-cols-2 gap-2">
                  <button disabled={busy} className="min-h-10 rounded-xl border border-line bg-white font-semibold" onClick={() => setDrafts({ ...drafts, [p.code]: toDraft(p) })}>{t('plans.edit')}</button>
                  {p.code !== 'free' && (
                    <button disabled={busy} className={`min-h-10 ${p.isActive === false ? `rounded-xl ${btn.solid}` : btn.dangerOutline}`}
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
      </div>

      {adding ? (
        <div className="flex flex-col gap-2 rounded-xl bg-ground p-3">
          <span className="font-bold">{t('plans.new')}</span>
          <PlanFields draft={adding} onChange={setAdding} disabled={busy} />
          <div className="grid grid-cols-2 gap-2">
            <button disabled={busy || adding.name.trim().length < 2 || !adding.priceYear1} className={`min-h-11 rounded-xl ${btn.solid}`}
              onClick={() => void run(async () => { await api('/v1/platform/plans', 'POST', toBody(adding)); setAdding(null) }, t('plans.added'))}>{t('plans.add')}</button>
            <button disabled={busy} className="min-h-11 rounded-xl border border-line font-semibold" onClick={() => setAdding(null)}>{t('plans.cancel')}</button>
          </div>
        </div>
      ) : (
        <button onClick={() => setAdding(blank)} className={`min-h-11 self-start ${btn.add}`}>+ {t('plans.new')}</button>
      )}
      {message && <p className="text-sm font-semibold text-brand-deep">{message}</p>}
    </Collapsible>
  )
}

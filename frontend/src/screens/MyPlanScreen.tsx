import { shownLocale } from '../i18n'
import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { api } from '../lib/api'
import { priceFor, useSubscription, type PlanTerm, type Quote } from '../lib/billing'
import { formatRupees, whatsAppLink } from '../lib/format'
import { navigate } from '../router'

const STATUS_STYLE: Record<string, string> = {
  trial: 'bg-brand-soft text-brand-deep', active: 'bg-emerald-50 text-emerald-900', complimentary: 'bg-violet-50 text-violet-900',
  grace: 'bg-warn-soft text-warn-ink', on_hold: 'bg-due-soft text-red-900', free: 'bg-[#edf2f1] text-ink',
}

/**
 * The owner's plan: where it stands (trial, paid, free…), the plans and what each opens, and paying by UPI: pay the
 * shown amount to Apna Kaarobar's UPI ID, then send the UPI reference so the team can confirm it. The shop's own khata,
 * bills and customers work on every plan, Free included.
 */
export function MyPlanScreen({ shopId, welcome = false }: { shopId: string; welcome?: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const subscription = useSubscription(shopId)
  const [picked, setPicked] = useState<string | null>(null)
  const [pickedTerm, setTerm] = useState<PlanTerm | null>(null)
  const [offer, setOffer] = useState('')
  const [quote, setQuote] = useState<Quote | null>(null)
  const [utr, setUtr] = useState('')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const s = subscription.data
  if (!s) return <p className="p-4 text-muted">…</p>

  // A shop whose plan ended starts from the plan it had.
  const term = pickedTerm ?? s.chosenTerm ?? 'year1'
  // A paying shop renews or moves up: its own plan and those above it, nothing below.
  const paying = s.status === 'active' || s.status === 'grace'
  const onSale = s.plans.filter((p) => p.code !== 'free')
  const offered = paying ? onSale.slice(Math.max(0, onSale.findIndex((p) => p.code === s.planCode))) : onSale
  const planCode = picked ?? (offered.some((p) => p.code === s.chosenPlanCode) ? s.chosenPlanCode : null) ?? (s.status !== 'trial' && s.planCode !== 'free' ? s.planCode : 'basic')
  const terms: PlanTerm[] = s.monthlyAvailable ? ['monthly', 'year1', 'year3', 'year5'] : ['year1', 'year3', 'year5']
  const chosen = s.plans.find((p) => p.code === planCode)
  const amount = quote && quote.planCode === planCode && quote.term === term && !quote.error ? quote.amount : chosen ? priceFor(chosen, term) : null
  const upiLink = s.payToUpi && amount
    ? `upi://pay?pa=${encodeURIComponent(s.payToUpi)}&pn=${encodeURIComponent(s.payeeName)}&am=${amount}&cu=INR&tn=${encodeURIComponent(`Apna Kaarobar ${planCode} ${s.referralCode}`)}`
    : null

  async function run(action: () => Promise<void>) {
    setBusy(true)
    setMessage(null)
    try {
      await action()
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  // Only a bookmark: if it fails the shop can still pay what's on screen.
  const remember = (code: string, period: PlanTerm) => { if (period !== 'monthly') void api('/v1/subscription/choice', 'POST', { planCode: code, term: period }).catch(() => undefined) }

  // Razorpay's own page takes the payment; the server opens the plan only on its signature.
  const payOnline = () => run(async () => {
    const order = await api<{ orderId: string; keyId: string; amountPaise: number }>('/v1/subscription/checkout', 'POST', { planCode, term, offerCode: quote?.offerCode ?? null })
    await new Promise<void>((resolve, reject) => {
      if ((window as unknown as { Razorpay?: unknown }).Razorpay) return resolve()
      const script = document.createElement('script')
      script.src = 'https://checkout.razorpay.com/v1/checkout.js'
      script.onload = () => resolve()
      script.onerror = () => reject(new Error(t('plan2.payFailed')))
      document.head.appendChild(script)
    })
    const Razorpay = (window as unknown as { Razorpay: new (options: unknown) => { open(): void } }).Razorpay
    new Razorpay({
      key: order.keyId, order_id: order.orderId, amount: order.amountPaise, currency: 'INR', name: 'Apna Kaarobar',
      handler: (r: { razorpay_order_id: string; razorpay_payment_id: string; razorpay_signature: string }) => void run(async () => {
        await api('/v1/subscription/checkout/verify', 'POST', { orderId: r.razorpay_order_id, paymentId: r.razorpay_payment_id, signature: r.razorpay_signature })
        await queryClient.invalidateQueries({ queryKey: [shopId, 'subscription'] })
        setMessage(t('plan2.paidOnline'))
      }),
    }).open()
  })

  const applyOffer = () => run(async () => {
    const q = await api<Quote>('/v1/subscription/quote', 'POST', { planCode, term, offerCode: offer.trim() || null })
    setQuote(q)
    if (q.error) setMessage(q.error)
  })

  const claim = () => run(async () => {
    await api('/v1/subscription/claims', 'POST', { planCode, term, offerCode: quote?.offerCode ?? null, upiRef: utr.trim() })
    setUtr('')
    await queryClient.invalidateQueries({ queryKey: [shopId, 'subscription'] })
    setMessage(t('plan2.claimSent'))
  })

  return (
    <div className="flex flex-col gap-3 p-4">
      {welcome && (
        <section className="flex flex-col gap-1">
          <h2 className="text-2xl font-extrabold">{t('plan2.welcomeTitle')}</h2>
          <button onClick={() => navigate('/')} className="min-h-11 text-left font-semibold text-brand underline">{t('plan2.tryFree')}</button>
        </section>
      )}

      {/* Where the shop stands */}
      <section className={`flex flex-col gap-1 rounded-[20px] p-4 ${STATUS_STYLE[s.status]}`}>
        <span className="text-sm font-semibold">{t(`plan2.status.${s.status}`)}</span>
        {s.status !== 'free' && <span className="text-2xl font-extrabold">{s.planName}</span>}
        {s.shopCode && <span className="text-sm">{t('plan2.shopId', { code: s.shopCode })}</span>}
        {s.daysLeft !== null && <span className="text-sm">{t('plan2.daysLeft', { count: s.daysLeft })}</span>}
        {s.compReason && <span className="text-sm">{s.compReason}</span>}
        {s.holdReason && <span className="text-sm font-semibold">{t('plan2.holdReason', { reason: s.holdReason })}</span>}
        {s.priceLockUntil && <span className="text-sm">{t('plan2.priceLock', { date: new Date(s.priceLockUntil).toLocaleDateString(shownLocale()) })}</span>}
        <span className="mt-1 text-xs opacity-80">{t('plan2.alwaysFree')}</span>
      </section>

      {s.rejectedClaim && (
        <p className="rounded-[14px] bg-due-soft p-3 text-sm text-red-900">
          {t('plan2.rejected', { amount: formatRupees(s.rejectedClaim.amount), ref: s.rejectedClaim.upiRef, reason: s.rejectedClaim.note ?? '' })}
        </p>
      )}
      {s.pendingClaim && (
        <p className="rounded-[14px] bg-warn-soft p-3 text-sm text-warn-ink">
          {t('plan2.pending', { amount: formatRupees(s.pendingClaim.amount), ref: s.pendingClaim.upiRef })}
        </p>
      )}

      {/* Plans */}
      <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
        {offered.map((p) => (
          <button key={p.code} onClick={() => { setPicked(p.code); setQuote(null); remember(p.code, term) }}
            aria-pressed={planCode === p.code}
            className={`flex flex-col gap-1 rounded-card border-2 bg-white p-4 text-left disabled:opacity-80 ${planCode === p.code ? 'border-brand' : 'border-transparent'}`}>
            <span className="flex items-baseline justify-between">
              <span className="text-lg font-extrabold">{p.name}</span>
              <span className="font-bold">{`${formatRupees(p.priceYear1 ?? 0)}/${t('plan2.year')}`}</span>
            </span>
            <span className="text-xs text-muted">
              {p.maxCustomers ? t('plan2.customersUpTo', { count: p.maxCustomers }) : t('plan2.customersUnlimited')}
              {p.maxStaff > 0 ? ` · ${t('plan2.staff', { count: p.maxStaff })}` : ''}
              {p.whatsAppPerMonth > 0 ? ` · ${t('plan2.whatsapp', { count: p.whatsAppPerMonth })}` : ''}
            </span>
            <span className="text-xs">{p.features.map((f) => t(`plan2.feature.${f}`)).join(' · ')}</span>
          </button>
        ))}
      </div>

      {/* Period, offer, pay */}
      {chosen && (
        <section className="flex flex-col gap-3 rounded-[20px] bg-white p-4 shadow-sm print:shadow-none">
          <div role="radiogroup" aria-label={t('plan2.period')} className="grid grid-cols-3 gap-2 sm:grid-cols-4">
            {terms.map((x) => {
              const price = priceFor(chosen, x)
              return price === null ? null : (
                <button key={x} role="radio" aria-checked={term === x} onClick={() => { setTerm(x); setQuote(null); remember(planCode, x) }}
                  className={`flex min-h-14 flex-col items-center justify-center rounded-xl text-sm font-semibold ${term === x ? 'bg-brand text-white' : 'border border-line'}`}>
                  <span>{t(`plan2.term.${x}`)}</span>
                  <span>{formatRupees(price)}{x === 'year5' ? `/${t('plan2.year')}` : ''}</span>
                </button>
              )
            })}
          </div>
          {term === 'year5' && <p className="text-xs text-muted">{t('plan2.year5Hint')}</p>}

          <div className="flex gap-2">
            <label className="sr-only" htmlFor="offer-code">{t('plan2.offer')}</label>
            <input id="offer-code" value={offer} onChange={(e) => setOffer(e.target.value.toUpperCase())} placeholder={t('plan2.offer')}
              className="h-12 min-w-0 flex-1 rounded-xl border border-line px-3 uppercase bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
            <button disabled={busy || !offer.trim()} onClick={() => void applyOffer()} className="h-12 rounded-xl border border-line px-4 font-semibold transition hover:bg-[#f6faf9]">{t('plan2.apply')}</button>
          </div>

          <div className="flex items-baseline justify-between text-lg">
            <span className="font-semibold">{t('plan2.toPay')}</span>
            <span className="text-2xl font-extrabold">{amount !== null ? formatRupees(amount) : '–'}</span>
          </div>
          {s.gstRegistered && <p className="-mt-2 text-xs text-muted">{t('plan2.gstIncluded')}</p>}

          {term === 'monthly' ? (
            <p className="text-sm text-muted">{t('plan2.monthlyAutoPay')}</p>
          ) : (
            <>
              {s.onlinePay && <Button disabled={busy || amount === null} onClick={() => void payOnline()}>{t('plan2.payOnline')}</Button>}
              {upiLink
                ? <a href={upiLink} className="flex h-12 items-center justify-center rounded-2xl bg-brand font-bold text-white">{t('plan2.payUpi', { upi: s.payToUpi })}</a>
                : <p className="rounded-xl bg-ground p-3 text-sm">{t('plan2.noUpi')}</p>}
              <label className="flex flex-col gap-1">
                <span className="text-sm font-semibold">{t('plan2.utr')}</span>
                <input value={utr} onChange={(e) => setUtr(e.target.value.replace(/[^A-Za-z0-9]/g, '').slice(0, 30))} inputMode="numeric" placeholder="412345678901"
                  className="h-12 rounded-xl border border-line px-3 text-lg bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
              </label>
              <Button disabled={busy || utr.length < 6 || !!s.pendingClaim} onClick={() => void claim()}>{t('plan2.sendRef')}</Button>
            </>
          )}
        </section>
      )}

      {message && <p className="rounded-[14px] bg-brand-soft p-3 text-sm font-semibold text-brand-deep">{message}</p>}

      {/* Bring a shop */}
      <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
        <h2 className="font-bold">{t('plan2.referTitle', { days: s.referralBonusDays })}</h2>
        <p className="text-sm text-muted">{t('plan2.referHint')}</p>
        <p className="rounded-xl bg-ground p-2 text-center font-mono text-lg font-bold">{s.referralCode}</p>
        <a href={whatsAppLink(null, t('plan2.referMessage', { code: s.referralCode }))} target="_blank" rel="noreferrer"
          className="flex min-h-11 items-center justify-center rounded-xl bg-[#15803d] font-semibold text-white">{t('plan2.referShare')}</a>
      </section>

      {s.payments.length > 0 && (
        <section className="rounded-card bg-white p-4 shadow-sm">
          <h2 className="mb-2 font-bold">{t('plan2.payments')}</h2>
          <ul className="divide-y divide-[#edf2f1] text-sm">
            {s.payments.map((p) => (
              <li key={p.id} className={`flex justify-between gap-2 py-2 ${p.refundedAt ? 'text-muted line-through' : ''}`}>
                <span>{p.invoiceNo} · {t(`plan2.term.${p.term}`)} {s.plans.find((x) => x.code === p.planCode)?.name ?? p.planCode}<br /><span className="text-xs text-muted">{new Date(p.paidAt).toLocaleDateString(shownLocale())} → {new Date(p.periodEnd).toLocaleDateString(shownLocale())}</span></span>
                <span className="flex flex-col items-end gap-1">
                  <span className="font-bold">{formatRupees(p.amount)}</span>
                  {p.billToken && <a href={`#/bill/plan/${p.id}?t=${p.billToken}`} className="text-xs font-semibold text-brand underline">{t('planBill.bill')}</a>}
                </span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  )
}

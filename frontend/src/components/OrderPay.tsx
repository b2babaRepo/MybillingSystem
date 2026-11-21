import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatRupees } from '../lib/format'
import { reachedStep, shouldAskAlerts, markAlertsAsked, stepsOf, turnOnAlerts, type KhataAtCheckout } from '../lib/orderFlow'
import type { Order, OrderPayment } from '../lib/planner'
import { upiUri } from '../lib/reports'
import { btn } from './ui'

// Customer app phase 2, the pieces every checkout and order screen shares: the order's steps, the payment choice,
// the honest bill, the shop's time chips, "Pay by UPI" with a QR, and the one-time ask for order notifications.

/** Placed → Accepted → Packed → Ready / Out for delivery → Delivered. */
export function OrderSteps({ order }: { order: Pick<Order, 'fulfilment' | 'status'> & { pending?: boolean } }) {
  const { t } = useTranslation()
  if (order.status === 'cancelled') return null
  const steps = stepsOf(order)
  const reached = order.pending ? -1 : reachedStep(order)
  return (
    <ol className="grid grid-cols-5 gap-1" aria-label={t(`plan.status.${order.status}`)}>
      {steps.map((step, i) => (
        <li key={step} className="flex flex-col gap-1" aria-current={i === reached ? 'step' : undefined}>
          <span className={`h-1.5 rounded-full ${i <= reached ? 'bg-brand' : 'bg-[#edf2f1]'}`} />
          <span className={`text-[11px] leading-tight ${i <= reached ? 'font-bold text-brand-strong' : 'text-muted'}`}>{t(`plan.status.${step}`)}</span>
        </li>
      ))}
    </ol>
  )
}

/** The three ways to pay, in plain words. Khata shows the running balance before the customer confirms. */
export function PaymentPicker({ value, onChange, total, upi, khata, signedIn }: {
  value: OrderPayment
  onChange: (p: OrderPayment) => void
  total: number
  upi: boolean
  khata: KhataAtCheckout | null | undefined
  signedIn: boolean
}) {
  const { t } = useTranslation()
  const after = khata ? Math.round((khata.balance + total) * 100) / 100 : 0
  const over = !!khata?.limit && after > khata.limit
  const khataReady = !!khata?.allowed && !over
  const option = (p: OrderPayment, title: string, hint: string, enabled: boolean) => (
    <button type="button" key={p} disabled={!enabled} onClick={() => onChange(p)} aria-pressed={value === p}
      className={`flex min-h-14 w-full flex-col items-start rounded-[14px] px-3 py-2 text-left disabled:opacity-50 ${value === p ? 'border-2 border-brand bg-brand-soft' : 'border border-line bg-white'}`}>
      <span className="font-bold">{title}</span>
      <span className="text-[13px] text-muted">{hint}</span>
    </button>
  )
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="mb-1 text-sm font-semibold text-muted">{t('cust2.payTitle')}</legend>
      {option('cash', t('cust2.payLater'), t('cust2.payLaterHint'), true)}
      {option('upi', t('cust2.payUpi'), upi ? t('cust2.payUpiHint') : t('cust2.payUpiNone'), upi)}
      {signedIn && khata !== undefined && option('khata', t('cust2.payKhata'),
        !khata ? t('cust2.khataNone')
          : !khata.allowed ? t('cust2.khataOff')
          : over ? t('cust2.khataOver', { limit: formatRupees(khata.limit!) })
          : `${t('cust2.payKhataSum', { balance: formatRupees(khata.balance), order: formatRupees(total), after: formatRupees(after) })}${khata.limit ? ` · ${t('cust2.khataLimit', { limit: formatRupees(khata.limit) })}` : ''}`,
        khataReady)}
    </fieldset>
  )
}

/** The whole bill on the cart, before checkout: nothing is added after it. */
export function BillSummary({ bill, minOrder }: { bill: { items: number; saved: number; fee: number; total: number; short: number; toFree: number }; minOrder: number | null }) {
  const { t } = useTranslation()
  const row = (label: string, value: string, strong = false, tone = '') => (
    <div className={`flex justify-between gap-2 ${strong ? 'border-t border-line pt-2 text-lg font-extrabold' : 'text-sm'} ${tone}`}><span>{label}</span><span>{value}</span></div>
  )
  return (
    <div className="flex flex-col gap-1 rounded-[14px] bg-white p-3">
      {row(t('cust2.itemTotal'), formatRupees(bill.items))}
      {bill.saved > 0 && row(t('cust2.youSave'), `− ${formatRupees(bill.saved)}`, false, 'font-semibold text-green-700')}
      {row(t('cust2.delivery'), bill.fee > 0 ? formatRupees(bill.fee) : t('cust2.free'))}
      {row(t('cust2.toPay'), formatRupees(bill.total), true)}
      {bill.toFree > 0 && <p className="text-xs font-semibold text-brand-strong">{t('cust2.addMoreFree', { amount: formatRupees(bill.toFree) })}</p>}
      {bill.short > 0 && minOrder && <p className="rounded-lg bg-warn-soft p-2 text-sm font-semibold text-amber-950">{t('cust2.belowMin', { amount: formatRupees(bill.short), min: formatRupees(minOrder) })}</p>}
      <p className="text-xs text-muted">{t('cust2.nothingLater')}</p>
    </div>
  )
}

/** The shop's own time chips ("Now", "Evening"), with a typed time when it has set none or the customer wants another. */
export function SlotChips({ slots, value, onChange, placeholder }: { slots: string[]; value: string; onChange: (v: string) => void; placeholder: string }) {
  const { t } = useTranslation()
  const [typing, setTyping] = useState(slots.length === 0 || (!!value && !slots.includes(value)))
  return (
    <div className="flex flex-col gap-2">
      <span className="text-sm font-semibold text-muted">{t('cust2.when')}</span>
      {slots.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {slots.map((s) => (
            <button type="button" key={s} onClick={() => { setTyping(false); onChange(value === s ? '' : s) }} aria-pressed={value === s}
              className={`min-h-11 rounded-full px-4 font-semibold ${value === s ? 'bg-brand text-white' : 'border border-line bg-white'}`}>{s}</button>
          ))}
          <button type="button" onClick={() => { setTyping(true); onChange('') }} aria-pressed={typing} className={`min-h-11 rounded-full px-4 font-semibold ${typing ? 'bg-brand text-white' : 'border border-line bg-white'}`}>{t('cust2.otherTime')}</button>
        </div>
      )}
      {typing && <input value={value} onChange={(e) => onChange(e.target.value)} maxLength={80} placeholder={placeholder} aria-label={t('cust2.when')} className="h-12 w-full rounded-[14px] border-[1.5px] border-line bg-white px-3 text-lg outline-none focus:border-brand-line" />}
    </div>
  )
}

const isPhone = () => typeof navigator !== 'undefined' && /Android|iPhone|iPad|Mobile/i.test(navigator.userAgent)

/** "Pay ₹X by UPI": opens the customer's UPI app on a phone; on a computer, a QR to scan with one. */
export function UpiPay({ upiId, shopName, amount, note }: { upiId: string; shopName: string; amount: number; note: string }) {
  const { t } = useTranslation()
  const uri = upiUri(upiId, shopName, amount, note)
  const [qr, setQr] = useState<string | null>(null)
  useEffect(() => {
    if (isPhone()) return
    let live = true
    void import('qrcode').then((m) => m.default.toDataURL(uri, { width: 200, margin: 1 })).then((url) => { if (live) setQr(url) }).catch(() => {})
    return () => { live = false }
  }, [uri])
  return (
    <div className="flex flex-col items-center gap-2 rounded-card bg-white p-4 text-center">
      <a href={uri} className={`flex min-h-12 w-full items-center justify-center rounded-xl text-lg ${btn.solid}`}>{t('cust2.payNowUpi', { amount: formatRupees(amount) })}</a>
      {qr && (<><span className="text-sm text-muted">{t('cust2.scanQr')}</span><img src={qr} alt={`UPI ${upiId}`} width={200} height={200} className="rounded-lg" /></>)}
      <span className="font-mono text-sm">{upiId}</span>
      <span className="text-xs text-muted">{t('cust2.upiPaidHint')}</span>
    </div>
  )
}

/** Asked once, at a meaningful moment (first order placed, a shop saved): may this phone tell them about their orders? */
export function AlertsPrompt({ show }: { show: boolean }) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  useEffect(() => { if (show && shouldAskAlerts()) setOpen(true) }, [show])
  if (!open) return null
  return (
    <div role="dialog" aria-label={t('cust2.alertsOn')} className="flex flex-col gap-2 rounded-card bg-brand-soft p-4 ring-1 ring-brand-line">
      <span className="font-semibold text-brand-deep">🔔 {t('cust2.alertsAsk')}</span>
      <div className="grid grid-cols-2 gap-2">
        <button onClick={() => { setOpen(false); void turnOnAlerts().catch(() => {}) }} className={`min-h-11 rounded-xl ${btn.solid}`}>{t('cust2.alertsYes')}</button>
        <button onClick={() => { markAlertsAsked(); setOpen(false) }} className="min-h-11 rounded-xl border border-line bg-white font-semibold">{t('cust2.alertsNo')}</button>
      </div>
    </div>
  )
}

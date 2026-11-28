import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { controlSm } from '../components/ui'
import { btn } from '../lib/buttonStyles'
import { shownLocale } from '../i18n'
import { api } from '../lib/api'
import { code128Modules } from '../lib/barcode'
import { formatRupees } from '../lib/format'
import { uuidv7 } from '../lib/ids'
import { shownError } from '../lib/notify'

interface Voucher { id: string; code: string; amount: number; balance: number; soldAt: string; soldMode: 'cash' | 'upi' | 'card'; expiresOn: string; buyerName: string | null; cancelledAt: string | null }

/** The voucher's code as Code 128 bars, so the counter can scan it. */
function Bars({ code }: { code: string }) {
  const bits = code128Modules(code)
  if (!bits) return null
  return (
    <svg viewBox={`0 0 ${bits.length + 20} 30`} preserveAspectRatio="none" className="h-14 w-full" role="img" aria-label={code}>
      {bits.split('').map((b, i) => (b === '1' ? <rect key={i} x={10 + i} y={0} width={1.02} height={30} fill="#000" /> : null))}
    </svg>
  )
}

/**
 * Gift vouchers: sell one (its money is taken now, by cash, UPI or card), print it with its code and bars, and see
 * what is left on the ones sold. On a bill, "Gift voucher" takes its code and pays from it.
 */
export function VouchersScreen({ shopId, shopName, isOwner }: { shopId: string; shopName: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const list = useQuery({ queryKey: [shopId, 'gift-vouchers'], queryFn: () => api<Voucher[]>('/v1/gift-vouchers'), retry: false })
  const [form, setForm] = useState({ amount: '', mode: 'cash' as Voucher['soldMode'], buyer: '', months: '12' })
  const [sold, setSold] = useState<Voucher | null>(null)
  const [message, setMessage] = useState('')

  async function sell() {
    setMessage('')
    try {
      const v = await api<Voucher>('/v1/gift-vouchers', 'POST', { id: uuidv7(), amount: Number(form.amount), mode: form.mode, buyerName: form.buyer.trim() || null, months: Number(form.months) || 12 })
      setSold(v)
      setForm({ ...form, amount: '', buyer: '' })
      await list.refetch()
    } catch (e) {
      setMessage(shownError(e))
    }
  }
  async function cancel(v: Voucher) {
    setMessage('')
    try {
      await api(`/v1/gift-vouchers/${v.id}/cancel`, 'POST')
      await list.refetch()
    } catch (e) {
      setMessage(shownError(e))
    }
  }
  const date = (iso: string) => new Date(iso).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric' })

  return (
    <div className="flex flex-col gap-3 p-4">
      <p className="text-sm text-muted print:hidden">{t('gift.hint')}</p>
      <section className="grid grid-cols-2 gap-2 rounded-card bg-white p-4 print:hidden">
        <input value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value.replace(/[^\d.]/g, '') })} inputMode="decimal" placeholder={t('gift.amount')} aria-label={t('gift.amount')} className={controlSm} />
        <select value={form.mode} onChange={(e) => setForm({ ...form, mode: e.target.value as Voucher['soldMode'] })} aria-label={t('gift.paidBy')} className={controlSm}>
          {(['cash', 'upi', 'card'] as const).map((m) => <option key={m} value={m}>{t(m)}</option>)}
        </select>
        <input value={form.buyer} onChange={(e) => setForm({ ...form, buyer: e.target.value })} maxLength={80} placeholder={t('gift.buyer')} aria-label={t('gift.buyer')} className={controlSm} />
        <select value={form.months} onChange={(e) => setForm({ ...form, months: e.target.value })} aria-label={t('gift.validFor')} className={controlSm}>
          {['3', '6', '12', '24'].map((m) => <option key={m} value={m}>{t('gift.months', { count: Number(m) })}</option>)}
        </select>
        <button type="button" disabled={!(Number(form.amount) > 0)} onClick={() => void sell()} className={`col-span-2 min-h-12 rounded-xl ${btn.solid}`}>{t('gift.sell', { amount: formatRupees(Number(form.amount) || 0) })}</button>
      </section>
      {message && <p className="text-sm text-due">{message}</p>}
      {sold && (
        <section className="mx-auto flex w-full max-w-sm flex-col gap-2 rounded-card border-2 border-dashed border-ink bg-white p-4 text-center">
          <p className="font-bold">{shopName}</p>
          <p className="text-sm uppercase tracking-widest text-muted">{t('gift.title')}</p>
          <p className="text-4xl font-extrabold">{formatRupees(sold.amount)}</p>
          <Bars code={sold.code} />
          <p className="font-mono text-lg tracking-[0.2em]">{sold.code}</p>
          <p className="text-xs text-muted">{t('gift.validTill', { date: date(sold.expiresOn) })}{sold.buyerName ? ` · ${sold.buyerName}` : ''}</p>
          <button type="button" onClick={() => window.print()} className={`min-h-11 rounded-xl print:hidden ${btn.outline}`}>{t('gift.print')}</button>
        </section>
      )}
      <ul className="flex flex-col gap-2 print:hidden">
        {list.data?.map((v) => (
          <li key={v.id} className="flex flex-wrap items-center justify-between gap-2 rounded-card bg-white p-3 text-sm">
            <span><b className="font-mono">{v.code}</b> · {formatRupees(v.amount)}{v.buyerName ? ` · ${v.buyerName}` : ''} · {date(v.soldAt)}</span>
            <span className={v.cancelledAt ? 'text-due' : 'font-semibold'}>{v.cancelledAt ? t('gift.cancelled') : t('gift.left', { amount: formatRupees(v.balance), date: date(v.expiresOn) })}</span>
            {isOwner && !v.cancelledAt && v.balance === v.amount && <button type="button" onClick={() => void cancel(v)} className={`min-h-9 rounded-lg px-3 text-xs ${btn.dangerOutline}`}>{t('gift.cancel')}</button>}
          </li>
        ))}
      </ul>
    </div>
  )
}

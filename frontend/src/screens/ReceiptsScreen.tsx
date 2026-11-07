import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { CustomerFinder } from '../components/CustomerFinder'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { useCustomers } from '../lib/data'
import { formatRupees } from '../lib/format'

interface Receipt {
  id: string
  amount: number
  left: number
  receivedAt: string
  utr: string | null
  payerName: string | null
  suggestion: { customerId: string; name: string; why: 'amount' | 'name' } | null
}

/**
 * Money that arrived and isn't on anyone's khata yet, including money taken back off the wrong customer. Nothing here
 * moves a balance until the shop matches it. The app only suggests a customer when exactly one fits.
 */
export function ReceiptsScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const key = [shopId, 'receipts']
  const tray = useQuery({ queryKey: key, queryFn: () => api<Receipt[]>('/v1/receipts') })
  const [amount, setAmount] = useState('')
  const [utr, setUtr] = useState('')
  const [payer, setPayer] = useState('')
  const [message, setMessage] = useState('')

  async function refresh() {
    await Promise.all([queryClient.invalidateQueries({ queryKey: key }), queryClient.invalidateQueries({ queryKey: [shopId, 'customers'] })])
  }

  async function run(action: () => Promise<unknown>) {
    setMessage('')
    try {
      await action()
      await refresh()
      return true
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
      return false
    }
  }

  async function note() {
    const done = await run(() => api('/v1/receipts', 'POST', { id: crypto.randomUUID(), amount: Number(amount), utr: utr.trim() || null, payerName: payer.trim() || null }))
    if (done) { setAmount(''); setUtr(''); setPayer('') }
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      <h1 className="text-lg font-bold">{t('receipts.title')}</h1>
      <p className="text-sm text-muted">{t('receipts.hint')}</p>
      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
        <Field label={t('receipts.amount')} value={amount} onChange={(e) => setAmount(e.target.value)} inputMode="decimal" />
        <Field label={t('receipts.utr')} value={utr} onChange={(e) => setUtr(e.target.value)} maxLength={30} />
        <Field label={t('receipts.payer')} value={payer} onChange={(e) => setPayer(e.target.value)} maxLength={120} />
        <Button disabled={!(Number(amount) > 0)} onClick={() => void note()}>{t('receipts.add')}</Button>
      </section>
      {message && <p className="text-sm font-semibold text-due">{message}</p>}
      {tray.data?.length === 0 && <p className="py-6 text-center text-muted">{t('receipts.empty')}</p>}
      {tray.data?.map((r) => <ReceiptCard key={r.id} shopId={shopId} receipt={r} isOwner={isOwner} run={run} />)}
    </div>
  )
}

function ReceiptCard({ shopId, receipt: r, isOwner, run }: { shopId: string; receipt: Receipt; isOwner: boolean; run: (action: () => Promise<unknown>) => Promise<boolean> }) {
  const { t } = useTranslation()
  const { customers } = useCustomers(shopId)
  const [customerId, setCustomerId] = useState('')
  const [part, setPart] = useState(String(r.left))
  const chosen = customers.find((c) => c.id === customerId) ?? null
  const matched = r.left < r.amount
  const allocate = (id: string, value: number) => run(() => api(`/v1/receipts/${r.id}/allocate`, 'POST', { id: crypto.randomUUID(), customerId: id, amount: value }))
  const dismiss = (as: 'counterSale' | 'notKhata') => run(() => api(`/v1/receipts/${r.id}/dismiss`, 'POST', { as }))

  return (
    <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="text-xl font-bold">{formatRupees(r.left)}</div>
          {matched && <div className="text-sm text-muted">{t('receipts.leftOf', { amount: formatRupees(r.amount) })}</div>}
          <div className="truncate text-sm text-muted">{[r.payerName, r.utr && `UTR ${r.utr}`].filter(Boolean).join(' · ')}</div>
        </div>
        <span className="shrink-0 text-sm text-muted">{new Date(r.receivedAt).toLocaleDateString('en-IN', { day: 'numeric', month: 'short' })}</span>
      </div>
      {r.suggestion && (
        <Button onClick={() => void allocate(r.suggestion!.customerId, r.left)}>{t(`receipts.suggest_${r.suggestion.why}`, { name: r.suggestion.name })}</Button>
      )}
      <CustomerFinder customers={customers} selected={chosen} onKhata={0} onPick={setCustomerId} inputId={`receipt-customer-${r.id}`} placeholder={t('receipts.findCustomer')} />
      {chosen && (
        <div className="flex items-end gap-2">
          <div className="min-w-0 flex-1"><Field label={t('receipts.part')} value={part} onChange={(e) => setPart(e.target.value)} inputMode="decimal" /></div>
          <Button disabled={!(Number(part) > 0)} onClick={() => void allocate(chosen.id, Number(part))}>{t('receipts.match')}</Button>
        </div>
      )}
      {isOwner && !matched && (
        <div className="flex flex-wrap gap-3">
          <button onClick={() => void dismiss('counterSale')} className="min-h-11 text-sm font-semibold text-teal-700 underline">{t('receipts.counterSale')}</button>
          <button onClick={() => void dismiss('notKhata')} className="min-h-11 text-sm font-semibold text-teal-700 underline">{t('receipts.notKhata')}</button>
        </div>
      )}
    </section>
  )
}

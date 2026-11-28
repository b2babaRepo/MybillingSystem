import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { addCustomer, useCustomers } from '../lib/data'
import { CustomerFinder } from './CustomerFinder'
import { CashLimitNote } from './CashLimitNote'
import { useNow } from '../lib/useNow'
import { shownError } from '../lib/notify'

/**
 * On a paid walk-in bill: put it on a customer's khata now that we know who it was. The bill itself (number, GST,
 * money) stays as it is; their statement gains the purchase and its payment, so what they owe doesn't change.
 */
export function AttachCustomer({ shopId, billId, occurredAt, cash, onDone }: { shopId: string; billId: string; occurredAt: string; cash: number; onDone: (customerId: string) => void }) {
  const { t } = useTranslation()
  const { customers } = useCustomers(shopId)
  const [open, setOpen] = useState(false)
  const [picked, setPicked] = useState<string | null>(null)
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const now = useNow()
  const old = now - Date.parse(occurredAt) > 7 * 86_400_000
  const customer = customers.find((c) => c.id === picked) ?? null

  async function save() {
    if (!picked) return
    setBusy(true)
    setError(null)
    try {
      await api(`/v1/bills/${billId}/attach-customer`, 'POST', { customerId: picked, reason: reason.trim() || null })
      onDone(picked)
    } catch (e) {
      setError(shownError(e))
    } finally {
      setBusy(false)
    }
  }

  if (!open) {
    return <button onClick={() => setOpen(true)} className="mt-4 h-11 w-full rounded-xl border border-brand font-semibold text-brand-strong print:hidden">{t('attach.button')}</button>
  }
  return (
    <section className="mt-4 flex flex-col gap-2 rounded-xl border border-line p-3 text-sm print:hidden">
      <b>{t('attach.title')}</b>
      <p className="text-muted">{t('attach.hint')}</p>
      <CustomerFinder customers={customers} selected={customer} onKhata={0} onPick={(id) => setPicked(id || null)} onCreate={(name, phone) => addCustomer(shopId, name, phone)} inputId="attach-customer" />
      {/* The ₹2 lakh cash rule counts per person per day: with a name, this bill's cash joins what they paid that day. */}
      {picked && <CashLimitNote customerId={picked} cash={cash} day={new Date(Date.parse(occurredAt) + 19_800_000).toISOString().slice(0, 10)} />}
      {old && (
        <label className="flex flex-col gap-1">{t('attach.reason')}
          <input value={reason} onChange={(e) => setReason(e.target.value.slice(0, 200))} className="h-11 rounded-xl border border-line px-3" /></label>
      )}
      {error && <p className="text-due">{error}</p>}
      <div className="flex gap-2">
        <button disabled={!picked || busy || (old && reason.trim().length < 3)} onClick={() => void save()} className="h-11 flex-1 rounded-xl bg-brand font-semibold text-white disabled:opacity-40">{t('attach.save')}</button>
        <button onClick={() => setOpen(false)} className="h-11 rounded-xl border border-line px-4 font-semibold">{t('cancel')}</button>
      </div>
    </section>
  )
}

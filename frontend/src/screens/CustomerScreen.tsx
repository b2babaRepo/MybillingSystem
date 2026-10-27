import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Keypad } from '../components/Keypad'
import { Balance, Button, PendingDot } from '../components/ui'
import { addKhataEntry, reverseEntry, undoEntry, useCustomers, useStatement } from '../lib/data'
import { formatDateTime, formatRupees, whatsAppLink } from '../lib/format'
import type { KhataEntryType, PaymentMode } from '../lib/types'

const UNDO_SECONDS = 30

/**
 * The fast path from the plan: pick a customer, type the amount, tap Udhaar or Jama. No items needed.
 * Saved to the phone at once and synced in the background.
 */
export function CustomerScreen({ shopId, shopName, customerId }: { shopId: string; shopName: string; customerId: string }) {
  const { t } = useTranslation()
  const { customers } = useCustomers(shopId)
  const customer = customers.find((c) => c.id === customerId)
  const { entries } = useStatement(shopId, customer)
  const [amount, setAmount] = useState('')
  const [mode, setMode] = useState<PaymentMode>('cash')
  const [note, setNote] = useState('')
  const [lastSaved, setLastSaved] = useState<{ id: string; amount: number } | null>(null)

  useEffect(() => {
    if (!lastSaved) return
    const timer = setTimeout(() => setLastSaved(null), UNDO_SECONDS * 1000)
    return () => clearTimeout(timer)
  }, [lastSaved])

  if (!customer) return <p className="p-4 text-slate-500">…</p>

  const value = Number(amount)
  const valid = value > 0

  async function save(type: KhataEntryType) {
    if (!customer || !valid) return
    const id = await addKhataEntry(shopId, customer, type, value, mode, note.trim())
    setLastSaved({ id, amount: type === 'credit' ? value : -value })
    setAmount('')
    setNote('')
  }

  const reminder = t('reminderText', { name: customer.name, shop: shopName, amount: customer.balance })

  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex items-start justify-between rounded-xl bg-white p-4 shadow-sm">
        <div>
          <div className="text-2xl font-bold">{customer.name}</div>
          {customer.phone && <div className="text-slate-500">{customer.phone}</div>}
        </div>
        <Balance amount={customer.balance} large />
      </div>

      <div className="rounded-xl bg-slate-100 p-3">
        <div className="mb-2 h-14 rounded-xl bg-white px-4 text-right text-4xl leading-[3.5rem] font-bold">
          ₹{amount || '0'}
        </div>
        <Keypad value={amount} onChange={setAmount} />
        <input
          value={note}
          onChange={(e) => setNote(e.target.value)}
          placeholder={t('note')}
          maxLength={200}
          className="mt-2 h-11 w-full rounded-xl border border-slate-300 bg-white px-3"
        />
        <div className="mt-2 flex gap-2 text-sm">
          {(['cash', 'upi'] as const).map((m) => (
            <button
              key={m}
              onClick={() => setMode(m)}
              className={`rounded-full px-3 py-1 font-medium ${mode === m ? 'bg-teal-700 text-white' : 'bg-white text-slate-600'}`}
            >
              {t(m)}
            </button>
          ))}
        </div>
        <div className="mt-3 grid grid-cols-2 gap-2">
          <Button tone="credit" disabled={!valid} onClick={() => save('credit')}>
            + {t('credit')}
          </Button>
          <Button tone="payment" disabled={!valid} onClick={() => save('payment')}>
            − {t('payment')}
          </Button>
        </div>
      </div>

      {customer.balance > 0 && (
        <a className="text-center font-medium text-emerald-700 underline" href={whatsAppLink(customer.phone, reminder)} target="_blank" rel="noreferrer">
          {t('remind')}
        </a>
      )}

      <ul className="divide-y divide-slate-200 rounded-xl bg-white shadow-sm">
        {entries.length === 0 && <li className="p-4 text-slate-500">{t('noEntries')}</li>}
        {[...entries].reverse().map((e) => (
          <li key={e.id} className={`flex items-center gap-3 p-3 ${e.isReversed ? 'opacity-40 line-through' : ''}`}>
            <div className="flex-1">
              <div className="flex items-center gap-2 font-semibold">
                {e.kind === 'reversal' ? t('reversal') : e.amount > 0 ? t('creditShort') : t('paymentShort')}
                {e.paymentMode && <span className="text-xs text-slate-500 uppercase">{t(e.paymentMode)}</span>}
                {e.pending && <PendingDot />}
              </div>
              <div className="text-xs text-slate-500">
                {formatDateTime(e.occurredAt)}
                {e.note && ` · ${e.note}`}
              </div>
            </div>
            <div className={`text-right font-bold ${e.amount > 0 ? 'text-rose-600' : 'text-emerald-600'}`}>
              {e.amount > 0 ? '+' : '−'}
              {formatRupees(Math.abs(e.amount))}
              <div className="text-xs font-normal text-slate-500">{formatRupees(e.runningBalance)}</div>
            </div>
            {e.kind !== 'reversal' && !e.isReversed && !e.pending && (
              <button
                className="text-sm text-slate-400 underline"
                onClick={() => confirm(t('removeConfirm')) && void reverseEntry(shopId, e, customer)}
              >
                {t('remove')}
              </button>
            )}
          </li>
        ))}
      </ul>

      {lastSaved && (
        <div className="fixed inset-x-4 bottom-24 z-10 flex items-center justify-between rounded-xl bg-slate-900 p-4 text-white shadow-lg">
          <span>
            {t('saved')} {formatRupees(Math.abs(lastSaved.amount))}
          </span>
          <button
            className="font-bold text-teal-300"
            onClick={() => {
              void undoEntry(shopId, lastSaved, customer)
              setLastSaved(null)
            }}
          >
            {t('undo')}
          </button>
        </div>
      )}
    </div>
  )
}

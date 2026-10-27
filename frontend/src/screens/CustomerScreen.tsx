import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Keypad } from '../components/Keypad'
import { Balance, Button, PendingDot } from '../components/ui'
import { addKhataEntry, reverseEntry, undoEntry, useCustomers, useStatement } from '../lib/data'
import { request } from '../lib/api'
import { formatDateTime, formatRupees, whatsAppLink } from '../lib/format'
import { buildReminder, useShopSettings } from '../lib/reports'
import type { KhataEntryType, PaymentMode } from '../lib/types'
import { navigate } from '../router'

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
  const settings = useShopSettings(shopId)
  const [amount, setAmount] = useState('')
  const [mode, setMode] = useState<PaymentMode>('cash')
  const [note, setNote] = useState('')
  const [lastSaved, setLastSaved] = useState<{ id: string; amount: number } | null>(null)

  useEffect(() => {
    if (!lastSaved) return
    const timer = setTimeout(() => setLastSaved(null), UNDO_SECONDS * 1000)
    return () => clearTimeout(timer)
  }, [lastSaved])

  if (!customer) return <p className="p-4 text-muted">…</p>

  const value = Number(amount)
  const valid = value > 0

  async function save(type: KhataEntryType) {
    if (!customer || !valid) return
    const id = await addKhataEntry(shopId, customer, type, value, mode, note.trim())
    setLastSaved({ id, amount: type === 'credit' ? value : -value })
    setAmount('')
    setNote('')
  }

  const reminder = buildReminder(settings.data?.reminderTemplate, { name: customer.name, amount: customer.balance, shop: shopName, upi: settings.data?.upiId ?? null })

  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex items-start justify-between rounded-[18px] bg-white p-4">
        <div>
          <div className="text-2xl font-bold">{customer.name}</div>
          {customer.phone && <div className="text-muted">{customer.phone}</div>}
        </div>
        <Balance amount={customer.balance} large />
      </div>

      <div className="flex flex-col">
        <div className="mb-2.5 h-[76px] rounded-[18px] bg-white px-5 text-right text-[44px] leading-[76px] font-extrabold">
          ₹{amount || '0'}
        </div>
        <Keypad value={amount} onChange={setAmount} />
        <input
          value={note}
          onChange={(e) => setNote(e.target.value)}
          placeholder={t('note')}
          maxLength={200}
          className="mt-2.5 h-11 w-full rounded-xl border border-line bg-white px-3 outline-none focus:border-teal-600"
        />
        <div className="mt-2 flex gap-2 text-sm">
          {(['cash', 'upi'] as const).map((m) => (
            <button
              key={m}
              onClick={() => setMode(m)}
              className={`h-11 rounded-xl px-4 font-semibold ${mode === m ? 'bg-ink text-white' : 'border border-line bg-white text-ink'}`}
            >
              {t(m)}
            </button>
          ))}
        </div>
        <div className="mt-3 grid grid-cols-2 gap-2">
          <Button tone="credit" className="h-16" disabled={!valid} onClick={() => save('credit')}>
            + {t('credit')}
          </Button>
          <Button tone="payment" className="h-16" disabled={!valid} onClick={() => save('payment')}>
            − {t('payment')}
          </Button>
        </div>
      </div>

      {!customer.isNew && (
        <div className="grid grid-cols-2 gap-2">
          {[0, -1].map((offset) => {
            const month = monthKey(offset)
            return (
              <button key={month} onClick={() => navigate(`/print/statement/${customer.id}/${month}`)} className="min-h-12 rounded-2xl border border-line bg-white px-2 font-semibold">
                {t('print.monthStatement')} · {monthLabel(month)}
              </button>
            )
          })}
        </div>
      )}

      {customer.balance > 0 && (
        <a className="flex h-12 items-center justify-center rounded-2xl bg-[#15803d] font-bold text-white" href={whatsAppLink(customer.phone, reminder)} target="_blank" rel="noreferrer"
          onClick={() => void request(`/v1/customers/${customer.id}/reminded`, 'POST').catch(() => {})}>
          {t('remind')}
        </a>
      )}

      <ul className="divide-y divide-[#edf2f1] rounded-2xl bg-white px-1">
        {entries.length === 0 && <li className="p-4 text-muted">{t('noEntries')}</li>}
        {[...entries].reverse().map((e) => (
          <li key={e.id} className={`flex items-center gap-3 p-3 ${e.isReversed ? 'opacity-40 line-through' : ''}`}>
            <div className="flex-1">
              <div className="flex items-center gap-2 font-semibold">
                {e.kind === 'reversal' ? t('reversal') : e.amount > 0 ? t('creditShort') : t('paymentShort')}
                {e.paymentMode && <span className="text-xs text-muted uppercase">{t(e.paymentMode)}</span>}
                {e.pending && <PendingDot />}
              </div>
              <div className="text-xs text-muted">
                {formatDateTime(e.occurredAt)}
                {e.note && ` · ${e.note}`}
              </div>
            </div>
            <div className={`text-right font-bold ${e.amount > 0 ? 'text-udhaar' : 'text-jama'}`}>
              {e.amount > 0 ? '+' : '−'}
              {formatRupees(Math.abs(e.amount))}
              <div className="text-xs font-normal text-muted">{formatRupees(e.runningBalance)}</div>
            </div>
            {e.kind !== 'reversal' && !e.isReversed && !e.pending && (
              <button
                className="min-h-11 px-1 text-sm text-muted underline"
                onClick={() => confirm(t('removeConfirm')) && void reverseEntry(shopId, e, customer)}
              >
                {t('remove')}
              </button>
            )}
          </li>
        ))}
      </ul>

      {lastSaved && (
        <div className="fixed inset-x-4 bottom-24 z-10 flex items-center justify-between rounded-2xl bg-ink p-4 text-white shadow-lg">
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

/** "2026-10" for this month (offset 0) or an earlier one, in India time. */
function monthKey(offset: number) {
  const india = new Date(Date.now() + 5.5 * 3600_000)
  const date = new Date(Date.UTC(india.getUTCFullYear(), india.getUTCMonth() + offset, 1))
  return date.toISOString().slice(0, 7)
}

const monthLabel = (month: string) => new Date(`${month}-01T00:00:00Z`).toLocaleDateString('en-IN', { month: 'short', timeZone: 'UTC' })

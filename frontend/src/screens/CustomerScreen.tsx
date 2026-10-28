import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Keypad } from '../components/Keypad'
import { Balance, Button, PendingDot } from '../components/ui'
import { addKhataEntry, reverseEntry, undoEntry, useCustomers, useStatement } from '../lib/data'
import { request } from '../lib/api'
import { formatDateTime, formatPhone, formatRupees, whatsAppLink } from '../lib/format'
import { buildReminder, useShopSettings } from '../lib/reports'
import type { KhataEntryType, PaymentMode, StatementLine } from '../lib/types'
import { navigate } from '../router'

const UNDO_SECONDS = 30

/**
 * One customer's khata. History first: every udhaar with the items behind it (date, quantity, price), every
 * payment, and a line wherever the account came back to zero. "Clear full due" records one payment for the whole
 * balance: the account resets to ₹0 and the history stays, since the ledger is never edited. The New entry tab is
 * the fast path from the plan: type the amount, tap Udhaar or Jama. Saved to the phone at once, synced later.
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
  const [tab, setTab] = useState<'history' | 'entry'>('history')
  const [open, setOpen] = useState<string | null>(null)

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

  async function settleAll() {
    if (!customer || customer.balance <= 0) return
    const due = customer.balance
    if (!confirm(t('ledgerView.settleConfirm', { amount: formatRupees(due), mode: t(mode) }))) return
    const id = await addKhataEntry(shopId, customer, 'payment', due, mode, t('ledgerView.settledNote'))
    setLastSaved({ id, amount: -due })
  }

  const modeButtons = (
    <div className="flex gap-2 text-sm">
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
  )

  const reminder = buildReminder(settings.data?.reminderTemplate, { name: customer.name, amount: customer.balance, shop: shopName, upi: settings.data?.upiId ?? null })

  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex items-start justify-between rounded-[18px] bg-white p-4">
        <div>
          <div className="text-2xl font-bold">{customer.name}</div>
          {customer.phone && <div className="text-muted">{formatPhone(customer.phone)}</div>}
        </div>
        <Balance amount={customer.balance} large />
      </div>

      {customer.balance > 0 && (
        <div className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
          {modeButtons}
          <Button tone="payment" className="h-14" onClick={() => void settleAll()}>
            ✓ {t('ledgerView.settleAll', { amount: formatRupees(customer.balance) })}
          </Button>
          <a className="flex h-12 items-center justify-center rounded-2xl bg-[#15803d] font-bold text-white" href={whatsAppLink(customer.phone, reminder)} target="_blank" rel="noreferrer"
            onClick={() => void request(`/v1/customers/${customer.id}/reminded`, 'POST').catch(() => {})}>
            {t('remind')}
          </a>
        </div>
      )}

      <div role="tablist" className="grid grid-cols-2 rounded-[14px] bg-white p-1">
        {(['history', 'entry'] as const).map((k) => (
          <button
            key={k}
            role="tab"
            aria-selected={tab === k}
            onClick={() => setTab(k)}
            className={`h-11 rounded-[10px] font-semibold ${tab === k ? 'bg-teal-700 text-white' : 'text-muted'}`}
          >
            {t(`ledgerView.${k}`)}
          </button>
        ))}
      </div>

      {tab === 'entry' && (
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
          <div className="mt-2">{modeButtons}</div>
          <div className="mt-3 grid grid-cols-2 gap-2">
            <Button tone="credit" className="h-16" disabled={!valid} onClick={() => save('credit')}>
              + {t('credit')}
            </Button>
            <Button tone="payment" className="h-16" disabled={!valid} onClick={() => save('payment')}>
              − {t('payment')}
            </Button>
          </div>
        </div>
      )}

      {tab === 'history' && (
        <>
          <ul className="divide-y divide-[#edf2f1] rounded-2xl bg-white px-1">
            {entries.length === 0 && <li className="p-4 text-muted">{t('noEntries')}</li>}
            {[...entries].reverse().map((e, i, newestFirst) => (
              <li key={e.id}>
                {/* The account came back to zero here: everything below this line is settled. */}
                {e.runningBalance === 0 && i > 0 && !e.isReversed && newestFirst.length > 1 && (
                  <div className="mx-2 mt-2 rounded-lg bg-teal-50 px-3 py-1.5 text-center text-sm font-semibold text-teal-800">
                    ✓ {t('ledgerView.settledDivider', { date: formatDateTime(e.occurredAt) })}
                  </div>
                )}
                <HistoryRow
                  entry={e}
                  open={open === e.id}
                  onToggle={() => setOpen(open === e.id ? null : e.id)}
                  onRemove={() => confirm(t('removeConfirm')) && void reverseEntry(shopId, e, customer)}
                />
              </li>
            ))}
          </ul>
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
        </>
      )}

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

/** One khata line. A bill's udhaar opens to show what was taken: item, quantity × price, total, paid at the counter. */
function HistoryRow({ entry: e, open, onToggle, onRemove }: { entry: StatementLine & { pending?: boolean }; open: boolean; onToggle: () => void; onRemove: () => void }) {
  const { t } = useTranslation()
  const items = e.bill?.items ?? []
  return (
    <div className={e.isReversed ? 'opacity-40' : ''}>
      <div className="flex items-center gap-3 p-3">
        <button
          className="flex flex-1 items-center gap-2 text-left"
          onClick={items.length ? onToggle : undefined}
          aria-expanded={items.length ? open : undefined}
        >
          <div className="flex-1">
            <div className={`flex flex-wrap items-center gap-x-2 font-semibold ${e.isReversed ? 'line-through' : ''}`}>
              {e.kind === 'reversal' ? t('reversal') : e.amount > 0 ? t('creditShort') : t('paymentShort')}
              {e.paymentMode && <span className="text-xs text-muted uppercase">{t(e.paymentMode)}</span>}
              {items.length > 0 && (
                <span className="text-xs font-normal text-teal-700">
                  {t('ledgerView.items', { count: items.length })} {open ? '▴' : '▾'}
                </span>
              )}
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
            <div className="text-xs font-normal text-muted">{t('ledgerView.balanceAfter', { amount: formatRupees(e.runningBalance) })}</div>
          </div>
        </button>
        {e.kind !== 'reversal' && !e.isReversed && !e.pending && (
          <button className="min-h-11 px-1 text-sm text-muted underline" onClick={onRemove}>
            {t('remove')}
          </button>
        )}
      </div>
      {open && e.bill && (
        <div className="mx-3 mb-3 rounded-xl bg-[#f4f8f7] p-3 text-sm">
          {items.map((item, n) => (
            <div key={n} className="flex justify-between gap-2 py-0.5">
              <span className="min-w-0 truncate">{item.name}</span>
              <span className="shrink-0 text-muted">
                {item.quantity} {t(`units.${item.unit}`)} × {formatRupees(item.unitPrice)} = <b className="text-ink">{formatRupees(item.amount)}</b>
              </span>
            </div>
          ))}
          <div className="mt-1.5 flex justify-between border-t border-line pt-1.5 font-semibold">
            <span>{t('ledgerView.billTotal')}</span>
            <span>{formatRupees(e.bill.total)}</span>
          </div>
          {e.bill.paid > 0 && (
            <div className="flex justify-between text-muted">
              <span>{t('ledgerView.paidAtCounter')}</span>
              <span>{formatRupees(e.bill.paid)}</span>
            </div>
          )}
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

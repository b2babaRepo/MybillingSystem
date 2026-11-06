import { lazy, Suspense, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Keypad } from '../components/Keypad'
import { KhataHistory } from '../components/KhataHistory'
import { Balance, Button } from '../components/ui'
import { addKhataEntry, reverseEntry, undoEntry, useCustomers, useStatement } from '../lib/data'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api, request } from '../lib/api'
import { round2 } from '../lib/local'
import { formatDateTime, formatPhone, formatRupees, whatsAppLink } from '../lib/format'
import { buildReminder, useShopSettings } from '../lib/reports'
import type { KhataEntryType, PaymentMode, StatementLine } from '../lib/types'
import { navigate } from '../router'
import { ask, confirmAsk } from '../components/Ask'

const CustomerTradePanel = lazy(() => import('./CustomerTrade').then((module) => ({ default: module.CustomerTradePanel })))

const UNDO_SECONDS = 30
/** Same as the server's LedgerService.RemovalWindow: after this only the owner may remove, with a reason. */
const REMOVAL_WINDOW_MS = 15 * 60_000

type Saved = { id: string; amount: number; dueAfter: number; mode: PaymentMode; at: string }

/**
 * One customer's khata. History first: every udhaar with the items behind it (date, quantity, price), every
 * payment, and a line wherever the account came back to zero. "Clear full due" records one payment for the whole
 * balance: the account resets to ₹0 and the history stays, since the ledger is never edited. The New entry tab is
 * the fast path from the plan: type the amount, tap Udhaar or Jama. Saved to the phone at once, synced later.
 *
 * Transparency: every payment offers a WhatsApp receipt for the customer, and every removal a notice with its
 * reason. Anyone can remove an entry for 15 minutes (a slip); after that only the owner, saying why.
 */
export function CustomerScreen({ shopId, shopName, customerId, focusEntryId, isOwner, userName }: {
  shopId: string
  shopName: string
  customerId: string
  /** Opened from a dispute: that bill is opened and highlighted. */
  focusEntryId?: string
  isOwner: boolean
  userName: string
}) {
  const { t } = useTranslation()
  const { customers } = useCustomers(shopId)
  const customer = customers.find((c) => c.id === customerId)
  const { entries } = useStatement(shopId, customer)
  const settings = useShopSettings(shopId)
  const [amount, setAmount] = useState('')
  const [mode, setMode] = useState<PaymentMode>('cash')
  const [note, setNote] = useState('')
  const [lastSaved, setLastSaved] = useState<Saved | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [tab, setTab] = useState<'history' | 'entry'>('history')
  const [part, setPart] = useState('')
  const [upiRef, setUpiRef] = useState('')

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
    const id = await addKhataEntry(shopId, customer, type, value, mode, note.trim(), upiRef)
    saved(id, type === 'credit' ? value : -value)
    setAmount('')
    setNote('')
  }

  async function settleAll() {
    if (!customer || customer.balance <= 0) return
    const due = customer.balance
    if (!(await confirmAsk(t('ledgerView.settleConfirm', { amount: formatRupees(due), mode: t(mode) })))) return
    const id = await addKhataEntry(shopId, customer, 'payment', due, mode, '', upiRef)
    saved(id, -due)
  }

  function saved(id: string, delta: number) {
    if (!customer) return
    setNotice(null)
    setUpiRef('')
    setLastSaved({ id, amount: delta, dueAfter: round2(customer.balance + delta), mode, at: new Date().toISOString() })
  }

  /** Inside the window anyone may remove with a tap; later the owner must give a reason the customer will see. */
  async function remove(e: StatementLine) {
    if (!customer) return
    const late = Date.now() - new Date(e.occurredAt).getTime() > REMOVAL_WINDOW_MS
    let reason = ''
    if (late) {
      reason = (await ask(t('ledgerView.removeReason')))?.trim() ?? ''
      if (reason.length < 3) return
    } else if (!(await confirmAsk(t('removeConfirm')))) {
      return
    }

    await reverseEntry(shopId, e, customer, reason || undefined)
    setLastSaved(null)
    setNotice(t('ledgerView.removedMessage', {
      shop: shopName,
      amount: formatRupees(Math.abs(e.amount)),
      date: formatDateTime(e.occurredAt),
      by: userName,
      reason: reason || t('ledgerView.noReason'),
      due: formatRupees(round2(customer.balance - e.amount)),
    }))
  }

  // Part payment: any amount up to the due (a quick 25-90% or typed); the rest stays on the khata.
  const partValue = Number(part)
  const partValid = partValue > 0 && partValue <= customer.balance && Math.abs(Math.round(partValue * 100) - partValue * 100) < 1e-6

  async function payPart() {
    if (!customer || !partValid) return
    const id = await addKhataEntry(shopId, customer, 'payment', partValue, mode, '', upiRef)
    saved(id, -partValue)
    setPart('')
  }

  // UPI: the bank reference (UTR) from the payment app. Optional, but a payment with one is locked for good.
  const modeButtons = (
    <div className="flex flex-col gap-2">
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
      {mode === 'upi' && (
        <input
          value={upiRef}
          onChange={(e) => setUpiRef(e.target.value.replace(/[^0-9a-zA-Z]/g, '').slice(0, 30))}
          placeholder={t('ledgerView.upiRefPlaceholder')}
          aria-label={t('ledgerView.upiRefPlaceholder')}
          className="h-11 rounded-xl border border-line bg-white px-3 text-sm outline-none focus:border-teal-600"
        />
      )}
    </div>
  )

  const reminder = buildReminder(settings.data?.reminderTemplate, { name: customer.name, amount: customer.balance, shop: shopName, upi: settings.data?.upiId ?? null, slug: settings.data?.slug })

  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex items-start justify-between rounded-[18px] bg-white p-4">
        <div>
          <div className="text-2xl font-bold">{customer.name}</div>
          {customer.phone && <div className="text-muted">{formatPhone(customer.phone)}</div>}
        </div>
        <Balance amount={customer.balance} large />
      </div>

      {settings.data?.type && settings.data.type !== 'kirana' && (
        <Suspense fallback={null}>
          <CustomerTradePanel shopId={shopId} customer={customer} isOwner={isOwner} />
        </Suspense>
      )}

      {customer.balance > 0 && (
        <div className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
          {modeButtons}
          <Button tone="payment" className="h-14" onClick={() => void settleAll()}>
            ✓ {t('ledgerView.settleAll', { amount: formatRupees(customer.balance) })}
          </Button>
          <div className="mt-1 flex flex-col gap-2 rounded-[14px] border border-line p-3">
            <span className="text-sm font-semibold text-muted">{t('ledgerView.partPay')}</span>
            <div className="grid grid-cols-4 gap-2">
              {[25, 50, 75, 90].map((percent) => {
                const share = Math.round((customer.balance * percent) / 100)
                return (
                  <button
                    key={percent}
                    onClick={() => setPart(String(share))}
                    aria-pressed={part === String(share)}
                    className={`flex min-h-12 flex-col items-center justify-center rounded-xl text-sm font-semibold ${part === String(share) ? 'bg-teal-700 text-white' : 'bg-[#edf2f1] text-ink'}`}
                  >
                    {percent}%<span className="text-xs font-normal">{formatRupees(share)}</span>
                  </button>
                )
              })}
            </div>
            <div className="flex gap-2">
              <input
                value={part}
                onChange={(e) => setPart(e.target.value.replace(/[^\d.]/g, ''))}
                inputMode="decimal"
                placeholder={t('ledgerView.partPlaceholder')}
                aria-label={t('ledgerView.partPay')}
                className="h-12 min-w-0 flex-1 rounded-xl border border-line px-3 text-lg font-bold outline-none focus:border-teal-600"
              />
              <Button tone="payment" className="h-12 px-4" disabled={!partValid} onClick={() => void payPart()}>
                {t('ledgerView.partSave')}
              </Button>
            </div>
            {partValue > 0 && (
              <p className={`text-sm font-semibold ${partValid ? 'text-muted' : 'text-due'}`}>
                {partValid
                  ? t('ledgerView.remaining', { amount: formatRupees(Math.round((customer.balance - partValue) * 100) / 100) })
                  : t('ledgerView.tooMuch')}
              </p>
            )}
          </div>
          <a className="flex h-12 items-center justify-center rounded-2xl bg-[#15803d] font-bold text-white" href={whatsAppLink(customer.phone, reminder)} target="_blank" rel="noreferrer"
            onClick={() => void request(`/v1/customers/${customer.id}/reminded`, 'POST').catch(() => {})}>
            {t('remind')}
          </a>
        </div>
      )}

      {!customer.isNew && <KhataLinkCard customer={customer} shopName={shopName} isOwner={isOwner} />}

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
          <KhataHistory
            entries={entries}
            focusEntryId={focusEntryId}
            canRemove={(e) => isOwner || Date.now() - new Date(e.occurredAt).getTime() <= REMOVAL_WINDOW_MS}
            onRemove={(e) => void remove(e)}
          />
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

      {notice && (
        <div className="fixed inset-x-4 bottom-24 z-10 lg:bottom-8 lg:left-64 mx-auto flex max-w-[34rem] flex-col gap-2 rounded-2xl bg-ink p-4 text-white shadow-lg">
          <span className="text-sm">{t('ledgerView.removedTell')}</span>
          <div className="flex items-center justify-between gap-3">
            <a className="flex h-11 flex-1 items-center justify-center rounded-xl bg-[#15803d] font-bold" href={whatsAppLink(customer.phone, notice)} target="_blank" rel="noreferrer"
              onClick={() => setNotice(null)}>
              {t('ledgerView.sendNotice')}
            </a>
            <button className="px-2 text-sm text-slate-300" onClick={() => setNotice(null)}>{t('ledgerView.later')}</button>
          </div>
        </div>
      )}

      {lastSaved && (
        <div className="fixed inset-x-4 bottom-24 z-10 lg:bottom-8 lg:left-64 flex items-center justify-between gap-2 rounded-2xl bg-ink p-4 text-white shadow-lg">
          <span>
            {t('saved')} {formatRupees(Math.abs(lastSaved.amount))}
          </span>
          {lastSaved.amount < 0 && (
            <a
              className="flex h-10 items-center rounded-xl bg-[#15803d] px-3 text-sm font-bold"
              href={whatsAppLink(customer.phone, t('ledgerView.receiptMessage', {
                shop: shopName,
                amount: formatRupees(-lastSaved.amount),
                name: customer.name,
                date: formatDateTime(lastSaved.at),
                mode: t(lastSaved.mode),
                due: formatRupees(lastSaved.dueAfter),
                ref: lastSaved.id.slice(-6).toUpperCase(),
              }))}
              target="_blank"
              rel="noreferrer"
            >
              {t('ledgerView.sendReceipt')}
            </a>
          )}
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

interface KhataLink {
  shopSlug: string
  customerId: string
  token: string
}

const khataUrl = (link: KhataLink) => `${window.location.origin}/#/k/${link.shopSlug}/${link.customerId}?t=${link.token}`

/**
 * The customer's private, read-only copy of this khata. The link stays the same each time it's shared; the owner
 * can replace it if it went to the wrong person (the old one stops working).
 */
function KhataLinkCard({ customer, shopName, isOwner }: { customer: { id: string; name: string; phone: string | null }; shopName: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const key = ['khata-link', customer.id]
  const link = useQuery({ queryKey: key, queryFn: () => api<KhataLink>(`/v1/customers/${customer.id}/khata-link`, 'POST'), staleTime: Infinity, retry: false })

  async function reset() {
    if (!(await confirmAsk(t('ledgerView.resetConfirm')))) return
    queryClient.setQueryData(key, await api<KhataLink>(`/v1/customers/${customer.id}/khata-link/reset`, 'POST'))
  }

  if (!link.data) return null
  const url = khataUrl(link.data)
  return (
    <div className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
      <span className="text-sm text-muted">{t('ledgerView.linkHint')}</span>
      <a
        className="flex h-12 items-center justify-center rounded-2xl border-2 border-teal-700 font-bold text-teal-800"
        href={whatsAppLink(customer.phone, t('ledgerView.linkMessage', { name: customer.name, shop: shopName, url }))}
        target="_blank"
        rel="noreferrer"
      >
        🔗 {t('ledgerView.shareLink')}
      </a>
      {isOwner && (
        <button className="min-h-11 text-sm text-muted underline" onClick={() => void reset()}>
          {t('ledgerView.resetLink')}
        </button>
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

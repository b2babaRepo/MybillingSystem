import { SendFromShop } from '../components/ShopWhatsApp'
import { shownLocale } from '../i18n'
import { lazy, Suspense, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Keypad } from '../components/Keypad'
import { KhataHistory } from '../components/KhataHistory'
import { KhataTable } from '../components/KhataTable'
import { Balance, Button, Drawer, btn } from '../components/ui'
import { addKhataEntry, reverseEntry, undoEntry, useCustomers, useStatement } from '../lib/data'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api, request } from '../lib/api'
import { round2 } from '../lib/local'
import { formatDateTime, formatPhone, formatRupees, whatsAppLink } from '../lib/format'
import { buildReminder, useShopSettings } from '../lib/reports'
import type { KhataEntryType, PaymentMode, StatementLine } from '../lib/types'
import { navigate } from '../router'
import { ask, confirmAsk, tell } from '../components/Ask'
import { CashLimitNote } from '../components/CashLimitNote'
import { CreditDays } from '../components/CreditDays'

import { OnlineKhataToggle } from '../components/StoreOrderSettings'
const CustomerTradePanel = lazy(() => import('./CustomerTrade').then((module) => ({ default: module.CustomerTradePanel })))

const UNDO_SECONDS = 30
/** Same as the server's LedgerService.RemovalWindow: after this only the owner may remove, with a reason. */
const REMOVAL_WINDOW_MS = 15 * 60_000

type Saved = { id: string; amount: number; dueAfter: number; mode: PaymentMode; at: string; proof?: string }

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
  const queryClient = useQueryClient()
  const customer = customers.find((c) => c.id === customerId)
  const { entries, confirmed } = useStatement(shopId, customer)
  const settings = useShopSettings(shopId)
  const [amount, setAmount] = useState('')
  const [mode, setMode] = useState<PaymentMode>('cash')
  const [note, setNote] = useState('')
  const [lastSaved, setLastSaved] = useState<Saved | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [tab, setTab] = useState<'history' | 'statements' | 'details'>('history')
  // A drawer for taking money or giving on credit; which coloured action is lit (none pressed yet: the likely one).
  const [panel, setPanel] = useState<'pay' | 'entry-pay' | 'entry-credit' | null>(null)
  const [act, setAct] = useState<'pay' | 'credit' | 'remind' | 'statement' | 'bill' | null>(null)
  const [part, setPart] = useState('')
  const [upiRef, setUpiRef] = useState('')
  const [cheque, setCheque] = useState({ no: '', bank: '', date: '' })
  const chequeOk = mode !== 'cheque' || /^\d{4,12}$/.test(cheque.no)

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
    const id = await addKhataEntry(shopId, customer, type, value, mode, note.trim(), upiRef, cheque)
    saved(id, type === 'credit' ? value : -value)
    setAmount('')
    setNote('')
  }

  async function settleAll() {
    if (!customer || customer.balance <= 0) return
    const due = customer.balance
    if (!(await confirmAsk(t('ledgerView.settleConfirm', { amount: formatRupees(due), mode: t(mode) })))) return
    if (!chequeOk) return
    const id = await addKhataEntry(shopId, customer, 'payment', due, mode, '', upiRef, cheque)
    saved(id, -due)
  }

  function saved(id: string, delta: number) {
    if (!customer) return
    setNotice(null)
    // The proof the customer can check: the UPI reference, or the cheque number (which counts once it clears).
    const proof = delta < 0 && mode === 'upi' && upiRef.trim() ? `UTR ${upiRef.trim()}` : delta < 0 && mode === 'cheque' && cheque.no ? t('cheques.receiptProof', { no: cheque.no }) : undefined
    setUpiRef('')
    setCheque({ no: '', bank: '', date: '' })
    setLastSaved({ id, amount: delta, dueAfter: round2(customer.balance + delta), mode, at: new Date().toISOString(), proof })
  }

  /** Inside the window anyone may remove with a tap; later the owner must give a reason the customer will see. */
  async function remove(e: StatementLine) {
    if (!customer) return
    const late = Date.now() - new Date(e.occurredAt).getTime() > REMOVAL_WINDOW_MS
    let reason = ''
    // A UPI payment always says why: the money stays proved received, and goes back to the tray to be matched again.
    if (late || e.upiRef) {
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
    const id = await addKhataEntry(shopId, customer, 'payment', partValue, mode, '', upiRef, cheque)
    saved(id, -partValue)
    setPart('')
  }

  // UPI: the bank reference (UTR) from the payment app. Optional. It proves the money came, and can't be entered twice.
  const modeButtons = (
    <div className="flex flex-col gap-2">
      <div className="grid grid-cols-4 gap-1 rounded-2xl bg-[#edf2f1] p-1 text-sm">
        {(['cash', 'upi', 'card', 'cheque'] as const).map((m) => (
          <button
            key={m}
            onClick={() => setMode(m)}
            aria-pressed={mode === m}
            className={`flex h-14 flex-col items-center justify-center rounded-xl font-semibold transition ${mode === m ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted hover:text-ink'}`}
          >
            <span aria-hidden className="text-lg leading-none">{m === 'cash' ? '💵' : m === 'upi' ? '📱' : m === 'card' ? '💳' : '🏦'}</span>{t(m)}
          </button>
        ))}
      </div>
      {mode === 'upi' && (
        <input
          value={upiRef}
          onChange={(e) => setUpiRef(e.target.value.replace(/[^0-9a-zA-Z]/g, '').slice(0, 30))}
          placeholder={t('ledgerView.upiRefPlaceholder')}
          aria-label={t('ledgerView.upiRefPlaceholder')}
          className="h-11 rounded-xl border border-line bg-white px-3 text-sm outline-none focus:border-brand-line"
        />
      )}
      {mode === 'cash' && <CashLimitNote customerId={customerId} cash={Math.max(valid ? value : 0, partValid ? partValue : 0)} />}
      {mode === 'cheque' && (
        <div className="grid grid-cols-2 gap-2 text-sm">
          <input value={cheque.no} onChange={(e) => setCheque({ ...cheque, no: e.target.value.replace(/\D/g, '').slice(0, 12) })} inputMode="numeric"
            placeholder={t('cheques.no')} aria-label={t('cheques.no')} className="h-11 rounded-xl border border-line bg-white px-3 outline-none focus:border-brand-line" />
          <input value={cheque.bank} onChange={(e) => setCheque({ ...cheque, bank: e.target.value.slice(0, 40) })}
            placeholder={t('cheques.bank')} aria-label={t('cheques.bank')} className="h-11 rounded-xl border border-line bg-white px-3 outline-none focus:border-brand-line" />
          <label className="col-span-2 flex items-center gap-2 text-muted">{t('cheques.date')}
            <input type="date" value={cheque.date} onChange={(e) => setCheque({ ...cheque, date: e.target.value })} className="h-11 flex-1 rounded-xl border border-line bg-white px-3 text-ink" /></label>
          <p className="col-span-2 text-xs text-muted">{t('cheques.hint')}</p>
        </div>
      )}
    </div>
  )

  /** Name and number, one question each. A new number means the old link and confirmation stop, so the owner is told first. */
  async function correctDetails() {
    const name = (await ask(t('customerEdit.name'), customer!.name))?.trim()
    if (!name) return
    const phone = await ask(t('customerEdit.phone'), customer!.phone?.replace(/^\+91/, '') ?? '')
    if (phone === null) return
    if (phone.replace(/\D/g, '').slice(-10) !== (customer!.phone ?? '').replace(/\D/g, '').slice(-10) && !(await confirmAsk(t('customerEdit.phoneWarn')))) return
    try {
      await api(`/v1/customers/${customer!.id}`, 'PUT', { name, phone: phone.trim() || null })
      await queryClient.invalidateQueries({ queryKey: [shopId, 'customers'] })
      queryClient.removeQueries({ queryKey: ['khata-link', customer!.id] })
    } catch (e) {
      void tell(e instanceof Error ? e.message : t('error'))
    }
  }

  const reminder = buildReminder(settings.data?.reminderTemplate, { name: customer.name, amount: customer.balance, shop: shopName, upi: settings.data?.upiId ?? null, slug: settings.data?.slug })

  const owes = customer.balance > 0
  const markReminded = () => { if (owes) void request(`/v1/customers/${customer.id}/reminded`, 'POST').catch(() => {}) }
  const icon = (d: string, size = 18) => <svg aria-hidden="true" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d={d} /></svg>
  // Each job keeps its own colour, as on the supplier page; the one in use is filled, the rest a soft tint.
  const ACTIONS = {
    pay: { on: 'bg-[#0b5e4f] text-white shadow-lg shadow-[#0b5e4f]/30', off: 'bg-[#e3f2ee] text-[#0b5e4f] hover:bg-[#d2ebe4]' },
    credit: { on: 'bg-[#a1460a] text-white shadow-lg shadow-[#a1460a]/30', off: 'bg-[#fdf1e3] text-[#a1460a] hover:bg-[#fbe6cc]' },
    remind: { on: 'bg-[#0f6a3d] text-white shadow-lg shadow-[#0f6a3d]/30', off: 'bg-[#e7f8ee] text-[#0f6a3d] hover:bg-[#d3f2e0]' },
    statement: { on: 'bg-[#1d4ed8] text-white shadow-lg shadow-[#1d4ed8]/30', off: 'bg-[#e8effd] text-[#1d4ed8] hover:bg-[#dbe5fc]' },
    bill: { on: 'bg-[#6d28d9] text-white shadow-lg shadow-[#6d28d9]/30', off: 'bg-[#f1eafd] text-[#6d28d9] hover:bg-[#e7dcfb]' },
  } as const
  type Act = keyof typeof ACTIONS
  const lit: Act = act ?? (owes ? 'pay' : 'credit')
  const action = (key: Act) => `flex min-h-[52px] cursor-pointer items-center justify-center gap-2.5 rounded-2xl px-3 text-sm font-bold whitespace-nowrap transition active:scale-[0.98] ${lit === key ? ACTIONS[key].on : ACTIONS[key].off}`
  const dot = (key: Act, d: string) => <span aria-hidden className={`flex h-[30px] w-[30px] shrink-0 items-center justify-center rounded-full ${lit === key ? 'bg-white/20' : 'bg-white'}`}>{icon(d)}</span>
  const showDetails = (settings.data?.type && settings.data.type !== 'kirana') || !customer.isNew
  const tabs = [
    { key: 'history', label: t('ledgerView.history') },
    ...(!customer.isNew ? [{ key: 'statements', label: t('cust3.statements') }] : []),
    ...(showDetails ? [{ key: 'details', label: t('custDetails.title') }] : []),
  ] as { key: 'history' | 'statements' | 'details'; label: string }[]
  const closeAfterSave = () => setPanel(null)
  const entryForm = (only?: 'credit' | 'payment') => (
    <div className="flex flex-col">
      <div className="mb-2.5 h-[76px] rounded-card bg-white px-5 text-right text-[44px] leading-[76px] font-extrabold">₹{amount || '0'}</div>
      <Keypad value={amount} onChange={setAmount} />
      <input value={note} onChange={(e) => setNote(e.target.value)} placeholder={t('note')} maxLength={200}
        className="mt-2.5 h-11 w-full rounded-xl border border-line bg-white px-3 outline-none focus:border-brand-line" />
      {only !== 'credit' && <div className="mt-2">{modeButtons}</div>}
      <div className={`mt-3 grid gap-2 ${only ? 'grid-cols-1' : 'grid-cols-2'}`}>
        {only !== 'payment' && <Button tone="credit" className="h-14" disabled={!valid} onClick={() => void save('credit').then(closeAfterSave)}>+ {t('credit')}</Button>}
        {only !== 'credit' && <Button tone="payment" className="h-14" disabled={!valid || !chequeOk} onClick={() => void save('payment').then(closeAfterSave)}>− {t('payment')}</Button>}
      </div>
    </div>
  )

  return (
    <div className="flex flex-col gap-3 p-4">
      {/* Who, what they owe, and the five jobs. Each opens on its own instead of every form stacked on the page. */}
      <section className="flex flex-col gap-4 rounded-card bg-white p-4 shadow-sm">
        <div className="flex items-center gap-3">
          <span aria-hidden className="flex h-14 w-14 shrink-0 items-center justify-center rounded-full bg-brand-soft text-xl font-extrabold text-brand">
            {customer.name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0]!.toUpperCase()).join('')}
          </span>
          <span className="flex min-w-0 flex-1 flex-col">
            <span className="truncate text-2xl font-extrabold">{customer.name}</span>
            <span className="flex flex-wrap items-center gap-x-2 text-sm text-muted">
              {customer.phone && <a href={`tel:${customer.phone}`} className="hover:text-brand-strong">{formatPhone(customer.phone)}</a>}
              {customer.phone && isOwner && !customer.isNew && <span aria-hidden>·</span>}
              {isOwner && !customer.isNew && <button onClick={() => void correctDetails()} className={`min-h-9 ${btn.link}`}>{t('customerEdit.button')}</button>}
            </span>
          </span>
          <div className={`shrink-0 rounded-2xl px-4 py-2 ${owes ? 'bg-due-soft' : customer.balance < 0 ? 'bg-green-50' : 'bg-[#edf2f1]'}`}><Balance amount={customer.balance} large /></div>
        </div>
        {customer.phone && !customer.isNew && (
          <div className="flex gap-2">
            <a href={`tel:${customer.phone}`} aria-label={t('quick.call')} title={t('quick.call')} className="flex h-11 w-11 items-center justify-center rounded-full bg-[#e3f2ee] text-[#0b5e4f] hover:bg-[#d2ebe4]">{icon('M5 4h4l2 5-2.5 1.5a11 11 0 005 5L15 13l5 2v4a2 2 0 01-2 2A16 16 0 013 6a2 2 0 012-2')}</a>
            <a href={`sms:${customer.phone}?body=${encodeURIComponent(owes ? reminder : t('quick.hello', { name: customer.name, shop: shopName }))}`} onClick={markReminded} aria-label={owes ? t('quick.smsRemind') : t('quick.sms')} title={owes ? t('quick.smsRemind') : t('quick.sms')}
              className="flex h-11 w-11 items-center justify-center rounded-full bg-[#fdf1e3] text-[#a1460a] hover:bg-[#fbe6cc]">{icon('M4 4h16v12H8l-4 4zM8 9h8M8 12h5')}</a>
          </div>
        )}
        {!customer.isNew && (
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 xl:grid-cols-5">
            <button onClick={() => { setAct('pay'); setPanel(owes ? 'pay' : 'entry-pay') }} aria-pressed={lit === 'pay'} className={action('pay')}>{dot('pay', 'M3 6h18v12H3zM12 9.5a2.5 2.5 0 100 5 2.5 2.5 0 000-5')}{t('cust3.takePayment')}</button>
            <button onClick={() => { setAct('credit'); setPanel('entry-credit') }} aria-pressed={lit === 'credit'} className={action('credit')}>{dot('credit', 'M12 5v14M5 12h14')}{t('cust3.giveCredit')}</button>
            <a href={whatsAppLink(customer.phone, owes ? reminder : t('quick.hello', { name: customer.name, shop: shopName }))} target="_blank" rel="noreferrer" onClick={() => { setAct('remind'); markReminded() }} className={action('remind')}>{dot('remind', 'M4 20l1.4-4A8 8 0 1112 20a8 8 0 01-4-1z')}{owes ? t('cust3.remind') : 'WhatsApp'}</a>
            <button onClick={() => { setAct('statement'); navigate(`/print/statement/${customer.id}/${monthKey(0)}`) }} className={action('statement')}>{dot('statement', 'M6 2h9l4 4v16H6zM9 11h7M9 15h7')}{t('cust3.statement')}</button>
            <button onClick={() => { setAct('bill'); navigate('/bill') }} className={action('bill')}>{dot('bill', 'M6 2h12v20l-3-2-3 2-3-2-3 2zM9 7h6M9 11h6')}{t('cust3.newBill')}</button>
          </div>
        )}
        {customer.isNew && entryForm()}
        {isOwner && customer.nameLater && !customer.isNew && <MoveInto from={customer.id} customers={customers.filter((c) => c.id !== customer.id && !c.nameLater)} onMoved={async (to) => { await queryClient.invalidateQueries({ queryKey: [shopId, 'customers'] }); navigate(`/c/${to}`) }} />}
      </section>

      {!customer.isNew && (
        <div role="tablist" aria-label={customer.name} className="-mx-4 flex gap-1 overflow-x-auto border-b border-line px-4 [scrollbar-width:none]">
          {tabs.map((x) => (
            <button key={x.key} role="tab" aria-selected={tab === x.key} onClick={() => setTab(x.key)}
              className={`min-h-12 shrink-0 border-b-[3px] px-3 text-sm whitespace-nowrap ${tab === x.key ? 'border-brand font-bold text-brand' : 'border-transparent font-semibold text-muted hover:text-ink'}`}>{x.label}</button>
          ))}
        </div>
      )}

      {tab === 'history' && !customer.isNew && (
        <>
          {confirmed && <p className="rounded-xl bg-brand-soft px-3 py-2 text-sm font-semibold text-brand-deep">✓ {t('cust9.shopSees', { amount: formatRupees(confirmed.balance), day: new Date(confirmed.at).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric' }) })}</p>}
          {/* A computer gets the register table (opened from a disputed entry: the list, which rings it); a phone the card list. */}
          {!focusEntryId && (
            <div className="hidden lg:block">
              <KhataTable entries={entries} customerName={customer.name}
                canRemove={(e) => !e.billId && (isOwner || Date.now() - new Date(e.occurredAt).getTime() <= REMOVAL_WINDOW_MS)}
                onRemove={(e) => void remove(e)} />
            </div>
          )}
          <div className={focusEntryId ? '' : 'lg:hidden'}>
            <KhataHistory entries={entries} focusEntryId={focusEntryId}
              canRemove={(e) => !e.billId && (isOwner || Date.now() - new Date(e.occurredAt).getTime() <= REMOVAL_WINDOW_MS)}
              onRemove={(e) => void remove(e)} />
          </div>
        </>
      )}

      {tab === 'statements' && !customer.isNew && (
        <div className="flex flex-col gap-3">
          <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
            <h2 className="font-bold">{t('print.monthStatement')}</h2>
            <div className="grid grid-cols-2 gap-2">
              {[0, -1].map((offset) => {
                const month = monthKey(offset)
                return (
                  <button key={month} onClick={() => navigate(`/print/statement/${customer.id}/${month}`)} className={`flex min-h-12 items-center justify-center gap-2 rounded-xl px-2 ${btn.outline}`}>
                    {icon('M6 9V3h12v6M3 9h18v8H3zM6 14h12v7H6z')}{monthLabel(month)}
                  </button>
                )
              })}
            </div>
            {customer.phone && <div className="flex flex-wrap gap-2">{owes && <SendFromShop kind="reminder" customerId={customer.id} />}<SendFromShop kind="statement" customerId={customer.id} /></div>}
          </section>
          <KhataLinkCard customer={customer} shopName={shopName} isOwner={isOwner} />
        </div>
      )}

      {tab === 'details' && showDetails && (
        <div className="flex flex-col gap-3">
          {settings.data?.type && settings.data.type !== 'kirana' && (
            <Suspense fallback={null}>
              <CustomerTradePanel shopId={shopId} customer={customer} isOwner={isOwner} />
            </Suspense>
          )}
          {!customer.isNew && <CreditDays shopId={shopId} customerId={customer.id} days={customer.creditDays} isOwner={isOwner} />}
          {!customer.isNew && customer.phone && <OnlineKhataToggle customerId={customer.id} isOwner={isOwner} />}
        </div>
      )}

      {panel === 'pay' && (
        <Drawer title={`${t('cust3.takePayment')} · ${customer.name}`} onClose={() => setPanel(null)}>
          {modeButtons}
          <Button tone="payment" className="h-14 text-lg" disabled={!chequeOk} onClick={() => void settleAll().then(closeAfterSave)}>
            ✓ {t('ledgerView.settleAll', { amount: formatRupees(customer.balance) })}
          </Button>
          <div className="flex flex-col gap-2 rounded-2xl bg-white p-3 ring-1 ring-line">
            <span className="text-sm font-semibold text-muted">{t('ledgerView.partPay')}</span>
            <div className="grid grid-cols-4 gap-2">
              {[25, 50, 75, 90].map((percent) => {
                const share = Math.round((customer.balance * percent) / 100)
                return (
                  <button key={percent} onClick={() => setPart(String(share))} aria-pressed={part === String(share)}
                    className={`flex min-h-12 flex-col items-center justify-center rounded-xl text-sm font-semibold transition ${part === String(share) ? 'bg-brand text-white shadow-sm' : 'bg-white text-ink ring-1 ring-line hover:ring-brand-line/40'}`}>
                    {percent}%<span className="text-xs font-normal">{formatRupees(share)}</span>
                  </button>
                )
              })}
            </div>
            <div className="flex gap-2">
              <input value={part} onChange={(e) => setPart(e.target.value.replace(/[^\d.]/g, ''))} inputMode="decimal" placeholder={t('ledgerView.partPlaceholder')} aria-label={t('ledgerView.partPay')}
                className="h-12 min-w-0 flex-1 rounded-xl border border-line bg-white px-3 text-lg font-bold outline-none focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
              <Button tone="payment" className="h-12 px-4" disabled={!partValid || !chequeOk} onClick={() => void payPart().then(closeAfterSave)}>{t('ledgerView.partSave')}</Button>
            </div>
            {partValue > 0 && (
              <p className={`text-sm font-semibold ${partValid ? 'text-muted' : 'text-due'}`}>
                {partValid ? t('ledgerView.remaining', { amount: formatRupees(Math.round((customer.balance - partValue) * 100) / 100) }) : t('ledgerView.tooMuch')}
              </p>
            )}
          </div>
          <button onClick={() => setPanel('entry-pay')} className={`min-h-11 self-start ${btn.link}`}>{t('cust3.otherAmount')}</button>
        </Drawer>
      )}
      {panel === 'entry-pay' && <Drawer title={`${t('cust3.takePayment')} · ${customer.name}`} onClose={() => setPanel(null)}>{entryForm('payment')}</Drawer>}
      {panel === 'entry-credit' && <Drawer title={`${t('cust3.giveCredit')} · ${customer.name}`} onClose={() => setPanel(null)}>{entryForm('credit')}</Drawer>}

      {notice && (
        <div className="fixed inset-x-4 bottom-24 z-10 lg:bottom-8 mx-auto flex max-w-[34rem] flex-col gap-2 rounded-2xl bg-ink p-4 text-white shadow-lg">
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
        <div className="fixed inset-x-4 bottom-24 z-10 lg:bottom-8 flex items-center justify-between gap-2 rounded-2xl bg-ink p-4 text-white shadow-lg">
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
                mode: lastSaved.proof ? `${t(lastSaved.mode)}, ${lastSaved.proof}` : t(lastSaved.mode),
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
  const [appCode, setAppCode] = useState<string | null>(null)
  async function showAppCode() {
    const made = await api<{ code: string | null; confirmed: boolean }>(`/v1/customers/${customer.id}/phone-code`, 'POST')
    setAppCode(made.confirmed ? t('phoneCode.done') : t('phoneCode.show', { code: made.code }))
  }
  const link = useQuery({ queryKey: key, queryFn: () => api<KhataLink>(`/v1/customers/${customer.id}/khata-link`, 'POST'), staleTime: Infinity, retry: false })

  async function reset() {
    if (!(await confirmAsk(t('ledgerView.resetConfirm')))) return
    queryClient.setQueryData(key, await api<KhataLink>(`/v1/customers/${customer.id}/khata-link/reset`, 'POST'))
  }

  if (!link.data) return null
  const url = khataUrl(link.data)
  return (
    <div className="flex flex-col gap-2 rounded-card bg-white p-3">
      <span className="text-sm text-muted">{t('ledgerView.linkHint')}</span>
      {customer.phone && <span className="text-sm font-semibold">{t('phoneCode.sendsTo', { phone: customer.phone })}</span>}
      <a
        className="flex h-12 items-center justify-center rounded-2xl border-2 border-brand font-bold text-brand-strong"
        href={whatsAppLink(customer.phone, t('ledgerView.linkMessage', { name: customer.name, shop: shopName, url }))}
        target="_blank"
        rel="noreferrer"
      >
        🔗 {t('ledgerView.shareLink')}
      </a>
      {customer.phone && <button onClick={() => void showAppCode()} className="min-h-11 text-sm font-semibold text-brand underline">{t('phoneCode.button')}</button>}
      {appCode && <p className="rounded-xl bg-brand-soft p-2 text-sm font-semibold text-brand-deep">{appCode}</p>}
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

const monthLabel = (month: string) => new Date(`${month}-01T00:00:00Z`).toLocaleDateString(shownLocale(), { month: 'short', timeZone: 'UTC' })

/** A "Name later" khata turned out to be someone already on the list: move everything onto them. */
function MoveInto({ from, customers, onMoved }: { from: string; customers: { id: string; name: string; phone: string | null }[]; onMoved: (to: string) => Promise<void> }) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const [to, setTo] = useState('')
  const [busy, setBusy] = useState(false)
  if (!open) return <button onClick={() => setOpen(true)} className="block min-h-9 text-sm font-semibold text-brand underline">{t('moveInto.button')}</button>
  async function move() {
    const target = customers.find((c) => c.id === to)
    if (!target || !(await confirmAsk(t('moveInto.confirm', { name: target.name })))) return
    setBusy(true)
    try {
      await api(`/v1/customers/${from}/move-into`, 'POST', { customerId: to })
      await onMoved(to)
    } catch (e) {
      void tell(e instanceof Error ? e.message : t('error'))
    } finally {
      setBusy(false)
    }
  }
  return (
    <div className="mt-2 flex flex-wrap gap-2 text-sm">
      <select aria-label={t('moveInto.button')} value={to} onChange={(e) => setTo(e.target.value)} className="h-11 min-w-48 rounded-xl border border-line px-2">
        <option value="">{t('moveInto.pick')}</option>
        {customers.map((c) => <option key={c.id} value={c.id}>{c.name}{c.phone ? ` · ${c.phone.replace(/^\+91/, '')}` : ''}</option>)}
      </select>
      <button disabled={!to || busy} onClick={() => void move()} className={`h-11 px-4 rounded-xl ${btn.solid}`}>{t('moveInto.save')}</button>
      <button onClick={() => setOpen(false)} className="h-11 rounded-xl border border-line px-4 font-semibold">{t('cancel')}</button>
    </div>
  )
}

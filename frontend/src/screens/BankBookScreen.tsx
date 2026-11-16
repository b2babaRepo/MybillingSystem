import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees, indiaToday } from '../lib/format'
import { navigate } from '../router'

type Source = 'bill' | 'bill_cancelled' | 'khata_payment' | 'reversal' | 'expense' | 'deposit'
interface Line { at: string; source: Source; who: string; mode: 'cash' | 'upi' | 'card' | 'cheque' | 'bank'; amount: number; reference: string | null; balance: number; billId: string | null }
interface Book {
  from: string; to: string; opening: number; in: number; out: number; closing: number
  inBy: { upi: number; card: number; cheque: number; deposits: number }
  lines: Line[]; startingBalance: number | null; startingDate: string | null
  account: string; accounts: { id: string; name: string }[] | null
}

const monthOf = (day: string) => day.slice(0, 7)
const lastDay = (month: string) => { const [y, m] = month.split('-').map(Number); return new Date(Date.UTC(y, m, 0)).toISOString().slice(0, 10) }
const shiftMonth = (month: string, by: number) => { const [y, m] = month.split('-').map(Number); return new Date(Date.UTC(y, m - 1 + by, 1)).toISOString().slice(0, 7) }
const when = (iso: string) => new Date(iso).toLocaleString(shownLocale(), { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })

/**
 * The bank book, a month at a time: where UPI, card and cleared cheques land and UPI or bank payments leave, with cash
 * put in from the galla as a contra line. Opening and closing balances, so the owner can tick it against the passbook.
 */
export function BankBookScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const client = useQueryClient()
  const today = indiaToday()
  const [month, setMonth] = useState(monthOf(today))
  const to = month === monthOf(today) ? today : lastDay(month)
  const [account, setAccount] = useState<string | null>(null)
  const [setupOpen, setSetupOpen] = useState(false)
  const book = useQuery({
    queryKey: [shopId, 'bank-book', month, account],
    queryFn: () => api<Book>(`/v1/bank-book?from=${month}-01&to=${to}${account ? `&account=${account}` : ''}`),
  })
  const [editing, setEditing] = useState(false)
  const [amount, setAmount] = useState('')
  const [date, setDate] = useState(today)
  const [error, setError] = useState<string | null>(null)
  const data = book.data
  const label = new Date(`${month}-01T00:00:00Z`).toLocaleDateString(shownLocale(), { month: 'long', year: 'numeric', timeZone: 'UTC' })

  async function saveOpening() {
    setError(null)
    try {
      await api('/v1/bank-book/opening', 'PUT', { amount: amount.trim() === '' ? null : Number(amount), date, account: data?.account ?? null })
      setEditing(false)
      await client.invalidateQueries({ queryKey: [shopId, 'bank-book'] })
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-3 p-4">
      <div className="flex w-fit gap-1 rounded-2xl bg-[#edf2f1] p-1 text-sm">
        <button onClick={() => navigate('/cashbook')} className="h-10 rounded-xl px-4 font-semibold text-muted transition hover:text-ink">{t('bankBook.cashBook')}</button>
        <button onClick={() => navigate('/cashledger')} className="h-10 rounded-xl px-4 font-semibold text-muted transition hover:text-ink">{t('cashLedger.title')}</button>
        <span className="flex h-10 items-center rounded-xl bg-white px-4 font-semibold text-brand-strong shadow-sm ring-1 ring-brand/20">{t('bankBook.title')}</span>
      </div>
      <div className="flex items-center justify-between rounded-card bg-white p-2 shadow-sm">
        <button onClick={() => setMonth(shiftMonth(month, -1))} aria-label={t('book.previous')} className="h-11 w-11 rounded-xl text-xl">‹</button>
        <span className="text-lg font-bold">{label}</span>
        <button onClick={() => setMonth(shiftMonth(month, 1))} disabled={month >= monthOf(today)} aria-label={t('book.next')} className="h-11 w-11 rounded-xl text-xl disabled:opacity-30">›</button>
      </div>

      {data && (
        <div className="flex flex-wrap items-center gap-2 text-sm">
          {(data.accounts?.length ?? 0) > 1 && data.accounts!.map((a) => (
            <button key={a.id} onClick={() => setAccount(a.id)} aria-pressed={data.account === a.id}
              className={`h-10 rounded-xl px-4 font-semibold ${data.account === a.id ? 'bg-brand text-white' : 'border border-line bg-white'}`}>{a.name}</button>
          ))}
          <button onClick={() => setSetupOpen(!setupOpen)} className="h-10 px-2 font-semibold text-brand-strong underline">{t('bankBook.accounts')}</button>
        </div>
      )}
      {setupOpen && <BankAccountsEditor shopId={shopId} onDone={() => setSetupOpen(false)} />}
      {book.isError && <p className="text-due">{book.error.message}</p>}
      {data && (
        <>
          <section className="grid grid-cols-2 gap-2 rounded-[20px] bg-white p-4 text-sm sm:grid-cols-4">
            <span className="flex flex-col"><span className="text-muted">{t('bankBook.opening')}</span><b className="text-lg">{formatRupees(data.opening)}</b></span>
            <span className="flex flex-col"><span className="text-muted">{t('bankBook.in')}</span><b className="text-lg text-jama">+{formatRupees(data.in)}</b></span>
            <span className="flex flex-col"><span className="text-muted">{t('bankBook.out')}</span><b className="text-lg text-udhaar">−{formatRupees(data.out)}</b></span>
            <span className="flex flex-col"><span className="text-muted">{t('bankBook.closing')}</span><b className="text-lg">{formatRupees(data.closing)}</b></span>
            <p className="col-span-2 text-xs text-muted sm:col-span-4">
              {t('upi')} {formatRupees(data.inBy.upi)} · {t('card')} {formatRupees(data.inBy.card)} · {t('cheque')} {formatRupees(data.inBy.cheque)} · {t('bankBook.deposits')} {formatRupees(data.inBy.deposits)}
            </p>
          </section>

          <section className="flex flex-col gap-2 rounded-[20px] bg-white p-4 text-sm">
            <p className="text-muted">
              {data.startingDate ? t('bankBook.startsAt', { amount: formatRupees(data.startingBalance ?? 0), date: data.startingDate }) : t('bankBook.noStart')}
              {' '}<button className="font-semibold text-brand-strong underline" onClick={() => { setEditing(!editing); setAmount(data.startingBalance?.toString() ?? ''); setDate(data.startingDate ?? today) }}>{t('bankBook.setStart')}</button>
            </p>
            {editing && (
              <div className="flex flex-wrap items-end gap-2">
                <label className="flex flex-col gap-1">{t('bankBook.passbookBalance')}
                  <input value={amount} onChange={(e) => setAmount(e.target.value.replace(/[^0-9.-]/g, ''))} inputMode="decimal" className="h-11 w-40 rounded-xl border border-line px-3 bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /></label>
                <label className="flex flex-col gap-1">{t('bankBook.onDate')}
                  <input type="date" value={date} max={today} onChange={(e) => setDate(e.target.value)} className="h-11 rounded-xl border border-line px-3 bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /></label>
                <button onClick={() => void saveOpening()} className="h-11 rounded-xl bg-ink px-4 font-semibold text-white">{t('bankBook.save')}</button>
              </div>
            )}
            {error && <p className="text-due">{error}</p>}
          </section>

          {data.lines.length === 0 ? <p className="text-muted">{t('bankBook.empty')}</p> : (
            <ul className="flex flex-col divide-y divide-line rounded-[20px] bg-white">
              {data.lines.map((l, i) => (
                <li key={i} className="flex items-center gap-3 p-3 text-sm">
                  <div className="flex flex-1 flex-col">
                    <span className="font-semibold">{t(`bankBook.sources.${l.source}`)}{l.who ? ` · ${l.who}` : ''}</span>
                    <span className="text-xs text-muted">{when(l.at)} · {l.source === 'deposit' ? t('bankBook.fromGalla') : t(l.mode)}{l.reference ? ` · ${l.reference}` : ''}</span>
                  </div>
                  <span className="flex flex-col items-end">
                    <b className={l.amount < 0 ? 'text-udhaar' : 'text-jama'}>{l.amount < 0 ? '−' : '+'}{formatRupees(Math.abs(l.amount))}</b>
                    <span className="text-xs text-muted">{formatRupees(l.balance)}</span>
                  </span>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </div>
  )
}

interface Setup { accounts: { id: string; name: string }[]; upi: string; card: string; cheque: string; out: string; deposit: string }
const KINDS = ['upi', 'card', 'cheque', 'out', 'deposit'] as const

/** Up to three accounts by name, and which kind of money goes to which: UPI, card, cheques, payments out, cash deposits. */
function BankAccountsEditor({ shopId, onDone }: { shopId: string; onDone: () => void }) {
  const { t } = useTranslation()
  const client = useQueryClient()
  const current = useQuery({ queryKey: [shopId, 'bank-accounts'], queryFn: () => api<Setup>('/v1/bank-book/accounts') })
  const [draft, setDraft] = useState<Setup | null>(null)
  const [error, setError] = useState<string | null>(null)
  const setup = draft ?? current.data
  if (!setup) return null
  // A new account has no id yet: a kind points at it by its place in the list.
  const keyOf = (i: number) => setup.accounts[i].id || `#${i}`

  async function save() {
    setError(null)
    try {
      await api('/v1/bank-book/accounts', 'PUT', setup)
      await client.invalidateQueries({ queryKey: [shopId] })
      onDone()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  return (
    <section className="flex flex-col gap-2 rounded-[20px] bg-white p-4 text-sm">
      <h2 className="font-bold">{t('bankBook.accounts')}</h2>
      {setup.accounts.map((a, i) => (
        <div key={i} className="flex gap-2">
          <input value={a.name} maxLength={40} aria-label={t('bankBook.accountName')} placeholder={t('bankBook.accountName')}
            onChange={(e) => setDraft({ ...setup, accounts: setup.accounts.map((x, j) => (j === i ? { ...x, name: e.target.value } : x)) })}
            className="h-11 flex-1 rounded-xl border border-line px-3 bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
          {setup.accounts.length > 1 && (
            <button aria-label={t('expense.remove')} className="h-11 w-11 text-xl text-muted"
              onClick={() => setDraft({ ...setup, accounts: setup.accounts.filter((_, j) => j !== i) })}>×</button>
          )}
        </div>
      ))}
      {setup.accounts.length < 3 && (
        <button className="self-start font-semibold text-brand-strong underline" onClick={() => setDraft({ ...setup, accounts: [...setup.accounts, { id: '', name: '' }] })}>{t('bankBook.addAccount')}</button>
      )}
      {setup.accounts.length > 1 && KINDS.map((k) => (
        <label key={k} className="flex items-center justify-between gap-2">{t(`bankBook.kinds.${k}`)}
          <select value={setup[k]} onChange={(e) => setDraft({ ...setup, [k]: e.target.value })} className="h-10 rounded-xl border border-line px-2 bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10">
            {setup.accounts.map((a, i) => <option key={i} value={keyOf(i)}>{a.name || '…'}</option>)}
          </select>
        </label>
      ))}
      {error && <p className="text-due">{error}</p>}
      <button onClick={() => void save()} className="h-11 rounded-xl bg-ink px-4 font-semibold text-white">{t('bankBook.save')}</button>
    </section>
  )
}

import { btn } from '../components/ui'
import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees, indiaToday } from '../lib/format'
import { navigate } from '../router'
import { DataTable } from '../components/DataTable'

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
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-3 p-4 lg:max-w-5xl">
      <div className="flex w-fit gap-1 rounded-2xl bg-[#edf2f1] p-1 text-sm">
        <button onClick={() => navigate('/cashbook')} className="h-10 rounded-xl px-4 font-semibold text-muted transition hover:text-ink">{t('bankBook.cashBook')}</button>
        <button onClick={() => navigate('/cashledger')} className="h-10 rounded-xl px-4 font-semibold text-muted transition hover:text-ink">{t('cashLedger.title')}</button>
        <span className="flex h-10 items-center rounded-xl bg-brand px-4 font-semibold text-white shadow-md shadow-teal-900/20">{t('bankBook.title')}</span>
      </div>
      <div className="flex items-center justify-between rounded-card bg-white p-2 shadow-sm">
        <button onClick={() => setMonth(shiftMonth(month, -1))} aria-label={t('book.previous')} className="flex h-11 w-11 items-center justify-center rounded-xl hover:bg-[#f1f5f4]"><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M15 18l-6-6 6-6" /></svg></button>
        <span className="flex items-center gap-2 text-lg font-bold"><span className="text-brand"><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M4 5h16v15H4zM4 9h16M8 3v4M16 3v4" /></svg></span>{label}</span>
        <button onClick={() => setMonth(shiftMonth(month, 1))} disabled={month >= monthOf(today)} aria-label={t('book.next')} className="flex h-11 w-11 items-center justify-center rounded-xl hover:bg-[#f1f5f4] disabled:opacity-30"><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M9 6l6 6-6 6" /></svg></button>
      </div>

      {data && (
        <div className="flex flex-wrap items-center gap-2 text-sm">
          {(data.accounts?.length ?? 0) > 1 && data.accounts!.map((a) => (
            <button key={a.id} onClick={() => setAccount(a.id)} aria-pressed={data.account === a.id}
              className={`flex h-10 items-center gap-1.5 rounded-xl px-4 font-semibold transition ${data.account === a.id ? 'bg-[#1d4ed8] text-white shadow-md shadow-[#1d4ed8]/30' : 'bg-[#e8effd] text-[#1d4ed8] hover:bg-[#dbe5fc]'}`}><svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M3 10l9-6 9 6M5 10v8M9 10v8M15 10v8M19 10v8M3 21h18" /></svg>{a.name}</button>
          ))}
          <button onClick={() => setSetupOpen(!setupOpen)} aria-expanded={setupOpen} className={`flex h-10 items-center gap-1.5 rounded-xl px-3 ${btn.outline}`}><svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M12 9a3 3 0 100 6 3 3 0 000-6zM12 2v3M12 19v3M4.2 4.2l2.1 2.1M17.7 17.7l2.1 2.1M2 12h3M19 12h3M4.2 19.8l2.1-2.1M17.7 6.3l2.1-2.1" /></svg>{t('bankBook.accounts')}</button>
        </div>
      )}
      {setupOpen && <BankAccountsEditor shopId={shopId} onDone={() => setSetupOpen(false)} />}
      {book.isError && <p className="text-due">{book.error.message}</p>}
      {data && (
        <>
          <section className="flex flex-col gap-2">
            <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
              <span className="flex min-h-[76px] flex-col justify-between rounded-card bg-white p-3 shadow-sm ring-1 ring-black/5"><span className="text-xs font-semibold text-muted">{t('bankBook.opening')}</span><b className="text-xl">{formatRupees(data.opening)}</b></span>
              <span className="flex min-h-[76px] flex-col justify-between rounded-card bg-emerald-50 p-3 shadow-sm ring-1 ring-black/5"><span className="flex items-center gap-1.5 text-xs font-semibold text-emerald-800"><svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M12 19V5M5 12l7-7 7 7" /></svg>{t('bankBook.in')}</span><b className="text-xl text-jama">+{formatRupees(data.in)}</b></span>
              <span className="flex min-h-[76px] flex-col justify-between rounded-card bg-due-soft p-3 shadow-sm ring-1 ring-black/5"><span className="flex items-center gap-1.5 text-xs font-semibold text-due"><svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M12 5v14M5 12l7 7 7-7" /></svg>{t('bankBook.out')}</span><b className="text-xl text-due">−{formatRupees(data.out)}</b></span>
              <span className="flex min-h-[76px] flex-col justify-between rounded-card bg-[#1d4ed8] p-3 text-white shadow-md shadow-[#1d4ed8]/25"><span className="flex items-center gap-1.5 text-xs font-semibold text-white/85"><svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M3 10l9-6 9 6M5 10v8M9 10v8M15 10v8M19 10v8M3 21h18" /></svg>{t('bankBook.closing')}</span><b className="text-xl">{formatRupees(data.closing)}</b></span>
            </div>
            <div className="flex flex-wrap gap-1.5 px-1 text-xs font-semibold">
              <span className="rounded-full bg-blue-50 px-2.5 py-1 text-blue-800">{t('upi')} {formatRupees(data.inBy.upi)}</span>
              <span className="rounded-full bg-amber-50 px-2.5 py-1 text-amber-800">{t('card')} {formatRupees(data.inBy.card)}</span>
              <span className="rounded-full bg-violet-50 px-2.5 py-1 text-violet-800">{t('cheque')} {formatRupees(data.inBy.cheque)}</span>
              <span className="rounded-full bg-emerald-50 px-2.5 py-1 text-emerald-800">{t('bankBook.deposits')} {formatRupees(data.inBy.deposits)}</span>
            </div>
          </section>

          <section className="flex flex-col gap-3 rounded-[20px] bg-white p-4 text-sm shadow-sm">
            <div className="flex flex-wrap items-center gap-3">
              <span aria-hidden className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[#e8effd] text-[#1d4ed8]"><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M4 4h11a3 3 0 013 3v13H7a3 3 0 01-3-3zM4 17a3 3 0 013-3h11" /></svg></span>
              <p className="min-w-0 flex-1 text-muted">{data.startingDate ? t('bankBook.startsAt', { amount: formatRupees(data.startingBalance ?? 0), date: data.startingDate }) : t('bankBook.noStart')}</p>
              <button aria-expanded={editing} className={`min-h-10 rounded-xl px-3 ${btn.outline}`} onClick={() => { setEditing(!editing); setAmount(data.startingBalance?.toString() ?? ''); setDate(data.startingDate ?? today) }}>{t('bankBook.setStart')}</button>
            </div>
            {editing && (
              <div className="flex flex-wrap items-end gap-2">
                <label className="flex flex-col gap-1">{t('bankBook.passbookBalance')}
                  <input value={amount} onChange={(e) => setAmount(e.target.value.replace(/[^0-9.-]/g, ''))} inputMode="decimal" className="h-11 w-40 rounded-xl border border-line px-3 bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /></label>
                <label className="flex flex-col gap-1">{t('bankBook.onDate')}
                  <input type="date" value={date} max={today} onChange={(e) => setDate(e.target.value)} className="h-11 rounded-xl border border-line px-3 bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /></label>
                <button onClick={() => void saveOpening()} className={`h-11 rounded-xl px-5 ${btn.solid}`}>{t('bankBook.save')}</button>
              </div>
            )}
            {error && <p className="text-due">{error}</p>}
          </section>

          {/* A computer: the bank book as a passbook-style table. */}
          {data.lines.length > 0 && (
            <div className="hidden lg:block">
              <DataTable label={t('bankBook.title')} rows={data.lines.map((l, i) => ({ ...l, n: i }))} rowKey={(l) => String(l.n)} download={`bank-book-${month}`}
                onRowClick={(l) => { if (l.billId) navigate(`/print/bill/${l.billId}`) }}
                columns={[
                  { key: 'at', header: t('cashTable.date'), width: 'w-36', sortValue: (l) => l.at, render: (l) => when(l.at), csv: (l) => new Date(l.at).toLocaleString() },
                  { key: 'type', header: t('cashTable.type'), width: 'w-40', sortValue: (l) => t(`bankBook.sources.${l.source}`),
                    filter: { kind: 'select', value: (l) => l.source, options: [...new Set(data.lines.map((l) => l.source))].map((src) => ({ value: src, label: t(`bankBook.sources.${src}`) })) },
                    render: (l) => <span className="font-semibold">{t(`bankBook.sources.${l.source}`)}</span>, csv: (l) => t(`bankBook.sources.${l.source}`) },
                  { key: 'who', header: t('cashTable.party'), sortValue: (l) => l.who || '', filter: { kind: 'text', value: (l) => l.who ?? '' }, render: (l) => l.who || '—', csv: (l) => l.who ?? '' },
                  { key: 'mode', header: t('loans.paidBy'), width: 'w-28', filter: { kind: 'select', value: (l) => (l.source === 'deposit' ? 'galla' : l.mode), options: [...new Set(data.lines.map((l) => (l.source === 'deposit' ? 'galla' : l.mode)))].map((m) => ({ value: m, label: m === 'galla' ? t('bankBook.fromGalla') : t(m) })) },
                    render: (l) => (l.source === 'deposit' ? t('bankBook.fromGalla') : t(l.mode)), csv: (l) => (l.source === 'deposit' ? t('bankBook.fromGalla') : t(l.mode)) },
                  { key: 'ref', header: t('bankTable.ref'), width: 'w-32', filter: { kind: 'text', value: (l) => l.reference ?? '' }, render: (l) => l.reference ?? '—', csv: (l) => l.reference ?? '' },
                  { key: 'in', header: t('bankTable.in'), align: 'right', width: 'w-28', sortValue: (l) => (l.amount > 0 ? l.amount : null), render: (l) => <span className="font-semibold text-jama">{l.amount > 0 ? formatRupees(l.amount) : ''}</span>, csv: (l) => (l.amount > 0 ? l.amount : '') },
                  { key: 'out', header: t('bankTable.out'), align: 'right', width: 'w-28', sortValue: (l) => (l.amount < 0 ? -l.amount : null), render: (l) => <span className="font-semibold text-udhaar">{l.amount < 0 ? formatRupees(-l.amount) : ''}</span>, csv: (l) => (l.amount < 0 ? -l.amount : '') },
                  { key: 'bal', header: t('cashTable.balance'), align: 'right', width: 'w-32', render: (l) => formatRupees(l.balance), csv: (l) => l.balance },
                ]}
                footer={[t('bankTable.totals', { opening: formatRupees(data.opening) }), '', '', '', '', formatRupees(data.in), formatRupees(data.out), formatRupees(data.closing)]} />
            </div>
          )}
          {data.lines.length === 0 ? <p className="flex items-center gap-3 rounded-card bg-white p-4 text-muted shadow-sm"><span aria-hidden className="flex h-10 w-10 items-center justify-center rounded-full bg-[#edf2f1]"><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M3 10l9-6 9 6M5 10v8M9 10v8M15 10v8M19 10v8M3 21h18" /></svg></span>{t('bankBook.empty')}</p> : (
            <ul className="flex flex-col divide-y divide-line rounded-[20px] bg-white shadow-sm lg:hidden">
              {data.lines.map((l, i) => (
                <li key={i} className="flex items-center gap-3 p-3 text-sm">
                  <span aria-hidden className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full ${l.amount < 0 ? 'bg-due-soft text-due' : 'bg-emerald-50 text-emerald-700'}`}><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d={l.amount < 0 ? 'M12 5v14M5 12l7 7 7-7' : 'M12 19V5M5 12l7-7 7 7'} /></svg></span>
                  <div className="flex min-w-0 flex-1 flex-col">
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
        <button className={`self-start ${btn.link}`} onClick={() => setDraft({ ...setup, accounts: [...setup.accounts, { id: '', name: '' }] })}>{t('bankBook.addAccount')}</button>
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

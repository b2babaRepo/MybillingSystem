import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees, indiaToday } from '../lib/format'
import { uuidv7 } from '../lib/ids'
import { navigate } from '../router'

interface Line { at: string; source: string; who: string; amount: number; balance: number; moveId: string | null; billId: string | null }
interface Day { date: string; in: number; out: number; closing: number; counted: number | null; difference: number | null }
interface Ledger { from: string; to: string; opening: number; openingCountedOn: string | null; in: number; out: number; closing: number; lines: Line[]; days: Day[] }
type Kind = 'added' | 'removed' | 'to_bank' | 'from_bank'

const monthOf = (day: string) => day.slice(0, 7)
const lastDay = (month: string) => { const [y, m] = month.split('-').map(Number); return new Date(Date.UTC(y, m, 0)).toISOString().slice(0, 10) }
const shiftMonth = (month: string, by: number) => { const [y, m] = month.split('-').map(Number); return new Date(Date.UTC(y, m - 1 + by, 1)).toISOString().slice(0, 7) }
const when = (iso: string) => new Date(iso).toLocaleString('en-IN', { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })

/**
 * Cash in hand, a month at a time: the opening (the last galla count before it), every rupee in and out of the drawer
 * with the balance after it, each day's close against the count, and a form to move cash by hand or to and from the bank.
 */
export function CashLedgerScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const client = useQueryClient()
  const today = indiaToday()
  const [month, setMonth] = useState(monthOf(today))
  const to = month === monthOf(today) ? today : lastDay(month)
  const ledger = useQuery({ queryKey: [shopId, 'cash-ledger', month], queryFn: () => api<Ledger>(`/v1/cash-ledger?from=${month}-01&to=${to}`) })
  const accounts = useQuery({ queryKey: [shopId, 'bank-accounts'], queryFn: () => api<{ accounts: { id: string; name: string }[]; deposit: string }>('/v1/bank-book/accounts') }).data
  const [kind, setKind] = useState<Kind | null>(null)
  const [amount, setAmount] = useState('')
  const [note, setNote] = useState('')
  const [account, setAccount] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const data = ledger.data
  const label = new Date(`${month}-01T00:00:00Z`).toLocaleDateString('en-IN', { month: 'long', year: 'numeric', timeZone: 'UTC' })
  const bankMove = kind === 'to_bank' || kind === 'from_bank'
  const needsNote = kind === 'added' || kind === 'removed'
  const ready = !!kind && Number(amount) > 0 && (!needsNote || note.trim().length > 0)

  async function save() {
    if (!ready || !kind) return
    setBusy(true)
    setError(null)
    try {
      await api('/v1/cash-moves', 'POST', { id: uuidv7(), kind, amount: Number(amount), note: note.trim() || null, bankAccount: bankMove ? account || null : null })
      setKind(null); setAmount(''); setNote('')
      await client.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  async function strike(id: string) {
    await api(`/v1/cash-moves/${id}/strike`, 'POST', {})
    await client.invalidateQueries({ queryKey: [shopId] })
  }

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-3 p-4">
      <div className="flex flex-wrap gap-2 text-sm">
        <button onClick={() => navigate('/cashbook')} className="h-10 rounded-xl border border-line bg-white px-4 font-semibold">{t('bankBook.cashBook')}</button>
        <span className="flex h-10 items-center rounded-xl bg-ink px-4 font-semibold text-white">{t('cashLedger.title')}</span>
        <button onClick={() => navigate('/bankbook')} className="h-10 rounded-xl border border-line bg-white px-4 font-semibold">{t('bankBook.title')}</button>
      </div>
      <div className="flex items-center justify-between rounded-[18px] bg-white p-2">
        <button onClick={() => setMonth(shiftMonth(month, -1))} aria-label={t('book.previous')} className="h-11 w-11 rounded-xl text-xl">‹</button>
        <span className="text-lg font-bold">{label}</span>
        <button onClick={() => setMonth(shiftMonth(month, 1))} disabled={month >= monthOf(today)} aria-label={t('book.next')} className="h-11 w-11 rounded-xl text-xl disabled:opacity-30">›</button>
      </div>
      {ledger.isError && <p className="text-due">{ledger.error.message}</p>}
      {data && (
        <section className="grid grid-cols-2 gap-2 rounded-[20px] bg-white p-4 text-sm sm:grid-cols-4">
          <span className="flex flex-col"><span className="text-muted">{t('bankBook.opening')}</span><b className="text-lg">{formatRupees(data.opening)}</b></span>
          <span className="flex flex-col"><span className="text-muted">{t('bankBook.in')}</span><b className="text-lg text-jama">+{formatRupees(data.in)}</b></span>
          <span className="flex flex-col"><span className="text-muted">{t('bankBook.out')}</span><b className="text-lg text-udhaar">−{formatRupees(data.out)}</b></span>
          <span className="flex flex-col"><span className="text-muted">{t('cashLedger.inHand')}</span><b className="text-lg">{formatRupees(data.closing)}</b></span>
          <p className="col-span-2 text-xs text-muted sm:col-span-4">{data.openingCountedOn ? t('cashLedger.openingFrom', { date: data.openingCountedOn }) : t('cashLedger.noCount')}</p>
        </section>
      )}

      <section className="flex flex-col gap-2 rounded-[20px] bg-white p-4 text-sm">
        <b>{t('cashLedger.move')}</b>
        <div className="flex flex-wrap gap-2">
          {(['added', 'removed', 'to_bank', 'from_bank'] as const).map((k) => (
            <button key={k} onClick={() => setKind(kind === k ? null : k)} aria-pressed={kind === k}
              className={`min-h-11 rounded-xl px-3 font-semibold ${kind === k ? 'bg-ink text-white' : 'border border-line bg-white'}`}>{t(`cashLedger.kinds.${k}`)}</button>
          ))}
        </div>
        {kind && (
          <div className="flex flex-wrap items-end gap-2">
            <label className="flex flex-col gap-1">{t('expense.amount')}
              <input value={amount} onChange={(e) => setAmount(e.target.value.replace(/[^\d.]/g, ''))} inputMode="decimal" className="h-11 w-36 rounded-xl border border-line px-3" /></label>
            <label className="flex min-w-48 flex-1 flex-col gap-1">{needsNote ? t('cashLedger.why') : t('expense.note')}
              <input value={note} onChange={(e) => setNote(e.target.value.slice(0, 120))} className="h-11 rounded-xl border border-line px-3" /></label>
            {bankMove && (accounts?.accounts.length ?? 0) > 1 && (
              <label className="flex flex-col gap-1">{t('cashLedger.account')}
                <select value={account || accounts!.deposit} onChange={(e) => setAccount(e.target.value)} className="h-11 rounded-xl border border-line px-2">
                  {accounts!.accounts.map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
                </select></label>
            )}
            <button disabled={!ready || busy} onClick={() => void save()} className="h-11 rounded-xl bg-teal-700 px-5 font-semibold text-white disabled:opacity-40">{t('bankBook.save')}</button>
          </div>
        )}
        {error && <p className="text-due">{error}</p>}
      </section>

      {data && (data.lines.length === 0 ? <p className="text-muted">{t('cashLedger.empty')}</p> : (
        <ul className="flex flex-col divide-y divide-line rounded-[20px] bg-white">
          {data.lines.map((l, i) => (
            <li key={i} className="flex items-center gap-3 p-3 text-sm">
              <div className="flex flex-1 flex-col">
                <span className="font-semibold">{t(`book.sources.${l.source}`, { defaultValue: t(`cashLedger.sources.${l.source}`, { defaultValue: l.source }) })}{l.who ? ` · ${l.who}` : ''}</span>
                <span className="text-xs text-muted">{when(l.at)}</span>
              </div>
              <span className="flex flex-col items-end">
                <b className={l.amount < 0 ? 'text-udhaar' : 'text-jama'}>{l.amount < 0 ? '−' : '+'}{formatRupees(Math.abs(l.amount))}</b>
                <span className="text-xs text-muted">{formatRupees(l.balance)}</span>
              </span>
              {l.moveId && <button onClick={() => void strike(l.moveId!)} aria-label={t('expense.remove')} className="h-11 w-11 text-xl text-muted">×</button>}
            </li>
          ))}
        </ul>
      ))}

      {data && data.days.some((d) => d.counted !== null) && (
        <section className="overflow-x-auto rounded-xl border border-line bg-white">
          <table className="w-full min-w-[520px] border-collapse text-sm">
            <caption className="p-3 text-left font-bold">{t('cashLedger.closes')}</caption>
            <thead><tr className="bg-[#f3f6f5] text-right"><th className="border-b border-line px-3 py-2 text-left">{t('rv.day')}</th><th className="border-b border-line px-3 py-2">{t('bankBook.in')}</th><th className="border-b border-line px-3 py-2">{t('bankBook.out')}</th><th className="border-b border-line px-3 py-2">{t('cashLedger.inHand')}</th><th className="border-b border-line px-3 py-2">{t('cashLedger.diff')}</th></tr></thead>
            <tbody className="text-right tabular-nums">
              {data.days.filter((d) => d.in || d.out || d.counted !== null).map((d) => (
                <tr key={d.date}>
                  <td className="px-3 py-2 text-left">{new Date(d.date).toLocaleDateString('en-IN', { day: 'numeric', month: 'short' })}</td>
                  <td className="px-3 py-2">{formatRupees(d.in)}</td><td className="px-3 py-2">{formatRupees(d.out)}</td><td className="px-3 py-2">{formatRupees(d.closing)}</td>
                  <td className={`px-3 py-2 ${(d.difference ?? 0) < 0 ? 'text-orange-700' : ''}`}>{d.counted === null ? '–' : formatRupees(d.difference ?? 0)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </section>
      )}
    </div>
  )
}

import { btn } from '../lib/buttonStyles'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { confirmAsk } from '../lib/ask'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { shownError } from '../lib/notify'

type Into = 'cash' | 'bank' | 'direct'
type Mode = 'cash' | 'upi' | 'bank'
interface Loan {
  id: string; lender: string; amount: number; ratePercent: number; startedOn: string; emi: number | null; receivedInto: Into; note: string | null
  principalPaid: number; interestPaid: number; outstanding: number; payments: number; lastPaidAt: string | null; nextInterest: number
}
interface Payment { id: string; occurredAt: string; amount: number; principal: number; interest: number; mode: Mode; outstandingAfter: number }
interface Statement { loan: Loan; payments: Payment[] }

const day = (iso: string) => new Date(iso.length === 10 ? `${iso}T00:00:00` : iso).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })
const field = 'h-11 w-full rounded-xl border border-line bg-white px-3 text-base'

/** Pay one EMI: the interest part is filled in from what is still owed, and can be changed to match the bank's statement. */
function PayEmi({ loan, onDone }: { loan: Loan; onDone: () => void }) {
  const { t } = useTranslation()
  const [amount, setAmount] = useState(loan.emi ? String(loan.emi) : '')
  const [interest, setInterest] = useState(String(Math.min(loan.nextInterest, loan.emi ?? loan.nextInterest)))
  const [mode, setMode] = useState<Mode>('bank')
  const [message, setMessage] = useState('')
  const total = Number(amount) || 0
  const part = Math.min(Number(interest) || 0, total)
  async function pay() {
    setMessage('')
    try {
      await api(`/v1/loans/${loan.id}/payments`, 'POST', { id: crypto.randomUUID(), amount: total, interest: part, mode })
      onDone()
    } catch (e) {
      setMessage(shownError(e))
    }
  }
  return (
    <div className="flex flex-col gap-2 rounded-xl bg-[#f6faf9] p-3">
      <div className="grid grid-cols-2 gap-2">
        <label className="text-sm"><span className="mb-1 block font-semibold text-muted">{t('loans.emiPaid')}</span>
          <input value={amount} onChange={(e) => setAmount(e.target.value.replace(/[^\d.]/g, ''))} inputMode="decimal" className={field} /></label>
        <label className="text-sm"><span className="mb-1 block font-semibold text-muted">{t('loans.interestPart')}</span>
          <input value={interest} onChange={(e) => setInterest(e.target.value.replace(/[^\d.]/g, ''))} inputMode="decimal" className={field} /></label>
      </div>
      <div className="flex flex-wrap gap-2" role="radiogroup" aria-label={t('loans.paidBy')}>
        {(['bank', 'upi', 'cash'] as const).map((m) => (
          <button key={m} type="button" role="radio" aria-checked={mode === m} onClick={() => setMode(m)} className={`min-h-10 rounded-full px-3 text-sm font-semibold ${mode === m ? 'bg-ink text-white' : 'bg-white ring-1 ring-line'}`}>{t(`loans.modes.${m}`)}</button>
        ))}
      </div>
      {total > 0 && <p className="text-sm text-muted">{t('loans.split', { principal: formatRupees(Math.max(0, total - part)), interest: formatRupees(part) })}</p>}
      {message && <p className="text-sm text-due">{message}</p>}
      <button disabled={total <= 0} onClick={() => void pay()} className="min-h-12 rounded-xl bg-brand font-semibold text-white disabled:opacity-40">{t('loans.saveEmi')}</button>
    </div>
  )
}

function LoanCard({ shopId, loan }: { shopId: string; loan: Loan }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [paying, setPaying] = useState(false)
  const statement = useQuery({ queryKey: [shopId, 'loan', loan.id], queryFn: () => api<Statement>(`/v1/loans/${loan.id}`), enabled: open })
  const refresh = async () => { setPaying(false); await queryClient.invalidateQueries({ queryKey: [shopId, 'loans'] }); await queryClient.invalidateQueries({ queryKey: [shopId, 'loan', loan.id] }) }
  const paidShare = loan.amount > 0 ? Math.min(100, Math.round((loan.principalPaid / loan.amount) * 100)) : 0
  async function undo(p: Payment) {
    if (!(await confirmAsk(t('loans.undoConfirm', { amount: formatRupees(p.amount) })))) return
    await api(`/v1/loans/${loan.id}/payments/${p.id}/strike`, 'POST', {})
    await refresh()
  }
  async function remove() {
    if (!(await confirmAsk(t('loans.removeConfirm', { lender: loan.lender })))) return
    await api(`/v1/loans/${loan.id}/strike`, 'POST', {})
    await queryClient.invalidateQueries({ queryKey: [shopId, 'loans'] })
  }
  return (
    <section className="flex flex-col gap-3 rounded-card bg-white p-4 shadow-sm">
      <div className="flex items-start justify-between gap-2">
        <div>
          <h2 className="text-lg font-bold">🏦 {loan.lender}</h2>
          <p className="text-sm text-muted">{t('loans.taken', { amount: formatRupees(loan.amount), rate: loan.ratePercent, date: day(loan.startedOn) })}</p>
        </div>
        <div className="text-right">
          <p className={`text-xl font-extrabold ${loan.outstanding > 0 ? 'text-due' : 'text-jama'}`}>{formatRupees(loan.outstanding)}</p>
          <p className="text-xs text-muted">{loan.outstanding > 0 ? t('loans.stillOwed') : t('loans.repaid')}</p>
        </div>
      </div>
      <div className="h-2 overflow-hidden rounded-full bg-[#edf2f1]" aria-hidden><div className="h-full bg-jama" style={{ width: `${paidShare}%` }} /></div>
      <div className="grid grid-cols-3 gap-2 text-center text-sm">
        <span className="rounded-xl bg-[#f6faf9] p-2"><span className="block text-xs text-muted">{t('loans.principalPaid')}</span><b>{formatRupees(loan.principalPaid)}</b></span>
        <span className="rounded-xl bg-[#f6faf9] p-2"><span className="block text-xs text-muted">{t('loans.interestPaid')}</span><b>{formatRupees(loan.interestPaid)}</b></span>
        <span className="rounded-xl bg-[#f6faf9] p-2"><span className="block text-xs text-muted">{t('loans.emis')}</span><b>{loan.payments}</b></span>
      </div>
      {loan.outstanding > 0 && (paying ? <PayEmi loan={loan} onDone={() => void refresh()} />
        : <button onClick={() => setPaying(true)} className={`min-h-12 rounded-xl ${btn.solid}`}>+ {t('loans.payEmi')}{loan.emi ? ` · ${formatRupees(loan.emi)}` : ''}</button>)}
      <button onClick={() => setOpen(!open)} aria-expanded={open} className="min-h-10 self-start text-sm font-semibold text-brand underline">{open ? t('loans.hide') : t('loans.statement')}</button>
      {open && statement.data && (
        <div className="overflow-x-auto">
          <table className="w-full border-collapse text-sm">
            <thead className="bg-brand-soft text-left text-xs uppercase"><tr>
              <th className="px-2 py-2">{t('loans.date')}</th><th className="px-2 py-2 text-right">{t('loans.emiPaid')}</th><th className="px-2 py-2 text-right">{t('loans.principal')}</th>
              <th className="px-2 py-2 text-right">{t('loans.interest')}</th><th className="px-2 py-2 text-right">{t('loans.owedAfter')}</th><th /></tr></thead>
            <tbody className="divide-y divide-line">
              <tr><td className="px-2 py-2">{day(loan.startedOn)}</td><td colSpan={3} className="px-2 py-2 text-muted">{t(`loans.into.${loan.receivedInto}`)}</td><td className="px-2 py-2 text-right font-semibold">{formatRupees(loan.amount)}</td><td /></tr>
              {statement.data.payments.map((p) => (
                <tr key={p.id}>
                  <td className="whitespace-nowrap px-2 py-2">{day(p.occurredAt)} <span className="text-xs text-muted">{t(`loans.modes.${p.mode}`)}</span></td>
                  <td className="px-2 py-2 text-right">{formatRupees(p.amount)}</td><td className="px-2 py-2 text-right">{formatRupees(p.principal)}</td>
                  <td className="px-2 py-2 text-right">{formatRupees(p.interest)}</td><td className="px-2 py-2 text-right font-semibold">{formatRupees(p.outstandingAfter)}</td>
                  <td className="px-2 py-2 text-right"><button onClick={() => void undo(p)} className="min-h-9 text-xs text-muted underline">{t('remove')}</button></td>
                </tr>
              ))}
            </tbody>
          </table>
          {loan.payments === 0 && <button onClick={() => void remove()} className={`mt-2 min-h-10 text-sm ${btn.dangerOutline}`}>{t('loans.removeLoan')}</button>}
        </div>
      )}
    </section>
  )
}

/**
 * Business loans kept apart from the day's money: each loan, what is still owed, and its EMIs split into principal
 * (lowers the loan, not a cost) and interest (a cost against profit). The balance sheet shows what is still owed.
 */
export function LoansScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const loans = useQuery({ queryKey: [shopId, 'loans'], queryFn: () => api<Loan[]>('/v1/loans') }).data ?? []
  const [adding, setAdding] = useState(false)
  const [form, setForm] = useState({ lender: '', amount: '', rate: '', emi: '', startedOn: '', into: 'bank' as Into })
  const [message, setMessage] = useState('')
  const owed = loans.reduce((n, l) => n + l.outstanding, 0)
  async function add() {
    setMessage('')
    try {
      await api('/v1/loans', 'POST', { id: crypto.randomUUID(), lender: form.lender.trim(), amount: Number(form.amount), ratePercent: Number(form.rate) || 0, emi: Number(form.emi) || null, startedOn: form.startedOn || null, receivedInto: form.into })
      setForm({ lender: '', amount: '', rate: '', emi: '', startedOn: '', into: 'bank' })
      setAdding(false)
      await queryClient.invalidateQueries({ queryKey: [shopId, 'loans'] })
    } catch (e) {
      setMessage(shownError(e))
    }
  }
  const num = (k: 'amount' | 'rate' | 'emi') => (e: React.ChangeEvent<HTMLInputElement>) => setForm({ ...form, [k]: e.target.value.replace(/[^\d.]/g, '') })
  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex items-start justify-between gap-2">
        <div><h1 className="text-lg font-extrabold">{t('loans.title')}</h1><p className="text-sm text-muted">{t('loans.hint')}</p></div>
        {loans.length > 0 && <div className="text-right"><p className="text-xs text-muted">{t('loans.totalOwed')}</p><p className="text-xl font-extrabold text-due">{formatRupees(owed)}</p></div>}
      </div>
      {loans.map((l) => <LoanCard key={l.id} shopId={shopId} loan={l} />)}
      {loans.length === 0 && !adding && <p className="rounded-card bg-white p-4 text-sm text-muted">{t('loans.none')}</p>}
      {adding ? (
        <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
          <h2 className="font-bold">{t('loans.addTitle')}</h2>
          <label className="text-sm"><span className="mb-1 block font-semibold text-muted">{t('loans.lender')}</span>
            <input value={form.lender} onChange={(e) => setForm({ ...form, lender: e.target.value })} placeholder={t('loans.lenderPlaceholder')} maxLength={80} className={field} /></label>
          <div className="grid grid-cols-2 gap-2">
            <label className="text-sm"><span className="mb-1 block font-semibold text-muted">{t('loans.amount')}</span><input value={form.amount} onChange={num('amount')} inputMode="decimal" className={field} /></label>
            <label className="text-sm"><span className="mb-1 block font-semibold text-muted">{t('loans.rate')}</span><input value={form.rate} onChange={num('rate')} inputMode="decimal" placeholder="12" className={field} /></label>
            <label className="text-sm"><span className="mb-1 block font-semibold text-muted">{t('loans.emi')}</span><input value={form.emi} onChange={num('emi')} inputMode="decimal" className={field} /></label>
            <label className="text-sm"><span className="mb-1 block font-semibold text-muted">{t('loans.startedOn')}</span><input type="date" value={form.startedOn} onChange={(e) => setForm({ ...form, startedOn: e.target.value })} className={field} /></label>
          </div>
          <fieldset className="text-sm"><legend className="mb-1 font-semibold text-muted">{t('loans.whereWent')}</legend>
            <div className="flex flex-wrap gap-2">
              {(['bank', 'cash', 'direct'] as const).map((k) => (
                <button key={k} type="button" aria-pressed={form.into === k} onClick={() => setForm({ ...form, into: k })} className={`min-h-10 rounded-full px-3 font-semibold ${form.into === k ? 'bg-ink text-white' : 'bg-white ring-1 ring-line'}`}>{t(`loans.into.${k}`)}</button>
              ))}
            </div></fieldset>
          {form.into === 'direct' && <p className="text-xs text-muted">{t('loans.directHint')}</p>}
          {message && <p className="text-sm text-due">{message}</p>}
          <div className="flex gap-2">
            <button disabled={form.lender.trim().length < 2 || !(Number(form.amount) > 0)} onClick={() => void add()} className="min-h-12 flex-1 rounded-xl bg-brand font-semibold text-white disabled:opacity-40">{t('loans.add')}</button>
            <button onClick={() => setAdding(false)} className="min-h-12 rounded-xl border border-line px-4 font-semibold">{t('cancel')}</button>
          </div>
        </section>
      ) : <button onClick={() => setAdding(true)} className={`min-h-12 ${btn.add}`}>+ {t('loans.addTitle')}</button>}
      <p className="text-xs text-muted">{t('loans.howItCounts')}</p>
    </div>
  )
}

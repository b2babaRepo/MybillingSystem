import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ask, confirmAsk } from '../components/Ask'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'

interface Cheque {
  id: string; customerId: string; customer: string; amount: number; chequeNo: string | null; bank: string | null; chequeDate: string | null
  status: 'pending' | 'cleared' | 'bounced'; receivedAt: string; settledAt: string | null; bounceNote: string | null
}
interface ChequeList { cheques: Cheque[]; pendingAmount: number; pendingCount: number }

const day = (iso: string) => new Date(iso).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })

/**
 * Customers' cheques: those waiting on the bank first (oldest date first), with Cleared and Bounced for the owner; then
 * the last 90 days' settled ones. A bounce puts the amount back on the customer's khata with the bank's reason.
 */
export function ChequesScreen({ isOwner }: { isOwner: boolean }) {
  const { t } = useTranslation()
  const client = useQueryClient()
  const { data } = useQuery({ queryKey: ['cheques'], queryFn: () => api<ChequeList>('/v1/cheques') })
  const [busy, setBusy] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const today = new Date().toISOString().slice(0, 10)

  async function settle(c: Cheque, cleared: boolean) {
    let reason: string | undefined
    if (cleared) {
      if (!(await confirmAsk(t('cheques.clearConfirm', { no: c.chequeNo ?? '', amount: formatRupees(c.amount) })))) return
    } else {
      reason = (await ask(t('cheques.bounceReason')))?.trim()
      if (!reason || reason.length < 3) return
    }

    setBusy(c.id)
    setError(null)
    try {
      await api(`/v1/cheques/${c.id}/${cleared ? 'clear' : 'bounce'}`, 'POST', cleared ? {} : { reason })
      await Promise.all([client.invalidateQueries({ queryKey: ['cheques'] }), client.invalidateQueries()])
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(null)
    }
  }

  const pending = data?.cheques.filter((c) => c.status === 'pending') ?? []
  const done = data?.cheques.filter((c) => c.status !== 'pending') ?? []
  const row = (c: Cheque) => (
    <li key={c.id} className="flex flex-col gap-1 rounded-xl bg-white p-3">
      <div className="flex items-baseline justify-between gap-2">
        <button className="text-left font-semibold underline-offset-2 hover:underline" onClick={() => navigate(`/c/${c.customerId}`)}>{c.customer}</button>
        <b className="text-lg">{formatRupees(c.amount)}</b>
      </div>
      <p className="text-sm text-muted">
        #{c.chequeNo}{c.bank ? ` · ${c.bank}` : ''}{c.chequeDate ? ` · ${t('cheques.dated', { date: day(c.chequeDate) })}` : ''} · {t('cheques.received', { date: day(c.receivedAt) })}
        {c.status === 'pending' && c.chequeDate && c.chequeDate > today && <span className="ml-1 rounded bg-amber-100 px-1 text-warn">{t('cheques.postDated')}</span>}
      </p>
      {c.status === 'pending' && isOwner && (
        <div className="flex gap-2">
          <button disabled={busy === c.id} onClick={() => void settle(c, true)} className="h-10 flex-1 rounded-xl bg-jama px-3 text-sm font-semibold text-white disabled:opacity-50">{t('cheques.clear')}</button>
          <button disabled={busy === c.id} onClick={() => void settle(c, false)} className="h-10 flex-1 rounded-xl border border-line px-3 text-sm font-semibold text-udhaar disabled:opacity-50">{t('cheques.bounce')}</button>
        </div>
      )}
      {c.status !== 'pending' && (
        <p className={`text-sm font-semibold ${c.status === 'cleared' ? 'text-jama' : 'text-udhaar'}`}>
          {t(c.status === 'cleared' ? 'cheques.cleared' : 'cheques.bounced')}{c.settledAt ? ` · ${day(c.settledAt)}` : ''}{c.bounceNote ? ` · ${c.bounceNote}` : ''}
        </p>
      )}
    </li>
  )

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-3 p-4">
      <h1 className="text-xl font-bold">{t('cheques.title')}</h1>
      {error && <p className="rounded-xl bg-due-soft p-3 text-sm text-red-800">{error}</p>}
      <h2 className="font-semibold">{t('cheques.pending')}{data && data.pendingCount > 0 ? ` · ${formatRupees(data.pendingAmount)}` : ''}</h2>
      {data && pending.length === 0 && <p className="text-muted">{t('cheques.none')}</p>}
      <ul className="flex flex-col gap-2">{pending.map(row)}</ul>
      {done.length > 0 && (
        <>
          <h2 className="mt-2 font-semibold">{t('cheques.done')}</h2>
          <ul className="flex flex-col gap-2">{done.map(row)}</ul>
        </>
      )}
    </div>
  )
}

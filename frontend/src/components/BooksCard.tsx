import { btn } from '../lib/buttonStyles'
import { shownLocale } from '../i18n'
import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { confirmAsk } from '../lib/ask'

interface Books { closedUntil: string | null; history: { at: string; by: string | null; action: 'close' | 'reopen'; month: string; reason: string | null }[] }

const monthName = (ym: string) => new Date(`${ym}-01T00:00`).toLocaleDateString(shownLocale(), { month: 'long', year: 'numeric' })
const previousMonth = () => { const d = new Date(); d.setDate(1); d.setMonth(d.getMonth() - 1); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}` }

/**
 * Close the books: once a month is filed, close it so nothing dated in it can change. Reopening needs a reason, and
 * both are kept in the log below.
 */
export function BooksCard({ month }: { month: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const books = useQuery({ queryKey: ['books'], queryFn: () => api<Books>('/v1/books') }).data
  const [reason, setReason] = useState('')
  const [reopening, setReopening] = useState(false)
  const [message, setMessage] = useState('')
  if (!books) return null
  const closedMonth = books.closedUntil?.slice(0, 7) ?? null
  const isClosed = !!closedMonth && month <= closedMonth
  const canClose = month <= previousMonth()

  async function run(path: string, body: object) {
    setMessage('')
    try {
      const next = await api<Books>(path, 'POST', body)
      queryClient.setQueryData(['books'], next)
      setReopening(false)
      setReason('')
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  return (
    <section className={`flex flex-col gap-2 rounded-card p-4 shadow-sm print:hidden ${isClosed ? 'bg-slate-50' : 'bg-white'}`}>
      <h2 className="font-bold">🔒 {t('books.title')}</h2>
      <p className="text-sm">
        {isClosed ? t('books.closed', { month: monthName(month), until: monthName(closedMonth!) }) : closedMonth ? t('books.closedUpTo', { until: monthName(closedMonth) }) : t('books.none')}
      </p>
      <p className="text-xs text-muted">{t('books.hint')}</p>
      {!isClosed && canClose && (
        <button onClick={async () => { if (await confirmAsk(t('books.closeConfirm', { month: monthName(month) }))) void run('/v1/books/close', { month }) }} className="min-h-11 self-start rounded-xl bg-slate-800 px-4 font-semibold text-white">{t('books.close', { month: monthName(month) })}</button>
      )}
      {isClosed && !reopening && <button onClick={() => setReopening(true)} className={`min-h-11 self-start text-sm ${btn.danger}`}>{t('books.reopen', { month: monthName(month) })}</button>}
      {reopening && (
        <div className="flex flex-col gap-2">
          <input value={reason} onChange={(e) => setReason(e.target.value)} maxLength={200} placeholder={t('books.reason')} aria-label={t('books.reason')} className="h-11 rounded-xl border border-line bg-white px-3" />
          <div className="flex gap-2">
            <button disabled={reason.trim().length < 5} onClick={() => void run('/v1/books/reopen', { month, reason: reason.trim() })} className="min-h-11 rounded-xl bg-due px-4 font-semibold text-white disabled:opacity-50">{t('books.reopenSave')}</button>
            <button onClick={() => setReopening(false)} className="min-h-11 rounded-xl border border-line px-4 font-semibold">{t('cancel')}</button>
          </div>
        </div>
      )}
      {message && <p className="text-sm text-due">{message}</p>}
      {books.history.length > 0 && (
        <details className="text-xs">
          <summary className="cursor-pointer font-semibold text-muted">{t('books.log')}</summary>
          <ul className="mt-1 flex flex-col gap-1">
            {books.history.map((h, i) => (
              <li key={i} className="border-t border-line pt-1">
                {new Date(h.at).toLocaleString(shownLocale(), { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })} · {h.by ?? ''} · {t(`books.action.${h.action}`, { month: monthName(h.month) })}{h.reason ? ` · “${h.reason}”` : ''}
              </li>
            ))}
          </ul>
        </details>
      )}
    </section>
  )
}

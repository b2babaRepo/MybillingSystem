import { btn } from './ui'
import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'

/**
 * How many days this customer has to pay for what they take on credit (15, 30): each udhaar then has a due date, the dues
 * list shows what is overdue, and credit bills print "Due by". Owner sets it; everyone sees it.
 */
export function CreditDays({ shopId, customerId, days, isOwner }: { shopId: string; customerId: string; days: number | null | undefined; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<string | null>(null)
  const [error, setError] = useState('')
  if (!isOwner && !days) return null

  async function save(value: string) {
    setError('')
    try {
      await api(`/v1/customers/${customerId}/credit-days`, 'PUT', { creditDays: value ? Number(value) : null })
      setEditing(null)
      await queryClient.invalidateQueries({ queryKey: [shopId, 'customers'] })
      await queryClient.invalidateQueries({ queryKey: [shopId, 'dues'] })
    } catch (err) {
      setError(err instanceof Error ? err.message : t('error'))
    }
  }

  return (
    <section className="flex flex-col gap-2 rounded-card bg-white p-4">
      <div className="flex items-center justify-between gap-2">
        <span className="flex items-center gap-3">
          <span aria-hidden className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-50 text-lg">📅</span>
          <span className="flex flex-col">
            <span className="text-sm text-muted">{t('creditDays.label')}</span>
            <span className="text-lg font-bold">{days ? t('creditDays.days', { count: days }) : t('creditDays.none')}</span>
          </span>
        </span>
        {isOwner && editing === null && (
          <button onClick={() => setEditing(days ? String(days) : '')} className="min-h-10 rounded-full border border-brand/30 bg-brand-soft/60 px-4 text-sm font-semibold text-brand-strong">{t('creditDays.set')}</button>
        )}
      </div>
      {editing !== null && (
        <div className="flex flex-wrap gap-2">
          {[7, 15, 30, 45].map((n) => (
            <button key={n} onClick={() => setEditing(String(n))} aria-pressed={editing === String(n)}
              className={`min-h-11 rounded-xl border px-3 font-semibold ${editing === String(n) ? 'border-brand bg-brand-soft' : 'border-line'}`}>{t('creditDays.days', { count: n })}</button>
          ))}
          <label className="sr-only" htmlFor="credit-days">{t('creditDays.label')}</label>
          <input id="credit-days" value={editing} onChange={(e) => setEditing(e.target.value.replace(/\D/g, '').slice(0, 3))} inputMode="numeric"
            className="h-11 w-20 rounded-xl border border-line px-2 text-center font-bold" />
          <button onClick={() => void save(editing)} className={`min-h-11 px-4 rounded-xl ${btn.solid}`}>{t('trade.save')}</button>
          {days ? <button onClick={() => void save('')} className={`min-h-11 px-3 ${btn.dangerOutline}`}>{t('creditDays.clear')}</button> : null}
        </div>
      )}
      {editing !== null && <p className="text-xs text-muted">{t('creditDays.hint')}</p>}
      {error && <p className="text-sm text-due">{error}</p>}
    </section>
  )
}

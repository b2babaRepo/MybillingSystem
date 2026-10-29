import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import type { CashBook } from '../lib/types'
import { navigate } from '../router'

/** Today in India as yyyy-mm-dd (the shop day the server counts by). */
function indiaToday() {
  return new Date(Date.now() + 5.5 * 3600_000).toISOString().slice(0, 10)
}

function shiftDay(day: string, by: number) {
  const date = new Date(`${day}T00:00:00Z`)
  date.setUTCDate(date.getUTCDate() + by)
  return date.toISOString().slice(0, 10)
}

/** The day book: money received by cash and UPI, line by line, so closing the drawer is a check, not a guess. */
export function CashBookScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const today = indiaToday()
  const [day, setDay] = useState(today)
  const book = useQuery({ queryKey: [shopId, 'cash-book', day], queryFn: () => api<CashBook>(`/v1/cash-book?date=${day}`) })
  const data = book.data
  const label = new Date(`${day}T00:00:00Z`).toLocaleDateString('en-IN', { weekday: 'short', day: 'numeric', month: 'short', timeZone: 'UTC' })

  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex items-center justify-between rounded-[18px] bg-white p-2">
        <button onClick={() => setDay(shiftDay(day, -1))} aria-label={t('book.previous')} className="h-11 w-11 rounded-xl text-xl">‹</button>
        <span className="text-lg font-bold">{label}</span>
        <button onClick={() => setDay(shiftDay(day, 1))} disabled={day >= today} aria-label={t('book.next')} className="h-11 w-11 rounded-xl text-xl disabled:opacity-30">›</button>
      </div>

      {book.isError && <p className="text-due">{book.error.message}</p>}
      {data && (
        <>
          <section className="flex flex-col gap-3 rounded-[20px] bg-white p-[18px]">
            <span className="text-[15px] font-semibold text-muted">{t('book.received')}</span>
            <span className="text-[40px] leading-none font-extrabold">{formatRupees(data.total)}</span>
            <div className="grid grid-cols-2 gap-2">
              <div className="flex flex-col rounded-xl bg-emerald-50 p-2.5 text-emerald-800"><span className="text-xs">{t('cash')}</span><span className="text-[17px] font-bold">{formatRupees(data.cash)}</span></div>
              <div className="flex flex-col rounded-xl bg-blue-50 p-2.5 text-blue-800"><span className="text-xs">{t('upi')}</span><span className="text-[17px] font-bold">{formatRupees(data.upi)}</span></div>
            </div>
            {(data.spentCash ?? 0) + (data.spentUpi ?? 0) > 0 && (
              <button onClick={() => navigate('/expenses')} className="flex items-center justify-between rounded-xl bg-red-50 p-2.5 text-left text-red-800">
                <span className="text-sm">{t('book.spent', { cash: formatRupees(data.spentCash ?? 0), upi: formatRupees(data.spentUpi ?? 0) })}</span>
                <span className="font-bold">{t('book.netCash')} {formatRupees(data.netCash ?? data.cash)}</span>
              </button>
            )}
            <p className="text-sm text-muted">{t('book.drawerHint')}</p>
          </section>
          <GallaCount shopId={shopId} day={day} />
          <div className="grid grid-cols-2 gap-2">
            <div className="flex flex-col rounded-[18px] bg-white p-3"><span className="text-xs text-muted">{t('book.sales')}</span><span className="text-lg font-bold">{formatRupees(data.salesTotal)}</span></div>
            <div className="flex flex-col rounded-[18px] bg-white p-3"><span className="text-xs text-muted">{t('book.udhaarGiven')}</span><span className="text-lg font-bold text-udhaar">{formatRupees(data.udhaarGiven)}</span></div>
          </div>
          <ul className="divide-y divide-[#edf2f1] rounded-[18px] bg-white px-4">
            {data.lines.length === 0 && <li className="py-4 text-muted">{t('book.empty')}</li>}
            {data.lines.map((line, i) => (
              <li key={i}>
                <button
                  onClick={() => line.billId && navigate(`/print/bill/${line.billId}`)}
                  disabled={!line.billId}
                  className="flex min-h-14 w-full items-center justify-between gap-2 py-2.5 text-left"
                >
                  <span className="flex flex-col">
                    <span className="font-semibold">{line.who || t('book.walkIn')}</span>
                    <span className="text-xs text-muted">
                      {new Date(line.occurredAt).toLocaleTimeString('en-IN', { hour: 'numeric', minute: '2-digit' })} · {t(`book.sources.${line.source}`)} · {t(line.mode)}
                    </span>
                  </span>
                  <span className={`font-bold ${line.amount < 0 ? 'text-due' : 'text-jama'}`}>{line.amount < 0 ? '−' : '+'}{formatRupees(Math.abs(line.amount))}</span>
                </button>
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  )
}

interface CashCount {
  date: string
  opening: number
  openingFromCount: boolean
  cashIn: number
  cashOut: number
  expected: number
  counted: number | null
  difference: number | null
  note: string | null
  countedBy: string | null
  countedAt: string | null
}

/**
 * The evening galla count, like the rokad's last line: opening + cash in − cash spent = what should be in the box.
 * Count it, type it, and any short or extra is kept with a note. The next day opens with what was counted.
 */
function GallaCount({ shopId, day }: { shopId: string; day: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const key = [shopId, 'cash-count', day]
  const count = useQuery({ queryKey: key, queryFn: () => api<CashCount>(`/v1/cash-count?date=${day}`) })
  const [opening, setOpening] = useState<string | null>(null)
  const [counted, setCounted] = useState('')
  const [note, setNote] = useState('')
  const [editing, setEditing] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const c = count.data
  if (!c) return null
  const askOpening = !c.openingFromCount || opening !== null
  const openingValue = opening ?? String(c.opening)
  const expected = c.expected - c.opening + (Number(openingValue) || 0)
  const done = c.counted !== null && !editing

  async function save() {
    setBusy(true)
    setError(null)
    try {
      const result = await api<CashCount>('/v1/cash-count', 'POST', {
        date: day, opening: askOpening ? Number(openingValue) || 0 : undefined, counted: Number(counted), note: note.trim() || undefined,
      })
      queryClient.setQueryData(key, result)
      setEditing(false)
      setOpening(null)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  const row = (label: string, value: string, className = '') => (
    <div className={`flex justify-between ${className}`}><span className="text-muted">{label}</span><span className="font-bold">{value}</span></div>
  )
  const money = (value: string) => value.replace(/[^\d.]/g, '')

  return (
    <section className="flex flex-col gap-2 rounded-[20px] bg-white p-4 text-[15px]">
      <h2 className="text-lg font-bold">{t('galla.title')}</h2>
      {askOpening && !done ? (
        <label className="flex items-center justify-between gap-2">
          <span className="text-muted">{t('galla.opening')}</span>
          <input value={openingValue} onChange={(e) => setOpening(money(e.target.value))} inputMode="decimal"
            className="h-11 w-32 rounded-xl border border-line px-3 text-right font-bold" />
        </label>
      ) : (
        <div className="flex items-center justify-between">
          <span className="text-muted">{t('galla.opening')}</span>
          <span className="flex items-center gap-2">
            <span className="font-bold">{formatRupees(c.opening)}</span>
            {!done && <button onClick={() => setOpening(String(c.opening))} className="min-h-11 px-1 text-sm text-teal-700 underline">{t('galla.change')}</button>}
          </span>
        </div>
      )}
      {row(`+ ${t('galla.cashIn')}`, formatRupees(c.cashIn))}
      {row(`− ${t('galla.cashOut')}`, formatRupees(c.cashOut))}
      {row(`= ${t('galla.shouldBe')}`, formatRupees(done ? c.expected : expected), 'border-t border-line pt-2 text-base')}

      {done ? (
        <div className={`flex flex-col gap-1 rounded-xl p-3 ${c.difference === 0 ? 'bg-emerald-50 text-emerald-900' : 'bg-red-50 text-red-900'}`}>
          <span className="font-bold">
            {t('galla.counted', { amount: formatRupees(c.counted ?? 0) })} ·{' '}
            {c.difference === 0
              ? t('galla.matches')
              : (c.difference ?? 0) < 0
                ? t('galla.short', { amount: formatRupees(-(c.difference ?? 0)) })
                : t('galla.extra', { amount: formatRupees(c.difference ?? 0) })}
          </span>
          {c.note && <span className="text-sm">{c.note}</span>}
          <span className="text-xs opacity-80">{c.countedBy} · {c.countedAt && new Date(c.countedAt).toLocaleTimeString('en-IN', { hour: 'numeric', minute: '2-digit' })}</span>
          <button onClick={() => { setEditing(true); setCounted(String(c.counted ?? '')) }} className="min-h-11 self-start text-sm underline">{t('galla.recount')}</button>
        </div>
      ) : (
        <>
          <label className="flex items-center justify-between gap-2">
            <span className="font-semibold">{t('galla.countedLabel')}</span>
            <input value={counted} onChange={(e) => setCounted(money(e.target.value))} inputMode="decimal" placeholder="₹"
              className="h-12 w-36 rounded-xl border-2 border-teal-700 px-3 text-right text-lg font-bold" />
          </label>
          {counted !== '' && Number(counted) !== expected && (
            <input value={note} onChange={(e) => setNote(e.target.value)} maxLength={200} placeholder={t('galla.notePlaceholder')} aria-label={t('galla.note')}
              className="h-11 rounded-xl border border-line px-3" />
          )}
          {error && <p className="text-sm text-due">{error}</p>}
          <button disabled={busy || counted === ''} onClick={() => void save()} className="h-12 rounded-2xl bg-teal-700 font-bold text-white disabled:opacity-40">
            {t('galla.save')}
          </button>
        </>
      )}
    </section>
  )
}

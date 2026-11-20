import { btn } from '../components/ui'
import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import type { CashBook } from '../lib/types'
import { navigate } from '../router'

const icon = (d: string, size = 18) => <svg aria-hidden="true" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d={d} /></svg>
const CASH = 'M3 6h18v12H3zM12 9.5a2.5 2.5 0 100 5 2.5 2.5 0 000-5'
const UPI = 'M7 2h10v20H7zM11 18h2'

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
export function CashBookScreen({ shopId, isOwner = false }: { shopId: string; isOwner?: boolean }) {
  const { t, i18n } = useTranslation()
  const today = indiaToday()
  const [day, setDay] = useState(today)
  const book = useQuery({ queryKey: [shopId, 'cash-book', day], queryFn: () => api<CashBook>(`/v1/cash-book?date=${day}`) })
  const data = book.data
  // The day in the app's language: "शुक्र, 13 नव॰" in Hindi.
  const label = new Date(`${day}T00:00:00Z`).toLocaleDateString(i18n.language === 'hi' ? 'hi-IN' : i18n.language === 'gu' ? 'gu-IN' : 'en-IN', { weekday: 'short', day: 'numeric', month: 'short', timeZone: 'UTC' })

  return (
    <div className="flex flex-col gap-3 p-4">
      {isOwner && (
        <div className="flex w-fit gap-1 rounded-2xl bg-[#edf2f1] p-1 text-sm">
          <span className="flex h-10 items-center rounded-xl bg-brand px-4 font-semibold text-white shadow-md shadow-teal-900/20">{t('bankBook.cashBook')}</span>
          <button onClick={() => navigate('/cashledger')} className="h-10 rounded-xl px-4 font-semibold text-muted transition hover:text-ink">{t('cashLedger.title')}</button>
          <button onClick={() => navigate('/bankbook')} className="h-10 rounded-xl px-4 font-semibold text-muted transition hover:text-ink">{t('bankBook.title')}</button>
        </div>
      )}
      <div className="flex items-center justify-between gap-2 rounded-card bg-white p-2 shadow-sm">
        <button onClick={() => setDay(shiftDay(day, -1))} aria-label={t('book.previous')} className="flex h-11 w-11 items-center justify-center rounded-xl hover:bg-[#f1f5f4]">{icon('M15 18l-6-6 6-6')}</button>
        <span className="flex items-center gap-2 text-lg font-bold"><span className="text-brand">{icon('M4 5h16v15H4zM4 9h16M8 3v4M16 3v4')}</span>{label}
          {day !== today && <button onClick={() => setDay(today)} className={`min-h-8 rounded-full px-3 text-xs ${btn.outline}`}>{t('cash3.today')}</button>}
        </span>
        <button onClick={() => setDay(shiftDay(day, 1))} disabled={day >= today} aria-label={t('book.next')} className="flex h-11 w-11 items-center justify-center rounded-xl hover:bg-[#f1f5f4] disabled:opacity-30">{icon('M9 6l6 6-6 6')}</button>
      </div>

      {/* The day's other money jobs, each in its own colour with a line icon on a white dot. */}
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        {[
          { to: '/expenses', label: t('expense.link'), d: 'M6 2h12v20l-3-2-3 2-3-2-3 2zM9 7h6M9 11h6M9 15h4', tone: 'bg-[#fde8ec] text-[#be123c] hover:bg-[#fbd5dd]' },
          { to: '/receipts', label: t('receipts.link'), d: CASH, tone: 'bg-[#e3f2ee] text-[#0b5e4f] hover:bg-[#d2ebe4]' },
          { to: '/reports/day', label: t('menu.dayBook'), d: 'M4 5h16v15H4zM4 9h16M8 13h8M8 16h5', tone: 'bg-[#e8effd] text-[#1d4ed8] hover:bg-[#dbe5fc]' },
          { to: '/cheques', label: t('cheques.link'), d: 'M3 7h18v10H3zM6 12h6M15 13l2-2 2 2', tone: 'bg-[#f1eafd] text-[#6d28d9] hover:bg-[#e7dcfb]' },
        ].map((x) => (
          <button key={x.to} onClick={() => navigate(x.to)} className={`flex min-h-[52px] items-center gap-2.5 rounded-2xl px-3 text-left text-sm font-bold transition active:scale-[0.98] ${x.tone}`}>
            <span aria-hidden className="flex h-[30px] w-[30px] shrink-0 items-center justify-center rounded-full bg-white">{icon(x.d)}</span>
            <span className="leading-tight">{x.label}</span>
          </button>
        ))}
      </div>

      {book.isError && <p className="text-due">{book.error.message}</p>}
      {data && (
        <div className="flex flex-col gap-3 lg:grid lg:grid-cols-2 lg:items-start lg:[&>ul]:col-start-2 lg:[&>ul]:row-span-3 lg:[&>ul]:row-start-1">
          <section className="flex flex-col gap-3 rounded-[20px] bg-white p-[18px] shadow-sm">
            <span className="text-[15px] font-semibold text-muted">{t('book.received')}</span>
            <span className="text-[40px] leading-none font-extrabold">{formatRupees(data.total)}</span>
            <div className="grid grid-cols-2 gap-2">
              <div className="flex items-center gap-2.5 rounded-xl bg-emerald-50 p-2.5 text-emerald-800"><span className="flex h-8 w-8 items-center justify-center rounded-full bg-white">{icon(CASH, 16)}</span><span className="flex flex-col"><span className="text-xs">{t('cash')}</span><span className="text-[17px] font-bold">{formatRupees(data.cash)}</span></span></div>
              <div className="flex items-center gap-2.5 rounded-xl bg-blue-50 p-2.5 text-blue-800"><span className="flex h-8 w-8 items-center justify-center rounded-full bg-white">{icon(UPI, 16)}</span><span className="flex flex-col"><span className="text-xs">{t('upi')}</span><span className="text-[17px] font-bold">{formatRupees(data.upi)}</span></span></div>
            </div>
            {(data.spentCash ?? 0) + (data.spentUpi ?? 0) > 0 && (
              <button onClick={() => navigate('/expenses')} className="flex items-center justify-between rounded-xl bg-due-soft p-2.5 text-left text-red-800">
                <span className="text-sm">{t('book.spent', { cash: formatRupees(data.spentCash ?? 0), upi: formatRupees(data.spentUpi ?? 0) })}</span>
                <span className="font-bold">{t('book.netCash')} {formatRupees(data.netCash ?? data.cash)}</span>
              </button>
            )}
            <p className="text-sm text-muted">{t('book.drawerHint')}</p>
          </section>
          <GallaCount shopId={shopId} day={day} />
          <div className="grid grid-cols-2 gap-2">
            <div className="flex min-h-[72px] flex-col justify-between rounded-card bg-white p-3 shadow-sm ring-1 ring-black/5"><span className="text-xs font-semibold text-muted">{t('book.sales')}</span><b className="text-xl">{formatRupees(data.salesTotal)}</b></div>
            <div className="flex min-h-[72px] flex-col justify-between rounded-card bg-warn-soft p-3 shadow-sm ring-1 ring-black/5"><span className="text-xs font-semibold text-warn-ink">{t('book.udhaarGiven')}</span><b className="text-xl text-udhaar">{formatRupees(data.udhaarGiven)}</b></div>
          </div>
          {data.lines.length > 0 && <p className="px-4 text-right text-xs font-semibold text-muted">{t('heads.cashRow')}</p>}
          <ul className="divide-y divide-[#edf2f1] rounded-card bg-white px-4 shadow-sm">
            {data.lines.length === 0 && <li className="flex items-center gap-3 py-5 text-muted"><span className="flex h-10 w-10 items-center justify-center rounded-full bg-[#edf2f1]">{icon(CASH)}</span>{t('book.empty')}</li>}
            {data.lines.map((line, i) => (
              <li key={i}>
                <button
                  onClick={() => line.billId && navigate(`/print/bill/${line.billId}`)}
                  disabled={!line.billId}
                  className="flex min-h-14 w-full items-center gap-3 py-2.5 text-left"
                >
                  <span aria-hidden className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full ${line.amount < 0 ? 'bg-due-soft text-due' : line.mode === 'upi' ? 'bg-blue-50 text-blue-700' : 'bg-emerald-50 text-emerald-700'}`}>{icon(line.mode === 'upi' ? UPI : CASH)}</span>
                  <span className="flex min-w-0 flex-1 flex-col">
                    <span className="font-semibold">{line.who || t('book.walkIn')}</span>
                    <span className="text-xs text-muted">
                      {new Date(line.occurredAt).toLocaleTimeString(shownLocale(), { hour: 'numeric', minute: '2-digit' })} · {t(`book.sources.${line.source}`)} · {t(line.mode)}
                      {line.chequePending && <span className="font-semibold text-amber-700"> · {t('book.chequePending')}</span>}
                    </span>
                  </span>
                  <span className={`font-bold ${line.amount < 0 ? 'text-due' : 'text-jama'}`}>{line.amount < 0 ? '−' : '+'}{formatRupees(Math.abs(line.amount))}</span>
                </button>
              </li>
            ))}
          </ul>
        </div>
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
  history?: { at: string; by: string | null; expected: number; counted: number; difference: number; note: string | null }[]
  expectedAtCount?: number | null
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
    <section className="flex flex-col gap-2 rounded-[20px] bg-white p-4 text-[15px] shadow-sm">
      <h2 className="flex items-center gap-2 text-lg font-bold"><span aria-hidden className="flex h-9 w-9 items-center justify-center rounded-full bg-warn-soft text-warn">{icon('M3 7h18v13H3zM3 7l3-4h12l3 4M9 12h6')}</span>{t('galla.title')}</h2>
      {askOpening && !done ? (
        <label className="flex items-center justify-between gap-2">
          <span className="text-muted">{t('galla.opening')}</span>
          <input value={openingValue} onChange={(e) => setOpening(money(e.target.value))} inputMode="decimal" aria-label={t('galla.opening')}
            className="h-11 w-32 rounded-xl border border-line px-3 text-right font-bold" />
        </label>
      ) : (
        <div className="flex items-center justify-between">
          <span className="text-muted">{t('galla.opening')}</span>
          <span className="flex items-center gap-2">
            <span className="font-bold">{formatRupees(c.opening)}</span>
            {!done && <button onClick={() => setOpening(String(c.opening))} className={`min-h-11 px-1 text-sm ${btn.link}`}>{t('galla.change')}</button>}
          </span>
        </div>
      )}
      {row(`+ ${t('galla.cashIn')}`, formatRupees(c.cashIn))}
      {row(`− ${t('galla.cashOut')}`, formatRupees(c.cashOut))}
      {row(`= ${t('galla.shouldBe')}`, formatRupees(done ? c.expectedAtCount ?? c.expected : expected), 'border-t border-line pt-2 text-base')}
      {done && c.expectedAtCount != null && c.expectedAtCount !== c.expected && <p className="text-xs text-warn">{t('galla.changedSince', { amount: formatRupees(c.expected) })}</p>}

      {done ? (
        <div className={`flex flex-col gap-1 rounded-xl p-3 ${c.difference === 0 ? 'bg-emerald-50 text-emerald-900' : 'bg-due-soft text-red-900'}`}>
          <span className="font-bold">
            {t('galla.counted', { amount: formatRupees(c.counted ?? 0) })} ·{' '}
            {c.difference === 0
              ? t('galla.matches')
              : (c.difference ?? 0) < 0
                ? t('galla.short', { amount: formatRupees(-(c.difference ?? 0)) })
                : t('galla.extra', { amount: formatRupees(c.difference ?? 0) })}
          </span>
          {c.note && <span className="text-sm">{c.note}</span>}
          <span className="text-xs opacity-80">{c.countedBy} · {c.countedAt && new Date(c.countedAt).toLocaleTimeString(shownLocale(), { hour: 'numeric', minute: '2-digit' })}</span>
          <button onClick={() => { setEditing(true); setCounted(''); setNote('') }} className={`min-h-11 self-start text-sm ${btn.link}`}>{t('galla.recount')}</button>
          {(c.history?.length ?? 0) > 1 && (
            <details className="text-xs">
              <summary className="cursor-pointer font-semibold">{t('galla.history', { count: c.history!.length })}</summary>
              <ul className="mt-1 flex flex-col gap-1">
                {c.history!.map((h, i) => (
                  <li key={i}>{new Date(h.at).toLocaleTimeString(shownLocale(), { hour: 'numeric', minute: '2-digit' })} · {h.by ?? ''} · {formatRupees(h.counted)} ({h.difference === 0 ? t('galla.matches') : h.difference < 0 ? t('galla.short', { amount: formatRupees(-h.difference) }) : t('galla.extra', { amount: formatRupees(h.difference) })}){h.note ? ` · ${h.note}` : ''}</li>
                ))}
              </ul>
            </details>
          )}
        </div>
      ) : (
        <>
          <label className="flex items-center justify-between gap-2">
            <span className="font-semibold">{t('galla.countedLabel')}</span>
            <input value={counted} onChange={(e) => setCounted(money(e.target.value))} inputMode="decimal" placeholder="₹" aria-label={t('galla.countedLabel')}
              className="h-12 w-36 rounded-xl border-2 border-brand px-3 text-right text-lg font-bold" />
          </label>
          {counted !== '' && Number(counted) !== expected && (
            <input value={note} onChange={(e) => setNote(e.target.value)} maxLength={200} placeholder={t('galla.notePlaceholder')} aria-label={t('galla.note')}
              className="h-11 rounded-xl border border-line px-3 bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
          )}
          {error && <p className="text-sm text-due">{error}</p>}
          <button disabled={busy || counted === ''} onClick={() => void save()} className={`h-12 rounded-2xl ${btn.solid}`}>
            {t('galla.save')}
          </button>
        </>
      )}
    </section>
  )
}

import { useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { shownLocale } from '../i18n'
import { Input } from '../components/ui'
import { api } from '../lib/api'
import { formatRupees, whatsAppLink } from '../lib/format'
import { navigate } from '../router'

type Status = 'paid' | 'partial' | 'due' | 'cancelled'
interface FoundBill { id: string; billNo: string; occurredAt: string; total: number; customerName: string | null; customerPhone: string | null; lines: number; items: string; cancelled: boolean; due: number; status: Status }
interface BillList { total: number; received: number; due: number; count: number; paidCount: number; partialCount: number; dueCount: number; cancelledCount: number; bills: FoundBill[] }

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
type Period = 'today' | 'month' | 'last3' | 'custom'

/** The dates a period chip stands for, in the phone's own calendar. */
function rangeOf(period: Period, from: string, to: string): [string, string] {
  const now = new Date()
  if (period === 'today') return [iso(now), iso(now)]
  if (period === 'month') return [iso(new Date(now.getFullYear(), now.getMonth(), 1)), iso(now)]
  if (period === 'last3') return [iso(new Date(now.getFullYear(), now.getMonth() - 2, 1)), iso(now)]
  return [from, to]
}

const tone: Record<Status, string> = {
  paid: 'bg-green-50 text-green-800', partial: 'bg-orange-50 text-orange-800', due: 'bg-due-soft text-due', cancelled: 'bg-[#edf2f1] text-muted',
}

/** One CSV cell: quoted when it holds a comma, quote or new line. */
const cell = (v: string | number) => (/[",\n]/.test(String(v)) ? `"${String(v).replace(/"/g, '""')}"` : String(v))

/**
 * Sale bills: every bill of a period with what came in and what is still due, like a register. Received + Due = Total
 * at the top; each bill says Paid, Partial or Due as of today (khata payments clear the oldest bills first); search by
 * number, name, mobile or item; open a bill to print, share, return, exchange or cancel; download the list for Excel.
 */
export function FindBillScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const [typed, setTyped] = useState('')
  const [q, setQ] = useState('')
  const [period, setPeriod] = useState<Period>('month')
  const [from, setFrom] = useState(() => iso(new Date(Date.now() - 30 * 86_400_000)))
  const [to, setTo] = useState(() => iso(new Date()))
  const [status, setStatus] = useState<'all' | Status>('all')
  // Search a moment after typing stops.
  useEffect(() => { const h = setTimeout(() => setQ(typed.trim()), 300); return () => clearTimeout(h) }, [typed])
  const [start, end] = rangeOf(period, from, to)
  const params = new URLSearchParams({ ...(q ? { q } : {}), from: start, to: end, status })
  const list = useQuery({ queryKey: [shopId, 'bills', 'list', q, start, end, status], queryFn: () => api<BillList>(`/v1/bills/list?${params}`) })
  const data = list.data
  const when = (s: string) => new Date(s).toLocaleString(shownLocale(), { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })

  function download() {
    if (!data) return
    const head = [t('billList.colDate'), t('billList.colNo'), t('billList.colParty'), t('billList.colItems'), t('billList.colTotal'), t('billList.colDue'), t('billList.colStatus')]
    const rows = data.bills.map((b) => [new Date(b.occurredAt).toLocaleString(shownLocale()), b.billNo, b.customerName ?? t('findBill.walkIn'), b.items, b.total, b.due, t(`billList.status.${b.status}`)])
    // The BOM makes Excel read Hindi and ₹ correctly.
    const csv = '﻿' + [head, ...rows].map((r) => r.map(cell).join(',')).join('\n')
    const a = document.createElement('a')
    a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = `bills-${start}-to-${end}.csv`
    a.click()
    setTimeout(() => URL.revokeObjectURL(a.href), 10_000)
  }

  const chip = (on: boolean) => `min-h-10 rounded-full px-3 text-sm font-semibold ${on ? 'bg-ink text-white' : 'bg-white text-ink ring-1 ring-line'}`

  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex items-center justify-between gap-2">
        <h1 className="text-lg font-extrabold">{t('billList.title')}</h1>
        <button onClick={() => navigate('/exchange-slips')} className="min-h-10 text-sm font-semibold text-brand underline">🎟 {t('slip.register')} ›</button>
      </div>

      {/* Which bills: a period, then a search. */}
      <div className="flex flex-wrap gap-2" role="group" aria-label={t('billList.period')}>
        {(['today', 'month', 'last3', 'custom'] as const).map((p) => (
          <button key={p} onClick={() => setPeriod(p)} aria-pressed={period === p} className={chip(period === p)}>{t(`billList.periods.${p}`)}</button>
        ))}
      </div>
      {period === 'custom' && (
        <div className="grid grid-cols-2 gap-2">
          <label className="block text-sm"><span className="mb-1 block font-semibold text-muted">{t('findBill.from')}</span>
            <Input type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)} className="w-full" /></label>
          <label className="block text-sm"><span className="mb-1 block font-semibold text-muted">{t('findBill.to')}</span>
            <Input type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} className="w-full" /></label>
        </div>
      )}
      <Input value={typed} onChange={(e) => setTyped(e.target.value)} placeholder={t('findBill.search')} aria-label={t('findBill.search')} className="w-full text-base" />

      {/* Received + Due = Total, the first thing a shop wants to know about its bills. */}
      {data && (
        <div className="grid grid-cols-[1fr_auto_1fr_auto_1fr] items-center gap-1.5">
          <div className="rounded-card bg-green-50 p-3 text-green-900"><span className="block text-xs font-semibold">{t('billList.received')}</span><span className="text-lg font-extrabold">{formatRupees(data.received)}</span></div>
          <span aria-hidden className="text-lg font-bold text-muted">+</span>
          <div className="rounded-card bg-due-soft p-3 text-red-900"><span className="block text-xs font-semibold">{t('billList.due')}</span><span className="text-lg font-extrabold">{formatRupees(data.due)}</span></div>
          <span aria-hidden className="text-lg font-bold text-muted">=</span>
          <div className="rounded-card bg-white p-3 shadow-sm"><span className="block text-xs font-semibold text-muted">{t('billList.total', { count: data.count })}</span><span className="text-lg font-extrabold">{formatRupees(data.total)}</span></div>
        </div>
      )}

      {data && (
        <div className="flex flex-wrap items-center gap-2" role="group" aria-label={t('billList.colStatus')}>
          {([['all', data.count + data.cancelledCount], ['paid', data.paidCount], ['partial', data.partialCount], ['due', data.dueCount], ['cancelled', data.cancelledCount]] as const)
            .filter(([s, n]) => s === 'all' || n > 0).map(([s, n]) => (
              <button key={s} onClick={() => setStatus(s)} aria-pressed={status === s} className={chip(status === s)}>{t(`billList.status.${s}`)} · {n}</button>
            ))}
          <button onClick={download} disabled={!data.bills.length} className="ml-auto min-h-10 rounded-full px-3 text-sm font-semibold text-brand underline disabled:opacity-40">⬇ {t('billList.excel')}</button>
        </div>
      )}

      {list.isError && <p className="text-sm text-due">{list.error.message}</p>}
      {data && data.bills.length === 0 && <p className="py-6 text-center text-muted">{t('findBill.none')}</p>}
      {data && data.bills.length > 0 && (
        <div className="divide-y divide-line rounded-card bg-white shadow-sm">
          {data.bills.map((b) => (
            <div key={b.id} className="flex items-center gap-2 px-3 py-2.5">
              <button onClick={() => navigate(`/print/bill/${b.id}`)} className="flex min-w-0 flex-1 flex-col gap-0.5 text-left">
                <span className="flex flex-wrap items-center gap-2">
                  <b>{b.billNo}</b>
                  <span className={`rounded-full px-2 text-xs font-bold ${tone[b.status]}`}>{t(`billList.status.${b.status}`)}</span>
                </span>
                <span className="truncate text-sm">{b.customerName ?? t('findBill.walkIn')}</span>
                <span className="truncate text-xs text-muted">{when(b.occurredAt)} · {b.items}</span>
              </button>
              <span className="flex shrink-0 flex-col items-end">
                <b className={b.cancelled ? 'text-muted line-through' : ''}>{formatRupees(b.total)}</b>
                {b.due > 0 && <span className="text-xs font-semibold text-due">{t('billList.dueAmount', { amount: formatRupees(b.due) })}</span>}
              </span>
              <span className="flex shrink-0 gap-1">
                <button onClick={() => { try { sessionStorage.setItem('gk-autoprint', '1') } catch { /* no storage */ } navigate(`/print/bill/${b.id}`) }}
                  aria-label={`${t('billList.print')} ${b.billNo}`} className="flex h-10 w-10 items-center justify-center rounded-lg border border-line">🖨️</button>
                {b.customerPhone && (
                  <a href={whatsAppLink(b.customerPhone, t('billList.shareText', { no: b.billNo, total: formatRupees(b.total), due: formatRupees(b.due) }))} target="_blank" rel="noreferrer"
                    aria-label={`${t('billList.share')} ${b.billNo}`} className="flex h-10 w-10 items-center justify-center rounded-lg border border-line">💬</a>
                )}
              </span>
            </div>
          ))}
        </div>
      )}
      {data && data.bills.length === 200 && <p className="px-1 text-xs text-muted">{t('billList.more')}</p>}
    </div>
  )
}

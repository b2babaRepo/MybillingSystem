import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatDateTime, formatRupees } from '../lib/format'
import { navigate } from '../router'
import { PendingDot } from './ui'
import { dueByLine, type Line } from '../lib/khataTable'

type Status = 'paid' | 'partial' | 'due'

const tone: Record<Status, string> = { paid: 'bg-green-50 text-green-800', partial: 'bg-orange-50 text-orange-800', due: 'bg-due-soft text-due' }

/** One CSV cell: quoted when it holds a comma, quote or new line. */
const cell = (v: string | number) => (/[",\n]/.test(String(v)) ? `"${String(v).replace(/"/g, '""')}"` : String(v))

/**
 * The khata as a table on a computer, like an accounts register: Type · Bill no. · Date · Mode · Udhaar · Jama ·
 * Balance · Status, a filter on the type, newest first, and a download for Excel. Phones keep the card list.
 */
export function KhataTable({ entries, customerName, canRemove, onRemove }: {
  entries: Line[]
  customerName: string
  canRemove?: (entry: Line) => boolean
  onRemove?: (entry: Line) => void
}) {
  const { t } = useTranslation()
  const [type, setType] = useState<'all' | 'udhaar' | 'jama' | 'other'>('all')
  const due = useMemo(() => dueByLine(entries), [entries])
  const kindOf = (e: Line) => (e.kind === 'reversal' || e.kind === 'return' ? 'other' : e.amount > 0 ? 'udhaar' : 'jama')
  const label = (e: Line) => e.kind === 'reversal' ? t('reversal') : e.kind === 'return' ? t('dispute.returned') : e.amount > 0 ? (e.billId ? t('khataTable.sale') : t('creditShort')) : t('paymentShort')
  const statusOf = (e: Line): Status | null => {
    const left = due.get(e.id)
    if (left == null) return null
    return left <= 0 ? 'paid' : left < e.amount ? 'partial' : 'due'
  }
  const rows = [...entries].reverse().filter((e) => type === 'all' || kindOf(e) === type)

  function download() {
    const head = [t('khataTable.type'), t('khataTable.number'), t('khataTable.date'), t('khataTable.mode'), t('khataTable.udhaar'), t('khataTable.jama'), t('khataTable.balance'), t('khataTable.status'), t('khataTable.note')]
    const body = [...entries].map((e) => {
      const s = statusOf(e)
      return [label(e), e.bill?.billNo ?? '', new Date(e.occurredAt).toLocaleString(), e.paymentMode ? t(e.paymentMode) : '', e.amount > 0 ? e.amount : '', e.amount < 0 ? -e.amount : '', e.runningBalance, s ? t(`billList.status.${s}`) : '', e.note ?? '']
    })
    const csv = '﻿' + [head, ...body].map((r) => r.map(cell).join(',')).join('\n')
    const a = document.createElement('a')
    a.href = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }))
    a.download = `khata-${customerName.replace(/[^\p{L}\p{N}]+/gu, '-')}.csv`
    a.click()
    setTimeout(() => URL.revokeObjectURL(a.href), 10_000)
  }

  const chip = (on: boolean) => `min-h-9 rounded-full px-3 text-sm font-semibold ${on ? 'bg-ink text-white' : 'bg-white text-ink ring-1 ring-line'}`
  return (
    <div className="flex flex-col gap-2 rounded-card bg-white p-3 shadow-sm">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="mr-2 text-base font-bold">{t('khataTable.title')}</h2>
        {(['all', 'udhaar', 'jama', 'other'] as const).map((k) => (
          <button key={k} onClick={() => setType(k)} aria-pressed={type === k} className={chip(type === k)}>{t(`khataTable.filter.${k}`)}</button>
        ))}
        <button onClick={download} disabled={entries.length === 0} className="ml-auto min-h-9 text-sm font-semibold text-brand underline disabled:opacity-40">⬇ {t('billList.excel')}</button>
      </div>
      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-sm">
          <thead className="bg-brand-soft text-left text-xs uppercase tracking-wide text-ink">
            <tr>
              <th className="px-3 py-2">{t('khataTable.type')}</th>
              <th className="px-2 py-2">{t('khataTable.number')}</th>
              <th className="px-2 py-2">{t('khataTable.date')}</th>
              <th className="px-2 py-2 text-right">{t('khataTable.udhaar')}</th>
              <th className="px-2 py-2 text-right">{t('khataTable.jama')}</th>
              <th className="px-2 py-2 text-right">{t('khataTable.balance')}</th>
              <th className="px-2 py-2">{t('khataTable.status')}</th>
              <th className="px-3 py-2" />
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {rows.length === 0 && <tr><td colSpan={8} className="p-4 text-center text-muted">{t('noEntries')}</td></tr>}
            {rows.map((e) => {
              const s = statusOf(e)
              const left = due.get(e.id) ?? 0
              return (
                <tr key={e.id} onClick={e.billId ? () => navigate(`/print/bill/${e.billId}`) : undefined}
                  className={`${e.billId ? 'cursor-pointer hover:bg-[#f6faf9]' : ''} ${e.isReversed ? 'text-muted line-through opacity-60' : ''}`}>
                  <td className="px-3 py-2 font-semibold">
                    <span className="flex items-center gap-1.5">{label(e)}{e.pending && <PendingDot />}</span>
                    {e.paymentMode && <span className="block text-xs font-normal text-muted">{t(e.paymentMode)}{e.chequeNo ? ` #${e.chequeNo}` : ''}</span>}
                    {e.amount > 0 && e.bill && e.bill.items.length > 0 && <span className="block max-w-44 truncate text-xs font-normal text-muted">{e.bill.items.map((i) => i.name).join(', ')}</span>}
                    {e.note && <span className="block max-w-56 truncate text-xs font-normal text-muted">{e.note}</span>}
                  </td>
                  <td className="whitespace-nowrap px-2 py-2">{e.bill?.billNo ?? '—'}</td>
                  <td className="px-2 py-2">{formatDateTime(e.occurredAt)}</td>
                  <td className="whitespace-nowrap px-2 py-2 text-right font-semibold text-udhaar">{e.amount > 0 ? formatRupees(e.amount) : ''}</td>
                  <td className="whitespace-nowrap px-2 py-2 text-right font-semibold text-jama">{e.amount < 0 ? formatRupees(-e.amount) : ''}</td>
                  <td className="whitespace-nowrap px-2 py-2 text-right font-bold">{e.runningBalance < 0 ? `${t('advance')} ${formatRupees(-e.runningBalance)}` : formatRupees(e.runningBalance)}</td>
                  <td className="whitespace-nowrap px-2 py-2">
                    {s && <span className={`rounded-full px-2 text-xs font-bold ${tone[s]}`}>{t(`billList.status.${s}`)}</span>}
                    {s === 'partial' && <span className="block text-xs text-due">{t('billList.dueAmount', { amount: formatRupees(left) })}</span>}
                  </td>
                  <td className="px-3 py-2 text-right" onClick={(ev) => ev.stopPropagation()}>
                    {onRemove && (canRemove?.(e) ?? true) && e.kind !== 'reversal' && !e.reversesEntryId && !e.isReversed && !e.pending && (
                      <button className="min-h-9 text-sm text-muted underline" onClick={() => onRemove(e)}>{t('remove')}</button>
                    )}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </div>
  )
}

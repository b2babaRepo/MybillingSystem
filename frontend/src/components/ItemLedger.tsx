import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { monthToDate, saveCsv } from '../lib/csv'
import { formatDateTime, formatRupees } from '../lib/format'
import { navigate } from '../router'
import type { Unit } from '../lib/types'

interface LedgerLine {
  id: string; occurredAt: string; kind: string; reason: string | null; in: number; out: number; balance: number
  costPrice: number | null; note: string | null; by: string; docType: string | null; docId: string | null; docLabel: string | null
  bags?: number | null; bagBalance?: number
}
interface ItemLedgerData { name: string; unit: Unit; opening: number; in: number; out: number; closing: number; lines: LedgerLine[]; bagSize?: number | null; openingBags?: number; closingBags?: number }

/**
 * One item's stock account for chosen dates: opening, every change with the balance after it, who made it and the
 * document behind it, and closing. What GST rule 56 asks a shop to keep, and what an owner checks when stock goes missing.
 */
export function ItemLedger({ productId }: { productId: string }) {
  const { t } = useTranslation()
  const [range, setRange] = useState(monthToDate)
  const ledger = useQuery({
    queryKey: ['item-ledger', productId, range.from, range.to],
    queryFn: () => api<ItemLedgerData>(`/v1/products/${productId}/ledger?from=${range.from}&to=${range.to}`),
  })
  const d = ledger.data
  const unit = d ? t(`units.${d.unit}`) : ''
  const what = (l: LedgerLine) => [t(`stock.kinds.${l.kind}`, { defaultValue: t(`sl.kinds.${l.kind}`, { defaultValue: l.kind }) }), l.reason && t(`stock.reasons.${l.reason}`)].filter(Boolean).join(' · ')
  const doc = (l: LedgerLine) => l.docLabel ?? (l.docType ? t(`sl.docs.${l.docType}`, { defaultValue: l.docType }) : null)

  function download() {
    if (!d) return
    saveCsv(`stock-${d.name}-${range.from}-to-${range.to}.csv`, [
      [t('sl.date'), t('sl.what'), t('sl.doc'), t('sl.in'), t('sl.out'), t('sl.balance'), t('sl.by'), 'Note'],
      ['', t('sl.opening'), '', '', '', d.opening, '', ''],
      ...d.lines.map((l) => [formatDateTime(l.occurredAt), what(l), doc(l), l.in || '', l.out || '', l.balance, l.by, [l.note, l.bags ? t('bag.inBags', { count: l.bags }) : null].filter(Boolean).join(' · ')]),
      ['', t('sl.closing'), '', d.in, d.out, d.closing, '', ''],
    ])
  }

  return (
    <section className="flex flex-col gap-3 rounded-[18px] bg-white p-4 shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="text-base font-bold">📒 {t('sl.ledger')}</h2>
        <button onClick={download} disabled={!d} className="h-10 rounded-xl border border-line px-3 text-sm font-semibold transition hover:bg-[#f6faf9] disabled:opacity-40">⬇ {t('sl.csv')}</button>
      </div>
      <p className="text-sm text-muted">{t('sl.ledgerHint')}</p>
      <div className="flex flex-wrap gap-2 text-sm">
        <label className="flex items-center gap-1.5">{t('sl.from')}<input type="date" value={range.from} max={range.to} onChange={(e) => e.target.value && setRange({ ...range, from: e.target.value })} className="h-10 rounded-xl border border-line bg-white px-2 outline-none focus:border-teal-600" /></label>
        <label className="flex items-center gap-1.5">{t('sl.to')}<input type="date" value={range.to} min={range.from} onChange={(e) => e.target.value && setRange({ ...range, to: e.target.value })} className="h-10 rounded-xl border border-line bg-white px-2 outline-none focus:border-teal-600" /></label>
      </div>
      {d && (
        <div className="grid grid-cols-4 gap-1.5 text-center text-sm">
          {([['opening', d.opening, 'bg-[#edf2f1]'], ['in', d.in, 'bg-emerald-50 text-emerald-800'], ['out', d.out, 'bg-orange-50 text-orange-800'], ['closing', d.closing, 'bg-teal-50 text-teal-900']] as const).map(([k, v, tone]) => (
            <span key={k} className={`flex flex-col rounded-xl px-1 py-2 ${tone}`}><span className="text-xs">{t(`sl.${k}`)}</span><b className="text-base">{v}</b>
              {d.bagSize && (k === 'opening' || k === 'closing') ? <span className="text-[11px]">{t('bag.inBags', { count: k === 'opening' ? d.openingBags ?? 0 : d.closingBags ?? 0 })}</span> : null}</span>
          ))}
        </div>
      )}
      {ledger.isError && <p className="text-sm text-due">{t('error')}</p>}
      {d && d.lines.length === 0 && <p className="py-2 text-center text-muted">{t('sl.none')}</p>}
      {d && d.lines.length > 0 && (
        <ul className="divide-y divide-[#edf2f1]">
          {[...d.lines].reverse().map((l) => (
            <li key={l.id} className="flex items-start gap-3 py-2.5">
              <span aria-hidden className={`mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-sm font-bold ${l.in > 0 ? 'bg-emerald-50 text-jama' : 'bg-orange-50 text-udhaar'}`}>{l.in > 0 ? '↓' : '↑'}</span>
              <span className="flex min-w-0 flex-1 flex-col">
                <span className="font-semibold">{what(l)}</span>
                <span className="text-xs text-muted">
                  {formatDateTime(l.occurredAt)} · {t('sl.byName', { name: l.by })}
                  {l.costPrice !== null && ` · ${formatRupees(l.costPrice)}`}
                </span>
                {doc(l) && (l.docType === 'bill' && l.docId
                  ? <button onClick={() => navigate(`/print/bill/${l.docId}`)} className="w-fit text-xs font-semibold text-teal-700 underline">🧾 {doc(l)}</button>
                  : <span className="text-xs font-semibold text-teal-800">📄 {doc(l)}</span>)}
                {l.note && !l.docLabel && <span className="text-xs text-muted">{l.note}</span>}
              </span>
              <span className="flex shrink-0 flex-col items-end">
                {(l.in > 0 || l.out > 0) && <b className={l.in > 0 ? 'text-jama' : 'text-udhaar'}>{l.in > 0 ? `+${l.in}` : `−${l.out}`} {unit}</b>}
                {l.bags ? <span className={`text-sm font-semibold ${l.bags > 0 ? 'text-jama' : 'text-udhaar'}`}>{l.bags > 0 ? '+' : '−'}{t('bag.inBags', { count: Math.abs(l.bags) })}</span> : null}
                <span className="text-xs text-muted">{t('sl.balance')} {l.balance}{d.bagSize ? ` · ${t('bag.inBags', { count: l.bagBalance ?? 0 })}` : ''}</span>
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

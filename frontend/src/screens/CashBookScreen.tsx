import { useQuery } from '@tanstack/react-query'
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
            <p className="text-sm text-muted">{t('book.drawerHint')}</p>
          </section>
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

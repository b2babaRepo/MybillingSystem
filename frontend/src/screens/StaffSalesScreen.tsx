import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'

interface StaffSales { userId: string; name: string; bills: number; lines: number; sales: number; commissionPercent: number | null; commission: number }

function thisMonth() {
  return new Date(Date.now() + 5.5 * 3600_000).toISOString().slice(0, 7)
}

function shiftMonth(month: string, by: number) {
  const [y, m] = month.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1 + by, 1)).toISOString().slice(0, 7)
}

/** Who sold how much this month, and the commission each has earned at their percent. */
export function StaffSalesScreen({ shopId }: { shopId: string }) {
  const { t, i18n } = useTranslation()
  const now = thisMonth()
  const [month, setMonth] = useState(now)
  const rows = useQuery({ queryKey: [shopId, 'staff-sales', month], queryFn: () => api<StaffSales[]>(`/v1/reports/staff?month=${month}`) }).data
  const label = new Date(`${month}-01T00:00:00Z`).toLocaleDateString(i18n.language === 'hi' ? 'hi-IN' : 'en-IN', { month: 'long', year: 'numeric', timeZone: 'UTC' })
  const totalCommission = (rows ?? []).reduce((n, r) => n + r.commission, 0)
  return (
    <div className="flex flex-col gap-3 p-4 pb-24">
      <div className="flex items-center justify-between rounded-[18px] bg-white p-2">
        <button onClick={() => setMonth(shiftMonth(month, -1))} aria-label={t('book.previous')} className="h-11 w-11 rounded-xl text-xl">‹</button>
        <span className="text-lg font-bold">{label}</span>
        <button onClick={() => setMonth(shiftMonth(month, 1))} disabled={month >= now} aria-label={t('book.next')} className="h-11 w-11 rounded-xl text-xl disabled:opacity-30">›</button>
      </div>
      {rows?.length === 0 && <p className="py-6 text-center text-muted">{t('sales.none')}</p>}
      <ul className="divide-y divide-[#edf2f1] rounded-[18px] bg-white px-3">
        {rows?.map((r) => (
          <li key={r.userId} className="flex items-center gap-3 py-3">
            <span className="flex min-w-0 flex-1 flex-col">
              <span className="truncate font-bold">{r.name}</span>
              <span className="text-[13px] text-muted">{t('sales.line', { bills: r.bills, lines: r.lines })}</span>
            </span>
            <span className="flex shrink-0 flex-col items-end">
              <span className="font-extrabold">{formatRupees(r.sales)}</span>
              <span className="text-[13px] font-semibold text-teal-800">{r.commissionPercent ? t('sales.earned', { amount: formatRupees(r.commission), percent: r.commissionPercent }) : t('sales.noCommission')}</span>
            </span>
          </li>
        ))}
      </ul>
      {totalCommission > 0 && <p className="text-right font-bold">{t('sales.total', { amount: formatRupees(totalCommission) })}</p>}
      <button onClick={() => navigate('/settings/staff')} className="min-h-11 self-start text-sm font-semibold text-teal-700 underline">{t('sales.setRates')}</button>
    </div>
  )
}

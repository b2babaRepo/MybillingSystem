import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { shownLocale } from '../i18n'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'

export type ProfitBy = 'party' | 'bill' | 'category'

interface ProfitRow { key: string; id: string | null; name: string; detail: string | null; bills: number; sales: number; cost: number | null; profit: number | null; marginPercent: number | null; uncosted: number; at: string | null }
interface ProfitByReport { month: string; by: ProfitBy; sales: number; profit: number; rows: ProfitRow[] }

const tone = (m: number | null) => (m === null ? 'bg-[#edf2f1] text-muted' : m < 0 ? 'bg-due-soft text-due' : m < 8 ? 'bg-warn-soft text-udhaar' : 'bg-brand-soft text-brand-strong')

/**
 * The month's profit by customer, by bill or by category: who brings the money and which bills were sold too cheap.
 * Tapping a customer opens their khata; a bill opens the bill.
 */
export function ProfitByList({ shopId, month, by }: { shopId: string; month: string; by: ProfitBy }) {
  const { t } = useTranslation()
  const report = useQuery({ queryKey: [shopId, 'profit-by', by, month], queryFn: () => api<ProfitByReport>(`/v1/reports/profit-by?by=${by}&month=${month}`) })
  if (report.isError) return <p className="text-due">{report.error.message}</p>
  const data = report.data
  if (!data) return <p className="text-muted">…</p>

  const name = (r: ProfitRow) => by === 'party' && !r.id ? t('profitBy.walkIn') : by === 'category' && !r.name ? t('profitBy.noCategory') : r.name
  const sub = (r: ProfitRow) => {
    const parts = [by === 'bill' && r.at ? new Date(r.at).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' }) : null,
      by === 'bill' ? r.detail ?? t('profitBy.walkIn') : t('profitBy.bills', { count: r.bills }), formatRupees(r.sales)]
    return parts.filter(Boolean).join(' · ')
  }
  const open = (r: ProfitRow) => {
    if (by === 'party' && r.id) navigate(`/c/${r.id}`)
    else if (by === 'bill' && r.id) navigate(`/print/bill/${r.id}`)
  }

  return (
    <>
      <div className="grid grid-cols-2 gap-2">
        <div className="flex flex-col rounded-card bg-white p-3 shadow-sm"><span className="text-xs text-muted">{t('margin.sales')}</span><span className="text-xl font-extrabold">{formatRupees(data.sales)}</span></div>
        <div className="flex flex-col rounded-card bg-white p-3 shadow-sm"><span className="text-xs text-muted">{t('margin.profit')}</span><span className="text-xl font-extrabold text-brand-strong">{formatRupees(data.profit)}</span></div>
      </div>
      {data.rows.length === 0 && <p className="py-6 text-center text-muted">{t('margin.noSales')}</p>}
      {/* Lowest margin first for bills: the ones sold too cheap are what the owner wants to see. */}
      <ul className="divide-y divide-[#edf2f1] rounded-card bg-white px-3 shadow-sm">
        {(by === 'bill' ? [...data.rows].sort((a, b) => (a.marginPercent ?? 999) - (b.marginPercent ?? 999)) : data.rows).map((r) => (
          <li key={r.key}>
            <button onClick={() => open(r)} className="flex min-h-14 w-full items-center gap-3 py-2 text-left">
              <span className="flex min-w-0 flex-1 flex-col">
                <span className="truncate font-semibold">{name(r)}</span>
                <span className="truncate text-[13px] text-muted">{sub(r)}</span>
                {r.uncosted > 0 && <span className="text-xs text-udhaar">{t('profitBy.uncosted', { amount: formatRupees(r.uncosted) })}</span>}
              </span>
              <span className="flex shrink-0 flex-col items-end">
                <span className={`font-bold ${r.profit !== null && r.profit < 0 ? 'text-due' : ''}`}>{r.profit === null ? '—' : formatRupees(r.profit)}</span>
                {r.marginPercent !== null && <span className={`rounded-full px-2 text-xs font-bold ${tone(r.marginPercent)}`}>{r.marginPercent}%</span>}
              </span>
            </button>
          </li>
        ))}
      </ul>
    </>
  )
}

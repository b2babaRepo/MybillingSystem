import { navigate } from '../router'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { downloadCsv, type MonthReport } from '../lib/reports'

function shiftMonth(month: string, by: number) {
  const [year, m] = month.split('-').map(Number)
  const date = new Date(Date.UTC(year, m - 1 + by, 1))
  return date.toISOString().slice(0, 7)
}

const thisMonth = () => new Date(Date.now() + 5.5 * 3600_000).toISOString().slice(0, 7)

/** One month: sales, money in, udhaar out and back, estimated profit, the best sellers, and downloads. */
export function ReportsScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const current = thisMonth()
  const [month, setMonth] = useState(current)
  const [error, setError] = useState('')
  const report = useQuery({ queryKey: [shopId, 'report', month], queryFn: () => api<MonthReport>(`/v1/reports/month?month=${month}`) })
  const data = report.data
  const today = new Date()
  const fy = today.getMonth() >= 3 ? today.getFullYear() : today.getFullYear() - 1
  const year = useQuery({
    queryKey: [shopId, 'report-year', fy], enabled: isOwner, retry: false,
    queryFn: () => api<{ financialYear: string; total: { sales: number; gst: number; turnover: number; noRate: number; cash: number; digital: number } }>(`/v1/reports/year?fy=${fy}`),
  }).data
  const label = new Date(`${month}-01T00:00:00Z`).toLocaleDateString('en-IN', { month: 'long', year: 'numeric', timeZone: 'UTC' })
  const peak = Math.max(1, ...(data?.days.map((d) => d.sales) ?? [1]))

  async function download(path: string, name: string) {
    setError('')
    try {
      await downloadCsv(path, name)
    } catch (err) {
      setError(err instanceof Error ? err.message : t('error'))
    }
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex items-center justify-between rounded-[18px] bg-white p-2">
        <button onClick={() => setMonth(shiftMonth(month, -1))} aria-label={t('reports.previous')} className="h-11 w-11 rounded-xl text-xl">‹</button>
        <span className="text-lg font-bold">{label}</span>
        <button onClick={() => setMonth(shiftMonth(month, 1))} disabled={month >= current} aria-label={t('reports.next')} className="h-11 w-11 rounded-xl text-xl disabled:opacity-30">›</button>
      </div>

      {report.isError && <p className="text-due">{report.error.message}</p>}
      {data && (
        <>
          <section className="flex flex-col gap-3 rounded-[20px] bg-white p-[18px]">
            <div className="flex items-baseline justify-between">
              <span className="text-[15px] font-semibold text-muted">{t('reports.sales')}</span>
              <span className="text-[13px] text-muted">{t('reports.bills', { count: data.bills })}</span>
            </div>
            <span className="text-[40px] leading-none font-extrabold">{formatRupees(data.sales)}</span>
            <div className="grid grid-cols-2 gap-2">
              <Stat label={`${t('reports.received')} · ${t('cash')}`} value={data.cash} tone="bg-emerald-50 text-emerald-800" />
              <Stat label={`${t('reports.received')} · ${t('upi')}`} value={data.upi} tone="bg-blue-50 text-blue-800" />
              <Stat label={t('reports.udhaarGiven')} value={data.udhaarGiven} tone="bg-orange-50 text-orange-800" />
              <Stat label={t('reports.collected')} value={data.khataCollected} tone="bg-emerald-50 text-emerald-800" />
            </div>
          </section>

          <section className="flex flex-col gap-1 rounded-[18px] bg-white p-4">
            {isOwner && <>
            <span className="text-sm font-semibold text-muted">{t('reports.profit')}</span>
            <span className="text-2xl font-extrabold text-jama">{formatRupees(data.estimatedProfit)}</span>
            {((data.returns ?? 0) !== 0 || (data.stockLoss ?? 0) !== 0) && <span className="text-xs text-muted">{t('reports.lessLosses', { returns: formatRupees(data.returns ?? 0), loss: formatRupees(data.stockLoss ?? 0) })}</span>}
            {data.profitCoverage < 1 && <span className="text-sm text-muted">{t('reports.coverage', { percent: Math.round(data.profitCoverage * 100) })}</span>}
            {/* Profit on goods less the month's kharcha = what the shop really earned. */}
            {(data.expenses ?? 0) > 0 && (
              <div className="mt-2 flex flex-col gap-1 border-t border-[#edf2f1] pt-2 text-sm">
                <span className="flex justify-between"><span className="text-muted">{t('reports.expenses')}</span><span className="font-bold text-due">−{formatRupees(data.expenses ?? 0)}</span></span>
                <span className="flex justify-between text-base"><span className="font-semibold">{t('reports.netProfit')}</span><span className="font-extrabold">{formatRupees(data.netProfit ?? data.estimatedProfit)}</span></span>
              </div>
            )}
            {((data.stockPurchases ?? 0) > 0 || (data.takenHome ?? 0) > 0) && (
              <span className="text-xs text-muted">{t('reports.notCosts', { stock: formatRupees(data.stockPurchases ?? 0), home: formatRupees(data.takenHome ?? 0) })}</span>
            )}
            <div className="mt-2 flex flex-col gap-1 border-t border-[#edf2f1] pt-2 text-sm">
              <span className="flex justify-between"><span className="text-muted">{t('reports.stockValue')}</span><span className="font-bold">{formatRupees(data.stockValue ?? 0)}</span></span>
              <span className="flex justify-between"><span className="text-muted">{t('reports.supplierDue')}</span><span className="font-bold text-due">{formatRupees(data.supplierDue ?? 0)}</span></span>
            </div>
            </>}
            <span className="mt-2 flex justify-between border-t border-[#edf2f1] pt-2 text-sm">
              <span className="text-muted">{t('reports.outstanding')}</span>
              <span className="font-bold text-due">{formatRupees(data.outstanding)}</span>
            </span>
          </section>

          <section className="rounded-[18px] bg-white p-4">
            <h2 className="mb-3 text-base font-bold">{t('reports.daily')}</h2>
            <div role="img" aria-label={t('reports.daily')} className="flex h-32 items-end gap-[2px]">
              {data.days.map((d) => (
                <span key={d.date} title={`${d.date}: ${formatRupees(d.sales)}`}
                  className={`flex-1 rounded-t-sm ${d.sales > 0 ? 'bg-teal-700' : 'bg-[#edf2f1]'}`}
                  style={{ height: `${Math.max(3, (d.sales / peak) * 100)}%` }} />
              ))}
            </div>
            <div className="mt-1 flex justify-between text-xs text-muted"><span>1</span><span>{data.days.length}</span></div>
          </section>

          {data.topItems.length > 0 && (
            <section className="rounded-[18px] bg-white px-4 py-3">
              <h2 className="pb-1 text-base font-bold">{t('reports.topItems')}</h2>
              <ol className="divide-y divide-[#edf2f1]">
                {data.topItems.map((item, i) => (
                  <li key={`${item.name}-${i}`} className="flex justify-between gap-2 py-2">
                    <span className="truncate"><span className="text-muted">{i + 1}.</span> {item.name}</span>
                    <span className="shrink-0 font-semibold">{item.quantity} {t(`units.${item.unit}`)} · {formatRupees(item.amount)}</span>
                  </li>
                ))}
              </ol>
            </section>
          )}

          {isOwner && <Button tone="plain" onClick={() => navigate('/reports/items')}>{t('margin.title')}</Button>}
          {isOwner && <Button tone="plain" onClick={() => navigate('/reports/sizes')}>{t('sizeReport.title')}</Button>}
          {isOwner && <Button tone="plain" onClick={() => navigate('/reports/staff')}>{t('sales.title')}</Button>}
          {isOwner && <Button tone="plain" onClick={() => navigate('/pnl')}>{t('pnl.title')}</Button>}
          {isOwner && <Button tone="plain" onClick={() => navigate('/gst')}>{t('gstExport.title')}</Button>}
          {year && (
            <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
              <h2 className="text-base font-bold">{t('reports.yearTitle', { fy: year.financialYear })}</h2>
              <p className="flex justify-between font-bold"><span>{t('reports.turnover')}</span><span>{formatRupees(year.total.turnover)}</span></p>
              <p className="flex justify-between text-sm"><span>{t('reports.gstInside')}</span><span>{formatRupees(year.total.gst)}</span></p>
              <p className="flex justify-between text-sm"><span>{t('reports.cashReceived')}</span><span>{formatRupees(year.total.cash)}</span></p>
              <p className="flex justify-between text-sm"><span>{t('reports.digitalReceived')}</span><span>{formatRupees(year.total.digital)}</span></p>
              {year.total.noRate > 0 && <p className="text-sm font-semibold text-due">{t('reports.noRate', { amount: formatRupees(year.total.noRate) })}</p>}
              <p className="text-xs text-muted">{t('reports.yearHint')}</p>
              <Button tone="plain" onClick={() => download(`/v1/reports/year.csv?fy=${fy}`, `year-${year.financialYear}.csv`)}>{t('reports.yearCsv')}</Button>
            </section>
          )}

          <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
            <h2 className="text-base font-bold">{t('reports.download')}</h2>
            {isOwner ? (
              <div className="grid grid-cols-2 gap-2">
                <Button tone="plain" onClick={() => download('/v1/reports/dues.csv', `dues-${current}.csv`)}>{t('reports.duesCsv')}</Button>
                <Button tone="plain" onClick={() => download(`/v1/reports/month.csv?month=${month}`, `month-${month}.csv`)}>{t('reports.monthCsv')}</Button>
                <Button tone="plain" onClick={() => download('/v1/reports/supplier-dues.csv', `supplier-dues-${current}.csv`)}>{t('reports.supplierDuesCsv')}</Button>
              </div>
            ) : (
              <p className="text-sm text-muted">{t('reports.ownerOnly')}</p>
            )}
            {error && <p className="text-sm text-due">{error}</p>}
          </section>
        </>
      )}
    </div>
  )
}

function Stat({ label, value, tone }: { label: string; value: number; tone: string }) {
  return (
    <div className={`flex flex-col rounded-xl p-2.5 ${tone}`}>
      <span className="text-xs">{label}</span>
      <span className="text-[17px] font-bold">{formatRupees(value)}</span>
    </div>
  )
}

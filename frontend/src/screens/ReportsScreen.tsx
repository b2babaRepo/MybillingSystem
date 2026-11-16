import { DataTable } from '../components/DataTable'
import { useItemName } from '../lib/useItemName'
import { shownLocale } from '../i18n'
import { navigate } from '../router'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { downloadCsv, type MonthReport } from '../lib/reports'
import { MonthStatementCard } from './MonthStatement'
import { change, Delta, ReportShell, Sparkline, UpDownBars, useReportView } from '../components/ReportShell'

function shiftMonth(month: string, by: number) {
  const [year, m] = month.split('-').map(Number)
  const date = new Date(Date.UTC(year, m - 1 + by, 1))
  return date.toISOString().slice(0, 7)
}

const thisMonth = () => new Date(Date.now() + 5.5 * 3600_000).toISOString().slice(0, 7)

/** One month: sales, money in, udhaar out and back, estimated profit, the best sellers, and downloads. */
export function ReportsScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const nm = useItemName(shopId)
  const current = thisMonth()
  const [month, setMonth] = useState(current)
  const [error, setError] = useState('')
  // Owners only: for a helper this fails quietly and the section stays away.
  const speed = useQuery({ queryKey: [shopId, 'bill-speed'], queryFn: () => api<{ bills: number; medianSeconds: number | null; rows: { group: string; bills: number; medianSeconds: number }[] }>('/v1/reports/bill-speed'), retry: false }).data
  const report = useQuery({ queryKey: [shopId, 'report', month], queryFn: () => api<MonthReport>(`/v1/reports/month?month=${month}`) })
  const data = report.data
  // The month before, for the moves (▲▼) and the dashed average line.
  const before = useQuery({ queryKey: [shopId, 'report', shiftMonth(month, -1)], queryFn: () => api<MonthReport>(`/v1/reports/month?month=${shiftMonth(month, -1)}`), retry: false }).data
  const [view, setView] = useReportView()
  const today = new Date()
  const fy = today.getMonth() >= 3 ? today.getFullYear() : today.getFullYear() - 1
  const year = useQuery({
    queryKey: [shopId, 'report-year', fy], enabled: isOwner, retry: false,
    queryFn: () => api<{ financialYear: string; months: YearRow[]; total: YearRow }>(`/v1/reports/year?fy=${fy}`),
  }).data
  const label = new Date(`${month}-01T00:00:00Z`).toLocaleDateString(shownLocale(), { month: 'long', year: 'numeric', timeZone: 'UTC' })
  const peak = Math.max(1, ...(data?.days.map((d) => d.sales) ?? [1]))

  async function download(path: string, name: string) {
    setError('')
    try {
      await downloadCsv(path, name)
    } catch (err) {
      setError(err instanceof Error ? err.message : t('error'))
    }
  }

  const avgBill = data && data.bills ? Math.round(data.sales / data.bills) : 0
  const avgBefore = before && before.bills ? before.sales / before.bills : null
  let run = 0
  const cumulative = data?.days.map((d) => (run += d.sales)) ?? []
  const ticks = data ? [
    { label: t('reports.sales'), value: formatRupees(data.sales), change: change(data.sales, before?.sales) },
    { label: t('rv.billsTick'), value: String(data.bills), change: change(data.bills, before?.bills) },
    { label: t('rv.avgBill'), value: formatRupees(avgBill), change: change(avgBill, avgBefore) },
    { label: t('reports.outstanding'), value: formatRupees(data.outstanding) },
    ...data.topItems.slice(0, 4).map((i) => ({ label: i.name, value: formatRupees(i.amount) })),
  ] : []
  const mix = data ? [
    { key: 'cash', amount: data.cash, color: '#0f766e' }, { key: 'upi', amount: data.upi, color: '#2563eb' },
    { key: 'card', amount: data.card ?? 0, color: '#f59e0b' }, { key: 'cheque', amount: data.cheque ?? 0, color: '#7c3aed' },
  ].filter((m) => m.amount > 0) : []
  const mixTotal = mix.reduce((a, m) => a + m.amount, 0)

  return (
    <ReportShell page="reports" title={t('rv.reportsTitle')} isOwner={isOwner} view={view} onView={setView} ticks={ticks}>
      <div className="flex items-center justify-between rounded-card bg-white p-2 shadow-sm print:shadow-none">
        <button onClick={() => setMonth(shiftMonth(month, -1))} aria-label={t('reports.previous')} className="h-11 w-11 rounded-xl text-xl">‹</button>
        <span className="text-lg font-bold">{label}</span>
        <button onClick={() => setMonth(shiftMonth(month, 1))} disabled={month >= current} aria-label={t('reports.next')} className="h-11 w-11 rounded-xl text-xl disabled:opacity-30">›</button>
      </div>

      {report.isError && <p className="text-due">{report.error.message}</p>}
      {data && view === 'chart' && <>
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
          <Tile label={t('reports.sales')} value={formatRupees(data.sales)} delta={change(data.sales, before?.sales)} trend={cumulative} />
          <Tile label={t('rv.billsTick')} value={String(data.bills)} delta={change(data.bills, before?.bills)} />
          <Tile label={t('rv.avgBill')} value={formatRupees(avgBill)} delta={change(avgBill, avgBefore)} />
          <Tile label={t('reports.outstanding')} value={formatRupees(data.outstanding)} />
        </div>
        <div className="flex flex-col gap-3 lg:grid lg:grid-cols-[2fr_1fr] lg:items-start">
          <UpDownBars days={data.days} average={before && before.days.length ? before.sales / before.days.length : undefined} label={t('rv.daily')} />
          {mixTotal > 0 && (
            <section className="flex flex-col gap-3 rounded-card bg-white p-4 shadow-sm print:shadow-none">
              <b>{t('rv.cameBy')}</b>
              <div role="img" aria-label={mix.map((m) => `${t(m.key)} ${Math.round((m.amount * 100) / mixTotal)}%`).join(', ')} className="flex h-4 overflow-hidden rounded-full">
                {mix.map((m) => <span key={m.key} style={{ width: `${(m.amount * 100) / mixTotal}%`, background: m.color }} />)}
              </div>
              <div className="grid grid-cols-2 gap-2 text-sm">
                {mix.map((m) => <span key={m.key}><span style={{ color: m.color }}>●</span> {t(m.key)} · {Math.round((m.amount * 100) / mixTotal)}%<b className="block text-base">{formatRupees(m.amount)}</b></span>)}
              </div>
            </section>
          )}
        </div>
      </>}
      {data && view === 'ledger' && (
        <section className="flex flex-col gap-2">
          <h2 className="px-1 font-bold">{year ? t('rv.monthsTable', { fy: year.financialYear }) : t('rv.daysTable')}</h2>
          {year ? (() => {
            // The change is worked out in month order before sorting, so it stays right whichever column is sorted.
            const months = year.months.filter((m) => m.month <= month && (m.sales > 0 || m.month === month))
              .map((m, i, all) => ({ ...m, change: i > 0 ? change(m.sales, all[i - 1].sales) : null }))
            return (
              <DataTable label={t('rv.monthsTable', { fy: year.financialYear })} rows={months} rowKey={(m) => m.month} selectedKey={month}
                columns={[
                  { key: 'month', header: t('rv.month'), sortValue: (m) => m.month, render: (m) => new Date(`${m.month}-01T00:00:00Z`).toLocaleDateString(shownLocale(), { month: 'short', year: '2-digit', timeZone: 'UTC' }) },
                  { key: 'sales', header: t('reports.sales'), align: 'right', sortValue: (m) => m.sales, render: (m) => formatRupees(m.sales) },
                  { key: 'bills', header: t('rv.billsTick'), align: 'right', sortValue: (m) => m.bills, render: (m) => m.bills },
                  { key: 'cash', header: t('cash'), align: 'right', sortValue: (m) => m.cash, render: (m) => formatRupees(m.cash) },
                  { key: 'digital', header: t('rv.digital'), align: 'right', sortValue: (m) => m.digital, render: (m) => formatRupees(m.digital) },
                  { key: 'gst', header: t('rv.gstCol'), align: 'right', sortValue: (m) => m.gst, render: (m) => formatRupees(m.gst) },
                  { key: 'change', header: t('rv.change'), align: 'right', sortValue: (m) => m.change ?? -1e9, render: (m) => <Delta value={m.change} /> },
                ]} />
            )
          })() : (() => {
            const days = data.days.map((d, i) => ({ ...d, change: i > 0 ? change(d.sales, data.days[i - 1].sales) : null }))
            return (
              <DataTable label={t('rv.daysTable')} rows={days} rowKey={(d) => d.date}
                columns={[
                  { key: 'day', header: t('rv.day'), sortValue: (d) => d.date, render: (d) => new Date(d.date).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' }) },
                  { key: 'sales', header: t('reports.sales'), align: 'right', sortValue: (d) => d.sales, render: (d) => formatRupees(d.sales) },
                  { key: 'received', header: t('rv.received'), align: 'right', sortValue: (d) => d.received, render: (d) => formatRupees(d.received) },
                  { key: 'change', header: t('rv.change'), align: 'right', sortValue: (d) => d.change ?? -1e9, render: (d) => <Delta value={d.change} /> },
                ]} />
            )
          })()}
        </section>
      )}
      {data && (
        <div className="flex flex-col gap-3 lg:grid lg:grid-cols-2 lg:items-start">
          <section className="flex flex-col gap-3 rounded-[20px] bg-white p-[18px] shadow-sm print:shadow-none">
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

          <section className="flex flex-col gap-1 rounded-card bg-white p-4 shadow-sm print:shadow-none">
            {isOwner && <>
            {data.statement ? <MonthStatementCard st={data.statement} /> : <>
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
            </>}
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

          <section className="rounded-card bg-white p-4 shadow-sm print:shadow-none">
            <h2 className="mb-3 text-base font-bold">{t('reports.daily')}</h2>
            <div role="img" aria-label={t('reports.daily')} className="flex h-32 items-end gap-[2px]">
              {data.days.map((d) => (
                <span key={d.date} title={`${d.date}: ${formatRupees(d.sales)}`}
                  className={`flex-1 rounded-t-sm ${d.sales > 0 ? 'bg-brand' : 'bg-[#edf2f1]'}`}
                  style={{ height: `${Math.max(3, (d.sales / peak) * 100)}%` }} />
              ))}
            </div>
            <div className="mt-1 flex justify-between text-xs text-muted"><span>1</span><span>{data.days.length}</span></div>
          </section>

          {data.topItems.length > 0 && (
            <section className="rounded-card bg-white px-4 py-3 shadow-sm print:shadow-none">
              <h2 className="flex justify-between gap-2 pb-1 text-base font-bold">{t('reports.topItems')}<span className="text-xs font-semibold text-muted">{t('heads.topRow')}</span></h2>
              <ol className="divide-y divide-[#edf2f1]">
                {data.topItems.map((item, i) => (
                  <li key={`${item.name}-${i}`} className="flex justify-between gap-2 py-2">
                    <span className="truncate"><span className="text-muted">{i + 1}.</span> {nm(item)}</span>
                    <span className="shrink-0 font-semibold">{item.quantity} {t(`units.${item.unit}`, { count: item.quantity })} · {formatRupees(item.amount)}</span>
                  </li>
                ))}
              </ol>
            </section>
          )}

          <Button tone="plain" onClick={() => navigate('/reports/day')}>{t('day.link')}</Button>
          <Button tone="plain" onClick={() => navigate('/cheques')}>{t('cheques.link')}</Button>
          <Button tone="plain" onClick={() => navigate('/reports/week')}>{t('insights.weekLink')}</Button>
          {isOwner && <Button tone="plain" onClick={() => navigate('/reports/customers')}>{t('insights.customersLink')}</Button>}
          {isOwner && <Button tone="plain" onClick={() => navigate('/reports/items')}>{t('margin.title')}</Button>}
          {isOwner && <Button tone="plain" onClick={() => navigate('/reports/sizes')}>{t('sizeReport.title')}</Button>}
          {isOwner && <Button tone="plain" onClick={() => navigate('/reports/staff')}>{t('sales.title')}</Button>}
          {isOwner && <Button tone="plain" onClick={() => navigate('/pnl')}>{t('pnl.title')}</Button>}
          {isOwner && <Button tone="plain" onClick={() => navigate('/gst')}>{t('gstExport.title')}</Button>}
          {year && (
            <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm print:shadow-none">
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

          {speed && speed.medianSeconds !== null && (
            <section className="flex flex-col gap-1 rounded-card bg-white p-4 shadow-sm print:shadow-none">
              <h2 className="text-base font-bold">{t('reports.speedTitle')}</h2>
              <p className="text-lg font-semibold">{t('reports.speedTypical', { seconds: speed.medianSeconds, count: speed.bills })}</p>
              {speed.rows.map((r) => (
                <p key={r.group} className="flex justify-between text-sm"><span>{t(`reports.speed_${r.group}`)} · {r.bills}</span><span className="font-semibold">{t('reports.speedSeconds', { seconds: r.medianSeconds })}</span></p>
              ))}
              <p className="text-xs text-muted">{t('reports.speedNote')}</p>
            </section>
          )}
          <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm print:shadow-none">
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
        </div>
      )}
    </ReportShell>
  )
}

type YearRow = { month: string; bills: number; sales: number; gst: number; turnover: number; noRate: number; cash: number; digital: number }

function Stat({ label, value, tone }: { label: string; value: number; tone: string }) {
  return (
    <div className={`flex flex-col rounded-xl p-2.5 ${tone}`}>
      <span className="text-xs">{label}</span>
      <span className="text-[17px] font-bold">{formatRupees(value)}</span>
    </div>
  )
}

function Tile({ label, value, delta, trend, invert = false }: { label: string; value: string; delta?: number | null; trend?: number[]; invert?: boolean }) {
  const { t } = useTranslation()
  return (
    <div className="flex flex-col gap-1 rounded-card bg-white p-4 shadow-sm print:shadow-none">
      <span className="text-[13px] text-muted">{label}</span>
      <b className="text-[26px] leading-tight">{value}</b>
      {delta === undefined ? null : delta !== null ? <span className="text-[13px]"><Delta value={delta} invert={invert} /> {t('rv.vsBefore')}</span> : <span className="text-[13px] text-muted">{t('rv.noCompare')}</span>}
      {trend && <Sparkline values={trend} />}
    </div>
  )
}

import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { api, request } from '../lib/api'
import { formatRupees } from '../lib/format'
import { change, Delta, ReportShell, useReportView, Waterfall } from '../components/ReportShell'

interface ProfitLoss {
  financialYear: string; sales: number; turnover: number; gst: number; goodsProfit: number; returns: number; stockLoss: number; expenses: number; netProfit: number
  takenHome: number; purchases: number; openingStock: number; closingStock: number; closingDate: string; supplierDue: number; customerDue: number; itemsWithoutCost: number; profitCoverage: number; workInProgress?: number; machineWear?: number; finishedGoods?: number
  expenseLines?: { category: string; amount: number }[] | null; notInProfit?: { category: string; amount: number }[] | null
  cashReceipts?: number; digitalReceipts?: number; presumedProfit?: number; turnoverAlerts?: { key: string; limit: number; turnover: number; crossed: boolean }[] | null
}

/** A financial year's statement for the owner and their CA, with 31 March stock read back from the movements. */
export function ProfitLossScreen() {
  const { t } = useTranslation()
  const today = new Date()
  const current = today.getMonth() >= 3 ? today.getFullYear() : today.getFullYear() - 1
  const [fy, setFy] = useState(current)
  const [error, setError] = useState('')
  const data = useQuery({ queryKey: ['pnl', fy], queryFn: () => api<ProfitLoss>(`/v1/reports/pnl?fy=${fy}`), retry: false })
  const r = data.data
  // Last year, for the moves on the ticker and the margin.
  const last = useQuery({ queryKey: ['pnl', fy - 1], queryFn: () => api<ProfitLoss>(`/v1/reports/pnl?fy=${fy - 1}`), retry: false }).data
  const [view, setView] = useReportView()
  const margin = (x?: ProfitLoss) => (x && x.turnover ? Math.round((x.netProfit * 1000) / x.turnover) / 10 : null)
  const cost = r ? r.turnover - r.goodsProfit : 0
  const ticks = r ? [
    { label: t('pnl.sales'), value: formatRupees(r.sales), change: change(r.sales, last?.sales) },
    { label: t('pnl.net'), value: formatRupees(r.netProfit), change: change(r.netProfit, last?.netProfit) },
    { label: t('rv.margin'), value: margin(r) === null ? '–' : `${margin(r)}%` },
    { label: t('pnl.expenses'), value: formatRupees(r.expenses), change: change(r.expenses, last?.expenses) },
    { label: t('pnl.customers'), value: formatRupees(r.customerDue) },
  ] : []
  const topExpense = Math.max(1, ...(r?.expenseLines ?? []).map((x) => x.amount))

  async function download() {
    setError('')
    try {
      const response = await request(`/v1/reports/pnl.csv?fy=${fy}`)
      const link = document.createElement('a')
      link.href = URL.createObjectURL(await response.blob())
      link.download = `profit-loss-${r?.financialYear}.csv`
      link.click()
      URL.revokeObjectURL(link.href)
    } catch (e) {
      setError(e instanceof Error ? e.message : t('error'))
    }
  }

  const row = (label: string, value: number, strong = false) => (
    <p key={label} className={`flex justify-between ${strong ? 'border-t border-line pt-2 text-lg font-extrabold' : ''}`}><span>{label}</span><span>{formatRupees(value)}</span></p>
  )

  return (
    <ReportShell page="pnl" title={t('rv.pnlTitle')} isOwner view={view} onView={setView} ticks={ticks}>
      <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('pnl.year')}</span>
        <select value={fy} onChange={(e) => setFy(Number(e.target.value))} className="h-12 rounded-xl border border-line bg-white px-3 text-lg">
          {[current, current - 1, current - 2].map((y) => <option key={y} value={y}>{y}-{String((y + 1) % 100).padStart(2, '0')}</option>)}
        </select></label>
      {data.isError && <p className="text-due">{data.error.message}</p>}
      {!r && !data.isError && <p className="text-muted">…</p>}
      {r && view === 'chart' && (
        <section className="flex flex-col gap-3 rounded-[18px] bg-white p-4">
          <div className="flex flex-wrap items-baseline justify-between gap-2">
            <b className="text-[17px]">{t('rv.howProfit', { sales: formatRupees(r.sales), net: formatRupees(r.netProfit) })}</b>
            {margin(r) !== null && <span className="font-bold text-teal-800">{t('rv.marginIs', { percent: margin(r) })} <Delta value={margin(last) === null ? null : Math.round(((margin(r) ?? 0) - (margin(last) ?? 0)) * 10) / 10} /></span>}
          </div>
          <Waterfall label={t('rv.howProfit', { sales: formatRupees(r.sales), net: formatRupees(r.netProfit) })} steps={[
            { label: t('pnl.sales'), amount: r.sales, total: true },
            { label: t('rv.gstShort'), amount: -r.gst },
            { label: t('rv.costShort'), amount: -cost },
            { label: t('rv.lossShort'), amount: -(r.returns + r.stockLoss) },
            { label: t('pnl.expenses'), amount: -(r.expenses + (r.machineWear ?? 0)) },
            { label: t('pnl.net'), amount: r.netProfit, total: true },
          ]} />
          {(r.expenseLines ?? []).length > 0 && (
            <div className="grid grid-cols-1 gap-2 text-sm sm:grid-cols-2 lg:grid-cols-3">
              {(r.expenseLines ?? []).map((x) => (
                <div key={x.category} className="flex flex-col gap-1">
                  <span className="flex justify-between"><span>{t(`expense.cat.${x.category}`)}</span><b>{formatRupees(x.amount)}</b></span>
                  <span className="h-2 rounded-full bg-[#edf2f1]"><span className="block h-2 rounded-full bg-orange-600" style={{ width: `${(x.amount * 100) / topExpense}%` }} /></span>
                </div>
              ))}
            </div>
          )}
        </section>
      )}
      {r && view === 'ledger' && (
        <section className="grid grid-cols-1 overflow-hidden rounded-xl border border-line bg-white text-sm sm:grid-cols-2">
          <div className="flex flex-col border-b border-line sm:border-r sm:border-b-0">
            <b className="border-b border-line bg-[#f3f6f5] px-3 py-2">{t('rv.dr')}</b>
            <Line label={t('rv.costShort')} value={cost} />
            <Line label={t('pnl.returns')} value={r.returns} />
            <Line label={t('pnl.loss')} value={r.stockLoss} />
            <Line label={t('rv.grossCd')} value={r.goodsProfit - r.returns - r.stockLoss} strong />
            {(r.expenseLines ?? []).length ? (r.expenseLines ?? []).map((x) => <Line key={x.category} label={t(`expense.cat.${x.category}`)} value={x.amount} />) : <Line label={t('pnl.expenses')} value={r.expenses} />}
            {(r.machineWear ?? 0) > 0 && <Line label={t('pnl.wear')} value={r.machineWear ?? 0} />}
            <Line label={t('pnl.net')} value={r.netProfit} strong tone={r.netProfit >= 0 ? 'text-teal-800' : 'text-orange-800'} />
          </div>
          <div className="flex flex-col">
            <b className="border-b border-line bg-[#f3f6f5] px-3 py-2">{t('rv.cr')}</b>
            <Line label={t('pnl.turnover')} value={r.turnover} />
            <p className="px-3 py-2 text-muted">{t('rv.gstApart', { gst: formatRupees(r.gst) })}</p>
            <Line label={t('rv.grossBd')} value={r.goodsProfit - r.returns - r.stockLoss} strong />
          </div>
        </section>
      )}
      {r && <div className="flex flex-col gap-3 lg:grid lg:grid-cols-2 lg:items-start lg:[&>p]:col-span-2 lg:[&>button]:col-span-2">
        {(r.turnoverAlerts ?? []).map((a) => (
          <p key={a.key} className={`rounded-xl p-3 text-sm font-semibold ${a.crossed ? 'bg-amber-50 text-due' : 'bg-teal-50 text-teal-900'}`}>
            {t(`pnl.limit.${a.key}`, { limit: formatRupees(a.limit), turnover: formatRupees(a.turnover) })} {a.crossed ? t('pnl.limitCrossed') : t('pnl.limitNear')}
          </p>
        ))}
        <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
          {row(t('pnl.sales'), r.sales)}
          {row(t('pnl.gst'), r.gst)}
          {row(t('pnl.turnover'), r.turnover, true)}
          {row(t('pnl.goods'), r.goodsProfit)}
          {row(t('pnl.returns'), -r.returns)}
          {row(t('pnl.loss'), -r.stockLoss)}
          {row(t('pnl.expenses'), -r.expenses)}
          {(r.expenseLines ?? []).map((x) => <p key={x.category} className="flex justify-between pl-4 text-sm text-muted"><span>{t(`expense.cat.${x.category}`)}</span><span>−{formatRupees(x.amount)}</span></p>)}
          {(r.machineWear ?? 0) > 0 && row(t('pnl.wear'), -(r.machineWear ?? 0))}
          {row(t('pnl.net'), r.netProfit, true)}
          {r.profitCoverage < 1 && r.sales > 0 && <p className="text-sm font-semibold text-due">{t('pnl.coverage', { percent: Math.round(r.profitCoverage * 100) })}</p>}
        </section>
        <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
          {row(t('pnl.opening'), r.openingStock)}
          {row(t('pnl.purchases'), r.purchases)}
          {row(t('pnl.closing', { date: new Date(r.closingDate).toLocaleDateString('en-IN') }), r.closingStock)}
          {(r.workInProgress ?? 0) > 0 && row(t('pnl.wip'), r.workInProgress ?? 0)}
          {(r.finishedGoods ?? 0) > 0 && row(t('pnl.finished'), r.finishedGoods ?? 0)}
          {row(t('pnl.home'), r.takenHome)}
          {(r.notInProfit ?? []).filter((x) => x.category !== 'home' && x.category !== 'purchase').map((x) => <p key={x.category} className="flex justify-between text-sm"><span className="text-muted">{t(`expense.cat.${x.category}`)}</span><span>{formatRupees(x.amount)}</span></p>)}
          {row(t('pnl.suppliers'), r.supplierDue)}
          {row(t('pnl.customers'), r.customerDue)}
          {r.itemsWithoutCost > 0 && <p className="text-sm font-semibold text-due">{t('pnl.missing', { count: r.itemsWithoutCost })}</p>}
        </section>
        <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
          {row(t('pnl.cash'), r.cashReceipts ?? 0)}
          {row(t('pnl.digital'), r.digitalReceipts ?? 0)}
          {row(t('pnl.presumed'), r.presumedProfit ?? 0, true)}
          {(r.cashReceipts ?? 0) + (r.digitalReceipts ?? 0) > 0 && (() => {
            const percent = Math.round(((r.cashReceipts ?? 0) * 1000) / ((r.cashReceipts ?? 0) + (r.digitalReceipts ?? 0))) / 10
            return <p className={`rounded-xl p-2 text-sm ${percent <= 5 ? 'bg-emerald-50 text-emerald-800' : 'bg-amber-50 text-amber-800'}`}>{t('cashLimit.share', { percent })} {t(percent <= 5 ? 'cashLimit.shareOk' : 'cashLimit.shareHigh')}</p>
          })()}
          <p className="text-xs text-muted">{t('pnl.presumedHint')}</p>
        </section>
        <p className="text-xs text-muted">{t('pnl.hint')}</p>
        <Button tone="plain" onClick={() => void download()}>{t('pnl.download')}</Button>
        {error && <p className="text-sm text-due">{error}</p>}
      </div>}
    </ReportShell>
  )
}

function Line({ label, value, strong = false, tone = '' }: { label: string; value: number; strong?: boolean; tone?: string }) {
  return <p className={`flex justify-between gap-2 px-3 py-2 ${strong ? 'border-t border-line font-bold' : ''} ${tone}`}><span>{label}</span><span className="tabular-nums">{formatRupees(value)}</span></p>
}

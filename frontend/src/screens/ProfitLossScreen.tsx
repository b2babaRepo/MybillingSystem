import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { api, request } from '../lib/api'
import { formatRupees } from '../lib/format'

interface ProfitLoss {
  financialYear: string; sales: number; turnover: number; gst: number; goodsProfit: number; returns: number; stockLoss: number; expenses: number; netProfit: number
  takenHome: number; purchases: number; openingStock: number; closingStock: number; closingDate: string; supplierDue: number; customerDue: number; itemsWithoutCost: number; profitCoverage: number; workInProgress?: number; machineWear?: number
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
    <div className="flex flex-col gap-3 p-4">
      <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('pnl.year')}</span>
        <select value={fy} onChange={(e) => setFy(Number(e.target.value))} className="h-12 rounded-xl border border-line bg-white px-3 text-lg">
          {[current, current - 1, current - 2].map((y) => <option key={y} value={y}>{y}-{String((y + 1) % 100).padStart(2, '0')}</option>)}
        </select></label>
      {data.isError && <p className="text-due">{data.error.message}</p>}
      {!r && !data.isError && <p className="text-muted">…</p>}
      {r && <>
        <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
          {row(t('pnl.sales'), r.sales)}
          {row(t('pnl.gst'), r.gst)}
          {row(t('pnl.turnover'), r.turnover, true)}
          {row(t('pnl.goods'), r.goodsProfit)}
          {row(t('pnl.returns'), -r.returns)}
          {row(t('pnl.loss'), -r.stockLoss)}
          {row(t('pnl.expenses'), -r.expenses)}
          {(r.machineWear ?? 0) > 0 && row(t('pnl.wear'), -(r.machineWear ?? 0))}
          {row(t('pnl.net'), r.netProfit, true)}
          {r.profitCoverage < 1 && r.sales > 0 && <p className="text-sm font-semibold text-due">{t('pnl.coverage', { percent: Math.round(r.profitCoverage * 100) })}</p>}
        </section>
        <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
          {row(t('pnl.opening'), r.openingStock)}
          {row(t('pnl.purchases'), r.purchases)}
          {row(t('pnl.closing', { date: new Date(r.closingDate).toLocaleDateString('en-IN') }), r.closingStock)}
          {(r.workInProgress ?? 0) > 0 && row(t('pnl.wip'), r.workInProgress ?? 0)}
          {row(t('pnl.home'), r.takenHome)}
          {row(t('pnl.suppliers'), r.supplierDue)}
          {row(t('pnl.customers'), r.customerDue)}
          {r.itemsWithoutCost > 0 && <p className="text-sm font-semibold text-due">{t('pnl.missing', { count: r.itemsWithoutCost })}</p>}
        </section>
        <p className="text-xs text-muted">{t('pnl.hint')}</p>
        <Button tone="plain" onClick={() => void download()}>{t('pnl.download')}</Button>
        {error && <p className="text-sm text-due">{error}</p>}
      </>}
    </div>
  )
}

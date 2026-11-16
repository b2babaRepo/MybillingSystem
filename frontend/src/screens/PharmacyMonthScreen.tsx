import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Field } from '../components/ui'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'

interface Month {
  month: string; sales: number; salesTaxable: number; outputGst: number; inputGst: number; gstPayable: number; creditCarried: number; purchases: number; costOfSales: number
  grossProfit: number; marginPercent: number; expiryLoss: number; expiredOnShelf: number; profitAfterExpiry: number; cashReceipts: number; digitalReceipts: number
  linesWithoutCost: number; composition: boolean
}

/** A medical shop's month: GST to pay after input credit, profit after expiry, and cash vs digital receipts. */
export function PharmacyMonthScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const now = new Date()
  const [month, setMonth] = useState(`${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`)
  const m = useQuery({ queryKey: [shopId, 'pharmacy-month', month], queryFn: () => api<Month>(`/v1/pharmacy/month?month=${month}`) }).data
  const row = (label: string, value: number, strong = false, tone = '') => (
    <p className={`flex justify-between ${strong ? 'font-bold' : 'text-sm'} ${tone}`}><span>{label}</span><span>{formatRupees(value)}</span></p>
  )
  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <Field label={t('pmonth.month')} type="month" value={month} onChange={(e) => setMonth(e.target.value)} />
      {m && (
        <>
          <section className="flex flex-col gap-1 rounded-card bg-white p-3">
            <h3 className="font-bold">{t('pmonth.gst')}</h3>
            {row(t('pmonth.outputGst'), m.outputGst)}
            {row(t('pmonth.inputGst'), m.inputGst)}
            {row(t('pmonth.payable'), m.gstPayable, true, m.gstPayable > 0 ? 'text-due' : '')}
            {m.creditCarried > 0 && row(t('pmonth.carried'), m.creditCarried, false, 'text-brand-strong')}
            <p className="text-xs text-muted">{t(m.composition ? 'pmonth.compositionHint' : 'pmonth.gstHint')}</p>
          </section>
          <section className="flex flex-col gap-1 rounded-card bg-white p-3">
            <h3 className="font-bold">{t('pmonth.profit')}</h3>
            {row(t('pmonth.sales'), m.sales)}
            {row(t('pmonth.salesTaxable'), m.salesTaxable)}
            {row(t('pmonth.cost'), m.costOfSales)}
            {row(t('pmonth.gross', { percent: m.marginPercent }), m.grossProfit, true)}
            {row(t('pmonth.expiryLoss'), m.expiryLoss, false, m.expiryLoss > 0 ? 'text-due' : '')}
            {row(t('pmonth.afterExpiry'), m.profitAfterExpiry, true)}
            {m.expiredOnShelf > 0 && <p className="rounded-xl bg-due-soft p-2 text-sm text-due">{t('pmonth.expiredOnShelf', { value: formatRupees(m.expiredOnShelf) })}</p>}
            {m.linesWithoutCost > 0 && <p className="rounded-xl bg-warn-soft p-2 text-sm">{t('pmonth.noCost', { count: m.linesWithoutCost })}</p>}
            {row(t('pmonth.purchases'), m.purchases)}
          </section>
          <section className="flex flex-col gap-1 rounded-card bg-white p-3">
            <h3 className="font-bold">{t('pmonth.receipts')}</h3>
            {row(t('cash'), m.cashReceipts)}
            {row(t('pmonth.digital'), m.digitalReceipts)}
            <p className="text-xs text-muted">{t('pmonth.receiptsHint')}</p>
          </section>
        </>
      )}
    </div>
  )
}

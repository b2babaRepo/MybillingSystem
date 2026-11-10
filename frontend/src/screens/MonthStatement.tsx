import { useTranslation } from 'react-i18next'
import { formatRupees } from '../lib/format'
import type { MonthStatement } from '../lib/reports'

/**
 * The month on one page, the way an accountant reads it: sales less GST and the goods' cost gives gross profit; less
 * each running cost gives net profit. Money out that is not a cost comes after, apart, so it never looks like a loss.
 */
export function MonthStatementCard({ st }: { st: MonthStatement }) {
  const { t } = useTranslation()
  const Row = ({ label, amount, minus = false, strong = false, note }: { label: string; amount: number; minus?: boolean; strong?: boolean; note?: string }) => (
    <div className={`flex justify-between gap-2 py-1 ${strong ? 'border-t border-[#d8e2df] font-extrabold' : ''}`}>
      <span className={strong ? '' : 'text-muted'}>{label}{note && <span className="block text-xs font-normal text-amber-800">{note}</span>}</span>
      <span className="shrink-0">{minus && amount !== 0 ? '−' : ''}{formatRupees(Math.abs(amount))}</span>
    </div>
  )
  return (
    <div className="flex flex-col text-sm">
      <span className="text-sm font-semibold text-muted">{t('statement.title')}</span>
      <Row label={t('statement.salesWithGst')} amount={st.salesWithGst} />
      {st.roundOff !== 0 && <Row label={t('statement.roundOff')} amount={st.roundOff} minus />}
      {st.gstInSales !== 0 && <Row label={t('statement.gst')} amount={st.gstInSales} minus />}
      <Row label={t('statement.sales')} amount={st.sales} strong />
      {st.noCostSales !== 0 && <Row label={t('statement.noCost')} amount={st.noCostSales} minus note={t('statement.noCostNote')} />}
      <Row label={t('statement.cost')} amount={st.costOfGoods} minus />
      {st.returns !== 0 && <Row label={t('statement.returns')} amount={st.returns} minus />}
      {st.stockLoss !== 0 && <Row label={t('statement.stockLoss')} amount={st.stockLoss} minus />}
      <Row label={t('statement.gross')} amount={st.grossProfit} strong />
      {st.running.map((x) => <Row key={x.category} label={t(`expense.cat.${x.category}`)} amount={x.amount} minus />)}
      {st.running.length === 0 && <p className="py-1 text-xs text-muted">{t('statement.noKharcha')}</p>}
      {st.running.length > 0 && <Row label={t('statement.expenses')} amount={st.expenses} minus />}
      <div className={`mt-1 flex justify-between gap-2 border-t-2 border-black pt-2 text-base font-extrabold ${st.netProfit < 0 ? 'text-due' : 'text-jama'}`}>
        <span>{st.netProfit < 0 ? t('statement.netLoss') : t('statement.net')}</span>
        <span>{formatRupees(Math.abs(st.netProfit))}</span>
      </div>
      {st.notInProfit.length > 0 && (
        <div className="mt-3 rounded-xl bg-ground p-3">
          <span className="text-xs font-semibold text-muted">{t('statement.notInProfit')}</span>
          {st.notInProfit.map((x) => (
            <div key={x.category} className="flex justify-between gap-2 py-0.5"><span>{t(`expense.cat.${x.category}`)}</span><span>{formatRupees(x.amount)}</span></div>
          ))}
        </div>
      )}
    </div>
  )
}

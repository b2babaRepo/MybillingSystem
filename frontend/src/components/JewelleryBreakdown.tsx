import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { karat, type JewelleryBill } from '../lib/jewellery'

/**
 * On a jewellery bill: every piece's weights, purity, rate, wastage, making, stones and hallmark charge, the old metal
 * taken in, and the BIS lines (purity can be checked at any BIS-recognised centre). Shows nothing on other bills.
 */
export function JewelleryBreakdown({ billId, enabled }: { billId: string; enabled: boolean }) {
  const { t } = useTranslation()
  const bill = useQuery({ queryKey: ['jewellery-bill', billId], queryFn: () => api<JewelleryBill>(`/v1/jewellery/bills/${billId}`), enabled, retry: false, staleTime: Infinity }).data
  if (!bill) return null
  const cell = 'border border-gray-300 px-1.5 py-1'
  return (
    <div className="mt-3 flex flex-col gap-2 text-[12px]">
      <table className="w-full border-collapse">
        <thead className="bg-gray-100 text-left">
          <tr>
            <th className={cell}>{t('jwl.item')}</th><th className={cell}>{t('jwl.purity')}</th><th className={`${cell} text-right`}>{t('jwl.gross')}</th>
            <th className={`${cell} text-right`}>{t('jwl.stoneWt')}</th><th className={`${cell} text-right`}>{t('jwl.net')}</th><th className={`${cell} text-right`}>{t('jwl.ratePerG')}</th>
            <th className={`${cell} text-right`}>{t('jwl.metalValue')}</th><th className={`${cell} text-right`}>{t('jwl.wastageShortHead')}</th><th className={`${cell} text-right`}>{t('jwl.making')}</th>
            <th className={`${cell} text-right`}>{t('jwl.stoneValueShort')}</th><th className={`${cell} text-right`}>{t('jwl.taxable')}</th>
          </tr>
        </thead>
        <tbody>
          {bill.lines.map((l) => (
            <tr key={l.lineNo}>
              <td className={cell}>{l.name}{l.tagNo ? ` (J-${l.tagNo})` : ''}{l.huid ? <span className="block">HUID {l.huid}</span> : null}</td>
              <td className={cell}>{l.metal === 'gold' ? `${karat(l.metal, l.purity)} (${l.purity})` : `${t('jwl.silver')} ${l.purity}`}</td>
              <td className={`${cell} text-right`}>{l.gross.toFixed(3)}</td>
              <td className={`${cell} text-right`}>{l.stone ? l.stone.toFixed(3) : '—'}</td>
              <td className={`${cell} text-right font-bold`}>{l.net.toFixed(3)}</td>
              <td className={`${cell} text-right`}>{l.fixedPrice ? '—' : formatRupees(l.ratePerGram)}</td>
              <td className={`${cell} text-right`}>{l.fixedPrice ? '—' : formatRupees(l.metalValue)}</td>
              <td className={`${cell} text-right`}>{l.wastageValue ? `${formatRupees(l.wastageValue)} (${l.wastagePercent}%)` : '—'}</td>
              <td className={`${cell} text-right`}>{l.makingAmount ? formatRupees(l.makingAmount) : '—'}</td>
              <td className={`${cell} text-right`}>{l.stoneValue + l.hallmarkCharge ? formatRupees(l.stoneValue + l.hallmarkCharge) : '—'}</td>
              <td className={`${cell} text-right font-bold`}>{formatRupees(l.taxable)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {bill.lines.some((l) => l.hallmarkCharge > 0) && <p>{t('jwl.hallmarkIncluded')}</p>}
      {bill.oldMetal.length > 0 && (
        <div className="rounded-md border border-gray-300 p-2">
          <b>{t('jwl.oldMetalTaken')}</b>
          {bill.oldMetal.map((o, i) => (
            <p key={i} className="flex justify-between gap-2">
              <span>{o.description ?? (o.metal === 'gold' ? t('jwl.gold') : t('jwl.silver'))}: {o.gross.toFixed(3)} g × {o.reading}% = {o.fine.toFixed(3)} g {t('jwl.fineShort')} × {formatRupees(o.ratePerGram)}{o.deductionPercent ? ` − ${o.deductionPercent}%` : ''}</span>
              <b>{formatRupees(o.value)}</b>
            </p>
          ))}
        </div>
      )}
      {bill.pan && <p>PAN: {bill.pan}</p>}
      <p className="text-gray-600">{t('jwl.bisNote')}</p>
    </div>
  )
}

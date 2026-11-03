import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatRupees } from '../lib/format'
import { exportPrice, landedCost } from '../lib/tradeCosting'

type Values = Record<string, string>

/** Two calculators for a workshop that exports furniture or imports fittings: worked out on the phone, nothing saved. */
export function ExportImportPanel() {
  const { t } = useTranslation()
  const [ex, setEx] = useState<Values>({ goodsCost: '', marginPercent: '25', packing: '', inlandFreight: '', portCharges: '', oceanFreight: '', insurancePercent: '0.5', exchangeRate: '85', rodtepPercent: '0.7' })
  const [im, setIm] = useState<Values>({ priceForeign: '', freightForeign: '', exchangeRate: '85', basicDutyPercent: '15', surchargePercent: '10', igstPercent: '18', clearing: '' })
  const num = (v: Values) => Object.fromEntries(Object.entries(v).map(([k, x]) => [k, Number(x) || 0]))
  const p = exportPrice(num(ex) as unknown as Parameters<typeof exportPrice>[0])
  const c = landedCost(num(im) as unknown as Parameters<typeof landedCost>[0])
  const usd = (n: number) => `US$ ${n.toLocaleString('en-IN', { maximumFractionDigits: 2 })}`

  const input = (values: Values, set: (v: Values) => void, key: string, label: string) => (
    <label key={key} className="block">
      <span className="mb-1 block text-xs font-semibold text-muted">{label}</span>
      <input value={values[key]} onChange={(e) => set({ ...values, [key]: e.target.value })} inputMode="decimal" className="h-11 w-full rounded-xl border border-line bg-white px-3" />
    </label>
  )
  const line = (label: string, value: string, strong = false) => (
    <div className={`flex justify-between ${strong ? 'font-bold' : ''}`}><span>{label}</span><span>{value}</span></div>
  )

  return (
    <div className="flex flex-col gap-3">
      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
        <h3 className="font-bold">{t('workshop.trade.exportTitle')}</h3>
        <p className="text-xs text-muted">{t('workshop.trade.exportHint')}</p>
        <div className="grid grid-cols-2 gap-2">
          {(['goodsCost', 'marginPercent', 'packing', 'inlandFreight', 'portCharges', 'oceanFreight', 'insurancePercent', 'exchangeRate', 'rodtepPercent'] as const).map((k) =>
            input(ex, setEx, k, t(`workshop.trade.${({ goodsCost: 'goodsCost', marginPercent: 'margin', packing: 'packing', inlandFreight: 'inland', portCharges: 'port', oceanFreight: 'ocean', insurancePercent: 'insurance', exchangeRate: 'rate', rodtepPercent: 'rodtepRate' } as const)[k]}`)))}
        </div>
        <div className="mt-1 flex flex-col gap-1 text-sm">
          {line(t('workshop.trade.exWorks'), formatRupees(p.exWorks))}
          {line(t('workshop.trade.fob'), `${formatRupees(p.fob)} · ${usd(p.fobForeign)}`, true)}
          {line(t('workshop.trade.insuranceAmount'), formatRupees(p.insurance))}
          {line(t('workshop.trade.cif'), `${formatRupees(p.cif)} · ${usd(p.cifForeign)}`, true)}
          {line(t('workshop.trade.rodtep'), formatRupees(p.rodtep))}
        </div>
      </section>

      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
        <h3 className="font-bold">{t('workshop.trade.importTitle')}</h3>
        <p className="text-xs text-muted">{t('workshop.trade.importHint')}</p>
        <div className="grid grid-cols-2 gap-2">
          {(['priceForeign', 'freightForeign', 'exchangeRate', 'basicDutyPercent', 'surchargePercent', 'igstPercent', 'clearing'] as const).map((k) =>
            input(im, setIm, k, t(`workshop.trade.${({ priceForeign: 'price', freightForeign: 'freight', exchangeRate: 'rate', basicDutyPercent: 'duty', surchargePercent: 'surcharge', igstPercent: 'igst', clearing: 'clearing' } as const)[k]}`)))}
        </div>
        <div className="mt-1 flex flex-col gap-1 text-sm">
          {line(t('workshop.trade.cifRs'), formatRupees(c.cif))}
          {line(t('workshop.trade.dutyAmount'), formatRupees(c.basicDuty))}
          {line(t('workshop.trade.surchargeAmount'), formatRupees(c.surcharge))}
          {line(t('workshop.trade.igstAmount'), formatRupees(c.igst))}
          {line(t('workshop.trade.totalPaid'), formatRupees(c.totalPaid), true)}
          {line(t('workshop.trade.landed'), formatRupees(c.landedCost), true)}
        </div>
      </section>
      <p className="text-xs text-muted">{t('workshop.trade.check')}</p>
    </div>
  )
}

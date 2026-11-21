import { useTranslation } from 'react-i18next'
import { formatDateTime, formatRupees } from '../lib/format'

/** A jewellery shop's selling rate per gram, before GST. */
export interface StoreRate { metal: string; purity: number; perGram: number; setAt: string }

const label = (r: StoreRate, silver: string) => (r.metal === 'silver' ? silver : r.purity === 999 ? '24K' : r.purity === 916 ? '22K' : r.purity === 750 ? '18K' : `${r.purity}`)

/** Today's gold and silver rates, as a jewellery customer checks them before coming in. compact: one line for a card. */
export function MetalRates({ rates, compact = false }: { rates: StoreRate[]; compact?: boolean }) {
  const { t } = useTranslation()
  if (rates.length === 0) return null
  const latest = rates.reduce((a, r) => (r.setAt > a ? r.setAt : a), rates[0].setAt)
  if (compact) {
    return (
      <span className="truncate text-xs font-semibold text-amber-800">
        {rates.filter((r) => r.metal === 'gold').slice(0, 2).map((r) => `${label(r, t('cust5.silver'))} ${formatRupees(r.perGram)}`).join(' · ')}{t('cust5.perGram')}
      </span>
    )
  }
  return (
    <section aria-label={t('cust5.todayRate')} className="mx-4 mt-3 flex flex-col gap-2 rounded-2xl border border-amber-200 bg-[#fff8e8] p-3">
      <div className="flex items-baseline justify-between gap-2">
        <h2 className="font-extrabold text-amber-950">{t('cust5.todayRate')}</h2>
        <span className="text-xs text-amber-900">{t('cust5.rateSet', { time: formatDateTime(latest) })}</span>
      </div>
      <div className="grid gap-2" style={{ gridTemplateColumns: `repeat(${Math.min(rates.length, 4)}, minmax(0, 1fr))` }}>
        {rates.map((r) => (
          <span key={`${r.metal}-${r.purity}`} className="flex flex-col rounded-xl bg-white px-2 py-1.5">
            <span className="text-xs font-bold text-amber-800">{label(r, t('cust5.silver'))}</span>
            <b className="text-[15px] tabular-nums">{formatRupees(r.perGram)}</b>
          </span>
        ))}
      </div>
      <span className="text-[11px] text-amber-900">{t('cust5.rateNote')}</span>
    </section>
  )
}

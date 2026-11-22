import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { shownLocale } from '../i18n'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { unitProfit } from '../lib/profit'

interface PastPrice { price: number; quantity: number; at: string; reference: string | null }
interface LastPricesDto { customer: PastPrice[]; anyone: PastPrice[]; bought: PastPrice[] | null }


/**
 * Under a bill line: for the owner, the profit on it ("Profit ₹25 a bag, 6.5%"), and for everyone a "Last prices" button:
 * what this customer paid before, the item's last prices to anyone and, for the owner, what it last cost from suppliers.
 * Tapping a price puts it on the line.
 */
export function LinePriceHints({ productId, customerId, isOwner, price, cost, gstRate, unitLabel, onPick }: {
  productId: string; customerId: string; isOwner: boolean; price: number; cost: number | null; gstRate: number | null; unitLabel: string; onPick: (price: number) => void
}) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const past = useQuery({
    queryKey: ['last-prices', productId, customerId],
    queryFn: () => api<LastPricesDto>(`/v1/products/${productId}/last-prices${customerId ? `?customerId=${customerId}` : ''}`),
    enabled: open, staleTime: 60_000, retry: false,
  })
  const profit = isOwner && cost != null && cost > 0 && price > 0 ? unitProfit(price, cost, gstRate) : null
  const day = (iso: string) => new Date(iso).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })

  const row = (title: string, list: PastPrice[], pick: boolean) => list.length === 0 ? null : (
    <div className="flex flex-wrap items-center gap-1">
      <span className="text-xs font-semibold text-muted">{title}:</span>
      {list.map((p, i) => pick ? (
        <button key={i} type="button" onClick={() => onPick(p.price)} title={p.reference ?? ''}
          className="min-h-8 rounded-full border border-line bg-white px-2 text-xs font-semibold hover:border-brand">{formatRupees(p.price)} <span className="font-normal text-muted">{day(p.at)}</span></button>
      ) : (
        <span key={i} title={p.reference ?? ''} className="rounded-full bg-[#edf2f1] px-2 py-1 text-xs font-semibold">{formatRupees(p.price)} <span className="font-normal text-muted">{day(p.at)}{p.reference ? ` · ${p.reference}` : ''}</span></span>
      ))}
    </div>
  )

  return (
    <div className="mt-1 flex flex-col gap-1">
      <div className="flex flex-wrap items-center gap-2">
        {/* Under 5% the line already warns of a thin margin or a loss. */}
        {profit && profit.percent >= 5 && (
          <span className="text-xs font-semibold text-emerald-700">{t('lastPrices.profit', { amount: formatRupees(profit.amount), unit: unitLabel, percent: profit.percent })}</span>
        )}
        <button type="button" onClick={() => setOpen(!open)} aria-expanded={open} className="min-h-8 text-xs font-semibold text-brand-strong underline">🕘 {t('lastPrices.button')}</button>
      </div>
      {open && (
        <div className="flex flex-col gap-1 rounded-lg bg-[#f6faf9] p-2">
          {past.isPending ? <span className="text-xs text-muted">…</span> : past.isError ? <span className="text-xs text-due">{t('error')}</span> : (
            <>
              {customerId && past.data.customer.length === 0 && <span className="text-xs text-muted">{t('lastPrices.firstTime')}</span>}
              {row(t('lastPrices.thisCustomer'), past.data.customer, true)}
              {row(t('lastPrices.anyone'), past.data.anyone, true)}
              {row(t('lastPrices.bought'), past.data.bought ?? [], false)}
              {past.data.anyone.length === 0 && !(past.data.bought?.length) && <span className="text-xs text-muted">{t('lastPrices.none')}</span>}
            </>
          )}
        </div>
      )}
    </div>
  )
}

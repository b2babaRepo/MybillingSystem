import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'

const LIMIT = 200_000
const NEAR = 180_000

/**
 * Warns before the shop takes ₹2 lakh or more in cash from one person in a day (or on one bill): the income-tax law
 * fines the full amount. With a customer, it adds what they already paid in cash today. A warning only: nothing is blocked.
 */
export function CashLimitNote({ customerId, cash, day }: { customerId: string | null; cash: number; day?: string }) {
  const { t } = useTranslation()
  // Only worth a look once the cash is large.
  const look = !!customerId && cash >= 20_000
  const today = useQuery({
    queryKey: ['cash-today', customerId, day],
    queryFn: () => api<{ cash: number }>(`/v1/customers/${customerId}/cash-today${day ? `?day=${day}` : ''}`),
    enabled: look,
    staleTime: 30_000,
  })
  const before = look ? today.data?.cash ?? 0 : 0
  const total = before + cash
  if (cash <= 0 || total < NEAR) return null
  const over = total >= LIMIT
  return (
    <p role="status" className={`rounded-xl p-3 text-sm ${over ? 'bg-red-50 text-red-800' : 'bg-amber-50 text-amber-800'}`}>
      {t(over ? 'cashLimit.over' : 'cashLimit.near', { total: formatRupees(total), before: formatRupees(before) })}
      {before > 0 && <> {t('cashLimit.already', { before: formatRupees(before) })}</>}
    </p>
  )
}

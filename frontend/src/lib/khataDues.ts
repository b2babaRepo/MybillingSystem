import { useQuery } from '@tanstack/react-query'
import i18n from 'i18next'
import { shownLocale } from '../i18n'
import { api } from './api'
import { formatRupees } from './format'
import { indiaDay } from './deliveries'

/** When a confirmed khata's udhaar falls due, from the shop's credit days for this customer. */
export interface KhataDue { shopId: string; shopName: string; customerId: string; balance: number; creditDays: number; dueOn: string | null; dueAmount: number; overdue: number }

export const useKhataDues = (enabled = true) =>
  useQuery({ queryKey: ['me', 'khata-dues'], queryFn: () => api<KhataDue[]>('/v1/me/khata-dues'), enabled, retry: false, staleTime: 5 * 60_000 })

/** "₹300 overdue", "₹300 due today", "₹300 due tomorrow" or "Pay ₹300 by 5 Dec", with how urgent it is. */
export function dueText(d: KhataDue): { text: string; urgent: boolean } | null {
  const t = i18n.t.bind(i18n)
  if (d.overdue > 0) return { text: t('cust8.overdue', { amount: formatRupees(d.overdue) }), urgent: true }
  if (!d.dueOn) return null
  if (d.dueOn === indiaDay(0)) return { text: t('cust8.dueToday', { amount: formatRupees(d.dueAmount) }), urgent: true }
  if (d.dueOn === indiaDay(1)) return { text: t('cust8.dueTomorrow', { amount: formatRupees(d.dueAmount) }), urgent: true }
  const day = new Date(`${d.dueOn}T00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })
  return { text: t('cust8.payBy', { amount: formatRupees(d.dueAmount), day }), urgent: false }
}

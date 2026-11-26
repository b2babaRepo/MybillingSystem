import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import { indiaToday } from './format'
import type { ExpenseMonth } from '../screens/KharchaMonth'

/** "2026-10" ± months. */
export function shiftMonth(month: string, by: number) {
  const [y, m] = month.split('-').map(Number)
  const d = new Date(Date.UTC(y, m - 1 + by, 1))
  return `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}`
}

/** The India-time month now, "2026-10". */
export const thisMonth = () => indiaToday().slice(0, 7)

export function useExpenseMonth(shopId: string, month: string) {
  return useQuery({ queryKey: [shopId, 'expenses', 'month', month], queryFn: () => api<ExpenseMonth>(`/v1/expenses/month?month=${month}`) })
}

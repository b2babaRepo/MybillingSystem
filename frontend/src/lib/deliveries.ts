import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import type { Unit } from './types'

// Daily delivery plans (milk, water jars, bread): a customer's standing order at a shop, with pause and stop.

export interface DeliveryPlan {
  id: string; shopId: string; shopName: string; shopSlug: string | null; productId: string; productName: string; unit: Unit; quantity: number
  days: number; startOn: string; pauseFrom: string | null; pauseTo: string | null; address: string | null; nextOn: string | null
}

export const EVERY_DAY = 127
/** Shop kinds that deliver every day. */
export const DELIVERY_TRADES = new Set(['dairy', 'water_supply', 'kirana'])

export const useDeliveryPlans = (enabled = true) =>
  useQuery({ queryKey: ['me', 'deliveries'], queryFn: () => api<DeliveryPlan[]>('/v1/me/deliveries'), enabled, retry: false })

export const startPlan = (shopId: string, plan: { id: string; productId: string; quantity: number; days: number; startOn: string; address: string }) =>
  api(`/v1/me/deliveries/${shopId}`, 'POST', plan)

export const pausePlan = (shopId: string, id: string, from: string | null, to: string | null) =>
  api(`/v1/me/deliveries/${shopId}/${id}/pause`, 'POST', { from, to })

export const stopPlan = (shopId: string, id: string) => api(`/v1/me/deliveries/${shopId}/${id}/stop`, 'POST', {})

/** India's date, n days from today, as yyyy-mm-dd. */
export function indiaDay(n = 0) {
  const d = new Date(Date.now() + (330 + new Date().getTimezoneOffset()) * 60_000)
  d.setDate(d.getDate() + n)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

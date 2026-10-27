import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import type { Unit } from './types'

export interface Due {
  customerId: string
  name: string
  phone: string | null
  balance: number
  days0To7: number
  days8To30: number
  over30: number
  oldestDays: number
  lastPaymentAt: string | null
  lastRemindedAt: string | null
}

export interface DuesReport {
  total: number
  days0To7: number
  days8To30: number
  over30: number
  customers: Due[]
}

export interface MonthReport {
  month: string
  sales: number
  bills: number
  cash: number
  upi: number
  udhaarGiven: number
  khataCollected: number
  outstanding: number
  estimatedProfit: number
  profitCoverage: number
  topItems: { name: string; quantity: number; unit: Unit; amount: number }[]
  days: { date: string; sales: number; received: number }[]
}

export interface ShopSettings {
  name: string
  slug: string
  area: string | null
  address: string | null
  upiId: string | null
  reminderTemplate: string | null
}

export function useShopSettings(shopId: string) {
  return useQuery({ queryKey: [shopId, 'settings'], queryFn: () => api<ShopSettings>('/v1/shop'), staleTime: 5 * 60_000 })
}

/** The app's wording when the shop hasn't written its own: polite Hinglish, the way shopkeepers write on WhatsApp. */
export const DEFAULT_REMINDER =
  'Namaste {name} ji, {shop} mein aapka ₹{amount} baaki hai. Suvidha anusaar jama kar dein. Dhanyavaad!'

/**
 * The reminder for one customer: the shop's template with {name}, {amount}, {shop} and {upi} filled in. When the
 * shop has a UPI ID but the template doesn't mention it, a "Pay by UPI" line is added, so customers can pay at once.
 */
export function buildReminder(template: string | null | undefined, values: { name: string; amount: number; shop: string; upi: string | null }) {
  const text = (template || DEFAULT_REMINDER)
    .replaceAll('{name}', values.name)
    .replaceAll('{amount}', values.amount.toLocaleString('en-IN', { maximumFractionDigits: 2 }))
    .replaceAll('{shop}', values.shop)
    .replaceAll('{upi}', values.upi ?? '')
  return values.upi && !(template ?? '').includes('{upi}') ? `${text}\nUPI: ${values.upi}` : text
}

/** Downloads an owner-only CSV with the signed-in token (a plain link would have none). */
export async function downloadCsv(path: string, fileName: string) {
  const { request } = await import('./api')
  const response = await request(path)
  if (!response.ok) throw new Error(`Download failed (${response.status})`)
  const url = URL.createObjectURL(await response.blob())
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  link.click()
  URL.revokeObjectURL(url)
}

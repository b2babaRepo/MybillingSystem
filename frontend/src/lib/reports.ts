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
  returns?: number
  stockLoss?: number
  stockValue?: number
  supplierDue?: number
  /** Running costs from the kharcha book (not stock bought, not money taken home). */
  expenses?: number
  stockPurchases?: number
  takenHome?: number
  netProfit?: number
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
  /** The trade: switches on its starter items, units and bill buttons. */
  type?: ShopType
  /** Printed on tax bills. */
  gstin?: string | null
  gstComposition?: boolean
  extraTypes?: ShopType[] | null
}

export type ShopType = 'kirana' | 'water_supply' | 'beauty' | 'sweets' | 'toys' | 'clothes' | 'building_materials' | 'paint' | 'hardware' | 'electrical' | 'woodwork' | 'other'
export const SHOP_TYPES: ShopType[] = ['kirana', 'water_supply', 'beauty', 'sweets', 'toys', 'clothes', 'building_materials', 'paint', 'hardware', 'electrical', 'woodwork', 'other']
/** Trades with a starter list of common items. */
export const STARTER_TYPES: ShopType[] = ['kirana', 'water_supply', 'beauty', 'sweets', 'toys', 'clothes', 'building_materials', 'paint', 'hardware', 'electrical', 'woodwork']
const TRADE_TYPES: ShopType[] = ['building_materials', 'paint', 'hardware', 'electrical', 'woodwork']
/** Trades where a fitter (painter, electrician, carpenter) brings customers for a commission. */
export const COMMISSION_TYPES: ShopType[] = ['paint', 'electrical', 'woodwork']

/** The shop's trades: its main type and any others it works in. */
export function shopTypes(s?: { type?: ShopType; extraTypes?: ShopType[] | null }): ShopType[] {
  return [s?.type ?? 'kirana', ...(s?.extraTypes ?? [])]
}

/** Building material, paint or hardware: site, challan and rates screens. */
export const isTrade = (types: ShopType[]) => types.some((x) => TRADE_TYPES.includes(x))

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
/** The pay page a reminder links to: opens the customer's UPI app with the shop's ID and the amount filled in. */
export const payLink = (slug: string, amount: number) => `${window.location.origin}/#/pay/${encodeURIComponent(slug)}/${amount.toFixed(2)}`

/** A upi:// address any UPI app opens (GPay, PhonePe, Paytm, BHIM), with the payee, amount and a note set. */
export const upiUri = (upiId: string, name: string, amount: number, note: string) =>
  `upi://pay?pa=${encodeURIComponent(upiId)}&pn=${encodeURIComponent(name)}&am=${amount.toFixed(2)}&cu=INR&tn=${encodeURIComponent(note)}`

export function buildReminder(template: string | null | undefined, values: { name: string; amount: number; shop: string; upi: string | null; slug?: string }) {
  const text = (template || DEFAULT_REMINDER)
    .replaceAll('{name}', values.name)
    .replaceAll('{amount}', values.amount.toLocaleString('en-IN', { maximumFractionDigits: 2 }))
    .replaceAll('{shop}', values.shop)
    .replaceAll('{upi}', values.upi ?? '')
  const withUpi = values.upi && !(template ?? '').includes('{upi}') ? `${text}\nUPI: ${values.upi}` : text
  // A tap-to-pay link: WhatsApp doesn't open upi:// links, so it goes through the shop's pay page.
  return values.upi && values.slug && values.amount > 0 ? `${withUpi}\n${payLink(values.slug, values.amount)}` : withUpi
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

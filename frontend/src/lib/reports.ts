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
  /** Money seen lately may be theirs: check before chasing. */
  moneyWaiting?: boolean
  /** over30 split as accounts read it. */
  days31To60?: number
  days61To90?: number
  over90?: number
  /** With credit days: udhaar past its due date, and when the oldest unpaid udhaar is (or was) due ("2026-12-01"). */
  creditDays?: number | null
  overdue?: number
  dueOn?: string | null
}

export interface DuesReport {
  total: number
  days0To7: number
  days8To30: number
  over30: number
  customers: Due[]
  days31To60?: number
  days61To90?: number
  over90?: number
  overdue?: number
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
  /** The month on one page (owners only). */
  statement?: MonthStatement | null
  topItems: { name: string; quantity: number; unit: Unit; amount: number }[]
  days: { date: string; sales: number; received: number }[]
  card?: number
  cheque?: number
}

export interface MonthStatement {
  salesWithGst: number
  roundOff: number
  gstInSales: number
  sales: number
  noCostSales: number
  costOfGoods: number
  returns: number
  stockLoss: number
  grossProfit: number
  running: { category: string; amount: number }[]
  expenses: number
  netProfit: number
  bagsSold?: number
  bagsBought?: number
  notInProfit: { category: string; amount: number }[]
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
  /** The most a helper may take off an item's regular price, in percent. */
  staffMaxDiscountPercent?: number
  /** The most a helper may write off at cost in one go (₹). */
  staffMaxWriteOff?: number
  exchangeSlipDays?: number
  exchangeSlipHelperMax?: number
  /** Put before new bill numbers ("MB" → MB/2026-27/0001). Null: none. */
  billPrefix?: string | null
  /** The shop's own extra bill fields ("PO no.", "Doctor"): up to 4 labels. */
  billFields?: string[]
  /** Bills offer delivery. Null: not chosen yet, the trade decides. */
  deliversGoods?: boolean | null
  extraTypes?: ShopType[] | null
}

export type ShopType = 'kirana' | 'water_supply' | 'beauty' | 'sweets' | 'toys' | 'clothes' | 'building_materials' | 'paint' | 'hardware' | 'electrical' | 'woodwork' | 'banquet' | 'restaurant' | 'lodge' | 'medical' | 'dairy' | 'sales_service' | 'jewellery' | 'other'
export const SHOP_TYPES: ShopType[] = ['kirana', 'water_supply', 'beauty', 'sweets', 'toys', 'clothes', 'building_materials', 'paint', 'hardware', 'electrical', 'woodwork', 'banquet', 'restaurant', 'lodge', 'medical', 'dairy', 'sales_service', 'jewellery', 'other']
/** Trades with a starter list of common items. */
export const STARTER_TYPES: ShopType[] = ['kirana', 'water_supply', 'beauty', 'sweets', 'toys', 'clothes', 'building_materials', 'paint', 'hardware', 'electrical', 'woodwork', 'banquet', 'restaurant', 'lodge', 'medical', 'sales_service']
const TRADE_TYPES: ShopType[] = ['building_materials', 'paint', 'hardware', 'electrical', 'woodwork']
/** Trades where a fitter (painter, electrician, carpenter) brings customers for a commission. */
export const COMMISSION_TYPES: ShopType[] = ['paint', 'electrical', 'woodwork']

/** Trades that send goods to a site: the site, vehicle and delivery challan go with their bills. */
export const sendsToSites = (type: ShopType) => type === 'building_materials' || type === 'hardware' || type === 'electrical' || type === 'woodwork'

/** Trades that serve people where they are, so bills never offer delivery. */
export const SERVICE_TYPES: ShopType[] = ['beauty', 'restaurant', 'lodge', 'banquet']

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

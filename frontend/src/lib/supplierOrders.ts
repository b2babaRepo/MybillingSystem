import type { Product } from './types'
import type { PurchaseOrder } from '../screens/SupplierOrders'

/** Kilograms in a quantity of an item: exact for weight units, from its weight per unit otherwise, null when not known. */
export function kgOf(p: Product | undefined, quantity: number): number | null {
  if (!p) return null
  const per: Partial<Record<string, number>> = { kg: 1, g: 0.001, quintal: 100, tonne: 1000 }
  const k = per[p.unit] ?? (p.weightKg && p.weightKg > 0 ? p.weightKg : null)
  return k == null ? null : quantity * k
}

/** Today's date as YYYY-MM-DD in the shop's own day, to compare with an order's expected date. */
export const todayIso = () => new Date(Date.now() - new Date().getTimezoneOffset() * 60_000).toISOString().slice(0, 10)

export const isLate = (o: PurchaseOrder) => o.status === 'open' && o.pending > 0 && !!o.expectedOn && o.expectedOn < todayIso()

/** The message a supplier gets: the shop's order, item by item. */
export function orderText(t: (key: string, values?: Record<string, unknown>) => string, shopName: string, number: number, lines: { name: string; quantity: number; unit?: string; bags?: number | null; packUnit?: string | null }[]) {
  return t('supplier2.orderMessage', { shop: shopName, number, lines: lines.map((l) => `${l.name} × ${l.quantity}${l.unit ? ` ${t(`units.${l.unit}`, { count: l.quantity })}` : ''}${l.bags ? ` (${l.packUnit && l.packUnit !== 'bag' ? `${l.bags} ${t(`units.${l.packUnit}`, { count: l.bags })}` : t('bag.inBags', { count: l.bags })})` : ''}`).join('\n') })
}

import type { Product } from './types'

/**
 * The name for the customer's bill and receipt: the item's own bill name, else "Innerwear" or "Nightwear" for items
 * in those groups (the same rule as the server's BillNames), else null (show the item's name).
 */
export function billNameFor(product: Pick<Product, 'billName' | 'category'> | undefined | null): string | null {
  if (!product) return null
  if (product.billName?.trim()) return product.billName.trim()
  const category = product.category ?? ''
  if (/innerwear/i.test(category)) return 'Innerwear'
  if (/nightwear/i.test(category)) return 'Nightwear'
  return null
}

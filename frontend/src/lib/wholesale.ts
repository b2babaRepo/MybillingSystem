/** What a bill line needs to know about its item's wholesale price. */
export interface WholesaleItem { sellingPrice: number; wholesalePrice?: number | null; wholesaleMinQty?: number | null }

/** The item's price for this quantity: wholesale from its minimum quantity, else the regular price. */
export function priceFor(item: WholesaleItem, quantity: number): number {
  return item.wholesalePrice && item.wholesaleMinQty && quantity >= item.wholesaleMinQty ? item.wholesalePrice : item.sellingPrice
}

/**
 * The line's price after its quantity changed: it moves between the regular and wholesale price as the quantity crosses
 * the minimum, but only while it is at one of them. A price the counter typed itself is left alone.
 */
export function fitPrice(item: WholesaleItem | undefined, quantity: number, current: number): number {
  if (!item?.wholesalePrice || !item.wholesaleMinQty) return current
  if (current !== item.sellingPrice && current !== item.wholesalePrice) return current
  return priceFor(item, quantity)
}

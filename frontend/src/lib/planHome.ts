import type { CatalogueItem, ListItem, ShopEntry } from './planner'

/** Helpers for the customer's shopping lists, shared by the planner screens. */

/** Rough cost of a list from the catalogue's typical prices; items typed by hand or in another unit don't count. */
export function listEstimate(items: ListItem[], catalogue: CatalogueItem[]) {
  const byId = new Map(catalogue.map((c) => [c.id, c]))
  return Math.round(items.reduce((sum, i) => {
    const c = i.catalogueItemId ? byId.get(i.catalogueItemId) : undefined
    return c && c.unit === i.unit ? sum + c.typicalPrice * i.quantity : sum
  }, 0))
}

/** A list item's photo: the master-list photo of its catalogue item, when there is one. */
export function listItemPhoto(item: ListItem, catalogue: CatalogueItem[]) {
  const c = item.catalogueItemId ? catalogue.find((x) => x.id === item.catalogueItemId) : undefined
  return c?.photoMasterId && c.photoAt ? `/api/v1/master-images/${c.photoMasterId}?v=${Date.parse(c.photoAt)}&size=thumb` : null
}

/** The shop the customer is ordering from: kept on this phone until they change or clear it. */
export function getChosenShop(userId: string): ShopEntry | null {
  try {
    const raw = localStorage.getItem(`chosenShop:${userId}`)
    return raw ? (JSON.parse(raw) as ShopEntry) : null
  } catch { return null }
}

export function setChosenShop(userId: string, shop: ShopEntry | null) {
  try {
    if (shop) localStorage.setItem(`chosenShop:${userId}`, JSON.stringify(shop))
    else localStorage.removeItem(`chosenShop:${userId}`)
  } catch { /* storage blocked: the choice lasts until the page closes */ }
}

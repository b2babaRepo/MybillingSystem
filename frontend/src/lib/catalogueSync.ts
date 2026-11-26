import { api, NetworkError } from './api'
import { readCache, writeCache } from './db'
import type { Product } from './types'

/**
 * The item list kept on the device. The first time (and every few hours) it comes whole; in between, the server sends
 * only the items changed since the last look and the ids of those removed. A 20,000-item shop then sends a handful
 * of rows on each refresh instead of the whole list.
 */
interface Kept {
  /** Ask for changes from here next time (the server's clock). */
  at: string
  /** When the whole list last came (this device's clock). */
  fullAt: number
  items: Product[]
}

export interface ProductChanges { at: string; full: boolean; changed: Product[]; removed: string[] }

/** A whole list now and then picks up what changes without touching the item (sold a day, photos from the master list). */
export const FULL_EVERY_MS = 6 * 3_600_000

/** The kept list with the changes in: changed items replace (or join), removed ones go; in the server's order. */
export function applyChanges(items: Product[], changes: ProductChanges): Product[] {
  if (changes.full) return changes.changed
  const byId = new Map(items.map((p) => [p.id, p]))
  for (const p of changes.changed) byId.set(p.id, p)
  for (const id of changes.removed) byId.delete(id)
  // By category (none last), then name, as the server lists them.
  return [...byId.values()].sort((a, b) =>
    a.category === b.category ? (a.name < b.name ? -1 : a.name > b.name ? 1 : 0)
      : a.category === null ? 1 : b.category === null ? -1 : a.category < b.category ? -1 : 1)
}

export async function syncProducts(shopId: string): Promise<Product[]> {
  const key = `${shopId}:products-sync`
  const kept = await readCache<Kept>(key)
  const recent = kept && Date.now() - kept.fullAt < FULL_EVERY_MS
  try {
    const changes = await api<ProductChanges>(`/v1/products/changes${recent ? `?since=${encodeURIComponent(kept.at)}` : ''}`)
    const items = applyChanges(recent ? kept.items : [], changes)
    const next: Kept = { at: changes.at, fullAt: changes.full || !kept ? Date.now() : kept.fullAt, items }
    await writeCache(key, next)
    return items
  } catch (error) {
    if (error instanceof NetworkError) {
      // No internet: the kept list, else the copy older versions of the app kept.
      if (kept) return kept.items
      const old = await readCache<Product[]>(`${shopId}:products`)
      if (old !== undefined) return old
    }
    throw error
  }
}

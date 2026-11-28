import type { Product } from './types'

/**
 * A little stock count every day instead of a big one a year: one category a day, in turn, so every shelf is
 * checked every few weeks (reports/Easy opening stock for new shops.md). Kept on this phone.
 */
const today = () => new Date(Date.now() + 5.5 * 3_600_000).toISOString().slice(0, 10)
const doneKey = (shopId: string) => `gk-count-done:${shopId}`
const pickKey = (shopId: string) => `gk-count-category:${shopId}`

/** The categories that hold counted items, largest first, and today's one in turn by the day of the year. */
export function todaysCategory(products: Product[]): { name: string; items: number } | null {
  const sizes = new Map<string, number>()
  for (const p of products) if (!p.isService && !p.uncounted) sizes.set(p.category?.trim() || '', (sizes.get(p.category?.trim() || '') ?? 0) + 1)
  const list = [...sizes.entries()].filter(([name]) => name !== '').sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))
  if (list.length === 0) return null
  const day = Math.floor(Date.now() / 86_400_000)
  const [name, items] = list[day % list.length]
  return { name, items }
}

export function countDoneToday(shopId: string): boolean {
  try { return localStorage.getItem(doneKey(shopId)) === today() } catch { return false }
}

export function markCountDone(shopId: string) {
  try { localStorage.setItem(doneKey(shopId), today()) } catch { /* storage off */ }
}

/** The category the count screen opens on (set by the home card); read once. */
export function chooseCountCategory(shopId: string, name: string) {
  try { sessionStorage.setItem(pickKey(shopId), name) } catch { /* storage off */ }
}

export function takeCountCategory(shopId: string): string | null {
  try { const v = sessionStorage.getItem(pickKey(shopId)); sessionStorage.removeItem(pickKey(shopId)); return v } catch { return null }
}

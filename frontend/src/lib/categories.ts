import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import type { Unit } from './types'

export interface CategoryRow {
  id: string; name: string; parentId: string | null; sortOrder: number; isActive: boolean; items: number
  defaultGstRate: number | null; defaultHsn: string | null; defaultUnit: Unit | null; targetCoverDays: number | null; slowAfterDays: number | null
}

/** The shop's categories in its own order (one list, used by every screen that picks or filters by category). */
export function useCategories(shopId: string) {
  return useQuery({ queryKey: [shopId, 'categories'], queryFn: () => api<CategoryRow[]>('/v1/categories'), staleTime: 60_000 })
}

/** "  Atta   & RICE " and "atta & rice" are the same category. */
export const sameCategory = (a: string | null | undefined, b: string | null | undefined) =>
  !!a && !!b && a.trim().replace(/\s+/g, ' ').toLowerCase() === b.trim().replace(/\s+/g, ' ').toLowerCase()

/**
 * The category names to offer as filter chips: the shop's order first, then any name an item carries that the list
 * doesn't know yet (an item added offline).
 */
export function orderedNames(rows: CategoryRow[] | undefined, used: (string | null | undefined)[]) {
  const present = new Set(used.filter((c): c is string => !!c))
  const known = (rows ?? []).filter((r) => r.isActive && present.has(r.name)).map((r) => r.name)
  const rest = [...present].filter((c) => !known.includes(c)).sort()
  return [...known, ...rest]
}

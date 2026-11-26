/** Sorting and filtering helpers shared by the desktop tables (column heads in components/TableHead). */

/** A filter box under a column head: small, quiet, the same in every desktop table. */
export const filterBox = 'mt-1 h-8 w-full rounded-md border border-line bg-white px-2 text-xs font-normal normal-case'

/** Sort state for a table: which column, and which way. */
export interface Sort<K extends string> { key: K; up: boolean }

/** Clicking a head sorts by it; clicking it again turns the order round. Names start A–Z, numbers biggest first. */
export function nextSort<K extends string>(s: Sort<K>, key: K, textColumn: boolean): Sort<K> {
  return { key, up: s.key === key ? !s.up : textColumn }
}

/** Compares two cells for sorting: numbers by size, text the way people read it ("Item 2" before "Item 10"). */
export function compareCells(a: string | number | null | undefined, b: string | number | null | undefined) {
  if (a == null && b == null) return 0
  if (a == null) return 1
  if (b == null) return -1
  return typeof a === 'number' && typeof b === 'number' ? a - b : String(a).localeCompare(String(b), undefined, { numeric: true })
}

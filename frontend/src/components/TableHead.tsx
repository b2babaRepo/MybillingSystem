import type { ReactNode } from 'react'

/** A filter box under a column head: small, quiet, the same in every desktop table. */
export const filterBox = 'mt-1 h-8 w-full rounded-md border border-line bg-white px-2 text-xs font-normal normal-case'

/** Sort state for a table: which column, and which way. */
export interface Sort<K extends string> { key: K; up: boolean }

/** Clicking a head sorts by it; clicking it again turns the order round. Names start A–Z, numbers biggest first. */
export function nextSort<K extends string>(s: Sort<K>, key: K, textColumn: boolean): Sort<K> {
  return { key, up: s.key === key ? !s.up : textColumn }
}

/** A column head that sorts, with ▲ ▼ for the column in use and ↕ for the others, and an optional filter under it. */
export function SortHead({ label, active, up, onClick, right = false, sortLabel, children }: {
  label: string; active: boolean; up: boolean; onClick: () => void; right?: boolean
  /** Read out for the button: "Sort by …". */
  sortLabel: string
  /** The column's filter (an input or select with {@link filterBox}). */
  children?: ReactNode
}) {
  return (
    <>
      <button type="button" onClick={onClick} aria-label={sortLabel} className={`flex w-full items-center gap-1 font-bold uppercase ${right ? 'justify-end' : ''}`}>
        {label}<span aria-hidden className={active ? 'text-ink' : 'text-muted'}>{active ? (up ? '▲' : '▼') : '↕'}</span>
      </button>
      {children}
    </>
  )
}

/** Compares two cells for sorting: numbers by size, text the way people read it ("Item 2" before "Item 10"). */
export function compareCells(a: string | number | null | undefined, b: string | number | null | undefined) {
  if (a == null && b == null) return 0
  if (a == null) return 1
  if (b == null) return -1
  return typeof a === 'number' && typeof b === 'number' ? a - b : String(a).localeCompare(String(b), undefined, { numeric: true })
}

import type { ReactNode } from 'react'

/** A column head that sorts, with ▲ ▼ for the column in use and ↕ for the others, and an optional filter under it. */
export function SortHead({ label, active, up, onClick, right = false, sortLabel, children }: {
  label: string; active: boolean; up: boolean; onClick: () => void; right?: boolean
  /** Read out for the button: "Sort by …". */
  sortLabel: string
  /** The column's filter (an input or select with `filterBox` from lib/tableSort). */
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

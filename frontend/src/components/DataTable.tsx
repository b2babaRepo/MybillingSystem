import { useState, type ReactNode } from 'react'

export interface Column<T> {
  key: string
  header: ReactNode
  /** What the cell shows. */
  render: (row: T) => ReactNode
  /** Set to make the column sortable by clicking its header. */
  sortValue?: (row: T) => string | number
  align?: 'left' | 'right' | 'center'
  /** Tailwind width class, e.g. 'w-32'. */
  width?: string
}

/**
 * Desktop (1024px+): a dense table with sortable headers and an optional footer row.
 * Phone: the caller's card for each row (renderCard), or the same table if none is given.
 */
export function DataTable<T>({ columns, rows, rowKey, onRowClick, renderCard, footer, empty, label, selectedKey }: {
  columns: Column<T>[]
  rows: T[]
  rowKey: (row: T) => string
  onRowClick?: (row: T) => void
  renderCard?: (row: T) => ReactNode
  footer?: ReactNode[]
  empty?: ReactNode
  label?: string
  selectedKey?: string | null
}) {
  const [sort, setSort] = useState<{ key: string; dir: 1 | -1 } | null>(null)
  const sortColumn = sort && columns.find((c) => c.key === sort.key)
  const sorted = sortColumn?.sortValue
    ? [...rows].sort((a, b) => {
        const x = sortColumn.sortValue!(a), y = sortColumn.sortValue!(b)
        return (typeof x === 'number' && typeof y === 'number' ? x - y : String(x).localeCompare(String(y))) * sort!.dir
      })
    : rows
  if (rows.length === 0 && empty) return <>{empty}</>
  const alignClass = (a?: string) => (a === 'right' ? 'text-right' : a === 'center' ? 'text-center' : 'text-left')

  return (
    <>
      {renderCard && <div className="lg:hidden">{sorted.map((row) => <div key={rowKey(row)}>{renderCard(row)}</div>)}</div>}
      <div className={`${renderCard ? 'hidden lg:block' : ''} overflow-x-auto rounded-card bg-white`}>
        <table className="w-full text-[15px]" aria-label={label}>
          <thead className="sticky top-0 bg-white">
            <tr className="border-b border-line text-[13px] text-muted">
              {columns.map((c) => (
                <th key={c.key} scope="col" className={`px-3 py-2.5 font-semibold ${alignClass(c.align)} ${c.width ?? ''}`}
                  aria-sort={sort?.key === c.key ? (sort.dir === 1 ? 'ascending' : 'descending') : undefined}>
                  {c.sortValue ? (
                    <button type="button" className="inline-flex items-center gap-1 hover:text-ink"
                      onClick={() => setSort((s) => (s?.key === c.key ? (s.dir === 1 ? { key: c.key, dir: -1 } : null) : { key: c.key, dir: 1 }))}>
                      {c.header}
                      <span aria-hidden className="text-[10px]">{sort?.key === c.key ? (sort.dir === 1 ? '▲' : '▼') : '↕'}</span>
                    </button>
                  ) : c.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {sorted.map((row) => {
              const key = rowKey(row)
              return (
                <tr key={key} onClick={onRowClick && (() => onRowClick(row))}
                  className={`border-b border-[#edf2f1] last:border-0 ${onRowClick ? 'cursor-pointer hover:bg-[#f6faf9]' : ''} ${selectedKey === key ? 'bg-brand-soft' : ''}`}>
                  {columns.map((c) => <td key={c.key} className={`px-3 py-2 ${alignClass(c.align)}`}>{c.render(row)}</td>)}
                </tr>
              )
            })}
          </tbody>
          {footer && (
            <tfoot>
              <tr className="border-t-2 border-line font-bold">
                {footer.map((cell, i) => <td key={i} className={`px-3 py-2.5 ${alignClass(columns[i]?.align)}`}>{cell}</td>)}
              </tr>
            </tfoot>
          )}
        </table>
      </div>
    </>
  )
}

import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { filterBox } from '../lib/tableSort'

/** A column's filter, shown under its head: a text box (matches anywhere) or a menu of fixed choices. */
export type ColumnFilter<T> =
  | { kind: 'text'; value: (row: T) => string; placeholder?: string }
  | { kind: 'select'; value: (row: T) => string; options: { value: string; label: string }[] }

export interface Column<T> {
  key: string
  header: ReactNode
  /** What the cell shows. */
  render: (row: T) => ReactNode
  /** Set to make the column sortable by clicking its header. */
  sortValue?: (row: T) => string | number | null | undefined
  align?: 'left' | 'right' | 'center'
  /** Tailwind width class, e.g. 'w-32'. */
  width?: string
  /** A filter box or menu under the head. */
  filter?: ColumnFilter<T>
  /** The cell in the Excel download; without it the column is left out of the file. */
  csv?: (row: T) => string | number | null | undefined
}

const cell = (v: string | number | null | undefined) => {
  const s = v == null ? '' : String(v)
  return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s
}

/**
 * Desktop (1024px+): a dense table like Sale bills: tinted heads that sort (▲ ▼, ↕ when not in use), a filter under any
 * column that asks for one, "N of M shown" while filtering, and Download for Excel. Phone: the caller's card for each row
 * (renderCard), or the same table if none is given.
 */
export function DataTable<T>({ columns, rows, rowKey, onRowClick, renderCard, footer, empty, label, selectedKey, download, defaultSort }: {
  columns: Column<T>[]
  rows: T[]
  rowKey: (row: T) => string
  onRowClick?: (row: T) => void
  renderCard?: (row: T) => ReactNode
  /** The total row. While a filter is on it is replaced by "N of M shown", since its sums would be for all rows. */
  footer?: ReactNode[]
  empty?: ReactNode
  label?: string
  selectedKey?: string | null
  /** File name (without .csv) for a Download for Excel button; the columns with csv go in it. */
  download?: string
  defaultSort?: { key: string; dir: 1 | -1 }
}) {
  const { t } = useTranslation()
  const [sort, setSort] = useState<{ key: string; dir: 1 | -1 } | null>(defaultSort ?? null)
  const [filters, setFilters] = useState<Record<string, string>>({})
  const filtered = rows.filter((row) => columns.every((c) => {
    const want = filters[c.key]?.trim().toLowerCase()
    if (!want || !c.filter) return true
    const have = c.filter.value(row).toLowerCase()
    return c.filter.kind === 'select' ? have === want : have.includes(want)
  }))
  const filtering = filtered.length !== rows.length || Object.values(filters).some((v) => v.trim())
  const sortColumn = sort && columns.find((c) => c.key === sort.key)
  const sorted = sortColumn?.sortValue
    ? [...filtered].sort((a, b) => {
        const x = sortColumn.sortValue!(a), y = sortColumn.sortValue!(b)
        if (x == null && y == null) return 0
        if (x == null) return 1
        if (y == null) return -1
        return (typeof x === 'number' && typeof y === 'number' ? x - y : String(x).localeCompare(String(y), undefined, { numeric: true })) * sort!.dir
      })
    : filtered
  if (rows.length === 0 && empty) return <>{empty}</>
  const alignClass = (a?: string) => (a === 'right' ? 'text-right' : a === 'center' ? 'text-center' : 'text-left')
  const hasFilters = columns.some((c) => c.filter)

  function save() {
    const cols = columns.filter((c) => c.csv)
    const head = cols.map((c) => (typeof c.header === 'string' ? c.header : c.key))
    const body = sorted.map((row) => cols.map((c) => cell(c.csv!(row))).join(','))
    const a = document.createElement('a')
    a.href = URL.createObjectURL(new Blob(['﻿' + [head.map(cell).join(','), ...body].join('\n')], { type: 'text/csv' }))
    a.download = `${download}.csv`
    a.click()
    setTimeout(() => URL.revokeObjectURL(a.href), 10_000)
  }

  return (
    <>
      {renderCard && <div className="lg:hidden">{sorted.map((row) => <div key={rowKey(row)}>{renderCard(row)}</div>)}</div>}
      <div className={`${renderCard ? 'hidden lg:flex' : 'flex'} flex-col gap-1.5`}>
        {(download || filtering) && (
          <div className="flex items-center justify-between gap-2 px-1 text-sm">
            <span className="text-muted">{filtering ? t('table.shown', { shown: sorted.length, count: rows.length }) : ''}
              {filtering && <button type="button" onClick={() => setFilters({})} className="ml-2 font-semibold text-brand underline">{t('table.clear')}</button>}</span>
            {download && <button type="button" onClick={save} disabled={sorted.length === 0} className="min-h-9 font-semibold text-brand underline disabled:opacity-40">⬇ {t('billList.excel')}</button>}
          </div>
        )}
        <div className="overflow-x-auto rounded-card bg-white shadow-sm">
          <table className="w-full border-collapse text-sm" aria-label={label}>
            <thead className="sticky top-0 bg-brand-soft text-xs tracking-wide">
              <tr className="align-top">
                {columns.map((c) => (
                  <th key={c.key} scope="col" className={`px-3 py-2 font-bold ${alignClass(c.align)} ${c.width ?? ''}`}
                    aria-sort={sort?.key === c.key ? (sort.dir === 1 ? 'ascending' : 'descending') : undefined}>
                    {c.sortValue ? (
                      <button type="button" className={`inline-flex items-center gap-1 uppercase hover:text-brand-strong ${c.align === 'right' ? 'flex-row-reverse' : ''}`}
                        onClick={() => setSort((s) => (s?.key === c.key ? { key: c.key, dir: s.dir === 1 ? -1 : 1 } : { key: c.key, dir: typeof c.sortValue!(rows[0] ?? ({} as T)) === 'number' ? -1 : 1 }))}>
                        <span aria-hidden className={sort?.key === c.key ? 'text-ink' : 'text-muted'}>{sort?.key === c.key ? (sort.dir === 1 ? '▲' : '▼') : '↕'}</span>
                        <span>{c.header}</span>
                      </button>
                    ) : <span className="uppercase">{c.header}</span>}
                    {hasFilters && (c.filter ? (
                      c.filter.kind === 'text'
                        ? <input value={filters[c.key] ?? ''} onChange={(e) => setFilters({ ...filters, [c.key]: e.target.value })} placeholder={c.filter.placeholder}
                            aria-label={t('billList.filterBy', { col: typeof c.header === 'string' ? c.header : c.key })} className={`${filterBox} ${c.align === 'right' ? 'text-right' : ''}`} />
                        : <select value={filters[c.key] ?? ''} onChange={(e) => setFilters({ ...filters, [c.key]: e.target.value })}
                            aria-label={t('billList.filterBy', { col: typeof c.header === 'string' ? c.header : c.key })} className={filterBox}>
                            <option value="">{t('billList.status.all')}</option>
                            {c.filter.options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                          </select>
                    ) : null)}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {sorted.length === 0 && <tr><td colSpan={columns.length} className="px-3 py-4 text-center text-muted">{t('stockTable.noMatch')}</td></tr>}
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
            {footer && !filtering && (
              <tfoot>
                <tr className="border-t-2 border-ink font-bold">
                  {footer.map((cell, i) => <td key={i} className={`px-3 py-2.5 ${alignClass(columns[i]?.align)}`}>{cell}</td>)}
                </tr>
              </tfoot>
            )}
          </table>
        </div>
      </div>
    </>
  )
}

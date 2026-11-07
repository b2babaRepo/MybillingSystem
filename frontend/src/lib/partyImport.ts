import { splitLine } from './catalogue'

export interface PartyRow {
  name: string
  phone: string | null
  balance: number
}

const NAME = /name|party|customer|ग्राहक|नाम/i
const PHONE = /phone|mobile|contact|फ़ोन|फोन|मोबाइल/i
const BALANCE = /balance|due|receivable|outstanding|amount|बकाया|राशि/i
const SIDE = /^(type|dr\s*\/?\s*cr|cr\s*\/?\s*dr)$/i

/**
 * What a cell says is owed: "₹1,200.50", "1200 Dr", "(300)", "-300" and "300 Cr" (an advance, shown below zero).
 * Null when it isn't an amount at all.
 */
export function parseBalance(text: string, side = ''): number | null {
  const cell = text.trim()
  if (!cell) return 0
  const advance = /cr\.?\s*$|advance|jama|जमा/i.test(cell) || /^cr/i.test(side.trim()) || /^\(.*\)$/.test(cell) || cell.startsWith('-')
  const digits = cell.replace(/₹|rs\.?|inr|dr\.?|cr\.?|advance|jama|जमा|[(),\s-]/gi, '')
  if (!/^\d+(\.\d+)?$/.test(digits)) return null
  const value = Number(digits)
  return advance && value !== 0 ? -value : value
}

const looksLikePhone = (cell: string) => /^(\+?91[\s-]?)?\d{10}$/.test(cell.replace(/[\s-]/g, ''))

/**
 * Customers from a paste or a CSV. A header row says which column is which, in any order; with none it is name, then
 * the phone and balance wherever they look like one. Lines it can't read come back in `errors`, by line number.
 */
export function parseParties(text: string): { rows: PartyRow[]; errors: number[] } {
  const lines = text.split(/\r?\n/).filter((l) => l.trim().length > 0)
  const first = lines[0] ? splitLine(lines[0]) : []
  const find = (pattern: RegExp) => first.findIndex((c) => pattern.test(c))
  const hasHeader = first.some((c) => NAME.test(c)) && first.some((c) => PHONE.test(c) || BALANCE.test(c))
  const columns = hasHeader ? { name: find(NAME), phone: find(PHONE), balance: find(BALANCE), side: find(SIDE) } : null

  const rows: PartyRow[] = []
  const errors: number[] = []
  lines.forEach((line, index) => {
    if (hasHeader && index === 0) return
    const cells = splitLine(line)
    const name = (columns ? cells[columns.name] : cells[0])?.trim() ?? ''
    const rest = columns ? [] : cells.slice(1)
    const phone = columns ? cells[columns.phone] ?? '' : rest.find(looksLikePhone) ?? ''
    const money = columns ? cells[columns.balance] ?? '' : rest.find((c) => c !== phone) ?? ''
    const balance = parseBalance(money, columns && columns.side >= 0 ? cells[columns.side] ?? '' : '')
    if (!name || balance === null) {
      errors.push(index + 1)
      return
    }
    rows.push({ name, phone: phone.trim() || null, balance })
  })
  return { rows, errors }
}

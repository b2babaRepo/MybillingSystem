import { splitLine } from './catalogue'

export interface SupplierRow {
  name: string
  phone: string | null
  gstin: string | null
  /** What the shop owes them; below zero when they owe the shop. */
  balance: number
}

const NAME = /name|party|supplier|vendor|सप्लायर|नाम/i
const PHONE = /phone|mobile|contact|फ़ोन|फोन|मोबाइल/i
const GSTIN = /gstin|gst no|gst number/i
const BALANCE = /balance|due|payable|outstanding|amount|बाक़ी|बकाया|राशि/i
const GSTIN_SHAPE = /^\d{2}[A-Z]{5}\d{4}[A-Z][1-9A-Z]Z[0-9A-Z]$/i

/**
 * What a cell says the shop owes a supplier. In books, money owed to a supplier is a credit ("Cr"), so a plain amount
 * or "Cr" is owed by the shop, and "Dr" or a minus means the supplier owes the shop. Null when it isn't an amount.
 */
export function parseOwed(text: string): number | null {
  const cell = text.trim()
  if (!cell) return 0
  const theyOwe = /dr\.?\s*$/i.test(cell) || /^[^\d]*-/.test(cell) || /^\(.*\)$/.test(cell)
  const digits = cell.replace(/₹|rs\.?|inr|dr\.?|cr\.?|[(),\s-]/gi, '')
  if (!/^\d+(\.\d+)?$/.test(digits)) return null
  const value = Number(digits)
  return theyOwe && value !== 0 ? -value : value
}

/**
 * Suppliers from a paste or a CSV (Tally, Vyapar, a notebook). A header row says which column is which, in any order;
 * with none it is the name first, and the phone, GSTIN and amount wherever they look like one. Lines it can't read come
 * back in `errors`, by line number.
 */
export function parseSuppliers(text: string): { rows: SupplierRow[]; errors: number[] } {
  const lines = text.split(/\r?\n/).filter((l) => l.trim().length > 0)
  const first = lines[0] ? splitLine(lines[0]) : []
  const find = (pattern: RegExp) => first.findIndex((c) => pattern.test(c))
  const hasHeader = first.some((c) => NAME.test(c)) && first.some((c) => PHONE.test(c) || GSTIN.test(c) || BALANCE.test(c))
  const columns = hasHeader ? { name: find(NAME), phone: find(PHONE), gstin: find(GSTIN), balance: find(BALANCE) } : null
  const rows: SupplierRow[] = []
  const errors: number[] = []
  lines.forEach((line, i) => {
    if (hasHeader && i === 0) return
    const cells = splitLine(line)
    const at = (index: number) => (index >= 0 ? cells[index] ?? '' : '')
    const name = (columns ? at(columns.name) : cells[0] ?? '').trim()
    const rest = columns ? [] : cells.slice(1)
    const phone = columns ? at(columns.phone) : rest.find((c) => /^(\+?91[\s-]?)?\d{10}$/.test(c.replace(/[\s-]/g, ''))) ?? ''
    const gstin = columns ? at(columns.gstin) : rest.find((c) => GSTIN_SHAPE.test(c.trim())) ?? ''
    const amountCell = columns ? at(columns.balance) : rest.find((c) => c !== phone && c !== gstin && parseOwed(c) !== null && /\d/.test(c)) ?? ''
    const balance = parseOwed(amountCell)
    if (!name || balance === null) {
      errors.push(i + 1)
      return
    }
    rows.push({ name, phone: phone.trim() || null, gstin: gstin.trim().toUpperCase() || null, balance })
  })
  return { rows, errors }
}

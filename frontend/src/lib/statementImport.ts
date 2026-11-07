import { splitLine } from './catalogue'

export interface StatementRow {
  amount: number
  receivedAt: string
  utr: string | null
  payerName: string | null
  /** The bank's own words: never stored, only used to tell one row from another when there's no UTR. */
  narration: string
}

const DATE = /^(txn\.?\s*|tran\.?\s*|transaction\s*|value\s*|posting\s*)?date\b/i
const NARRATION = /narration|description|particulars|remarks|details/i
const CREDIT = /^(credit|deposit|cr\b|credit\s*amount|deposit\s*amt|deposit\s*amount|amount\s*\(?cr\)?)/i
const AMOUNT = /^(transaction\s*)?amount/i
const SIDE = /^(type|dr\s*\/?\s*cr|cr\s*\/?\s*dr|credit\s*\/\s*debit|debit\s*\/\s*credit)/i
const MONTHS = ['jan', 'feb', 'mar', 'apr', 'may', 'jun', 'jul', 'aug', 'sep', 'oct', 'nov', 'dec']

/** "07/11/2026", "07-11-26", "7 Nov 2026", "2026-11-07": Indian banks put the day first. Midnight in India. Null if it isn't a date. */
export function parseDate(text: string): string | null {
  const cell = text.trim()
  const iso = cell.match(/^(\d{4})-(\d{1,2})-(\d{1,2})/)
  const named = cell.match(/^(\d{1,2})[-/\s]([a-z]{3})[a-z]*[-/\s,]*(\d{2,4})/i)
  const dayFirst = cell.match(/^(\d{1,2})[-/.](\d{1,2})[-/.](\d{2,4})/)
  let parts: [number, number, number] | null = null
  if (iso) parts = [Number(iso[1]), Number(iso[2]), Number(iso[3])]
  else if (named && MONTHS.includes(named[2].toLowerCase())) parts = [Number(named[3]), MONTHS.indexOf(named[2].toLowerCase()) + 1, Number(named[1])]
  else if (dayFirst) parts = [Number(dayFirst[3]), Number(dayFirst[2]), Number(dayFirst[1])]
  if (!parts) return null
  const [rawYear, month, day] = parts
  const year = rawYear < 100 ? 2000 + rawYear : rawYear
  if (month < 1 || month > 12 || day < 1 || day > 31) return null
  return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}T00:00:00+05:30`
}

const toAmount = (text: string): number | null => {
  const digits = text.replace(/₹|rs\.?|inr|cr\.?|,|\s/gi, '')
  return /^\d+(\.\d+)?$/.test(digits) && Number(digits) > 0 ? Number(digits) : null
}

/**
 * The UPI reference and the payer's name from a bank's narration. Banks write these differently, for example
 * "UPI/CR/432112345678/RAMESH KUMAR/HDFC/..." and "UPI-RAMESH KUMAR-ramesh@okhdfc-HDFC0001-432112345678-NOTE".
 */
export function readNarration(narration: string): { utr: string | null; payerName: string | null } {
  // Twelve digits on their own, on UPI and IMPS rows only: a phone in a UPI ID (919876543210@ybl) or an account number
  // in a NEFT row looks the same, and treating it as a UTR would make that person's later payments look like repeats.
  const utr = /\b(upi|imps)\b/i.test(narration.replace(/[/-]/g, ' ')) ? narration.match(/(?<![\dA-Za-z@.])\d{12}(?![\dA-Za-z@.])/)?.[0] ?? null : null
  const parts = narration.split(/[/-]/).map((p) => p.trim())
  const looksLikeName = (p: string) => /^[A-Za-z][A-Za-z .]{2,}$/.test(p) && !/^(upi|cr|dr|in|p2a|p2m|payment|from|phone|paid|via|neft|imps|rtgs)$/i.test(p) && !/^[A-Z]{4}\d/.test(p)
  const afterUtr = utr ? parts.slice(parts.indexOf(utr) + 1).find(looksLikeName) : undefined
  const before = parts.find(looksLikeName)
  // After the UTR when the narration is slash-separated, otherwise the first name-like part.
  return { utr, payerName: (narration.includes('/') ? afterUtr ?? before : before) ?? null }
}

/**
 * Money in from a bank or UPI statement, CSV or pasted from Excel. Banks put lines above the table, so the header is
 * searched for. Money out and anything that isn't a UPI or bank transfer (interest, cash deposits) is left out.
 */
export function parseStatement(text: string): { rows: StatementRow[]; skipped: number; found: boolean } {
  const lines = text.split(/\r?\n/).filter((l) => l.trim().length > 0).map(splitLine)
  const head = lines.slice(0, 40).findIndex((cells) => cells.some((c) => DATE.test(c.trim())) && cells.some((c) => NARRATION.test(c)) && cells.some((c) => CREDIT.test(c.trim()) || AMOUNT.test(c.trim())))
  if (head < 0) return { rows: [], skipped: 0, found: false }
  const find = (pattern: RegExp) => lines[head].findIndex((c) => pattern.test(c.trim()))
  // "Cr/Dr" and "Credit/Debit" say which way the money went. They aren't the credit amount.
  const columns = { date: find(DATE), narration: find(NARRATION), credit: lines[head].findIndex((c) => CREDIT.test(c.trim()) && !SIDE.test(c.trim())), amount: find(AMOUNT), side: find(SIDE) }

  const rows: StatementRow[] = []
  let skipped = 0
  for (const cells of lines.slice(head + 1)) {
    const receivedAt = parseDate(cells[columns.date] ?? '')
    if (!receivedAt) continue // a footer or a note, not a transaction
    const narration = (cells[columns.narration] ?? '').trim()
    const isCredit = columns.credit >= 0 || /^c/i.test((cells[columns.side] ?? '').trim()) || /cr\.?\s*$/i.test(cells[columns.amount] ?? '')
    const amount = isCredit ? toAmount(cells[columns.credit >= 0 ? columns.credit : columns.amount] ?? '') : null
    if (amount === null) continue // money out
    if (!/\b(upi|imps|neft|rtgs)\b/i.test(narration.replace(/[/-]/g, ' '))) { skipped++; continue }
    rows.push({ amount, receivedAt, narration, ...readNarration(narration) })
  }
  return { rows, skipped, found: true }
}

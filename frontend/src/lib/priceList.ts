/** One line of a company's price list: what it calls the item (name or code), and the new price. */
export interface PriceRow { key: string; price: number }

/** Splits one CSV line, keeping commas inside quotes ("Pipe 1/2"", 3 m",450). */
function cells(line: string): string[] {
  const out: string[] = []
  let cur = ''
  let quoted = false
  for (let i = 0; i < line.length; i++) {
    const ch = line[i]
    if (ch === '"') {
      if (quoted && line[i + 1] === '"') { cur += '"'; i++ } else quoted = !quoted
    } else if ((ch === ',' || ch === '\t' || ch === ';') && !quoted) { out.push(cur.trim()); cur = '' } else cur += ch
  }
  out.push(cur.trim())
  return out
}

const money = (s: string) => Number(s.replace(/[₹,\s]/g, ''))

/**
 * Reads a price list CSV. With a header row, the item column is the one named item, name, product, description, code or
 * SKU, and the price column the one named price, rate, MRP, DLP, retail or new; without one, the first column is the item
 * and the first number after it the price. Lines without both are skipped and counted.
 */
export function readPriceList(text: string): { rows: PriceRow[]; skipped: number } {
  const lines = text.replace(/^﻿/, '').split(/\r?\n/).filter((l) => l.trim())
  if (lines.length === 0) return { rows: [], skipped: 0 }
  const head = cells(lines[0]).map((h) => h.toLowerCase())
  const keyCol = head.findIndex((h) => /^(item|name|product|description|item name|product name|code|sku|barcode|item code)$/.test(h))
  const priceCol = head.findIndex((h) => /(price|rate|mrp|dlp|retail|new)/.test(h))
  const hasHeader = keyCol >= 0 || priceCol >= 0
  const rows: PriceRow[] = []
  let skipped = 0
  for (const line of hasHeader ? lines.slice(1) : lines) {
    const c = cells(line)
    const key = c[keyCol >= 0 ? keyCol : 0] ?? ''
    const priceText = priceCol >= 0 ? c[priceCol] : c.slice(1).find((x) => x && !Number.isNaN(money(x)))
    const price = priceText ? money(priceText) : NaN
    if (key && Number.isFinite(price) && price > 0) rows.push({ key, price: Math.round(price * 100) / 100 })
    else skipped++
  }
  return { rows, skipped }
}

const norm = (s: string) => s.toLowerCase().replace(/\s+/g, ' ').trim()

/** Matches each row to an item by barcode first, then by exact name (spaces and case ignored). */
export function matchPrices<T extends { id: string; name: string; barcode?: string | null }>(rows: PriceRow[], items: T[]): { matched: { item: T; price: number }[]; unmatched: string[] } {
  const byCode = new Map(items.filter((p) => p.barcode).map((p) => [p.barcode!.trim(), p]))
  const byName = new Map(items.map((p) => [norm(p.name), p]))
  const matched: { item: T; price: number }[] = []
  const unmatched: string[] = []
  for (const r of rows) {
    const item = byCode.get(r.key.trim()) ?? byName.get(norm(r.key))
    if (item) matched.push({ item, price: r.price })
    else unmatched.push(r.key)
  }
  return { matched, unmatched }
}

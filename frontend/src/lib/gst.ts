import type { BillLineView } from './types'

/** Rates offered for an item. 12% and 28% went in the Sep 2025 rate change; an older item keeps its rate until changed. */
export const GST_RATES = ['0', '5', '18', '40']

/**
 * Clothes and footwear (HSN 61, 62, 64): 5% when a piece sells at up to ₹2,500 before GST, 18% above (from Sep 2025).
 * The same rule as the server's GarmentGst, so the bill screen shows what the saved bill will charge.
 */
export function garmentRate(hsnCode: string | null | undefined, itemRate: number | null | undefined, unitPrice: number) {
  const garment = !!hsnCode && /^(61|62|64)/.test(hsnCode)
  if (!garment || !(itemRate === 5 || itemRate === 12 || itemRate === 18)) return itemRate ?? null
  return unitPrice / 1.05 <= 2500 ? 5 : 18
}

/**
 * GST inside GST-inclusive prices, by rate: taxable value = amount ÷ (1 + rate), tax = the rest, split equally into
 * CGST and SGST (a sale within the state, which a counter sale is). 0% lines are a row of their own; lines with no
 * rate set aren't in the table (see {@link gstUnsetAmount}).
 */
export function gstSummary(lines: Pick<BillLineView, 'amount' | 'gstRate'>[]) {
  const byRate = new Map<number, number>()
  for (const line of lines) {
    if (line.gstRate != null) byRate.set(line.gstRate, (byRate.get(line.gstRate) ?? 0) + line.amount)
  }
  return [...byRate.entries()].sort((a, b) => a[0] - b[0]).map(([rate, amount]) => {
    const taxable = Math.round((amount / (1 + rate / 100)) * 100) / 100
    const tax = Math.round((amount - taxable) * 100) / 100
    const half = Math.round((tax / 2) * 100) / 100
    return { rate, amount, taxable, cgst: half, sgst: Math.round((tax - half) * 100) / 100 }
  })
}

/** Tax by rate as kept on the bill when it was made; null for bills from before tax was kept. */
export function storedSummary(lines: Pick<BillLineView, 'amount' | 'gstRate' | 'taxableValue' | 'cgst' | 'sgst' | 'igst'>[]) {
  if (!lines.some((l) => l.taxableValue != null)) return null
  const byRate = new Map<number, { rate: number; amount: number; taxable: number; cgst: number; sgst: number; igst: number }>()
  for (const l of lines) {
    if (l.gstRate == null || l.taxableValue == null) continue
    const row = byRate.get(l.gstRate) ?? { rate: l.gstRate, amount: 0, taxable: 0, cgst: 0, sgst: 0, igst: 0 }
    row.amount += l.amount; row.taxable += l.taxableValue; row.cgst += l.cgst ?? 0; row.sgst += l.sgst ?? 0; row.igst += l.igst ?? 0
    byRate.set(l.gstRate, row)
  }
  const round = (n: number) => Math.round(n * 100) / 100
  return [...byRate.values()].sort((a, b) => a.rate - b.rate).map((r) => ({ ...r, taxable: round(r.taxable), cgst: round(r.cgst), sgst: round(r.sgst), igst: round(r.igst) }))
}

/** What's on the bill with no GST rate set, so the table can still add up to the total. */
export function gstUnsetAmount(lines: Pick<BillLineView, 'amount' | 'gstRate'>[]) {
  return Math.round(lines.reduce((sum, l) => sum + (l.gstRate == null ? l.amount : 0), 0) * 100) / 100
}

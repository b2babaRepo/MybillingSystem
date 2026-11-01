import type { BillLineView } from './types'

/** Rates offered for an item. 12% and 28% went in the Sep 2025 rate change; an older item keeps its rate until changed. */
export const GST_RATES = ['0', '5', '18', '40']

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

/** What's on the bill with no GST rate set, so the table can still add up to the total. */
export function gstUnsetAmount(lines: Pick<BillLineView, 'amount' | 'gstRate'>[]) {
  return Math.round(lines.reduce((sum, l) => sum + (l.gstRate == null ? l.amount : 0), 0) * 100) / 100
}

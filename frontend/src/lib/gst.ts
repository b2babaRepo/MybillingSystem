import type { BillLineView } from './types'

/**
 * GST inside GST-inclusive prices, by rate: taxable value = amount ÷ (1 + rate), tax = the rest, split equally into
 * CGST and SGST (a sale within the state, which a counter sale is). Lines without a rate aren't in the table.
 */
export function gstSummary(lines: Pick<BillLineView, 'amount' | 'gstRate'>[]) {
  const byRate = new Map<number, number>()
  for (const line of lines) {
    if (line.gstRate) byRate.set(line.gstRate, (byRate.get(line.gstRate) ?? 0) + line.amount)
  }
  return [...byRate.entries()].sort((a, b) => a[0] - b[0]).map(([rate, amount]) => {
    const taxable = Math.round((amount / (1 + rate / 100)) * 100) / 100
    const tax = Math.round((amount - taxable) * 100) / 100
    const half = Math.round((tax / 2) * 100) / 100
    return { rate, amount, taxable, cgst: half, sgst: Math.round((tax - half) * 100) / 100 }
  })
}

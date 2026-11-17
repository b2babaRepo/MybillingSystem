/** Profit on one unit at `price` (GST taken out first, at `gstRate`), and as a % of that net price. */
export function unitProfit(price: number, cost: number, gstRate: number | null): { amount: number; percent: number } {
  const net = price / (1 + (gstRate ?? 0) / 100)
  return { amount: Math.round((net - cost) * 100) / 100, percent: net > 0 ? Math.round(((net - cost) / net) * 1000) / 10 : 0 }
}

/** Turns a rate chart copied from Excel (fat down the first column, SNF across the first row) into cells. */
export function parseGrid(text: string): { fat: number; snf: number; ratePerLitre: number }[] {
  const rows = text.trim().split(/\r?\n/).map((r) => r.split(/\t|,|;/).map((c) => c.trim()))
  if (rows.length < 2) return []
  const snfs = rows[0].slice(1).map(Number)
  const cells: { fat: number; snf: number; ratePerLitre: number }[] = []
  for (const row of rows.slice(1)) {
    const fat = Number(row[0])
    if (!(fat > 0)) continue
    row.slice(1).forEach((v, i) => { const rate = Number(v); if (rate > 0 && snfs[i] > 0) cells.push({ fat: Math.round(fat * 10) / 10, snf: Math.round(snfs[i] * 10) / 10, ratePerLitre: Math.round(rate * 100) / 100 }) })
  }
  return cells
}

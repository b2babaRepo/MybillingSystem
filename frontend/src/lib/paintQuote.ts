/** A room to paint, in feet; doors and windows in square feet, taken off the walls. */
export interface Room { name: string; length: number; width: number; height: number; openings: number; ceiling: boolean }

/** Paintable area: the four walls less doors and windows, plus the ceiling when it is being painted too. */
export function roomArea(r: Room): number {
  const walls = 2 * (r.length + r.width) * r.height
  return Math.max(0, walls - r.openings) + (r.ceiling ? r.length * r.width : 0)
}

/** Litres needed: area × coats ÷ coverage (square feet one litre covers in one coat), to 0.1 L. */
export function litresFor(area: number, coats: number, coverage: number): number {
  if (!(coverage > 0) || !(coats > 0)) return 0
  return Math.ceil((area * coats) / coverage * 10) / 10
}

/** Litres in a tin, read from its name: "Royale Emulsion 4 L", "Apex 10 Ltr", "Primer 500 ml". */
export function packLitres(name: string): number | null {
  const m = name.match(/(\d+(?:\.\d+)?)\s*(l|ltr|litre|liter|lt|ml)\b/i)
  if (!m) return null
  const n = Number(m[1])
  return m[2].toLowerCase() === 'ml' ? n / 1000 : n
}

export interface Pack { id: string; name: string; litres: number; price: number }

/**
 * The cheapest set of tins that holds at least the litres needed. Tins are few (1, 4, 10, 20 L), so every count of each
 * size up to what would cover the job alone is tried: the answer is exact, not a greedy guess.
 */
export function packMix(litres: number, packs: Pack[]): { pack: Pack; count: number }[] {
  const sizes = packs.filter((p) => p.litres > 0 && p.price > 0).sort((a, b) => b.litres - a.litres).slice(0, 5)
  if (litres <= 0 || sizes.length === 0) return []
  let best: { cost: number; litres: number; counts: number[] } | null = null
  const counts = sizes.map(() => 0)
  const walk = (i: number, have: number, cost: number) => {
    if (best && cost > best.cost) return
    if (have >= litres - 1e-9) {
      if (!best || cost < best.cost || (cost === best.cost && have < best.litres)) best = { cost, litres: have, counts: [...counts] }
      return
    }
    if (i >= sizes.length) return
    const most = Math.ceil((litres - have) / sizes[i].litres)
    for (let n = most; n >= 0; n--) {
      counts[i] = n
      walk(i + 1, have + n * sizes[i].litres, cost + n * sizes[i].price)
    }
    counts[i] = 0
  }
  walk(0, 0, 0)
  const found = best as { counts: number[] } | null
  return found ? sizes.map((pack, i) => ({ pack, count: found.counts[i] })).filter((x) => x.count > 0) : []
}

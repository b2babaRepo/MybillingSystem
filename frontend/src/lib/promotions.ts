/**
 * The promotion engine: the shop's planned offers applied to a whole cart. The server runs the same rules
 * (backend Ledger/Promotions.cs) and both are tested against shared/promotions.cases.json, so a bill priced on a till
 * with no internet is priced the same when it reaches the server.
 *
 * Kinds: percent / amount (off each piece), buyGet (buy N get M free, same item), slab (a percent off once the cart has
 * at least N of the covered items), combo (a set of items together for one price), billPercent / billAmount (off the
 * whole bill once it reaches an amount). Time windows: dates, plus weekdays and hours of the day (India time).
 *
 * Rules: an item offer never stacks on another; the one saving the customer most is taken first, and a line it uses is
 * not used again. Then the best bill offer comes off what is left. Money is worked in whole paise.
 */

export type PromotionKind = 'percent' | 'amount' | 'buyGet' | 'slab' | 'combo' | 'billPercent' | 'billAmount'

export interface Promotion {
  id: string
  name: string
  kind: PromotionKind
  /** percent / slab / billPercent: the percent; amount / billAmount: rupees. */
  value: number
  startsAt: string
  endsAt: string
  stoppedAt?: string | null
  scope: 'all' | 'category' | 'items'
  category?: string | null
  productIds: string[]
  buyQty?: number | null
  getQty?: number | null
  /** slab: from this many of the covered items. */
  minQty?: number | null
  /** billPercent / billAmount: from this bill amount (₹). */
  minBill?: number | null
  /** combo: one of each item in productIds, together, for this price (₹). */
  comboPrice?: number | null
  /** Weekdays it runs (bit 0 Sunday … bit 6 Saturday); null: every day. */
  days?: number | null
  /** Minutes after midnight, India time, it runs from and until (until exclusive); null: all day. */
  fromMinute?: number | null
  toMinute?: number | null
}

export interface PromoLine { productId: string | null; category: string | null; regular: number; quantity: number }

/** A piece of a cart line at one price: paid units, or free units at ₹0. */
export interface PricedPart { quantity: number; unitPrice: number; offerId: string | null; offerName: string | null }

export interface PromotionResult {
  /** For each cart line, its parts (one part at its regular price when no offer touched it). */
  parts: PricedPart[][]
  /** The bill offer taken, and how much it takes off (₹). */
  billOff: { offerId: string; offerName: string; amount: number } | null
  /** Saved in all (₹), item offers and bill offer together. */
  saved: number
}

const IST_MS = 5.5 * 3_600_000
const paise = (rupees: number) => Math.round(rupees * 100)
const rupees = (p: number) => p / 100
const roundHalfUp = (x: number) => Math.sign(x) * Math.round(Math.abs(x))

export function runsAt(p: Pick<Promotion, 'startsAt' | 'endsAt' | 'stoppedAt' | 'days' | 'fromMinute' | 'toMinute'>, at: Date): boolean {
  const t = at.getTime()
  const end = Math.min(Date.parse(p.endsAt), p.stoppedAt ? Date.parse(p.stoppedAt) : Infinity)
  if (!(Date.parse(p.startsAt) <= t && t < end)) return false
  const india = new Date(t + IST_MS)
  if (p.days != null && ((p.days >> india.getUTCDay()) & 1) === 0) return false
  if (p.fromMinute != null && p.toMinute != null) {
    const minute = india.getUTCHours() * 60 + india.getUTCMinutes()
    return p.fromMinute <= p.toMinute ? p.fromMinute <= minute && minute < p.toMinute : minute >= p.fromMinute || minute < p.toMinute
  }
  return true
}

function covers(p: Promotion, line: PromoLine) {
  if (!line.productId) return false
  if (p.scope === 'all') return true
  if (p.scope === 'category') return !!line.category && !!p.category && line.category.toLowerCase() === p.category.toLowerCase()
  return p.productIds.includes(line.productId)
}

/** A choice the engine could make: what it saves, the lines it uses, and the parts it gives them. */
interface Candidate { saving: number; order: number; lines: number[]; parts: Map<number, PricedPart[]> }

const whole = (q: number) => Math.floor(q + 1e-9)

export function applyPromotions(lines: PromoLine[], promotions: Promotion[], at: Date): PromotionResult {
  const running = promotions.filter((p) => runsAt(p, at))
  const regular = lines.map((l) => paise(l.regular))
  const candidates: Candidate[] = []
  const part = (p: Promotion, quantity: number, unitPaise: number): PricedPart => ({ quantity, unitPrice: rupees(unitPaise), offerId: p.id, offerName: p.name })
  const rest = (i: number, quantity: number): PricedPart[] => (quantity > 1e-9 ? [{ quantity: Math.round(quantity * 1000) / 1000, unitPrice: rupees(regular[i]), offerId: null, offerName: null }] : [])

  running.forEach((p, order) => {
    const covered = lines.map((l, i) => (covers(p, l) && l.quantity > 0 && regular[i] > 0 ? i : -1)).filter((i) => i >= 0)
    if (p.kind === 'percent' || p.kind === 'amount') {
      for (const i of covered) {
        const price = Math.max(0, p.kind === 'percent' ? roundHalfUp((regular[i] * (100 - p.value)) / 100) : regular[i] - paise(p.value))
        const saving = Math.round((regular[i] - price) * lines[i].quantity)
        if (saving > 0) candidates.push({ saving, order, lines: [i], parts: new Map([[i, [part(p, lines[i].quantity, price)]]]) })
      }
    } else if (p.kind === 'buyGet' && (p.buyQty ?? 0) >= 1 && (p.getQty ?? 0) >= 1) {
      for (const i of covered) {
        const sets = Math.floor(whole(lines[i].quantity) / (p.buyQty! + p.getQty!))
        const free = sets * p.getQty!
        if (free > 0) candidates.push({ saving: free * regular[i], order, lines: [i], parts: new Map([[i, [...rest(i, lines[i].quantity - free), part(p, free, 0)]]]) })
      }
    } else if (p.kind === 'slab' && covered.length > 0 && covered.reduce((n, i) => n + lines[i].quantity, 0) >= (p.minQty ?? Infinity) - 1e-9) {
      const parts = new Map<number, PricedPart[]>()
      let saving = 0
      for (const i of covered) {
        const price = roundHalfUp((regular[i] * (100 - p.value)) / 100)
        saving += Math.round((regular[i] - price) * lines[i].quantity)
        parts.set(i, [part(p, lines[i].quantity, price)])
      }
      if (saving > 0) candidates.push({ saving, order, lines: covered, parts })
    } else if (p.kind === 'combo' && p.comboPrice != null && p.productIds.length >= 2) {
      // One line per combo item (the first line holding it); as many combos as the scarcest item allows.
      const at = p.productIds.map((id) => lines.findIndex((l, i) => l.productId === id && regular[i] > 0))
      if (at.some((i) => i < 0)) return
      const sets = Math.min(...at.map((i) => whole(lines[i].quantity)))
      const full = at.reduce((n, i) => n + regular[i], 0)
      const price = paise(p.comboPrice)
      if (sets < 1 || price >= full) return
      // Each item's share of the combo price, in proportion to its regular price; the first item takes the rounding.
      const shares = at.map((i) => roundHalfUp((regular[i] * price) / full))
      shares[0] = Math.max(0, price - shares.slice(1).reduce((n, s) => n + s, 0))
      const parts = new Map<number, PricedPart[]>()
      at.forEach((i, k) => parts.set(i, [part(p, sets, shares[k]), ...rest(i, lines[i].quantity - sets)]))
      candidates.push({ saving: sets * (full - price), order, lines: at, parts })
    }
  })

  // Biggest saving first; a tie goes to the offer listed first, then the earlier line.
  candidates.sort((a, b) => b.saving - a.saving || a.order - b.order || a.lines[0] - b.lines[0])
  const parts: PricedPart[][] = lines.map((l, i) => [{ quantity: l.quantity, unitPrice: rupees(regular[i]), offerId: null, offerName: null }])
  const used = new Set<number>()
  let saved = 0
  for (const c of candidates) {
    if (c.lines.some((i) => used.has(i))) continue
    c.lines.forEach((i) => { used.add(i); parts[i] = c.parts.get(i)! })
    saved += c.saving
  }

  // The bill offer comes off what is left after item offers.
  const subtotal = parts.reduce((n, ps) => n + ps.reduce((m, x) => m + Math.round(paise(x.unitPrice) * x.quantity), 0), 0)
  let billOff: PromotionResult['billOff'] = null
  let best = 0
  for (const p of running) {
    if ((p.kind !== 'billPercent' && p.kind !== 'billAmount') || subtotal < paise(p.minBill ?? 0) || subtotal <= 0) continue
    const off = Math.min(subtotal - 1, p.kind === 'billPercent' ? roundHalfUp((subtotal * p.value) / 100) : paise(p.value))
    if (off > best) { best = off; billOff = { offerId: p.id, offerName: p.name, amount: rupees(off) } }
  }
  return { parts, billOff, saved: rupees(saved + best) }
}

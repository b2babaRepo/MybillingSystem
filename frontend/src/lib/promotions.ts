/**
 * The promotion engine: the shop's planned offers applied to a whole cart. The server runs the same rules
 * (backend Ledger/Promotions.cs) and both are tested against shared/promotions.cases.json, so a bill priced on a till
 * with no internet is priced the same when it reaches the server.
 *
 * Kinds: percent / amount (off each piece), buy_get (buy N get M free, same item), slab (a percent off once the cart has
 * at least N of the covered items), combo (a set of items together for one price), bill_percent / bill_amount (off the
 * whole bill once it reaches an amount). Time windows: dates, plus weekdays and hours of the day (India time).
 *
 * Rules: an item offer never stacks on another; the one saving the customer most is taken first, and a line it uses is
 * not used again. Then the best bill offer comes off what is left. Money is worked in whole paise.
 */

export type PromotionKind = 'percent' | 'amount' | 'buy_get' | 'slab' | 'combo' | 'bill_percent' | 'bill_amount'

export interface Promotion {
  id: string
  name: string
  kind: PromotionKind
  /** percent / slab / bill_percent: the percent; amount / bill_amount: rupees. */
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
  /** bill_percent / bill_amount: from this bill amount (₹). */
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
    } else if (p.kind === 'buy_get' && (p.buyQty ?? 0) >= 1 && (p.getQty ?? 0) >= 1) {
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
  const billOff = bestBillOff(rupees(subtotal), running)
  return { parts, billOff, saved: rupees(saved + paise(billOff?.amount ?? 0)) }
}

/** Of the bill offers running, the one taking most off a bill of this amount (never all of it). */
export function bestBillOff(amount: number, running: Promotion[]): PromotionResult['billOff'] {
  const subtotal = paise(amount)
  let billOff: PromotionResult['billOff'] = null
  let best = 0
  for (const p of running) {
    if ((p.kind !== 'bill_percent' && p.kind !== 'bill_amount') || subtotal < paise(p.minBill ?? 0) || subtotal <= 0) continue
    const off = Math.min(subtotal - 1, p.kind === 'bill_percent' ? roundHalfUp((subtotal * p.value) / 100) : paise(p.value))
    if (off > best) { best = off; billOff = { offerId: p.id, offerName: p.name, amount: rupees(off) } }
  }
  return billOff
}

/** What one offer saved on the bill, for the bill screen's list of offers taken. */
export interface Applied { offerId: string; offerName: string; saved: number }

/**
 * The cart as it will be billed: lines still at their item's regular price (no offer, no price typed) go through the
 * engine; each comes back as its parts (free pieces their own line at ₹0). A bill offer's money off is then spread
 * over the lines' prices, as a whole-bill discount is. Lines with a price of their own pass through unchanged.
 */
export function promoteCart<L extends { productId: string | null; quantity: number; unitPrice: number; offerId?: string | null; offerName?: string | null }>(
  cart: L[], products: { id: string; category: string | null; sellingPrice: number }[], promotions: Promotion[], at: Date,
): { lines: L[]; applied: Applied[]; billOfferId: string | null } {
  const byId = new Map(products.map((p) => [p.id, p]))
  const open = cart.map((l, i) => ({ l, i, p: l.productId ? byId.get(l.productId) : undefined }))
    .filter(({ l, p }) => p && !l.offerId && p.sellingPrice > 0 && l.unitPrice === p.sellingPrice)
  if (promotions.length === 0 || cart.length === 0) return { lines: cart, applied: [], billOfferId: null }
  // Item offers on the lines still at their regular price; the bill offer then on the whole bill.
  const itemOffers = promotions.filter((p) => p.kind !== 'bill_percent' && p.kind !== 'bill_amount')
  const result = applyPromotions(open.map(({ l, p }) => ({ productId: l.productId, category: p!.category, regular: p!.sellingPrice, quantity: l.quantity })), itemOffers, at)
  const saved = new Map<string, Applied>()
  const partsOf = new Map(open.map(({ i }, k) => [i, result.parts[k]]))
  let lines: L[] = cart.flatMap((l, i) => {
    const parts = partsOf.get(i)
    if (!parts) return [l]
    return parts.map((part) => {
      if (part.offerId) {
        const s = saved.get(part.offerId) ?? { offerId: part.offerId, offerName: part.offerName ?? '', saved: 0 }
        s.saved = Math.round((s.saved + (l.unitPrice - part.unitPrice) * part.quantity) * 100) / 100
        saved.set(part.offerId, s)
      }
      return { ...l, quantity: part.quantity, unitPrice: part.unitPrice, offerId: part.offerId ?? l.offerId ?? null, offerName: part.offerName ?? l.offerName ?? null }
    })
  })
  const amount = (x: L) => Math.round(x.quantity * x.unitPrice * 100) / 100
  const total = Math.round(lines.reduce((n, x) => n + amount(x), 0) * 100) / 100
  const billOff = bestBillOff(total, promotions.filter((p) => runsAt(p, at)))
  if (billOff) {
    const factor = (total - billOff.amount) / total
    lines = lines.map((x) => ({ ...x, unitPrice: Math.round(x.unitPrice * factor * 100) / 100 }))
    // Rounding can leave a few paise: the single-piece line with the most money takes them, so the bill is exact.
    const gap = Math.round((total - billOff.amount - lines.reduce((n, x) => n + amount(x), 0)) * 100) / 100
    const single = lines.map((x, k) => ({ x, k })).filter(({ x }) => x.quantity === 1 && x.unitPrice + gap >= 0)
    const big = single.reduce<number | null>((best, { x, k }) => (best === null || amount(x) > amount(lines[best]) ? k : best), null)
    if (gap !== 0 && big !== null) lines[big] = { ...lines[big], unitPrice: Math.round((lines[big].unitPrice + gap) * 100) / 100 }
    saved.set(billOff.offerId, { offerId: billOff.offerId, offerName: billOff.offerName, saved: Math.round((total - lines.reduce((n, x) => n + amount(x), 0)) * 100) / 100 })
  }
  return { lines, applied: [...saved.values()], billOfferId: billOff?.offerId ?? null }
}

/**
 * Money off the whole bill spread over its lines by their share (a bill offer, loyalty points): each line's price goes
 * down, so GST is worked on what is really paid. A single-piece line takes the rounding paise, so the total is exact.
 */
export function spreadOff<L extends { quantity: number; unitPrice: number }>(lines: L[], off: number): L[] {
  const amount = (x: L) => Math.round(x.quantity * x.unitPrice * 100) / 100
  const total = Math.round(lines.reduce((n, x) => n + amount(x), 0) * 100) / 100
  if (!(off > 0) || total <= 0) return lines
  const factor = (total - off) / total
  const next = lines.map((x) => ({ ...x, unitPrice: Math.round(x.unitPrice * factor * 100) / 100 }))
  const gap = Math.round((total - off - next.reduce((n, x) => n + amount(x), 0)) * 100) / 100
  const single = next.map((x, k) => ({ x, k })).filter(({ x }) => x.quantity === 1 && x.unitPrice + gap >= 0)
  const big = single.reduce<number | null>((best, { x, k }) => (best === null || amount(x) > amount(next[best]) ? k : best), null)
  if (gap !== 0 && big !== null) next[big] = { ...next[big], unitPrice: Math.round((next[big].unitPrice + gap) * 100) / 100 }
  return next
}

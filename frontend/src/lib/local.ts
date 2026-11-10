import type { OutboxOp } from './outbox'
import type { Customer, LedgerEntryKind, Product, Statement, StatementLine, Today } from './types'

// What the shopkeeper sees = the last server data + their own writes still in the outbox.
// Pure functions, so they're easy to test. Failed ops are left out: the server refused them.

export interface LocalCustomer extends Customer {
  /** Writes for this customer not yet on the server. */
  pending: number
  /** Added on this phone and not synced yet: the server doesn't know them. */
  isNew: boolean
}

export interface LocalLine extends StatementLine {
  pending?: boolean
}

export const round2 = (value: number) => Math.round(value * 100) / 100

const live = (ops: OutboxOp[]) => ops.filter((op) => op.status === 'pending')

export function mergeCustomers(server: Customer[], ops: OutboxOp[]): LocalCustomer[] {
  const byId = new Map<string, LocalCustomer>(server.map((c) => [c.id, { ...c, pending: 0, isNew: false }]))
  for (const op of live(ops)) {
    if (op.kind === 'customer' && !byId.has(op.id)) {
      const body = op.body as { id: string; name: string; phone?: string | null }
      byId.set(op.id, { id: body.id, name: body.name, phone: body.phone ?? null, balance: 0, pending: 1, isNew: true })
    }
  }

  for (const op of live(ops)) {
    const customer = op.customerId ? byId.get(op.customerId) : undefined
    if (customer && op.delta !== undefined) {
      customer.balance = round2(customer.balance + op.delta)
      customer.pending++
    }
  }

  return [...byId.values()].sort((a, b) => a.name.localeCompare(b.name))
}

/** Marked sold out for today, by the shop's clock (India time, as the server counts it). */
export const isOutToday = (outOn: string | null | undefined, now = new Date()) => !!outOn && outOn === now.toLocaleDateString('en-CA', { timeZone: 'Asia/Kolkata' })

/** A product is low when it's out, or at/below its reorder level (the same rule as the server). */
export const isLow = (stock: number, reorderLevel: number | null) => stock <= 0 || (reorderLevel !== null && stock <= reorderLevel)

/**
 * Server products + products added on this phone + stock moved on this phone (purchases, adjustments,
 * and catalogue items on unsynced bills), so the shelf count is right before the sync.
 */
export function mergeProducts(server: Product[], ops: OutboxOp[]): Product[] {
  const byId = new Map(server.map((p) => [p.id, { ...p }]))
  for (const op of live(ops)) {
    if (op.kind === 'product' && !byId.has(op.id)) {
      const body = op.body as { id: string; name: string; unit: Product['unit']; sellingPrice: number; costPrice: number | null; category?: string | null; reorderLevel?: number | null; openingStock?: number | null }
      const stock = body.openingStock ?? 0
      const reorderLevel = body.reorderLevel ?? null
      byId.set(op.id, { id: body.id, name: body.name, unit: body.unit, sellingPrice: body.sellingPrice, costPrice: body.costPrice, category: body.category ?? null, stock, reorderLevel, isLow: isLow(stock, reorderLevel), pending: true })
    }
  }

  const move = (productId: string, delta: number) => {
    const product = byId.get(productId)
    if (!product) return
    product.stock = Math.round((product.stock + delta) * 1000) / 1000
    product.isLow = !product.uncounted && isLow(product.stock, product.reorderLevel)
    product.pending = true
  }
  for (const op of live(ops)) {
    if (op.kind === 'stock') {
      const body = op.body as { productId: string; quantity: number }
      move(body.productId, body.quantity)
    } else if (op.kind === 'bill') {
      for (const line of (op.body as { lines: { productId: string | null; quantity: number }[] }).lines) {
        if (line.productId) move(line.productId, -line.quantity)
      }
    }
  }

  return [...byId.values()].sort((a, b) => a.name.localeCompare(b.name))
}

/** The customer's khata with unsynced entries added at the end, marked pending. */
export function mergeStatement(server: Statement | undefined, customer: LocalCustomer | undefined, ops: OutboxOp[]) {
  const entries: LocalLine[] = server ? server.entries.map((e) => ({ ...e })) : []
  let running = entries.at(-1)?.runningBalance ?? 0

  for (const op of live(ops)) {
    if (!customer || op.customerId !== customer.id || op.delta === undefined) continue
    running = round2(running + op.delta)
    const body = op.body as { id: string; note?: string | null; paymentMode?: 'cash' | 'upi'; occurredAt?: string; type?: string }
    const kind: LedgerEntryKind = op.kind === 'reverse' ? 'reversal' : op.delta < 0 ? 'payment' : 'credit'
    entries.push({
      id: op.id,
      kind,
      amount: op.delta,
      paymentMode: kind === 'payment' ? (body.paymentMode ?? 'cash') : null,
      billId: op.kind === 'bill' ? op.id : null,
      reversesEntryId: op.reversesEntryId ?? null,
      note: body.note ?? null,
      occurredAt: body.occurredAt ?? op.createdAt,
      runningBalance: running,
      isReversed: false,
      pending: true,
    })
  }

  const reversed = new Set(entries.map((e) => e.reversesEntryId).filter(Boolean))
  for (const entry of entries) entry.isReversed ||= reversed.has(entry.id)

  return { customer, entries }
}

/** Today's numbers with unsynced bills and khata entries added, so the home screen moves at once, offline too. */
export function mergeToday(server: Today | undefined, ops: OutboxOp[]): Today | undefined {
  if (!server) return undefined
  const today = { ...server }
  for (const op of live(ops)) {
    if (op.kind === 'khata') {
      const body = op.body as { type: 'credit' | 'payment'; amount: number; paymentMode?: string | null }
      if (body.type === 'credit') today.udhaar = round2(today.udhaar + body.amount)
      else if (body.paymentMode === 'upi') today.upi = round2(today.upi + body.amount)
      else if (body.paymentMode === 'card') today.card = round2((today.card ?? 0) + body.amount)
      // A cheque waits on the bank: not money in yet.
      else if (body.paymentMode === 'cheque') today.chequesPending = round2((today.chequesPending ?? 0) + body.amount)
      else today.cash = round2(today.cash + body.amount)
    } else if (op.kind === 'bill') {
      const body = op.body as { customerId: string | null; lines: { quantity: number; unitPrice: number }[]; paidAmount: number; paymentMode: 'cash' | 'upi'; payments?: { mode: string; amount: number }[] }
      const total = round2(body.lines.reduce((sum, l) => sum + round2(l.quantity * l.unitPrice), 0))
      today.sales = round2(today.sales + total)
      today.bills += 1
      for (const p of body.payments ?? [{ mode: body.paymentMode, amount: body.paidAmount }]) {
        if (p.mode === 'upi') today.upi = round2(today.upi + p.amount)
        else if (p.mode === 'card') today.card = round2((today.card ?? 0) + p.amount)
        else today.cash = round2(today.cash + p.amount)
      }
      if (body.customerId) today.udhaar = round2(today.udhaar + total - body.paidAmount)
    }
  }
  return today
}

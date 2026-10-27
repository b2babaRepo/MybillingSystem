import type { OutboxOp } from './outbox'
import type { Customer, LedgerEntryKind, Product, Statement, StatementLine } from './types'

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

export function mergeProducts(server: Product[], ops: OutboxOp[]): Product[] {
  const ids = new Set(server.map((p) => p.id))
  const pending = live(ops)
    .filter((op) => op.kind === 'product' && !ids.has(op.id))
    .map((op) => op.body as Product)
  return [...server, ...pending].sort((a, b) => a.name.localeCompare(b.name))
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

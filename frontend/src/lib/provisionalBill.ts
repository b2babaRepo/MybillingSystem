import type { CartLine } from './data'
import type { OutboxOp } from './outbox'
import type { PaymentMode } from './types'

export interface ProvisionalBill {
  occurredAt: string
  customerName: string | null
  lines: (CartLine & { amount: number })[]
  total: number
  paidAmount: number
  onKhata: number
  paymentMode: PaymentMode
}

const round2 = (n: number) => Math.round(n * 100) / 100

/** A bill still in the outbox, as a receipt can show it. Null when there is no such op. */
export function provisionalBill(op: OutboxOp | undefined): ProvisionalBill | null {
  if (!op) return null
  const body = op.body as { lines: CartLine[]; paidAmount: number; paymentMode: PaymentMode; occurredAt: string }
  const lines = body.lines.map((l) => ({ ...l, amount: round2(l.quantity * l.unitPrice) }))
  return {
    occurredAt: body.occurredAt,
    customerName: op.customerName ?? null,
    lines,
    total: round2(lines.reduce((sum, l) => sum + l.amount, 0)),
    paidAmount: body.paidAmount,
    onKhata: Math.max(op.delta ?? 0, 0),
    paymentMode: body.paymentMode,
  }
}

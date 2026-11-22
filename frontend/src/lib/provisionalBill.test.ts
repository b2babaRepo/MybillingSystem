import { describe, expect, it } from 'vitest'
import type { OutboxOp } from './outbox'
import { provisionalBill } from './provisionalBill'

const op = (over: Partial<OutboxOp> = {}): OutboxOp => ({
  id: 'b1', shopId: 's1', kind: 'bill', path: '/v1/bills', label: 'Bill', createdAt: '', attempts: 0, status: 'pending',
  body: {
    lines: [{ productId: null, name: 'Rice', quantity: 1.5, unit: 'kg', unitPrice: 33.33 }, { productId: null, name: 'Soap', quantity: 2, unit: 'piece', unitPrice: 10 }],
    paidAmount: 50, paymentMode: 'cash', occurredAt: '2026-11-07T10:00:00Z',
  },
  ...over,
})

describe('provisionalBill', () => {
  it('is null when the bill is not waiting on this device', () => {
    expect(provisionalBill(undefined)).toBeNull()
  })

  it('works out line amounts and the total the way saving the bill does', () => {
    const bill = provisionalBill(op())!
    expect(bill.lines.map((l) => l.amount)).toEqual([50, 20])
    expect(bill.total).toBe(70)
  })

  it('shows what went on khata and who for', () => {
    const bill = provisionalBill(op({ customerName: 'Ramesh', delta: 20 }))!
    expect([bill.customerName, bill.onKhata]).toEqual(['Ramesh', 20])
  })

  it('never shows a negative khata amount', () => {
    expect(provisionalBill(op({ delta: -5 }))!.onKhata).toBe(0)
  })
})

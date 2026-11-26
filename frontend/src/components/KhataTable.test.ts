import { describe, expect, it } from 'vitest'
import type { StatementLine } from '../lib/types'
import { dueByLine } from '../lib/khataTable'

const line = (id: string, amount: number, extra: Partial<StatementLine> = {}): StatementLine => ({
  id, kind: amount > 0 ? 'credit' : 'payment', amount, paymentMode: null, billId: null, reversesEntryId: null, note: null,
  occurredAt: '2026-11-01T10:00:00Z', runningBalance: 0, isReversed: false, ...extra,
} as StatementLine)

describe('dueByLine', () => {
  it('lets payments clear the oldest udhaar first', () => {
    const due = dueByLine([line('a', 300), line('b', 500), line('p', -350)])
    expect(due.get('a')).toBe(0)
    expect(due.get('b')).toBe(450)
  })

  it('ignores removed lines and uses an advance against later udhaar', () => {
    const due = dueByLine([line('adv', -100), line('gone', 200, { isReversed: true }), line('c', 250)])
    expect(due.has('gone')).toBe(false)
    expect(due.get('c')).toBe(150)
  })
})

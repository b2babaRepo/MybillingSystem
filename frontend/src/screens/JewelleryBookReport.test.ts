import { describe, expect, it } from 'vitest'
import type { JewelleryPiece } from '../lib/jewellery'
import type { Member } from './JewelleryBookPanels'
import { schemeOwed, slowStock } from './JewelleryBookReport'

const piece = (tagNo: number, createdAt: string, sold = false) => ({ id: `p${tagNo}`, tagNo, createdAt, sold }) as JewelleryPiece
const member = (paidTotal: number, bonus: number, closedOn: string | null = null) => ({ paidTotal, bonus, closedOn, redeemedBillId: null }) as Member

describe('jewellery book report', () => {
  it('lists unsold pieces older than the chosen days, oldest first', () => {
    const now = Date.parse('2026-11-24T00:00:00Z')
    const slow = slowStock([piece(1, '2026-01-01T00:00:00Z'), piece(2, '2026-11-01T00:00:00Z'), piece(3, '2025-06-01T00:00:00Z'), piece(4, '2025-01-01T00:00:00Z', true)], 180, now)
    expect(slow.map((s) => s.piece.tagNo)).toEqual([3, 1])
    expect(slow[1].age).toBe(327)
  })

  it('owes running scheme members what they paid plus their bonus', () => {
    expect(schemeOwed([member(11_000, 1_000), member(5_000, 1_000), member(12_000, 1_000, '2026-10-01')])).toEqual({ running: 2, collected: 16_000, bonus: 2_000, owed: 18_000 })
  })
})

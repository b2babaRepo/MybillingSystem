import { describe, expect, it } from 'vitest'
import cases from '../../../shared/promotions.cases.json'
import { applyPromotions, promoteCart, type Promotion, type PromoLine } from './promotions'

// The same cases run against the server's engine (backend tests, PromotionsTests), so both price a bill alike.
describe('promotion engine (shared cases)', () => {
  for (const c of cases) {
    it(c.name, () => {
      const result = applyPromotions(c.lines as PromoLine[], c.promotions as Promotion[], new Date(c.at))
      expect(result.parts.map((ps) => ps.map((p) => ({ quantity: p.quantity, unitPrice: p.unitPrice, offerId: p.offerId })))).toEqual(c.expect.parts)
      expect(result.billOff ? { offerId: result.billOff.offerId, amount: result.billOff.amount } : null).toEqual(c.expect.billOff)
      expect(result.saved).toBe(c.expect.saved)
    })
  }
})

describe('promoteCart', () => {
  const at = new Date('2026-11-26T06:30:00Z')
  const offer = (o: Partial<Promotion>): Promotion => ({ id: 'x', name: 'x', kind: 'percent', value: 0, startsAt: '2026-11-01T00:00:00Z', endsAt: '2026-12-31T00:00:00Z', scope: 'all', productIds: [], ...o })
  const products = [{ id: 'maggi', category: null, sellingPrice: 14 }, { id: 'oil', category: null, sellingPrice: 180 }]
  it('splits free pieces into their own line and lists what each offer saved', () => {
    const { lines, applied } = promoteCart([{ productId: 'maggi', quantity: 6, unitPrice: 14, offerId: null as string | null }], products, [offer({ id: 'b2g1', name: 'Buy 2 get 1', kind: 'buy_get', buyQty: 2, getQty: 1 })], at)
    expect(lines.map((l) => [l.quantity, l.unitPrice, l.offerId])).toEqual([[4, 14, null], [2, 0, 'b2g1']])
    expect(applied).toEqual([{ offerId: 'b2g1', offerName: 'Buy 2 get 1', saved: 28 }])
  })
  it('leaves a line with its own price or offer alone, and spreads a bill offer over every line', () => {
    const cart = [{ productId: 'oil', quantity: 1, unitPrice: 170 }, { productId: 'oil', quantity: 5, unitPrice: 180 }]
    const { lines, billOfferId, applied } = promoteCart(cart, products, [offer({ id: 'b100', name: '₹100 off', kind: 'bill_amount', value: 100, minBill: 1000 })], at)
    expect(billOfferId).toBe('b100')
    expect(applied[0].saved).toBe(100)
    expect(Math.round(lines.reduce((n, l) => n + l.quantity * l.unitPrice, 0) * 100) / 100).toBe(970)
  })
})

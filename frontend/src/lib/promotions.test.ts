import { describe, expect, it } from 'vitest'
import cases from '../../../shared/promotions.cases.json'
import { applyPromotions, type Promotion, type PromoLine } from './promotions'

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

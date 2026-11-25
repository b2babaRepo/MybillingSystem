import { describe, expect, it } from 'vitest'
import { groupOff, groupPrice } from './priceGroup'

describe('price groups', () => {
  it('gives contractors and dealers their percent off list', () => {
    const settings = { contractorDiscountPercent: 5, dealerDiscountPercent: 12 }
    expect(groupOff('contractor', settings)).toBe(5)
    expect(groupOff('dealer', settings)).toBe(12)
    expect(groupOff(null, settings)).toBe(0)
    expect(groupPrice(1250, 5)).toBe(1187.5)
    expect(groupPrice(99.99, 12)).toBe(87.99)
    expect(groupPrice(100, 0)).toBe(100)
  })
})

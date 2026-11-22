import { describe, expect, it } from 'vitest'
import { fitPrice, priceFor } from './wholesale'

const soap = { sellingPrice: 40, wholesalePrice: 36, wholesaleMinQty: 12 }

describe('wholesale price', () => {
  it('applies from the minimum quantity', () => {
    expect(priceFor(soap, 11)).toBe(40)
    expect(priceFor(soap, 12)).toBe(36)
    expect(priceFor({ sellingPrice: 40 }, 100)).toBe(40)
  })
  it('moves both ways as the quantity crosses the minimum', () => {
    expect(fitPrice(soap, 12, 40)).toBe(36)
    expect(fitPrice(soap, 5, 36)).toBe(40)
  })
  it('leaves a price the counter typed', () => {
    expect(fitPrice(soap, 12, 38)).toBe(38)
    expect(fitPrice(undefined, 12, 40)).toBe(40)
  })
})

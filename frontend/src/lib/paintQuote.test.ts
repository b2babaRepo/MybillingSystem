import { describe, expect, it } from 'vitest'
import { litresFor, packLitres, packMix, roomArea } from './paintQuote'

describe('paint quotation', () => {
  it('works out the wall area less doors and windows, with the ceiling if painted', () => {
    // 12 × 10 room, 10 ft high: walls 440 sq ft, less 40 for a door and a window; ceiling 120.
    expect(roomArea({ name: 'Hall', length: 12, width: 10, height: 10, openings: 40, ceiling: false })).toBe(400)
    expect(roomArea({ name: 'Hall', length: 12, width: 10, height: 10, openings: 40, ceiling: true })).toBe(520)
  })

  it('turns area into litres for the coats and coverage', () => {
    expect(litresFor(400, 2, 120)).toBe(6.7)
    expect(litresFor(400, 2, 0)).toBe(0)
  })

  it('reads the tin size from the name', () => {
    expect(packLitres('Royale Emulsion 4 L')).toBe(4)
    expect(packLitres('Apex 10 Ltr')).toBe(10)
    expect(packLitres('Primer 500 ml')).toBe(0.5)
    expect(packLitres('Brush 4 inch')).toBeNull()
  })

  it('picks the cheapest tins that hold enough', () => {
    const packs = [
      { id: '1', name: '1 L', litres: 1, price: 300 },
      { id: '4', name: '4 L', litres: 4, price: 1100 },
      { id: '10', name: '10 L', litres: 10, price: 2600 },
    ]
    // 6.7 L: 4 L + 3 × 1 L = ₹2,000 beats 2 × 4 L = ₹2,200 and 10 L = ₹2,600.
    expect(packMix(6.7, packs).map((x) => `${x.count}×${x.pack.litres}`)).toEqual(['1×4', '3×1'])
    // 9 L: 2 × 4 L + 1 × 1 L (₹2,500) is cheaper than one 10 L tin (₹2,600).
    expect(packMix(9, packs).map((x) => `${x.count}×${x.pack.litres}`)).toEqual(['2×4', '1×1'])
    expect(packMix(0, packs)).toEqual([])
  })
})

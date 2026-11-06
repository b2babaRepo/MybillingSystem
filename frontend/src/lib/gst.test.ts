import { garmentRate } from './gst'
import { describe, expect, it } from 'vitest'
import { gstSummary, gstUnsetAmount } from './gst'

describe('gstSummary', () => {
  it('takes GST out of inclusive prices by rate and splits it into CGST and SGST', () => {
    // 20 bags of cement at ₹385 (18%), half a brass of sand ₹3,250 (5%), transport ₹800 (no GST).
    const rows = gstSummary([{ amount: 7700, gstRate: 18 }, { amount: 3250, gstRate: 5 }, { amount: 800, gstRate: null }])
    expect(rows).toEqual([
      { rate: 5, amount: 3250, taxable: 3095.24, cgst: 77.38, sgst: 77.38 },
      { rate: 18, amount: 7700, taxable: 6525.42, cgst: 587.29, sgst: 587.29 },
    ])
  })

  it('is empty when no line has a GST rate', () => {
    expect(gstSummary([{ amount: 120, gstRate: null }, { amount: 40 }])).toEqual([])
  })

  it('keeps 0% lines as a row', () => {
    expect(gstSummary([{ amount: 100, gstRate: 0 }])).toEqual([{ rate: 0, amount: 100, taxable: 100, cgst: 0, sgst: 0 }])
  })

  it('adds up lines with no rate set, leaving out 0%', () => {
    expect(gstUnsetAmount([{ amount: 120, gstRate: null }, { amount: 40 }, { amount: 100, gstRate: 0 }])).toBe(160)
  })
})

describe('garmentRate', () => {
  it('charges 5% up to ₹2,500 a piece before GST and 18% above', () => {
    expect(garmentRate('6204', 5, 999)).toBe(5)
    expect(garmentRate('6204', 5, 2625)).toBe(5) // 2,625 ÷ 1.05 = 2,500
    expect(garmentRate('6203', 5, 2626)).toBe(18)
    expect(garmentRate('6109', 18, 1200)).toBe(5)
    expect(garmentRate('6402', 5, 4000)).toBe(18) // footwear too
  })
  it('leaves other items and deliberate rates alone', () => {
    expect(garmentRate('1001', 5, 5000)).toBe(5)
    expect(garmentRate('6204', 0, 5000)).toBe(0)
    expect(garmentRate(null, 5, 5000)).toBe(5)
    expect(garmentRate('5208', 5, 5000)).toBe(5) // fabric by the metre is not a garment
  })
})

import { garmentRate, billTax } from './gst'
import { isOutToday } from './local'
import { describe, expect, it } from 'vitest'
import { gstSummary, gstUnsetAmount } from './gst'

describe('isOutToday', () => {
  // 7 Nov 19:00 UTC is already 8 Nov in India.
  const now = new Date('2026-11-07T19:00:00Z')
  it('goes by the India date', () => {
    expect(isOutToday('2026-11-08', now)).toBe(true)
    expect(isOutToday('2026-11-07', now)).toBe(false)
  })
  it('is false when never marked', () => {
    expect(isOutToday(null, now)).toBe(false)
  })
})

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

describe('billTax', () => {
  it('takes the GST out of the price, line by line like the saved bill', () => {
    const bill = billTax([{ amount: 1050, gstRate: 5 }, { amount: 118, gstRate: 18 }, { amount: 118, gstRate: 18 }], false)
    expect(bill.rows).toEqual([
      { rate: 5, taxable: 1000, cgst: 25, sgst: 25, igst: 0 },
      { rate: 18, taxable: 200, cgst: 18, sgst: 18, igst: 0 },
    ])
    expect(bill.tax).toBe(86)
  })

  it('charges IGST alone on a sale to another state', () => {
    expect(billTax([{ amount: 1180, gstRate: 18 }], true).rows).toEqual([{ rate: 18, taxable: 1000, cgst: 0, sgst: 0, igst: 180 }])
  })

  it('splits an odd paisa the way the server rounds it', () => {
    // Tax of ₹0.03: half is 1.5 paise, which goes to the even 2, so CGST 0.02 and SGST 0.01.
    expect(billTax([{ amount: 0.63, gstRate: 5 }], false).rows[0]).toMatchObject({ taxable: 0.6, cgst: 0.02, sgst: 0.01 })
  })

  it('leaves lines with no rate out of the tax and says how much they come to', () => {
    const bill = billTax([{ amount: 200, gstRate: null }, { amount: 100, gstRate: 0 }], false)
    expect(bill).toMatchObject({ tax: 0, unset: 200 })
    expect(bill.rows).toEqual([{ rate: 0, taxable: 100, cgst: 0, sgst: 0, igst: 0 }])
  })
})

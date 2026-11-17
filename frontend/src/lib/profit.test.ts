import { describe, expect, it } from 'vitest'
import { unitProfit } from './profit'

describe('unitProfit', () => {
  it('takes GST out of the price before comparing with cost', () => {
    // ₹385 a bag with 18% GST is ₹326.27 before tax; bought at ₹300.
    expect(unitProfit(385, 300, 18)).toEqual({ amount: 26.27, percent: 8.1 })
  })
  it('works without GST and shows a loss as negative', () => {
    expect(unitProfit(50, 40, null)).toEqual({ amount: 10, percent: 20 })
    expect(unitProfit(35, 40, 0)).toEqual({ amount: -5, percent: -14.3 })
  })
})

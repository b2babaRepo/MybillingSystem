import { describe, expect, it } from 'vitest'
import { rateMessage, type MetalRate } from './jewellery'

const rate = (metal: 'gold' | 'silver', purity: number, sellPerGram: number): MetalRate => ({ metal, purity, sellPerGram, buyPerGram: null, setAt: '2026-11-24T05:00:00Z', derived: false })

describe('rateMessage', () => {
  it('quotes gold per 10 g and silver per kg, in Indian digit grouping', () => {
    const text = rateMessage([rate('gold', 999, 7820), rate('gold', 916, 7170), rate('silver', 999, 95)], 'Rates · 24 Nov', 'GST extra.', 'Silver')
    expect(text).toBe('Rates · 24 Nov\n\n24K (999): ₹78,200 / 10 g\n22K (916): ₹71,700 / 10 g\nSilver 999: ₹95,000 / kg\n\nGST extra.')
  })
})

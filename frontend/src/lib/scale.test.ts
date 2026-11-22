import { describe, expect, it } from 'vitest'
import { parseGrams, parseWeight, toInterval } from './scale'

describe('parseWeight', () => {
  it('reads the common scale formats', () => {
    expect(parseWeight('ST,GS,+  1.250kg')).toEqual({ kg: 1.25, stable: true })
    expect(parseWeight('  1250 g')).toEqual({ kg: 1.25, stable: true })
    expect(parseWeight('W: 0.875')).toEqual({ kg: 0.875, stable: true })
    expect(parseWeight('US,GS,+  0.900kg')).toEqual({ kg: 0.9, stable: false })
  })

  it('ignores a line with no number', () => {
    expect(parseWeight('-----')).toBeNull()
  })
})

describe('jewellery scale', () => {
  it('reads grams to the milligram, carats as grams, and rounds to the stamped interval', () => {
    expect(parseGrams('ST,GS,+  12.345 g')).toEqual({ grams: 12.345, stable: true })
    expect(parseGrams('+0012.34')).toEqual({ grams: 12.34, stable: true })
    expect(parseGrams('  1.50 ct')?.grams).toBeCloseTo(0.3)
    expect(toInterval(12.3456, 0.001)).toBe(12.346)
    expect(toInterval(12.345, 0.01)).toBe(12.35)
  })
})

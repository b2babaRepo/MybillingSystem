import { describe, expect, it } from 'vitest'
import { CODE128_PATTERNS, code128Modules, ean13Modules, normBarcode, readScaleLabel, sameItemCode, itemForBarcode } from './barcode'

describe('code128', () => {
  it('has 107 distinct symbols of 11 modules (stop: 13)', () => {
    expect(CODE128_PATTERNS).toHaveLength(107)
    expect(new Set(CODE128_PATTERNS).size).toBe(107)
    CODE128_PATTERNS.forEach((p, i) => expect([...p].reduce((s, w) => s + Number(w), 0)).toBe(i === 106 ? 13 : 11))
  })
  it('draws start, characters, check and stop, starting and ending on a bar', () => {
    const bits = code128Modules('SHIRT40')!
    expect(bits).toHaveLength(11 * (1 + 7 + 1) + 13)
    expect(bits.startsWith('11010010000')).toBe(true) // start B
    expect(bits.endsWith('1100011101011')).toBe(true) // stop
  })
  it('refuses characters set B lacks', () => {
    expect(code128Modules('')).toBeNull()
    expect(code128Modules('दाल')).toBeNull()
  })
  it('EAN-13 is 95 modules', () => expect(ean13Modules('8901030865336')).toHaveLength(95))
})

describe('normBarcode', () => {
  it('gives a 12-digit UPC-A its leading 0, so it matches the 13-digit scan', () => {
    expect(normBarcode('012345678905')).toBe('0012345678905')
    expect(normBarcode('0012345678905')).toBe('0012345678905')
  })
  it('keeps only letters and digits', () => {
    expect(normBarcode(' 8901-0308 65336 ')).toBe('8901030865336')
    expect(normBarcode('SHIRT-40')).toBe('SHIRT40')
    expect(normBarcode(null)).toBe('')
  })
})

describe('readScaleLabel', () => {
  it('reads the item code and weight in grams', () => {
    expect(readScaleLabel('2100123012503', '21PPPPPWWWWWC')).toEqual({ itemCode: '00123', kg: 1.25 })
  })
  it('reads a price label in paise', () => {
    // 20 + item 00045 + 1234 paise (₹12.34)
    expect(readScaleLabel('2000045012349', '2xPPPPPRRRRRC')).toEqual({ itemCode: '00045', price: 12.34 })
  })
  it('refuses a wrong check digit, another prefix, no layout, or a pack barcode', () => {
    expect(readScaleLabel('2100123012504', '21PPPPPWWWWWC')).toBeNull()
    expect(readScaleLabel('2200123012503', '21PPPPPWWWWWC')).toBeNull()
    expect(readScaleLabel('2100123012503', null)).toBeNull()
    expect(readScaleLabel('8901030865278', '21PPPPPWWWWWC')).toBeNull()
  })
  it('matches item codes with or without leading zeros, never a pack barcode', () => {
    expect(sameItemCode('123', '00123')).toBe(true)
    expect(sameItemCode('00123', '00123')).toBe(true)
    expect(sameItemCode('124', '00123')).toBe(false)
    expect(sameItemCode('8901030865278', '00123')).toBe(false)
    expect(sameItemCode(null, '00123')).toBe(false)
  })
})

describe('itemForBarcode', () => {
  const items = [{ id: 'oil', barcode: '8901030865275', extraBarcodes: ['8901030865282'] }, { id: 'soap', barcode: null, extraBarcodes: null }]
  it('finds an item by its main or an extra code, in either UPC spelling', () => {
    expect(itemForBarcode(items, '8901030865275')?.id).toBe('oil')
    expect(itemForBarcode(items, '8901030865282')?.id).toBe('oil')
    expect(itemForBarcode([{ barcode: '0012345678905' }], '012345678905')).toBeDefined()
    expect(itemForBarcode(items, '8901030865299')).toBeUndefined()
    expect(itemForBarcode(items, '12')).toBeUndefined()
  })
})

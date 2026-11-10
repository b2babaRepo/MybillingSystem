import { describe, expect, it } from 'vitest'
import { normBarcode } from './barcode'

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

import { describe, expect, it } from 'vitest'
import { decimalText } from './decimalText'

describe('decimalText', () => {
  it('keeps digits while typing a decimal', () => {
    expect(decimalText('2')).toBe('2')
    expect(decimalText('12.')).toBe('12.')
    expect(decimalText('12.50')).toBe('12.50')
    expect(decimalText('0.250')).toBe('0.250')
  })

  it('reads a comma as the decimal point', () => {
    expect(decimalText('12,50')).toBe('12.50')
  })

  it('drops other characters and extra points', () => {
    expect(decimalText('₹45.75')).toBe('45.75')
    expect(decimalText('1.2.3')).toBe('1.23')
    expect(decimalText('abc')).toBe('')
  })
})

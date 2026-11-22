import { describe, expect, it } from 'vitest'
import { rupeesInWords, wholeInWords } from './inWords'

describe('rupeesInWords', () => {
  it('writes lakhs and crores', () => {
    expect(wholeInWords(0)).toBe('Zero')
    expect(wholeInWords(15)).toBe('Fifteen')
    expect(wholeInWords(105)).toBe('One Hundred Five')
    expect(wholeInWords(125050)).toBe('One Lakh Twenty Five Thousand Fifty')
    expect(wholeInWords(10_00_00_000)).toBe('Ten Crore')
    expect(wholeInWords(9_99_99_999)).toBe('Nine Crore Ninety Nine Lakh Ninety Nine Thousand Nine Hundred Ninety Nine')
  })
  it('adds paise and Only', () => {
    expect(rupeesInWords(208)).toBe('Rupees Two Hundred Eight Only')
    expect(rupeesInWords(1250.5)).toBe('Rupees One Thousand Two Hundred Fifty and Fifty Paise Only')
    expect(rupeesInWords(0.07)).toBe('Rupees Zero and Seven Paise Only')
  })
})

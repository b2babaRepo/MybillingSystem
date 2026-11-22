import { describe, expect, it } from 'vitest'
import { loose, nameMatches, splitQuantity, toLatin } from './nameMatch'

describe('nameMatch', () => {
  it('spells Hindi in Latin letters', () => {
    expect(toLatin('आटा')).toBe('aataa')
    expect(toLatin('चीनी')).toBe('cheenee')
  })

  it('finds a Hindi name from a typed Latin word and the other way round', () => {
    expect(nameMatches('आटा', 'aata')).toBe(true)
    expect(nameMatches('आटा', 'atta')).toBe(true)
    expect(nameMatches('चीनी 1 किलो', 'cheeni')).toBe(true)
    expect(nameMatches('Basmati Rice', 'बासमती')).toBe(true)
    expect(nameMatches('Tata Salt', 'ta sa')).toBe(true)
    expect(nameMatches('मटर', 'aata')).toBe(false)
    expect(nameMatches('Atta', '')).toBe(false)
  })

  it('treats spelling variants alike', () => {
    expect(loose('Cheeni')).toBe(loose('chini'))
  })

  it('reads a quantity before the item', () => {
    expect(splitQuantity('2*aata')).toEqual({ quantity: 2, rest: 'aata' })
    expect(splitQuantity('1.5 x चीनी')).toEqual({ quantity: 1.5, rest: 'चीनी' })
    expect(splitQuantity('3*8901234567890')).toEqual({ quantity: 3, rest: '8901234567890' })
    expect(splitQuantity('aata')).toEqual({ quantity: null, rest: 'aata' })
    expect(splitQuantity('0*aata')).toEqual({ quantity: null, rest: '0*aata' })
  })
})

import { describe, expect, it } from 'vitest'
import { itemName, nameMatches } from './itemName'

const atta = { name: 'Atta', nameEn: 'Atta', nameHi: 'आटा' }

describe('itemName', () => {
  it('shows the list name in the language being shown', () => {
    expect(itemName(atta, 'hi')).toBe('आटा')
    expect(itemName(atta, 'en')).toBe('Atta')
    expect(itemName({ ...atta, name: 'आटा' }, 'en')).toBe('Atta')
    expect(itemName(atta, 'gu')).toBe('Atta')
  })

  it("keeps a name the shop typed or changed", () => {
    expect(itemName({ ...atta, name: 'Chakki fresh atta' }, 'hi')).toBe('Chakki fresh atta')
    expect(itemName({ name: 'Local namkeen' }, 'hi')).toBe('Local namkeen')
  })

  it('falls back to the stored name when the list has none for that language', () => {
    expect(itemName({ name: 'Pipe', nameEn: 'Pipe', nameHi: null }, 'hi')).toBe('Pipe')
  })

  it('finds an item by any of its names', () => {
    expect(nameMatches(atta, 'आट')).toBe(true)
    expect(nameMatches({ ...atta, name: 'आटा' }, 'att')).toBe(true)
    expect(nameMatches(atta, 'rice')).toBe(false)
  })
})

import { describe, expect, it } from 'vitest'
import { packGroups, packStem, sizeOf } from './packGroups'

describe('packGroups', () => {
  it('finds the item behind a pack size', () => {
    expect(packStem('मैगी केचप 1 kg')).toBe('मैगी केचप')
    expect(packStem('मैगी केचप 200 g')).toBe('मैगी केचप')
    expect(packStem('Godrej No.1 Sandal/Lime/Jasmine 4x100 g')).toBe('Godrej No.1 Sandal/Lime/Jasmine')
    expect(packStem('Mangaldeep Sandal Rs10')).toBe('Mangaldeep Sandal')
    expect(packStem('आटा')).toBe('आटा')
    expect(sizeOf('मैगी केचप 500 g', 'मैगी केचप')).toBe('500 g')
  })

  it('keeps sizes together in order of first appearance', () => {
    const groups = packGroups([{ name: 'Ketchup 1 kg' }, { name: 'Atta' }, { name: 'Ketchup 200 g' }, { name: 'Ketchup 500 g' }])
    expect(groups.map((g) => [g.stem, g.items.length])).toEqual([['Ketchup', 3], ['Atta', 1]])
  })
})

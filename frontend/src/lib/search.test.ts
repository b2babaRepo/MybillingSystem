import { describe, expect, it } from 'vitest'
import { distance, rank, score } from './search'

const names = ['Amul Taaza Milk 500 ml', 'Sugar 1 kg', 'Basmati Rice', 'Aashirvaad Atta 5kg', 'Gold Jhumka 22K', 'Parle-G Biscuit']

describe('forgiving search', () => {
  it('finds everyday words in any script', () => {
    expect(rank(names, 'doodh', (x) => x)).toEqual(['Amul Taaza Milk 500 ml'])
    expect(rank(names, 'cheeni', (x) => x)).toEqual(['Sugar 1 kg'])
    expect(rank(names, 'चावल', (x) => x)).toEqual(['Basmati Rice'])
    expect(rank(names, 'aata', (x) => x)).toEqual(['Aashirvaad Atta 5kg'])
  })

  it('forgives a letter or two', () => {
    expect(distance('biscut', 'biscuit')).toBe(1)
    expect(score('Parle-G Biscuit', 'biscit')).toBeGreaterThan(0)
    expect(score('Basmati Rice', 'basmti')).toBeGreaterThan(0)
    expect(score('Sugar 1 kg', 'milk')).toBe(0)
  })

  it('ranks a word start above a match inside a word', () => {
    expect(rank(['Toor Dal', 'Sandalwood soap'], 'dal', (x) => x)[0]).toBe('Toor Dal')
  })
})

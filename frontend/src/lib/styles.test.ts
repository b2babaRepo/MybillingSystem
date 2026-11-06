import { describe, expect, it } from 'vitest'
import { compareSizes, groupStyles } from './styles'
import type { Product } from './types'

describe('compareSizes', () => {
  it('orders letter, number, bra and kids sizes the way a shop does', () => {
    expect(['XL', 'S', 'M', 'XXL', 'L'].sort(compareSizes)).toEqual(['S', 'M', 'L', 'XL', 'XXL'])
    expect(['34', '30', '32', '28'].sort(compareSizes)).toEqual(['28', '30', '32', '34'])
    expect(['34B', '32C', '32B', '36B'].sort(compareSizes)).toEqual(['32B', '32C', '34B', '36B'])
    expect(['2-3Y', '6-12M', '0-3M', '3-6M'].sort(compareSizes)).toEqual(['0-3M', '3-6M', '6-12M', '2-3Y'])
  })
})

describe('groupStyles', () => {
  const p = (id: string, name: string, styleName: string | null, size: string | null) =>
    ({ id, name, styleName, size, colour: null } as unknown as Product)
  it('groups a style once, in size order, and leaves other items alone', () => {
    const out = groupStyles([p('1', 'Jeans 34', 'Jeans', '34'), p('2', 'Atta', null, null), p('3', 'Jeans 30', 'Jeans', '30')])
    expect(out.map((e) => e.kind)).toEqual(['style', 'item'])
    expect(out[0].kind === 'style' && out[0].items.map((i) => i.size)).toEqual(['30', '34'])
  })
})

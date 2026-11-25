import { describe, expect, it } from 'vitest'
import { matchPrices, readPriceList } from './priceList'

describe('price list from a file', () => {
  it('reads a header row, quotes and rupee signs', () => {
    const { rows, skipped } = readPriceList('Item Name,Pack,New Rate\n"Polycab Wire 1.5 sq mm, 90 m",1,"₹1,850"\nFinolex Pipe 1/2,3 m,145\nNo price here,,\n')
    expect(rows).toEqual([{ key: 'Polycab Wire 1.5 sq mm, 90 m', price: 1850 }, { key: 'Finolex Pipe 1/2', price: 145 }])
    expect(skipped).toBe(1)
  })

  it('reads a list with no header: item, then the first number', () => {
    expect(readPriceList('Royale 4 L\t1250\nApex 10 L\t2600').rows).toEqual([{ key: 'Royale 4 L', price: 1250 }, { key: 'Apex 10 L', price: 2600 }])
  })

  it('matches by barcode, then by name ignoring case and spaces', () => {
    const items = [{ id: 'a', name: 'Royale 4 L', barcode: '8901234' }, { id: 'b', name: 'Apex  10 L', barcode: null }]
    const { matched, unmatched } = matchPrices([{ key: '8901234', price: 1300 }, { key: 'apex 10 l', price: 2700 }, { key: 'Tractor 1 L', price: 300 }], items)
    expect(matched.map((m) => [m.item.id, m.price])).toEqual([['a', 1300], ['b', 2700]])
    expect(unmatched).toEqual(['Tractor 1 L'])
  })
})

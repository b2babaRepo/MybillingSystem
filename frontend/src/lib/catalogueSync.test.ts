import { describe, expect, it } from 'vitest'
import { barcodeIndex } from './barcode'
import { applyChanges } from './catalogueSync'
import type { Product } from './types'

const item = (id: string, name: string, category: string | null, sellingPrice = 10) =>
  ({ id, name, category, sellingPrice, unit: 'piece', costPrice: null, stock: 1, reorderLevel: null, isLow: false }) as Product

describe('applyChanges', () => {
  const kept = [item('a', 'Atta', 'Grain'), item('b', 'Biscuit', 'Snacks'), item('c', 'Candle', null)]
  it('replaces changed items, adds new ones, drops removed ones, in the server order', () => {
    const next = applyChanges(kept, { at: 'x', full: false, changed: [item('b', 'Biscuit', 'Snacks', 12), item('d', 'Dal', 'Grain')], removed: ['a'] })
    expect(next.map((p) => p.id)).toEqual(['d', 'b', 'c'])
    expect(next.find((p) => p.id === 'b')?.sellingPrice).toBe(12)
  })
  it('takes a whole list as it is', () => {
    expect(applyChanges(kept, { at: 'x', full: true, changed: [item('z', 'Zeera', null)], removed: [] }).map((p) => p.id)).toEqual(['z'])
  })
})

describe('barcodeIndex', () => {
  it('finds items by main and extra codes, in either UPC spelling', () => {
    const index = barcodeIndex([{ id: 1, barcode: '012345678905', extraBarcodes: ['8901030865282'] }, { id: 2, barcode: '12' }])
    expect(index.get('0012345678905')?.id).toBe(1)
    expect(index.get('8901030865282')?.id).toBe(1)
    expect(index.has('12')).toBe(false)
  })
})

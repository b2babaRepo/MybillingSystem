import { describe, expect, it } from 'vitest'
import { kgOf } from '../screens/SupplierOrders'
import type { Product } from './types'

const item = (unit: string, weightKg?: number) => ({ id: 'x', name: 'x', unit, weightKg }) as unknown as Product

describe('kgOf', () => {
  it('counts every weight unit, not only kg', () => {
    expect(kgOf(item('kg'), 50)).toBe(50)
    expect(kgOf(item('g'), 500)).toBe(0.5)
    expect(kgOf(item('quintal'), 2)).toBe(200)
    expect(kgOf(item('tonne'), 1)).toBe(1000)
  })

  it('weighs pieces only when their weight is known', () => {
    expect(kgOf(item('piece', 2), 10)).toBe(20)
    expect(kgOf(item('piece'), 10)).toBeNull()
    expect(kgOf(undefined, 10)).toBeNull()
  })
})

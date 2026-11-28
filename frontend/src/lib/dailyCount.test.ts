import { afterEach, describe, expect, it, vi } from 'vitest'
import { countDoneToday, markCountDone, todaysCategory } from './dailyCount'
import type { Product } from './types'

const item = (category: string | null, extra: Partial<Product> = {}) => ({ id: Math.random().toString(), name: 'x', category, unit: 'kg', sellingPrice: 1, costPrice: 1, stock: 1, reorderLevel: null, isLow: false, ...extra }) as Product

afterEach(() => vi.useRealTimers())

describe('daily count', () => {
  it('takes the categories in turn, largest first, one a day', () => {
    const products = [item('Dal'), item('Dal'), item('Dal'), item('Masala'), item('Masala'), item('Tea'), item(null), item('Soap', { isService: true })]
    vi.useFakeTimers()
    const days = [0, 1, 2, 3].map((d) => { vi.setSystemTime(new Date(Date.UTC(2026, 10, 30 + d, 6))); return todaysCategory(products)?.name })
    expect(new Set(days)).toEqual(new Set(['Dal', 'Masala', 'Tea']))
    expect(days[0]).not.toEqual(days[1])
  })

  it('has nothing to count with no categories', () => {
    expect(todaysCategory([item(null)])).toBeNull()
  })

  it('remembers today’s count is done', () => {
    // A stand-in for the phone's storage (tests run without a browser).
    const store = new Map<string, string>()
    vi.stubGlobal('localStorage', { getItem: (k: string) => store.get(k) ?? null, setItem: (k: string, v: string) => void store.set(k, v), removeItem: (k: string) => void store.delete(k) })
    expect(countDoneToday('shop-a')).toBe(false)
    markCountDone('shop-a')
    expect(countDoneToday('shop-a')).toBe(true)
  })
})

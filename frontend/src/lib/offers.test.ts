import { describe, expect, it } from 'vitest'
import { bestOffer, isRunning, offerPrice, type SaleOffer } from './offers'

const now = Date.parse('2026-11-10T06:00:00Z')
const offer = (over: Partial<SaleOffer>): SaleOffer => ({
  id: 'o', name: 'Sale', kind: 'percent', value: 10, startsAt: '2026-11-09T18:30:00Z', endsAt: '2026-11-20T18:30:00Z', scope: 'all',
  category: null, productIds: [], stoppedAt: null, stats: null, ...over,
})

describe('offerPrice', () => {
  it('takes a percent or rupees off each piece, never below zero', () => {
    expect(offerPrice({ kind: 'percent', value: 30 }, 999)).toBe(699.3)
    expect(offerPrice({ kind: 'amount', value: 50 }, 120)).toBe(70)
    expect(offerPrice({ kind: 'amount', value: 500 }, 120)).toBe(0)
  })
})

describe('bestOffer', () => {
  const kurta = { id: 'k', category: 'Kurtas', sellingPrice: 1000 }

  it('picks the running offer that gives the lowest price, by category or chosen items', () => {
    const offers = [
      offer({ id: 'all', value: 10 }),
      offer({ id: 'cat', value: 30, scope: 'category', category: 'kurtas' }),
      offer({ id: 'other', value: 50, scope: 'items', productIds: ['x'] }),
    ]
    expect(bestOffer(offers, kurta, now)?.id).toBe('cat')
  })

  it('leaves out offers not yet started, ended or stopped', () => {
    expect(isRunning(offer({ startsAt: '2026-11-11T00:00:00Z' }), now)).toBe(false)
    expect(isRunning(offer({ stoppedAt: '2026-11-10T05:00:00Z' }), now)).toBe(false)
    expect(bestOffer([offer({ endsAt: '2026-11-10T05:00:00Z' })], kurta, now)).toBeNull()
  })
})

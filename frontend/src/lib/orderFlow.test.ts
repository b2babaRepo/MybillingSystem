import { describe, expect, it } from 'vitest'
import { billOf, canChange, deliveryFee, reachedStep, stepsOf } from './orderFlow'

const shop = { deliveryFee: 20, freeDeliveryAbove: 500, minOrder: 100, slots: [] }

describe('the honest bill', () => {
  it('charges delivery below the free amount, never for pickup', () => {
    expect(deliveryFee(shop, 'delivery', 150)).toBe(20)
    expect(deliveryFee(shop, 'delivery', 500)).toBe(0)
    expect(deliveryFee(shop, 'pickup', 150)).toBe(0)
    expect(deliveryFee({ ...shop, deliveryFee: null }, 'delivery', 150)).toBe(0)
  })

  it('shows the saving, the charge, the total and what is left to reach the minimum', () => {
    const bill = billOf([{ price: 45, quantity: 2, was: 50 }, { price: 10, quantity: 1 }], shop, 'delivery')
    expect(bill).toEqual({ items: 100, saved: 10, fee: 20, total: 120, short: 0, toFree: 400 })
    expect(billOf([{ price: 30, quantity: 2 }], shop, 'pickup')).toMatchObject({ items: 60, fee: 0, total: 60, short: 40, toFree: 0 })
  })
})

describe('order steps', () => {
  it('follow pickup or delivery', () => {
    expect(stepsOf({ fulfilment: 'pickup' })[3]).toBe('ready_for_pickup')
    expect(stepsOf({ fulfilment: 'delivery' })[3]).toBe('out_for_delivery')
    expect(reachedStep({ fulfilment: 'delivery', status: 'out_for_delivery' })).toBe(3)
    expect(reachedStep({ fulfilment: 'pickup', status: 'packed' })).toBe(2)
    expect(reachedStep({ fulfilment: 'pickup', status: 'cancelled' })).toBe(-1)
  })

  it('let the customer change the order only before packing', () => {
    expect(canChange('placed')).toBe(true)
    expect(canChange('accepted')).toBe(true)
    expect(canChange('packed')).toBe(false)
  })
})

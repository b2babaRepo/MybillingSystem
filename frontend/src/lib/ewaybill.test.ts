import { describe, expect, it } from 'vitest'
import { ewayBillJson, pincodeIn } from './ewaybill'
import type { BillView } from './types'

const bill = {
  id: '01a1b71e-0000-7000-8000-000000000001', customerId: null, total: 59000, paidAmount: 59000, onKhata: 0, paymentMode: 'upi',
  occurredAt: '2026-11-09T20:00:00Z', customerBalance: null, billNo: '2026-27/0042', gstin: '24AAAAA0000A1Z5', placeOfSupply: '24',
  shopAddress: 'MG Road, Ahmedabad 380001', buyerGstin: null,
  lines: [{ productId: null, name: 'Cement 50kg', quantity: 100, unit: 'bag', unitPrice: 590, amount: 59000, gstRate: 18, hsnCode: '2523' }],
} as BillView['bill']
const trip = { fromPincode: '380001', toPincode: '380015', distanceKm: 12, vehicleNo: 'gj01 ab 1234', buyerName: 'Ramesh' }

describe('ewayBillJson', () => {
  it('builds the portal file for an in-state bill', () => {
    const result = ewayBillJson('Shree Cement Depot', bill, trip)
    if (!('json' in result)) throw new Error(result.problems.join(' '))
    const b = (result.json as { billLists: Record<string, unknown>[] }).billLists[0]
    expect(b.docDate).toBe('10/11/2026') // India time
    expect(b.toGstin).toBe('URP')
    expect(b.vehicleNo).toBe('GJ01AB1234')
    expect(b.totalValue).toBe(50000)
    expect([b.cgstValue, b.sgstValue, b.igstValue]).toEqual([4500, 4500, 0])
    expect((b.itemList as Record<string, unknown>[])[0]).toMatchObject({ hsnCode: 2523, qtyUnit: 'BAG', cgstRate: 9, sgstRate: 9 })
  })

  it('says what is missing', () => {
    const result = ewayBillJson('Shop', { ...bill, lines: [{ ...bill.lines[0], hsnCode: null }] }, { ...trip, toPincode: '12' })
    expect('problems' in result && result.problems.length).toBe(2)
  })

  it('finds a pincode in an address', () => {
    expect(pincodeIn('MG Road, Ahmedabad 380001')).toBe('380001')
    expect(pincodeIn(null)).toBe('')
  })
})

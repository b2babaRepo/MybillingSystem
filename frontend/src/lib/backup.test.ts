import { describe, expect, it } from 'vitest'
import { chunks, daysSince, parseBackup } from './backup'

const file = {
  format: 'grocery-khata-backup', version: 1, exportedAt: '2026-11-10T10:00:00Z',
  shop: { name: 'Sharma Kirana', slug: 'sharma' },
  customers: [{ name: 'Ramesh', phone: '9876543210', balance: 1200 }, { name: ' ', phone: null, balance: 5 }, { name: 'Suresh', phone: null, balance: -300 }],
  suppliers: [{ name: 'Gupta Traders', phone: null, gstin: '24ABCDE1234F1Z5', balance: 5000 }],
  products: [
    { name: 'Atta', unit: 'kg', sellingPrice: 42, costPrice: 38, category: 'Atta', stock: 25.5, reorderLevel: 10, isActive: true, gstRate: 0 },
    { name: 'Old soap', unit: 'piece', sellingPrice: 30, stock: 4, isActive: false },
    { name: 'Haircut', unit: 'piece', sellingPrice: 100, stock: 0, isService: true },
    { name: 'Sugar', unit: 'kg', sellingPrice: 44, stock: -2 },
  ],
  bills: [{}, {}], ledgerEntries: [{}, {}, {}], expenses: [],
}

describe('parseBackup', () => {
  it('reads customers, suppliers and active items as opening figures', () => {
    const b = parseBackup(JSON.stringify(file))
    if ('error' in b) throw new Error(b.error)
    expect(b.shopName).toBe('Sharma Kirana')
    expect(b.customers).toEqual([{ name: 'Ramesh', phone: '9876543210', balance: 1200 }, { name: 'Suresh', phone: null, balance: -300 }])
    expect(b.suppliers[0]).toEqual({ name: 'Gupta Traders', phone: null, gstin: '24ABCDE1234F1Z5', balance: 5000 })
    expect(b.products.map((p) => p.name)).toEqual(['Atta', 'Haircut', 'Sugar'])
    expect(b.products[0]).toMatchObject({ unit: 'kg', sellingPrice: 42, costPrice: 38, openingStock: 25.5, reorderLevel: 10, gstRate: 0 })
    expect(b.products[1].openingStock).toBeNull() // a service has no stock
    expect(b.products[2].openingStock).toBeNull() // a shelf in the minus starts at nothing
    expect(b.history).toEqual({ bills: 2, entries: 3, expenses: 0 })
  })

  it('refuses files that are not backups', () => {
    expect(parseBackup('not json')).toEqual({ error: 'backup.notJson' })
    expect(parseBackup(JSON.stringify({ format: 'something-else' }))).toEqual({ error: 'backup.notBackup' })
    expect(parseBackup(JSON.stringify([1, 2]))).toEqual({ error: 'backup.notBackup' })
  })

  it('still reads a file from before the format mark', () => {
    const { format: _, version: __, ...old } = file
    const b = parseBackup(JSON.stringify(old))
    expect('error' in b).toBe(false)
  })
})

describe('helpers', () => {
  it('splits rows into batches', () => {
    expect(chunks([1, 2, 3, 4, 5], 2)).toEqual([[1, 2], [3, 4], [5]])
    expect(chunks([], 2)).toEqual([])
  })
  it('counts whole days since a backup', () => {
    expect(daysSince(null)).toBeNull()
    expect(daysSince('2026-11-10T10:00:00Z', new Date('2026-11-18T09:00:00Z'))).toBe(7)
  })
})

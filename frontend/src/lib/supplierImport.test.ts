import { describe, expect, it } from 'vitest'
import { parseOwed, parseSuppliers } from './supplierImport'

describe('parseOwed', () => {
  it('reads a supplier balance the way books write it', () => {
    expect(parseOwed('₹4,500')).toBe(4500)
    expect(parseOwed('4500 Cr')).toBe(4500)
    expect(parseOwed('200 Dr')).toBe(-200)
    expect(parseOwed('-200')).toBe(-200)
    expect(parseOwed('')).toBe(0)
    expect(parseOwed('abc')).toBeNull()
  })
})

describe('parseSuppliers', () => {
  it('follows a header in any order', () => {
    const { rows, errors } = parseSuppliers('GSTIN,Supplier name,Balance,Mobile\n08ABCDE1234F1Z0,Jain Traders,4500 Cr,9811177701\n,Gupta Pipes,200 Dr,')
    expect(errors).toEqual([])
    expect(rows).toEqual([
      { name: 'Jain Traders', phone: '9811177701', gstin: '08ABCDE1234F1Z0', balance: 4500 },
      { name: 'Gupta Pipes', phone: null, gstin: null, balance: -200 },
    ])
  })

  it('finds the phone, GSTIN and amount without a header, and reports lines it cannot read', () => {
    const { rows, errors } = parseSuppliers('Jain Traders, 08abcde1234f1z0, 98111 77701, 1200\n,no name\nGupta Pipes, oops-amount')
    // A name with words that aren't an amount is still a supplier, owed nothing.
    expect(rows).toEqual([
      { name: 'Jain Traders', phone: '98111 77701', gstin: '08ABCDE1234F1Z0', balance: 1200 },
      { name: 'Gupta Pipes', phone: null, gstin: null, balance: 0 },
    ])
    expect(errors).toEqual([2])
  })
})

import { describe, expect, it } from 'vitest'
import { parseBalance, parseParties } from './partyImport'

describe('parseBalance', () => {
  it('reads what shops write as owed', () => {
    expect(parseBalance('₹1,200.50')).toBe(1200.5)
    expect(parseBalance('1200 Dr')).toBe(1200)
    expect(parseBalance('Rs. 500')).toBe(500)
  })

  it('treats credit and negatives as an advance', () => {
    expect(parseBalance('300 Cr')).toBe(-300)
    expect(parseBalance('(300)')).toBe(-300)
    expect(parseBalance('-300')).toBe(-300)
    expect(parseBalance('300', 'Cr')).toBe(-300)
  })

  it('takes empty as nothing owed, and rejects words', () => {
    expect(parseBalance('')).toBe(0)
    expect(parseBalance('unknown')).toBeNull()
  })
})

describe('parseParties', () => {
  it('follows a header in any order', () => {
    const { rows, errors } = parseParties('Mobile,Balance,Party Name\n9876543210,"₹1,200",Ramesh Kumar')
    expect(rows).toEqual([{ name: 'Ramesh Kumar', phone: '9876543210', balance: 1200 }])
    expect(errors).toEqual([])
  })

  it('reads a separate Dr/Cr column', () => {
    expect(parseParties('Name,Amount,Type\nSuresh,300,Cr').rows[0].balance).toBe(-300)
  })

  it('works out columns with no header, pasted from Excel', () => {
    expect(parseParties('Ramesh\t9876543210\t450\nSuresh\t200').rows).toEqual([
      { name: 'Ramesh', phone: '9876543210', balance: 450 },
      { name: 'Suresh', phone: null, balance: 200 },
    ])
  })

  it('reports lines it cannot read by line number', () => {
    const { rows, errors } = parseParties('Ramesh,9876543210,450\nSuresh,not a number')
    expect(rows).toHaveLength(1)
    expect(errors).toEqual([2])
  })
})

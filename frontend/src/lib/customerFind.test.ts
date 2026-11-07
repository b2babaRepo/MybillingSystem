import { describe, expect, it } from 'vitest'
import { changeDue, findCustomers } from './customerFind'

const people = [
  { name: 'Ramesh Kumar', phone: '9876543210' },
  { name: 'Suresh Verma', phone: '9812344321' },
  { name: 'Delhi Builders', phone: null },
]

describe('findCustomers', () => {
  it('finds by part of a name', () => {
    expect(findCustomers(people, 'verma').map((c) => c.name)).toEqual(['Suresh Verma'])
  })

  it('finds by the last digits of a phone', () => {
    expect(findCustomers(people, '3210').map((c) => c.name)).toEqual(['Ramesh Kumar'])
  })

  it('does not trip on customers with no phone', () => {
    expect(findCustomers(people, '0000')).toEqual([])
  })

  it('shows the first few when nothing is typed', () => {
    expect(findCustomers(people, '  ', 2)).toHaveLength(2)
  })
})

describe('changeDue', () => {
  it('is what to hand back', () => {
    expect(changeDue(500, 340.5)).toBe(159.5)
  })

  it('is negative when the cash falls short', () => {
    expect(changeDue(100, 150)).toBe(-50)
  })
})

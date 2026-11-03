import { describe, expect, it } from 'vitest'
import { parseCsv } from './catalogue'

describe('parseCsv', () => {
  it('reads every unit a trade sells by, in English and Hindi', () => {
    const text = [
      'name, unit, price, cost, category, stock, reorder level',
      'Teak veneer, sq_ft, 85, 62',
      'Sheesham plank, cft, 2800, 2300',
      'Teak beading, running_ft, 28, 20',
      'Sofa fabric, metre, 450, 320',
      'Soft-close hinge, pair, 260, 190',
      'Screws, box, 140, 105',
      'Cement, bag, 400',
      'कीकर लकड़ी, घन फुट, 1100',
    ].join('\n')
    const { rows, errors } = parseCsv(text)
    expect(errors).toEqual([])
    expect(rows.map((r) => r.unit)).toEqual(['sq_ft', 'cft', 'running_ft', 'metre', 'pair', 'box', 'bag', 'cft'])
  })

  it('still flags a unit it does not know', () => {
    expect(parseCsv('name, unit, price\nTest item, sheets, 100').errors).toHaveLength(1)
  })
})

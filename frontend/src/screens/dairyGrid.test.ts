import { describe, expect, it } from 'vitest'
import { parseGrid } from './DairyScreen'

describe('parseGrid', () => {
  it('reads a chart copied from Excel: SNF across the top, fat down the side', () => {
    const cells = parseGrid('FAT\t8.9\t9.0\n6.0\t48.10\t48.60\n6.1\t48.70\t\n')
    expect(cells).toEqual([
      { fat: 6, snf: 8.9, ratePerLitre: 48.1 },
      { fat: 6, snf: 9, ratePerLitre: 48.6 },
      { fat: 6.1, snf: 8.9, ratePerLitre: 48.7 },
    ])
  })

  it('takes commas too and skips rows that are not numbers', () => {
    expect(parseGrid('fat,8.5\nnote,x\n4.0,30')).toEqual([{ fat: 4, snf: 8.5, ratePerLitre: 30 }])
  })
})

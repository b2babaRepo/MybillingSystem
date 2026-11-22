import { describe, expect, it } from 'vitest'
import { LABEL_SHEETS, labelPageRule, pages } from './labels'

const sheet = (id: string) => LABEL_SHEETS.find((s) => s.id === id)!

describe('label sheets', () => {
  it('fit on A4 (210 x 297 mm)', () => {
    for (const s of LABEL_SHEETS.filter((x) => !x.roll)) {
      expect(s.left * 2 + s.cols * s.w + (s.cols - 1) * s.gapX).toBeLessThanOrEqual(210.5)
      expect(s.top + s.rows * s.h + (s.rows - 1) * s.gapY).toBeLessThanOrEqual(297.5)
    }
  })

  it('split labels a sheet at a time, after the spots already used', () => {
    const a4 = sheet('a4-24')
    const out = pages(Array.from({ length: 30 }, (_, i) => i), a4, 5)
    expect(out.map((p) => p.length)).toEqual([24, 11])
    expect(out[0].slice(0, 6)).toEqual([null, null, null, null, null, 0])
  })

  it('put one label on each roll page and size the page to it', () => {
    const roll = sheet('roll-50x25')
    expect(pages([1, 2, 3], roll, 4)).toEqual([[1], [2], [3]])
    expect(labelPageRule(roll)).toBe('@page { size: 50mm 25mm; margin: 0; }')
    expect(labelPageRule(sheet('a4-65'))).toContain('A4')
  })
})

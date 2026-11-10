import { describe, expect, it } from 'vitest'
import { parseWeight } from './scale'

describe('parseWeight', () => {
  it('reads the common scale formats', () => {
    expect(parseWeight('ST,GS,+  1.250kg')).toEqual({ kg: 1.25, stable: true })
    expect(parseWeight('  1250 g')).toEqual({ kg: 1.25, stable: true })
    expect(parseWeight('W: 0.875')).toEqual({ kg: 0.875, stable: true })
    expect(parseWeight('US,GS,+  0.900kg')).toEqual({ kg: 0.9, stable: false })
  })

  it('ignores a line with no number', () => {
    expect(parseWeight('-----')).toBeNull()
  })
})

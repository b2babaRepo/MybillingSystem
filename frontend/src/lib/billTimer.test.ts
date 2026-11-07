import { describe, expect, it } from 'vitest'
import { billSeconds } from './billTimer'

describe('billSeconds', () => {
  it('rounds to whole seconds', () => {
    expect(billSeconds(1_000, 19_600)).toBe(19)
  })

  it('has nothing for a bill that was not timed', () => {
    expect(billSeconds(null, 5_000)).toBeNull()
    expect(billSeconds(0, 5_000)).toBeNull()
  })

  it('drops a bill left open', () => {
    expect(billSeconds(1_000, 1_000 + 31 * 60_000)).toBeNull()
  })

  it('drops a clock that went backwards', () => {
    expect(billSeconds(5_000, 1_000)).toBeNull()
  })
})

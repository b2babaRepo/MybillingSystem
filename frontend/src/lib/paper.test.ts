import { describe, expect, it } from 'vitest'
import { pageRule } from './paper'

describe('pageRule', () => {
  it('sizes a roll to its width and the length of the bill, in real lengths a browser accepts', () => {
    expect(pageRule('58', 171.2)).toBe('@page { size: 58mm 172mm; margin: 0; }')
    expect(pageRule('80', 90)).toBe('@page { size: 80mm 90mm; margin: 0; }')
  })

  it('never makes a page too short to feed', () => {
    expect(pageRule('58', 0)).toBe('@page { size: 58mm 40mm; margin: 0; }')
  })

  it('leaves A4 to the browser', () => {
    expect(pageRule('a4', 100)).toBeNull()
  })
})

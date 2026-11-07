import { describe, expect, it } from 'vitest'
import { pageRule } from './paper'

describe('pageRule', () => {
  it('sizes a roll to its width with no fixed length', () => {
    expect(pageRule('58')).toBe('@page { size: 58mm auto; margin: 0; }')
    expect(pageRule('80')).toBe('@page { size: 80mm auto; margin: 0; }')
  })

  it('leaves A4 to the browser', () => {
    expect(pageRule('a4')).toBeNull()
  })
})

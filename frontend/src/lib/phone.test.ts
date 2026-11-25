import { describe, expect, it } from 'vitest'
import { phoneOk, validPhone } from './phone'

describe('validPhone', () => {
  it('takes Indian mobiles in the usual ways', () => {
    expect(validPhone('98765 43210')).toBe('+919876543210')
    expect(validPhone('09876543210')).toBe('+919876543210')
    expect(validPhone('919876543210')).toBe('+919876543210')
    expect(validPhone('+44 20 7946 0958')).toBe('+442079460958')
  })

  it('refuses what the server refuses', () => {
    for (const bad of ['12345', '0000000000', '5876543210', '98765-4321x', '+0123456789', '98765432100']) expect(validPhone(bad)).toBeNull()
  })

  it('lets the field stay empty', () => {
    expect(phoneOk('')).toBe(true)
    expect(phoneOk('  ')).toBe(true)
    expect(phoneOk('12345')).toBe(false)
  })
})

import { describe, expect, it } from 'vitest'
import { DRAWER_KICK, dotsFor, rasterBytes } from './escpos'

describe('rasterBytes', () => {
  it('sends a reset, the picture in bands, then feed and cut', () => {
    // 16 dots wide, 3 rows: the first dot of each row black.
    const bytes = rasterBytes({ width: 16, height: 3, black: (x) => x === 0 }, 2)
    expect([...bytes.slice(0, 2)]).toEqual([0x1b, 0x40])
    // First band: GS v 0, 2 bytes across, 2 rows.
    expect([...bytes.slice(2, 10)]).toEqual([0x1d, 0x76, 0x30, 0x00, 2, 0, 2, 0])
    expect([...bytes.slice(10, 14)]).toEqual([0x80, 0, 0x80, 0])
    // Second band: the last row.
    expect([...bytes.slice(14, 22)]).toEqual([0x1d, 0x76, 0x30, 0x00, 2, 0, 1, 0])
    expect([...bytes.slice(-7)]).toEqual([0x1b, 0x64, 0x04, 0x1d, 0x56, 0x42, 0x00])
  })

  it('pads a width that is not a whole byte', () => {
    const bytes = rasterBytes({ width: 10, height: 1, black: () => true })
    expect([...bytes.slice(6, 8)]).toEqual([2, 0])
    expect([...bytes.slice(10, 12)]).toEqual([0xff, 0xc0])
  })

  it('knows the roll widths', () => {
    expect(dotsFor('58')).toBe(384)
    expect(dotsFor('80')).toBe(576)
  })
})

describe('cash drawer kick', () => {
  const image = { width: 8, height: 1, black: () => false }
  it('pulses the drawer after the receipt only when asked', () => {
    const tail = (bytes: Uint8Array) => [...bytes.slice(-5)]
    expect(tail(rasterBytes(image, 96, true))).toEqual(DRAWER_KICK)
    expect(tail(rasterBytes(image))).not.toEqual(DRAWER_KICK)
    expect(DRAWER_KICK).toEqual([0x1b, 0x70, 0x00, 0x19, 0xfa])
  })
})

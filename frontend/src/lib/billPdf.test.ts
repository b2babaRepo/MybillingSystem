import { describe, expect, it } from 'vitest'
import { jpegPdf } from './billPdf'

const latin1 = (b: Uint8Array) => Array.from(b, (c) => String.fromCharCode(c)).join('')

describe('jpegPdf', () => {
  const jpeg = new Uint8Array([0xff, 0xd8, 0xff, 0xd9])
  const pdf = latin1(jpegPdf(jpeg, 600, 1200, 300))

  it('is a one-page PDF with the picture scaled to the page', () => {
    expect(pdf.startsWith('%PDF-1.4')).toBe(true)
    expect(pdf.trimEnd().endsWith('%%EOF')).toBe(true)
    expect(pdf).toContain('/MediaBox [0 0 300 600]')
    expect(pdf).toContain('/Width 600 /Height 1200')
    expect(pdf).toContain('/Length 4 >>')
  })

  it('has xref offsets pointing at each object', () => {
    const start = Number(pdf.match(/startxref\n(\d+)/)![1])
    expect(pdf.slice(start, start + 4)).toBe('xref')
    const offsets = [...pdf.slice(start).matchAll(/(\d{10}) 00000 n/g)].map((m) => Number(m[1]))
    expect(offsets).toHaveLength(5)
    offsets.forEach((o, i) => expect(pdf.slice(o, o + 8)).toBe(`${i + 1} 0 obj\n`))
  })
})

/**
 * A bill as a PDF file, made on the phone with no library: the receipt is drawn on a canvas (so Hindi, Gujarati and ₹
 * come out exactly as on screen), saved as a JPEG and wrapped in a one-page PDF. Small (tens of KB) and opens anywhere.
 */
import { drawReceipt, type ReceiptRow } from './escpos'

/** One-page PDF holding a JPEG picture `w` x `h` pixels, laid on a page `pageWidthPt` points wide. */
export function jpegPdf(jpeg: Uint8Array, w: number, h: number, pageWidthPt = 300): Uint8Array<ArrayBuffer> {
  const pw = pageWidthPt
  const ph = Math.round((h / w) * pw * 100) / 100
  const enc = new TextEncoder()
  const parts: Uint8Array[] = []
  const offsets: number[] = []
  let length = 0
  const push = (b: Uint8Array) => { parts.push(b); length += b.length }
  const text = (s: string) => push(enc.encode(s))
  const obj = (n: number, body: string) => { offsets[n] = length; text(`${n} 0 obj\n${body}\nendobj\n`) }
  text('%PDF-1.4\n%\xE2\xE3\xCF\xD3\n')
  obj(1, '<< /Type /Catalog /Pages 2 0 R >>')
  obj(2, '<< /Type /Pages /Kids [3 0 R] /Count 1 >>')
  obj(3, `<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ${pw} ${ph}] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>`)
  offsets[4] = length
  text(`4 0 obj\n<< /Type /XObject /Subtype /Image /Width ${w} /Height ${h} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length ${jpeg.length} >>\nstream\n`)
  push(jpeg)
  text('\nendstream\nendobj\n')
  const draw = `q ${pw} 0 0 ${ph} 0 0 cm /Im0 Do Q`
  obj(5, `<< /Length ${draw.length} >>\nstream\n${draw}\nendstream`)
  const xref = length
  text(`xref\n0 6\n0000000000 65535 f \n${offsets.slice(1, 6).map((o) => `${String(o).padStart(10, '0')} 00000 n \n`).join('')}`)
  text(`trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF\n`)
  const out = new Uint8Array(length)
  let at = 0
  for (const p of parts) { out.set(p, at); at += p.length }
  return out
}

/** The receipt rows as a PDF file named `name`.pdf. */
export async function receiptPdf(rows: ReceiptRow[], name: string): Promise<File> {
  const inner = drawReceipt(rows, 576).canvas
  // A white margin round the receipt, so it doesn't touch the edge of the page.
  const margin = 32
  const canvas = document.createElement('canvas')
  canvas.width = inner.width + margin * 2
  canvas.height = inner.height + margin * 2
  const ctx = canvas.getContext('2d')!
  ctx.fillStyle = '#fff'
  ctx.fillRect(0, 0, canvas.width, canvas.height)
  ctx.drawImage(inner, margin, margin)
  const blob = await new Promise<Blob>((ok, fail) => canvas.toBlob((b) => (b ? ok(b) : fail(new Error('canvas'))), 'image/jpeg', 0.9))
  const pdf = jpegPdf(new Uint8Array(await blob.arrayBuffer()), canvas.width, canvas.height)
  const safe = name.replace(/[^\p{L}\p{N}_-]+/gu, '-').replace(/^-+|-+$/g, '') || 'bill'
  return new File([pdf], `${safe}.pdf`, { type: 'application/pdf' })
}

/**
 * Sends the PDF: the phone's share sheet (WhatsApp is in it) where it can share files, with the message alongside.
 * Elsewhere (most computers), saves the file and returns 'saved' so the caller can open WhatsApp to attach it.
 */
export async function sharePdf(file: File, message: string): Promise<'shared' | 'saved' | 'cancelled'> {
  const data = { files: [file], text: message }
  if (typeof navigator !== 'undefined' && navigator.canShare?.(data)) {
    try { await navigator.share(data); return 'shared' } catch (e) {
      if (e instanceof Error && e.name === 'AbortError') return 'cancelled'
    }
  }
  const url = URL.createObjectURL(file)
  const a = document.createElement('a')
  a.href = url
  a.download = file.name
  a.click()
  setTimeout(() => URL.revokeObjectURL(url), 10_000)
  return 'saved'
}

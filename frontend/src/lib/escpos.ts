/**
 * A receipt for a thermal roll printer, drawn as a picture and sent as ESC/POS raster commands. Drawing it means Hindi,
 * ₹ and any font print exactly as on screen: cheap Bluetooth printers only know English letters in text mode.
 */

/** One line of a receipt. */
export type ReceiptRow =
  | { text: string; align?: 'left' | 'center' | 'right'; bold?: boolean; big?: boolean }
  | { left: string; right: string; bold?: boolean; big?: boolean }
  | { rule: true }
  | { gap: true }

/** Printable dots across: 58 mm rolls print 384, 80 mm rolls 576. */
export const dotsFor = (paper: string) => (paper === '58' ? 384 : 576)

/** Draws the rows onto a canvas `width` dots wide; returns its 1-bit pixels, row by row (true = black). */
export function drawReceipt(rows: ReceiptRow[], width: number): { width: number; height: number; black: (x: number, y: number) => boolean; canvas: HTMLCanvasElement } {
  const canvas = document.createElement('canvas')
  const ctx = canvas.getContext('2d')!
  const size = width <= 384 ? 22 : 26
  const fontOf = (bold?: boolean, big?: boolean) => `${bold || big ? 'bold ' : ''}${big ? Math.round(size * 1.4) : size}px sans-serif`
  const pad = 4
  // Lay out first (to know the height), then draw.
  type Op = { y: number; draw: (c: CanvasRenderingContext2D) => void }
  const ops: Op[] = []
  let y = 0
  const wrap = (text: string, max: number) => {
    const out: string[] = []
    let line = ''
    for (const word of text.split(/\s+/)) {
      const next = line ? `${line} ${word}` : word
      if (ctx.measureText(next).width <= max || !line) line = next
      else { out.push(line); line = word }
    }
    out.push(line)
    return out
  }
  for (const row of rows) {
    if ('rule' in row) {
      const at = y + 6
      ops.push({ y: at, draw: (c) => c.fillRect(0, at, width, 2) })
      y += 14
      continue
    }
    if ('gap' in row) { y += size / 2; continue }
    ctx.font = fontOf(row.bold, row.big)
    const lineHeight = Math.round((row.big ? size * 1.4 : size) * 1.3)
    const font = ctx.font
    if ('left' in row) {
      const rightWidth = ctx.measureText(row.right).width
      const lefts = wrap(row.left, width - rightWidth - pad * 3)
      lefts.forEach((text, i) => {
        const at = y + lineHeight * (i + 1) - lineHeight * 0.25
        const last = i === lefts.length - 1
        ops.push({ y: at, draw: (c) => { c.font = font; c.textAlign = 'left'; c.fillText(text, pad, at); if (last) { c.textAlign = 'right'; c.fillText(row.right, width - pad, at) } } })
      })
      y += lineHeight * lefts.length
      continue
    }
    for (const text of wrap(row.text, width - pad * 2)) {
      const at = y + lineHeight * 0.75
      const align = row.align ?? 'left'
      const x = align === 'center' ? width / 2 : align === 'right' ? width - pad : pad
      ops.push({ y: at, draw: (c) => { c.font = font; c.textAlign = align; c.fillText(text, x, at) } })
      y += lineHeight
    }
  }
  const height = Math.ceil(y + 8)
  canvas.width = width
  canvas.height = height
  ctx.fillStyle = '#fff'
  ctx.fillRect(0, 0, width, height)
  ctx.fillStyle = '#000'
  ctx.textBaseline = 'alphabetic'
  for (const op of ops) op.draw(ctx)
  const pixels = ctx.getImageData(0, 0, width, height).data
  return { width, height, canvas, black: (x, yy) => { const i = (yy * width + x) * 4; return pixels[i] * 0.3 + pixels[i + 1] * 0.59 + pixels[i + 2] * 0.11 < 140 } }
}

/** ESC/POS bytes for a 1-bit picture: reset, the picture in bands (small printers have small buffers), feed and cut. */
export function rasterBytes(image: { width: number; height: number; black: (x: number, y: number) => boolean }, band = 96): Uint8Array {
  const bytesPerRow = Math.ceil(image.width / 8)
  const out: number[] = [0x1b, 0x40] // ESC @: reset
  for (let top = 0; top < image.height; top += band) {
    const rows = Math.min(band, image.height - top)
    // GS v 0: raster bit image, normal size; width in bytes, height in dots (little-endian).
    out.push(0x1d, 0x76, 0x30, 0x00, bytesPerRow & 0xff, bytesPerRow >> 8, rows & 0xff, rows >> 8)
    for (let y = top; y < top + rows; y++) {
      for (let bx = 0; bx < bytesPerRow; bx++) {
        let byte = 0
        for (let bit = 0; bit < 8; bit++) {
          const x = bx * 8 + bit
          if (x < image.width && image.black(x, y)) byte |= 0x80 >> bit
        }
        out.push(byte)
      }
    }
  }
  out.push(0x1b, 0x64, 0x04) // ESC d 4: feed four lines to clear the tear bar
  out.push(0x1d, 0x56, 0x42, 0x00) // GS V B 0: cut, where the printer has a cutter (others ignore it)
  return new Uint8Array(out)
}

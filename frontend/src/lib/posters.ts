import QRCode from 'qrcode'

export type PosterGroup = 'greeting' | 'festival' | 'shop' | 'offer'

/** A ready poster: its art (an emoji, drawn large), colours, and the text keys under posters.t.{id}. */
export interface PosterTemplate { id: string; group: PosterGroup; art: string; from: string; to: string; ink: string }

export const POSTER_TEMPLATES: PosterTemplate[] = [
  { id: 'morning', group: 'greeting', art: '🌅', from: '#ffecd2', to: '#fcb69f', ink: '#5a2a0c' },
  { id: 'suvichar1', group: 'greeting', art: '🪔', from: '#fdfbfb', to: '#ebedee', ink: '#1f2937' },
  { id: 'suvichar2', group: 'greeting', art: '🌱', from: '#d4fc79', to: '#96e6a1', ink: '#14532d' },
  { id: 'thanks', group: 'greeting', art: '🙏', from: '#a1c4fd', to: '#c2e9fb', ink: '#0c3b66' },
  { id: 'diwali', group: 'festival', art: '🪔', from: '#2b0a3d', to: '#a4508b', ink: '#ffe08a' },
  { id: 'holi', group: 'festival', art: '🎨', from: '#f857a6', to: '#ffb347', ink: '#ffffff' },
  { id: 'rakhi', group: 'festival', art: '🧵', from: '#ff9a9e', to: '#fecfef', ink: '#7a1335' },
  { id: 'ganesh', group: 'festival', art: '🌺', from: '#ff8008', to: '#ffc837', ink: '#5a1f00' },
  { id: 'navratri', group: 'festival', art: '💃', from: '#e52d27', to: '#b31217', ink: '#ffe9a8' },
  { id: 'eid', group: 'festival', art: '🌙', from: '#134e5e', to: '#71b280', ink: '#fff7d6' },
  { id: 'christmas', group: 'festival', art: '🎄', from: '#0f5132', to: '#b91c1c', ink: '#ffffff' },
  { id: 'newyear', group: 'festival', art: '🎉', from: '#141e30', to: '#243b55', ink: '#ffd86b' },
  { id: 'sankranti', group: 'festival', art: '🪁', from: '#56ccf2', to: '#2f80ed', ink: '#ffffff' },
  { id: 'independence', group: 'festival', art: '🇮🇳', from: '#ff9933', to: '#138808', ink: '#ffffff' },
  { id: 'open', group: 'shop', art: '🏪', from: '#11998e', to: '#38ef7d', ink: '#ffffff' },
  { id: 'delivery', group: 'shop', art: '🛵', from: '#f7971e', to: '#ffd200', ink: '#3b2300' },
  { id: 'khata', group: 'shop', art: '📒', from: '#0f766e', to: '#14b8a6', ink: '#ffffff' },
  { id: 'upi', group: 'shop', art: '📱', from: '#4b6cb7', to: '#182848', ink: '#ffffff' },
  { id: 'newstock', group: 'shop', art: '📦', from: '#ee9ca7', to: '#ffdde1', ink: '#5b1a26' },
]

/** The offer poster's colours: one bright look for every running offer. */
export const OFFER_LOOK = { art: '🏷️', from: '#ff416c', to: '#ff4b2b', ink: '#ffffff' }

export interface PosterContent {
  art: string; from: string; to: string; ink: string
  title: string; sub: string
  shop: string; contact: string
  /** The shop's UPI ID: printed with a scan-to-pay QR in the footer. Null: no QR. */
  upi: string | null
}

const SIZE = 1080

/** Lines that fit the width, breaking at spaces (or anywhere, for a single long word). */
function wrap(ctx: CanvasRenderingContext2D, text: string, width: number): string[] {
  const lines: string[] = []
  for (const para of text.split('\n')) {
    let line = ''
    for (const word of para.split(/\s+/).filter(Boolean)) {
      const next = line ? `${line} ${word}` : word
      if (ctx.measureText(next).width <= width || !line) line = next
      else { lines.push(line); line = word }
    }
    lines.push(line)
  }
  return lines
}

/** Text in the biggest size (down to a floor) that fits in the lines allowed. */
function fit(ctx: CanvasRenderingContext2D, text: string, weight: string, max: number, min: number, width: number, maxLines: number) {
  for (let size = max; size >= min; size -= 4) {
    ctx.font = `${weight} ${size}px "Noto Sans", "Noto Sans Devanagari", "Noto Sans Gujarati", system-ui, sans-serif`
    const lines = wrap(ctx, text, width)
    if (lines.length <= maxLines) return { size, lines }
  }
  return { size: min, lines: wrap(ctx, text, width).slice(0, maxLines) }
}

/** Draws the 1080×1080 poster: gradient, big art, headline, line under it, and a white footer with the shop. */
export async function drawPoster(canvas: HTMLCanvasElement, p: PosterContent) {
  canvas.width = SIZE
  canvas.height = SIZE
  const ctx = canvas.getContext('2d')!
  const bg = ctx.createLinearGradient(0, 0, SIZE, SIZE)
  bg.addColorStop(0, p.from)
  bg.addColorStop(1, p.to)
  ctx.fillStyle = bg
  ctx.fillRect(0, 0, SIZE, SIZE)
  // Soft circles for depth.
  ctx.fillStyle = 'rgba(255,255,255,0.12)'
  for (const [x, y, r] of [[930, 150, 220], [120, 640, 160], [980, 700, 120]] as const) { ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.fill() }

  const footer = 250
  ctx.textAlign = 'center'
  ctx.textBaseline = 'middle'
  // Solid fill: a see-through one fades colour emoji too.
  ctx.fillStyle = '#000000'
  ctx.font = '220px "Noto Color Emoji", "Segoe UI Emoji", "Apple Color Emoji", sans-serif'
  ctx.fillText(p.art, SIZE / 2, 230)

  ctx.fillStyle = p.ink
  const title = fit(ctx, p.title, '800', 110, 56, SIZE - 140, 3)
  let y = 420 + title.size / 2
  for (const line of title.lines) { ctx.fillText(line, SIZE / 2, y); y += title.size * 1.15 }
  if (p.sub.trim()) {
    const sub = fit(ctx, p.sub, '500', 52, 30, SIZE - 160, 3)
    y += 10
    for (const line of sub.lines) { ctx.fillText(line, SIZE / 2, y + sub.size / 2); y += sub.size * 1.25 }
  }

  // Footer: the shop, how to reach it, and a QR to pay.
  ctx.fillStyle = '#ffffff'
  ctx.fillRect(0, SIZE - footer, SIZE, footer)
  ctx.fillStyle = p.from
  ctx.fillRect(0, SIZE - footer, SIZE, 10)
  const qrSize = p.upi ? 190 : 0
  const textWidth = SIZE - 120 - (qrSize ? qrSize + 40 : 0)
  ctx.textAlign = 'left'
  ctx.fillStyle = '#10201d'
  const name = fit(ctx, p.shop, '800', 64, 36, textWidth, 1)
  ctx.fillText(name.lines[0] ?? '', 60, SIZE - footer + 80)
  ctx.fillStyle = '#41524e'
  const contact = fit(ctx, p.contact, '500', 38, 26, textWidth, 2)
  contact.lines.forEach((line, i) => ctx.fillText(line, 60, SIZE - footer + 150 + i * contact.size * 1.2))
  if (p.upi) {
    const qr = await QRCode.toDataURL(`upi://pay?pa=${encodeURIComponent(p.upi)}&pn=${encodeURIComponent(p.shop)}&cu=INR`, { width: qrSize, margin: 1 })
    const img = new Image()
    img.src = qr
    await img.decode()
    ctx.drawImage(img, SIZE - 60 - qrSize, SIZE - footer + 25, qrSize, qrSize)
  }
}

/** The poster as a PNG file, for sharing or saving. */
export function posterFile(canvas: HTMLCanvasElement, name: string): Promise<File> {
  return new Promise((resolve, reject) => canvas.toBlob((b) => (b ? resolve(new File([b], `${name}.png`, { type: 'image/png' })) : reject(new Error('No picture'))), 'image/png'))
}

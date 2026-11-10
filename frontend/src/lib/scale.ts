/**
 * Reads the weight from a shop weighing scale plugged in by USB or serial cable (Essae, Aczet, Atom and most others send
 * the weight as text, over and over). Chrome on computers can do this; Android Chrome cannot use USB serial scales yet.
 */

/** The weight in kg in one line of what a scale sends ("ST,GS,+  1.250kg", "  1250 g", "W: 0.875"); null if none. */
export function parseWeight(line: string): { kg: number; stable: boolean } | null {
  const matches = [...line.matchAll(/([-+]?\s*\d+(?:\.\d+)?)\s*(kg|g)?\b/gi)]
  if (matches.length === 0) return null
  const last = matches[matches.length - 1]
  const value = Number(last[1].replace(/\s/g, ''))
  if (!Number.isFinite(value)) return null
  const grams = last[2]?.toLowerCase() === 'g'
  // "US" (unstable) or "?" marks a weight still moving; everything else is taken as settled.
  const stable = !/\bUS\b|\?/i.test(line)
  return { kg: Math.round((grams ? value / 1000 : value) * 1000) / 1000, stable }
}

/* Web Serial isn't in TypeScript's DOM types yet: the small part used here. */
interface Port { readable: ReadableStream<Uint8Array> | null; open(options: { baudRate: number }): Promise<void>; close(): Promise<void> }
interface Serial { requestPort(): Promise<Port>; getPorts(): Promise<Port[]> }
const serial = () => (navigator as Navigator & { serial?: Serial }).serial
export const canReadScale = () => typeof navigator !== 'undefined' && !!serial()

const BAUD_KEY = 'gk-scale-baud'
let port: Port | null = null

/** Opens the scale (asking the first time), waits for the same settled weight twice, and returns it in kg. */
export async function readScale(choose = false, timeoutMs = 4000): Promise<number> {
  if (choose || !port) port = (!choose && (await serial()!.getPorts())[0]) || (await serial()!.requestPort())
  let baud = 9600
  try { baud = Number(localStorage.getItem(BAUD_KEY)) || 9600 } catch { /* no storage */ }
  await port.open({ baudRate: baud })
  const reader = port.readable!.getReader()
  const decoder = new TextDecoder()
  let buffer = ''
  let previous: number | null = null
  const deadline = Date.now() + timeoutMs
  try {
    while (Date.now() < deadline) {
      const chunk = await Promise.race([reader.read(), new Promise<null>((r) => setTimeout(() => r(null), deadline - Date.now()))])
      if (!chunk || chunk.done) break
      buffer += decoder.decode(chunk.value, { stream: true })
      const lines = buffer.split(/[\r\n]+/)
      buffer = lines.pop() ?? ''
      for (const line of lines) {
        const reading = parseWeight(line)
        if (!reading || !reading.stable) continue
        if (previous !== null && reading.kg === previous && reading.kg > 0) return reading.kg
        previous = reading.kg
      }
    }
    if (previous !== null && previous > 0) return previous
    throw new Error('No weight came from the scale. Check the cable, and that the scale is on.')
  } finally {
    await reader.cancel().catch(() => undefined)
    reader.releaseLock()
    await port.close().catch(() => undefined)
  }
}

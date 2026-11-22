/**
 * The app passcode on this phone: the app opens locked, and locks again after a while in the background, so whoever
 * picks the phone up can't open the khata. Kept on this phone only (a passcode for this device, not the account), as a
 * salted SHA-256 of the digits, never the digits themselves.
 */
const KEY = 'gk-app-lock'
/** How long the app may sit in the background before it asks again. */
export const RELOCK_MS = 2 * 60_000

interface Saved { salt: string; hash: string }

function read(): Saved | null {
  try {
    const raw = localStorage.getItem(KEY)
    return raw ? (JSON.parse(raw) as Saved) : null
  } catch {
    return null
  }
}

async function digest(salt: string, pin: string): Promise<string> {
  const bytes = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(`${salt}:${pin}`))
  return Array.from(new Uint8Array(bytes), (b) => b.toString(16).padStart(2, '0')).join('')
}

export const hasAppLock = () => read() !== null

export async function setAppLock(pin: string): Promise<void> {
  const salt = Array.from(crypto.getRandomValues(new Uint8Array(16)), (b) => b.toString(16).padStart(2, '0')).join('')
  localStorage.setItem(KEY, JSON.stringify({ salt, hash: await digest(salt, pin) } satisfies Saved))
}

export function clearAppLock() {
  try { localStorage.removeItem(KEY) } catch { /* nothing saved */ }
}

export async function appLockMatches(pin: string): Promise<boolean> {
  const saved = read()
  return !!saved && (await digest(saved.salt, pin)) === saved.hash
}

/** 4 to 6 digits, and not one anyone would try first (0000, 1234, 9876): the same rule as staff PINs. */
export function passcodeProblem(pin: string): 'length' | 'easy' | null {
  if (!/^\d{4,6}$/.test(pin)) return 'length'
  const d = [...pin].map(Number)
  const same = d.every((x) => x === d[0])
  const up = d.every((x, i) => i === 0 || x === d[i - 1] + 1)
  const down = d.every((x, i) => i === 0 || x === d[i - 1] - 1)
  return same || up || down ? 'easy' : null
}

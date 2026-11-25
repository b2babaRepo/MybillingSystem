/**
 * The app passcode on this phone: the app opens locked, and locks again after a while in the background, so whoever
 * picks the phone up can't open the khata. Kept on this phone only (a passcode for this device, not the account), as a
 * salted PBKDF2 hash of the digits, never the digits themselves. Wrong tries are counted on the phone (a reload doesn't
 * reset them); after {@link MAX_WRONG} the app signs out, so the khata then needs the password or an SMS code.
 */
const KEY = 'gk-app-lock'
/** How long the app may sit in the background before it asks again. */
export const RELOCK_MS = 2 * 60_000
/** Wrong passcodes in a row before the app signs out. */
export const MAX_WRONG = 10
/** PBKDF2 rounds: slow enough that trying every passcode from a copy of the phone's storage takes real time. */
const ROUNDS = 100_000

/** No `rounds`: a passcode saved as a single SHA-256 by an older version, upgraded the next time it is entered right. */
interface Saved { salt: string; hash: string; rounds?: number; wrong?: number }

function read(): Saved | null {
  try {
    const raw = localStorage.getItem(KEY)
    return raw ? (JSON.parse(raw) as Saved) : null
  } catch {
    return null
  }
}

function write(saved: Saved) {
  try { localStorage.setItem(KEY, JSON.stringify(saved)) } catch { /* storage blocked: the lock stays as it was */ }
}

const hex = (bytes: ArrayBuffer | Uint8Array) => Array.from(new Uint8Array(bytes), (b) => b.toString(16).padStart(2, '0')).join('')

async function digest(salt: string, pin: string, rounds?: number): Promise<string> {
  const text = new TextEncoder().encode(rounds ? pin : `${salt}:${pin}`)
  if (!rounds) return hex(await crypto.subtle.digest('SHA-256', text))
  const key = await crypto.subtle.importKey('raw', text, 'PBKDF2', false, ['deriveBits'])
  return hex(await crypto.subtle.deriveBits({ name: 'PBKDF2', hash: 'SHA-256', salt: new TextEncoder().encode(salt), iterations: rounds }, key, 256))
}

export const hasAppLock = () => read() !== null

export async function setAppLock(pin: string): Promise<void> {
  const salt = hex(crypto.getRandomValues(new Uint8Array(16)))
  localStorage.setItem(KEY, JSON.stringify({ salt, hash: await digest(salt, pin, ROUNDS), rounds: ROUNDS } satisfies Saved))
}

export function clearAppLock() {
  try { localStorage.removeItem(KEY) } catch { /* nothing saved */ }
}

/** Whether this is the passcode. Only checks: wrong tries are counted by {@link wrongPasscode}. */
export async function appLockMatches(pin: string): Promise<boolean> {
  const saved = read()
  if (!saved || (await digest(saved.salt, pin, saved.rounds)) !== saved.hash) return false
  if (!saved.rounds) await setAppLock(pin) // an old single hash: saved again the slow way
  return true
}

/** The passcode was right: the count of wrong tries starts again. */
export function rightPasscode() {
  const saved = read()
  if (saved?.wrong) write({ ...saved, wrong: 0 })
}

/** Counts a wrong passcode and returns how many in a row so far, kept on the phone so a reload doesn't reset it. */
export function wrongPasscode(): number {
  const saved = read()
  if (!saved) return 0
  const wrong = (saved.wrong ?? 0) + 1
  write({ ...saved, wrong })
  return wrong
}

/** Wrong passcodes in a row so far. */
export const wrongSoFar = () => read()?.wrong ?? 0

/** 4 to 6 digits, and not one anyone would try first (0000, 1234, 9876): the same rule as staff PINs. */
export function passcodeProblem(pin: string): 'length' | 'easy' | null {
  if (!/^\d{4,6}$/.test(pin)) return 'length'
  const d = [...pin].map(Number)
  const same = d.every((x) => x === d[0])
  const up = d.every((x, i) => i === 0 || x === d[i - 1] + 1)
  const down = d.every((x, i) => i === 0 || x === d[i - 1] - 1)
  return same || up || down ? 'easy' : null
}

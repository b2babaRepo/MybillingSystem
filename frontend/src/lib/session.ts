import type { AuthResponse } from './types'

const KEY = 'grocery-khata.session'
const listeners = new Set<() => void>()
let current: AuthResponse | null = read()

function read(): AuthResponse | null {
  try {
    const raw = localStorage.getItem(KEY)
    return raw ? (JSON.parse(raw) as AuthResponse) : null
  } catch {
    return null
  }
}

/** The signed-in session, kept across restarts so the shop opens without signing in again, even offline. */
export function getSession(): AuthResponse | null {
  return current
}

export function setSession(session: AuthResponse | null) {
  current = session
  try {
    if (session) localStorage.setItem(KEY, JSON.stringify(session))
    else localStorage.removeItem(KEY)
  } catch {
    // Private mode or storage blocked: the session lasts until the tab closes.
  }
  listeners.forEach((listener) => listener())
}

/** The session as last saved by any tab: another tab may have refreshed or ended it since this one read it. */
export function savedSession(): AuthResponse | null {
  return read()
}

/** Adopts what another tab saved, without writing it back. */
function adopt(session: AuthResponse | null) {
  current = session
  listeners.forEach((listener) => listener())
}

// Another tab of the app refreshed, switched shop or signed out: take its session, so this tab never sends a refresh
// token that tab has already swapped (the server takes a stale token for a stolen one and signs the person out).
if (typeof window !== 'undefined') {
  window.addEventListener('storage', (event) => {
    if (event.key === KEY) adopt(read())
  })
}

export function subscribeSession(listener: () => void) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

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

export function subscribeSession(listener: () => void) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

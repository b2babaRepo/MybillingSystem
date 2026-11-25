import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { request } from '../lib/api'
import { appLockMatches, clearAppLock, hasAppLock, MAX_WRONG, RELOCK_MS, rightPasscode, wrongPasscode, wrongSoFar } from '../lib/appLock'
import { getSession, setSession, subscribeSession } from '../lib/session'

/**
 * Shows the app only after the passcode set on this phone: on opening, and again after it has been in the background
 * for {@link RELOCK_MS}. Signed out, there is nothing to lock. Every five wrong tries wait 30 seconds, and
 * {@link MAX_WRONG} in a row (counted on the phone, so a reload doesn't reset them) sign out, as "Forgot passcode?" does:
 * the owner signs in again with their password or an SMS code. Entries not yet sent stay on the phone and go after.
 */
export function AppLockGate({ children }: { children: ReactNode }) {
  const { t } = useTranslation()
  const [signedIn, setSignedIn] = useState(() => !!getSession())
  const [locked, setLocked] = useState(() => hasAppLock())
  const [pin, setPin] = useState('')
  const [wrong, setWrong] = useState(wrongSoFar)
  // Seconds left to wait after too many wrong tries.
  const [waiting, setWaiting] = useState(0)
  const hiddenAt = useRef<number | null>(null)

  useEffect(() => subscribeSession(() => setSignedIn(!!getSession())), [])
  useEffect(() => {
    const onVisible = () => {
      if (document.visibilityState === 'hidden') hiddenAt.current = Date.now()
      else if (hiddenAt.current && Date.now() - hiddenAt.current > RELOCK_MS && hasAppLock()) setLocked(true)
    }
    document.addEventListener('visibilitychange', onVisible)
    return () => document.removeEventListener('visibilitychange', onVisible)
  }, [])
  // Counts down the wait after too many wrong tries.
  useEffect(() => {
    if (waiting <= 0) return
    const timer = setTimeout(() => setWaiting((s) => s - 1), 1000)
    return () => clearTimeout(timer)
  }, [waiting])

  // A computer's keys work on the lock too: digits, Backspace, Enter to open.
  const keys = useRef<{ press: (d: string) => void; back: () => void; open: () => void }>({ press: () => {}, back: () => {}, open: () => {} })
  useEffect(() => {
    if (!signedIn || !locked) return
    const onKey = (e: KeyboardEvent) => {
      if (e.ctrlKey || e.metaKey || e.altKey) return
      if (/^[0-9]$/.test(e.key)) { e.preventDefault(); keys.current.press(e.key) }
      else if (e.key === 'Backspace') { e.preventDefault(); keys.current.back() }
      else if (e.key === 'Enter') { e.preventDefault(); keys.current.open() }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [signedIn, locked])

  if (!signedIn || !locked) return <>{children}</>

  /** Signs out on this phone and on the server, and removes the passcode. */
  function signOut() {
    void request('/v1/auth/sign-out', 'POST', { refreshToken: getSession()?.refreshToken }).catch(() => {})
    clearAppLock(); setSession(null); setLocked(false); setWrong(0)
  }

  async function check(value: string) {
    if (await appLockMatches(value)) {
      rightPasscode()
      setLocked(false); setPin(''); setWrong(0)
      return
    }
    const tries = wrongPasscode()
    setWrong(tries); setPin('')
    if (tries >= MAX_WRONG) signOut()
    else if (tries % 5 === 0) setWaiting(30)
  }

  const press = (digit: string) => {
    if (waiting) return
    const next = (pin + digit).slice(0, 6)
    setPin(next)
    if (next.length >= 4) void appLockMatches(next).then((ok) => { if (ok || next.length === 6) void check(next) })
  }
  keys.current = { press, back: () => setPin(pin.slice(0, -1)), open: () => { if (pin.length >= 4 && !waiting) void check(pin) } }

  return (
    <div className="fixed inset-0 z-[60] flex flex-col items-center justify-center gap-5 bg-brand p-6 text-white">
      <span aria-hidden className="text-5xl">🔒</span>
      <h1 className="text-xl font-bold">{t('appLock.title')}</h1>
      <div className="flex gap-3" aria-label={t('appLock.entered', { count: pin.length })}>
        {Array.from({ length: Math.max(4, pin.length) }, (_, i) => <span key={i} className={`h-4 w-4 rounded-full border-2 border-white ${i < pin.length ? 'bg-white' : ''}`} />)}
      </div>
      <p role="status" className="min-h-6 text-center text-sm font-semibold">{waiting ? t('appLock.wait', { seconds: waiting }) : wrong ? t('appLock.wrongLeft', { count: MAX_WRONG - wrong }) : ''}</p>
      <div className="grid w-full max-w-xs grid-cols-3 gap-3">
        {['1', '2', '3', '4', '5', '6', '7', '8', '9'].map((d) => (
          <button key={d} onClick={() => press(d)} disabled={!!waiting} className="h-16 rounded-2xl bg-white/15 text-2xl font-bold active:bg-white/30 disabled:opacity-40">{d}</button>
        ))}
        <button onClick={() => setPin(pin.slice(0, -1))} aria-label={t('appLock.back')} className="h-16 rounded-2xl text-2xl">⌫</button>
        <button onClick={() => press('0')} disabled={!!waiting} className="h-16 rounded-2xl bg-white/15 text-2xl font-bold active:bg-white/30 disabled:opacity-40">0</button>
        <button onClick={() => void check(pin)} disabled={pin.length < 4 || !!waiting} aria-label={t('appLock.open')} className="h-16 rounded-2xl text-2xl disabled:opacity-40">✓</button>
      </div>
      <button onClick={signOut} className="min-h-11 text-sm font-semibold underline">{t('appLock.forgot')}</button>
    </div>
  )
}

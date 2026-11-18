import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { appLockMatches, clearAppLock, hasAppLock, RELOCK_MS } from '../lib/appLock'
import { getSession, setSession, subscribeSession } from '../lib/session'

/**
 * Shows the app only after the passcode set on this phone: on opening, and again after it has been in the background
 * for {@link RELOCK_MS}. Signed out, there is nothing to lock. Five wrong tries wait 30 seconds; "Forgot passcode?"
 * signs out and removes it, so the owner signs in again with their password or OTP.
 */
export function AppLockGate({ children }: { children: ReactNode }) {
  const { t } = useTranslation()
  const [signedIn, setSignedIn] = useState(() => !!getSession())
  const [locked, setLocked] = useState(() => hasAppLock())
  const [pin, setPin] = useState('')
  const [wrong, setWrong] = useState(0)
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

  if (!signedIn || !locked) return <>{children}</>

  async function check(value: string) {
    if (await appLockMatches(value)) {
      setLocked(false); setPin(''); setWrong(0)
      return
    }
    const tries = wrong + 1
    setWrong(tries); setPin('')
    if (tries % 5 === 0) setWaiting(30)
  }

  const press = (digit: string) => {
    if (waiting) return
    const next = (pin + digit).slice(0, 6)
    setPin(next)
    if (next.length >= 4) void appLockMatches(next).then((ok) => { if (ok || next.length === 6) void check(next) })
  }

  return (
    <div className="fixed inset-0 z-[60] flex flex-col items-center justify-center gap-5 bg-brand p-6 text-white">
      <span aria-hidden className="text-5xl">🔒</span>
      <h1 className="text-xl font-bold">{t('appLock.title')}</h1>
      <div className="flex gap-3" aria-label={t('appLock.entered', { count: pin.length })}>
        {Array.from({ length: Math.max(4, pin.length) }, (_, i) => <span key={i} className={`h-4 w-4 rounded-full border-2 border-white ${i < pin.length ? 'bg-white' : ''}`} />)}
      </div>
      <p role="status" className="min-h-6 text-sm font-semibold">{waiting ? t('appLock.wait', { seconds: waiting }) : wrong ? t('appLock.wrong') : ''}</p>
      <div className="grid w-full max-w-xs grid-cols-3 gap-3">
        {['1', '2', '3', '4', '5', '6', '7', '8', '9'].map((d) => (
          <button key={d} onClick={() => press(d)} disabled={!!waiting} className="h-16 rounded-2xl bg-white/15 text-2xl font-bold active:bg-white/30 disabled:opacity-40">{d}</button>
        ))}
        <button onClick={() => setPin(pin.slice(0, -1))} aria-label={t('appLock.back')} className="h-16 rounded-2xl text-2xl">⌫</button>
        <button onClick={() => press('0')} disabled={!!waiting} className="h-16 rounded-2xl bg-white/15 text-2xl font-bold active:bg-white/30 disabled:opacity-40">0</button>
        <button onClick={() => void check(pin)} disabled={pin.length < 4 || !!waiting} aria-label={t('appLock.open')} className="h-16 rounded-2xl text-2xl disabled:opacity-40">✓</button>
      </div>
      <button onClick={() => { clearAppLock(); setSession(null); setLocked(false) }} className="min-h-11 text-sm font-semibold underline">{t('appLock.forgot')}</button>
    </div>
  )
}

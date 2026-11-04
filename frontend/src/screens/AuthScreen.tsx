import { SHOP_TYPES } from '../lib/reports'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { navigate, usePath } from '../router'
import { Button, Field } from '../components/ui'
import { ApiError, authenticate, readProblem, request } from '../lib/api'
import type { AuthResponse } from '../lib/types'

export function AuthScreen() {
  const { t, i18n } = useTranslation()
  // A trade's own sign-up link opens straight on that shop's registration: #/start/salon (beauty parlour), #/start/medical (chemist).
  const path = usePath()
  const preset = path === '/start/salon' ? 'beauty' : path === '/start/medical' ? 'medical' : path === '/start/dairy' ? 'dairy' : path === '/start/service' ? 'sales_service' : null
  const [mode, setMode] = useState<'sign-in' | 'sign-up'>(preset ? 'sign-up' : 'sign-in')
  const [role, setRole] = useState<'home' | 'shop'>(preset ? 'shop' : 'home')
  const [form, setForm] = useState({ shopName: '', area: '', fullName: '', phone: '', password: '', type: preset ?? 'kirana' })
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  // Signing in: a code by SMS (default; no password to remember) or a password / counter PIN.
  const [method, setMethod] = useState<'otp' | 'password'>('otp')
  const [otpStep, setOtpStep] = useState<'phone' | 'code' | 'name'>('phone')
  const [code, setCode] = useState('')
  const [info, setInfo] = useState('')
  const useOtp = mode === 'sign-in' && method === 'otp'
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value })

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError('')
    try {
      if (useOtp) await otp()
      else if (mode === 'sign-in') await authenticate('/v1/auth/sign-in', { phone: form.phone, password: form.password })
      else if (role === 'shop') {
        await authenticate('/v1/auth/sign-up', { ...form, language: i18n.language })
        navigate('/choose-plan')
      }
      else await authenticate('/v1/auth/register', { fullName: form.fullName, phone: form.phone, password: form.password, language: i18n.language })
    } catch (err) {
      setError(err instanceof Error ? err.message : t('error'))
    } finally {
      setBusy(false)
    }
  }

  async function sendCode() {
    const response = await request('/v1/auth/otp/send', 'POST', { phone: form.phone })
    if (!response.ok) throw new ApiError(response.status, await readProblem(response))
    setCode('')
    setOtpStep('code')
    setInfo(t('otp.sent'))
  }

  async function otp() {
    if (otpStep === 'phone') return sendCode()
    try {
      await authenticate('/v1/auth/otp/verify', {
        phone: form.phone, code, fullName: otpStep === 'name' ? form.fullName : undefined, language: i18n.language,
      })
    } catch (err) {
      // Right code for a new number: ask for a name, then the same code again.
      if (err instanceof ApiError && err.status === 409) {
        setOtpStep('name')
        setInfo(t('otp.newNumber'))
        return
      }
      throw err
    }
  }

  const agentPath = usePath() === '/agent'

  return (
    <form onSubmit={submit} className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-4 p-4">
      <h1 className="text-center text-3xl font-bold text-teal-800">{t('appName')}</h1>
      {preset && mode === 'sign-up' && <p className="text-center text-lg font-semibold">{t(preset === 'beauty' ? 'salon.signUpTitle' : preset === 'dairy' ? 'dairy.signUpTitle' : preset === 'sales_service' ? 'serials.signUpTitle' : 'medical.signUpTitle')}</p>}
      {mode === 'sign-up' && (
        <>
          {!preset && <div role="radiogroup" className="grid grid-cols-2 gap-2">
            {(['home', 'shop'] as const).map((r) => (
              <button
                key={r}
                type="button"
                role="radio"
                aria-checked={role === r}
                onClick={() => setRole(r)}
                className={`min-h-14 rounded-2xl px-2 font-semibold ${role === r ? 'border-2 border-teal-700 bg-teal-50 text-teal-800' : 'border border-line bg-white'}`}
              >
                {r === 'home' ? t('plan.iAmHome') : t('plan.iAmShop')}
              </button>
            ))}
          </div>}
          {role === 'shop' && <Field label={t('shopName')} value={form.shopName} onChange={set('shopName')} required autoComplete="organization" />}
          {role === 'shop' && <Field label={t('plan.searchShop')} value={form.area} onChange={set('area')} placeholder="Raja Park bazaar" />}
          {role === 'shop' && !preset && (
            <label className="flex flex-col gap-1">
              <span className="text-sm font-medium text-muted">{t('shopType.label')}</span>
              <select value={form.type} onChange={set('type')} className="h-12 rounded-xl border border-line bg-white px-3 text-lg">
                {SHOP_TYPES.map((type) => <option key={type} value={type}>{t(`shopType.${type}`)}</option>)}
              </select>
            </label>
          )}
          <Field label={t('yourName')} value={form.fullName} onChange={set('fullName')} required autoComplete="name" />
        </>
      )}
      <Field label={t('phone')} value={form.phone} onChange={(e) => { set('phone')(e); setOtpStep('phone') }} required inputMode="tel" autoComplete="tel" />
      {useOtp && otpStep !== 'phone' && (
        <Field label={t('otp.code')} value={code} onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
          required inputMode="numeric" autoComplete="one-time-code" minLength={6} maxLength={6} />
      )}
      {useOtp && otpStep === 'name' && <Field label={t('yourName')} value={form.fullName} onChange={set('fullName')} required autoComplete="name" />}
      {info && !error && <p className="rounded-lg bg-teal-50 p-3 text-teal-900">{info}</p>}
      {!useOtp && <Field
        label={t('password')}
        value={form.password}
        onChange={set('password')}
        required
        type="password"
        // A new password needs 6 characters; signing in also takes a helper's 4-digit counter PIN.
        minLength={mode === 'sign-in' ? 4 : 6}
        autoComplete={mode === 'sign-in' ? 'current-password' : 'new-password'}
      />}
      {error && <p className="rounded-lg bg-red-50 p-3 text-due">{error}</p>}
      <Button type="submit" disabled={busy}>
        {useOtp ? (otpStep === 'phone' ? t('otp.send') : t('signIn')) : mode === 'sign-in' ? t('signIn') : role === 'shop' ? t('signUp') : t('plan.register')}
      </Button>
      {useOtp && otpStep !== 'phone' && (
        <button type="button" disabled={busy} className="text-teal-700 underline" onClick={() => { setError(''); void sendCode().catch((err: Error) => setError(err.message)) }}>{t('otp.resend')}</button>
      )}
      {mode === 'sign-in' && (
        <button type="button" className="text-teal-700 underline" onClick={() => { setMethod(method === 'otp' ? 'password' : 'otp'); setError(''); setInfo('') }}>
          {method === 'otp' ? t('otp.usePassword') : t('otp.useCode')}
        </button>
      )}
      <button type="button" className="text-teal-700 underline" onClick={() => setMode(mode === 'sign-in' ? 'sign-up' : 'sign-in')}>
        {mode === 'sign-in' ? t('newShop') : t('haveAccount')}
      </button>
      {/* Field agents sign in (or register as a person) first; the app then opens their application. */}
      {agentPath
        ? <p className="rounded-lg bg-amber-50 p-3 text-sm text-amber-900">{t('fieldAgent.signInFirst')}</p>
        : <button type="button" className="text-sm text-muted underline" onClick={() => { navigate('/agent'); setMode('sign-up'); setRole('home') }}>{t('fieldAgent.join')}</button>}
    </form>
  )
}

/** Shown when one person belongs to several shops and hasn't picked one. */
export function ShopPicker({ session, onHousehold }: { session: AuthResponse; onHousehold: () => void }) {
  const { t } = useTranslation()
  const [error, setError] = useState('')
  return (
    <div className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-3 p-4">
      <h1 className="text-2xl font-bold">{t('pickShop')}</h1>
      <BlockedShopsNotice session={session} />
      {session.memberships.map((m) => (
        <Button
          key={m.membershipId}
          tone="plain"
          onClick={() =>
            authenticate('/v1/auth/select-shop', { membershipId: m.membershipId }).catch((err: Error) => setError(err.message))
          }
        >
          {m.shopName}
        </Button>
      ))}
      <Button tone="plain" onClick={onHousehold}>{t('plan.householdLists')}</Button>
      {error && <p className="text-due">{error}</p>}
    </div>
  )
}

/** Tells a person why a shop of theirs is missing: a platform admin blocked it, with this reason. */
export function BlockedShopsNotice({ session }: { session: AuthResponse }) {
  const { t } = useTranslation()
  if (!session.blockedShops?.length) return null
  return (
    <div role="alert" className="mx-4 mt-3 flex flex-col gap-1 rounded-2xl bg-red-50 p-4 text-red-900">
      {session.blockedShops.map((b) => (
        <p key={b.shopName}><b>{t('blocked.title', { shop: b.shopName })}</b><br />{t('blocked.reason', { reason: b.reason })}</p>
      ))}
      <p className="text-sm">{t('blocked.help')}</p>
    </div>
  )
}

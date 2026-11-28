import { SHOP_TYPES } from '../lib/reports'
import { useState, type FormEvent } from 'react'
import { forgetReferral, referralFromLink } from '../lib/referral'
import { useTranslation } from 'react-i18next'
import { navigate, usePath } from '../router'
import { Button, Field } from '../components/ui'
import { btn } from '../lib/buttonStyles'
import { ApiError, authenticate, readProblem, request } from '../lib/api'
import type { AuthResponse } from '../lib/types'
import { shownError } from '../lib/notify'

export function AuthScreen() {
  const { t, i18n } = useTranslation()
  // A trade's own sign-up link opens straight on that shop's registration: #/start/salon (beauty parlour), #/start/medical (chemist).
  const path = usePath()
  const preset = path === '/start/salon' ? 'beauty' : path === '/start/medical' ? 'medical' : path === '/start/dairy' ? 'dairy' : path === '/start/service' ? 'sales_service' : null
  // Opened from a shop's invite link: straight to a new shop's sign-up, with the code filled in.
  const [referral] = useState(referralFromLink)
  const [mode, setMode] = useState<'sign-in' | 'sign-up'>(preset || referral ? 'sign-up' : 'sign-in')
  const [role, setRole] = useState<'home' | 'shop'>(preset || referral ? 'shop' : 'home')
  const [form, setForm] = useState({ shopName: '', area: '', fullName: '', phone: '', password: '', type: preset ?? 'kirana', referralCode: referral })
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  // Signing in: a code by SMS (default; no password to remember) or a password / counter PIN.
  const [method, setMethod] = useState<'otp' | 'password' | 'reset'>('otp')
  const [otpStep, setOtpStep] = useState<'phone' | 'code' | 'name'>('phone')
  const [code, setCode] = useState('')
  const [info, setInfo] = useState('')
  const useOtp = mode === 'sign-in' && method === 'otp'
  // Forgot password: a code to the mobile, then the code and a new password (signs in at once).
  const resetting = mode === 'sign-in' && method === 'reset'
  const [newPassword, setNewPassword] = useState('')
  // The code can go by SMS to the mobile, or to a recovery email the person verified in Profile.
  const [resetBy, setResetBy] = useState<'sms' | 'email'>('sms')
  const [email, setEmail] = useState('')
  const byEmail = resetting && resetBy === 'email'
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value })

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError('')
    try {
      if (resetting) {
        if (otpStep === 'phone') await sendCode()
        else if (byEmail) await authenticate('/v1/auth/email/reset-password', { email, code, newPassword })
        else await authenticate('/v1/auth/otp/reset-password', { phone: form.phone, code, newPassword })
      }
      else if (useOtp) await otp()
      else if (mode === 'sign-in') await authenticate('/v1/auth/sign-in', { phone: form.phone, password: form.password })
      else if (role === 'shop') {
        await authenticate('/v1/auth/sign-up', { ...form, referralCode: form.referralCode.trim() || null, language: i18n.language })
        forgetReferral()
        navigate('/choose-plan')
      }
      else await authenticate('/v1/auth/register', { fullName: form.fullName, phone: form.phone, password: form.password, language: i18n.language })
    } catch (err) {
      setError(shownError(err))
    } finally {
      setBusy(false)
    }
  }

  async function sendCode() {
    const response = byEmail
      ? await request('/v1/auth/email/send-reset', 'POST', { email })
      : await request('/v1/auth/otp/send', 'POST', { phone: form.phone })
    if (!response.ok) throw new ApiError(response.status, await readProblem(response))
    setCode('')
    setOtpStep('code')
    setInfo(byEmail ? t('emailReset.sent') : t('otp.sent'))
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
    <div className="min-h-dvh bg-gradient-to-b from-teal-700 via-teal-700 to-ground to-[42%]">
    <form onSubmit={submit} className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-4 p-4">
      {/* A branded top: the app's mark, name and what it does. */}
      <div className="flex flex-col items-center gap-2 pb-2 text-center text-white">
        <span aria-hidden className="flex h-16 w-16 items-center justify-center rounded-3xl bg-white/15 text-4xl shadow-lg ring-1 ring-white/25 backdrop-blur">📒</span>
        <h1 className="text-3xl font-extrabold tracking-tight">{t('appName')}</h1>
        <p className="text-[15px] text-white/85">{t('authTagline')}</p>
      </div>
      <div className="flex flex-col gap-4 rounded-3xl bg-white p-5 shadow-[0_12px_40px_rgba(16,32,29,0.16)]">
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
                className={`flex min-h-20 flex-col items-center justify-center gap-1 rounded-2xl px-2 font-semibold transition ${role === r ? 'border-2 border-brand bg-brand-soft text-brand-strong shadow-sm' : 'border border-line bg-white hover:bg-[#f6faf9]'}`}
              >
                <span aria-hidden className="text-2xl">{r === 'home' ? '🏠' : '🏪'}</span>
                {r === 'home' ? t('plan.iAmHome') : t('plan.iAmShop')}
              </button>
            ))}
          </div>}
          {role === 'shop' && <Field label={t('shopName')} value={form.shopName} onChange={set('shopName')} required autoComplete="organization" />}
          {role === 'shop' && <Field label={t('plan.searchShop')} value={form.area} onChange={set('area')} placeholder="Raja Park bazaar" />}
          {role === 'shop' && <Field label={t('referral.codeLabel')} value={form.referralCode} onChange={set('referralCode')} placeholder="sharma-store" autoComplete="off" />}
          {role === 'shop' && !preset && (
            <label className="flex flex-col gap-1">
              <span className="text-sm font-medium text-muted">{t('shopType.label')}</span>
              <select value={form.type} onChange={set('type')} className="h-12 rounded-xl border border-line bg-white px-3 text-lg outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10">
                {SHOP_TYPES.map((type) => <option key={type} value={type}>{t(`shopType.${type}`)}</option>)}
              </select>
            </label>
          )}
          <Field label={t('yourName')} value={form.fullName} onChange={set('fullName')} required autoComplete="name" />
        </>
      )}
      {mode === 'sign-in' && !resetting && <p className="rounded-lg bg-brand-soft p-3 text-sm text-brand-deep">{t('mode.signInHint')}</p>}
      {resetting && (
        <div role="radiogroup" className="grid grid-cols-2 gap-2">
          {(['sms', 'email'] as const).map((by) => (
            <button key={by} type="button" role="radio" aria-checked={resetBy === by} onClick={() => { setResetBy(by); setOtpStep('phone'); setCode(''); setError(''); setInfo('') }}
              className={`min-h-11 rounded-xl px-2 text-sm font-semibold ${resetBy === by ? 'border-2 border-brand bg-brand-soft text-brand-strong' : 'border border-line bg-white'}`}>
              {by === 'sms' ? t('emailReset.bySms') : t('emailReset.byEmail')}
            </button>
          ))}
        </div>
      )}
      {byEmail
        ? <Field label={t('emailReset.email')} value={email} onChange={(e) => { setEmail(e.target.value); setOtpStep('phone') }} required type="email" inputMode="email" autoComplete="email" />
        : <Field label={t('phone')} value={form.phone} onChange={(e) => { set('phone')(e); setOtpStep('phone') }} required inputMode="tel" autoComplete="tel" />}
      {resetting && <p className="rounded-lg bg-brand-soft p-3 text-sm text-brand-deep">{byEmail ? t('emailReset.hint') : t('forgot.hint')}</p>}
      {resetting && otpStep !== 'phone' && (
        <>
          <Field label={t('otp.code')} value={code} onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
            required inputMode="numeric" autoComplete="one-time-code" minLength={6} maxLength={6} />
          <Field label={t('forgot.newPassword')} value={newPassword} onChange={(e) => setNewPassword(e.target.value)} required type="password" minLength={6} autoComplete="new-password" />
        </>
      )}
      {useOtp && otpStep !== 'phone' && (
        <Field label={t('otp.code')} value={code} onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
          required inputMode="numeric" autoComplete="one-time-code" minLength={6} maxLength={6} />
      )}
      {useOtp && otpStep === 'name' && <Field label={t('yourName')} value={form.fullName} onChange={set('fullName')} required autoComplete="name" />}
      {info && !error && <p className="rounded-lg bg-brand-soft p-3 text-brand-deep">{info}</p>}
      {!useOtp && !resetting && <Field
        label={t('password')}
        value={form.password}
        onChange={set('password')}
        required
        type="password"
        // A new password needs 6 characters; signing in also takes a helper's 4-digit counter PIN.
        minLength={mode === 'sign-in' ? 4 : 6}
        autoComplete={mode === 'sign-in' ? 'current-password' : 'new-password'}
      />}
      {error && <p className="rounded-lg bg-due-soft p-3 text-due">{error}</p>}
      <Button type="submit" disabled={busy} className="h-14">
        {resetting ? (otpStep === 'phone' ? (byEmail ? t('emailReset.sendCode') : t('otp.send')) : t('forgot.save')) : useOtp ? (otpStep === 'phone' ? t('otp.send') : t('signIn')) : mode === 'sign-in' ? t('signIn') : role === 'shop' ? t('signUp') : t('plan.register')}
      </Button>
      {(useOtp || resetting) && otpStep !== 'phone' && (
        <button type="button" disabled={busy} className="text-brand underline" onClick={() => { setError(''); void sendCode().catch((err: Error) => setError(err.message)) }}>{t('otp.resend')}</button>
      )}
      {mode === 'sign-in' && method === 'password' && (
        <button type="button" className="min-h-10 text-sm font-semibold text-brand underline" onClick={() => { setMethod('reset'); setOtpStep('phone'); setCode(''); setError(''); setInfo('') }}>
          {t('forgot.link')}
        </button>
      )}
      {mode === 'sign-in' && (
        <button type="button" className="min-h-10 text-sm font-semibold text-brand underline" onClick={() => { setMethod(method === 'otp' ? 'password' : 'otp'); setOtpStep('phone'); setError(''); setInfo('') }}>
          {method === 'otp' ? t('otp.usePassword') : t('otp.useCode')}
        </button>
      )}
      </div>
      <button type="button" className={`min-h-12 rounded-xl ${btn.outline}`} onClick={() => setMode(mode === 'sign-in' ? 'sign-up' : 'sign-in')}>
        {mode === 'sign-in' ? t('newShop') : t('haveAccount')}
      </button>
      <button type="button" className="text-sm font-semibold text-brand underline" onClick={() => navigate('/pricing')}>{t('jwl7.pricing')}</button>
      {/* Field agents sign in (or register as a person) first; the app then opens their application. */}
      {agentPath
        ? <p className="rounded-lg bg-warn-soft p-3 text-sm text-warn-ink">{t('fieldAgent.signInFirst')}</p>
        : <button type="button" className="text-sm text-muted underline" onClick={() => { navigate('/agent'); setMode('sign-up'); setRole('home') }}>{t('fieldAgent.join')}</button>}
    </form>
    </div>
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
    <div role="alert" className="mx-4 mt-3 flex flex-col gap-1 rounded-2xl bg-due-soft p-4 text-red-900">
      {session.blockedShops.map((b) => (
        <p key={b.shopName}><b>{t('blocked.title', { shop: b.shopName })}</b><br />{t('blocked.reason', { reason: b.reason })}</p>
      ))}
      <p className="text-sm">{t('blocked.help')}</p>
    </div>
  )
}

import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { authenticate } from '../lib/api'
import type { AuthResponse } from '../lib/types'

export function AuthScreen() {
  const { t, i18n } = useTranslation()
  const [mode, setMode] = useState<'sign-in' | 'sign-up'>('sign-in')
  const [form, setForm] = useState({ shopName: '', fullName: '', phone: '', password: '' })
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value })

  async function submit(e: FormEvent) {
    e.preventDefault()
    setBusy(true)
    setError('')
    try {
      if (mode === 'sign-in') await authenticate('/v1/auth/sign-in', { phone: form.phone, password: form.password })
      else await authenticate('/v1/auth/sign-up', { ...form, language: i18n.language })
    } catch (err) {
      setError(err instanceof Error ? err.message : t('error'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={submit} className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-4 p-4">
      <h1 className="text-center text-3xl font-bold text-teal-800">{t('appName')}</h1>
      {mode === 'sign-up' && (
        <>
          <Field label={t('shopName')} value={form.shopName} onChange={set('shopName')} required autoComplete="organization" />
          <Field label={t('yourName')} value={form.fullName} onChange={set('fullName')} required autoComplete="name" />
        </>
      )}
      <Field label={t('phone')} value={form.phone} onChange={set('phone')} required inputMode="tel" autoComplete="tel" />
      <Field
        label={t('password')}
        value={form.password}
        onChange={set('password')}
        required
        type="password"
        minLength={6}
        autoComplete={mode === 'sign-in' ? 'current-password' : 'new-password'}
      />
      {error && <p className="rounded-lg bg-rose-50 p-3 text-rose-700">{error}</p>}
      <Button type="submit" disabled={busy}>
        {mode === 'sign-in' ? t('signIn') : t('signUp')}
      </Button>
      <button type="button" className="text-teal-700 underline" onClick={() => setMode(mode === 'sign-in' ? 'sign-up' : 'sign-in')}>
        {mode === 'sign-in' ? t('newShop') : t('haveAccount')}
      </button>
    </form>
  )
}

/** Shown when one person belongs to several shops and hasn't picked one. */
export function ShopPicker({ session }: { session: AuthResponse }) {
  const { t } = useTranslation()
  const [error, setError] = useState('')
  return (
    <div className="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-3 p-4">
      <h1 className="text-2xl font-bold">{t('pickShop')}</h1>
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
      {error && <p className="text-rose-700">{error}</p>}
    </div>
  )
}

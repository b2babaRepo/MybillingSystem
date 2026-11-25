import { CUSTOMER_LANGUAGES, LANGUAGES, setLanguage, type Language } from '../i18n'
import { getSession, setSession } from '../lib/session'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { formatPhone } from '../lib/format'

interface Profile { fullName: string; phone: string; preferredLanguage: string; email: string | null; address: string | null; city: string | null; pincode: string | null; hasPassword?: boolean; gstin?: string | null }

/** The signed-in person's own details. The mobile number is the login, so it only shows here. */
/** customer: the shopping app's profile (offers Hinglish too); the shop app's leaves it out. */
export function ProfileScreen({ customer = false }: { customer?: boolean }) {
  const profile = useQuery({ queryKey: ['profile'], queryFn: () => api<Profile>('/v1/profile') })
  if (!profile.data) return <p className="p-4 text-muted">…</p>
  return <ProfileForm initial={profile.data} customer={customer} />
}

function ProfileForm({ initial, customer }: { initial: Profile; customer: boolean }) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const [form, setForm] = useState({
    fullName: initial.fullName, preferredLanguage: initial.preferredLanguage, email: initial.email ?? '',
    address: initial.address ?? '', city: initial.city ?? '', pincode: initial.pincode ?? '', gstin: initial.gstin ?? '',
  })
  const [message, setMessage] = useState('')
  const [changing, setChanging] = useState(false)
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value })

  const [current, setCurrent] = useState('')
  const [fresh, setFresh] = useState('')
  async function savePassword() {
    setMessage('')
    try {
      await api('/v1/profile/password', 'PUT', { currentPassword: current || null, newPassword: fresh })
      setCurrent('')
      setFresh('')
      setMessage(t('profile.passwordSaved'))
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  async function save() {
    setMessage('')
    try {
      await api('/v1/profile', 'PUT', customer ? form : { ...form, gstin: undefined })
      await queryClient.invalidateQueries({ queryKey: ['profile'] })
      // The name and language the app shows follow at once (the account keeps them too).
      const session = getSession()
      if (session) setSession({ ...session, user: { ...session.user, fullName: form.fullName.trim(), preferredLanguage: form.preferredLanguage } })
      void setLanguage(form.preferredLanguage as Language, { toAccount: false })
      void i18n
      setMessage(t('saved'))
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  return (
    <section className="m-4 flex flex-col gap-3 rounded-card bg-white p-4">
      <Field label={t('profile.name')} value={form.fullName} onChange={set('fullName')} maxLength={120} />
      <div className="flex items-center justify-between gap-2">
        <p className="text-sm text-muted">{t('profile.phone')}: <b className="text-ink">{formatPhone(initial.phone)}</b></p>
        <button onClick={() => setChanging(true)} className="min-h-11 rounded-xl border border-line px-3 text-sm font-bold text-brand">{t('cust9.changeNumber')}</button>
      </div>
      {changing && <PhoneChangeSheet hasPassword={!!initial.hasPassword} onClose={() => setChanging(false)} />}
      <Field label={t('profile.email')} value={form.email} onChange={set('email')} inputMode="email" maxLength={160} />
      <Field label={t('profile.address')} value={form.address} onChange={set('address')} maxLength={300} />
      <div className="grid grid-cols-2 gap-2">
        <Field label={t('profile.city')} value={form.city} onChange={set('city')} maxLength={80} />
        <Field label={t('profile.pincode')} value={form.pincode} onChange={set('pincode')} inputMode="numeric" maxLength={6} />
      </div>
      {customer && <>
        <Field label={t('cust7.gstin')} value={form.gstin} onChange={set('gstin')} maxLength={15} />
        <p className="-mt-2 text-xs text-muted">{t('cust7.gstinHint')}</p>
      </>}
      <label className="flex flex-col gap-1">
        <span className="text-sm font-medium text-muted">{t('profile.language')}</span>
        <select value={form.preferredLanguage} onChange={set('preferredLanguage')} className="h-12 rounded-xl border border-line bg-white px-3 text-lg">
          {(customer ? CUSTOMER_LANGUAGES : LANGUAGES).map((l) => <option key={l.code} value={l.code}>{l.name}</option>)}
        </select>
      </label>
      <Button disabled={form.fullName.trim().length < 2} onClick={() => void save()}>{t('save')}</Button>
      {message && <p className="text-sm text-muted">{message}</p>}
      <div className="flex flex-col gap-2 border-t border-line pt-3">
        <h2 id="password" className="font-bold">{t('profile.passwordTitle')}</h2>
        {initial.hasPassword ? <Field label={t('profile.passwordCurrent')} type="password" value={current} onChange={(e) => setCurrent(e.target.value)} autoComplete="current-password" /> : <p className="text-sm text-muted">{t('profile.passwordNone')}</p>}
        <Field label={t('profile.passwordNew')} type="password" value={fresh} onChange={(e) => setFresh(e.target.value)} autoComplete="new-password" />
        <Button disabled={fresh.length < 6} onClick={() => void savePassword()}>{t('profile.passwordSave')}</Button>
      </div>
    </section>
  )
}

/**
 * Moving the account to a new number: a code to the new number, and the password (or, with no password, a code to the
 * old number). Every other sign-in ends; this phone carries on under the new number.
 */
function PhoneChangeSheet({ hasPassword, onClose }: { hasPassword: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [phone, setPhone] = useState('')
  const [sent, setSent] = useState(false)
  const [code, setCode] = useState('')
  const [password, setPassword] = useState('')
  const [oldSent, setOldSent] = useState(false)
  const [oldCode, setOldCode] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [done, setDone] = useState(false)
  async function run(action: () => Promise<void>) {
    setBusy(true)
    setError('')
    try { await action() } catch (e) { setError(e instanceof Error ? e.message : t('error')) } finally { setBusy(false) }
  }
  const send = () => run(async () => { await api('/v1/profile/phone/send', 'POST', { newPhone: phone }); setSent(true) })
  const sendOld = () => run(async () => { await api('/v1/profile/phone/send', 'POST', { toOld: true }); setOldSent(true) })
  const change = () => run(async () => {
    const session = await api<NonNullable<ReturnType<typeof getSession>>>('/v1/profile/phone', 'POST', { newPhone: phone, code, password: hasPassword ? password : null, oldCode: hasPassword ? null : oldCode })
    setSession(session)
    await queryClient.invalidateQueries({ queryKey: ['profile'] })
    setDone(true)
  })
  return (
    <div className="fixed inset-0 z-30 flex items-end bg-black/40 lg:items-center lg:justify-center lg:p-6" onClick={onClose}>
      <div role="dialog" aria-label={t('cust9.changeNumber')} onClick={(e) => e.stopPropagation()} className="flex max-h-[90dvh] w-full flex-col gap-3 overflow-y-auto rounded-t-3xl bg-white p-4 lg:max-w-md lg:rounded-3xl">
        <h2 className="text-lg font-extrabold">{t('cust9.changeNumber')}</h2>
        {done ? <>
          <p className="rounded-xl bg-brand-soft p-3 font-semibold text-brand-deep">{t('cust9.changed')}</p>
          <p className="text-sm text-muted">{t('cust9.khataNote')}</p>
          <Button onClick={onClose}>{t('a11y.close')}</Button>
        </> : <>
          <Field label={t('cust9.newNumber')} value={phone} onChange={(e) => { setPhone(e.target.value); setSent(false) }} inputMode="tel" maxLength={14} />
          {!sent ? <Button disabled={busy || phone.replace(/\D/g, '').length < 10} onClick={() => void send()}>{t('cust9.sendCode')}</Button> : <>
            <Field label={t('cust9.codeNew')} value={code} onChange={(e) => setCode(e.target.value)} inputMode="numeric" maxLength={6} autoComplete="one-time-code" />
            {hasPassword
              ? <Field label={t('profile.passwordCurrent')} type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" />
              : !oldSent
                ? <button onClick={() => void sendOld()} disabled={busy} className="min-h-11 rounded-xl border border-line font-bold text-brand">{t('cust9.sendOld')}</button>
                : <Field label={t('cust9.codeOld')} value={oldCode} onChange={(e) => setOldCode(e.target.value)} inputMode="numeric" maxLength={6} />}
            <Button disabled={busy || code.length !== 6 || (hasPassword ? password.length < 6 : oldCode.length !== 6)} onClick={() => void change()}>{t('cust9.change')}</Button>
          </>}
          {error && <p className="text-sm text-due">{error}</p>}
          <p className="text-xs text-muted">{hasPassword ? t('cust9.hintPassword') : t('cust9.hintNoPassword')}</p>
        </>}
      </div>
    </div>
  )
}

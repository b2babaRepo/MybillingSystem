import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { formatPhone } from '../lib/format'

interface Profile { fullName: string; phone: string; preferredLanguage: string; email: string | null; address: string | null; city: string | null; pincode: string | null; hasPassword?: boolean }

/** The signed-in person's own details. The mobile number is the login, so it only shows here. */
export function ProfileScreen() {
  const profile = useQuery({ queryKey: ['profile'], queryFn: () => api<Profile>('/v1/profile') })
  if (!profile.data) return <p className="p-4 text-muted">…</p>
  return <ProfileForm initial={profile.data} />
}

function ProfileForm({ initial }: { initial: Profile }) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const [form, setForm] = useState({
    fullName: initial.fullName, preferredLanguage: initial.preferredLanguage, email: initial.email ?? '',
    address: initial.address ?? '', city: initial.city ?? '', pincode: initial.pincode ?? '',
  })
  const [message, setMessage] = useState('')
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
      await api('/v1/profile', 'PUT', form)
      await queryClient.invalidateQueries({ queryKey: ['profile'] })
      void i18n.changeLanguage(form.preferredLanguage)
      setMessage(t('saved'))
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  return (
    <section className="m-4 flex flex-col gap-3 rounded-[18px] bg-white p-4">
      <Field label={t('profile.name')} value={form.fullName} onChange={set('fullName')} maxLength={120} />
      <p className="text-sm text-muted">{t('profile.phone')}: {formatPhone(initial.phone)}</p>
      <Field label={t('profile.email')} value={form.email} onChange={set('email')} inputMode="email" maxLength={160} />
      <Field label={t('profile.address')} value={form.address} onChange={set('address')} maxLength={300} />
      <div className="grid grid-cols-2 gap-2">
        <Field label={t('profile.city')} value={form.city} onChange={set('city')} maxLength={80} />
        <Field label={t('profile.pincode')} value={form.pincode} onChange={set('pincode')} inputMode="numeric" maxLength={6} />
      </div>
      <label className="flex flex-col gap-1">
        <span className="text-sm font-medium text-muted">{t('profile.language')}</span>
        <select value={form.preferredLanguage} onChange={set('preferredLanguage')} className="h-12 rounded-xl border border-line bg-white px-3 text-lg">
          <option value="hi">हिंदी</option><option value="en">English</option>
        </select>
      </label>
      <Button disabled={form.fullName.trim().length < 2} onClick={() => void save()}>{t('save')}</Button>
      {message && <p className="text-sm text-muted">{message}</p>}
      <div className="flex flex-col gap-2 border-t border-line pt-3">
        <h2 className="font-bold">{t('profile.passwordTitle')}</h2>
        {initial.hasPassword ? <Field label={t('profile.passwordCurrent')} type="password" value={current} onChange={(e) => setCurrent(e.target.value)} autoComplete="current-password" /> : <p className="text-sm text-muted">{t('profile.passwordNone')}</p>}
        <Field label={t('profile.passwordNew')} type="password" value={fresh} onChange={(e) => setFresh(e.target.value)} autoComplete="new-password" />
        <Button disabled={fresh.length < 6} onClick={() => void savePassword()}>{t('profile.passwordSave')}</Button>
      </div>
    </section>
  )
}

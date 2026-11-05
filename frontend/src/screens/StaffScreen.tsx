import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api, request } from '../lib/api'
import { formatPhone } from '../lib/format'
import { ask, confirmAsk } from '../components/Ask'

interface Staff {
  membershipId: string
  userId: string
  name: string
  phone: string
  role: 'owner' | 'staff'
  isActive: boolean
  hasPin: boolean
}

/**
 * The owner's helpers. Add one by name and phone with a 4-6 digit counter PIN; they sign in with phone + PIN and
 * land in this shop, able to bill and keep khata but not to open owner screens. Change a PIN (also lifts a
 * lockout) or turn access off: they're signed out on their next tap.
 */
export function StaffScreen() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const staff = useQuery({ queryKey: ['staff'], queryFn: () => api<Staff[]>('/v1/shop/staff') })
  const [name, setName] = useState('')
  const [phone, setPhone] = useState('')
  const [pin, setPin] = useState('')
  const [role, setRole] = useState<'staff' | 'owner'>('staff')
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function run(action: () => Promise<unknown>, done?: string) {
    setBusy(true)
    setMessage(null)
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: ['staff'] })
      if (done) setMessage(done)
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  const add = () => run(async () => {
    await api('/v1/shop/staff', 'POST', { name: name.trim(), phone: phone.trim(), role, pin: role === 'staff' ? pin : null })
    setName('')
    setPhone('')
    setPin('')
  }, role === 'owner' ? t('staff.ownerAdded') : t('staff.added'))

  async function changePin(s: Staff) {
    const next = (await ask(t('staff.newPinAsk', { name: s.name })))?.trim()
    if (next) void run(async () => {
      const response = await request(`/v1/shop/staff/${s.membershipId}/pin`, 'POST', { pin: next })
      if (!response.ok) throw new Error(((await response.json()) as { errors?: Record<string, string[]> }).errors?.Pin?.[0] ?? t('error'))
    }, t('staff.pinChanged', { name: s.name }))
  }

  const setActive = async (s: Staff, active: boolean) =>
    (active || (await confirmAsk(t('staff.offConfirm', { name: s.name })))) &&
    void run(() => request(`/v1/shop/staff/${s.membershipId}/active?active=${active}`, 'POST', {}))

  return (
    <div className="flex flex-col gap-3 p-4">
      <p className="text-sm text-muted">{t('staff.hint')}</p>
      {message && <p className="rounded-[14px] bg-teal-50 p-3 text-sm font-semibold text-teal-900">{message}</p>}

      <ul className="divide-y divide-[#edf2f1] rounded-[18px] bg-white">
        {staff.data?.map((s) => (
          <li key={s.membershipId} className={`flex flex-col gap-2 p-4 ${s.isActive ? '' : 'opacity-60'}`}>
            <div className="flex items-start justify-between gap-2">
              <span className="flex flex-col">
                <span className="text-[17px] font-bold">{s.name}</span>
                <span className="text-sm text-muted">{formatPhone(s.phone)}</span>
              </span>
              <span className={`rounded-full px-2 py-0.5 text-xs font-bold ${s.role === 'owner' ? 'bg-teal-50 text-teal-800' : s.isActive ? 'bg-[#edf2f1]' : 'bg-red-50 text-due'}`}>
                {s.role === 'owner' ? t('staff.owner') : s.isActive ? t('staff.helper') : t('staff.off')}
              </span>
            </div>
            {(s.role === 'staff' || staff.data!.filter((x) => x.role === 'owner' && x.isActive).length > 1 || !s.isActive) && (
              <div className="grid grid-cols-2 gap-2 text-sm">
                {s.role === 'staff' ? <button disabled={busy} onClick={() => changePin(s)} className="min-h-11 rounded-xl border border-line font-semibold">🔑 {t('staff.changePin')}</button> : <span />}
                <button disabled={busy} onClick={() => setActive(s, !s.isActive)}
                  className={`min-h-11 rounded-xl font-semibold ${s.isActive ? 'border border-line text-due' : 'bg-teal-700 text-white'}`}>
                  {s.isActive ? t('staff.turnOff') : t('staff.turnOn')}
                </button>
              </div>
            )}
          </li>
        ))}
      </ul>

      <section className="flex flex-col gap-3 rounded-[18px] bg-white p-4">
        <h2 className="text-lg font-bold">{t('staff.addTitle')}</h2>
        <Field label={t('staff.name')} value={name} onChange={(e) => setName(e.target.value)} />
        <Field label={t('staff.phone')} value={phone} onChange={(e) => setPhone(e.target.value)} inputMode="tel" />
        <div role="radiogroup" aria-label={t('staff.roleLabel')} className="grid grid-cols-2 gap-2">
          {(['staff', 'owner'] as const).map((r) => (
            <button key={r} type="button" role="radio" aria-checked={role === r} onClick={() => setRole(r)}
              className={`min-h-12 rounded-xl font-semibold ${role === r ? 'bg-teal-700 text-white' : 'border border-line'}`}>{r === 'owner' ? t('staff.owner') : t('staff.helper')}</button>
          ))}
        </div>
        {role === 'staff' ? <>
          <Field label={t('staff.pin')} value={pin} onChange={(e) => setPin(e.target.value.replace(/\D/g, '').slice(0, 6))} inputMode="numeric" />
          <p className="-mt-2 text-xs text-muted">{t('staff.pinHint')}</p>
        </> : <p className="text-xs text-muted">{t('staff.ownerHint')}</p>}
        <p className="text-xs text-muted">{t('staff.planLimit')}</p>
        <Button disabled={busy || name.trim().length < 2 || phone.trim().length < 10 || (role === 'staff' && pin.length < 4)} onClick={() => void add()}>
          {t('staff.add')}
        </Button>
      </section>
    </div>
  )
}

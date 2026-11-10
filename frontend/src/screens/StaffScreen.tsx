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
  commissionPercent?: number | null
  blocked?: string[] | null
}

const LIMITS = ['udhaar', 'cancel', 'items', 'purchases', 'expenses', 'reports'] as const

/** What the owner can stop one helper doing. Nothing is stopped until ticked; the server enforces it. */
function Limits({ staff, onSaved }: { staff: Staff; onSaved: () => void }) {
  const { t } = useTranslation()
  const [blocked, setBlocked] = useState<string[]>(staff.blocked ?? [])
  const [busy, setBusy] = useState(false)
  async function toggle(limit: string) {
    const next = blocked.includes(limit) ? blocked.filter((b) => b !== limit) : [...blocked, limit]
    setBusy(true)
    try {
      await api(`/v1/shop/staff/${staff.membershipId}/limits`, 'POST', { blocked: next })
      setBlocked(next)
      onSaved()
    } catch (e) {
      window.alert(e instanceof Error ? e.message : t('error'))
    } finally {
      setBusy(false)
    }
  }
  return (
    <fieldset className="flex flex-col gap-1 text-sm">
      <legend className="font-semibold text-muted">{t('staffLimits.title')}</legend>
      {LIMITS.map((limit) => (
        <label key={limit} className="flex min-h-10 items-center gap-2">
          <input type="checkbox" disabled={busy} checked={blocked.includes(limit)} onChange={() => void toggle(limit)} className="h-5 w-5" />
          {t(`staffLimits.${limit}`)}
        </label>
      ))}
    </fieldset>
  )
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
    <div className="flex flex-col gap-3 p-4 lg:grid lg:grid-cols-[minmax(0,1fr)_360px] lg:items-start lg:gap-4 lg:[&>p]:col-start-1 lg:[&>ul]:col-start-1 lg:[&>section]:col-start-2 lg:[&>section]:row-span-3 lg:[&>section]:row-start-1">
      <p className="text-sm text-muted">{t('staff.hint')}</p>
      {message && <p className="rounded-[14px] bg-teal-50 p-3 text-sm font-semibold text-teal-900">{message}</p>}

      <ul className="divide-y divide-[#edf2f1] rounded-[18px] bg-white shadow-sm">
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
            <Commission staff={s} onSaved={() => void staff.refetch()} />
            {s.role === 'staff' && s.isActive && <Limits staff={s} onSaved={() => void staff.refetch()} />}
            {(s.role === 'staff' || staff.data!.filter((x) => x.role === 'owner' && x.isActive).length > 1 || !s.isActive) && (
              <div className="grid grid-cols-2 gap-2 text-sm">
                {s.role === 'staff' ? <button disabled={busy} onClick={() => changePin(s)} className="min-h-11 rounded-xl border border-line font-semibold transition hover:bg-[#f6faf9]">🔑 {t('staff.changePin')}</button> : <span />}
                <button disabled={busy} onClick={() => setActive(s, !s.isActive)}
                  className={`min-h-11 rounded-xl font-semibold ${s.isActive ? 'border border-line text-due' : 'bg-teal-700 text-white'}`}>
                  {s.isActive ? t('staff.turnOff') : t('staff.turnOn')}
                </button>
              </div>
            )}
          </li>
        ))}
      </ul>

      <section className="flex flex-col gap-3 rounded-[18px] bg-white p-4 shadow-sm">
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

/** The person's commission on their sales, in percent (0 or empty: none). The staff sales report works it out. */
function Commission({ staff, onSaved }: { staff: { membershipId: string; commissionPercent?: number | null }; onSaved: () => void }) {
  const { t } = useTranslation()
  const [value, setValue] = useState(staff.commissionPercent ? String(staff.commissionPercent) : '')
  const [saved, setSaved] = useState(false)
  async function save() {
    await api(`/v1/shop/staff/${staff.membershipId}/commission`, 'POST', { percent: Number(value) || null })
    setSaved(true)
    onSaved()
  }
  return (
    <label className="flex items-center gap-2 text-sm">
      <span className="flex-1 text-muted">{t('sales.commission')}</span>
      <input value={value} onChange={(e) => { setValue(e.target.value); setSaved(false) }} inputMode="decimal" placeholder="0" aria-label={t('sales.commission')} className="h-10 w-20 rounded-lg border border-line px-2 text-center" />
      <span>%</span>
      <button type="button" onClick={() => void save()} className="min-h-10 rounded-lg border border-line px-3 font-semibold">{saved ? '✓' : t('save')}</button>
    </label>
  )
}

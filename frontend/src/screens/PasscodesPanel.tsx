import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { askSecret } from '../components/Ask'
import { api } from '../lib/api'
import { clearAppLock, hasAppLock, passcodeProblem, setAppLock } from '../lib/appLock'

/**
 * Two passcodes in Settings. The app passcode locks the app on this phone (on opening, and after 2 minutes away). The
 * bill passcode is the shop's, checked by the server: cancelling a bill, a return or credit note, cancelling an exchange
 * slip or a supplier bill asks for it, on every phone.
 */
export function PasscodesPanel() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [appLock, setAppLockOn] = useState(hasAppLock)
  const [message, setMessage] = useState('')
  const bill = useQuery({ queryKey: ['bill-passcode'], queryFn: () => api<{ set: boolean }>('/v1/shop/bill-passcode') })

  /** Asks for a new passcode twice (to catch a slip) and checks it isn't too easy. Null when cancelled or not matching. */
  async function newPasscode(): Promise<string | null> {
    const first = await askSecret(t('passcode.new'))
    if (!first) return null
    const problem = passcodeProblem(first)
    if (problem) { setMessage(t(`passcode.${problem}`)); return null }
    const again = await askSecret(t('passcode.again'))
    if (again !== first) { setMessage(t('passcode.mismatch')); return null }
    return first
  }

  async function setLock() {
    setMessage('')
    const pin = await newPasscode()
    if (!pin) return
    await setAppLock(pin)
    setAppLockOn(true)
    setMessage(t('passcode.appSet'))
  }

  async function saveBill(remove: boolean) {
    setMessage('')
    const current = bill.data?.set ? await askSecret(t('passcode.current')) : null
    if (bill.data?.set && !current) return
    const passcode = remove ? null : await newPasscode()
    if (!remove && !passcode) return
    try {
      await api('/v1/shop/bill-passcode', 'PUT', { passcode, current })
      await queryClient.invalidateQueries({ queryKey: ['bill-passcode'] })
      setMessage(t(remove ? 'passcode.billRemoved' : 'passcode.billSet'))
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('error'))
    }
  }

  const row = (icon: string, title: string, hint: string, on: boolean, set: () => void, remove: () => void) => (
    <div className="flex flex-col gap-2 rounded-2xl border border-line p-3">
      <div className="flex items-start gap-3">
        <span aria-hidden className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-[#edf2f1] text-lg">{icon}</span>
        <span className="flex flex-1 flex-col">
          <span className="font-bold">{title} <span className={`ml-1 rounded-full px-2 py-0.5 text-xs ${on ? 'bg-brand-soft text-brand-strong' : 'bg-[#edf2f1] text-muted'}`}>{t(on ? 'passcode.on' : 'passcode.off')}</span></span>
          <span className="text-sm text-muted">{hint}</span>
        </span>
      </div>
      <div className="flex gap-2">
        <button onClick={set} className="min-h-11 flex-1 rounded-xl bg-brand px-3 font-semibold text-white">{t(on ? 'passcode.change' : 'passcode.set')}</button>
        {on && <button onClick={remove} className="min-h-11 rounded-xl border border-line px-3 font-semibold text-due">{t('passcode.remove')}</button>}
      </div>
    </div>
  )

  return (
    <section className="mt-4 flex flex-col gap-3 rounded-card bg-white p-4">
      <h2 className="text-lg font-bold">{t('passcode.title')}</h2>
      {row('📱', t('passcode.appTitle'), t('passcode.appHint'), appLock, () => void setLock(), () => { clearAppLock(); setAppLockOn(false); setMessage(t('passcode.appRemoved')) })}
      {bill.data && row('🧾', t('passcode.billTitle'), t('passcode.billHint'), bill.data.set, () => void saveBill(false), () => void saveBill(true))}
      {message && <p role="status" className="text-sm font-semibold">{message}</p>}
    </section>
  )
}

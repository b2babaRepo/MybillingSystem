import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { confirmAsk } from '../components/Ask'
import { api, request } from '../lib/api'
import { formatRupees, whatsAppLink } from '../lib/format'
import { buildReminder, useShopSettings } from '../lib/reports'
import { navigate } from '../router'

interface Item {
  customerId: string
  name: string
  phone: string | null
  balance: number
  oldestDays: number
  lastRemindedAt: string | null
  hold: 'noPhone' | 'noConsent' | 'moneyWaiting' | 'recent' | 'tooNew' | null
}

/**
 * Today's reminders: who to send to, one tap each, and who is being left alone and why. The app never messages anyone
 * by itself here. A customer appears only once the shop has recorded that they agreed.
 */
export function RemindersScreen({ shopId, shopName }: { shopId: string; shopName: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const key = [shopId, 'reminders']
  const plan = useQuery({ queryKey: key, queryFn: () => api<{ ready: Item[]; held: Item[] }>('/v1/reminders/today') })
  const settings = useShopSettings(shopId)
  const [message, setMessage] = useState('')
  const refresh = () => queryClient.invalidateQueries({ queryKey: key })

  // Opening WhatsApp must not wait for the server (pop-up blockers); the log goes in the background.
  const sent = (i: Item) => { void request(`/v1/customers/${i.customerId}/reminded`, 'POST').then(refresh).catch(() => {}) }

  async function consent(i: Item, given: boolean) {
    if (!(await confirmAsk(t(given ? 'reminders.agreeConfirm' : 'reminders.stopConfirm', { name: i.name })))) return
    setMessage('')
    try {
      await api(`/v1/customers/${i.customerId}/reminder-consent`, 'PUT', { given })
      await refresh()
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  if (plan.isError) return <p className="p-4 text-due">{plan.error.message}</p>
  if (!plan.data) return <p className="p-4 text-muted">…</p>
  const { ready, held } = plan.data
  const reasons = ['noConsent', 'moneyWaiting', 'recent', 'tooNew', 'noPhone'] as const

  return (
    <div className="flex flex-col gap-3 p-4">
      <h1 className="text-lg font-bold">{t('reminders.title')}</h1>
      <p className="text-sm text-muted">{t('reminders.hint')}</p>
      {message && <p className="text-sm font-semibold text-due">{message}</p>}
      {ready.length === 0 && <p className="rounded-card bg-white p-4 text-center text-muted">{t('reminders.none')}</p>}
      {ready.map((i) => (
        <section key={i.customerId} className="flex flex-col gap-2 rounded-card bg-white p-4">
          <button onClick={() => navigate(`/c/${i.customerId}`)} className="flex items-start justify-between gap-2 text-left">
            <span className="min-w-0">
              <span className="block truncate font-bold">{i.name}</span>
              <span className="block text-sm text-muted">{t('dues.oldest', { days: i.oldestDays })}</span>
            </span>
            <span className="shrink-0 text-lg font-bold text-due">{formatRupees(i.balance)}</span>
          </button>
          <a href={whatsAppLink(i.phone, buildReminder(settings.data?.reminderTemplate, { name: i.name, amount: i.balance, shop: shopName, upi: settings.data?.upiId ?? null, slug: settings.data?.slug }))}
            target="_blank" rel="noreferrer" onClick={() => sent(i)} className="flex h-12 items-center justify-center rounded-2xl bg-[#15803d] font-bold text-white">{t('reminders.send')}</a>
          <button onClick={() => void consent(i, false)} className="min-h-11 self-start text-sm font-semibold text-brand underline">{t('reminders.stop')}</button>
        </section>
      ))}
      {reasons.map((reason) => {
        const group = held.filter((h) => h.hold === reason)
        return group.length === 0 ? null : (
          <section key={reason} className="flex flex-col gap-1 rounded-card bg-white p-4">
            <h2 className="font-bold">{t(`reminders.hold_${reason}`)} · {group.length}</h2>
            <ul className="divide-y divide-[#edf2f1]">
              {group.map((h) => (
                <li key={h.customerId} className="flex items-center justify-between gap-2 py-1.5 text-sm">
                  <button onClick={() => navigate(`/c/${h.customerId}`)} className="min-h-11 min-w-0 truncate text-left">{h.name} · {formatRupees(h.balance)}</button>
                  {reason === 'noConsent' && <button onClick={() => void consent(h, true)} className="min-h-11 shrink-0 font-semibold text-brand underline">{t('reminders.agreed')}</button>}
                </li>
              ))}
            </ul>
          </section>
        )
      })}
    </div>
  )
}

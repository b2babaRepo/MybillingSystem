import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { confirmAsk } from '../components/Ask'
import { api, request } from '../lib/api'
import { formatRupees, whatsAppLink } from '../lib/format'
import { buildReminder, useShopSettings } from '../lib/reports'
import { navigate } from '../router'
import { DataTable } from '../components/DataTable'

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
    <div className="flex flex-col gap-3 p-4 lg:mx-auto lg:w-full lg:max-w-5xl">
      <h1 className="text-lg font-bold">{t('reminders.title')}</h1>
      <p className="text-sm text-muted">{t('reminders.hint')}</p>
      {message && <p className="text-sm font-semibold text-due">{message}</p>}
      {/* A computer: everyone owing in one table, ready to send first, the rest with why they are left alone. */}
      {ready.length + held.length > 0 && (
        <div className="hidden lg:block">
          <DataTable label={t('reminders.title')} rows={[...ready, ...held]} rowKey={(i) => i.customerId} download="reminders"
            columns={[
              { key: 'name', header: t('chequeTable.customer'), sortValue: (i) => i.name, filter: { kind: 'text', value: (i) => `${i.name} ${i.phone ?? ''}` },
                render: (i) => <button onClick={() => navigate(`/c/${i.customerId}`)} className="text-left font-semibold hover:text-brand hover:underline">{i.name}</button>, csv: (i) => i.name },
              { key: 'phone', header: t('remTable.phone'), width: 'w-36', render: (i) => <span className="text-muted">{i.phone ?? '—'}</span>, csv: (i) => i.phone ?? '' },
              { key: 'owes', header: t('remTable.owes'), align: 'right', width: 'w-28', sortValue: (i) => i.balance, render: (i) => <b className="text-due">{formatRupees(i.balance)}</b>, csv: (i) => i.balance },
              { key: 'oldest', header: t('supTable.oldest'), align: 'right', width: 'w-28', sortValue: (i) => i.oldestDays, render: (i) => t('supTable.days', { count: i.oldestDays }), csv: (i) => i.oldestDays },
              { key: 'last', header: t('remTable.last'), width: 'w-32', sortValue: (i) => i.lastRemindedAt ?? '', render: (i) => (i.lastRemindedAt ? new Date(i.lastRemindedAt).toLocaleDateString() : '—'), csv: (i) => i.lastRemindedAt?.slice(0, 10) ?? '' },
              { key: 'status', header: t('chequeTable.status'), width: 'w-48', sortValue: (i) => i.hold ?? '',
                filter: { kind: 'select', value: (i) => i.hold ?? 'ready', options: [{ value: 'ready', label: t('remTable.ready') }, ...reasons.map((x) => ({ value: x, label: t(`reminders.hold_${x}`) }))] },
                render: (i) => (i.hold ? <span className="text-xs text-muted">{t(`reminders.hold_${i.hold}`)}</span> : <span className="rounded-full bg-green-50 px-2 py-0.5 text-xs font-bold text-green-800">{t('remTable.ready')}</span>),
                csv: (i) => (i.hold ? t(`reminders.hold_${i.hold}`) : t('remTable.ready')) },
              { key: 'act', header: '', width: 'w-52', render: (i) => !i.hold ? (
                <span className="flex items-center justify-end gap-2">
                  <a href={whatsAppLink(i.phone, buildReminder(settings.data?.reminderTemplate, { name: i.name, amount: i.balance, shop: shopName, upi: settings.data?.upiId ?? null, slug: settings.data?.slug }))}
                    target="_blank" rel="noreferrer" onClick={() => sent(i)} className="rounded-lg bg-[#15803d] px-2 py-1 text-xs font-bold text-white">💬 {t('remTable.send')}</a>
                  <button onClick={() => void consent(i, false)} className="text-xs text-muted underline">{t('reminders.stop')}</button>
                </span>) : i.hold === 'noConsent' ? <span className="flex justify-end"><button onClick={() => void consent(i, true)} className="text-xs font-semibold text-brand underline">{t('reminders.agreed')}</button></span> : null },
            ]}
            footer={[t('remTable.total', { count: ready.length + held.length }), '', formatRupees([...ready, ...held].reduce((n, i) => n + i.balance, 0)), '', '', '', '']} />
        </div>
      )}
      <div className="flex flex-col gap-3 lg:hidden">
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
    </div>
  )
}

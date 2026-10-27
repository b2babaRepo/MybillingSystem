import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { buildReminder, DEFAULT_REMINDER, useShopSettings, type ShopSettings } from '../lib/reports'

/** The shop's name and place, its UPI ID, and the reminder wording, with a live preview. */
export function SettingsScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const settings = useShopSettings(shopId)
  if (!settings.data) return <p className="p-4 text-muted">…</p>
  return <SettingsForm shopId={shopId} isOwner={isOwner} initial={settings.data} />
}

function SettingsForm({ shopId, isOwner, initial }: { shopId: string; isOwner: boolean; initial: ShopSettings }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [form, setForm] = useState({
    name: initial.name,
    area: initial.area ?? '',
    address: initial.address ?? '',
    upiId: initial.upiId ?? '',
    reminderTemplate: initial.reminderTemplate ?? '',
  })
  const [message, setMessage] = useState('')
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value })
  const preview = buildReminder(form.reminderTemplate, { name: 'Suresh', amount: 1320, shop: form.name, upi: form.upiId.trim() || null })

  async function save() {
    setMessage('')
    try {
      const saved = await api<ShopSettings>('/v1/shop', 'PUT', form)
      queryClient.setQueryData([shopId, 'settings'], saved)
      setMessage(t('settings.saved'))
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('error'))
    }
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      {!isOwner && <p className="rounded-xl bg-amber-50 p-3 text-amber-900">{t('settings.ownerOnly')}</p>}
      <fieldset disabled={!isOwner} className="flex flex-col gap-3 rounded-[18px] bg-white p-4">
        <Field label={t('settings.shopName')} value={form.name} onChange={set('name')} />
        <Field label={t('settings.area')} value={form.area} onChange={set('area')} />
        <Field label={t('settings.address')} value={form.address} onChange={set('address')} />
        <div>
          <Field label={t('settings.upi')} value={form.upiId} onChange={set('upiId')} autoCapitalize="none" inputMode="email" />
          <p className="mt-1 text-sm text-muted">{t('settings.upiHint')}</p>
        </div>
        <label className="block">
          <span className="mb-1 block text-sm font-semibold text-muted">{t('settings.template')}</span>
          <textarea value={form.reminderTemplate} onChange={set('reminderTemplate')} rows={4} maxLength={500} placeholder={DEFAULT_REMINDER}
            className="w-full rounded-[14px] border-[1.5px] border-line p-3 text-base" />
          <span className="mt-1 block text-sm text-muted">{t('settings.templateHint')}</span>
        </label>
        <div>
          <span className="mb-1 block text-sm font-semibold text-muted">{t('settings.preview')}</span>
          <p className="rounded-2xl rounded-br-sm bg-[#dcf8c6] p-3 text-[15px] whitespace-pre-line">{preview}</p>
        </div>
        {isOwner && <Button onClick={save} disabled={!form.name.trim()}>{t('save')}</Button>}
        {message && <p className="text-sm text-muted">{message}</p>}
      </fieldset>
    </div>
  )
}

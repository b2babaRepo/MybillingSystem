import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { navigate } from '../router'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { buildReminder, DEFAULT_REMINDER, SHOP_TYPES, useShopSettings, type ShopSettings, type ShopType, sendsToSites, SERVICE_TYPES } from '../lib/reports'

/** The shop's name and place, its UPI ID, and the reminder wording, with a live preview. */
export function SettingsScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const settings = useShopSettings(shopId)
  if (!settings.data) return <p className="p-4 text-muted">…</p>
  return <SettingsForm shopId={shopId} isOwner={isOwner} initial={settings.data} />
}

function SettingsForm({ shopId, isOwner, initial }: { shopId: string; isOwner: boolean; initial: ShopSettings }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [extra, setExtra] = useState<ShopType[]>(initial.extraTypes ?? [])
  const [form, setForm] = useState({
    name: initial.name,
    area: initial.area ?? '',
    address: initial.address ?? '',
    upiId: initial.upiId ?? '',
    reminderTemplate: initial.reminderTemplate ?? '',
    type: initial.type ?? 'kirana',
    gstin: initial.gstin ?? '',
    gstComposition: initial.gstComposition ? 'yes' : '',
    deliversGoods: initial.deliversGoods ?? sendsToSites(initial.type ?? 'kirana') ? 'yes' : '',
    staffMaxDiscountPercent: String(initial.staffMaxDiscountPercent ?? 10),
    staffMaxWriteOff: String(initial.staffMaxWriteOff ?? 1000),
    exchangeSlipDays: String(initial.exchangeSlipDays ?? 90),
  })
  const [message, setMessage] = useState('')
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value })
  const preview = buildReminder(form.reminderTemplate, { name: 'Suresh', amount: 1320, shop: form.name, upi: form.upiId.trim() || null })

  async function save() {
    setMessage('')
    try {
      const saved = await api<ShopSettings>('/v1/shop', 'PUT', { ...form, gstComposition: !!form.gstComposition, deliversGoods: !!form.deliversGoods, staffMaxDiscountPercent: Number(form.staffMaxDiscountPercent) || 0, staffMaxWriteOff: Number(form.staffMaxWriteOff) || 0, exchangeSlipDays: Number(form.exchangeSlipDays) || 90, extraTypes: extra.filter((x) => x !== form.type) })
      queryClient.setQueryData([shopId, 'settings'], saved)
      setMessage(t('settings.saved'))
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('error'))
    }
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      {!isOwner && <p className="rounded-xl bg-warn-soft p-3 text-warn-ink">{t('settings.ownerOnly')}</p>}
      {isOwner && (
        <button onClick={() => navigate('/settings/staff')} className="flex min-h-14 items-center gap-3 rounded-card bg-white px-4 text-left font-bold shadow-sm">
          <span aria-hidden className="text-2xl">👥</span>
          <span className="flex-1">{t('staff.title')}<span className="block text-sm font-normal text-muted">{t('staff.menuHint')}</span></span>
          <span aria-hidden>›</span>
        </button>
      )}
      <fieldset disabled={!isOwner} className="flex flex-col gap-3 rounded-card bg-white p-4 lg:grid lg:grid-cols-2 lg:items-start lg:gap-x-6 lg:gap-y-4 lg:p-6 lg:[&>button]:col-span-2 lg:[&>button]:justify-self-end lg:[&>button]:px-10 lg:[&>fieldset]:col-span-2 shadow-sm">
        <Field label={t('settings.shopName')} value={form.name} onChange={set('name')} />
        <label className="flex flex-col gap-1">
          <span className="text-sm font-medium text-muted">{t('shopType.label')}</span>
          <select value={form.type} onChange={set('type')} className="h-12 rounded-xl border border-line bg-white px-3 text-lg outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10">
            {SHOP_TYPES.map((type) => <option key={type} value={type}>{t(`shopType.${type}`)}</option>)}
          </select>
        </label>
        <fieldset className="flex flex-col gap-1">
          <legend className="text-sm font-medium text-muted">{t('shopType.extra')}</legend>
          <div className="grid grid-cols-2 gap-1 lg:grid-cols-4">
            {SHOP_TYPES.filter((type) => type !== form.type && type !== 'other').map((type) => (
              <label key={type} className="flex min-h-11 items-center gap-2 text-sm">
                <input type="checkbox" checked={extra.includes(type)} onChange={(e) => setExtra(e.target.checked ? [...extra, type] : extra.filter((x) => x !== type))} className="h-5 w-5" />
                {t(`shopType.${type}`)}
              </label>
            ))}
          </div>
        </fieldset>
        <Field label={t('settings.gstin')} value={form.gstin} onChange={set('gstin')} maxLength={15} autoCapitalize="characters" placeholder="08ABCDE1234F1Z0" />
        {form.gstin.trim() && (
          <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={!!form.gstComposition} onChange={(e) => setForm({ ...form, gstComposition: e.target.checked ? 'yes' : '' })} className="h-5 w-5" />
            <span>{t('settings.composition')}</span></label>
        )}
        <label className="block">
          <span className="mb-1 block text-sm font-semibold text-muted">{t('discount.limitLabel')}</span>
          <input value={form.staffMaxDiscountPercent} onChange={(e) => setForm({ ...form, staffMaxDiscountPercent: e.target.value })} inputMode="decimal" className="h-12 w-28 rounded-xl border border-line px-3 text-lg bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /> %
        </label>
        <label className="block">
          <span className="mb-1 block text-sm font-semibold text-muted">{t('sl.writeOff')}</span>
          ₹ <input value={form.staffMaxWriteOff} onChange={(e) => setForm({ ...form, staffMaxWriteOff: e.target.value })} inputMode="decimal" className="h-12 w-32 rounded-xl border border-line bg-white px-3 text-lg outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
          <span className="mt-1 block text-xs text-muted">{t('sl.writeOffHint')}</span>
        </label>
        <label className="block">
          <span className="mb-1 block text-sm font-semibold text-muted">{t('slip.daysLabel')}</span>
          <input value={form.exchangeSlipDays} onChange={(e) => setForm({ ...form, exchangeSlipDays: e.target.value })} inputMode="numeric" className="h-12 w-28 rounded-xl border border-line bg-white px-3 text-lg outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /> {t('slip.days')}
          <span className="mt-1 block text-xs text-muted">{t('slip.daysHint')}</span>
        </label>
        {!SERVICE_TYPES.includes(form.type as ShopType) && (
          <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={!!form.deliversGoods} onChange={(e) => setForm({ ...form, deliversGoods: e.target.checked ? 'yes' : '' })} className="h-5 w-5" />
            <span>{t('settings.delivers')}</span></label>
        )}
        <Field label={t('settings.area')} value={form.area} onChange={set('area')} />
        <Field label={t('settings.address')} value={form.address} onChange={set('address')} />
        <div>
          <Field label={t('settings.upi')} value={form.upiId} onChange={set('upiId')} autoCapitalize="none" inputMode="email" />
          <p className="mt-1 text-sm text-muted">{t('settings.upiHint')}</p>
        </div>
        <label className="block">
          <span className="mb-1 block text-sm font-semibold text-muted">{t('settings.template')}</span>
          <textarea value={form.reminderTemplate} onChange={set('reminderTemplate')} rows={4} maxLength={500} placeholder={DEFAULT_REMINDER}
            className="w-full rounded-[14px] border-[1.5px] border-line p-3 text-base bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
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

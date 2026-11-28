import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import type { StoreSettings } from '../lib/orderFlow'
import { btn } from '../lib/buttonStyles'
import { FESTIVE } from '../lib/festive'
import { shownError, shownDone } from '../lib/notify'

const input = 'h-12 w-full rounded-xl border border-line bg-white px-3 text-lg outline-none focus:border-brand-line'

/** The owner sets how online orders work: delivery charge, free delivery above, minimum order, and the time chips. */
export function StoreOrderSettings({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const query = useQuery({ queryKey: [shopId, 'store-settings'], queryFn: () => api<StoreSettings>('/v1/store-settings') })
  if (!query.data) return null
  return <Form key={JSON.stringify(query.data)} shopId={shopId} isOwner={isOwner} initial={query.data} title={t('cust2.storeSettings')} />
}

function Form({ shopId, isOwner, initial, title }: { shopId: string; isOwner: boolean; initial: StoreSettings; title: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const money = (v: number | null) => (v ? String(v) : '')
  const [form, setForm] = useState({ fee: money(initial.deliveryFee), free: money(initial.freeDeliveryAbove), min: money(initial.minOrder), slots: initial.slots.join('\n'), theme: initial.theme ?? '' })
  const [message, setMessage] = useState('')
  const num = (s: string) => (s.trim() ? Number(s) : null)
  async function save() {
    setMessage('')
    try {
      await api('/v1/store-settings', 'PUT', { deliveryFee: num(form.fee), freeDeliveryAbove: num(form.free), minOrder: num(form.min), slots: form.slots.split('\n').map((s) => s.trim()).filter(Boolean), theme: form.theme || null })
      await queryClient.invalidateQueries({ queryKey: [shopId, 'store-settings'] })
      setMessage(shownDone(t('cust2.saved')))
    } catch (e) {
      setMessage(shownError(e))
    }
  }
  const field = (key: 'fee' | 'free' | 'min', label: string) => (
    <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{label}</span>
      <input value={form[key]} disabled={!isOwner} onChange={(e) => setForm({ ...form, [key]: e.target.value.replace(/[^\d.]/g, '') })} inputMode="decimal" className={input} /></label>
  )
  return (
    <section className="flex flex-col gap-3 rounded-card bg-white p-4 shadow-sm" aria-label={title}>
      <h2 className="text-lg font-bold">🛵 {title}</h2>
      <p className="text-sm text-muted">{t('cust2.storeSettingsHint')}</p>
      <div className="grid gap-3 sm:grid-cols-3">
        {field('fee', t('cust2.deliveryFeeLabel'))}
        {field('free', t('cust2.freeAboveLabel'))}
        {field('min', t('cust2.minOrderLabel'))}
      </div>
      <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('cust2.slots')}</span>
        <textarea value={form.slots} disabled={!isOwner} onChange={(e) => setForm({ ...form, slots: e.target.value })} rows={3} placeholder={t('cust2.slotsHint')} className="w-full rounded-xl border border-line bg-white p-3 outline-none focus:border-brand-line" /></label>
      <div className="flex flex-col gap-2">
        <span className="text-sm font-semibold text-muted">{t('cust4.festiveLook')}</span>
        <div className="flex flex-wrap gap-2">
          <button type="button" disabled={!isOwner} onClick={() => setForm({ ...form, theme: '' })} aria-pressed={!form.theme} className={`min-h-11 rounded-full px-4 font-semibold ${!form.theme ? 'bg-brand text-white' : 'border border-line bg-white'}`}>{t('cust4.usualLook')}</button>
          {Object.entries(FESTIVE).map(([key, f]) => (
            <button type="button" key={key} disabled={!isOwner} onClick={() => setForm({ ...form, theme: key })} aria-pressed={form.theme === key}
              className={`min-h-11 rounded-full px-4 font-semibold ${form.theme === key ? `${f.header} text-white` : 'border border-line bg-white'}`}>{f.emoji} {t(`cust4.festiveName.${key}`)}</button>
          ))}
        </div>
      </div>
      {isOwner && <button onClick={() => void save()} className={`h-12 rounded-xl ${btn.solid}`}>{t('save')}</button>}
      {message && <p className="text-sm font-semibold text-brand-strong">{message}</p>}
    </section>
  )
}

/** On a customer's page: may they put app orders on their khata? (Up to the credit limit, if one is set.) */
export function OnlineKhataToggle({ customerId, isOwner }: { customerId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const key = ['online-khata', customerId]
  const allowed = useQuery({ queryKey: key, queryFn: () => api<{ allowed: boolean }>(`/v1/customers/${customerId}/online-khata`), retry: false }).data?.allowed
  if (allowed === undefined) return null
  async function flip() {
    await api(`/v1/customers/${customerId}/online-khata`, 'PUT', { allowed: !allowed })
    await queryClient.invalidateQueries({ queryKey: key })
  }
  return (
    <button onClick={() => void flip()} disabled={!isOwner} role="switch" aria-checked={allowed} className="flex min-h-14 w-full items-center gap-3 rounded-2xl border border-line p-3 text-left disabled:opacity-70">
      <span aria-hidden className="flex h-10 w-10 items-center justify-center rounded-xl bg-brand-soft text-lg">📱</span>
      <span className="flex flex-1 flex-col"><span className="font-semibold">{t('cust2.onlineKhata')}</span><span className="text-[13px] text-muted">{t('cust2.onlineKhataHint')}</span></span>
      <span className={`relative h-7 w-12 shrink-0 rounded-full transition ${allowed ? 'bg-brand' : 'bg-[#cfd8d6]'}`}><span className={`absolute top-1 h-5 w-5 rounded-full bg-white shadow transition-all ${allowed ? 'left-6' : 'left-1'}`} /></span>
    </button>
  )
}

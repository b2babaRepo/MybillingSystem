import { isTrade } from '../lib/reports'
import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { navigate } from '../router'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { buildReminder, DEFAULT_REMINDER, SHOP_TYPES, useShopSettings, type ShopSettings, type ShopType, sendsToSites, SERVICE_TYPES } from '../lib/reports'

/** The shop's name and place, its UPI ID, and the reminder wording, with a live preview. */
import { PrintLookPanel } from './PrintLookPanel'
import { PasscodesPanel } from './PasscodesPanel'
import { WhatsAppBusinessPanel } from './WhatsAppBusinessPanel'

import { StoreOrderSettings } from '../components/StoreOrderSettings'
import { ShopLocation } from '../components/ShopLocation'

export function SettingsScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const settings = useShopSettings(shopId)
  if (!settings.data) return <p className="p-4 text-muted">…</p>
  return (
    <>
      <SettingsForm shopId={shopId} isOwner={isOwner} initial={settings.data} />
      <div className="mx-auto max-w-xl px-4 pb-6 lg:max-w-4xl"><ShopLocation isOwner={isOwner} /><StoreOrderSettings shopId={shopId} isOwner={isOwner} /></div>
    </>
  )
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
    exchangeSlipHelperMax: String(initial.exchangeSlipHelperMax ?? 5000),
    cashReceiptLimit: String(initial.cashReceiptLimit ?? 200000),
    panFrom: String(initial.panFrom ?? 200000),
    cashPayoutLimit: String(initial.cashPayoutLimit ?? 10000),
    girviState: initial.girviState ?? '',
    girviLicence: initial.girviLicence ?? '',
    girviMaxYearlyRate: initial.girviMaxYearlyRate != null ? String(initial.girviMaxYearlyRate) : '',
    girviNoticeDays: String(initial.girviNoticeDays ?? 15),
    contractorDiscountPercent: String(initial.contractorDiscountPercent ?? 0),
    dealerDiscountPercent: String(initial.dealerDiscountPercent ?? 0),
    billPrefix: initial.billPrefix ?? '',
    billFields: [0, 1, 2, 3].map((i) => initial.billFields?.[i] ?? ''),
    scaleLabel: initial.scaleLabel ?? '',
  })
  const [message, setMessage] = useState('')
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value })
  const preview = buildReminder(form.reminderTemplate, { name: 'Suresh', amount: 1320, shop: form.name, upi: form.upiId.trim() || null })

  async function save() {
    setMessage('')
    try {
      const saved = await api<ShopSettings>('/v1/shop', 'PUT', { ...form, gstComposition: !!form.gstComposition, deliversGoods: !!form.deliversGoods, staffMaxDiscountPercent: Number(form.staffMaxDiscountPercent) || 0, staffMaxWriteOff: Number(form.staffMaxWriteOff) || 0, exchangeSlipDays: Number(form.exchangeSlipDays) || 90, exchangeSlipHelperMax: Number(form.exchangeSlipHelperMax) || 0, cashReceiptLimit: Number(form.cashReceiptLimit) || 200000, panFrom: Number(form.panFrom) || 200000, cashPayoutLimit: Number(form.cashPayoutLimit) || 0, girviState: form.girviState, girviLicence: form.girviLicence, girviMaxYearlyRate: Number(form.girviMaxYearlyRate) || 0, girviNoticeDays: Number(form.girviNoticeDays) || 0, contractorDiscountPercent: Number(form.contractorDiscountPercent) || 0, dealerDiscountPercent: Number(form.dealerDiscountPercent) || 0, scaleLabel: form.scaleLabel.trim(), billPrefix: form.billPrefix.trim(), billFields: form.billFields.map((f) => f.trim()).filter(Boolean), extraTypes: extra.filter((x) => x !== form.type) })
      queryClient.setQueryData([shopId, 'settings'], saved)
      setMessage(t('settings.saved'))
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('error'))
    }
  }

  const [tab, setTab] = useState<'shop' | 'bills' | 'staff' | 'reminder' | 'print' | 'security' | 'whatsapp'>('shop')
  const icon = (d: string, size = 18) => <svg aria-hidden="true" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d={d} /></svg>
  const tabs = [
    { key: 'shop', label: t('set3.shop') },
    { key: 'bills', label: t('set3.bills') },
    { key: 'staff', label: t('set3.staff') },
    { key: 'reminder', label: t('set3.reminder') },
    ...(isOwner ? [{ key: 'print', label: t('set3.print') }, { key: 'security', label: t('set3.security') }, { key: 'whatsapp', label: 'WhatsApp' }] : []),
  ] as { key: typeof tab; label: string }[]
  const formTab = tab === 'shop' || tab === 'bills' || tab === 'staff' || tab === 'reminder'
  const shortcuts = isOwner ? [
    { to: '/settings/staff', label: t('staff.title'), d: 'M9 11a4 4 0 100-8 4 4 0 000 8zM2 21v-1a7 7 0 0114 0v1M17 3a4 4 0 010 8M22 21v-1a7 7 0 00-4-6', tone: 'bg-[#e8effd] text-[#1d4ed8] hover:bg-[#dbe5fc]' },
    { to: '/backup', label: t('backup.title'), d: 'M7 18a5 5 0 01-.5-10 6 6 0 0111.5 1.5A4 4 0 0117 18zM12 10v6M9.5 13.5L12 16l2.5-2.5', tone: 'bg-[#e3f2ee] text-[#0b5e4f] hover:bg-[#d2ebe4]' },
    { to: '/plan', label: t('set3.plan'), d: 'M12 3l2.8 5.7 6.2.9-4.5 4.4 1 6.2L12 17.3 6.5 20.2l1-6.2L3 9.6l6.2-.9z', tone: 'bg-[#fdf1e3] text-[#a1460a] hover:bg-[#fbe6cc]' },
    { to: '/store', label: t('menu.store'), d: 'M4 9l1-5h14l1 5M4 9h16v11H4zM9 20v-6h6v6', tone: 'bg-[#f1eafd] text-[#6d28d9] hover:bg-[#e7dcfb]' },
  ] : []

  return (
    <div className="flex flex-col gap-3 p-4">
      {!isOwner && <p className="rounded-xl bg-warn-soft p-3 text-warn-ink">{t('settings.ownerOnly')}</p>}

      {/* The shop at a glance, then the jobs that live on their own pages. */}
      <section className="flex flex-col gap-4 rounded-card bg-white p-4 shadow-sm">
        <div className="flex items-center gap-3">
          <span aria-hidden className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-brand-soft text-brand">{icon('M4 9l1-5h14l1 5M4 9h16v11H4zM9 20v-6h6v6', 26)}</span>
          <span className="flex min-w-0 flex-1 flex-col">
            <span className="truncate text-2xl font-extrabold">{form.name || '…'}</span>
            <span className="flex flex-wrap gap-1.5 text-xs font-semibold">
              <span className="rounded-full bg-[#e3f2ee] px-2.5 py-0.5 text-[#0b5e4f]">{t(`shopType.${form.type}`)}</span>
              {form.gstin.trim() && <span className="rounded-full bg-[#e8effd] px-2.5 py-0.5 text-[#1d4ed8]">GSTIN {form.gstin.trim()}</span>}
              {form.upiId.trim() && <span className="rounded-full bg-[#f1eafd] px-2.5 py-0.5 text-[#6d28d9]">UPI {form.upiId.trim()}</span>}
            </span>
          </span>
        </div>
        {shortcuts.length > 0 && (
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            {shortcuts.map((x) => (
              <button key={x.to} onClick={() => navigate(x.to)} className={`flex min-h-[52px] items-center gap-2.5 rounded-2xl px-3 text-left text-sm font-bold transition active:scale-[0.98] ${x.tone}`}>
                <span aria-hidden className="flex h-[30px] w-[30px] shrink-0 items-center justify-center rounded-full bg-white">{icon(x.d)}</span>
                <span className="leading-tight">{x.label}</span>
              </button>
            ))}
          </div>
        )}
      </section>

      <div role="tablist" aria-label={t('settings.link')} className="-mx-4 flex gap-1 overflow-x-auto border-b border-line px-4 [scrollbar-width:none]">
        {tabs.map((x) => (
          <button key={x.key} role="tab" aria-selected={tab === x.key} onClick={() => setTab(x.key)}
            className={`min-h-12 shrink-0 border-b-[3px] px-3 text-sm whitespace-nowrap ${tab === x.key ? 'border-brand font-bold text-brand' : 'border-transparent font-semibold text-muted hover:text-ink'}`}>{x.label}</button>
        ))}
      </div>

      {formTab && (
        <fieldset disabled={!isOwner} className="flex flex-col gap-4 rounded-card bg-white p-4 shadow-sm lg:grid lg:grid-cols-2 lg:items-start lg:gap-x-6 lg:p-6 lg:[&>fieldset]:col-span-2">
          {tab === 'shop' && <>
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
          </>}
          {tab === 'bills' && <>
        <div className="flex flex-col gap-2 rounded-2xl border border-line p-3">
          <label className="flex flex-wrap items-center gap-2">
            <span className="text-sm font-semibold text-muted">{t('billSetup.prefix')}</span>
            <input value={form.billPrefix} onChange={(e) => setForm({ ...form, billPrefix: e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 3) })} maxLength={3} placeholder="MB"
              className="h-12 w-24 rounded-xl border border-line bg-white px-3 text-lg font-bold uppercase outline-none focus:border-brand-line" />
            <span className="text-sm text-muted">{t('billSetup.looksLike', { no: `${form.billPrefix ? `${form.billPrefix}/` : ''}2026-27/0001` })}</span>
          </label>
          <p className="text-xs text-muted">{t('billSetup.prefixHint')}</p>
          <span className="text-sm font-semibold text-muted">{t('billSetup.fields')}</span>
          <div className="grid grid-cols-2 gap-2">
            {form.billFields.map((f, i) => (
              <input key={i} value={f} maxLength={30} aria-label={`${t('billSetup.fields')} ${i + 1}`} placeholder={t(`billSetup.example${i}`)}
                onChange={(e) => setForm({ ...form, billFields: form.billFields.map((x, j) => (j === i ? e.target.value : x)) })}
                className="h-11 rounded-xl border border-line bg-white px-3 outline-none focus:border-brand-line" />
            ))}
          </div>
          <p className="text-xs text-muted">{t('billSetup.fieldsHint')}</p>
        </div>
        <div className="flex flex-col gap-2 rounded-2xl border border-line p-3">
          <span className="text-sm font-semibold text-muted">{t('billSetup.scale')}</span>
          <div className="flex flex-wrap gap-2">
            {([['', 'scaleOff'], ['21PPPPPWWWWWC', 'scaleWeight'], ['21PPPPPRRRRRC', 'scalePrice']] as const).map(([layout, key]) => (
              <button key={key} type="button" onClick={() => setForm({ ...form, scaleLabel: layout })} aria-pressed={form.scaleLabel === layout}
                className={`min-h-11 rounded-xl border-2 px-3 text-sm font-semibold ${form.scaleLabel === layout ? 'border-brand bg-brand-soft text-brand-strong' : 'border-line'}`}>{t(`billSetup.${key}`)}</button>
            ))}
          </div>
          {form.scaleLabel && <label className="flex flex-wrap items-center gap-2">
            <span className="text-sm text-muted">{t('billSetup.scaleOwn')}</span>
            <input value={form.scaleLabel} onChange={(e) => setForm({ ...form, scaleLabel: e.target.value.replace(/[^0-9xPWRC]/g, '').slice(0, 13) })} maxLength={13}
              className="h-11 w-48 rounded-xl border border-line bg-white px-3 font-mono outline-none focus:border-brand-line" />
          </label>}
          <p className="text-xs text-muted">{t('billSetup.scaleHint')}</p>
        </div>
        <label className="block">
          <span className="mb-1 block text-sm font-semibold text-muted">{t('slip.daysLabel')}</span>
          <input value={form.exchangeSlipDays} onChange={(e) => setForm({ ...form, exchangeSlipDays: e.target.value })} inputMode="numeric" className="h-12 w-28 rounded-xl border border-line bg-white px-3 text-lg outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /> {t('slip.days')}
          <span className="mt-1 block text-xs text-muted">{t('slip.daysHint')}</span>
        </label>
          </>}
          {tab === 'staff' && <>
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
          <span className="mb-1 block text-sm font-semibold text-muted">{t('slip.helperMax')}</span>
          ₹ <input value={form.exchangeSlipHelperMax} onChange={(e) => setForm({ ...form, exchangeSlipHelperMax: e.target.value })} inputMode="decimal" className="h-12 w-32 rounded-xl border border-line bg-white px-3 text-lg outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
          <span className="mt-1 block text-xs text-muted">{t('slip.helperMaxHint')}</span>
        </label>
        {(form.type === 'jewellery' || extra.includes('jewellery' as ShopType)) && (
          <fieldset className="flex flex-col gap-3 rounded-xl border border-line p-3">
            <legend className="px-1 text-sm font-bold">{t('jwl6.cashRules')}</legend>
            <p className="text-xs text-muted">{t('jwl6.cashRulesHint')}</p>
            <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('jwl6.cashReceiptLimit')}</span>
              ₹ <input value={form.cashReceiptLimit} onChange={(e) => setForm({ ...form, cashReceiptLimit: e.target.value })} inputMode="decimal" className="h-12 w-36 rounded-xl border border-line bg-white px-3 text-lg outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /></label>
            <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('jwl6.panFrom')}</span>
              ₹ <input value={form.panFrom} onChange={(e) => setForm({ ...form, panFrom: e.target.value })} inputMode="decimal" className="h-12 w-36 rounded-xl border border-line bg-white px-3 text-lg outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /></label>
            <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('jwl6.cashPayoutLimit')}</span>
              ₹ <input value={form.cashPayoutLimit} onChange={(e) => setForm({ ...form, cashPayoutLimit: e.target.value })} inputMode="decimal" className="h-12 w-36 rounded-xl border border-line bg-white px-3 text-lg outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /></label>
          </fieldset>
        )}
        {(form.type === 'jewellery' || extra.includes('jewellery' as ShopType)) && (
          <fieldset className="flex flex-col gap-3 rounded-xl border border-line p-3">
            <legend className="px-1 text-sm font-bold">{t('jwl7.title')}</legend>
            <p className="text-xs text-muted">{t('jwl7.hint')}</p>
            <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('jwl7.state')}</span>
              <select value={form.girviState} onChange={(e) => setForm({ ...form, girviState: e.target.value })} className="h-12 rounded-xl border border-line bg-white px-3">
                <option value="">{t('jwl7.noState')}</option>
                <option value="gujarat">Gujarat · Gujarat Money-Lenders Act, 2011</option>
              </select></label>
            <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('jwl7.licence')}</span>
              <input value={form.girviLicence} onChange={(e) => setForm({ ...form, girviLicence: e.target.value })} maxLength={60} className="h-12 w-full max-w-sm rounded-xl border border-line bg-white px-3" /></label>
            <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('jwl7.cap')}</span>
              <input value={form.girviMaxYearlyRate} onChange={(e) => setForm({ ...form, girviMaxYearlyRate: e.target.value })} inputMode="decimal" placeholder={t('jwl7.capNone')} className="h-12 w-36 rounded-xl border border-line bg-white px-3 text-lg outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /> %
              <span className="mt-1 block text-xs text-muted">{t('jwl7.capHint')}</span></label>
            <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('jwl7.notice')}</span>
              <input value={form.girviNoticeDays} onChange={(e) => setForm({ ...form, girviNoticeDays: e.target.value })} inputMode="numeric" className="h-12 w-36 rounded-xl border border-line bg-white px-3 text-lg outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /> {t('slip.days')}</label>
          </fieldset>
        )}
        {isTrade([form.type as ShopType, ...extra]) && (
          <fieldset className="flex flex-col gap-3 rounded-xl border border-line p-3">
            <legend className="px-1 text-sm font-bold">{t('hwp4.groupsTitle')}</legend>
            <p className="text-xs text-muted">{t('hwp4.groupsHint')}</p>
            {(['contractor', 'dealer'] as const).map((g) => (
              <label key={g} className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t(`hwp4.${g}Off`)}</span>
                <input value={form[`${g}DiscountPercent`]} onChange={(e) => setForm({ ...form, [`${g}DiscountPercent`]: e.target.value.replace(/[^\d.]/g, '') })} inputMode="decimal"
                  className="h-12 w-36 rounded-xl border border-line bg-white px-3 text-lg outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /> %</label>
            ))}
          </fieldset>
        )}
          </>}
          {tab === 'reminder' && <>
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
          </>}
          {isOwner && (
            <div className="flex flex-wrap items-center gap-3 border-t border-line pt-4 lg:col-span-2">
              <Button className="px-10" onClick={save} disabled={!form.name.trim()}>{t('save')}</Button>
              {message && <p role="status" className="text-sm font-semibold text-brand-deep">{message}</p>}
            </div>
          )}
        </fieldset>
      )}
      {tab === 'print' && isOwner && <PrintLookPanel />}
      {tab === 'security' && isOwner && <PasscodesPanel />}
      {tab === 'whatsapp' && isOwner && <WhatsAppBusinessPanel />}
    </div>
  )
}

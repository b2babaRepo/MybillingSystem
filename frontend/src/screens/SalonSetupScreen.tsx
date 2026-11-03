import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'
import type { Product } from '../lib/types'
import { navigate } from '../router'

interface StaffRow { membershipId: string; name: string; phone: string; role: string; isActive: boolean; hasPin: boolean }

/**
 * A new salon's first steps: its services (price and minutes each), then its stylists (each signs in with a PIN).
 * Done once at the start, and opened again from Home to change prices or add a service or stylist.
 */
export function SalonSetupScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const [step, setStep] = useState<'services' | 'stylists'>('services')
  const chip = (on: boolean) => `min-h-11 flex-1 rounded-full text-sm font-semibold ${on ? 'bg-teal-700 text-white' : 'border border-line bg-white'}`
  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <h2 className="text-lg font-bold">{t('salon.setupTitle')}</h2>
      <div className="flex gap-2 rounded-full bg-white p-1">
        <button onClick={() => setStep('services')} className={chip(step === 'services')}>1 · {t('salon.services')}</button>
        <button onClick={() => setStep('stylists')} className={chip(step === 'stylists')}>2 · {t('salon.stylists')}</button>
      </div>
      {step === 'services' ? <Services shopId={shopId} onNext={() => setStep('stylists')} /> : <Stylists shopId={shopId} />}
    </div>
  )
}

function Services({ shopId, onNext }: { shopId: string; onNext: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { products } = useProducts(shopId)
  const services = products.filter((p) => p.isService)
  const [edits, setEdits] = useState<Record<string, { price: string; minutes: string }>>({})
  const [d, setD] = useState({ name: '', price: '', minutes: '' })
  const [message, setMessage] = useState('')
  const refresh = () => queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })

  async function run(action: () => Promise<unknown>, done = '') {
    setMessage('')
    try {
      await action()
      setMessage(done)
      void refresh()
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  const save = (p: Product) => {
    const e = edits[p.id]
    return api(`/v1/products/${p.id}`, 'PUT', {
      name: p.name, unit: p.unit, sellingPrice: Number(e?.price ?? p.sellingPrice) || 0, costPrice: p.costPrice, category: p.category, reorderLevel: null,
      gstRate: p.gstRate ?? null, hsnCode: p.hsnCode ?? null, isService: true, durationMinutes: Number(e?.minutes ?? p.durationMinutes) || null,
    })
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm text-muted">{t('salon.servicesHint')}</p>
      {services.length === 0 && (
        <Button onClick={() => void run(() => api('/v1/products/starter', 'POST', {}), t('salon.added'))}>{t('salon.addCommon')}</Button>
      )}
      <ul className="flex flex-col gap-1 rounded-[18px] bg-white p-2">
        {services.map((p) => (
          <li key={p.id} className="grid grid-cols-[1fr_5.5rem_4.5rem_auto] items-center gap-2 px-1 py-1">
            <span className="min-w-0 truncate font-semibold">{p.name}</span>
            <input value={edits[p.id]?.price ?? (p.sellingPrice || '')} onChange={(e) => setEdits({ ...edits, [p.id]: { price: e.target.value, minutes: edits[p.id]?.minutes ?? String(p.durationMinutes ?? '') } })}
              inputMode="decimal" placeholder="₹" aria-label={t('salon.price', { name: p.name })} className="h-11 rounded-xl border border-line px-2" />
            <input value={edits[p.id]?.minutes ?? (p.durationMinutes ?? '')} onChange={(e) => setEdits({ ...edits, [p.id]: { minutes: e.target.value, price: edits[p.id]?.price ?? String(p.sellingPrice || '') } })}
              inputMode="numeric" placeholder={t('salon.min')} aria-label={t('salon.minutes', { name: p.name })} className="h-11 rounded-xl border border-line px-2" />
            <button disabled={!edits[p.id]} onClick={() => void run(() => save(p), t('saved'))} className="min-h-11 px-2 text-sm font-semibold text-teal-700 disabled:opacity-40">{t('save')}</button>
          </li>
        ))}
      </ul>
      {services.some((p) => !(p.sellingPrice > 0)) && <p className="text-sm font-semibold text-udhaar">{t('salon.setPrices')}</p>}
      <div className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
        <h3 className="font-bold">{t('salon.addService')}</h3>
        <div className="grid grid-cols-[2fr_1fr_1fr] gap-2">
          <Field label={t('salon.serviceName')} value={d.name} onChange={(e) => setD({ ...d, name: e.target.value })} placeholder={t('salon.serviceHint')} />
          <Field label={t('salon.priceLabel')} value={d.price} onChange={(e) => setD({ ...d, price: e.target.value })} inputMode="decimal" />
          <Field label={t('salon.min')} value={d.minutes} onChange={(e) => setD({ ...d, minutes: e.target.value })} inputMode="numeric" />
        </div>
        <Button disabled={d.name.trim().length < 2 || !(Number(d.price) > 0)} onClick={() => void run(async () => {
          await api('/v1/products', 'POST', { id: crypto.randomUUID(), name: d.name.trim(), unit: 'piece', sellingPrice: Number(d.price), costPrice: null, category: 'Services', gstRate: 5, hsnCode: '999721', isService: true, durationMinutes: Number(d.minutes) || null })
          setD({ name: '', price: '', minutes: '' })
        }, t('saved'))}>{t('salon.addService')}</Button>
      </div>
      {message && <p className="text-sm text-muted">{message}</p>}
      <Button onClick={onNext}>{t('salon.next')}</Button>
    </div>
  )
}

function Stylists({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const staff = useQuery({ queryKey: [shopId, 'staff'], queryFn: () => api<StaffRow[]>('/v1/shop/staff') }).data ?? []
  const [d, setD] = useState({ name: '', phone: '', pin: '' })
  const [message, setMessage] = useState('')
  async function add() {
    setMessage('')
    try {
      await api('/v1/shop/staff', 'POST', { name: d.name.trim(), phone: d.phone.trim(), role: 'staff', pin: d.pin })
      setD({ name: '', phone: '', pin: '' })
      setMessage(t('saved'))
      void queryClient.invalidateQueries({ queryKey: [shopId, 'staff'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm text-muted">{t('salon.stylistsHint')}</p>
      <ul className="flex flex-col gap-1 rounded-[18px] bg-white p-2">
        {staff.filter((s) => s.isActive && s.role === 'staff').map((s) => <li key={s.membershipId} className="flex min-h-11 items-center justify-between px-2"><span className="font-semibold">{s.name}</span><span className="text-xs text-muted">{s.hasPin ? t('salon.hasPin') : ''}</span></li>)}
        {staff.filter((s) => s.role === 'staff').length === 0 && <li className="p-2 text-center text-muted">{t('salon.noStylists')}</li>}
      </ul>
      <div className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
        <h3 className="font-bold">{t('salon.addStylist')}</h3>
        <div className="grid grid-cols-2 gap-2">
          <Field label={t('salon.stylistName')} value={d.name} onChange={(e) => setD({ ...d, name: e.target.value })} />
          <Field label={t('tables.phone')} value={d.phone} onChange={(e) => setD({ ...d, phone: e.target.value })} inputMode="tel" />
        </div>
        <Field label={t('salon.pin')} value={d.pin} onChange={(e) => setD({ ...d, pin: e.target.value.replace(/\D/g, '').slice(0, 6) })} inputMode="numeric" />
        <Button disabled={d.name.trim().length < 2 || d.phone.replace(/\D/g, '').length < 10 || d.pin.length < 4} onClick={() => void add()}>{t('salon.addStylist')}</Button>
        {message && <p className="text-sm text-muted">{message}</p>}
      </div>
      <CommissionRates shopId={shopId} />
      <Button onClick={() => navigate('/')}>{t('salon.done')}</Button>
    </div>
  )
}

/** On a salon's Home: start a walk-in bill, change services and prices, and how many services are set up. */
export function SalonHomeCard({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const { products } = useProducts(shopId)
  const services = products.filter((p) => p.isService)
  const unpriced = services.filter((p) => !(p.sellingPrice > 0)).length
  const row = 'flex min-h-12 items-center justify-between rounded-[18px] bg-white px-4 text-left font-semibold'
  return (
    <section className="mx-4 mt-3 flex flex-col gap-2">
      {isOwner && (services.length === 0 || unpriced > 0) && (
        <button onClick={() => navigate('/salon/setup')} className="flex min-h-12 items-center justify-between rounded-[18px] bg-teal-700 px-4 text-left font-semibold text-white">
          <span>{services.length === 0 ? t('salon.startSetup') : t('salon.pricesMissing', { count: unpriced })}</span><span aria-hidden="true">→</span>
        </button>
      )}
      <button onClick={() => navigate('/appointments')} className="flex min-h-14 items-center justify-between rounded-[18px] bg-white px-4 text-left text-lg font-bold">
        <span>{t('appointments.link')}</span><span aria-hidden="true">→</span>
      </button>
      <div className="grid grid-cols-2 gap-2">
        <button onClick={() => navigate('/bill')} className={row}><span>{t('salon.walkIn')}</span><span aria-hidden="true">→</span></button>
        {isOwner && <button onClick={() => navigate('/salon/setup')} className={row}><span>{t('salon.servicesShort', { count: services.length })}</span><span aria-hidden="true">→</span></button>}
      </div>
      {isOwner && <button onClick={() => navigate('/stylists')} className={row}><span>{t('salon.earningsLink')}</span><span aria-hidden="true">→</span></button>}
      {services.length > 0 && <p className="px-1 text-xs text-muted">{services.slice(0, 4).map((s) => `${s.name} ${s.sellingPrice > 0 ? formatRupees(s.sellingPrice) : ''}`).join(' · ')}</p>}
    </section>
  )
}

interface Earnings { month: string; stylists: { membershipId: string; name: string; percent: number; services: number; sales: number; commission: number; tips: number; owed: number }[] }

/** Each stylist's commission percent, set by the owner. */
export function CommissionRates({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const data = useQuery({ queryKey: [shopId, 'stylist-earnings', ''], queryFn: () => api<Earnings>('/v1/stylists/earnings') }).data
  const [rates, setRates] = useState<Record<string, string>>({})
  const [message, setMessage] = useState('')
  const save = (id: string) => api(`/v1/stylists/${id}/commission`, 'PUT', { percent: Number(rates[id]) || 0 })
    .then(() => { setMessage(t('saved')); void queryClient.invalidateQueries({ queryKey: [shopId, 'stylist-earnings'] }) })
    .catch((e: unknown) => setMessage(e instanceof Error ? e.message : t('error')))
  return (
    <div className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
      <h3 className="font-bold">{t('salon.commissionTitle')}</h3>
      <p className="text-xs text-muted">{t('salon.commissionHint')}</p>
      {data?.stylists.map((s) => (
        <div key={s.membershipId} className="grid grid-cols-[1fr_5rem_auto] items-center gap-2">
          <span className="font-semibold">{s.name}</span>
          <input value={rates[s.membershipId] ?? String(s.percent)} onChange={(e) => setRates({ ...rates, [s.membershipId]: e.target.value })} inputMode="decimal" aria-label={t('salon.percentFor', { name: s.name })} className="h-11 rounded-xl border border-line px-2" />
          <button disabled={rates[s.membershipId] === undefined} onClick={() => void save(s.membershipId)} className="min-h-11 px-2 text-sm font-semibold text-teal-700 disabled:opacity-40">{t('save')}</button>
        </div>
      ))}
      {message && <p className="text-sm text-muted">{message}</p>}
    </div>
  )
}

/** A month of each stylist's work: services, sales, commission and tips, what is owed, and paying it out. */
export function StylistEarningsScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const now = new Date()
  const [month, setMonth] = useState(`${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`)
  const data = useQuery({ queryKey: [shopId, 'stylist-earnings', month], queryFn: () => api<Earnings>(`/v1/stylists/earnings?month=${month}`) }).data
  const [message, setMessage] = useState('')
  const shift = (n: number) => { const [y, m] = month.split('-').map(Number); const d = new Date(y, m - 1 + n, 1); setMonth(`${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`) }
  async function pay(id: string, name: string, owed: number) {
    if (!confirm(t('salon.payConfirm', { name, amount: formatRupees(owed) }))) return
    try {
      await api(`/v1/stylists/${id}/payout`, 'POST', { mode: 'cash' })
      setMessage(t('salon.paid', { name }))
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <div className="flex items-center justify-between rounded-[18px] bg-white p-2">
        <button onClick={() => shift(-1)} aria-label={t('bookings.prev')} className="h-11 w-11 text-xl">‹</button>
        <span className="font-bold">{new Date(`${month}-01T00:00`).toLocaleDateString('en-IN', { month: 'long', year: 'numeric' })}</span>
        <button onClick={() => shift(1)} aria-label={t('bookings.next')} className="h-11 w-11 text-xl">›</button>
      </div>
      {message && <p className="text-sm text-muted">{message}</p>}
      {data?.stylists.map((s) => (
        <section key={s.membershipId} className="flex flex-col gap-1 rounded-[18px] bg-white p-4 text-sm">
          <h3 className="text-base font-bold">{s.name} <span className="font-normal text-muted">· {s.percent}%</span></h3>
          <div className="flex justify-between"><span>{t('salon.servicesDone', { count: s.services })}</span><span>{formatRupees(s.sales)}</span></div>
          <div className="flex justify-between"><span>{t('salon.commission')}</span><span>{formatRupees(s.commission)}</span></div>
          <div className="flex justify-between"><span>{t('salon.tips')}</span><span>{formatRupees(s.tips)}</span></div>
          <div className="flex justify-between font-bold"><span>{t('salon.owed')}</span><span>{formatRupees(s.owed)}</span></div>
          {s.owed > 0 && <Button className="mt-2" onClick={() => void pay(s.membershipId, s.name, s.owed)}>{t('salon.payOut', { amount: formatRupees(s.owed) })}</Button>}
        </section>
      ))}
      <CommissionRates shopId={shopId} />
      <p className="text-xs text-muted">{t('salon.earningsHint')}</p>
    </div>
  )
}

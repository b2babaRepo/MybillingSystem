import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { useCustomers, useProducts } from '../lib/data'
import { formatRupees, whatsAppLink } from '../lib/format'
import { confirmAsk } from '../components/Ask'

interface Plan { id: string; name: string; kind: 'package' | 'membership'; price: number; serviceName: string | null; sessions: number | null; discountPercent: number | null; validDays: number }
interface CustomerPlan {
  id: string; customerName: string; customerPhone: string | null; name: string; kind: 'package' | 'membership'; sessionsTotal: number; sessionsLeft: number
  discountPercent: number | null; expiresOn: string; state: 'active' | 'used' | 'expired' | 'closed'
}

const day = (iso: string) => new Date(`${iso}T00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric' })

/** Packages and memberships: sell one to a customer, see who is running out, and (owner) set what the salon offers. */
export function PackagesScreen({ shopId, shopName, isOwner }: { shopId: string; shopName: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const plans = useQuery({ queryKey: [shopId, 'prepaid-plans'], queryFn: () => api<Plan[]>('/v1/prepaid-plans') }).data ?? []
  const expiring = useQuery({ queryKey: [shopId, 'customer-plans', 'expiring'], queryFn: () => api<CustomerPlan[]>('/v1/customer-plans?expiringDays=30') }).data ?? []
  const { customers } = useCustomers(shopId)
  const [sale, setSale] = useState({ customerId: '', planId: '', mode: 'upi' })
  const [message, setMessage] = useState('')

  async function run(path: string, body: unknown, done: string) {
    setMessage('')
    try {
      await api(path, 'POST', body)
      setMessage(done)
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  const select = 'h-12 w-full rounded-xl border border-line bg-white px-3'
  const describe = (p: Plan) => (p.kind === 'package' ? t('packages.packageLine', { count: p.sessions ?? 0, service: p.serviceName ?? '', days: p.validDays }) : t('packages.membershipLine', { percent: p.discountPercent, days: p.validDays }))
  const reminder = (p: CustomerPlan) => t('packages.reminder', { name: p.customerName, shop: shopName, plan: p.name, date: day(p.expiresOn), left: p.kind === 'package' ? t('packages.sessionsLeft', { count: p.sessionsLeft }) : '' })

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <p className="text-sm text-muted">{t('packages.hint')}</p>
      {message && <p className="rounded-xl bg-brand-soft p-3 text-sm font-semibold text-brand-deep">{message}</p>}

      <section className="flex flex-col gap-2 rounded-card bg-white p-3">
        <h3 className="font-bold">{t('packages.sell')}</h3>
        <select value={sale.customerId} onChange={(e) => setSale({ ...sale, customerId: e.target.value })} aria-label={t('jobs.customer')} className={select}>
          <option value="">{t('packages.chooseCustomer')}</option>{customers.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
        <select value={sale.planId} onChange={(e) => setSale({ ...sale, planId: e.target.value })} aria-label={t('packages.plan')} className={select}>
          <option value="">{t('packages.choosePlan')}</option>{plans.map((p) => <option key={p.id} value={p.id}>{p.name} · {formatRupees(p.price)}</option>)}
        </select>
        <select value={sale.mode} onChange={(e) => setSale({ ...sale, mode: e.target.value })} aria-label={t('jobs.mode')} className={select}><option value="upi">UPI</option><option value="cash">{t('cash')}</option></select>
        <Button disabled={!sale.customerId || !sale.planId} onClick={() => void run('/v1/customer-plans', { id: crypto.randomUUID(), customerId: sale.customerId, planId: sale.planId, mode: sale.mode }, t('packages.sold'))}>{t('packages.sellButton')}</Button>
        <p className="text-xs text-muted">{t('packages.sellHint')}</p>
      </section>

      <section className="flex flex-col gap-2 rounded-card bg-white p-3">
        <h3 className="font-bold">{t('packages.expiring', { count: expiring.length })}</h3>
        {expiring.length === 0 && <p className="text-sm text-muted">{t('packages.noneExpiring')}</p>}
        {expiring.map((p) => (
          <div key={p.id} className={`rounded-xl p-2 ${p.state === 'expired' ? 'bg-due-soft' : 'bg-warn-soft'}`}>
            <p className="font-semibold">{p.customerName} · {p.name}</p>
            <p className="text-xs">{p.kind === 'package' ? t('packages.sessionsLeft', { count: p.sessionsLeft }) + ' · ' : ''}{t(p.state === 'expired' ? 'packages.expiredOn' : 'packages.endsOn', { date: day(p.expiresOn) })}</p>
            <div className="mt-1 flex gap-2">
              {p.customerPhone && p.state !== 'expired' && <a href={whatsAppLink(p.customerPhone, reminder(p))} target="_blank" rel="noreferrer" className="flex h-10 items-center rounded-xl bg-[#15803d] px-3 text-sm font-semibold text-white">{t('tables.remind')}</a>}
              {isOwner && p.state === 'expired' && p.kind === 'package' && <Button tone="plain" className="h-10 px-3 text-sm" onClick={async () => { if ((await confirmAsk(t('packages.closeConfirm', { count: p.sessionsLeft })))) void run(`/v1/customer-plans/${p.id}/close`, {}, t('packages.closed')) }}>{t('packages.close')}</Button>}
            </div>
          </div>
        ))}
      </section>

      <section className="flex flex-col gap-2 rounded-card bg-white p-3">
        <h3 className="font-bold">{t('packages.offer')}</h3>
        {plans.length === 0 && <p className="text-sm text-muted">{t('packages.noPlans')}</p>}
        {plans.map((p) => <p key={p.id} className="text-sm"><span className="font-semibold">{p.name}</span> · {formatRupees(p.price)} · {describe(p)}</p>)}
        {isOwner && <NewPlan shopId={shopId} onDone={(m) => setMessage(m)} />}
      </section>
    </div>
  )
}

function NewPlan({ shopId, onDone }: { shopId: string; onDone: (message: string) => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const services = useProducts(shopId).products.filter((p) => p.isService)
  const [d, setD] = useState({ kind: 'package', name: '', price: '', serviceId: '', sessions: '6', discount: '10', days: '180' })
  const [error, setError] = useState('')
  const set = (key: keyof typeof d) => (e: { target: { value: string } }) => setD({ ...d, [key]: e.target.value })
  async function save() {
    setError('')
    try {
      await api('/v1/prepaid-plans', 'POST', {
        id: crypto.randomUUID(), name: d.name.trim(), kind: d.kind, price: Number(d.price) || 0, validDays: Number(d.days) || 0,
        serviceId: d.kind === 'package' ? d.serviceId || null : null, sessions: d.kind === 'package' ? Number(d.sessions) || null : null, discountPercent: d.kind === 'membership' ? Number(d.discount) || null : null,
      })
      setD({ ...d, name: '', price: '' })
      onDone(t('saved'))
      void queryClient.invalidateQueries({ queryKey: [shopId, 'prepaid-plans'] })
    } catch (e) {
      setError(e instanceof Error ? e.message : t('error'))
    }
  }
  const select = 'h-12 w-full rounded-xl border border-line bg-white px-3'
  return (
    <details className="rounded-xl bg-ground p-2">
      <summary className="min-h-11 cursor-pointer font-semibold">{t('packages.add')}</summary>
      <div className="mt-2 flex flex-col gap-2">
        <select value={d.kind} onChange={set('kind')} aria-label={t('packages.kind')} className={select}><option value="package">{t('packages.kindPackage')}</option><option value="membership">{t('packages.kindMembership')}</option></select>
        <div className="grid grid-cols-2 gap-2">
          <Field label={t('packages.name')} value={d.name} onChange={set('name')} placeholder={d.kind === 'package' ? t('packages.packageHint') : t('packages.membershipHint')} />
          <Field label={t('packages.price')} value={d.price} onChange={set('price')} inputMode="decimal" />
        </div>
        {d.kind === 'package' ? (
          <div className="grid grid-cols-2 gap-2">
            <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('packages.service')}</span>
              <select value={d.serviceId} onChange={set('serviceId')} className={select}><option value="">—</option>{services.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}</select>
            </label>
            <Field label={t('packages.sessions')} value={d.sessions} onChange={set('sessions')} inputMode="numeric" />
          </div>
        ) : <Field label={t('packages.discount')} value={d.discount} onChange={set('discount')} inputMode="decimal" />}
        <Field label={t('packages.days')} value={d.days} onChange={set('days')} inputMode="numeric" />
        {error && <p className="text-sm text-due">{error}</p>}
        <Button disabled={d.name.trim().length < 2 || !(Number(d.price) > 0)} onClick={() => void save()}>{t('save')}</Button>
      </div>
    </details>
  )
}

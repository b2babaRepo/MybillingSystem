import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BackIcon } from '../components/Icons'
import { Button } from '../components/ui'
import { api, request } from '../lib/api'
import { formatDateTime, formatPhone } from '../lib/format'
import { useSession } from '../lib/data'
import { ActivityPanel, AgentPanel, BillingPanel, PlansPanel, ShopBillingPanel } from './PlatformBilling'
import { usePlans } from '../lib/billing'
import { AgentsPanel, PerformancePanel, RegistrationsPanel } from './PlatformAgents'
import { addText } from '../i18n'
import { adminText } from '../i18n/admin'

addText(adminText)
import type { AdminRole } from '../lib/types'
import { navigate } from '../router'

interface Overview { shops: number; active: number; blocked: number; awaitingApproval: number; newThisWeek: number; households: number }
interface ShopRow {
  id: string
  name: string
  area: string | null
  slug: string
  createdAt: string
  approvedAt: string | null
  isActive: boolean
  blockedReason: string | null
  ownerName: string | null
  ownerPhone: string | null
  planCode?: string | null
  planStatus?: string | null
  planEndsAt?: string | null
  /** "GKS-000042". */
  shopCode?: string | null
}
interface Person { userId: string; membershipId: string; name: string; phone: string; role: 'owner' | 'staff'; isActive: boolean; lockedOut: boolean }
interface Detail { shop: ShopRow; address: string | null; adminNote: string | null; people: Person[]; customers: number; billsLast30Days: number; lastBillAt: string | null; lastBalanceCheck: { mismatches: number } | null }

type Status = 'new' | 'active' | 'blocked' | 'all'

/**
 * The platform admin's view of every shop: counts at the top, then new / active / blocked shops with search. Open a
 * shop to see its people (call or WhatsApp them, unlock a lockout), its activity, a private support note, and to
 * approve, block (with a reason) or unblock it.
 */
export function PlatformScreen() {
  const { t } = useTranslation()
  // What this admin may do; the server checks the same on every request (a session from before roles counts as super).
  const role: AdminRole = useSession()?.user.platformRole ?? 'super'
  const [status, setStatus] = useState<Status>('new')
  const [q, setQ] = useState('')
  const [plan, setPlan] = useState('')
  const plans = usePlans().data ?? []
  const [openId, setOpenId] = useState<string | null>(null)
  const overview = useQuery({ queryKey: ['platform', 'overview'], queryFn: () => api<Overview>('/v1/platform/overview') })
  const shops = useQuery({
    queryKey: ['platform', 'shops', status, q, plan],
    queryFn: () => api<ShopRow[]>(`/v1/platform/shops?status=${status}&q=${encodeURIComponent(q.trim())}&plan=${plan}`),
  })
  const tiles: [string, number | undefined][] = [
    [t('platform.awaiting'), overview.data?.awaitingApproval],
    [t('platform.active'), overview.data?.active],
    [t('platform.blocked'), overview.data?.blocked],
    [t('platform.newWeek'), overview.data?.newThisWeek],
    [t('platform.households'), overview.data?.households],
  ]

  return (
    <div className="mx-auto min-h-dvh max-w-3xl pb-10">
      <header className="sticky top-0 z-10 flex items-center gap-2 bg-slate-800 px-4 py-3 text-white">
        <button onClick={() => navigate('/')} className="-ml-2 flex h-11 w-11 items-center justify-center" aria-label={t('a11y.back')}><BackIcon /></button>
        <h1 className="text-xl font-bold">{t('platform.title')}</h1>
      </header>

      <div className="grid grid-cols-3 gap-2 p-4 sm:grid-cols-5">
        {tiles.map(([label, value]) => (
          <div key={label} className="rounded-2xl bg-white p-3">
            <div className="text-2xl font-extrabold tabular-nums">{value ?? '–'}</div>
            <div className="text-xs text-muted">{label}</div>
          </div>
        ))}
      </div>

      <div className="flex flex-col gap-3 px-4 pb-3">
        {role !== 'support' && <NewShop />}
        <AgentPanel role={role} />
        {role !== 'support' && <RegistrationsPanel role={role} />}
        {(role === 'super' || role === 'agent_manager') && <AgentsPanel />}
        {(role === 'super' || role === 'agent_manager' || role === 'billing') && <PerformancePanel role={role} />}
        {(role === 'super' || role === 'billing') && <PlansPanel />}
        {(role === 'super' || role === 'billing') && <BillingPanel />}
        {role === 'super' && <ManageAdmins />}
        {role === 'super' && <ActivityPanel />}
      </div>

      <div className="flex flex-col gap-3 px-4">
        <div role="tablist" className="grid grid-cols-4 gap-1 rounded-2xl bg-white p-1 text-sm font-semibold">
          {(['new', 'active', 'blocked', 'all'] as const).map((s) => (
            <button key={s} role="tab" aria-selected={status === s} onClick={() => setStatus(s)}
              className={`min-h-11 rounded-xl ${status === s ? 'bg-teal-700 text-white' : ''}`}>{t(`platform.tab.${s}`)}</button>
          ))}
        </div>
        <label className="sr-only" htmlFor="platform-search">{t('platform.search')}</label>
        <input id="platform-search" value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('platform.searchWithId')}
          className="min-h-12 rounded-2xl border border-line bg-white px-4" />
        <label className="sr-only" htmlFor="platform-plan">{t('plans.filter')}</label>
        <select id="platform-plan" value={plan} onChange={(e) => setPlan(e.target.value)} className="min-h-12 rounded-2xl border border-line bg-white px-4">
          <option value="">{t('plans.allPlans')}</option>
          <option value="paid">{t('billing.paidShops')}</option>
          <option value="trial">{t('plan2.status.trial')}</option>
          {plans.map((p) => <option key={p.code} value={p.code}>{p.name}</option>)}
        </select>

        {shops.data?.length === 0 && <p className="p-6 text-center text-muted">{t('platform.none')}</p>}
        <ul className="flex flex-col gap-2">
          {shops.data?.map((s) => (
            <li key={s.id} className="rounded-2xl bg-white">
              <button onClick={() => setOpenId(openId === s.id ? null : s.id)} aria-expanded={openId === s.id} className="flex w-full items-start gap-3 p-4 text-left">
                <span className="flex min-w-0 flex-1 flex-col">
                  <span className="truncate text-[17px] font-bold">{s.name}{s.shopCode && <span className="ml-2 font-mono text-xs font-semibold text-muted">{s.shopCode}</span>}</span>
                  <span className="truncate text-sm text-muted">
                    {[s.area, s.ownerName, formatPhone(s.ownerPhone)].filter(Boolean).join(' · ')}
                  </span>
                  <span className="text-xs text-muted">
                    {t('platform.opened', { date: formatDateTime(s.createdAt) })}
                    {s.planCode && ` · ${s.planStatus === 'trial' ? t('plan2.status.trial') : `${plans.find((p) => p.code === s.planCode)?.name ?? s.planCode}${s.planStatus === 'active' || s.planStatus === 'grace' ? ` ${t('billing.paidTag')}` : ''}`}`}
                    {s.planEndsAt && ` · ${t('billing.until', { date: new Date(s.planEndsAt).toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' }) })}`}
                  </span>
                </span>
                <StatusChip shop={s} />
              </button>
              {openId === s.id && <ShopDetail id={s.id} role={role} />}
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}

function StatusChip({ shop }: { shop: ShopRow }) {
  const { t } = useTranslation()
  const [label, style] = !shop.isActive
    ? [t('platform.chip.blocked'), 'bg-red-50 text-red-700']
    : !shop.approvedAt
      ? [t('platform.chip.new'), 'bg-amber-100 text-amber-900']
      : [t('platform.chip.active'), 'bg-teal-50 text-teal-800']
  return <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-bold ${style}`}>{label}</span>
}

function ShopDetail({ id, role }: { id: string; role: AdminRole }) {
  const canApprove = role === 'super' || role === 'approver'
  const canSupport = role === 'super' || role === 'support'
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const detail = useQuery({ queryKey: ['platform', 'shop', id], queryFn: () => api<Detail>(`/v1/platform/shops/${id}`) })
  const [note, setNote] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const d = detail.data
  if (!d) return <p className="px-4 pb-4 text-muted">{t('platform.loading')}</p>

  async function run(path: string, body: object = {}, method: 'POST' | 'PUT' = 'POST', done?: string) {
    setBusy(true)
    setMessage(null)
    try {
      const response = await request(path, method, body)
      if (!response.ok) throw new Error(((await response.json()) as { errors?: Record<string, string[]>; title?: string }).errors?.Reason?.[0] ?? t('error'))
      await queryClient.invalidateQueries({ queryKey: ['platform'] })
      if (done) setMessage(done)
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  function block() {
    const reason = prompt(t('platform.blockAsk', { name: d!.shop.name }))?.trim()
    if (reason) void run(`/v1/platform/shops/${id}/block`, { reason })
  }

  const stats: [string, string | number][] = [
    [t('platform.customers'), d.customers],
    [t('platform.bills30'), d.billsLast30Days],
    [t('platform.lastBill'), d.lastBillAt ? formatDateTime(d.lastBillAt) : '–'],
    [t('platform.khataCheck'), d.lastBalanceCheck ? (d.lastBalanceCheck.mismatches === 0 ? t('platform.checkOk') : t('platform.checkBad', { count: d.lastBalanceCheck.mismatches })) : '–'],
  ]

  return (
    <div className="flex flex-col gap-3 border-t border-line p-4">
      {d.shop.blockedReason && <p className="rounded-xl bg-red-50 p-3 text-sm text-red-800">{t('platform.blockedBecause', { reason: d.shop.blockedReason })}</p>}
      {message && <p className="rounded-xl bg-teal-50 p-3 text-sm font-semibold text-teal-900">{message}</p>}
      <dl className="grid grid-cols-2 gap-2 text-sm">
        {stats.map(([label, value]) => (
          <div key={label} className="rounded-xl bg-ground p-2"><dt className="text-xs text-muted">{label}</dt><dd className="font-bold">{value}</dd></div>
        ))}
      </dl>
      {d.address && <p className="text-sm text-muted">{d.address}</p>}

      <h3 className="font-bold">{t('platform.people')}</h3>
      <ul className="flex flex-col gap-2">
        {d.people.map((p) => (
          <li key={p.membershipId} className="flex flex-wrap items-center gap-2 text-sm">
            <span className="min-w-0 flex-1"><b>{p.name}</b> · {p.role === 'owner' ? t('staff.owner') : t('staff.helper')}{!p.isActive && ` · ${t('staff.off')}`}<br /><span className="text-muted">{formatPhone(p.phone)}</span></span>
            <a href={`tel:${p.phone}`} className="flex min-h-11 items-center rounded-xl border border-line px-3 font-semibold no-underline">{t('platform.call')}</a>
            <a href={`https://wa.me/${p.phone.replace(/\D/g, '')}`} target="_blank" rel="noreferrer" className="flex min-h-11 items-center rounded-xl border border-line px-3 font-semibold no-underline">WhatsApp</a>
            {p.lockedOut && canSupport && <button disabled={busy} onClick={() => void run(`/v1/platform/users/${p.userId}/unlock`, {}, 'POST', t('platform.unlocked', { name: p.name }))} className="min-h-11 rounded-xl bg-amber-100 px-3 font-semibold text-amber-900">{t('platform.unlock')}</button>}
          </li>
        ))}
      </ul>

      <label className="flex flex-col gap-1 text-sm font-bold">
        {t('platform.note')}
        <textarea value={note ?? d.adminNote ?? ''} onChange={(e) => setNote(e.target.value)} rows={3} readOnly={!canSupport}
          className="rounded-xl border border-line p-3 font-normal read-only:bg-ground" placeholder={t('platform.notePlaceholder')} />
      </label>
      {note !== null && note !== (d.adminNote ?? '') && (
        <Button tone="plain" disabled={busy} onClick={() => void run(`/v1/platform/shops/${id}/note`, { note }, 'PUT', t('platform.noteSaved')).then(() => setNote(null))}>{t('platform.saveNote')}</Button>
      )}

      <ShopBillingPanel shopId={id} role={role} shopName={d.shop.name} ownerPhone={d.people.find((x) => x.role === 'owner')?.phone ?? null} />

      {canApprove && <div className="grid grid-cols-2 gap-2">
        {d.shop.isActive && !d.shop.approvedAt && <Button disabled={busy} onClick={() => void run(`/v1/platform/shops/${id}/approve`)}>{t('platform.approve')}</Button>}
        {d.shop.isActive
          ? <button disabled={busy} onClick={block} className="min-h-12 rounded-2xl border border-line font-bold text-red-700">{t('platform.block')}</button>
          : <Button disabled={busy} onClick={() => void run(`/v1/platform/shops/${id}/unblock`)}>{t('platform.unblock')}</Button>}
      </div>}
    </div>
  )
}

interface Admin { userId: string; name: string; phone: string; role: AdminRole; addedAt: string; addedBy: string | null }

const ROLES: AdminRole[] = ['super', 'approver', 'support', 'billing', 'agent_manager']

/**
 * Super admins only: who else has admin access, and what they may do. Add a person by their phone number (they need
 * an account, and on the live server must have signed in once with an SMS code), change their role, or remove them;
 * every change works on their next tap and is logged. There is always at least one super admin.
 */
const SHOP_TYPES = ['kirana', 'building_materials', 'paint', 'hardware', 'other'] as const

/**
 * Opens a shop for a shopkeeper who can't sign up alone (on a visit or a call). It starts approved on the free trial.
 * A new number gets a one-time password, shown once here, to read out to the owner.
 */
function NewShop() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const empty = { shopName: '', ownerName: '', phone: '', area: '', type: 'kirana', language: 'hi' }
  const [form, setForm] = useState(empty)
  const [open, setOpen] = useState(false)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [created, setCreated] = useState<{ name: string; phone: string; password: string | null; code: string | null } | null>(null)
  const set = (key: keyof typeof empty) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value })

  async function create() {
    setBusy(true)
    setMessage(null)
    setCreated(null)
    try {
      const result = await api<{ shopId: string; newAccount: boolean; tempPassword: string | null; shopCode: string | null }>('/v1/platform/shops', 'POST', {
        ...form, shopName: form.shopName.trim(), ownerName: form.ownerName.trim(), phone: form.phone.trim(), area: form.area.trim() || null,
      })
      setCreated({ name: form.shopName.trim(), phone: form.phone.trim(), password: result.tempPassword, code: result.shopCode })
      setForm(empty)
      await queryClient.invalidateQueries({ queryKey: ['platform'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  const input = 'h-12 rounded-xl border border-line bg-white px-3'
  return (
    <section className="flex flex-col gap-3 rounded-2xl bg-white p-4">
      <button onClick={() => setOpen(!open)} aria-expanded={open} className="flex min-h-11 items-center justify-between text-left">
        <h2 className="text-lg font-bold">{t('openShop.title')}</h2>
        <span aria-hidden className="text-xl">{open ? '−' : '+'}</span>
      </button>
      {open && <>
        <p className="text-sm text-muted">{t('openShop.hint')}</p>
        <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('openShop.shopName')}</span>
          <input value={form.shopName} onChange={set('shopName')} className={input} /></label>
        <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('openShop.ownerName')}</span>
          <input value={form.ownerName} onChange={set('ownerName')} className={input} /></label>
        <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('openShop.phone')}</span>
          <input value={form.phone} onChange={set('phone')} inputMode="tel" className={input} /></label>
        <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('openShop.area')}</span>
          <input value={form.area} onChange={set('area')} className={input} /></label>
        <div className="grid grid-cols-2 gap-2">
          <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('shopType.label')}</span>
            <select value={form.type} onChange={set('type')} className={input}>
              {SHOP_TYPES.map((x) => <option key={x} value={x}>{t(`shopType.${x}`)}</option>)}
            </select></label>
          <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('openShop.language')}</span>
            <select value={form.language} onChange={set('language')} className={input}>
              <option value="hi">हिंदी</option><option value="en">English</option>
            </select></label>
        </div>
        <Button disabled={busy || form.shopName.trim().length < 2 || form.phone.trim().length < 10} onClick={() => void create()}>{t('openShop.create')}</Button>
        {created && (
          <div className="flex flex-col gap-1 rounded-xl bg-teal-50 p-3 text-sm text-teal-900">
            <span className="font-semibold">{t('openShop.done', { name: created.name })}{created.code && <span className="ml-2 font-mono">{created.code}</span>}</span>
            {created.password
              ? <span>{t('openShop.login', { phone: created.phone })} <span className="font-mono text-base font-bold">{created.password}</span><br />{t('openShop.once')}</span>
              : <span>{t('openShop.existing')}</span>}
          </div>
        )}
        {message && <p className="rounded-xl bg-red-50 p-3 text-sm font-semibold text-red-800">{message}</p>}
      </>}
    </section>
  )
}

function ManageAdmins() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const me = useSession()?.user.id
  const admins = useQuery({ queryKey: ['platform', 'admins'], queryFn: () => api<Admin[]>('/v1/platform/admins') })
  const [phone, setPhone] = useState('')
  const [role, setRole] = useState<AdminRole>('support')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [open, setOpen] = useState(false)

  async function run(path: string, method: 'POST' | 'PUT' | 'DELETE', body?: object, done?: string) {
    setBusy(true)
    setMessage(null)
    try {
      const list = await api<Admin[]>(path, method, body)
      queryClient.setQueryData(['platform', 'admins'], list)
      if (method === 'POST') setPhone('')
      if (done) setMessage(done)
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="flex flex-col gap-3 rounded-2xl bg-white p-4">
      <button onClick={() => setOpen(!open)} aria-expanded={open} className="flex min-h-11 items-center justify-between text-left">
        <h2 className="text-lg font-bold">{t('admins.title')}{admins.data ? ` (${admins.data.length})` : ''}</h2>
        <span aria-hidden className="text-xl">{open ? '−' : '+'}</span>
      </button>
      {open && <>
      <p className="text-sm text-muted">{t('admins.hint')}</p>
      <ul className="flex flex-col divide-y divide-[#edf2f1]">
        {admins.data?.map((a) => (
          <li key={a.userId} className="flex flex-wrap items-center gap-2 py-2">
            <span className="flex min-w-0 flex-1 flex-col">
              <span className="font-semibold">{a.name}{a.userId === me ? ` (${t('admins.you')})` : ''}</span>
              <span className="text-xs text-muted">{formatPhone(a.phone)}{a.addedBy ? ` · ${t('admins.addedBy', { name: a.addedBy })}` : ''}</span>
            </span>
            <label className="sr-only" htmlFor={`role-${a.userId}`}>{t('admins.role')}</label>
            <select id={`role-${a.userId}`} value={a.role} disabled={busy}
              onChange={(e) => void run(`/v1/platform/admins/${a.userId}`, 'PUT', { role: e.target.value }, t('admins.changed', { name: a.name }))}
              className="h-11 rounded-xl border border-line bg-white px-2">
              {ROLES.map((r) => <option key={r} value={r}>{t(`admins.roles.${r}`)}</option>)}
            </select>
            <button disabled={busy} aria-label={t('admins.remove', { name: a.name })}
              onClick={() => confirm(t('admins.removeAsk', { name: a.name })) && void run(`/v1/platform/admins/${a.userId}`, 'DELETE', undefined, t('admins.removed', { name: a.name }))}
              className="h-11 rounded-xl border border-line px-3 text-sm font-semibold text-red-700">{t('admins.removeShort')}</button>
          </li>
        ))}
      </ul>
      <div className="flex flex-col gap-2 rounded-xl bg-ground p-3">
        <span className="font-semibold">{t('admins.addTitle')}</span>
        <label className="sr-only" htmlFor="admin-phone">{t('staff.phone')}</label>
        <input id="admin-phone" value={phone} onChange={(e) => setPhone(e.target.value)} inputMode="tel" placeholder={t('staff.phone')}
          className="h-12 rounded-xl border border-line bg-white px-3" />
        <label className="sr-only" htmlFor="admin-role">{t('admins.role')}</label>
        <select id="admin-role" value={role} onChange={(e) => setRole(e.target.value as AdminRole)} className="h-12 rounded-xl border border-line bg-white px-3">
          {ROLES.map((r) => <option key={r} value={r}>{t(`admins.roles.${r}`)} — {t(`admins.can.${r}`)}</option>)}
        </select>
        <Button disabled={busy || phone.trim().length < 10} onClick={() => void run('/v1/platform/admins', 'POST', { phone: phone.trim(), role }, t('admins.added'))}>
          {t('admins.add')}
        </Button>
      </div>
      {message && <p className="rounded-xl bg-teal-50 p-3 text-sm font-semibold text-teal-900">{message}</p>}
      </>}
    </section>
  )
}

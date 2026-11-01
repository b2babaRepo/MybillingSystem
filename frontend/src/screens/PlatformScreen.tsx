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
import { shrinkPhoto } from '../lib/images'

interface MarketRow { id: string; name: string; state: string | null; district: string | null; city: string | null; village: string | null; isActive: boolean; shops: number }

const marketPlace = (m: MarketRow) => [m.village, m.city, m.district, m.state].filter(Boolean).join(', ')
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
type Section = 'shops' | 'plans' | 'markets' | 'agents' | 'ai' | 'admins'

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
  const [section, setSection] = useState<Section>('shops')
  const overview = useQuery({ queryKey: ['platform', 'overview'], queryFn: () => api<Overview>('/v1/platform/overview') })
  const shops = useQuery({
    queryKey: ['platform', 'shops', status, q, plan],
    queryFn: () => api<ShopRow[]>(`/v1/platform/shops?status=${status}&q=${encodeURIComponent(q.trim())}&plan=${plan}`),
  })
  const seesBilling = role === 'super' || role === 'billing'
  const byPlan = useQuery({
    queryKey: ['platform', 'billing', 'byPlan'],
    queryFn: () => api<{ byPlan?: Record<string, number> }>('/v1/platform/billing'),
    enabled: seesBilling,
    select: (data) => data.byPlan ?? {},
  }).data
  const planTiles: [string, string][] = [['trial', t('plan2.status.trial')], ...plans.map((p): [string, string] => [p.code, p.name])]
  function showPlan(code: string) {
    setStatus('all')
    setPlan(code)
  }
  const tiles: { label: string; value: number | undefined; onClick?: () => void; urgent?: boolean }[] = [
    { label: t('platform.awaiting'), value: overview.data?.awaitingApproval, onClick: () => showStatus('new'), urgent: !!overview.data?.awaitingApproval },
    { label: t('platform.active'), value: overview.data?.active, onClick: () => showStatus('active') },
    { label: t('platform.blocked'), value: overview.data?.blocked, onClick: () => showStatus('blocked') },
    { label: t('platform.newWeek'), value: overview.data?.newThisWeek },
    { label: t('platform.households'), value: overview.data?.households },
  ]
  function showStatus(next: Status) {
    setStatus(next)
    setPlan('')
  }
  const sections: { id: Section; label: string; show: boolean }[] = [
    { id: 'shops', label: t('platform.title'), show: true },
    { id: 'plans', label: t('platformNav.plans'), show: seesBilling },
    { id: 'markets', label: t('platformNav.markets'), show: role !== 'support' },
    { id: 'agents', label: t('platformNav.agents'), show: role !== 'support' },
    { id: 'ai', label: t('platformNav.ai'), show: true },
    { id: 'admins', label: t('platformNav.admins'), show: role === 'super' },
  ]

  return (
    <div className="min-h-dvh bg-ground">
      <div className="mx-auto flex max-w-6xl flex-col pb-10 lg:flex-row">
        <nav aria-label={t('platform.title')} className="z-10 bg-slate-800 text-white lg:sticky lg:top-0 lg:h-dvh lg:w-60 lg:shrink-0 lg:p-3">
          <div className="flex items-center gap-2 px-4 py-3 lg:px-2">
            <button onClick={() => navigate('/')} className="-ml-2 flex h-11 w-11 items-center justify-center" aria-label={t('a11y.back')}><BackIcon /></button>
            <span className="text-lg font-bold">{t('platformNav.brand')}</span>
          </div>
          <div className="flex gap-1 overflow-x-auto px-3 pb-2 lg:flex-col lg:overflow-visible lg:px-0">
            {sections.filter((x) => x.show).map((x) => (
              <button key={x.id} onClick={() => setSection(x.id)} aria-current={section === x.id ? 'page' : undefined}
                className={`min-h-11 shrink-0 rounded-xl px-3 text-left font-semibold ${section === x.id ? 'bg-teal-700' : 'text-slate-200'}`}>{x.label}</button>
            ))}
          </div>
        </nav>

        <main className="flex min-w-0 flex-1 flex-col gap-3 p-4">
          {section === 'shops' && <>
            <h1 className="text-2xl font-extrabold">{t('platform.title')}</h1>
            <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-5">
              {tiles.map((x) => {
                const body = <><div className="text-2xl font-extrabold tabular-nums">{x.value ?? '–'}</div><div className="text-xs">{x.label}</div></>
                const style = `rounded-2xl p-3 text-left ${x.urgent ? 'bg-amber-100 text-amber-900' : 'bg-white'}`
                return x.onClick ? <button key={x.label} onClick={x.onClick} className={style}>{body}</button> : <div key={x.label} className={style}>{body}</div>
              })}
            </div>
            {role !== 'support' && <NewShop canBill={seesBilling} />}
            <section className="flex flex-col gap-3 rounded-2xl bg-white p-3">
              <label className="sr-only" htmlFor="platform-search">{t('platform.search')}</label>
              <input id="platform-search" value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('platform.searchWithId')}
                className="min-h-12 rounded-2xl border border-line bg-white px-4" />
              <div role="tablist" className="grid grid-cols-4 gap-1 text-sm font-semibold">
                {(['new', 'active', 'blocked', 'all'] as const).map((x) => (
                  <button key={x} role="tab" aria-selected={status === x} onClick={() => setStatus(x)}
                    className={`min-h-11 rounded-xl ${status === x ? 'bg-teal-700 text-white' : 'bg-ground'}`}>{t(`platform.tab.${x}`)}</button>
                ))}
              </div>
              <div className="flex flex-wrap gap-2" aria-label={t('plans.filter')}>
                <button aria-pressed={plan === ''} onClick={() => setPlan('')} className={`min-h-11 rounded-full px-4 text-sm font-semibold ${plan === '' ? 'bg-teal-700 text-white' : 'border border-line'}`}>{t('plans.allPlans')}</button>
                {seesBilling
                  ? planTiles.map(([code, label]) => (
                    <button key={code} aria-pressed={plan === code} onClick={() => showPlan(code)} className={`min-h-11 rounded-full px-4 text-sm font-semibold ${plan === code ? 'bg-teal-700 text-white' : 'border border-line'}`}>{label} · {byPlan ? byPlan[code] ?? 0 : '–'}</button>
                  ))
                  : plans.map((p) => (
                    <button key={p.code} aria-pressed={plan === p.code} onClick={() => showPlan(p.code)} className={`min-h-11 rounded-full px-4 text-sm font-semibold ${plan === p.code ? 'bg-teal-700 text-white' : 'border border-line'}`}>{p.name}</button>
                  ))}
              </div>
            </section>

            {shops.data?.length === 0 && <p className="p-6 text-center text-muted">{t('platform.none')}</p>}
            <ul className="flex flex-col gap-2">
              {shops.data?.map((s) => (
                <li key={s.id} className="rounded-2xl bg-white">
                  <button onClick={() => setOpenId(openId === s.id ? null : s.id)} aria-expanded={openId === s.id} className="flex w-full items-start gap-3 p-4 text-left">
                    <span className="flex min-w-0 flex-1 flex-col lg:grid lg:grid-cols-[2fr_1fr_1fr] lg:gap-3">
                      <span className="min-w-0">
                        <span className="block truncate text-[17px] font-bold">{s.name}{s.shopCode && <span className="ml-2 font-mono text-xs font-semibold text-muted">{s.shopCode}</span>}</span>
                        <span className="block truncate text-sm text-muted">{[s.area, formatDateTime(s.createdAt)].filter(Boolean).join(' · ')}</span>
                      </span>
                      <span className="truncate text-sm text-muted">{[s.ownerName, formatPhone(s.ownerPhone)].filter(Boolean).join(' · ')}</span>
                      <span className="text-sm text-muted">
                        {s.planCode && (s.planStatus === 'trial' ? t('plan2.status.trial') : `${plans.find((p) => p.code === s.planCode)?.name ?? s.planCode}${s.planStatus === 'active' || s.planStatus === 'grace' ? ` ${t('billing.paidTag')}` : ''}`)}
                        {s.planEndsAt && ` · ${t('billing.until', { date: new Date(s.planEndsAt).toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' }) })}`}
                      </span>
                    </span>
                    <StatusChip shop={s} />
                  </button>
                  {openId === s.id && <ShopDetail id={s.id} role={role} />}
                </li>
              ))}
            </ul>
          </>}
          {section === 'plans' && <><PlansPanel /><BillingPanel /></>}
          {section === 'markets' && <MarketsPanel />}
          {section === 'agents' && <>
            {role !== 'support' && <RegistrationsPanel role={role} />}
            {(role === 'super' || role === 'agent_manager') && <AgentsPanel />}
            {(role === 'super' || role === 'agent_manager' || role === 'billing') && <PerformancePanel role={role} />}
          </>}
          {section === 'ai' && <AgentPanel role={role} />}
          {section === 'admins' && <><ManageAdmins /><ActivityPanel /></>}
        </main>
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
  const canPeople = role !== 'support'
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
            {canPeople && (
              <button disabled={busy} onClick={() => (!p.isActive || confirm(t('staff.offConfirm', { name: p.name }))) && void run(`/v1/platform/shops/${id}/people/${p.membershipId}/active?active=${!p.isActive}`, {}, 'POST')}
                className={`min-h-11 rounded-xl px-3 font-semibold ${p.isActive ? 'border border-line text-due' : 'bg-teal-700 text-white'}`}>{p.isActive ? t('staff.turnOff') : t('staff.turnOn')}</button>
            )}
          </li>
        ))}
      </ul>
      {canPeople && <AddPerson shopId={id} />}

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
function useMarkets() {
  return useQuery({ queryKey: ['platform', 'markets'], queryFn: () => api<MarketRow[]>('/v1/platform/markets') })
}

/** The platform's list of markets: add one, rename it, or stop offering it. Shops keep the market they have. */
const emptyMarket = { name: '', state: '', district: '', city: '', village: '' }

function MarketsPanel() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const markets = useMarkets().data ?? []
  const [open, setOpen] = useState(false)
  const [draft, setDraft] = useState(emptyMarket)
  const [message, setMessage] = useState<string | null>(null)

  async function run(action: () => Promise<unknown>) {
    setMessage(null)
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: ['platform', 'markets'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    }
  }

  const add = () => run(async () => {
    await api('/v1/platform/markets', 'POST', { name: draft.name.trim(), state: draft.state.trim() || null, district: draft.district.trim() || null, city: draft.city.trim() || null, village: draft.village.trim() || null })
    setDraft({ ...emptyMarket, state: draft.state, district: draft.district })
  })
  const rename = (m: MarketRow) => {
    const name = window.prompt(t('markets.name'), m.name)?.trim()
    if (name) void run(() => api(`/v1/platform/markets/${m.id}`, 'PUT', { ...m, name }))
  }

  const input = 'h-12 min-w-0 rounded-xl border border-line bg-white px-3'
  return (
    <section className="flex flex-col gap-3 rounded-2xl bg-white p-4">
      <button onClick={() => setOpen(!open)} aria-expanded={open} className="flex min-h-11 items-center justify-between text-left">
        <h2 className="text-lg font-bold">{t('markets.title', { count: markets.length })}</h2>
        <span aria-hidden className="text-xl">{open ? '−' : '+'}</span>
      </button>
      {open && <>
        <div className="grid grid-cols-2 gap-2">
          <input aria-label={t('markets.name')} placeholder={t('markets.name')} value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} className={`${input} col-span-2`} />
          {(['state', 'district', 'city', 'village'] as const).map((key) => (
            <input key={key} aria-label={t(`markets.${key}`)} placeholder={t(`markets.${key}`)} value={draft[key]} onChange={(e) => setDraft({ ...draft, [key]: e.target.value })} className={input} />
          ))}
        </div>
        <Button disabled={draft.name.trim().length < 2} onClick={() => void add()}>{t('markets.add')}</Button>
        {message && <p className="text-sm text-due">{message}</p>}
        <ul className="flex flex-col gap-2">
          {markets.map((m) => (
            <li key={m.id} className={`flex items-center gap-2 border-t border-line pt-2 ${m.isActive ? '' : 'opacity-60'}`}>
              <span className="min-w-0 flex-1 truncate">{m.name}{marketPlace(m) ? `, ${marketPlace(m)}` : ''} · {t('markets.shops', { count: m.shops })}</span>
              <button onClick={() => rename(m)} className="min-h-11 px-2 text-sm font-semibold text-teal-700">{t('markets.rename')}</button>
              <button onClick={() => void run(() => api(`/v1/platform/markets/${m.id}`, 'PUT', { ...m, isActive: !m.isActive }))} className="min-h-11 px-2 text-sm font-semibold">{m.isActive ? t('markets.stop') : t('markets.restart')}</button>
            </li>
          ))}
        </ul>
      </>}
    </section>
  )
}

function NewShop({ canBill }: { canBill: boolean }) {
  const { t } = useTranslation()
  const plans = usePlans().data ?? []
  // Empty plan: the shop starts on the usual free trial.
  const markets = useMarkets().data ?? []
  const [marketId, setMarketId] = useState('')
  const emptyIds = { gstin: '', shopNo: '', state: '', district: '', city: '', village: '' }
  const [ids, setIds] = useState(emptyIds)
  const [gps, setGps] = useState<{ latitude: number; longitude: number } | null>(null)
  const [photo, setPhoto] = useState<string | null>(null)
  const locate = () => navigator.geolocation.getCurrentPosition(
    (p) => {
      setGps({ latitude: p.coords.latitude, longitude: p.coords.longitude })
      // Fills what it finds; anything it can't find stays as typed.
      void api<Record<'state' | 'district' | 'city' | 'village', string | null>>(`/v1/platform/markets/place?lat=${p.coords.latitude}&lng=${p.coords.longitude}`)
        .then((f) => setIds((x) => ({ ...x, state: f.state ?? x.state, district: f.district ?? x.district, city: f.city ?? x.city, village: f.village ?? x.village })))
        .catch(() => undefined)
    },
    () => setMessage(t('markets.gpsFailed')),
    { enableHighAccuracy: true, timeout: 20_000 },
  )
  // Only the markets of the place entered: the village if given, else the city, else the district or state.
  const same = (a: string | null, b: string) => (a ?? '').trim().toLowerCase() === b.trim().toLowerCase()
  const placeMarkets = markets.filter((m) => m.isActive && (
    ids.village.trim() ? same(m.village, ids.village)
      : ids.city.trim() ? same(m.city, ids.city)
        : ids.district.trim() ? same(m.district, ids.district)
          : ids.state.trim() ? same(m.state, ids.state) : true))
  const chosenMarket = placeMarkets.some((m) => m.id === marketId) ? marketId : ''
  const [paid, setPaid] = useState({ planCode: '', term: 'year1', method: 'cash', reference: '' })
  const queryClient = useQueryClient()
  const empty = { shopName: '', ownerName: '', phone: '', area: '', type: 'kirana', language: 'hi' }
  const [people, setPeople] = useState<PersonDraft[]>([])
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
        people: people.filter((p) => p.name.trim() && p.phone.trim()).map(toPerson),
        gstin: ids.gstin.trim() || null, shopNo: ids.shopNo.trim() || null,
        state: ids.state.trim() || null, district: ids.district.trim() || null, city: ids.city.trim() || null, village: ids.village.trim() || null,
        marketId: chosenMarket || null, latitude: gps?.latitude ?? null, longitude: gps?.longitude ?? null, photoDataUrl: photo,
      })
      setMarketId('')
      setIds(emptyIds)
      setGps(null)
      setPhoto(null)
      setCreated({ name: form.shopName.trim(), phone: form.phone.trim(), password: result.tempPassword, code: result.shopCode })
      if (paid.planCode) {
        // The shop exists either way: a failed payment can be recorded again from the shop's billing panel.
        await api(`/v1/platform/shops/${result.shopId}/payments`, 'POST', { ...paid, reference: paid.reference.trim() || null })
        setPaid({ ...paid, planCode: '', reference: '' })
      }
      setForm(empty)
      setPeople([])
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
        {canBill && (
          <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('billing.plan')}</span>
            <select value={paid.planCode} onChange={(e) => setPaid({ ...paid, planCode: e.target.value })} className={input}>
              <option value="">{t('plan2.status.trial')}</option>
              {plans.filter((p) => p.code !== 'free').map((p) => <option key={p.code} value={p.code}>{p.name}</option>)}
            </select></label>
        )}
        {canBill && paid.planCode && (
          <div className="grid grid-cols-2 gap-2">
            <select aria-label={t('billing.plan')} value={paid.term} onChange={(e) => setPaid({ ...paid, term: e.target.value })} className={input}>
              {['year1', 'year3', 'year5'].map((term) => <option key={term} value={term}>{t(`plan2.term.${term}`)}</option>)}
            </select>
            <select aria-label={t('billing.plan')} value={paid.method} onChange={(e) => setPaid({ ...paid, method: e.target.value })} className={input}>
              {['cash', 'upi'].map((method) => <option key={method} value={method}>{t(`billing.methods.${method}`)}</option>)}
            </select>
          </div>
        )}
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
        <button type="button" onClick={locate} className="min-h-11 rounded-xl border border-line font-semibold">{gps ? t('markets.gpsDone') : t('markets.gps')}</button>
        <div className="grid grid-cols-2 gap-2">
          {(['state', 'district', 'city', 'village'] as const).map((key) => (
            <label key={key} className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t(`markets.${key}`)}</span>
              <input value={ids[key]} onChange={(e) => setIds({ ...ids, [key]: e.target.value })} maxLength={80} className={`${input} min-w-0`} /></label>
          ))}
        </div>
        <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('markets.market')}</span>
          <select value={chosenMarket} onChange={(e) => {
            setMarketId(e.target.value)
            const m = markets.find((x) => x.id === e.target.value)
            if (m) setIds((x) => ({ ...x, state: m.state ?? x.state, district: m.district ?? x.district, city: m.city ?? x.city, village: m.village ?? x.village }))
          }} className={input}>
            <option value="">{t(placeMarkets.length ? 'markets.none' : 'markets.noneHere')}</option>
            {placeMarkets.map((m) => <option key={m.id} value={m.id}>{m.name}{marketPlace(m) ? `, ${marketPlace(m)}` : ''}</option>)}
          </select></label>
        <div className="grid grid-cols-2 gap-2">
          <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('markets.shopNo')}</span>
            <input value={ids.shopNo} onChange={(e) => setIds({ ...ids, shopNo: e.target.value })} maxLength={30} className={`${input} min-w-0`} /></label>
          <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('markets.gstin')}</span>
            <input value={ids.gstin} onChange={(e) => setIds({ ...ids, gstin: e.target.value.toUpperCase() })} maxLength={15} className={`${input} min-w-0 uppercase`} /></label>
        </div>
        <label className="flex min-h-11 cursor-pointer items-center justify-center rounded-xl border border-line font-semibold">
          {photo ? t('markets.photoDone') : t('markets.photo')}
          <input type="file" accept="image/*" capture="environment" className="sr-only"
            onChange={(e) => { const file = e.target.files?.[0]; if (file) void shrinkPhoto(file, 1024, 140_000, false).then(setPhoto) }} />
        </label>
        <PeopleRows people={people} onChange={setPeople} />
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

interface PersonDraft { name: string; phone: string; role: 'owner' | 'staff'; pin: string }
const toPerson = (p: PersonDraft) => ({ name: p.name.trim(), phone: p.phone.trim(), role: p.role, pin: p.role === 'staff' ? p.pin : null })

/** Extra owners and helpers typed in while opening a shop; the plan (trial: Gold) sets how many fit. */
function PeopleRows({ people, onChange }: { people: PersonDraft[]; onChange: (p: PersonDraft[]) => void }) {
  const { t } = useTranslation()
  const set = (i: number, patch: Partial<PersonDraft>) => onChange(people.map((p, j) => (j === i ? { ...p, ...patch } : p)))
  const box = 'h-11 min-w-0 rounded-xl border border-line bg-white px-2'
  return (
    <div className="flex flex-col gap-2">
      {people.map((p, i) => (
        <div key={i} className="grid grid-cols-2 gap-2 rounded-xl bg-ground p-2 text-sm">
          <select aria-label={t('staff.roleLabel')} value={p.role} onChange={(e) => set(i, { role: e.target.value as PersonDraft['role'] })} className={box}>
            <option value="owner">{t('staff.owner')}</option><option value="staff">{t('staff.helper')}</option>
          </select>
          <button type="button" onClick={() => onChange(people.filter((_, j) => j !== i))} className="min-h-11 rounded-xl border border-line font-semibold">{t('plans.cancel')}</button>
          <input aria-label={t('staff.name')} placeholder={t('staff.name')} value={p.name} onChange={(e) => set(i, { name: e.target.value })} className={box} />
          <input aria-label={t('staff.phone')} placeholder={t('staff.phone')} value={p.phone} inputMode="tel" onChange={(e) => set(i, { phone: e.target.value })} className={box} />
          {p.role === 'staff' && <input aria-label={t('staff.pin')} placeholder={t('staff.pin')} value={p.pin} inputMode="numeric"
            onChange={(e) => set(i, { pin: e.target.value.replace(/\D/g, '').slice(0, 6) })} className={`${box} col-span-2`} />}
        </div>
      ))}
      <button type="button" onClick={() => onChange([...people, { name: '', phone: '', role: 'owner', pin: '' }])}
        className="min-h-11 rounded-xl border-2 border-dashed border-line font-semibold">+ {t('people.addMore')}</button>
    </div>
  )
}

/** In a shop's admin detail: add an owner or a helper (with a PIN), within the shop's plan. */
function AddPerson({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState<PersonDraft[]>([])
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  async function save() {
    setBusy(true)
    setMessage(null)
    try {
      for (const p of draft.filter((x) => x.name.trim() && x.phone.trim())) await api(`/v1/platform/shops/${shopId}/people`, 'POST', toPerson(p))
      setDraft([])
      await queryClient.invalidateQueries({ queryKey: ['platform'] })
      setMessage(t('people.added'))
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }
  return (
    <div className="flex flex-col gap-2">
      <PeopleRows people={draft} onChange={setDraft} />
      {draft.length > 0 && <Button disabled={busy} onClick={() => void save()}>{t('people.save')}</Button>}
      {message && <p className="rounded-xl bg-teal-50 p-3 text-sm font-semibold text-teal-900">{message}</p>}
    </div>
  )
}

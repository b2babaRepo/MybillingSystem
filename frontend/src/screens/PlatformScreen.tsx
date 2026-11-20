import { SHOP_TYPES, type ShopType } from '../lib/reports'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { lazy, Suspense, useState, Fragment } from 'react'
import { useTranslation } from 'react-i18next'
import { BackIcon } from '../components/Icons'
import { Button, btn } from '../components/ui'
import { api, request } from '../lib/api'
import { formatDateTime, formatPhone } from '../lib/format'
import { useOutbox, useSession } from '../lib/data'
import { ActivityPanel, AgentPanel, BillingPanel, PlansPanel, ShopBillingPanel } from './PlatformBilling'
import { usePlans } from '../lib/billing'
import { AgentsPanel, AgentsSummary, PerformancePanel, RegistrationsPanel } from './PlatformAgents'
import { addText, shownLocale } from '../i18n'
import { adminText } from '../i18n/admin'
import { ask, confirmAsk } from '../components/Ask'

addText(adminText)
import type { AdminRole } from '../lib/types'
import { navigate } from '../router'
import { getSession, setSession } from '../lib/session'

const AccountMenu = lazy(() => import('../components/AccountMenu').then((module) => ({ default: module.AccountMenu })))
import { shrinkPhoto } from '../lib/images'

interface MarketRow { id: string; name: string; state: string | null; district: string | null; city: string | null; village: string | null; isActive: boolean; shops: number }

const marketPlace = (m: MarketRow) => [m.village, m.city, m.district, m.state].filter(Boolean).join(', ')
interface Overview { shops: number; active: number; blocked: number; awaitingApproval: number; newThisWeek: number; households: number; oldClaims?: number; jobsBehind?: number }
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
interface Detail { shopNo?: string | null; gstin?: string | null; market?: string | null; place?: string | null; latitude?: number | null; longitude?: number | null; hasPhoto?: boolean; shop: ShopRow; address: string | null; adminNote: string | null; people: Person[]; customers: number; billsLast30Days: number; lastBillAt: string | null; lastBalanceCheck: { mismatches: number } | null }

type Status = 'new' | 'active' | 'blocked' | 'all'
type Section = 'shops' | 'plans' | 'markets' | 'items' | 'agents' | 'ai' | 'admins'

/**
 * The platform admin's view of every shop: counts at the top, then new / active / blocked shops with search. Open a
 * shop to see its people (call or WhatsApp them, unlock a lockout), its activity, a private support note, and to
 * approve, block (with a reason) or unblock it.
 */
export function PlatformScreen() {
  const { t } = useTranslation()
  // What this admin may do; the server checks the same on every request (a session from before roles counts as super).
  const session = useSession()
  const role: AdminRole = session?.user.platformRole ?? 'super'
  // An admin who also runs a shop may have bills not yet sent: warn before signing out, as the shop screens do.
  const { pending, failed } = useOutbox(session?.shopId ?? '')
  async function signOut() {
    if (pending + failed > 0 && !(await confirmAsk(t('unsyncedSignOut', { count: pending + failed })))) return
    void request('/v1/auth/sign-out', 'POST', { refreshToken: getSession()?.refreshToken }).catch(() => {})
    setSession(null)
    navigate('/')
  }
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
    ...(seesBilling && overview.data?.oldClaims ? [{ label: t('platformNav.oldClaims'), value: overview.data.oldClaims, onClick: () => setSection('plans' as Section), urgent: true }] : []),
    ...(overview.data?.jobsBehind ? [{ label: t('platformNav.jobsBehind'), value: overview.data.jobsBehind, urgent: true }] : []),
  ]
  function showStatus(next: Status) {
    setStatus(next)
    setPlan('')
  }
  const SECTION_ICON: Record<Section, string> = {
    shops: 'M4 9l1-5h14l1 5M4 9h16v11H4zM9 20v-6h6v6', plans: 'M3 6h18v12H3zM3 10h18M7 15h3',
    markets: 'M12 21s7-6 7-11a7 7 0 10-14 0c0 5 7 11 7 11zM12 8a2 2 0 100 4 2 2 0 000-4', items: 'M9 5h11M9 12h11M9 19h11M4 5h.01M4 12h.01M4 19h.01',
    agents: 'M9 11a4 4 0 100-8 4 4 0 000 8zM2 21v-1a7 7 0 0114 0v1M17 3a4 4 0 010 8M22 21v-1a7 7 0 00-4-6', ai: 'M12 3l2 5 5 2-5 2-2 5-2-5-5-2 5-2z',
    admins: 'M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z',
  }
  const queryClient = useQueryClient()
  // Shop ticks for approving several at once (new shops only).
  const [picked, setPicked] = useState<string[]>([])
  const [hideTest, setHideTest] = useState(false)
  const [bulkBusy, setBulkBusy] = useState(false)
  const [addAgent, setAddAgent] = useState(0)
  const canApprove = role === 'super' || role === 'approver'
  const isTest = (s: ShopRow) => /\b(test|qa)\b|\(test\)/i.test(s.name)
  const shown = (shops.data ?? []).filter((s) => !hideTest || !isTest(s))
  async function approve(ids: string[]) {
    setBulkBusy(true)
    try {
      for (const id of ids) await request(`/v1/platform/shops/${id}/approve`, 'POST', {})
      setPicked([])
      await queryClient.invalidateQueries({ queryKey: ['platform'] })
    } finally {
      setBulkBusy(false)
    }
  }
  const sections: { id: Section; label: string; show: boolean }[] = [
    { id: 'shops', label: t('platform.title'), show: true },
    { id: 'plans', label: t('platformNav.plans'), show: seesBilling },
    { id: 'markets', label: t('platformNav.markets'), show: role !== 'support' },
    { id: 'items', label: t('masterItems.title'), show: role !== 'support' },
    { id: 'agents', label: t('platformNav.agents'), show: role !== 'support' },
    { id: 'ai', label: t('platformNav.ai'), show: true },
    { id: 'admins', label: t('platformNav.admins'), show: role === 'super' },
  ]

  return (
    <div className="min-h-dvh bg-ground">
      <div className="mx-auto flex max-w-6xl flex-col pb-10 lg:flex-row">
        <nav aria-label={t('platform.title')} className="z-10 bg-[#13241f] text-white lg:sticky lg:top-0 lg:h-dvh lg:w-60 lg:shrink-0 lg:p-3">
          <div className="flex items-center gap-2 px-4 py-3 lg:px-2">
            <button onClick={() => navigate('/')} className="-ml-2 flex h-11 w-11 items-center justify-center" aria-label={t('a11y.back')}><BackIcon /></button>
            <span className="flex-1 text-lg font-bold">{t('platformNav.brand')}</span>
            {session && (
              <Suspense fallback={<span className="h-11 w-11 shrink-0 rounded-full bg-amber-400" aria-hidden="true" />}>
                <AccountMenu name={session.user.fullName} role={t('platformNav.role')} unsynced={pending + failed} onSignOut={() => void signOut()}
                  actions={[
                    // Profile is a shop screen, so both show only to an admin who also runs a shop.
                    ...(session.memberships.length > 0 ? [{ label: t('platformNav.toShop'), onClick: () => navigate('/') }, { label: t('profile.title'), onClick: () => navigate('/profile') }] : []),
                  ]} />
              </Suspense>
            )}
          </div>
          <div className="flex gap-1 overflow-x-auto px-3 pb-2 lg:flex-col lg:overflow-visible lg:px-0">
            {sections.filter((x) => x.show).map((x) => (
              <button key={x.id} onClick={() => setSection(x.id)} aria-current={section === x.id ? 'page' : undefined}
                className={`flex min-h-11 shrink-0 items-center gap-3 rounded-xl px-3 text-left font-semibold transition ${section === x.id ? 'bg-brand text-white' : 'text-[#cfe0db] hover:bg-white/10'}`}>
                <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d={SECTION_ICON[x.id]} /></svg>
                <span className="flex-1">{x.label}</span>
                {x.id === 'shops' && !!overview.data?.awaitingApproval && <span className="rounded-full bg-amber-400 px-2 text-xs font-extrabold text-amber-950">{overview.data.awaitingApproval}</span>}
              </button>
            ))}
          </div>
        </nav>

        <main className="flex min-w-0 flex-1 flex-col gap-3 p-4">
          {section === 'shops' && <>
            <h1 className="text-2xl font-extrabold">{t('platform.title')}</h1>
            <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-5">
              {tiles.map((x) => {
                const body = <><div className="text-[28px] leading-none font-extrabold tabular-nums">{x.value ?? '–'}</div><div className="text-[13px] font-bold">{x.label}</div></>
                const tones = ['bg-[#e3f2ee] text-[#0b5e4f]', 'bg-[#fde8ec] text-[#be123c]', 'bg-[#e8effd] text-[#1d4ed8]', 'bg-[#f1eafd] text-[#6d28d9]', 'bg-[#e0f2fe] text-[#0369a1]']
                const style = `flex min-h-[84px] flex-col-reverse justify-between rounded-[18px] p-3 text-left transition ${x.urgent ? 'bg-[#b45309] text-white shadow-lg shadow-[#b45309]/30' : tones[Math.max(0, tiles.indexOf(x) - 1) % tones.length]}`
                return x.onClick ? <button key={x.label} onClick={x.onClick} className={style}>{body}</button> : <div key={x.label} className={style}>{body}</div>
              })}
            </div>
            {role !== 'support' && <NewShop canBill={seesBilling} />}
            <section className="flex flex-col gap-3 rounded-2xl bg-white p-3">
              <label className="sr-only" htmlFor="platform-search">{t('platform.search')}</label>
              <div className="flex flex-wrap items-center gap-3">
                <span className="relative min-w-0 flex-1 basis-64">
                  <span aria-hidden className="pointer-events-none absolute top-1/2 left-4 -translate-y-1/2 text-muted"><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M11 4a7 7 0 100 14 7 7 0 000-14zM20 20l-3.5-3.5" /></svg></span>
                  <input id="platform-search" value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('platform.searchWithId')}
                    className="min-h-12 w-full rounded-2xl border border-line bg-[#f6faf9] pr-4 pl-11" />
                </span>
                <label className="flex min-h-11 items-center gap-2 text-sm font-semibold"><input type="checkbox" checked={hideTest} onChange={(e) => setHideTest(e.target.checked)} className="h-5 w-5" />{t('plat3.hideTest')}</label>
              </div>
              <div role="tablist" className="grid grid-cols-4 gap-1 text-sm font-semibold">
                {(['new', 'active', 'blocked', 'all'] as const).map((x) => (
                  <button key={x} role="tab" aria-selected={status === x} onClick={() => setStatus(x)}
                    className={`min-h-11 rounded-xl transition ${status === x ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'bg-ground text-muted hover:text-ink'}`}>{t(`platform.tab.${x}`)}</button>
                ))}
              </div>
              <div className="flex flex-wrap gap-2" aria-label={t('plans.filter')}>
                <button aria-pressed={plan === ''} onClick={() => setPlan('')} className={`min-h-11 rounded-full px-4 text-sm font-semibold ${plan === '' ? 'bg-brand text-white' : 'border border-line'}`}>{t('plans.allPlans')}</button>
                {seesBilling
                  ? planTiles.map(([code, label]) => (
                    <button key={code} aria-pressed={plan === code} onClick={() => showPlan(code)} className={`min-h-11 rounded-full px-4 text-sm font-semibold ${plan === code ? 'bg-brand text-white' : 'border border-line'}`}>{label} · {byPlan ? byPlan[code] ?? 0 : '–'}</button>
                  ))
                  : plans.map((p) => (
                    <button key={p.code} aria-pressed={plan === p.code} onClick={() => showPlan(p.code)} className={`min-h-11 rounded-full px-4 text-sm font-semibold ${plan === p.code ? 'bg-brand text-white' : 'border border-line'}`}>{p.name}</button>
                  ))}
              </div>
            </section>

            {shops.data?.length === 0 && <p className="rounded-2xl bg-white p-6 text-center text-muted">{t('platform.none')}</p>}
            {canApprove && picked.length > 0 && (
              <div className="sticky top-2 z-10 flex flex-wrap items-center gap-3 rounded-2xl bg-[#13241f] px-4 py-2.5 text-white shadow-lg">
                <b>{t('plat3.ticked', { count: picked.length })}</b>
                <button disabled={bulkBusy} onClick={() => void approve(picked)} className="min-h-10 rounded-xl bg-green-400 px-4 font-bold text-green-950 disabled:opacity-50">{t('plat3.approveAll')}</button>
                <button onClick={() => setPicked([])} className="ml-auto min-h-10 px-2 text-sm font-semibold underline">{t('plat3.clear')}</button>
              </div>
            )}
            {shown.length > 0 && (
              <div className="overflow-x-auto rounded-2xl bg-white shadow-sm">
                <table className="w-full min-w-[820px] border-collapse text-sm">
                  <thead className="bg-[#f6faf9] text-left text-[11px] tracking-wide text-muted uppercase">
                    <tr>
                      <th className="w-10 px-3 py-2.5">{canApprove && <input type="checkbox" aria-label={t('plat3.tickAll')} className="h-5 w-5"
                        checked={shown.some((x) => !x.approvedAt && x.isActive) && shown.filter((x) => !x.approvedAt && x.isActive).every((x) => picked.includes(x.id))}
                        onChange={(e) => setPicked(e.target.checked ? shown.filter((x) => !x.approvedAt && x.isActive).map((x) => x.id) : [])} />}</th>
                      <th className="px-2 py-2.5">{t('plat3.shop')}</th>
                      <th className="px-2 py-2.5">{t('plat3.owner')}</th>
                      <th className="px-2 py-2.5">{t('plat3.plan')}</th>
                      <th className="px-2 py-2.5">{t('plat3.status')}</th>
                      <th className="px-3 py-2.5 text-right">{t('plat3.action')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {shown.map((s) => {
                      const waiting = s.isActive && !s.approvedAt
                      return (
                        <Fragment key={s.id}>
                          <tr className={`border-t border-line ${openId === s.id || picked.includes(s.id) ? 'bg-[#eef7f4]' : 'hover:bg-[#f6faf9]'}`}>
                            <td className="px-3 py-2.5">{canApprove && waiting && <input type="checkbox" aria-label={t('plat3.tick', { name: s.name })} checked={picked.includes(s.id)} className="h-5 w-5"
                              onChange={(e) => setPicked(e.target.checked ? [...picked, s.id] : picked.filter((x) => x !== s.id))} />}</td>
                            <td className="px-2 py-2.5">
                              <button onClick={() => setOpenId(openId === s.id ? null : s.id)} aria-expanded={openId === s.id} className="flex items-center gap-2.5 text-left">
                                <span aria-hidden className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-brand-soft text-xs font-extrabold text-brand">{s.name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0]!.toUpperCase()).join('')}</span>
                                <span className="flex min-w-0 flex-col">
                                  <span className="flex flex-wrap items-center gap-1.5"><b>{s.name}</b>{isTest(s) && <span className="rounded-full bg-[#edf2f1] px-2 text-[11px] font-bold text-muted">{t('plat3.test')}</span>}</span>
                                  <span className="text-xs text-muted">{[s.shopCode, s.area, formatDateTime(s.createdAt)].filter(Boolean).join(' · ')}</span>
                                </span>
                              </button>
                            </td>
                            <td className="px-2 py-2.5"><span className="flex flex-col"><span className="font-semibold">{s.ownerName}</span><span className="text-xs text-muted">{formatPhone(s.ownerPhone)}</span></span></td>
                            <td className="px-2 py-2.5">
                              <span className="flex flex-col">
                                <span className="font-semibold">{s.planCode ? (s.planStatus === 'trial' ? t('plan2.status.trial') : `${plans.find((p) => p.code === s.planCode)?.name ?? s.planCode}${s.planStatus === 'active' || s.planStatus === 'grace' ? ` ${t('billing.paidTag')}` : ''}`) : '—'}</span>
                                {s.planEndsAt && <span className="text-xs text-muted">{t('billing.until', { date: new Date(s.planEndsAt).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric' }) })}</span>}
                              </span>
                            </td>
                            <td className="px-2 py-2.5"><StatusChip shop={s} /></td>
                            <td className="px-3 py-2.5 text-right whitespace-nowrap">
                              {canApprove && waiting && <button disabled={bulkBusy} onClick={() => void approve([s.id])} className={`mr-1.5 min-h-9 rounded-xl px-3 text-sm ${btn.solid}`}>{t('platform.approve')}</button>}
                              <button onClick={() => setOpenId(openId === s.id ? null : s.id)} className={`min-h-9 rounded-xl px-3 text-sm ${btn.outline}`}>{openId === s.id ? t('plat3.close') : t('plat3.open')}</button>
                            </td>
                          </tr>
                          {openId === s.id && <tr><td colSpan={6} className="border-t border-line bg-white"><ShopDetail id={s.id} role={role} /></td></tr>}
                        </Fragment>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </>}
          {section === 'plans' && <><PlansPanel /><BillingPanel /></>}
          {section === 'markets' && <MarketsPanel />}
          {section === 'items' && <MasterItemsPanel />}
          {section === 'agents' && <>
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="mr-auto text-2xl font-extrabold">{t('platformNav.agents')}</h1>
              {(role === 'super' || role === 'agent_manager') && <button onClick={() => setAddAgent((n) => n + 1)} className={`min-h-11 rounded-xl px-4 ${btn.solid}`}>+ {t('agentsAdmin.addTitle')}</button>}
            </div>
            <AgentsSummary showMoney={role === 'super' || role === 'agent_manager' || role === 'billing'} />
            <div className="grid items-start gap-3 xl:grid-cols-2">
            {role !== 'support' && <RegistrationsPanel role={role} />}
            {(role === 'super' || role === 'agent_manager') && <AgentsPanel addRequest={addAgent} />}
            </div>
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
    ? [t('platform.chip.blocked'), 'bg-due-soft text-red-700']
    : !shop.approvedAt
      ? [t('platform.chip.new'), 'bg-amber-100 text-warn-ink']
      : [t('platform.chip.active'), 'bg-brand-soft text-brand-strong']
  return <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-bold ${style}`}>{label}</span>
}

/** Where the shop is and how to know it: photo, numbers, market and map. */
function ShopFacts({ id, d }: { id: string; d: Detail }) {
  const { t } = useTranslation()
  const photo = useQuery({
    queryKey: ['platform', 'shop-photo', id], enabled: !!d.hasPhoto, staleTime: Infinity,
    queryFn: async () => URL.createObjectURL(await (await request(`/v1/platform/shops/${id}/photo`)).blob()),
  }).data
  const facts = [
    d.shopNo && `${t('markets.shopNo')}: ${d.shopNo}`, d.gstin && `GSTIN ${d.gstin}`, d.market && `${t('markets.market')}: ${d.market}`, d.place, d.address,
  ].filter(Boolean)
  return (
    <div className="flex gap-3">
      {photo && <img src={photo} alt={t('markets.photo')} className="h-24 w-24 shrink-0 rounded-xl object-cover" />}
      <div className="flex min-w-0 flex-col gap-1 text-sm text-muted">
        {facts.map((x) => <span key={x as string}>{x}</span>)}
        {d.latitude != null && d.longitude != null && (
          <a href={`https://www.google.com/maps?q=${d.latitude},${d.longitude}`} target="_blank" rel="noreferrer" className="min-h-11 font-semibold">{t('markets.openMap')}</a>
        )}
      </div>
    </div>
  )
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

  async function block() {
    const reason = (await ask(t('platform.blockAsk', { name: d!.shop.name })))?.trim()
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
      {d.shop.blockedReason && <p className="rounded-xl bg-due-soft p-3 text-sm text-red-800">{t('platform.blockedBecause', { reason: d.shop.blockedReason })}</p>}
      {message && <p className="rounded-xl bg-brand-soft p-3 text-sm font-semibold text-brand-deep">{message}</p>}
      <dl className="grid grid-cols-2 gap-2 text-sm">
        {stats.map(([label, value]) => (
          <div key={label} className="rounded-xl bg-ground p-2"><dt className="text-xs text-muted">{label}</dt><dd className="font-bold">{value}</dd></div>
        ))}
      </dl>
      <ShopFacts id={id} d={d} />

      <h3 className="font-bold">{t('platform.people')}</h3>
      <ul className="flex flex-col gap-2">
        {d.people.map((p) => (
          <li key={p.membershipId} className="flex flex-wrap items-center gap-2 text-sm">
            <span className="min-w-0 flex-1"><b>{p.name}</b> · {p.role === 'owner' ? t('staff.owner') : t('staff.helper')}{!p.isActive && ` · ${t('staff.off')}`}<br /><span className="text-muted">{formatPhone(p.phone)}</span></span>
            <a href={`tel:${p.phone}`} className="flex min-h-11 items-center rounded-xl border border-line px-3 font-semibold no-underline">{t('platform.call')}</a>
            <a href={`https://wa.me/${p.phone.replace(/\D/g, '')}`} target="_blank" rel="noreferrer" className="flex min-h-11 items-center rounded-xl border border-line px-3 font-semibold no-underline">WhatsApp</a>
            {p.lockedOut && canSupport && <button disabled={busy} onClick={() => void run(`/v1/platform/users/${p.userId}/unlock`, {}, 'POST', t('platform.unlocked', { name: p.name }))} className="min-h-11 rounded-xl bg-amber-100 px-3 font-semibold text-warn-ink">{t('platform.unlock')}</button>}
            {canPeople && (
              <button disabled={busy} onClick={async () => (!p.isActive || (await confirmAsk(t('staff.offConfirm', { name: p.name })))) && void run(`/v1/platform/shops/${id}/people/${p.membershipId}/active?active=${!p.isActive}`, {}, 'POST')}
                className={`min-h-11 rounded-xl px-3 font-semibold ${p.isActive ? 'border border-line text-due' : 'bg-brand text-white'}`}>{p.isActive ? t('staff.turnOff') : t('staff.turnOn')}</button>
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
  const [open, setOpen] = useState(true)
  const [draft, setDraft] = useState(emptyMarket)
  const [editingId, setEditingId] = useState<string | null>(null)
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
    const body = { name: draft.name.trim(), state: draft.state.trim() || null, district: draft.district.trim() || null, city: draft.city.trim() || null, village: draft.village.trim() || null }
    const editing = markets.find((m) => m.id === editingId)
    if (editing) await api(`/v1/platform/markets/${editing.id}`, 'PUT', { ...body, isActive: editing.isActive })
    else await api('/v1/platform/markets', 'POST', body)
    setEditingId(null)
    setDraft({ ...emptyMarket, state: draft.state, district: draft.district })
  })
  // Editing fills the form above with the market, so its place can be changed too.
  const rename = (m: MarketRow) => {
    setEditingId(m.id)
    setDraft({ name: m.name, state: m.state ?? '', district: m.district ?? '', city: m.city ?? '', village: m.village ?? '' })
  }

  const input = 'h-12 min-w-0 rounded-xl border border-line bg-white px-3'
  return (
    <section className="flex flex-col gap-3 rounded-2xl bg-white p-4">
      <button onClick={() => setOpen(!open)} aria-expanded={open} className="flex min-h-11 items-center justify-between text-left">
        <h2 className="text-lg font-bold">{t('markets.title', { count: markets.length })}</h2>
        <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={`shrink-0 text-muted transition ${open ? 'rotate-180' : ''}`}><path d="M6 9l6 6 6-6" /></svg>
      </button>
      {open && <>
        <div className="grid grid-cols-2 gap-2">
          <input aria-label={t('markets.name')} placeholder={t('markets.name')} value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} className={`${input} col-span-2`} />
          {(['state', 'district', 'city', 'village'] as const).map((key) => (
            <input key={key} aria-label={t(`markets.${key}`)} placeholder={t(`markets.${key}`)} value={draft[key]} onChange={(e) => setDraft({ ...draft, [key]: e.target.value })} className={input} />
          ))}
        </div>
        <Button disabled={draft.name.trim().length < 2} onClick={() => void add()}>{editingId ? t('save') : t('markets.add')}</Button>
        {editingId && <button onClick={() => { setEditingId(null); setDraft(emptyMarket) }} className="min-h-11 font-semibold text-brand">{t('cancel')}</button>}
        {message && <p className="text-sm text-due">{message}</p>}
        <ul className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
          {markets.map((m, i) => {
            const tone = [['bg-[#e3f2ee]', 'text-[#0b5e4f]'], ['bg-[#e8effd]', 'text-[#1d4ed8]'], ['bg-[#fdf1e3]', 'text-[#a1460a]'], ['bg-[#f1eafd]', 'text-[#6d28d9]']][i % 4]
            return (
              <li key={m.id} className={`flex flex-col gap-3 rounded-[18px] border border-line bg-white p-4 shadow-sm ${m.isActive ? '' : 'opacity-60'}`}>
                <span className="flex items-center gap-3">
                  <span aria-hidden className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full ${tone[0]} ${tone[1]}`}><svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M12 21s7-6 7-11a7 7 0 10-14 0c0 5 7 11 7 11zM12 8a2 2 0 100 4 2 2 0 000-4" /></svg></span>
                  <span className="flex min-w-0 flex-1 flex-col"><b className="truncate text-[17px]">{m.name}</b>{marketPlace(m) && <span className="truncate text-xs text-muted">{marketPlace(m)}</span>}</span>
                </span>
                <span className="flex items-baseline gap-1.5"><b className="text-2xl">{m.shops}</b><span className="text-sm text-muted">{t('plat4.shopsWord', { count: m.shops })}</span></span>
                <span className="mt-auto grid grid-cols-2 gap-2">
                  <button onClick={() => rename(m)} className="min-h-10 rounded-xl border border-line font-semibold">{t('markets.edit')}</button>
                  <button onClick={() => void run(() => api(`/v1/platform/markets/${m.id}`, 'PUT', { ...m, isActive: !m.isActive }))} className={`min-h-10 ${m.isActive ? btn.dangerOutline : `rounded-xl ${btn.solid}`}`}>{m.isActive ? t('markets.stop') : t('markets.restart')}</button>
                </span>
              </li>
            )
          })}
        </ul>
      </>}
    </section>
  )
}

interface MasterRow { id: number; trade: string; category: string; name: string; nameHi: string | null; unit: string; gstRate: number | null; hsnCode: string | null; isActive: boolean; packSizes: string[]; imageAt?: string | null; imageCredit?: string | null }

interface SuggestionRow { id: number; shopName: string; trade: string; category: string; name: string; unit: string; note: string | null }

/** Items shops asked for: approve (it joins that trade's master list) or turn down. */
function SuggestionsQueue({ onDone }: { onDone: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const rows = useQuery({ queryKey: ['platform', 'suggestions'], queryFn: () => api<SuggestionRow[]>('/v1/platform/master-items/suggestions') }).data ?? []
  const [message, setMessage] = useState<string | null>(null)
  const decide = async (r: SuggestionRow, approve: boolean) => {
    setMessage(null)
    try {
      await (approve
        ? api(`/v1/platform/master-items/suggestions/${r.id}/approve`, 'POST', { trade: r.trade, name: r.name, category: r.category, unit: r.unit })
        : api(`/v1/platform/master-items/suggestions/${r.id}/reject`, 'POST', {}))
      void queryClient.invalidateQueries({ queryKey: ['platform', 'suggestions'] })
      onDone()
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  if (rows.length === 0) return null
  return (
    <div className="rounded-xl bg-warn-soft p-3">
      <h3 className="font-bold">{t('masterItems.suggestions', { count: rows.length })}</h3>
      {message && <p className="text-sm text-due">{message}</p>}
      <ul className="mt-2 flex flex-col gap-1">
        {rows.map((r) => (
          <li key={r.id} className="flex items-center gap-2 rounded-xl bg-white px-2">
            <span className="min-w-0 flex-1">{r.name}<span className="block text-xs text-muted">{t(`shopType.${r.trade}`)} · {r.category} · {t(`units.${r.unit}`)} · {r.shopName}{r.note ? ` · ${r.note}` : ''}</span></span>
            <button onClick={() => void decide(r, true)} className="min-h-11 px-2 font-semibold text-brand">{t('masterItems.approve')}</button>
            <button onClick={() => void decide(r, false)} className="min-h-11 px-2 font-semibold">{t('masterItems.reject')}</button>
          </li>
        ))}
      </ul>
    </div>
  )
}

/** The master item lists every shop picks from, one per trade: add, edit, or turn an item off (never deleted). */
function MasterItemsPanel() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [trade, setTrade] = useState<ShopType>('kirana')
  const [q, setQ] = useState('')
  const empty = { name: '', nameHi: '', category: '', unit: 'piece', gst: '', hsn: '', packs: '' }
  const [draft, setDraft] = useState(empty)
  const [importing, setImporting] = useState(false)
  const [unitFilter, setUnitFilter] = useState('')
  const [gstFilter, setGstFilter] = useState('')
  const [noPhotoOnly, setNoPhotoOnly] = useState(false)
  const [adding, setAdding] = useState(false)
  const [editing, setEditing] = useState<number | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  // Credit line for photos uploaded next (empty for the platform's own photos); browsers like some in-app ones block pop-up prompts.
  const [photoCredit, setPhotoCredit] = useState('')
  const rows = useQuery({ queryKey: ['platform', 'master', trade, q], queryFn: () => api<MasterRow[]>(`/v1/platform/master-items?trade=${trade}&q=${encodeURIComponent(q.trim())}`) }).data ?? []
  const body = (d: typeof empty, isActive?: boolean) => ({ trade, name: d.name.trim(), nameHi: d.nameHi.trim() || null, category: d.category.trim(), unit: d.unit, gstRate: d.gst === '' ? null : Number(d.gst), hsnCode: d.hsn.trim() || null, isActive, packSizes: d.packs.split(',').map((s) => s.trim()).filter(Boolean) })

  async function run(action: () => Promise<unknown>) {
    setMessage(null)
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: ['platform', 'master'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    }
  }

  const save = () => run(async () => {
    if (editing) await api(`/v1/platform/master-items/${editing}`, 'PUT', body(draft))
    else await api('/v1/platform/master-items', 'POST', body(draft))
    setDraft({ ...empty, category: draft.category, unit: draft.unit })
    setEditing(null)
  })
  const view = rows.filter((r) => (!unitFilter || r.unit === unitFilter)
    && (!gstFilter || (gstFilter === 'none' ? r.gstRate == null : r.gstRate === Number(gstFilter)))
    && (!noPhotoOnly || !r.imageAt))
  const [csv, setCsv] = useState('')
  const importCsv = () => run(async () => {
    const r = await api<{ added: number; updated: number; problems: string[] }>('/v1/platform/master-items/import', 'POST', { trade, text: csv })
    setCsv('')
    setMessage([t('masterItems.imported', r), ...r.problems].join('\n'))
  })
  const edit = (r: MasterRow) => { setEditing(r.id); setDraft({ name: r.name, nameHi: r.nameHi ?? '', category: r.category, unit: r.unit, gst: r.gstRate == null ? '' : String(r.gstRate), hsn: r.hsnCode ?? '', packs: r.packSizes.join(', ') }) }

  const input = 'h-11 min-w-0 rounded-xl border border-line bg-white px-3'
  return (
    <section className="flex flex-col gap-3 rounded-2xl bg-white p-4">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="mr-auto text-lg font-bold">{t('masterItems.title')} · {t('masterItems.count', { count: rows.length })}</h2>
        <button onClick={() => setImporting(!importing)} aria-expanded={importing} className={`flex min-h-11 items-center gap-2 rounded-xl px-4 ${btn.outline}`}><svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M12 15V3M7 8l5-5 5 5M4 15v5h16v-5" /></svg>{t('masterItems.import')}</button>
        <button onClick={() => { setAdding(!adding || editing !== null); setEditing(null); setDraft(empty) }} className={`min-h-11 rounded-xl px-4 ${adding && editing === null ? btn.outline : btn.solid}`}>{adding && editing === null ? t('cancel') : `+ ${t('masterItems.add')}`}</button>
      </div>
      {/* What needs work in this trade's list, at a glance. */}
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        <div className="flex min-h-[76px] flex-col justify-between rounded-[18px] bg-[#e3f2ee] p-3 text-[#0b5e4f]"><span className="text-[13px] font-bold">{t('plat5.items')}</span><b className="text-2xl">{rows.length}</b></div>
        <div className="flex min-h-[76px] flex-col justify-between rounded-[18px] bg-[#fdf1e3] p-3 text-[#a1460a]"><span className="text-[13px] font-bold">{t('plat5.noPhoto')}</span><b className="text-2xl">{rows.filter((r) => !r.imageAt).length}</b></div>
        <div className="flex min-h-[76px] flex-col justify-between rounded-[18px] bg-[#e8effd] p-3 text-[#1d4ed8]"><span className="text-[13px] font-bold">{t('plat5.noGst')}</span><b className="text-2xl">{rows.filter((r) => r.gstRate == null || !r.hsnCode).length}</b></div>
        <div className="flex min-h-[76px] flex-col justify-between rounded-[18px] bg-[#edf2f1] p-3 text-[#33423d]"><span className="text-[13px] font-bold">{t('plat5.off')}</span><b className="text-2xl">{rows.filter((r) => !r.isActive).length}</b></div>
      </div>
      <div className="-mx-1 flex gap-2 overflow-x-auto px-1 pb-1 [scrollbar-width:none]">
        {SHOP_TYPES.filter((x) => x !== 'other').map((x) => (
          <button key={x} onClick={() => { setTrade(x); setEditing(null) }} aria-pressed={trade === x}
            className={`min-h-10 shrink-0 rounded-full px-4 text-sm font-semibold whitespace-nowrap transition ${trade === x ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'border border-line bg-white'}`}>{t(`shopType.${x}`)}</button>
        ))}
      </div>
      <span className="relative block">
        <span aria-hidden className="pointer-events-none absolute top-1/2 left-3.5 -translate-y-1/2 text-muted"><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M11 4a7 7 0 100 14 7 7 0 000-14zM20 20l-3.5-3.5" /></svg></span>
        <input aria-label={t('masterItems.search')} placeholder={t('masterItems.search')} value={q} onChange={(e) => setQ(e.target.value)} className={`${input} w-full bg-[#f6faf9] pl-10`} />
      </span>
      <div className="flex flex-wrap items-end gap-3">
        <label className="flex flex-col gap-1 text-[11px] font-bold tracking-wide text-muted uppercase">{t('masterItems.unit')}
          <select value={unitFilter} onChange={(e) => setUnitFilter(e.target.value)} className="h-10 rounded-xl border border-line bg-white px-2 text-sm font-medium normal-case text-ink">
            <option value="">{t('plat6.all')}</option>{[...new Set(rows.map((r) => r.unit))].map((u) => <option key={u} value={u}>{t(`units.${u}`)}</option>)}
          </select></label>
        <label className="flex flex-col gap-1 text-[11px] font-bold tracking-wide text-muted uppercase">GST
          <select value={gstFilter} onChange={(e) => setGstFilter(e.target.value)} className="h-10 rounded-xl border border-line bg-white px-2 text-sm font-medium normal-case text-ink">
            <option value="">{t('plat6.all')}</option><option value="none">{t('plat5.gstNotSet')}</option>{['0', '5', '18', '40'].map((g) => <option key={g} value={g}>{g}%</option>)}
          </select></label>
        <label className="flex min-h-10 items-center gap-2 text-sm font-semibold"><input type="checkbox" checked={noPhotoOnly} onChange={(e) => setNoPhotoOnly(e.target.checked)} className="h-5 w-5" />{t('plat6.noPhotoOnly')}</label>
        {(unitFilter || gstFilter || noPhotoOnly) && <span className="text-sm text-muted">{t('plat6.showing', { shown: view.length, total: rows.length })}</span>}
      </div>
      {(adding || editing !== null) && <div className="grid grid-cols-2 gap-2 rounded-xl bg-ground p-2 ring-2 ring-brand/30">
        <input aria-label={t('masterItems.name')} placeholder={t('masterItems.name')} value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} className={input} />
        <input aria-label={t('masterItems.nameHi')} placeholder={t('masterItems.nameHi')} value={draft.nameHi} onChange={(e) => setDraft({ ...draft, nameHi: e.target.value })} className={input} />
        <input aria-label={t('masterItems.category')} placeholder={t('masterItems.category')} value={draft.category} onChange={(e) => setDraft({ ...draft, category: e.target.value })} className={input} />
        <select aria-label={t('masterItems.unit')} value={draft.unit} onChange={(e) => setDraft({ ...draft, unit: e.target.value })} className={input}>
          {['piece', 'kg', 'g', 'litre', 'ml', 'packet', 'pair', 'dozen', 'box', 'jar', 'bag', 'tonne', 'brass', 'cft', 'metre', 'sq_ft', 'running_ft', 'quintal', 'carton', 'bottle', 'set', 'strip'].map((u) => <option key={u} value={u}>{t(`units.${u}`)}</option>)}
        </select>
        <select aria-label={t('masterItems.gst')} value={draft.gst} onChange={(e) => setDraft({ ...draft, gst: e.target.value })} className={input}>
          <option value="">{t('masterItems.gst')}: –</option>{['0', '5', '18', '40'].map((r) => <option key={r} value={r}>{r}%</option>)}
        </select>
        <input aria-label={t('masterItems.hsn')} placeholder={t('masterItems.hsn')} value={draft.hsn} onChange={(e) => setDraft({ ...draft, hsn: e.target.value })} inputMode="numeric" maxLength={8} className={input} />
        <input aria-label={t('masterItems.packs')} placeholder={t('masterItems.packs')} value={draft.packs} onChange={(e) => setDraft({ ...draft, packs: e.target.value })} className={`${input} col-span-2`} />
        <Button className="col-span-2" disabled={draft.name.trim().length < 2 || draft.category.trim().length < 2} onClick={() => void save()}>{editing ? t('save') : t('masterItems.add')}</Button>
      </div>}
      <SuggestionsQueue onDone={() => void queryClient.invalidateQueries({ queryKey: ['platform', 'master'] })} />
      {importing && <div className="flex flex-col gap-2 rounded-xl bg-ground p-3 ring-2 ring-brand/30">
        <b>{t('masterItems.import')}</b>
        <p className="text-xs text-muted">{t('masterItems.importHint')}</p>
        <textarea value={csv} onChange={(e) => setCsv(e.target.value)} rows={6} placeholder={'Toor dal, तूर दाल, Dal, kg, 5, 0713, 500 g|1 kg'} className="mt-2 w-full rounded-xl border border-line p-2 font-mono text-sm" />
        <Button disabled={!csv.trim()} onClick={() => void importCsv()}>{t('masterItems.importButton')}</Button>
      </div>}
      {message && <p className="whitespace-pre-line text-sm text-due">{message}</p>}
      <label className="flex flex-col gap-1 text-xs text-muted">{t('masterItems.creditLabel')}
        <input value={photoCredit} onChange={(e) => setPhotoCredit(e.target.value)} maxLength={120} placeholder="Unsplash: name" className="h-10 rounded-xl border border-line bg-white px-3 text-sm text-ink" />
      </label>
      <div className="hidden max-h-[40rem] overflow-auto rounded-xl border border-line lg:block">
        <table className="w-full min-w-[860px] border-collapse text-sm">
          <thead className="sticky top-0 bg-[#f6faf9] text-left text-[11px] tracking-wide text-muted uppercase">
            <tr><th className="w-16 px-3 py-2.5">{t('plat5.photo')}</th><th className="px-2 py-2.5">{t('plat5.item')}</th><th className="px-2 py-2.5">{t('masterItems.category')}</th><th className="px-2 py-2.5">{t('masterItems.unit')}</th><th className="px-2 py-2.5">{t('plat5.gstHsn')}</th><th className="px-2 py-2.5">{t('plat5.sizes')}</th><th className="px-3 py-2.5 text-right" /></tr>
          </thead>
          <tbody>
            {view.map((r) => (
              <tr key={r.id} className={`border-t border-line ${r.isActive ? '' : 'opacity-50'} ${editing === r.id ? 'bg-[#eef7f4]' : 'hover:bg-[#f6faf9]'}`}>
                <td className="px-3 py-1.5"><MasterPhoto row={r} run={run} credit={photoCredit} /></td>
                <td className="px-2 py-1.5"><span className="flex flex-col"><b>{r.name}</b>{r.nameHi && <span className="text-xs text-muted">{r.nameHi}</span>}</span></td>
                <td className="px-2 py-1.5"><span className="rounded-full bg-[#edf2f1] px-2.5 py-0.5 text-xs font-semibold">{r.category}</span></td>
                <td className="px-2 py-1.5">{t(`units.${r.unit}`)}</td>
                <td className="px-2 py-1.5">{r.gstRate == null
                  ? <span className="rounded-full bg-warn-soft px-2.5 py-0.5 text-xs font-bold text-warn-ink">{t('plat5.gstNotSet')}{r.hsnCode ? ` · ${r.hsnCode}` : ''}</span>
                  : <span>{r.gstRate}%{r.hsnCode ? ` · ${r.hsnCode}` : ''}</span>}</td>
                <td className="px-2 py-1.5 text-muted">{r.packSizes.join(' / ') || '—'}</td>
                <td className="px-3 py-1.5 text-right whitespace-nowrap">
                  <button onClick={() => edit(r)} className="mr-1.5 min-h-9 rounded-xl border border-line px-3 text-sm font-semibold">{t('masterItems.edit')}</button>
                  <button onClick={() => void run(() => api(`/v1/platform/master-items/${r.id}`, 'PUT', { ...body({ name: r.name, nameHi: r.nameHi ?? '', category: r.category, unit: r.unit, gst: r.gstRate == null ? '' : String(r.gstRate), hsn: r.hsnCode ?? '', packs: r.packSizes.join(', ') }), isActive: !r.isActive }))}
                    className={`min-h-9 text-sm ${r.isActive ? btn.dangerOutline : `rounded-xl px-3 ${btn.solid}`}`}>{r.isActive ? t('masterItems.off') : t('masterItems.on')}</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <ul className="flex max-h-[32rem] flex-col overflow-y-auto lg:hidden">
        {view.map((r) => (
          <li key={r.id} className={`flex items-center gap-2 border-t border-line py-1 text-sm ${r.isActive ? '' : 'opacity-50'}`}>
            <MasterPhoto row={r} run={run} credit={photoCredit} />
            <span className="min-w-0 flex-1 truncate">{r.name}{r.nameHi ? ` · ${r.nameHi}` : ''}<span className="block text-xs text-muted">{r.category} · {t(`units.${r.unit}`)}{r.gstRate != null ? ` · ${r.gstRate}%` : ''}{r.hsnCode ? ` · ${r.hsnCode}` : ''}{r.packSizes.length ? ` · ${r.packSizes.join(' / ')}` : ''}</span></span>
            <button onClick={() => edit(r)} className="min-h-11 px-2 font-semibold text-brand">{t('masterItems.edit')}</button>
            <button onClick={() => void run(() => api(`/v1/platform/master-items/${r.id}`, 'PUT', { ...body({ name: r.name, nameHi: r.nameHi ?? '', category: r.category, unit: r.unit, gst: r.gstRate == null ? '' : String(r.gstRate), hsn: r.hsnCode ?? '', packs: r.packSizes.join(', ') }), isActive: !r.isActive }))}
              className="min-h-11 px-2 font-semibold">{r.isActive ? t('masterItems.off') : t('masterItems.on')}</button>
          </li>
        ))}
      </ul>
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
  const [addItems, setAddItems] = useState(true)
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
        gstin: ids.gstin.trim() || null, shopNo: ids.shopNo.trim() || null, addTradeItems: addItems,
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
    <section className={`flex flex-col gap-3 ${open ? 'rounded-2xl bg-white p-4 shadow-sm' : ''}`}>
      <button onClick={() => setOpen(!open)} aria-expanded={open} className={`flex min-h-11 items-center gap-2 self-start rounded-xl px-4 ${open ? btn.outline : btn.solid}`}>
        <span aria-hidden className="text-xl leading-none">{open ? '×' : '+'}</span>{t('openShop.title')}
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
        <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={addItems} onChange={(e) => setAddItems(e.target.checked)} className="h-5 w-5" />{t('masterItems.addToShop')}</label>
        <PeopleRows people={people} onChange={setPeople} />
        <Button disabled={busy || form.shopName.trim().length < 2 || form.phone.trim().length < 10} onClick={() => void create()}>{t('openShop.create')}</Button>
        {created && (
          <div className="flex flex-col gap-1 rounded-xl bg-brand-soft p-3 text-sm text-brand-deep">
            <span className="font-semibold">{t('openShop.done', { name: created.name })}{created.code && <span className="ml-2 font-mono">{created.code}</span>}</span>
            {created.password
              ? <span>{t('openShop.login', { phone: created.phone })} <span className="font-mono text-base font-bold">{created.password}</span><br />{t('openShop.once')}</span>
              : <span>{t('openShop.existing')}</span>}
          </div>
        )}
        {message && <p className="rounded-xl bg-due-soft p-3 text-sm font-semibold text-red-800">{message}</p>}
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
  const [open, setOpen] = useState(true)
  const [giving, setGiving] = useState(false)

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
        <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className={`shrink-0 text-muted transition ${open ? 'rotate-180' : ''}`}><path d="M6 9l6 6 6-6" /></svg>
      </button>
      {open && <>
      {/* What each role may do, as coloured cards, so choosing one below needs no guessing. */}
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 xl:grid-cols-5">
        {ROLES.map((r, i) => (
          <div key={r} className={`flex min-h-[76px] flex-col gap-1 rounded-2xl p-3 ${['bg-[#13241f] text-white', 'bg-[#e3f2ee] text-[#0b5e4f]', 'bg-[#e8effd] text-[#1d4ed8]', 'bg-[#fdf1e3] text-[#a1460a]', 'bg-[#f1eafd] text-[#6d28d9]'][i % 5]}`}>
            <b className="text-sm">{t(`admins.roles.${r}`)}</b><span className="text-xs leading-snug opacity-90">{t(`admins.can.${r}`)}</span>
          </div>
        ))}
      </div>
      <p className="text-sm text-muted">{t('admins.hint')}</p>
      <ul className="flex flex-col divide-y divide-[#edf2f1]">
        {admins.data?.map((a) => (
          <li key={a.userId} className="flex flex-wrap items-center gap-3 py-3">
            <span aria-hidden className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-brand-soft font-extrabold text-brand">{a.name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0]!.toUpperCase()).join('')}</span>
            <span className="flex min-w-0 flex-1 basis-48 flex-col">
              <span className="font-semibold">{a.name}{a.userId === me ? ` (${t('admins.you')})` : ''}</span>
              <span className="text-xs text-muted">{formatPhone(a.phone)}{a.addedBy ? ` · ${t('admins.addedBy', { name: a.addedBy })}` : ''}</span>
            </span>
            <div role="radiogroup" aria-label={t('admins.role')} className="flex flex-wrap gap-1 rounded-xl bg-ground p-1">
              {ROLES.map((r) => (
                <button key={r} role="radio" aria-checked={a.role === r} disabled={busy || a.role === r}
                  onClick={() => void run(`/v1/platform/admins/${a.userId}`, 'PUT', { role: r }, t('admins.changed', { name: a.name }))}
                  className={`min-h-9 rounded-lg px-2.5 text-xs font-semibold transition ${a.role === r ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted hover:text-ink'}`}>{t(`admins.roles.${r}`)}</button>
              ))}
            </div>
            {a.userId !== me && <button disabled={busy} aria-label={t('admins.remove', { name: a.name })}
              onClick={async () => (await confirmAsk(t('admins.removeAsk', { name: a.name }))) && void run(`/v1/platform/admins/${a.userId}`, 'DELETE', undefined, t('admins.removed', { name: a.name }))}
              className={`h-9 text-sm ${btn.dangerOutline}`}>{t('admins.removeShort')}</button>}
          </li>
        ))}
      </ul>
      {!giving && <button onClick={() => setGiving(true)} className={`min-h-11 self-start rounded-xl px-4 ${btn.solid}`}>+ {t('admins.addTitle')}</button>}
      {giving && <div className="flex flex-col gap-2 rounded-xl bg-ground p-3 ring-2 ring-brand/30">
        <span className="flex items-center justify-between font-semibold">{t('admins.addTitle')}<button onClick={() => setGiving(false)} className={`min-h-9 text-sm ${btn.link}`}>{t('cancel')}</button></span>
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
      </div>}
      {message && <p className="rounded-xl bg-brand-soft p-3 text-sm font-semibold text-brand-deep">{message}</p>}
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
        className={`min-h-11 self-start ${btn.add}`}>+ {t('people.addMore')}</button>
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
      {message && <p className="rounded-xl bg-brand-soft p-3 text-sm font-semibold text-brand-deep">{message}</p>}
    </div>
  )
}

/**
 * A master item's photo: every shop item picked from it shows this until the shop adds its own. Only photos the
 * platform owns or is licensed for go here (own shoot, free stock, credited CC); the credit shows under it in shops.
 */
function MasterPhoto({ row, run, credit }: { row: MasterRow; run: (action: () => Promise<unknown>) => Promise<void>; credit: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
  // The photo just sent, shown at once: the browser can hold back the first load of the server's new copy for a long time.
  const [preview, setPreview] = useState<string | null>(null)
  const url = preview ?? (row.imageAt ? `/api/v1/master-images/${row.id}?v=${Date.parse(row.imageAt)}&size=thumb` : null)
  // Kept under the server's 1 MB request limit once sent (photos grow by a third as text): a 1200 px WebP of a pack is a few hundred KB.
  async function upload(file: File) {
    setBusy(true)
    setError(null)
    setSaved(false)
    try {
      // Step times go to the console, so a slow upload shows which step waited (reading the file, or the server).
      const started = performance.now()
      const dataUrl = await shrinkPhoto(file, 1200, 600_000, false)
      const shrunk = performance.now()
      await api(`/v1/platform/master-items/${row.id}/image`, 'PUT', { dataUrl, credit: credit.trim() || null })
      console.info(`[master photo] read+shrink ${Math.round(shrunk - started)} ms, upload ${Math.round(performance.now() - shrunk)} ms, ${file.size} bytes in, ${Math.round(dataUrl.length * 0.75)} bytes sent`)
      setPreview(dataUrl)
      setSaved(true)
      setTimeout(() => setSaved(false), 4000)
      // Refresh the list in the background so the new photo shows; a failed refresh is not a failed upload.
      void queryClient.invalidateQueries({ queryKey: ['platform', 'master'] })
    } catch (e) {
      setError(t('masterItems.photoFailed', { reason: e instanceof Error ? e.message : String(e) }))
    } finally {
      setBusy(false)
    }
  }
  return (
    <span className="flex w-14 shrink-0 flex-col items-center">
      <label className={`flex h-12 w-12 cursor-pointer items-center justify-center overflow-hidden rounded-lg border border-line bg-white text-lg ${busy ? 'opacity-50' : ''}`} title={row.imageCredit ?? t('masterItems.photo')}>
        {busy ? '…' : url ? <img src={url} alt="" className="h-full w-full object-contain" /> : '📷'}
        <input type="file" accept="image/*" className="sr-only" aria-label={t('masterItems.photo')} disabled={busy}
          onChange={(e) => { const file = e.target.files?.[0]; if (file) void upload(file); e.target.value = '' }} />
      </label>
      {url && !busy && <button onClick={() => { setPreview(null); void run(() => request(`/v1/platform/master-items/${row.id}/image`, 'DELETE')) }} className="text-[10px] text-muted underline">{t('masterItems.removePhoto')}</button>}
      {saved && <span role="status" className="mt-0.5 text-center text-[11px] font-semibold leading-tight text-brand">✓ {t('masterItems.photoSaved')}</span>}
      {error && <span role="alert" className="mt-0.5 text-center text-[11px] font-semibold leading-tight text-due">{error}</span>}
    </span>
  )
}

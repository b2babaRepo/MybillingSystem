import { useQuery, useQueryClient } from '@tanstack/react-query'
import { lazy, Suspense, useState, Fragment } from 'react'
import { useTranslation } from 'react-i18next'
import { BackIcon } from '../components/Icons'
import { Button, btn } from '../components/ui'
import { api, request } from '../lib/api'
import { formatDateTime, formatPhone } from '../lib/format'
import { useOutbox, useSession } from '../lib/data'
import { ActivityPanel, AgentPanel, BillingPanel, CustomerUsagePanel, PlansPanel, ShopBillingPanel } from './PlatformBilling'
import { usePlans } from '../lib/billing'
import { AgentsPanel, AgentsSummary, PerformancePanel, RegistrationsPanel } from './PlatformAgents'
import { addText, shownLocale } from '../i18n'
import { adminText } from '../i18n/admin'
import { ask, confirmAsk } from '../components/Ask'

addText(adminText)
import type { AdminRole } from '../lib/types'
import { navigate } from '../router'
import { getSession, setSession } from '../lib/session'
import { MasterItemsPanel } from './PlatformMasterItems'
import { AddPerson, NewShop } from './PlatformNewShop'
import { marketPlace, useMarkets, type MarketRow } from '../lib/markets'

const AccountMenu = lazy(() => import('../components/AccountMenu').then((module) => ({ default: module.AccountMenu })))

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
          {section === 'ai' && <><AgentPanel role={role} /><CustomerUsagePanel /></>}
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

import { btn } from '../components/ui'
import { shownLocale } from '../i18n'
import { lazy, Suspense } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
// Below the fold and owner-only: kept out of the first load.
const OrderToday = lazy(() => import('../components/OrderToday').then((m) => ({ default: m.OrderToday })))
const FestivalCard = lazy(() => import('./FestivalsScreen').then((m) => ({ default: m.FestivalCard })))
const TodaySummary = lazy(() => import('../components/TodaySummary').then((m) => ({ default: m.TodaySummary })))
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { BillIcon, CustomersIcon, PaymentIcon, PlusIcon } from '../components/Icons'
import { api, request } from '../lib/api'
import { useProducts, useToday } from '../lib/data'
import { formatRupees } from '../lib/format'
import { useSubscription } from '../lib/billing'
import { useShopSettings, shopTypes, isTrade, COMMISSION_TYPES } from '../lib/reports'
import { navigate } from '../router'
// Last week's summary, on Mondays and Tuesdays only: loaded then, not with the app.
const WeekSummaryCard = lazy(() => import('./SalesInsightsScreens').then((module) => ({ default: module.WeekSummaryCard })))
// Each trade's card comes with its whole screen, so only the shops of that trade fetch it.
const SalonHomeCard = lazy(() => import('./SalonSetupScreen').then((module) => ({ default: module.SalonHomeCard })))
const MedicalHomeCard = lazy(() => import('./MedicalScreen').then((module) => ({ default: module.MedicalHomeCard })))
const DairyHomeCard = lazy(() => import('./DairyScreen').then((module) => ({ default: module.DairyHomeCard })))
const ServiceHomeCard = lazy(() => import('./SerialsScreen').then((module) => ({ default: module.ServiceHomeCard })))

/**
 * Dashboard A, "Clean Teal": today's sales with the cash / UPI / udhaar split, four big actions,
 * and who owes the most. The teal band continues the app header; the summary card overlaps it.
 */
/** The owner's to-do for today: customers to chase for old dues, and what suppliers are owed. Hidden when nothing is due. */
function Digest() {
  const { t } = useTranslation()
  const d = useQuery({ queryKey: ['digest'], queryFn: () => api<{ oldDueCustomers: number; oldDueAmount: number; supplierDue: number }>('/v1/digest'), retry: false, staleTime: 300_000 }).data
  if (!d || (d.oldDueCustomers === 0 && d.supplierDue <= 0)) return null
  return (
    <section className="mx-4 mt-4 flex flex-col gap-1 rounded-card bg-white p-4 shadow-sm ring-1 ring-amber-100">
      <h2 className="flex items-center gap-2 font-bold"><span aria-hidden className="flex h-8 w-8 items-center justify-center rounded-full bg-warn-soft text-warn"><svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M6 16V11a6 6 0 0112 0v5l2 2H4zM10 21h4" /></svg></span>{t('digest.title')}</h2>
      {d.oldDueCustomers > 0 && (
        <button onClick={() => navigate('/dues')} className="flex min-h-11 items-center justify-between text-left">
          <span>{t('digest.oldDues', { count: d.oldDueCustomers })}</span><span className="font-bold text-due">{formatRupees(d.oldDueAmount)}</span>
        </button>
      )}
      {d.supplierDue > 0 && (
        <button onClick={() => navigate('/suppliers')} className="flex min-h-11 items-center justify-between text-left">
          <span>{t('digest.suppliers')}</span><span className="font-bold text-due">{formatRupees(d.supplierDue)}</span>
        </button>
      )}
    </section>
  )
}

export function HomeScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const settings = useShopSettings(shopId).data
  const types = shopTypes(settings)
  const { t } = useTranslation()
  const { today } = useToday(shopId)
  const { products } = useProducts(shopId)
  const masterUpdates = useQuery({ queryKey: [shopId, 'master-updates'], queryFn: () => api<{ changes: unknown[] }>('/v1/master-items/updates'), enabled: isOwner, retry: false, staleTime: 600_000 }).data
  const lowCount = products.filter((p) => p.isLow).length
  // Same key as the backup screen, so taking a backup there clears this at once.
  const backup = useQuery({
    queryKey: [shopId, 'backup-status'], queryFn: () => api<{ lastBackupAt: string | null; empty: boolean }>('/v1/shop/backup-status'), enabled: isOwner, retry: false, staleTime: 60_000,
    select: (s) => ({ ...s, days: s.lastBackupAt ? Math.floor((Date.now() - new Date(s.lastBackupAt).getTime()) / 86_400_000) : null }),
  }).data
  const backupDays = backup?.days ?? null

  return (
    <div className="pb-4">
      {/* On a phone the sales card rises into the bar; on a large screen it sits below it. */}
      <div className="h-14 bg-brand lg:hidden" />
      <section className="mx-4 -mt-12 lg:mt-4 flex flex-col gap-3.5 rounded-[20px] bg-white p-[18px] shadow-[0_6px_20px_rgba(16,32,29,0.08)]">
        <div className="flex items-baseline justify-between">
          <span className="text-[15px] font-semibold text-muted">{t('todaySales')}</span>
          <button onClick={() => navigate('/reports/day')} className="min-h-9 text-[13px] font-semibold text-brand underline">{today ? t('billsCount', { count: today.bills }) : ' '} ›</button>
        </div>
        <div className="text-[40px] leading-none font-extrabold">{today ? formatRupees(today.sales) : '…'}</div>
        <div className="grid grid-cols-3 gap-2">
          <Split label={t('cash')} amount={today?.cash} className="bg-emerald-50 text-emerald-800" />
          <Split label={t('upi')} amount={today?.upi} className="bg-blue-50 text-blue-800" />
          <Split label={t('creditShort')} amount={today?.udhaar} className="bg-orange-50 text-orange-800" />
        </div>
      </section>

      <section className="mx-4 mt-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Action label={t('newBill')} to="/bill" tone="bg-[#0b5e4f] text-white shadow-lg shadow-[#0b5e4f]/30 hover:bg-[#084a3e]" dot="bg-white/20" icon={<BillIcon size={26} />} />
        <Action label={t('addUdhaar')} to="/khata" tone="bg-[#fdf1e3] text-[#a1460a] hover:bg-[#fbe6cc]" icon={<PlusIcon size={26} />} />
        <Action label={t('receivePayment')} to="/khata" tone="bg-[#e3f2ee] text-[#0b5e4f] hover:bg-[#d2ebe4]" icon={<PaymentIcon size={26} />} />
        <Action label={t('customers')} to="/khata" tone="bg-[#e8effd] text-[#1d4ed8] hover:bg-[#dbe5fc]" icon={<CustomersIcon size={26} />} />
      </section>

      {isOwner && <PlanBanner shopId={shopId} />}
      {isOwner && <BalanceAlert />}
      {isOwner && <Suspense fallback={null}><TodaySummary today={today} shopName={settings?.name ?? ''} lowCount={lowCount} /></Suspense>}
      {isOwner && <Suspense fallback={null}><FestivalCard shopId={shopId} /></Suspense>}
      {isOwner && <Suspense fallback={null}><OrderToday /></Suspense>}
      {isOwner && <Digest />}
      {isOwner && [1, 2].includes(new Date(Date.now() + 5.5 * 3_600_000).getUTCDay()) && <Suspense fallback={null}><WeekSummaryCard shopId={shopId} shopName={settings?.name ?? ''} /></Suspense>}

      {/* What needs a look first: master-list updates, getting started, low stock. */}
      {isOwner && (masterUpdates?.changes.length ?? 0) > 0 && (
        <Alert to="/items/pick" icon="M4 12a8 8 0 0114-5l2 2M20 4v5h-5M20 12a8 8 0 01-14 5l-2-2M4 20v-5h5" tone="bg-warn-soft text-warn-ink ring-amber-200">{t('pick.updatesBanner')} ({masterUpdates!.changes.length})</Alert>
      )}
      {isOwner && products.length === 0 && (
        <Alert to="/items/pick" icon="M12 2c3 3 4 7 3 11l-3 3-3-3c-1-4 0-8 3-11zM9 13l-4 1 2 3M15 13l4 1-2 3M12 16v5" tone="bg-brand text-white ring-teal-800">{t('pick.start')}</Alert>
      )}
      {isOwner && backup && !backup.empty && (backupDays == null || backupDays > 7) && (
        <Alert to="/backup" icon="M5 3h11l3 3v15H5zM8 3v5h7V3M8 21v-6h8v6" tone="bg-warn-soft text-warn-ink ring-amber-200">{backupDays == null ? t('backup.homeNever') : t('backup.homeOld', { count: backupDays })}</Alert>
      )}
      {lowCount > 0 && <Alert to="/items" icon="M3 7l9-4 9 4v10l-9 4-9-4zM3 7l9 4 9-4M12 11v10" tone="bg-orange-50 text-orange-800 ring-orange-200">{t('stock.lowCount', { count: lowCount })}</Alert>}

      {/* Every other page as one card of tiles, instead of a long stack of rows. */}
      <section className="mx-4 mt-4 grid grid-cols-2 gap-2.5 lg:grid-cols-4">
        <Tile to="/cashbook" icon="M5 4h14v16H5zM9 4v16" tone="bg-emerald-50 text-emerald-700">{t('book.todayLink')}</Tile>
        <KharchaRow shopId={shopId} />
        <Tile to="/dues" icon="M5 4h4l2 5-2.5 1.5a11 11 0 005 5L15 13l5 2v4a2 2 0 01-2 2A16 16 0 013 6a2 2 0 012-2" tone="bg-due-soft text-due">{t('dues.link')}</Tile>
        {/* Owner screens: helpers (staff) don't see them; the server refuses them too. */}
        {isOwner && <Tile to="/reports" icon="M4 20V10M10 20V4M16 20v-7M22 20H2" tone="bg-blue-50 text-blue-700">{t('reports.link')}</Tile>}
        <Tile to="/store" icon="M4 9l1-5h14l1 5M4 9h16v11H4zM9 20v-6h6v6" tone="bg-violet-50 text-violet-700">{t('myStore.link')}</Tile>
        {types.includes('jewellery') && <Tile to="/jewellery" icon="M6 3h12l3 6-9 12L3 9zM3 9h18M9 3l3 6 3-6M12 9v12" tone="bg-warn-soft text-warn">{t('jwl.title')}</Tile>}
        {types.includes('lodge') && <Tile to="/rooms" icon="M3 18V7M3 13h18v5M21 18v-5a3 3 0 00-3-3h-7v3" tone="bg-sky-50 text-sky-700">{t('rooms.link')}</Tile>}
        {types.includes('restaurant') && <Tile to="/tables" icon="M7 3v8M5 3v5a2 2 0 004 0V3M7 11v10M17 3c-2 2-2 6 0 8v10" tone="bg-warn-soft text-warn">{t('tables.link')}</Tile>}
        {types.includes('banquet') && <Tile to="/bookings" icon="M3 21l5-14 9 9zM14 4l1 2M19 5l-2 2M20 10l-2-1" tone="bg-pink-50 text-pink-700">{t('bookings.link')}</Tile>}
        {(types.includes('woodwork') || types.includes('sales_service')) && <Tile to="/jobs" icon="M14 6a4 4 0 005 5l-8 8-3-3 8-8a4 4 0 01-2-2zM5 19l-2 2" tone="bg-slate-100 text-slate-700">{t('jobs.link')}</Tile>}
        {isOwner && types.some((x) => COMMISSION_TYPES.includes(x)) && <Tile to="/painters" icon="M12 3a9 9 0 100 18c1 0 2-1 1-2-1-2 0-3 2-3h2a4 4 0 004-4 9 9 0 00-9-9zM7.5 11h.01M10 7h.01M15 7h.01" tone="bg-warn-soft text-warn">{t('painters.link')}</Tile>}
        {isOwner && isTrade(types) && <Tile to="/rates" icon="M3 3h8l10 10-8 8L3 11zM8 8h.01" tone="bg-brand-soft text-brand">{t('rates.link')}</Tile>}
        {isOwner && <Tile to="/suppliers" icon="M2 6h13v11H2zM15 10h4l3 3v4h-7M6 20h.01M18 20h.01" tone="bg-orange-50 text-orange-700">{t('side.suppliers')}</Tile>}
        {isOwner && <Tile to="/reorder" icon="M9 3h6v3H9zM6 5h3M15 5h3v16H6V5M9 12h6M9 16h4" tone="bg-cyan-50 text-cyan-700">{t('reorder.link')}</Tile>}
        {isOwner && <Tile to="/offers" icon="M3 3h8l10 10-8 8L3 11zM8 8h.01" tone="bg-rose-50 text-rose-700">{t('offers.link')}</Tile>}
        {isOwner && <Tile to="/settings" icon="M12 9a3 3 0 100 6 3 3 0 000-6zM12 2v3M12 19v3M4.2 4.2l2.1 2.1M17.7 17.7l2.1 2.1M2 12h3M19 12h3M4.2 19.8l2.1-2.1M17.7 6.3l2.1-2.1" tone="bg-slate-100 text-slate-700">{t('settings.link')}</Tile>}
      </section>

      <Suspense fallback={null}>
        {types.includes('medical') && <MedicalHomeCard shopId={shopId} />}
        {types.includes('dairy') && <DairyHomeCard shopId={shopId} />}
        {types.includes('sales_service') && <ServiceHomeCard isOwner={isOwner} />}
        {types.includes('beauty') && <SalonHomeCard shopId={shopId} isOwner={isOwner} />}
      </Suspense>

      <section className="mx-4 mt-4 flex flex-col rounded-card bg-white px-4 py-3.5 shadow-sm">
        <div className="flex items-center justify-between">
          <h2 className="text-base font-bold">{t('topDues')}</h2>
          <button className={`min-h-9 rounded-full px-3 text-sm ${btn.outline}`} onClick={() => navigate('/khata')}>
            {t('seeAll')}
          </button>
        </div>
        {today && today.topDues.length === 0 && <p className="py-2 text-muted">{t('noDues')}</p>}
        {today?.topDues.map((c) => (
          <button
            key={c.id}
            onClick={() => navigate(`/c/${c.id}`)}
            className="flex min-h-14 items-center gap-3 border-b border-[#edf2f1] py-2.5 text-left last:border-0"
          >
            <span aria-hidden className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-due-soft text-sm font-bold text-due">{c.name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0]!.toUpperCase()).join('')}</span>
            <span className="min-w-0 flex-1 truncate text-base font-semibold">{c.name}</span>
            <span className="text-base font-bold text-due">{formatRupees(c.balance)}</span>
            <span aria-hidden className="text-muted">›</span>
          </button>
        ))}
        {today && today.customersOwing > 0 && (
          <div className="mt-2 flex justify-between rounded-xl bg-due-soft px-3 py-2 text-sm text-due">
            <span>{t('inMarket', { count: today.customersOwing })}</span>
            <span className="font-bold">{formatRupees(today.outstanding)}</span>
          </div>
        )}
      </section>
    </div>
  )
}

function Split({ label, amount, className }: { label: string; amount: number | undefined; className: string }) {
  return (
    <div className={`flex flex-col rounded-xl p-2.5 ${className}`}>
      <span className="text-xs">{label}</span>
      <span className="text-[17px] font-bold">{amount === undefined ? '…' : formatRupees(amount)}</span>
    </div>
  )
}

/** A page on Home: a line icon in a soft coloured square, the page's name, and a chevron. `icon` is the icon's path. */
function Tile({ to, icon, tone, children, sub }: { to: string; icon: string; tone: string; children: ReactNode; sub?: ReactNode }) {
  return (
    <button onClick={() => navigate(to)} className="flex min-h-16 items-center gap-3 rounded-2xl bg-white p-3 text-left text-[15px] font-semibold leading-snug shadow-sm transition hover:bg-[#f6faf9] active:scale-[0.98]">
      <span aria-hidden className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${tone}`}><svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d={icon} /></svg></span>
      <span className="flex min-w-0 flex-1 flex-col">{children}{sub}</span>
      <span aria-hidden className="text-muted">›</span>
    </button>
  )
}

/** Something to act on, shown above the tiles. `icon` is a line icon's path. */
function Alert({ to, icon, tone, children }: { to: string; icon: string; tone: string; children: ReactNode }) {
  return (
    <button onClick={() => navigate(to)} className={`mx-4 mt-3 flex min-h-14 w-[calc(100%-2rem)] items-center gap-3 rounded-card px-4 text-left font-semibold ring-1 ${tone}`}>
      <span aria-hidden className="flex h-[30px] w-[30px] shrink-0 items-center justify-center rounded-full bg-white/80 text-current"><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d={icon} /></svg></span><span className="flex-1">{children}</span><span aria-hidden>→</span>
    </button>
  )
}

/** One of the four main jobs, in its own colour (as on the customer page): the icon on a white dot, the name under it. */
function Action({ label, to, icon, tone, dot = 'bg-white' }: { label: string; to: string; icon: ReactNode; tone: string; dot?: string }) {
  return (
    <button onClick={() => navigate(to)} className={`flex min-h-[100px] flex-col justify-between rounded-[20px] p-4 text-left transition active:scale-[0.98] ${tone}`}>
      <span className={`flex h-12 w-12 items-center justify-center rounded-full ${dot}`}>{icon}</span>
      <span className="text-lg font-bold">{label}</span>
    </button>
  )
}

interface BalanceCheck {
  checkedAt: string
  mismatches: number
  details: { customerId: string; name: string; storedBalance: number; sumOfEntries: number }[]
}

/**
 * The owner sees this only when last night's khata check found a balance that doesn't match its entries (a bug or
 * a hand edit in the database). Normally nothing shows. "Check again" re-runs it after a fix.
 */
function BalanceAlert() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const check = useQuery({
    queryKey: ['balance-check'],
    queryFn: async () => {
      const response = await request('/v1/ledger/checks/latest')
      return response.status === 200 ? ((await response.json()) as BalanceCheck) : null
    },
    staleTime: 600_000,
  })
  if (!check.data || check.data.mismatches === 0) return null
  return (
    <div className="mx-4 mt-4 flex flex-col gap-2 rounded-card bg-due-soft p-4 text-due ring-1 ring-red-200">
      <span className="flex items-center gap-2 font-bold"><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M12 3l10 18H2zM12 10v4M12 17h.01" /></svg>{t('balanceCheck.title', { count: check.data.mismatches })}</span>
      {check.data.details.slice(0, 3).map((d) => (
        <button key={d.customerId} onClick={() => navigate(`/c/${d.customerId}`)} className="text-left text-sm underline">
          {t('balanceCheck.line', { name: d.name, stored: formatRupees(d.storedBalance), entries: formatRupees(d.sumOfEntries) })}
        </button>
      ))}
      <span className="text-xs text-ink">{t('balanceCheck.hint')}</span>
      <button className="min-h-11 self-start rounded-xl border border-red-200 bg-white px-3 text-sm font-semibold"
        onClick={() => void api('/v1/ledger/checks', 'POST', {}).then(() => queryClient.invalidateQueries({ queryKey: ['balance-check'] }))}>
        {t('balanceCheck.again')}
      </button>
    </div>
  )
}

/**
 * The plan in a line, only when it matters: the trial's last week, payment due (grace), on hold, or on Free.
 * Taps through to My plan.
 */
function PlanBanner({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const s = useSubscription(shopId).data
  if (!s) return null
  const show = (s.status === 'trial' && (s.daysLeft ?? 99) <= 7) || s.status === 'grace' || s.status === 'on_hold' || s.status === 'free'
  if (!show) return null
  const tone = s.status === 'trial' ? 'bg-brand-soft text-brand-deep' : 'bg-warn-soft text-warn-ink'
  return (
    <button onClick={() => navigate('/plan')} className={`mx-4 mt-3 flex min-h-12 w-[calc(100%-2rem)] items-center justify-between rounded-card px-4 text-left font-semibold ${tone}`}>
      <span>{t(`plan2.banner.${s.status}`, { count: s.daysLeft ?? 0, reason: s.holdReason ?? '' })}</span>
      <span aria-hidden="true">→</span>
    </button>
  )
}

/** "Today's kharcha" with this month's total under it; in a month's first week, last month's closed kharcha too. */
function KharchaRow({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const now = new Date(Date.now() + 5.5 * 3600_000)
  const month = now.toISOString().slice(0, 7)
  const last = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() - 1, 1)).toISOString().slice(0, 7)
  const firstWeek = now.getUTCDate() <= 7
  const current = useQuery({ queryKey: [shopId, 'expenses', 'month', month], queryFn: () => api<{ shopKharcha: number }>(`/v1/expenses/month?month=${month}`), staleTime: 60_000 }).data
  const previous = useQuery({ queryKey: [shopId, 'expenses', 'month', last], queryFn: () => api<{ shopKharcha: number; count: number }>(`/v1/expenses/month?month=${last}`), enabled: firstWeek, staleTime: 600_000 }).data
  return (
    <>
      <Tile to="/expenses" icon="M6 2h12v20l-3-2-3 2-3-2-3 2zM9 7h6M9 11h6M9 15h4" tone="bg-warn-soft text-warn" sub={current && <span className="text-xs font-normal text-muted">{t('kharchaMonth.homeThisMonth', { amount: formatRupees(current.shopKharcha) })}</span>}>{t('expense.link')}</Tile>
      {firstWeek && previous && previous.count > 0 && (
        <button onClick={() => navigate('/expenses/month')} className="col-span-full flex min-h-12 items-center justify-between rounded-2xl bg-brand-soft px-4 text-left font-semibold text-brand-deep ring-1 ring-teal-100">
          <span>{t('kharchaMonth.lastMonthReady', { month: new Date(`${last}-01T00:00:00`).toLocaleDateString(shownLocale(), { month: 'long' }), amount: formatRupees(previous.shopKharcha) })}</span>
          <span aria-hidden="true">→</span>
        </button>
      )}
    </>
  )
}

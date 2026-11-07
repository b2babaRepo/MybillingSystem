import { ListDetail } from './components/ListDetail'
import { lazy, Suspense, useCallback, useEffect, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { AccountMenu } from './components/AccountMenu'
import { AppBanners } from './components/AppBanners'
import { BackIcon, BillIcon, CheckIcon, CloudOffIcon, HomeIcon, ItemsIcon, KhataIcon, PaymentIcon } from './components/Icons'
import { request } from './lib/api'
import { useOutbox, useSession } from './lib/data'
import { useNewOrders } from './lib/orderAlerts'
import type { Order } from './lib/planner'
import { requestPersistentStorage } from './lib/db'
import { flush } from './lib/outbox'
import { getSession, setSession } from './lib/session'
import { navigate, usePath } from './router'
import { confirmAsk } from './components/Ask'
// Signed-in people (most opens) never need the sign-in screen, so it loads on demand.
const AuthScreen = lazy(() => import('./screens/AuthScreen').then((module) => ({ default: module.AuthScreen })))
const ShopPicker = lazy(() => import('./screens/AuthScreen').then((module) => ({ default: module.ShopPicker })))
import { BillScreen } from './screens/BillScreen'
const CustomerScreen = lazy(() => import('./screens/CustomerScreen').then((module) => ({ default: module.CustomerScreen })))
const DisputesScreen = lazy(() => import('./screens/DisputesScreen').then((module) => ({ default: module.DisputesScreen })))
const CommonListScreen = lazy(() => import('./screens/CommonListScreen').then((module) => ({ default: module.CommonListScreen })))
const CustomersScreen = lazy(() => import('./screens/CustomersScreen').then((module) => ({ default: module.CustomersScreen })))
import { HomeScreen } from './screens/HomeScreen'
const ItemsScreen = lazy(() => import('./screens/ItemsScreen').then((module) => ({ default: module.ItemsScreen })))
const SyncScreen = lazy(() => import('./screens/SyncScreen').then((module) => ({ default: module.SyncScreen })))

// Billing, khata and items load with the app; screens used now and then load on first visit (and are then
// cached by the service worker, so they open offline too). Keeps the start fast on low-end phones.
const InboxScreen = lazy(() => import('./screens/InboxScreen').then((module) => ({ default: module.InboxScreen })))
const CashBookScreen = lazy(() => import('./screens/CashBookScreen').then((module) => ({ default: module.CashBookScreen })))
const ImportScreen = lazy(() => import('./screens/ImportScreen').then((module) => ({ default: module.ImportScreen })))
const PrintScreen = lazy(() => import('./screens/PrintScreen').then((module) => ({ default: module.PrintScreen })))
const OrderScreen = lazy(() => import('./screens/OrderScreen').then((module) => ({ default: module.OrderScreen })))
const ProductScreen = lazy(() => import('./screens/ProductScreen').then((module) => ({ default: module.ProductScreen })))
const StocktakeScreen = lazy(() => import('./screens/StocktakeScreen').then((module) => ({ default: module.StocktakeScreen })))
const DuesScreen = lazy(() => import('./screens/DuesScreen').then((module) => ({ default: module.DuesScreen })))
const StaffSalesScreen = lazy(() => import('./screens/StaffSalesScreen').then((module) => ({ default: module.StaffSalesScreen })))
const TagsScreen = lazy(() => import('./screens/TagsScreen').then((module) => ({ default: module.TagsScreen })))
const SizeReportScreen = lazy(() => import('./screens/SizeReportScreen').then((module) => ({ default: module.SizeReportScreen })))
const ItemMarginScreen = lazy(() => import('./screens/ItemMarginScreen').then((module) => ({ default: module.ItemMarginScreen })))
const ReportsScreen = lazy(() => import('./screens/ReportsScreen').then((module) => ({ default: module.ReportsScreen })))
const StaffScreen = lazy(() => import('./screens/StaffScreen').then((module) => ({ default: module.StaffScreen })))
const ExpensesScreen = lazy(() => import('./screens/ExpensesScreen').then((module) => ({ default: module.ExpensesScreen })))
const RatesScreen = lazy(() => import('./screens/RatesScreen').then((module) => ({ default: module.RatesScreen })))
const PaintersScreen = lazy(() => import('./screens/PaintersScreen').then((module) => ({ default: module.PaintersScreen })))
const GstExportScreen = lazy(() => import('./screens/GstExportScreen').then((module) => ({ default: module.GstExportScreen })))
const PickItemsScreen = lazy(() => import('./screens/PickItemsScreen').then((module) => ({ default: module.PickItemsScreen })))
const ProfitLossScreen = lazy(() => import('./screens/ProfitLossScreen').then((module) => ({ default: module.ProfitLossScreen })))
const ClientCardsScreen = lazy(() => import('./screens/ClientCardsScreen').then((module) => ({ default: module.ClientCardsScreen })))
const BridalScreen = lazy(() => import('./screens/BridalScreen').then((module) => ({ default: module.BridalScreen })))
const PackagesScreen = lazy(() => import('./screens/PackagesScreen').then((module) => ({ default: module.PackagesScreen })))
const StylistEarningsScreen = lazy(() => import('./screens/SalonSetupScreen').then((module) => ({ default: module.StylistEarningsScreen })))
const AppointmentsScreen = lazy(() => import('./screens/AppointmentsScreen').then((module) => ({ default: module.AppointmentsScreen })))
const ServiceDeskScreen = lazy(() => import('./screens/ServiceDeskScreen').then((module) => ({ default: module.ServiceDeskScreen })))
const AmcScreen = lazy(() => import('./screens/AmcScreen').then((module) => ({ default: module.AmcScreen })))
const TripsScreen = lazy(() => import('./screens/TripsScreen').then((module) => ({ default: module.TripsScreen })))
const ClaimsScreen = lazy(() => import('./screens/ClaimsScreen').then((module) => ({ default: module.ClaimsScreen })))
const SerialsScreen = lazy(() => import('./screens/SerialsScreen').then((module) => ({ default: module.SerialsScreen })))
const DairyScreen = lazy(() => import('./screens/DairyScreen').then((module) => ({ default: module.DairyScreen })))
const RefillsScreen = lazy(() => import('./screens/PharmacyAssistScreens').then((module) => ({ default: module.RefillsScreen })))
const ReorderScreen = lazy(() => import('./screens/PharmacyAssistScreens').then((module) => ({ default: module.ReorderScreen })))
const PharmacyMonthScreen = lazy(() => import('./screens/PharmacyMonthScreen').then((module) => ({ default: module.PharmacyMonthScreen })))
const ReturnsScreen = lazy(() => import('./screens/ReturnsScreen').then((module) => ({ default: module.ReturnsScreen })))
const H1RegisterScreen = lazy(() => import('./screens/H1RegisterScreen').then((module) => ({ default: module.H1RegisterScreen })))
const MedicineStockScreen = lazy(() => import('./screens/MedicineStockScreen').then((module) => ({ default: module.MedicineStockScreen })))
const MedicalSetupScreen = lazy(() => import('./screens/MedicalScreen').then((module) => ({ default: module.MedicalSetupScreen })))
const SalonSetupScreen = lazy(() => import('./screens/SalonSetupScreen').then((module) => ({ default: module.SalonSetupScreen })))
const RoomsScreen = lazy(() => import('./screens/RoomsScreen').then((module) => ({ default: module.RoomsScreen })))
const TableBookingsScreen = lazy(() => import('./screens/TableBookingsScreen').then((module) => ({ default: module.TableBookingsScreen })))
const BookingsScreen = lazy(() => import('./screens/BookingsScreen').then((module) => ({ default: module.BookingsScreen })))
const JobsScreen = lazy(() => import('./screens/JobsScreen').then((module) => ({ default: module.JobsScreen })))
const SuppliersScreen = lazy(() => import('./screens/SuppliersScreen').then((module) => ({ default: module.SuppliersScreen })))
const ProfileScreen = lazy(() => import('./screens/ProfileScreen').then((module) => ({ default: module.ProfileScreen })))
const MyPlanScreen = lazy(() => import('./screens/MyPlanScreen').then((module) => ({ default: module.MyPlanScreen })))
const SettingsScreen = lazy(() => import('./screens/SettingsScreen').then((module) => ({ default: module.SettingsScreen })))
const MyStoreScreen = lazy(() => import('./screens/MyStoreScreen').then((module) => ({ default: module.MyStoreScreen })))
const StoreApp = lazy(() => import('./screens/store/StoreApp').then((module) => ({ default: module.StoreApp })))
const KhataLinkPage = lazy(() => import('./screens/khata/KhataViews').then((module) => ({ default: module.KhataLinkPage })))
const AgentScreen = lazy(() => import('./screens/AgentScreen').then((module) => ({ default: module.AgentScreen })))
const PlanBillPage = lazy(() => import('./screens/PlanBillPage').then((module) => ({ default: module.PlanBillPage })))
const KharchaPrint = lazy(() => import('./screens/KharchaMonth').then((module) => ({ default: module.KharchaPrint })))
const TeamScreen = lazy(() => import('./screens/TeamScreen').then((module) => ({ default: module.TeamScreen })))
const TeamViewPage = lazy(() => import('./screens/TeamViewPage').then((module) => ({ default: module.TeamViewPage })))
const PlatformScreen = lazy(() => import('./screens/PlatformScreen').then((module) => ({ default: module.PlatformScreen })))
const PayPage = lazy(() => import('./screens/PayPage').then((module) => ({ default: module.PayPage })))
const PlannerApp = lazy(() => import('./screens/planner/PlannerApp').then((module) => ({ default: module.PlannerApp })))

export default function App() {
  const session = useSession()
  const path = usePath()
  // A shopkeeper can also plan their own home's lists; everyone without a shop only sees the planner.
  const [household, setHousehold] = useState(false)
  // The public online store: open to anyone, signed in or not.
  if (path.startsWith('/s/')) {
    return (
      <Suspense fallback={<Loading />}>
        <StoreApp path={path} />
      </Suspense>
    )
  }
  // The pay page from a dues reminder: the shop's UPI ID and the amount, no account needed. Early reminders linked
  // #/p/{shop}/{amount}; an item's own page is #/p/{id} (one part), so only the two-part form is the pay page.
  if (path.startsWith('/pay/') || /^\/p\/[^/]+\/[^/]+$/.test(path)) {
    return (
      <Suspense fallback={<Loading />}>
        <PayPage path={path} />
      </Suspense>
    )
  }
  // A customer's private khata link: read-only, no account needed.
  if (path.startsWith('/k/')) {
    return (
      <Suspense fallback={<Loading />}>
        <KhataLinkPage path={path} />
      </Suspense>
    )
  }
  // A team's view link, for people without the app: the token is the key.
  if (path.startsWith('/tv/')) {
    return (
      <Suspense fallback={<Loading />}>
        <TeamViewPage path={path} />
      </Suspense>
    )
  }
  // A plan bill opened from WhatsApp: the token in the link is the key, no account needed.
  if (path.startsWith('/bill/plan/')) {
    return (
      <Suspense fallback={<Loading />}>
        <PlanBillPage path={path} />
      </Suspense>
    )
  }
  if (!session) return <Suspense fallback={<Loading />}><AuthScreen /></Suspense>
  // Team kharcha: any account (shop or household), any plan.
  if (path === '/team' || path.startsWith('/team/')) {
    return (
      <Suspense fallback={<Loading />}>
        <TeamScreen path={path} userName={session.user.fullName} />
      </Suspense>
    )
  }
  // Applying to be a field agent, and the agent's code once approved: any account.
  if (path === '/agent') {
    return (
      <Suspense fallback={<Loading />}>
        <AgentScreen userName={session.user.fullName} />
      </Suspense>
    )
  }
  if (path === '/platform' && session.user.isPlatformAdmin) {
    return (
      <Suspense fallback={<Loading />}>
        <PlatformScreen />
      </Suspense>
    )
  }
  const switchTo = (toHousehold: boolean) => {
    navigate('/')
    setHousehold(toHousehold)
  }
  const hasShops = session.memberships.length > 0
  if (!hasShops || household) {
    return (
      <Suspense fallback={<Loading />}>
        <PlannerApp userId={session.user.id} userName={session.user.fullName} onSwitchToShop={hasShops ? () => switchTo(false) : undefined} />
      </Suspense>
    )
  }
  if (!session.shopId) return <Suspense fallback={<Loading />}><ShopPicker session={session} onHousehold={() => switchTo(true)} /></Suspense>
  const shop = session.memberships.find((m) => m.shopId === session.shopId)
  return (
    <Shop shopId={session.shopId} shopName={shop?.shopName ?? ''} shopSlug={shop?.shopSlug ?? ''} userName={session.user.fullName}
      isOwner={session.role === 'owner'} onHousehold={() => switchTo(true)} />
  )
}

function Shop({ shopId, shopName, shopSlug, userName, isOwner, onHousehold }: { shopId: string; shopName: string; shopSlug: string; userName: string; isOwner: boolean; onHousehold: () => void }) {
  const { t } = useTranslation()
  const path = usePath()
  const { pending, failed, state } = useOutbox(shopId)
  const notifyText = useCallback((order: Order) => t('myStore.newOrder', { name: order.customerName }), [t])
  const newOrders = useNewOrders(shopId, notifyText)
  const [persistent, setPersistent] = useState(true)

  useEffect(() => {
    void requestPersistentStorage().then(setPersistent)
    // Sync now, whenever the connection comes back, and every 30 seconds as a fallback.
    const sync = () => void flush(shopId)
    sync()
    window.addEventListener('online', sync)
    const timer = setInterval(sync, 30_000)
    return () => {
      window.removeEventListener('online', sync)
      clearInterval(timer)
    }
  }, [shopId])

  useEffect(() => {
    // Closing the tab with unsynced entries gets a warning.
    const warn = (e: BeforeUnloadEvent) => {
      if (pending > 0) e.preventDefault()
    }
    window.addEventListener('beforeunload', warn)
    return () => window.removeEventListener('beforeunload', warn)
  }, [pending])

  async function signOut() {
    if (pending + failed > 0 && !(await confirmAsk(t('unsyncedSignOut', { count: pending + failed })))) return
    void request('/v1/auth/sign-out', 'POST', { refreshToken: getSession()?.refreshToken }).catch(() => {})
    setSession(null)
  }

  const customerMatch = path.match(/^\/c\/([0-9a-f-]+)(?:\?e=([0-9a-f-]+))?$/)
  const orderMatch = path.match(/^\/o\/([0-9a-f-]+)$/)
  const productMatch = path.match(/^\/p\/([0-9a-f-]+)$/)
  const tagsMatch = path.match(/^\/tags\/([0-9a-f,-]+)$/)
  const printMatch = path.match(/^\/print\/(statement|bill)\/([0-9a-f-]+)(?:\/(\d{4}-\d{2}))?$/)
  const isItemsPage = !!productMatch || path === '/items/pick' || path === '/stocktake' || path === '/import' || path === '/items/common'
  const tabs: { path: string; label: string; icon: ReactNode; badge?: number }[] = [
    { path: '/', label: t('home'), icon: <HomeIcon size={22} /> },
    { path: '/khata', label: t('khata'), icon: <KhataIcon size={22} /> },
    { path: '/bill', label: t('bill'), icon: <BillIcon size={22} /> },
    { path: '/inbox', label: t('orders'), icon: <PaymentIcon size={22} />, badge: newOrders },
    { path: '/items', label: t('items'), icon: <ItemsIcon size={22} /> },
  ]
  const isTab = tabs.some((tab) => tab.path === path)
  // Desktop only: the pages a phone reaches from Home get their own place in the side menu, in two groups.
  const sideGroups: { title: string; links: { path: string; label: string }[] }[] = [
    { title: t('side.money'), links: [
      { path: '/dues', label: t('side.dues') },
      { path: '/cashbook', label: t('side.cashbook') },
      { path: '/expenses', label: t('side.expenses') },
      { path: '/reports', label: t('side.reports') },
      ...(isOwner ? [{ path: '/pnl', label: t('side.pnl') }, { path: '/gst', label: 'GST' }] : []),
    ] },
    { title: t('side.shop'), links: [
      { path: '/stocktake', label: t('side.stocktake') },
      ...(isOwner ? [{ path: '/suppliers', label: t('side.suppliers') }, { path: '/settings', label: t('side.settings') }] : []),
    ] },
  ]
  const sideLink = sideGroups.flatMap((g) => g.links).filter((l) => path === l.path || path.startsWith(l.path + '/')).sort((a, b) => b.path.length - a.path.length)[0]
  const pageTitle = tabs.find((tab) => tab.path === path)?.label ?? sideLink?.label
  const unsynced = pending + failed

  const kharchaPrint = path.match(/^\/print\/kharcha\/(\d{4}-\d{2})$/)
  if (kharchaPrint) {
    return (
      <Suspense fallback={<Loading />}>
        <KharchaPrint shopId={shopId} shopName={shopName} month={kharchaPrint[1]} />
      </Suspense>
    )
  }
  if (printMatch) {
    return (
      <Suspense fallback={<Loading />}>
        <PrintScreen shopId={shopId} shopName={shopName} kind={printMatch[1] as 'statement' | 'bill'} id={printMatch[2]} month={printMatch[3]} />
      </Suspense>
    )
  }

  return (
    <div className="mx-auto min-h-dvh max-w-xl pb-20 lg:max-w-none lg:pb-6 lg:pl-60">
      <header className="sticky top-0 z-10 flex items-center gap-2 bg-teal-700 px-4 py-3 text-white">
        {!isTab && (
          <button onClick={() => navigate(customerMatch ? '/khata' : orderMatch ? '/inbox' : isItemsPage ? '/items' : path === '/settings/staff' ? '/settings' : '/')} className={`-ml-2 flex h-11 w-11 items-center justify-center ${sideLink || customerMatch || orderMatch ? 'lg:hidden' : ''}`} aria-label={t('a11y.back')}>
            <BackIcon />
          </button>
        )}
        <div className="flex min-w-0 flex-1 flex-col">
          {path === '/' && <span className="text-sm opacity-85">{t('greeting', { name: userName.split(' ')[0] })}</span>}
          <h1 className="truncate text-xl font-bold">{shopName}{pageTitle && <span className="hidden font-semibold opacity-85 lg:inline"> · {pageTitle}</span>}</h1>
        </div>
        <button
          onClick={() => navigate('/sync')}
          className={`flex h-11 items-center gap-1.5 rounded-full px-3 text-[13px] font-semibold ${
            unsynced > 0 || state === 'offline' ? 'bg-amber-400 text-amber-950' : 'bg-white/20 text-white'
          }`}
        >
          {state === 'offline' ? <CloudOffIcon size={16} /> : unsynced === 0 ? <CheckIcon size={16} /> : null}
          {unsynced > 0 ? t('waiting', { count: unsynced }) : state === 'offline' ? t('pwa.offline') : t('synced')}
        </button>
        <AccountMenu
          name={userName}
          role={t(isOwner ? 'account.ownerAt' : 'account.helperAt', { shop: shopName })}
          unsynced={unsynced}
          onSignOut={signOut}
          actions={[
            ...(isOwner ? [{ label: t('settings.link'), onClick: () => navigate('/settings') }] : []),
            ...(isOwner ? [{ label: t('suppliers.title'), onClick: () => navigate('/suppliers') }] : []),
            { label: t('profile.title'), onClick: () => navigate('/profile') },
            ...(isOwner ? [{ label: t('plan2.myPlan'), onClick: () => navigate('/plan') }] : []),
            { label: t('teamLink'), onClick: () => navigate('/team') },
            { label: t('plan.householdLists'), onClick: onHousehold },
            { label: t('account.syncStatus'), onClick: () => navigate('/sync') },
          ]}
        />
      </header>

      {/* Desktop width: billing and the item list use the room; the other pages stay a readable width. */}
      <main className={`lg:mx-auto ${path === '/bill' || path === '/items' || path === '/items/pick' || path === '/khata' || path === '/dues' || path === '/inbox' || path === '/suppliers' || path === '/settings' || path === '/settings/staff' || customerMatch || orderMatch ? 'lg:max-w-6xl' : 'lg:max-w-4xl'}`}>
        {/* Keyed by screen: a page change shows the loading state at once, never the previous screen lingering. */}
        <Suspense key={customerMatch ? '/khata' : orderMatch ? '/inbox' : path} fallback={<Loading />}>
        {customerMatch ? (
          <ListDetail hasDetail list={<CustomersScreen shopId={shopId} selectedId={customerMatch[1]} />} detail={<CustomerScreen shopId={shopId} shopName={shopName} customerId={customerMatch[1]} focusEntryId={customerMatch[2]} isOwner={isOwner} userName={userName} />} />
        ) : path === '/dues' ? (
          <DuesScreen shopId={shopId} shopName={shopName} />
        ) : path === '/reports' ? (
          <ReportsScreen shopId={shopId} isOwner={isOwner} />
        ) : path === '/settings/staff' ? (
          <StaffScreen />
        ) : path === '/settings' ? (
          <SettingsScreen shopId={shopId} isOwner={isOwner} />
        ) : path === '/store' ? (
          <MyStoreScreen shopName={shopName} slug={shopSlug} />
        ) : path === '/cashbook' ? (
          <CashBookScreen shopId={shopId} />
        ) : path === '/expenses' ? (
          <ExpensesScreen shopId={shopId} shopName={shopName} />
        ) : path === '/expenses/month' ? (
          <ExpensesScreen shopId={shopId} shopName={shopName} month />
        ) : path === '/items/pick' ? (
          <PickItemsScreen shopId={shopId} />
        ) : path === '/gst' && isOwner ? (
          <GstExportScreen />
        ) : path === '/reports/staff' && isOwner ? (
          <StaffSalesScreen shopId={shopId} />
        ) : path === '/reports/sizes' && isOwner ? (
          <SizeReportScreen shopId={shopId} />
        ) : path === '/reports/items' && isOwner ? (
          <ItemMarginScreen shopId={shopId} />
        ) : path === '/pnl' && isOwner ? (
          <ProfitLossScreen />
        ) : path === '/client-cards' ? (
          <ClientCardsScreen shopId={shopId} isOwner={isOwner} />
        ) : path === '/bridal' ? (
          <BridalScreen shopId={shopId} shopName={shopName} isOwner={isOwner} />
        ) : path === '/packages' ? (
          <PackagesScreen shopId={shopId} shopName={shopName} isOwner={isOwner} />
        ) : path === '/stylists' && isOwner ? (
          <StylistEarningsScreen shopId={shopId} />
        ) : path === '/appointments' ? (
          <AppointmentsScreen shopId={shopId} shopName={shopName} />
        ) : path === '/serials' ? (
          <SerialsScreen shopId={shopId} />
        ) : path === '/claims' && isOwner ? (
          <ClaimsScreen shopId={shopId} />
        ) : path === '/trips' ? (
          <TripsScreen shopId={shopId} isOwner={isOwner} />
        ) : path === '/amc' ? (
          <AmcScreen shopId={shopId} />
        ) : path === '/service-desk' && isOwner ? (
          <ServiceDeskScreen shopId={shopId} />
        ) : path === '/dairy' ? (
          <DairyScreen shopId={shopId} shopName={shopName} isOwner={isOwner} />
        ) : path === '/medical/refills' ? (
          <RefillsScreen shopId={shopId} shopName={shopName} />
        ) : path === '/reorder' && isOwner ? (
          <ReorderScreen shopId={shopId} shopName={shopName} general />
        ) : path === '/medical/reorder' && isOwner ? (
          <ReorderScreen shopId={shopId} shopName={shopName} />
        ) : path === '/medical/month' && isOwner ? (
          <PharmacyMonthScreen shopId={shopId} />
        ) : path === '/medical/returns' && isOwner ? (
          <ReturnsScreen shopId={shopId} />
        ) : path === '/medical/h1' ? (
          <H1RegisterScreen shopId={shopId} />
        ) : path === '/medical/stock' ? (
          <MedicineStockScreen shopId={shopId} />
        ) : path === '/medical/setup' ? (
          <MedicalSetupScreen shopId={shopId} isOwner={isOwner} />
        ) : path === '/salon/setup' ? (
          <SalonSetupScreen shopId={shopId} />
        ) : path === '/rooms' ? (
          <RoomsScreen shopId={shopId} isOwner={isOwner} />
        ) : path === '/tables' ? (
          <TableBookingsScreen shopId={shopId} shopName={shopName} />
        ) : path === '/bookings' ? (
          <BookingsScreen shopId={shopId} isOwner={isOwner} />
        ) : path === '/jobs' ? (
          <JobsScreen shopId={shopId} isOwner={isOwner} />
        ) : path === '/suppliers' ? (
          <SuppliersScreen shopId={shopId} />
        ) : path === '/profile' ? (
          <ProfileScreen />
        ) : path === '/plan' || path === '/choose-plan' ? (
          <MyPlanScreen shopId={shopId} welcome={path === '/choose-plan'} />
        ) : path === '/rates' ? (
          <RatesScreen shopId={shopId} />
        ) : path === '/painters' ? (
          <PaintersScreen shopId={shopId} shopName={shopName} />
        ) : tagsMatch && isOwner ? (
          <TagsScreen shopId={shopId} shopName={shopName} ids={tagsMatch[1].split(',')} />
        ) : productMatch ? (
          <ProductScreen shopId={shopId} productId={productMatch[1]} />
        ) : path === '/stocktake' ? (
          <StocktakeScreen shopId={shopId} />
        ) : path === '/items/common' ? (
          <CommonListScreen shopId={shopId} />
        ) : path === '/import' ? (
          <ImportScreen shopId={shopId} />
        ) : orderMatch ? (
          <ListDetail hasDetail list={<InboxScreen shopId={shopId} selectedId={orderMatch[1]} />} detail={<OrderScreen shopId={shopId} orderId={orderMatch[1]} />} />
        ) : path === '/inbox' ? (
          <ListDetail hasDetail={false} list={<InboxScreen shopId={shopId} />} detail={null} placeholder={t('inboxPick')} />
        ) : path === '/khata' ? (
          <ListDetail hasDetail={false} list={<CustomersScreen shopId={shopId} />} detail={null} placeholder={t('khataPick')} />
        ) : path === '/disputes' ? (
          <DisputesScreen isOwner={isOwner} />
        ) : path === '/bill' ? (
          <BillScreen shopId={shopId} shopName={shopName} isOwner={isOwner} />
        ) : path === '/items' ? (
          <ItemsScreen shopId={shopId} isOwner={isOwner} />
        ) : path === '/sync' ? (
          <SyncScreen shopId={shopId} persistent={persistent} />
        ) : (
          <HomeScreen shopId={shopId} isOwner={isOwner} />
        )}
        </Suspense>
      </main>

      <AppBanners />

      <nav className="fixed inset-x-0 bottom-0 z-10 mx-auto grid h-[68px] max-w-xl grid-cols-5 border-t border-line bg-white pb-[env(safe-area-inset-bottom)] lg:inset-y-0 lg:right-auto lg:mx-0 lg:h-auto lg:w-60 lg:max-w-none lg:auto-rows-auto lg:grid-cols-1 lg:content-start lg:gap-1 lg:overflow-y-auto lg:border-t-0 lg:border-r lg:px-3 lg:pt-20 lg:pb-6 lg:[&>button]:min-h-12">
        {tabs.map((tab) => (
          <button
            key={tab.path}
            onClick={() => navigate(tab.path)}
            aria-current={path === tab.path ? 'page' : undefined}
            className={`relative flex flex-col items-center justify-center gap-0.5 text-[13px] lg:flex-row lg:justify-start lg:gap-3 lg:rounded-xl lg:px-4 lg:text-base ${
              path === tab.path ? 'font-bold text-teal-700 lg:bg-teal-50' : 'font-semibold text-muted lg:hover:bg-[#f1f5f4]'
            }`}
          >
            {tab.icon}
            {tab.label}
            {!!tab.badge && (
              <span className="absolute top-1.5 right-[calc(50%-22px)] min-w-5 lg:static lg:ml-auto rounded-full bg-udhaar px-1.5 text-xs font-bold text-white" aria-label={`${tab.badge}`}>
                {tab.badge}
              </span>
            )}
          </button>
        ))}
        {sideGroups.map((group) => group.links.length > 0 && (
          <div key={group.title} className="hidden lg:flex lg:flex-col lg:gap-0.5">
            <span className="mt-4 px-4 pb-1 text-xs font-bold tracking-wide text-muted uppercase">{group.title}</span>
            {group.links.map((link) => (
              <button
                key={link.path}
                onClick={() => navigate(link.path)}
                aria-current={sideLink?.path === link.path ? 'page' : undefined}
                className={`flex min-h-10 items-center rounded-xl px-4 text-left text-[15px] ${sideLink?.path === link.path ? 'bg-teal-50 font-bold text-teal-700' : 'font-semibold text-muted hover:bg-[#f1f5f4]'}`}
              >
                {link.label}
              </button>
            ))}
          </div>
        ))}
      </nav>
    </div>
  )
}

function Loading() {
  return <p className="p-6 text-center text-muted" role="status">…</p>
}

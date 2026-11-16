import { shopTypes, useShopSettings } from './lib/reports'
import { ListDetail } from './components/ListDetail'
import { lazy, Suspense, useCallback, useEffect, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
// The account menu loads just after the first screen: its circle shows meanwhile.
const AccountMenu = lazy(() => import('./components/AccountMenu').then((module) => ({ default: module.AccountMenu })))
// Update and install banners: shown a moment after the first screen.
const AppBanners = lazy(() => import('./components/AppBanners').then((module) => ({ default: module.AppBanners })))
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
const billScreen = () => import('./screens/BillScreen')
const BillScreen = lazy(() => billScreen().then((module) => ({ default: module.BillScreen })))
const CustomerScreen = lazy(() => import('./screens/CustomerScreen').then((module) => ({ default: module.CustomerScreen })))
const DisputesScreen = lazy(() => import('./screens/DisputesScreen').then((module) => ({ default: module.DisputesScreen })))
const CommonListScreen = lazy(() => import('./screens/CommonListScreen').then((module) => ({ default: module.CommonListScreen })))
const CustomerImportScreen = lazy(() => import('./screens/CustomerImportScreen').then((module) => ({ default: module.CustomerImportScreen })))
const ReceiptsScreen = lazy(() => import('./screens/ReceiptsScreen').then((module) => ({ default: module.ReceiptsScreen })))
const RemindersScreen = lazy(() => import('./screens/RemindersScreen').then((module) => ({ default: module.RemindersScreen })))
const CustomersScreen = lazy(() => import('./screens/CustomersScreen').then((module) => ({ default: module.CustomersScreen })))
import { HomeScreen } from './screens/HomeScreen'
const ItemsScreen = lazy(() => import('./screens/ItemsScreen').then((module) => ({ default: module.ItemsScreen })))
const SyncScreen = lazy(() => import('./screens/SyncScreen').then((module) => ({ default: module.SyncScreen })))

// Billing, khata and items load with the app; screens used now and then load on first visit (and are then
// cached by the service worker, so they open offline too). Keeps the start fast on low-end phones.
const InboxScreen = lazy(() => import('./screens/InboxScreen').then((module) => ({ default: module.InboxScreen })))
const CashLedgerScreen = lazy(() => import('./screens/CashLedgerScreen').then((module) => ({ default: module.CashLedgerScreen })))
const BankBookScreen = lazy(() => import('./screens/BankBookScreen').then((module) => ({ default: module.BankBookScreen })))
const CashBookScreen = lazy(() => import('./screens/CashBookScreen').then((module) => ({ default: module.CashBookScreen })))
const ImportScreen = lazy(() => import('./screens/ImportScreen').then((module) => ({ default: module.ImportScreen })))
const PrintScreen = lazy(() => import('./screens/PrintScreen').then((module) => ({ default: module.PrintScreen })))
const OrderScreen = lazy(() => import('./screens/OrderScreen').then((module) => ({ default: module.OrderScreen })))
const ProductScreen = lazy(() => import('./screens/ProductScreen').then((module) => ({ default: module.ProductScreen })))
const GoodsToComeScreen = lazy(() => import('./screens/GoodsToComeScreen').then((module) => ({ default: module.GoodsToComeScreen })))
const DiscountsScreen = lazy(() => import('./screens/DiscountsScreen').then((module) => ({ default: module.DiscountsScreen })))
const LotsScreen = lazy(() => import('./screens/LotsScreen').then((module) => ({ default: module.LotsScreen })))
const FestivalsScreen = lazy(() => import('./screens/FestivalsScreen').then((module) => ({ default: module.FestivalsScreen })))
const BardanaScreen = lazy(() => import('./screens/BardanaScreen').then((module) => ({ default: module.BardanaScreen })))
const GodownsScreen = lazy(() => import('./screens/GodownsScreen').then((module) => ({ default: module.GodownsScreen })))
const HoldsScreen = lazy(() => import('./screens/HoldsScreen').then((module) => ({ default: module.HoldsScreen })))
const ReceivedBySupplierScreen = lazy(() => import('./screens/ReceivedBySupplierScreen').then((module) => ({ default: module.ReceivedBySupplierScreen })))
const CategoriesScreen = lazy(() => import('./screens/CategoriesScreen').then((module) => ({ default: module.CategoriesScreen })))
const GoodsInScreen = lazy(() => import('./screens/GoodsInScreen').then((module) => ({ default: module.GoodsInScreen })))
const OrdersPlacedScreen = lazy(() => import('./screens/OrdersPlacedScreen').then((module) => ({ default: module.OrdersPlacedScreen })))
const StockHealthScreen = lazy(() => import('./screens/StockHealthScreen').then((module) => ({ default: module.StockHealthScreen })))
const StockStatementScreen = lazy(() => import('./screens/StockStatementScreen').then((module) => ({ default: module.StockStatementScreen })))
const SalesByScreen = lazy(() => import('./screens/SalesByScreen').then((module) => ({ default: module.SalesByScreen })))
const StocktakeScreen = lazy(() => import('./screens/StocktakeScreen').then((module) => ({ default: module.StocktakeScreen })))
const DuesScreen = lazy(() => import('./screens/DuesScreen').then((module) => ({ default: module.DuesScreen })))
const StaffSalesScreen = lazy(() => import('./screens/StaffSalesScreen').then((module) => ({ default: module.StaffSalesScreen })))
const TagsScreen = lazy(() => import('./screens/TagsScreen').then((module) => ({ default: module.TagsScreen })))
const SizeReportScreen = lazy(() => import('./screens/SizeReportScreen').then((module) => ({ default: module.SizeReportScreen })))
const SideGroups = lazy(() => import('./components/SideGroups').then((module) => ({ default: module.SideGroups })))
const ChequesScreen = lazy(() => import('./screens/ChequesScreen').then((module) => ({ default: module.ChequesScreen })))
const DayReportScreen = lazy(() => import('./screens/DayReportScreen').then((module) => ({ default: module.DayReportScreen })))
const SalesWeekScreen = lazy(() => import('./screens/SalesInsightsScreens').then((module) => ({ default: module.SalesWeekScreen })))
const CustomerPatternsScreen = lazy(() => import('./screens/SalesInsightsScreens').then((module) => ({ default: module.CustomerPatternsScreen })))
const ItemMarginScreen = lazy(() => import('./screens/ItemMarginScreen').then((module) => ({ default: module.ItemMarginScreen })))
const ReportsScreen = lazy(() => import('./screens/ReportsScreen').then((module) => ({ default: module.ReportsScreen })))
const StaffScreen = lazy(() => import('./screens/StaffScreen').then((module) => ({ default: module.StaffScreen })))
const ExpensesScreen = lazy(() => import('./screens/ExpensesScreen').then((module) => ({ default: module.ExpensesScreen })))
const RatesScreen = lazy(() => import('./screens/RatesScreen').then((module) => ({ default: module.RatesScreen })))
const PaintersScreen = lazy(() => import('./screens/PaintersScreen').then((module) => ({ default: module.PaintersScreen })))
const GstExportScreen = lazy(() => import('./screens/GstExportScreen').then((module) => ({ default: module.GstExportScreen })))
const PickItemsScreen = lazy(() => import('./screens/PickItemsScreen').then((module) => ({ default: module.PickItemsScreen })))
const ProfitLossScreen = lazy(() => import('./screens/ProfitLossScreen').then((module) => ({ default: module.ProfitLossScreen })))
const BalanceSheetScreen = lazy(() => import('./screens/BalanceSheetScreen').then((module) => ({ default: module.BalanceSheetScreen })))
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
const SaleOrdersScreen = lazy(() => import('./screens/SaleOrdersScreen').then((module) => ({ default: module.SaleOrdersScreen })))
const EstimatesScreen = lazy(() => import('./screens/EstimatesScreen').then((module) => ({ default: module.EstimatesScreen })))
const EstimatePrint = lazy(() => import('./screens/EstimatesScreen').then((module) => ({ default: module.EstimatePrint })))
const OffersScreen = lazy(() => import('./screens/OffersScreen').then((module) => ({ default: module.OffersScreen })))
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
const BillLinkPage = lazy(() => import('./screens/PrintScreen').then((module) => ({ default: module.BillLinkPage })))
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
  // A bill sent to a customer: open to anyone holding the link.
  if (path.startsWith('/b/')) {
    return (
      <Suspense fallback={<Loading />}>
        <BillLinkPage path={path} />
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
  // Large screens: the side menu stays out of the way until asked for, and the page keeps its full width.
  // Laptop side menu: open (icons and names) or narrow (icons only), toggled by the panel button and remembered.
  const [menuOpen, setMenuOpenState] = useState(() => { try { return localStorage.getItem('gk-menu') !== 'narrow' } catch { return true } })
  const setMenuOpen = (open: boolean) => { setMenuOpenState(open); try { localStorage.setItem('gk-menu', open ? 'open' : 'narrow') } catch { /* not remembered */ } }

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
  const printMatch = path.match(/^\/print\/(statement|bill|supplier)\/([0-9a-f-]+)(?:\/(\d{4}-\d{2}))?$/)
  const isItemsPage = !!productMatch || path === '/items/pick' || path === '/stocktake' || path === '/stock-statement' || path === '/stock-health' || path === '/categories' || path === '/import' || path === '/items/common'
  const tabs: { path: string; label: string; icon: ReactNode; badge?: number }[] = [
    { path: '/', label: t('home'), icon: <HomeIcon size={22} /> },
    { path: '/khata', label: t('khata'), icon: <KhataIcon size={22} /> },
    { path: '/bill', label: t('bill'), icon: <BillIcon size={22} /> },
    { path: '/inbox', label: t('orders'), icon: <PaymentIcon size={22} />, badge: newOrders },
    { path: '/items', label: t('items'), icon: <ItemsIcon size={22} /> },
  ]
  const isTab = tabs.some((tab) => tab.path === path)
  // Desktop only: the pages a phone reaches from Home, grouped by the job (sell, people, buy, money, stock, reports).
  // Each group folds, and its + starts the group's most common job.
  // The shop's trades decide the trade-only pages: batches and the H1 register for a medical shop, empty bags for grocery.
  const trades = shopTypes(useShopSettings(shopId).data)
  const medical = trades.includes('medical')
  const sideGroups: { key: string; title: string; add?: { path: string; label: string }; links: { path: string; label: string }[] }[] = [
    { key: 'sell', title: t('menu.sell'), add: { path: '/bill', label: t('menu.addBill') }, links: [
      { path: '/estimates', label: t('estimate.link') },
      { path: '/sale-orders', label: t('saleOrder.link') },
      { path: '/holds', label: t('holds.link') },
      { path: '/reports/day', label: t('menu.dayBook') },
      ...(isOwner ? [{ path: '/offers', label: t('offers.link') }] : []),
    ] },
    { key: 'people', title: t('menu.people'), add: { path: '/khata', label: t('menu.addCustomer') }, links: [
      { path: '/dues', label: t('side.dues') },
      { path: '/receipts', label: t('side.receipts') },
      { path: '/reminders', label: t('menu.reminders') },
      { path: '/disputes', label: t('menu.disputes') },
    ] },
    ...(isOwner ? [{ key: 'buy', title: t('menu.buy'), add: { path: '/suppliers', label: t('menu.addPurchase') }, links: [
      { path: '/suppliers', label: t('side.suppliers') },
      { path: '/reorder', label: t('reorder.link') },
      { path: '/orders-placed', label: t('po.link') },
      { path: '/goods-to-come', label: t('gtc.link') },
      { path: '/goods-in', label: t('gi.link') },
      { path: '/godowns', label: t('godown.title') },
      ...(trades.includes('kirana') ? [{ path: '/bardana', label: t('bardana.title') }] : []),
      { path: '/festivals', label: t('fest.title') },
      ...(isOwner ? [{ path: '/lots', label: t('landed.compareTitle') }] : []),
      { path: '/received-by-supplier', label: t('rs.link') },
    ] }] : []),
    { key: 'money', title: t('menu.money'), add: { path: '/expenses', label: t('menu.addExpense') }, links: [
      { path: '/cashbook', label: t('side.cashbook') },
      ...(isOwner ? [{ path: '/cashledger', label: t('cashLedger.title') }] : []),
      ...(isOwner ? [{ path: '/bankbook', label: t('bankBook.title') }] : []),
      { path: '/cheques', label: t('cheques.link') },
      { path: '/expenses', label: t('side.expenses') },
    ] },
    { key: 'stock', title: t('menu.stock'), add: { path: '/stocktake', label: t('menu.addCount') }, links: [
      { path: '/stocktake', label: t('side.stocktake') },
      { path: '/stock-statement', label: t('sl.statement') },
      { path: '/stock-health', label: t('sh.title') },
      ...(medical ? [{ path: '/medical/stock', label: t('batches.link') }, { path: '/medical/h1', label: t('rx.register') }, { path: '/medical/refills', label: t('refills.link') }] : []),
      ...(medical && isOwner ? [{ path: '/medical/returns', label: t('returns.link') }, { path: '/medical/month', label: t('pmonth.link') }] : []),
      ...(isOwner ? [{ path: '/categories', label: t('cat.title') }] : []),
    ] },
    { key: 'reports', title: t('menu.reports'), links: [
      { path: '/reports', label: t('side.reports') },
      { path: '/sales-by', label: t('sl.salesByShort') },
      ...(isOwner ? [{ path: '/pnl', label: t('side.pnl') }, { path: '/balance-sheet', label: t('rv.balance') }, { path: '/gst', label: 'GST' }, { path: '/discounts', label: t('disc.title') }] : []),
    ] },
    { key: 'more', title: t('menu.more'), links: [
      ...(isOwner ? [{ path: '/store', label: t('menu.store') }] : []),
      { path: '/sync', label: t('menu.sync') },
      ...(isOwner ? [{ path: '/settings', label: t('side.settings') }] : []),
    ] },
  ]
  const sideLink = sideGroups.flatMap((g) => g.links).filter((l) => path === l.path || path.startsWith(l.path + '/')).sort((a, b) => b.path.length - a.path.length)[0]
  const pageTitle = tabs.find((tab) => tab.path === path)?.label ?? sideLink?.label
  const unsynced = pending + failed

  const estimatePrint = path.match(/^\/print\/estimate\/([0-9a-f-]+)$/)
  if (estimatePrint) {
    return (
      <Suspense fallback={<Loading />}>
        <EstimatePrint shopId={shopId} shopName={shopName} id={estimatePrint[1]} />
      </Suspense>
    )
  }
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
        <PrintScreen shopId={shopId} shopName={shopName} kind={printMatch[1] as 'statement' | 'bill' | 'supplier'} id={printMatch[2]} month={printMatch[3]} />
      </Suspense>
    )
  }

  return (
    <div className="mx-auto min-h-dvh max-w-xl pb-20 lg:max-w-none lg:pb-6">
      <header className="sticky top-0 z-10 flex items-center gap-2 bg-brand px-4 py-3 text-white lg:h-16 lg:py-0 print:hidden">
        <button type="button" onClick={() => setMenuOpen(!menuOpen)} aria-label={t('a11y.menu')} aria-expanded={menuOpen} className="-ml-2 hidden h-10 w-10 items-center justify-center rounded-lg border border-white/50 lg:flex">
          <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><rect x="3" y="4" width="18" height="16" rx="2.5" /><path d="M9 4v16" /></svg>
        </button>
        <img src="/favicon.svg" alt="" className="hidden h-9 w-9 lg:block" />
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
        <Suspense fallback={<span className="h-11 w-11 shrink-0 rounded-full bg-amber-400" aria-hidden="true" />}><AccountMenu
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
        /></Suspense>
      </header>

      {/* Desktop width: billing and the item list use the room; the other pages stay a readable width. */}
      <main className={`${menuOpen ? 'lg:ml-60' : 'lg:ml-16'} lg:mx-auto ${path === '/bill' || path === '/items' || path === '/items/pick' || path === '/khata' || path === '/dues' || path === '/inbox' || path === '/suppliers' || path === '/settings' || path === '/settings/staff' || customerMatch || orderMatch ? 'lg:max-w-6xl 2xl:max-w-[1500px]' : 'lg:max-w-4xl xl:max-w-5xl'}`}>
        {/* Keyed by screen: a page change shows the loading state at once, never the previous screen lingering. */}
        <Suspense key={customerMatch ? '/khata' : orderMatch ? '/inbox' : path} fallback={<Loading />}>
        {customerMatch ? (
          <ListDetail hasDetail list={<CustomersScreen shopId={shopId} isOwner={isOwner} selectedId={customerMatch[1]} />} detail={<CustomerScreen shopId={shopId} shopName={shopName} customerId={customerMatch[1]} focusEntryId={customerMatch[2]} isOwner={isOwner} userName={userName} />} />
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
          <CashBookScreen shopId={shopId} isOwner={isOwner} />
        ) : path === '/cashledger' && isOwner ? (
          <CashLedgerScreen shopId={shopId} />
        ) : path === '/bankbook' && isOwner ? (
          <BankBookScreen shopId={shopId} />
        ) : path === '/expenses' ? (
          <ExpensesScreen shopId={shopId} shopName={shopName} isOwner={isOwner} />
        ) : path === '/expenses/month' ? (
          <ExpensesScreen shopId={shopId} shopName={shopName} isOwner={isOwner} month />
        ) : path === '/items/pick' ? (
          <PickItemsScreen shopId={shopId} />
        ) : path === '/gst' && isOwner ? (
          <GstExportScreen />
        ) : path === '/reports/staff' && isOwner ? (
          <StaffSalesScreen shopId={shopId} />
        ) : path === '/reports/sizes' && isOwner ? (
          <SizeReportScreen shopId={shopId} />
        ) : path === '/cheques' ? (
          <ChequesScreen isOwner={isOwner} />
        ) : path === '/reports/day' ? (
          <DayReportScreen shopName={shopName} />
        ) : path === '/reports/week' ? (
          <SalesWeekScreen shopId={shopId} shopName={shopName} isOwner={isOwner} />
        ) : path === '/reports/customers' && isOwner ? (
          <CustomerPatternsScreen shopId={shopId} shopName={shopName} />
        ) : path === '/discounts' && isOwner ? (
          <DiscountsScreen />
        ) : path === '/reports/items' && isOwner ? (
          <ItemMarginScreen shopId={shopId} />
        ) : path === '/pnl' && isOwner ? (
          <ProfitLossScreen />
        ) : path === '/balance-sheet' && isOwner ? (
          <BalanceSheetScreen shopId={shopId} />
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
          <H1RegisterScreen shopId={shopId} shopName={shopName} />
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
        ) : path === '/estimates' ? (
          <EstimatesScreen shopId={shopId} />
        ) : path === '/sale-orders' ? (
          <SaleOrdersScreen />
        ) : path === '/offers' && isOwner ? (
          <OffersScreen shopId={shopId} />
        ) : path === '/suppliers' ? (
          <SuppliersScreen shopId={shopId} shopName={shopName} />
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
        ) : path === '/stock-statement' ? (
          <StockStatementScreen shopId={shopId} />
        ) : path === '/goods-to-come' && isOwner ? (
          <GoodsToComeScreen shopId={shopId} shopName={shopName} />
        ) : path === '/lots' && isOwner ? (
          <LotsScreen shopId={shopId} />
        ) : path === '/festivals' ? (
          <FestivalsScreen shopId={shopId} isOwner={isOwner} />
        ) : path === '/bardana' ? (
          <BardanaScreen shopId={shopId} isOwner={isOwner} />
        ) : path === '/godowns' ? (
          <GodownsScreen shopId={shopId} isOwner={isOwner} />
        ) : path === '/holds' ? (
          <HoldsScreen shopId={shopId} />
        ) : path === '/received-by-supplier' && isOwner ? (
          <ReceivedBySupplierScreen shopId={shopId} />
        ) : path === '/categories' ? (
          <CategoriesScreen shopId={shopId} />
        ) : path === '/goods-in' ? (
          <GoodsInScreen shopId={shopId} shopName={shopName} isOwner={isOwner} />
        ) : path === '/orders-placed' ? (
          <OrdersPlacedScreen shopId={shopId} />
        ) : path === '/stock-health' ? (
          <StockHealthScreen shopId={shopId} />
        ) : path === '/sales-by' ? (
          <SalesByScreen />
        ) : path === '/items/common' ? (
          <CommonListScreen shopId={shopId} />
        ) : path === '/receipts' ? (
          <ReceiptsScreen shopId={shopId} isOwner={isOwner} />
        ) : path === '/reminders' ? (
          <RemindersScreen shopId={shopId} shopName={shopName} />
        ) : path === '/khata/import' ? (
          <CustomerImportScreen shopId={shopId} />
        ) : path === '/import' ? (
          <ImportScreen shopId={shopId} />
        ) : orderMatch ? (
          <ListDetail hasDetail list={<InboxScreen shopId={shopId} selectedId={orderMatch[1]} />} detail={<OrderScreen shopId={shopId} orderId={orderMatch[1]} />} />
        ) : path === '/inbox' ? (
          <ListDetail hasDetail={false} list={<InboxScreen shopId={shopId} />} detail={null} placeholder={t('inboxPick')} />
        ) : path === '/khata' ? (
          <ListDetail hasDetail={false} list={<CustomersScreen shopId={shopId} isOwner={isOwner} />} detail={null} placeholder={t('khataPick')} />
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

      <Suspense fallback={null}><AppBanners /></Suspense>

      <nav className={`fixed inset-x-0 bottom-0 z-10 mx-auto grid h-[68px] max-w-xl grid-cols-5 border-t border-line bg-white pb-[env(safe-area-inset-bottom)] lg:top-16 lg:bottom-0 lg:right-auto lg:z-20 print:hidden lg:mx-0 lg:h-auto ${menuOpen ? 'lg:w-60' : 'lg:w-16 lg:px-2'} lg:max-w-none lg:auto-rows-auto lg:grid-cols-1 lg:content-start lg:gap-1 lg:overflow-y-auto lg:border-t-0 lg:border-r lg:px-3 lg:pt-3 lg:pb-6 lg:[scrollbar-width:thin] lg:[&>button]:min-h-12`}>
        {tabs.map((tab) => (
          <button
            key={tab.path}
            onClick={() => navigate(tab.path)}
            title={tab.label}
            aria-current={path === tab.path ? 'page' : undefined}
            className={`relative flex flex-col items-center justify-center gap-0.5 text-[13px] lg:flex-row ${menuOpen ? 'lg:justify-start lg:px-4' : 'lg:justify-center lg:px-0'} lg:gap-3 lg:rounded-xl lg:text-base ${
              path === tab.path ? 'font-bold text-brand lg:bg-brand-soft' : 'font-semibold text-muted lg:hover:bg-[#f1f5f4]'
            }`}
          >
            {tab.icon}
            <span className={menuOpen ? '' : 'lg:sr-only'}>{tab.label}</span>
            {!!tab.badge && (
              <span className={`absolute top-1.5 right-[calc(50%-22px)] min-w-5 ${menuOpen ? 'lg:static lg:ml-auto' : 'lg:top-0.5 lg:right-0.5'} rounded-full bg-udhaar px-1.5 text-xs font-bold text-white`} aria-label={`${tab.badge}`}>
                {tab.badge}
              </span>
            )}
          </button>
        ))}
        <Suspense fallback={null}><SideGroups groups={sideGroups} open={menuOpen} current={sideLink?.path} /></Suspense>
      </nav>
    </div>
  )
}

function Loading() {
  return <p className="p-6 text-center text-muted" role="status">…</p>
}

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
const ReportsScreen = lazy(() => import('./screens/ReportsScreen').then((module) => ({ default: module.ReportsScreen })))
const StaffScreen = lazy(() => import('./screens/StaffScreen').then((module) => ({ default: module.StaffScreen })))
const ExpensesScreen = lazy(() => import('./screens/ExpensesScreen').then((module) => ({ default: module.ExpensesScreen })))
const RatesScreen = lazy(() => import('./screens/RatesScreen').then((module) => ({ default: module.RatesScreen })))
const PaintersScreen = lazy(() => import('./screens/PaintersScreen').then((module) => ({ default: module.PaintersScreen })))
const MyPlanScreen = lazy(() => import('./screens/MyPlanScreen').then((module) => ({ default: module.MyPlanScreen })))
const SettingsScreen = lazy(() => import('./screens/SettingsScreen').then((module) => ({ default: module.SettingsScreen })))
const MyStoreScreen = lazy(() => import('./screens/MyStoreScreen').then((module) => ({ default: module.MyStoreScreen })))
const StoreApp = lazy(() => import('./screens/store/StoreApp').then((module) => ({ default: module.StoreApp })))
const KhataLinkPage = lazy(() => import('./screens/khata/KhataViews').then((module) => ({ default: module.KhataLinkPage })))
const AgentScreen = lazy(() => import('./screens/AgentScreen').then((module) => ({ default: module.AgentScreen })))
const PlanBillPage = lazy(() => import('./screens/PlanBillPage').then((module) => ({ default: module.PlanBillPage })))
const KharchaPrint = lazy(() => import('./screens/KharchaMonth').then((module) => ({ default: module.KharchaPrint })))
const PlatformScreen = lazy(() => import('./screens/PlatformScreen').then((module) => ({ default: module.PlatformScreen })))
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
  // A customer's private khata link: read-only, no account needed.
  if (path.startsWith('/k/')) {
    return (
      <Suspense fallback={<Loading />}>
        <KhataLinkPage path={path} />
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
    if (pending + failed > 0 && !confirm(t('unsyncedSignOut', { count: pending + failed }))) return
    void request('/v1/auth/sign-out', 'POST', { refreshToken: getSession()?.refreshToken }).catch(() => {})
    setSession(null)
  }

  const customerMatch = path.match(/^\/c\/([0-9a-f-]+)(?:\?e=([0-9a-f-]+))?$/)
  const orderMatch = path.match(/^\/o\/([0-9a-f-]+)$/)
  const productMatch = path.match(/^\/p\/([0-9a-f-]+)$/)
  const printMatch = path.match(/^\/print\/(statement|bill)\/([0-9a-f-]+)(?:\/(\d{4}-\d{2}))?$/)
  const isItemsPage = !!productMatch || path === '/stocktake' || path === '/import' || path === '/items/common'
  const tabs: { path: string; label: string; icon: ReactNode; badge?: number }[] = [
    { path: '/', label: t('home'), icon: <HomeIcon size={22} /> },
    { path: '/khata', label: t('khata'), icon: <KhataIcon size={22} /> },
    { path: '/bill', label: t('bill'), icon: <BillIcon size={22} /> },
    { path: '/inbox', label: t('orders'), icon: <PaymentIcon size={22} />, badge: newOrders },
    { path: '/items', label: t('items'), icon: <ItemsIcon size={22} /> },
  ]
  const isTab = tabs.some((tab) => tab.path === path)
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
        <PrintScreen shopName={shopName} kind={printMatch[1] as 'statement' | 'bill'} id={printMatch[2]} month={printMatch[3]} />
      </Suspense>
    )
  }

  return (
    <div className="mx-auto min-h-dvh max-w-xl pb-20">
      <header className="sticky top-0 z-10 flex items-center gap-2 bg-teal-700 px-4 py-3 text-white">
        {!isTab && (
          <button onClick={() => navigate(customerMatch ? '/khata' : orderMatch ? '/inbox' : isItemsPage ? '/items' : path === '/settings/staff' ? '/settings' : '/')} className="-ml-2 flex h-11 w-11 items-center justify-center" aria-label={t('a11y.back')}>
            <BackIcon />
          </button>
        )}
        <div className="flex min-w-0 flex-1 flex-col">
          {path === '/' && <span className="text-sm opacity-85">{t('greeting', { name: userName.split(' ')[0] })}</span>}
          <h1 className="truncate text-xl font-bold">{shopName}</h1>
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
            ...(isOwner ? [{ label: t('plan2.myPlan'), onClick: () => navigate('/plan') }] : []),
            { label: t('plan.householdLists'), onClick: onHousehold },
            { label: t('account.syncStatus'), onClick: () => navigate('/sync') },
          ]}
        />
      </header>

      <main>
        <Suspense fallback={<Loading />}>
        {customerMatch ? (
          <CustomerScreen shopId={shopId} shopName={shopName} customerId={customerMatch[1]} focusEntryId={customerMatch[2]} isOwner={isOwner} userName={userName} />
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
        ) : path === '/plan' ? (
          <MyPlanScreen shopId={shopId} />
        ) : path === '/rates' ? (
          <RatesScreen shopId={shopId} />
        ) : path === '/painters' ? (
          <PaintersScreen shopId={shopId} shopName={shopName} />
        ) : productMatch ? (
          <ProductScreen shopId={shopId} productId={productMatch[1]} />
        ) : path === '/stocktake' ? (
          <StocktakeScreen shopId={shopId} />
        ) : path === '/items/common' ? (
          <CommonListScreen shopId={shopId} />
        ) : path === '/import' ? (
          <ImportScreen shopId={shopId} />
        ) : orderMatch ? (
          <OrderScreen shopId={shopId} orderId={orderMatch[1]} />
        ) : path === '/inbox' ? (
          <InboxScreen shopId={shopId} />
        ) : path === '/khata' ? (
          <CustomersScreen shopId={shopId} />
        ) : path === '/disputes' ? (
          <DisputesScreen isOwner={isOwner} />
        ) : path === '/bill' ? (
          <BillScreen shopId={shopId} shopName={shopName} />
        ) : path === '/items' ? (
          <ItemsScreen shopId={shopId} />
        ) : path === '/sync' ? (
          <SyncScreen shopId={shopId} persistent={persistent} />
        ) : (
          <HomeScreen shopId={shopId} isOwner={isOwner} />
        )}
        </Suspense>
      </main>

      <AppBanners />

      <nav className="fixed inset-x-0 bottom-0 z-10 mx-auto grid h-[68px] max-w-xl grid-cols-5 border-t border-line bg-white pb-[env(safe-area-inset-bottom)]">
        {tabs.map((tab) => (
          <button
            key={tab.path}
            onClick={() => navigate(tab.path)}
            aria-current={path === tab.path ? 'page' : undefined}
            className={`relative flex flex-col items-center justify-center gap-0.5 text-[13px] ${
              path === tab.path ? 'font-bold text-teal-700' : 'font-semibold text-muted'
            }`}
          >
            {tab.icon}
            {tab.label}
            {!!tab.badge && (
              <span className="absolute top-1.5 right-[calc(50%-22px)] min-w-5 rounded-full bg-udhaar px-1.5 text-xs font-bold text-white" aria-label={`${tab.badge}`}>
                {tab.badge}
              </span>
            )}
          </button>
        ))}
      </nav>
    </div>
  )
}

function Loading() {
  return <p className="p-6 text-center text-muted" role="status">…</p>
}

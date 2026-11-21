import { lazy, Suspense, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { AppBanners } from '../../components/AppBanners'
import { BackIcon, CheckIcon, CloudOffIcon } from '../../components/Icons'
import { request } from '../../lib/api'
import { useCarts } from '../../lib/customer'
import { useOutbox, useSession } from '../../lib/data'
import { requestPersistentStorage } from '../../lib/db'
import { flush } from '../../lib/outbox'
import { homeKey, useMyOrders, type ListKind } from '../../lib/planner'
import { getSession, setSession } from '../../lib/session'
import { navigate, usePath } from '../../router'
import { BlockedShopsNotice } from '../AuthScreen'
import { ListScreen } from './ListScreen'
import { KhataOrders, MyOrdersScreen, OrderDetail } from './MyOrdersScreen'
import { PlanHomeScreen } from './PlanHomeScreen'
import { SendScreen } from './SendScreen'
import { AiOrderScreen } from './AiOrderScreen'
import { MyKhataDetail, MyKhataScreen } from '../khata/KhataViews'
import { confirmAsk } from '../../components/Ask'
import { AccountScreen, AddressesScreen, CartScreen, LanguageScreen, PrivacyScreen, SavedItemsScreen, SavedShopsScreen, SearchScreen } from './CustomerScreens'

const StoreApp = lazy(() => import('../store/StoreApp').then((m) => ({ default: m.StoreApp })))

// The five tabs every customer screen shares, like the big shopping apps: bottom bar on phones, a side rail on computers.
const ICON: Record<string, string> = {
  home: 'M3 11l9-8 9 8M5 10v10h5v-6h4v6h5V10',
  search: 'M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14zM20 20l-4-4',
  orders: 'M6 3h12v18l-3-2-3 2-3-2-3 2zM9 8h6M9 12h6',
  cart: 'M3 4h2l2.4 11h11L21 7H6.2M9 20a1 1 0 1 0 0-2 1 1 0 0 0 0 2zM18 20a1 1 0 1 0 0-2 1 1 0 0 0 0 2z',
  account: 'M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM4 21a8 8 0 0 1 16 0',
}

/** The customer app: shops, lists, carts, orders, khata and account, with the same five tabs on every screen (shop pages too). */
export function PlannerApp({ userId, userName, onSwitchToShop }: { userId: string; userName: string; onSwitchToShop?: () => void }) {
  const { t } = useTranslation()
  const path = usePath()
  const session = useSession()
  const key = homeKey(userId)
  const { pending, failed, state } = useOutbox(key)
  const carts = useCarts().data ?? []
  const { orders } = useMyOrders(userId)
  // An order waiting for the customer's OK on a change: a badge on Orders.
  const waiting = orders.filter((o) => o.status === 'packed' && o.lines.some((l) => l.approval === 'pending')).length

  useEffect(() => {
    void requestPersistentStorage()
    const sync = () => void flush(key)
    sync()
    window.addEventListener('online', sync)
    const timer = setInterval(sync, 30_000)
    return () => {
      window.removeEventListener('online', sync)
      clearInterval(timer)
    }
  }, [key])

  async function signOut() {
    if (pending + failed > 0 && !(await confirmAsk(t('unsyncedSignOut', { count: pending + failed })))) return
    void request('/v1/auth/sign-out', 'POST', { refreshToken: getSession()?.refreshToken }).catch(() => {})
    setSession(null)
  }

  const listMatch = path.match(/^\/list\/(monthly|weekly)$/)
  const sendMatch = path.match(/^\/send\/(monthly|weekly)$/)
  const khataMatch = path.match(/^\/khata\/([0-9a-f-]+)\/([0-9a-f-]+)$/)
  const orderMatch = path.match(/^\/orders\/([0-9a-f-]+)$/)
  const inShop = path.startsWith('/s/')
  const tabs = [
    { to: '/', key: 'home', label: t('cust.tab.home'), active: path === '/' || !!listMatch || !!sendMatch || path === '/ai' },
    { to: '/search', key: 'search', label: t('cust.tab.search'), active: path === '/search' },
    { to: '/orders', key: 'orders', label: t('cust.tab.orders'), active: path === '/orders' || !!orderMatch, badge: waiting },
    { to: '/cart', key: 'cart', label: t('cust.tab.cart'), active: path === '/cart', badge: carts.length },
    { to: '/account', key: 'account', label: t('cust.tab.account'), active: ['/account', '/addresses', '/language', '/shops', '/khata', '/saved', '/privacy'].includes(path) || !!khataMatch },
  ]
  const isTab = ['/', '/search', '/orders', '/cart', '/account'].includes(path)
  // Each screen says what it is.
  const title = listMatch ? t(`plan.${listMatch[1]}List`) : sendMatch ? t('orders2.sendTitle') : orderMatch ? t('cust.orderTitle')
    : path === '/orders' ? t('orders') : path === '/khata' || khataMatch ? t('myKhata.title') : path === '/ai' ? t('ai.title')
    : path === '/search' ? t('cust.tab.search') : path === '/cart' ? t('cust.myCarts') : path === '/account' ? t('cust.tab.account')
    : path === '/addresses' ? t('cust.addresses') : path === '/language' ? t('cust.language') : path === '/shops' ? t('cust.savedShops') : path === '/saved' ? t('cust4.savedItems') : path === '/privacy' ? t('cust4.privacy') : t('plan.title')
  // Back goes to the screen the person came from; opened straight from a link, to the screen above this one.
  const back = () => {
    if (window.history.length > 1) window.history.back()
    else navigate(sendMatch ? `/list/${sendMatch[1]}` : khataMatch ? '/khata' : orderMatch ? '/orders' : ['/addresses', '/language', '/shops', '/khata', '/saved', '/privacy'].includes(path) ? '/account' : '/')
  }
  const unsynced = pending + failed

  return (
    <div className="min-h-dvh pb-20 lg:pb-6 lg:pl-60">
      {/* A shop's page brings its own header (name, search); every other screen has this one. */}
      {!inShop && (
        <header className="sticky top-0 z-10 flex items-center gap-2 bg-brand px-4 py-3 text-white lg:h-16 lg:py-0 print:hidden">
          {!isTab && (
            <button onClick={back} className="-ml-2 flex h-11 w-11 items-center justify-center" aria-label={t('a11y.back')}>
              <BackIcon />
            </button>
          )}
          <div className="flex min-w-0 flex-1 flex-col">
            {path === '/' && <span className="text-sm opacity-85">{t('greeting', { name: userName.split(' ')[0] })}</span>}
            <h1 className="truncate text-xl font-bold">{title}</h1>
          </div>
          <span className={`flex h-11 items-center gap-1 rounded-full px-3 text-[13px] font-semibold ${unsynced > 0 || state === 'offline' ? 'bg-amber-400 text-amber-950' : 'bg-white/20'}`}>
            {state === 'offline' ? <CloudOffIcon size={16} /> : unsynced === 0 ? <CheckIcon size={16} /> : null}
            {unsynced > 0 ? t('waiting', { count: unsynced }) : state === 'offline' ? t('pwa.offline') : t('synced')}
          </span>
        </header>
      )}

      <main className={inShop ? '' : 'mx-auto max-w-xl lg:max-w-4xl'}>
        {path === '/' && session && <BlockedShopsNotice session={session} />}
        {inShop ? (
          <Suspense fallback={<p className="p-8 text-center text-muted">…</p>}><StoreApp path={path} /></Suspense>
        ) : listMatch ? (
          <ListScreen userId={userId} kind={listMatch[1] as ListKind} />
        ) : sendMatch ? (
          <SendScreen userId={userId} userName={userName} kind={sendMatch[1] as ListKind} />
        ) : orderMatch ? (
          <OrderDetail userId={userId} orderId={orderMatch[1]} />
        ) : path === '/orders' ? (
          <MyOrdersScreen userId={userId} />
        ) : khataMatch ? (
          <MyKhataDetail shopId={khataMatch[1]} customerId={khataMatch[2]} extra={<KhataOrders userId={userId} shopId={khataMatch[1]} />} />
        ) : path === '/ai' ? (
          <AiOrderScreen userId={userId} />
        ) : path === '/khata' ? (
          <MyKhataScreen />
        ) : path === '/search' ? (
          <SearchScreen />
        ) : path === '/cart' ? (
          <CartScreen />
        ) : path === '/account' ? (
          <AccountScreen onSignOut={() => void signOut()} onSwitchToShop={onSwitchToShop} />
        ) : path === '/addresses' ? (
          <AddressesScreen />
        ) : path === '/language' ? (
          <LanguageScreen />
        ) : path === '/shops' ? (
          <SavedShopsScreen />
        ) : path === '/saved' ? (
          <SavedItemsScreen />
        ) : path === '/privacy' ? (
          <PrivacyScreen />
        ) : (
          <PlanHomeScreen userId={userId} userName={userName} />
        )}
      </main>

      <AppBanners />

      <nav aria-label={t('a11y.menu')} className="fixed inset-x-0 bottom-0 z-20 mx-auto grid h-[68px] max-w-xl grid-cols-5 border-t sm:rounded-t-2xl sm:border-x sm:shadow-[0_-4px_16px_rgba(16,32,29,0.06)] lg:mx-0 lg:max-w-none lg:rounded-none lg:border-l-0 lg:shadow-none border-line bg-white pb-[env(safe-area-inset-bottom)] print:hidden lg:inset-y-0 lg:right-auto lg:h-auto lg:w-60 lg:auto-rows-[56px] lg:grid-cols-1 lg:content-start lg:gap-1 lg:border-t-0 lg:border-r lg:px-3 lg:pt-4">
        <div className="hidden items-center gap-2 px-2 pb-2 text-lg font-extrabold text-brand lg:flex"><img src="/favicon.svg" alt="" className="h-8 w-8" />Grocery Khata</div>
        {tabs.map((tab) => (
          <button
            key={tab.to}
            onClick={() => navigate(tab.to)}
            aria-current={tab.active ? 'page' : undefined}
            className={`relative flex flex-col items-center justify-center gap-0.5 text-[12px] lg:flex-row lg:justify-start lg:gap-3 lg:rounded-xl lg:px-4 lg:text-base ${tab.active ? 'font-bold text-brand lg:bg-brand-soft' : 'font-semibold text-muted lg:hover:bg-[#f1f5f4]'}`}
          >
            <span className="relative">
              <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden><path d={ICON[tab.key]} /></svg>
              {!!tab.badge && <span className="absolute -right-2.5 -top-1.5 min-w-[18px] rounded-full bg-due px-1 text-center text-[10px] font-bold leading-[18px] text-white">{tab.badge}</span>}
            </span>
            {tab.label}
          </button>
        ))}
      </nav>
    </div>
  )
}

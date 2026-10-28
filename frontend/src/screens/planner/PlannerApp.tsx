import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { AppBanners } from '../../components/AppBanners'
import { BackIcon, CheckIcon, CloudOffIcon, KhataIcon, BillIcon, PaymentIcon } from '../../components/Icons'
import { setLanguage } from '../../i18n'
import { request } from '../../lib/api'
import { useOutbox } from '../../lib/data'
import { requestPersistentStorage } from '../../lib/db'
import { flush } from '../../lib/outbox'
import { homeKey, type ListKind } from '../../lib/planner'
import { getSession, setSession } from '../../lib/session'
import { navigate, usePath } from '../../router'
import { ListScreen } from './ListScreen'
import { MyOrdersScreen } from './MyOrdersScreen'
import { PlanHomeScreen } from './PlanHomeScreen'
import { SendScreen } from './SendScreen'
import { AiOrderScreen } from './AiOrderScreen'
import { MyKhataDetail, MyKhataScreen } from '../khata/KhataViews'

/** The household app: plan monthly and weekly lists, send one to a shop, follow the order. */
export function PlannerApp({ userId, userName, onSwitchToShop }: { userId: string; userName: string; onSwitchToShop?: () => void }) {
  const { t, i18n } = useTranslation()
  const path = usePath()
  const key = homeKey(userId)
  const { pending, failed, state } = useOutbox(key)

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

  function signOut() {
    if (pending + failed > 0 && !confirm(t('unsyncedSignOut', { count: pending + failed }))) return
    void request('/v1/auth/sign-out', 'POST', { refreshToken: getSession()?.refreshToken }).catch(() => {})
    setSession(null)
  }

  const listMatch = path.match(/^\/list\/(monthly|weekly)$/)
  const sendMatch = path.match(/^\/send\/(monthly|weekly)$/)
  const khataMatch = path.match(/^\/khata\/([0-9a-f-]+)\/([0-9a-f-]+)$/)
  const isTab = path === '/' || path === '/orders' || path === '/khata'
  const unsynced = pending + failed

  return (
    <div className="mx-auto min-h-dvh max-w-xl pb-20">
      <header className="sticky top-0 z-10 flex items-center gap-2 bg-teal-700 px-4 py-3 text-white">
        {!isTab && (
          <button onClick={() => navigate(sendMatch ? `/list/${sendMatch[1]}` : khataMatch ? '/khata' : '/')} className="-ml-2 flex h-11 w-11 items-center justify-center" aria-label={t('a11y.back')}>
            <BackIcon />
          </button>
        )}
        <div className="flex min-w-0 flex-1 flex-col">
          {path === '/' && <span className="text-sm opacity-85">{t('greeting', { name: userName.split(' ')[0] })}</span>}
          <h1 className="truncate text-xl font-bold">{t('plan.title')}</h1>
        </div>
        <span className={`flex h-11 items-center gap-1 rounded-full px-3 text-[13px] font-semibold ${unsynced > 0 || state === 'offline' ? 'bg-amber-400 text-amber-950' : 'bg-white/20'}`}>
          {state === 'offline' ? <CloudOffIcon size={16} /> : unsynced === 0 ? <CheckIcon size={16} /> : null}
          {unsynced > 0 ? t('waiting', { count: unsynced }) : state === 'offline' ? t('pwa.offline') : t('synced')}
        </span>
        <button onClick={() => setLanguage(i18n.language === 'hi' ? 'en' : 'hi')} className="h-11 rounded-full bg-white/20 px-3 text-sm font-semibold">
          {i18n.language === 'hi' ? 'EN' : 'हि'}
        </button>
      </header>

      <main>
        {listMatch ? (
          <ListScreen userId={userId} kind={listMatch[1] as ListKind} />
        ) : sendMatch ? (
          <SendScreen userId={userId} userName={userName} kind={sendMatch[1] as ListKind} />
        ) : path === '/orders' ? (
          <MyOrdersScreen userId={userId} />
        ) : khataMatch ? (
          <MyKhataDetail shopId={khataMatch[1]} customerId={khataMatch[2]} />
        ) : path === '/ai' ? (
          <AiOrderScreen userId={userId} />
        ) : path === '/khata' ? (
          <MyKhataScreen />
        ) : (
          <PlanHomeScreen userId={userId} />
        )}
        {path === '/' && (
          <div className="mx-4 mt-6 flex flex-wrap justify-center gap-3 text-sm">
            {onSwitchToShop && (
              <button onClick={onSwitchToShop} className="min-h-11 px-2 font-semibold text-teal-700 underline">{t('plan.iAmShop')}</button>
            )}
            <button onClick={signOut} className="min-h-11 px-2 font-semibold text-muted underline">{t('signOut')}</button>
          </div>
        )}
      </main>

      <AppBanners />

      <nav className="fixed inset-x-0 bottom-0 z-10 mx-auto grid h-[68px] max-w-xl grid-cols-3 border-t border-line bg-white pb-[env(safe-area-inset-bottom)]">
        {[
          { to: '/', label: t('plan.lists'), icon: <KhataIcon size={22} />, active: path === '/' || !!listMatch || !!sendMatch },
          { to: '/orders', label: t('orders'), icon: <BillIcon size={22} />, active: path === '/orders' },
          { to: '/khata', label: t('myKhata.title'), icon: <PaymentIcon size={22} />, active: path === '/khata' || !!khataMatch },
        ].map((tab) => (
          <button
            key={tab.to}
            onClick={() => navigate(tab.to)}
            aria-current={tab.active ? 'page' : undefined}
            className={`flex flex-col items-center justify-center gap-0.5 text-[13px] ${tab.active ? 'font-bold text-teal-700' : 'font-semibold text-muted'}`}
          >
            {tab.icon}
            {tab.label}
          </button>
        ))}
      </nav>
    </div>
  )
}

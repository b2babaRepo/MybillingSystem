import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { AccountMenu } from '../../components/AccountMenu'
import { AppBanners } from '../../components/AppBanners'
import { BackIcon, CheckIcon, CloudOffIcon, KhataIcon, BillIcon, PaymentIcon } from '../../components/Icons'
import { request } from '../../lib/api'
import { useOutbox, useSession } from '../../lib/data'
import { requestPersistentStorage } from '../../lib/db'
import { flush } from '../../lib/outbox'
import { homeKey, type ListKind } from '../../lib/planner'
import { getSession, setSession } from '../../lib/session'
import { navigate, usePath } from '../../router'
import { BlockedShopsNotice } from '../AuthScreen'
import { ListScreen } from './ListScreen'
import { MyOrdersScreen } from './MyOrdersScreen'
import { PlanHomeScreen } from './PlanHomeScreen'
import { SendScreen } from './SendScreen'
import { AiOrderScreen } from './AiOrderScreen'
import { MyKhataDetail, MyKhataScreen } from '../khata/KhataViews'
import { confirmAsk } from '../../components/Ask'

/** The household app: plan monthly and weekly lists, send one to a shop, follow the order. */
export function PlannerApp({ userId, userName, onSwitchToShop }: { userId: string; userName: string; onSwitchToShop?: () => void }) {
  const { t } = useTranslation()
  const path = usePath()
  const session = useSession()
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

  async function signOut() {
    if (pending + failed > 0 && !(await confirmAsk(t('unsyncedSignOut', { count: pending + failed })))) return
    void request('/v1/auth/sign-out', 'POST', { refreshToken: getSession()?.refreshToken }).catch(() => {})
    setSession(null)
  }

  const listMatch = path.match(/^\/list\/(monthly|weekly)$/)
  const sendMatch = path.match(/^\/send\/(monthly|weekly)$/)
  const khataMatch = path.match(/^\/khata\/([0-9a-f-]+)\/([0-9a-f-]+)$/)
  const isTab = path === '/' || path === '/orders' || path === '/khata'
  const unsynced = pending + failed
  const [menuOpen, setMenuOpen] = useState(false)

  return (
    <div className="mx-auto min-h-dvh max-w-xl pb-20 lg:max-w-none lg:pb-6">
      <header className="sticky top-0 z-10 flex items-center gap-2 bg-brand px-4 py-3 text-white lg:h-16 lg:py-0 print:hidden">
        <button type="button" onClick={() => setMenuOpen(!menuOpen)} aria-label={t('a11y.menu')} aria-expanded={menuOpen} className="-ml-2 hidden h-11 w-11 items-center justify-center lg:flex">
          <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true"><path d="M4 6h16M4 12h16M4 18h16" /></svg>
        </button>
        <img src="/favicon.svg" alt="" className="hidden h-9 w-9 lg:block" />
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
        <AccountMenu
          name={userName}
          role={t('account.household')}
          unsynced={unsynced}
          onSignOut={signOut}
          actions={[
            { label: t('account.myKhata'), onClick: () => navigate('/khata') },
            { label: t('teamLink'), onClick: () => navigate('/team') },
            ...(onSwitchToShop ? [{ label: t('plan.iAmShop'), onClick: onSwitchToShop }] : []),
          ]}
        />
      </header>

      <main className="lg:mx-auto lg:max-w-4xl">
        {path === '/' && session && <BlockedShopsNotice session={session} />}
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
          <PlanHomeScreen userId={userId} userName={userName} />
        )}
      </main>

      <AppBanners />

      {menuOpen && <button type="button" onClick={() => setMenuOpen(false)} aria-label={t('a11y.close')} className="fixed inset-x-0 top-16 bottom-0 z-10 hidden bg-black/25 lg:block" />}
      <nav onClick={() => setMenuOpen(false)} className={`fixed inset-x-0 bottom-0 z-10 mx-auto grid h-[68px] max-w-xl grid-cols-3 border-t border-line bg-white pb-[env(safe-area-inset-bottom)] lg:top-16 lg:bottom-0 lg:right-auto lg:z-20 lg:shadow-xl print:hidden lg:mx-0 lg:h-auto lg:w-60 lg:max-w-none lg:auto-rows-[56px] lg:grid-cols-1 lg:content-start lg:gap-1 lg:border-t-0 lg:border-r lg:px-3 lg:pt-3 ${menuOpen ? '' : 'lg:hidden'}`}>
        {[
          { to: '/', label: t('plan.lists'), icon: <KhataIcon size={22} />, active: path === '/' || !!listMatch || !!sendMatch },
          { to: '/orders', label: t('orders'), icon: <BillIcon size={22} />, active: path === '/orders' },
          { to: '/khata', label: t('myKhata.title'), icon: <PaymentIcon size={22} />, active: path === '/khata' || !!khataMatch },
        ].map((tab) => (
          <button
            key={tab.to}
            onClick={() => navigate(tab.to)}
            aria-current={tab.active ? 'page' : undefined}
            className={`flex flex-col items-center justify-center gap-0.5 text-[13px] lg:flex-row lg:justify-start lg:gap-3 lg:rounded-xl lg:px-4 lg:text-base ${tab.active ? 'font-bold text-brand lg:bg-brand-soft' : 'font-semibold text-muted lg:hover:bg-[#f1f5f4]'}`}
          >
            {tab.icon}
            {tab.label}
          </button>
        ))}
      </nav>
    </div>
  )
}

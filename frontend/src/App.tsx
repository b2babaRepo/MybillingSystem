import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { setLanguage } from './i18n'
import { request } from './lib/api'
import { useOutbox, useSession } from './lib/data'
import { requestPersistentStorage } from './lib/db'
import { flush } from './lib/outbox'
import { getSession, setSession } from './lib/session'
import { navigate, usePath } from './router'
import { AuthScreen, ShopPicker } from './screens/AuthScreen'
import { BillScreen } from './screens/BillScreen'
import { CustomerScreen } from './screens/CustomerScreen'
import { CustomersScreen } from './screens/CustomersScreen'
import { ItemsScreen } from './screens/ItemsScreen'
import { SyncScreen } from './screens/SyncScreen'

export default function App() {
  const session = useSession()
  if (!session) return <AuthScreen />
  if (!session.shopId) return <ShopPicker session={session} />
  const shop = session.memberships.find((m) => m.shopId === session.shopId)
  return <Shop shopId={session.shopId} shopName={shop?.shopName ?? ''} />
}

function Shop({ shopId, shopName }: { shopId: string; shopName: string }) {
  const { t, i18n } = useTranslation()
  const path = usePath()
  const { pending, failed, state } = useOutbox(shopId)
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

  const customerMatch = path.match(/^\/c\/([0-9a-f-]+)$/)
  const tabs = [
    { path: '/', label: t('khata') },
    { path: '/bill', label: t('bill') },
    { path: '/items', label: t('items') },
    { path: '/sync', label: t('sync'), badge: pending + failed },
  ]

  return (
    <div className="mx-auto min-h-dvh max-w-xl pb-20">
      <header className="sticky top-0 z-10 flex items-center gap-2 bg-teal-700 px-4 py-3 text-white">
        {path !== '/' && (
          <button onClick={() => navigate('/')} className="text-2xl" aria-label="Back">←</button>
        )}
        <h1 className="flex-1 truncate text-lg font-bold">{shopName}</h1>
        {state === 'offline' && <span className="rounded-full bg-amber-400 px-2 py-0.5 text-xs font-bold text-amber-950">offline</span>}
        <button onClick={() => setLanguage(i18n.language === 'hi' ? 'en' : 'hi')} className="rounded-lg bg-teal-800 px-2 py-1 text-sm">
          {i18n.language === 'hi' ? 'EN' : 'हि'}
        </button>
        <button onClick={signOut} className="rounded-lg bg-teal-800 px-2 py-1 text-sm">{t('signOut')}</button>
      </header>

      <main>
        {customerMatch ? (
          <CustomerScreen shopId={shopId} shopName={shopName} customerId={customerMatch[1]} />
        ) : path === '/bill' ? (
          <BillScreen shopId={shopId} />
        ) : path === '/items' ? (
          <ItemsScreen shopId={shopId} />
        ) : path === '/sync' ? (
          <SyncScreen shopId={shopId} persistent={persistent} />
        ) : (
          <CustomersScreen shopId={shopId} />
        )}
      </main>

      <nav className="fixed inset-x-0 bottom-0 z-10 mx-auto grid max-w-xl grid-cols-4 border-t border-slate-200 bg-white pb-[env(safe-area-inset-bottom)]">
        {tabs.map((tab) => (
          <button
            key={tab.path}
            onClick={() => navigate(tab.path)}
            className={`relative h-16 text-base font-semibold ${path === tab.path ? 'text-teal-700' : 'text-slate-500'}`}
          >
            {tab.label}
            {!!tab.badge && (
              <span className="absolute top-2 right-4 rounded-full bg-amber-500 px-1.5 text-xs text-white">{tab.badge}</span>
            )}
          </button>
        ))}
      </nav>
    </div>
  )
}

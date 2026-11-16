import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import { AskHost } from './components/Ask'
import { i18nReady } from './i18n'
import './index.css'
import { installSyncHandler } from './lib/data'
import { getSession, subscribeSession } from './lib/session'
import './lib/pwa'

// A tab left open across an update asks for files the update replaced: without this it goes white. Once only,
// so a real outage shows its error instead of reloading for ever.
window.addEventListener('vite:preloadError', () => {
  try {
    if (Date.now() - Number(sessionStorage.getItem('gk-update-reload') ?? 0) < 30_000) return
    sessionStorage.setItem('gk-update-reload', String(Date.now()))
  } catch { /* storage blocked: reload anyway */ }
  window.location.reload()
})

const queryClient = new QueryClient({
  defaultOptions: {
    // Query functions fall back to the saved copy themselves when offline, so let them run.
    queries: { networkMode: 'always', retry: false, staleTime: 30_000 },
  },
})
installSyncHandler(queryClient)

// Another shop or person signing in on this phone must never see the last one's figures, even for a moment: some
// screens' data isn't keyed by shop, so drop everything cached whenever the signed-in user or shop changes.
let signedIn = `${getSession()?.user.id ?? ''}/${getSession()?.shopId ?? ''}`
subscribeSession(() => {
  const now = `${getSession()?.user.id ?? ''}/${getSession()?.shopId ?? ''}`
  if (now !== signedIn) queryClient.clear()
  signedIn = now
})

// The text for the shown language arrives first (one small file, cached offline by the service worker).
void i18nReady.then(() => createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
      {/* The app's own question box (in place of browser pop-ups, which some browsers block). */}
      <AskHost />
    </QueryClientProvider>
  </StrictMode>,
))

// The counter's main screen isn't in the first download, but it mustn't make anyone wait: fetch it once the app is up.
void i18nReady.then(() => setTimeout(() => void import('./screens/BillScreen'), 500))

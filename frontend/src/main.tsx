import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import { AskHost } from './components/Ask'
import { i18nReady } from './i18n'
import './index.css'
import { installSyncHandler } from './lib/data'
import './lib/pwa'

const queryClient = new QueryClient({
  defaultOptions: {
    // Query functions fall back to the saved copy themselves when offline, so let them run.
    queries: { networkMode: 'always', retry: false, staleTime: 30_000 },
  },
})
installSyncHandler(queryClient)

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

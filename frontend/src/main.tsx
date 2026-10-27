import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { registerSW } from 'virtual:pwa-register'
import App from './App'
import './i18n'
import './index.css'
import { installSyncHandler } from './lib/data'

const queryClient = new QueryClient({
  defaultOptions: {
    // Query functions fall back to the saved copy themselves when offline, so let them run.
    queries: { networkMode: 'always', retry: false, staleTime: 30_000 },
  },
})
installSyncHandler(queryClient)
registerSW({ immediate: true })

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </StrictMode>,
)

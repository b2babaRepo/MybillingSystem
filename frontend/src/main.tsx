import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import './i18n'
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

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </StrictMode>,
)

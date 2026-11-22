import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { btn } from './ui'

/**
 * The shopping app's privacy notice, once, on first use: what is kept, who sees it, the person's rights, and the
 * declaration that they are 18 or older. Recorded on the account; until then the app waits behind it.
 */
export function PrivacyGate({ children }: { children: ReactNode }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const profile = useQuery({ queryKey: ['profile'], queryFn: () => api<{ privacyAcceptedAt?: string | null }>('/v1/profile'), retry: false })
  const [adult, setAdult] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  // Unknown (offline, or still loading): the app opens; the notice comes the next time it can be checked.
  if (!profile.data || profile.data.privacyAcceptedAt) return <>{children}</>

  async function accept() {
    setBusy(true)
    setError('')
    try {
      await api('/v1/profile/privacy', 'POST', { adult: true })
      await queryClient.invalidateQueries({ queryKey: ['profile'] })
    } catch (e) {
      setError(e instanceof Error ? e.message : t('error'))
    } finally {
      setBusy(false)
    }
  }
  const points = ['cust11.p1', 'cust11.p2', 'cust11.p3', 'cust11.p4', 'cust11.p5'] as const

  return (
    <div className="flex min-h-dvh items-center justify-center bg-ground p-4">
      <section className="flex w-full max-w-lg flex-col gap-3 rounded-3xl bg-white p-5 shadow-sm">
        <img src="/favicon.svg" alt="" className="h-12 w-12" />
        <h1 className="text-2xl font-extrabold">{t('cust11.title')}</h1>
        <p className="text-muted">{t('cust11.intro')}</p>
        <ul className="flex flex-col gap-2">
          {points.map((key) => (
            <li key={key} className="flex gap-2 text-sm"><span aria-hidden className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-brand" />{t(key)}</li>
          ))}
        </ul>
        <label className="mt-1 flex min-h-12 items-center gap-3 rounded-xl border-2 border-line px-3 font-semibold">
          <input type="checkbox" checked={adult} onChange={(e) => setAdult(e.target.checked)} className="h-5 w-5 accent-[#0b6e5c]" />
          {t('cust11.adult')}
        </label>
        {error && <p className="text-sm text-due">{error}</p>}
        <button onClick={() => void accept()} disabled={!adult || busy} className={`h-14 rounded-xl text-lg disabled:opacity-50 ${btn.solid}`}>{t('cust11.agree')}</button>
        <p className="text-xs text-muted">{t('cust11.under18')}</p>
      </section>
    </div>
  )
}

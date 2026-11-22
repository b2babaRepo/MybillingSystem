import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { navigate } from '../router'
import type { Dispute } from '../screens/DisputesScreen'

// Kept apart from DisputesScreen so the khata list can show the banner without loading the whole screen.

export const useOpenDisputes = () => useQuery({ queryKey: ['disputes'], queryFn: () => api<Dispute[]>('/v1/disputes'), staleTime: 60_000 })

/** On the khata list: customers have questioned entries and are waiting for an answer. */
export function OpenDisputesBanner() {
  const { t } = useTranslation()
  const count = useOpenDisputes().data?.length ?? 0
  if (count === 0) return null
  return (
    <button onClick={() => navigate('/disputes')} className="mb-3 flex min-h-12 w-full items-center justify-between rounded-[14px] bg-warn-soft px-4 text-left font-semibold text-amber-950">
      ⚠ {t('dispute.openCount', { count })}
      <span aria-hidden>›</span>
    </button>
  )
}


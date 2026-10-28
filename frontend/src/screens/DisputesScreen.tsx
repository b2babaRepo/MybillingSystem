import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatDateTime, formatRupees } from '../lib/format'
import type { LedgerEntryKind } from '../lib/types'
import { navigate } from '../router'

interface Dispute {
  id: string
  customerId: string
  customerName: string
  entryId: string
  entryKind: LedgerEntryKind
  entryAmount: number
  entryOccurredAt: string
  message: string
  raisedAt: string
  resolution: string | null
  resolvedAt: string | null
}

const useOpenDisputes = () => useQuery({ queryKey: ['disputes'], queryFn: () => api<Dispute[]>('/v1/disputes'), staleTime: 60_000 })

/** On the khata list: customers have questioned entries and are waiting for an answer. */
export function OpenDisputesBanner() {
  const { t } = useTranslation()
  const count = useOpenDisputes().data?.length ?? 0
  if (count === 0) return null
  return (
    <button onClick={() => navigate('/disputes')} className="mb-3 flex min-h-12 w-full items-center justify-between rounded-[14px] bg-amber-50 px-4 text-left font-semibold text-amber-950">
      ⚠ {t('dispute.openCount', { count })}
      <span aria-hidden>›</span>
    </button>
  )
}

/**
 * Entries customers have questioned. The owner answers each one; the answer shows on the customer's khata next to
 * the entry. If the customer is right, fix the khata on their page (a new entry or a removal) and say so here.
 */
export function DisputesScreen({ isOwner }: { isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const disputes = useOpenDisputes()

  async function resolve(d: Dispute) {
    const resolution = prompt(t('dispute.answerAsk', { name: d.customerName }))?.trim()
    if (!resolution || resolution.length < 3) return
    await api(`/v1/disputes/${d.id}/resolve`, 'POST', { resolution })
    await queryClient.invalidateQueries({ queryKey: ['disputes'] })
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      <p className="text-sm text-muted">{t('dispute.shopHint')}</p>
      {disputes.data?.length === 0 && <p className="py-8 text-center text-muted">{t('dispute.none')}</p>}
      {disputes.data?.map((d) => (
        <div key={d.id} className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
          <button className="flex items-start justify-between text-left" onClick={() => navigate(`/c/${d.customerId}`)}>
            <div>
              <div className="text-lg font-bold">{d.customerName}</div>
              <div className="text-sm text-muted">
                {d.entryAmount > 0 ? t('creditShort') : t('paymentShort')} {formatRupees(Math.abs(d.entryAmount))} · {formatDateTime(d.entryOccurredAt)}
              </div>
            </div>
            <span aria-hidden className="text-xl text-muted">›</span>
          </button>
          <div className="rounded-xl bg-amber-50 p-3 text-sm text-amber-950">
            <div className="font-semibold">⚠ {t('dispute.customerSays', { message: d.message })}</div>
            <div className="text-xs opacity-80">{formatDateTime(d.raisedAt)}</div>
          </div>
          {isOwner ? (
            <button className="h-12 rounded-2xl bg-teal-700 font-bold text-white" onClick={() => void resolve(d)}>
              {t('dispute.answer')}
            </button>
          ) : (
            <p className="text-sm text-muted">{t('dispute.ownerAnswers')}</p>
          )}
        </div>
      ))}
    </div>
  )
}

import { btn } from '../components/ui'
import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { DisputeItems } from '../components/KhataHistory'
import { api } from '../lib/api'
import { formatDateTime, formatRupees } from '../lib/format'
import type { DisputeItemView, ItemOutcome, LedgerEntryKind } from '../lib/types'
import { navigate } from '../router'
import { useOpenDisputes } from '../components/OpenDisputesBanner'
import { ask, confirmAsk } from '../components/Ask'

export interface Dispute {
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
  items: DisputeItemView[]
}

/**
 * Entries customers have questioned. "Open the bill" goes to the customer's page with that bill open and the
 * questioned items highlighted. The owner settles each item: handed over now (nothing changes), take it off the
 * khata (the amount comes off and the stock goes back), or disagree with a reason. A dispute without items gets
 * one written answer.
 */
export function DisputesScreen({ isOwner }: { isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const disputes = useOpenDisputes()
  const [error, setError] = useState<string | null>(null)

  async function act(run: () => Promise<unknown>) {
    setError(null)
    try {
      await run()
      await queryClient.invalidateQueries({ queryKey: ['disputes'] })
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  async function resolveItem(d: Dispute, item: DisputeItemView, outcome: ItemOutcome) {
    let note: string | undefined
    if (outcome === 'disagreed') {
      note = (await ask(t('dispute.disagreeAsk', { name: d.customerName, item: item.name })))?.trim()
      if (!note || note.length < 3) return
    } else if (!(await confirmAsk(t(`dispute.confirm.${outcome}`, { item: item.name, amount: formatRupees(item.disputedAmount) })))) {
      return
    }

    void act(() => api(`/v1/disputes/${d.id}/items/${item.id}/resolve`, 'POST', { outcome, note }))
  }

  async function answer(d: Dispute) {
    const resolution = (await ask(t('dispute.answerAsk', { name: d.customerName })))?.trim()
    if (!resolution || resolution.length < 3) return
    void act(() => api(`/v1/disputes/${d.id}/resolve`, 'POST', { resolution }))
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      <p className="text-sm text-muted">{t('dispute.shopHint')}</p>
      {error && <p className="rounded-[14px] bg-due-soft p-3 text-sm text-due">{error}</p>}
      {disputes.data?.length === 0 && <p className="py-8 text-center text-muted">{t('dispute.none')}</p>}
      {disputes.data?.map((d) => (
        <div key={d.id} className="flex flex-col gap-2 rounded-card bg-white p-4">
          <div>
            <div className="text-lg font-bold">{d.customerName}</div>
            <div className="text-sm text-muted">
              {d.entryAmount > 0 ? t('creditShort') : t('paymentShort')} {formatRupees(Math.abs(d.entryAmount))} · {formatDateTime(d.entryOccurredAt)}
              {' · '}{t('dispute.raisedOn', { date: formatDateTime(d.raisedAt) })}
            </div>
          </div>
          {d.message && <div className="rounded-xl bg-warn-soft p-3 text-sm font-semibold text-amber-950">⚠ {t('dispute.customerSays', { message: d.message })}</div>}
          {d.items.length > 0 && <DisputeItems items={d.items} onResolve={isOwner ? (item, outcome) => resolveItem(d, item, outcome) : undefined} />}
          <button className="h-11 rounded-2xl border border-line font-semibold text-brand-strong" onClick={() => navigate(`/c/${d.customerId}?e=${d.entryId}`)}>
            {t('dispute.openBill')} ›
          </button>
          {d.items.length === 0 && isOwner && (
            <button className={`h-12 rounded-xl ${btn.solid}`} onClick={() => answer(d)}>
              {t('dispute.answer')}
            </button>
          )}
          {!isOwner && <p className="text-sm text-muted">{t('dispute.ownerAnswers')}</p>}
        </div>
      ))}
    </div>
  )
}

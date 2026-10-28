import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatDateTime, formatRupees } from '../lib/format'
import type { StatementLine } from '../lib/types'
import { PendingDot } from './ui'

type Line = StatementLine & { pending?: boolean }

/**
 * A khata, newest first: the same view for the shop, the customer's private link and the household app.
 * Bills open to show their items; a line marks where the account came back to zero. Removing is shop-only:
 * without `onRemove` the list is read-only.
 */
export function KhataHistory({ entries, canRemove, onRemove, onDispute }: {
  entries: Line[]
  canRemove?: (entry: Line) => boolean
  onRemove?: (entry: Line) => void
  /** Customer views only: question an entry. */
  onDispute?: (entry: Line) => void
}) {
  const { t } = useTranslation()
  const [open, setOpen] = useState<string | null>(null)
  return (
    <ul className="divide-y divide-[#edf2f1] rounded-2xl bg-white px-1">
      {entries.length === 0 && <li className="p-4 text-muted">{t('noEntries')}</li>}
      {[...entries].reverse().map((e, i, newestFirst) => (
        <li key={e.id}>
          {/* The account came back to zero here: everything below this line is settled. */}
          {e.runningBalance === 0 && i > 0 && !e.isReversed && newestFirst.length > 1 && (
            <div className="mx-2 mt-2 rounded-lg bg-teal-50 px-3 py-1.5 text-center text-sm font-semibold text-teal-800">
              ✓ {t('ledgerView.settledDivider', { date: formatDateTime(e.occurredAt) })}
            </div>
          )}
          <HistoryRow
            entry={e}
            open={open === e.id}
            onToggle={() => setOpen(open === e.id ? null : e.id)}
            canRemove={!!onRemove && !e.upiRef && (canRemove?.(e) ?? true)}
            onRemove={() => onRemove?.(e)}
            onDispute={onDispute && e.kind !== 'reversal' && !(e.dispute && !e.dispute.resolvedAt) ? () => onDispute(e) : undefined}
          />
        </li>
      ))}
    </ul>
  )
}

/** One khata line. A bill's udhaar opens to show what was taken: item, quantity × price, total, paid at the counter. */
function HistoryRow({ entry: e, open, canRemove, onToggle, onRemove, onDispute }: {
  entry: StatementLine & { pending?: boolean }
  open: boolean
  canRemove: boolean
  onToggle: () => void
  onRemove: () => void
  onDispute?: () => void
}) {
  const { t } = useTranslation()
  const items = e.bill?.items ?? []
  return (
    <div className={e.isReversed ? 'opacity-40' : ''}>
      <div className="flex items-center gap-3 p-3">
        <button
          className="flex flex-1 items-center gap-2 text-left"
          onClick={items.length ? onToggle : undefined}
          aria-expanded={items.length ? open : undefined}
        >
          <div className="flex-1">
            <div className={`flex flex-wrap items-center gap-x-2 font-semibold ${e.isReversed ? 'line-through' : ''}`}>
              {e.kind === 'reversal' ? t('reversal') : e.amount > 0 ? t('creditShort') : t('paymentShort')}
              {e.paymentMode && <span className="text-xs text-muted uppercase">{t(e.paymentMode)}</span>}
              {items.length > 0 && (
                <span className="text-xs font-normal text-teal-700">
                  {t('ledgerView.items', { count: items.length })} {open ? '▴' : '▾'}
                </span>
              )}
              {e.pending && <PendingDot />}
            </div>
            <div className="text-xs text-muted">
              {formatDateTime(e.occurredAt)}
              {e.recordedBy && ` · ${t(e.kind === 'reversal' ? 'ledgerView.removedBy' : 'ledgerView.by', { name: e.recordedBy })}`}
            </div>
            {e.note && (
              <div className={`text-xs ${e.kind === 'reversal' ? 'font-semibold text-due' : 'text-muted'}`}>
                {e.kind === 'reversal' ? t('ledgerView.reason', { reason: e.note }) : e.note}
              </div>
            )}
          </div>
          <div className={`text-right font-bold ${e.amount > 0 ? 'text-udhaar' : 'text-jama'}`}>
            {e.amount > 0 ? '+' : '−'}
            {formatRupees(Math.abs(e.amount))}
            <div className="text-xs font-normal text-muted">{t('ledgerView.balanceAfter', { amount: formatRupees(e.runningBalance) })}</div>
          </div>
        </button>
        {onDispute && (
          <button className="min-h-11 px-1 text-sm font-semibold text-due underline" onClick={onDispute}>
            {t('dispute.raise')}
          </button>
        )}
        {canRemove && e.kind !== 'reversal' && !e.isReversed && !e.pending && (
          <button className="min-h-11 px-1 text-sm text-muted underline" onClick={onRemove}>
            {t('remove')}
          </button>
        )}
      </div>
      {e.upiRef && (
        <div className="-mt-2 px-3 pb-2 text-xs font-semibold text-jama">🔒 {t('ledgerView.upiLocked', { ref: e.upiRef })}</div>
      )}
      {e.dispute && (
        <div className={`mx-3 mb-3 rounded-xl p-3 text-sm ${e.dispute.resolvedAt ? 'bg-[#f4f8f7]' : 'bg-amber-50 text-amber-950'}`}>
          <div className="font-semibold">⚠ {t('dispute.customerSays', { message: e.dispute.message })}</div>
          <div className="text-xs opacity-80">{formatDateTime(e.dispute.raisedAt)}</div>
          {e.dispute.resolvedAt ? (
            <div className="mt-1.5 border-t border-line pt-1.5">
              <span className="font-semibold">{t('dispute.shopAnswer')}</span> {e.dispute.resolution}
              <div className="text-xs text-muted">{formatDateTime(e.dispute.resolvedAt)}</div>
            </div>
          ) : (
            <div className="mt-1 text-xs font-semibold">{t('dispute.waiting')}</div>
          )}
        </div>
      )}
      {open && e.bill && (
        <div className="mx-3 mb-3 rounded-xl bg-[#f4f8f7] p-3 text-sm">
          {items.map((item, n) => (
            <div key={n} className="flex justify-between gap-2 py-0.5">
              <span className="min-w-0 truncate">{item.name}</span>
              <span className="shrink-0 text-muted">
                {item.quantity} {t(`units.${item.unit}`)} × {formatRupees(item.unitPrice)} = <b className="text-ink">{formatRupees(item.amount)}</b>
              </span>
            </div>
          ))}
          <div className="mt-1.5 flex justify-between border-t border-line pt-1.5 font-semibold">
            <span>{t('ledgerView.billTotal')}</span>
            <span>{formatRupees(e.bill.total)}</span>
          </div>
          {e.bill.paid > 0 && (
            <div className="flex justify-between text-muted">
              <span>{t('ledgerView.paidAtCounter')}</span>
              <span>{formatRupees(e.bill.paid)}</span>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

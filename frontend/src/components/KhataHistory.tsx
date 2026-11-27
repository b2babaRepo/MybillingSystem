import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { formatDateTime, formatRupees } from '../lib/format'
import type { DisputeItemView, ItemOutcome, StatementLine } from '../lib/types'
import { PendingDot } from './ui'
import { btn } from '../lib/buttonStyles'

type Line = StatementLine & { pending?: boolean }

/**
 * A khata, newest first: the same view for the shop, the customer's private link and the household app.
 * Bills open to show their items; a line marks where the account came back to zero. Removing is shop-only:
 * without `onRemove` the list is read-only.
 */
export function KhataHistory({ entries, canRemove, onRemove, onDispute, focusEntryId }: {
  entries: Line[]
  canRemove?: (entry: Line) => boolean
  onRemove?: (entry: Line) => void
  /** Customer views only: question an entry. */
  onDispute?: (entry: Line) => void
  /** Opened from a link to one entry (a disputed bill): open it, scroll to it and ring it. */
  focusEntryId?: string
}) {
  const { t } = useTranslation()
  const [open, setOpen] = useState<string | null>(focusEntryId ?? null)
  const focused = useRef<HTMLLIElement>(null)
  const loaded = entries.some((e) => e.id === focusEntryId)
  const [openedFor, setOpenedFor] = useState<string | null>(null)
  const focusKey = focusEntryId && loaded ? focusEntryId : null
  if (focusKey !== openedFor) {
    setOpenedFor(focusKey)
    if (focusKey) setOpen(focusKey)
  }
  useEffect(() => {
    if (focusKey) focused.current?.scrollIntoView({ behavior: 'smooth', block: 'center' })
  }, [focusKey])
  return (
    <ul className="divide-y divide-[#edf2f1] rounded-card bg-white px-1 shadow-sm">
      {entries.length === 0 && <li className="p-4 text-muted">{t('noEntries')}</li>}
      {[...entries].reverse().map((e, i, newestFirst) => (
        <li key={e.id} ref={e.id === focusEntryId ? focused : undefined} className={e.id === focusEntryId ? 'rounded-xl ring-2 ring-amber-400' : undefined}>
          {/* The account came back to zero here: everything below this line is settled. */}
          {e.runningBalance === 0 && i > 0 && !e.isReversed && newestFirst.length > 1 && (
            <div className="mx-2 mt-2 rounded-lg bg-brand-soft px-3 py-1.5 text-center text-sm font-semibold text-brand-strong">
              ✓ {t('ledgerView.settledDivider', { date: formatDateTime(e.occurredAt) })}
            </div>
          )}
          <HistoryRow
            entry={e}
            open={open === e.id}
            onToggle={() => setOpen(open === e.id ? null : e.id)}
            canRemove={!!onRemove && (canRemove?.(e) ?? true)}
            onRemove={() => onRemove?.(e)}
            onDispute={onDispute && e.kind !== 'reversal' && e.kind !== 'return' && !(e.dispute && !e.dispute.resolvedAt) ? () => onDispute(e) : undefined}
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
          className="flex flex-1 items-center gap-3 text-left"
          onClick={items.length ? onToggle : undefined}
          aria-expanded={items.length ? open : undefined}
        >
          <span aria-hidden className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full text-lg font-bold ${e.kind === 'reversal' ? 'bg-slate-100 text-muted' : e.amount > 0 ? 'bg-orange-50 text-udhaar' : 'bg-green-50 text-jama'}`}>
            {e.kind === 'reversal' ? '↺' : e.amount > 0 ? '↑' : '↓'}
          </span>
          <div className="min-w-0 flex-1">
            <div className={`flex flex-wrap items-center gap-x-2 font-semibold ${e.isReversed ? 'line-through' : ''}`}>
              {e.kind === 'reversal' ? t('reversal') : e.kind === 'return' ? t('dispute.returned') : e.amount > 0 ? t('creditShort') : t('paymentShort')}
              {e.paymentMode && <span className="rounded-full bg-[#edf2f1] px-2 py-0.5 text-[11px] font-semibold text-muted uppercase">{t(e.paymentMode)}{e.chequeNo ? ` #${e.chequeNo}` : ''}</span>}
              {e.chequeStatus && e.kind !== 'reversal' && <span className={`rounded px-1 text-xs font-normal ${e.chequeStatus === 'cleared' ? 'bg-emerald-50 text-emerald-800' : e.chequeStatus === 'bounced' ? 'bg-due-soft text-red-800' : 'bg-warn-soft text-warn'}`}>{t(`cheques.status.${e.chequeStatus}`)}</span>}
              {items.length > 0 && (
                <span className="rounded-full bg-brand-soft px-2 py-0.5 text-[11px] font-semibold text-brand-strong">
                  {t('ledgerView.items', { count: items.length })} {open ? '▴' : '▾'}
                </span>
              )}
              {e.pending && <PendingDot />}
            </div>
            <div className="text-xs text-muted">
              {formatDateTime(e.occurredAt)}
              {e.recordedBy && ` · ${t(e.kind === 'reversal' ? 'ledgerView.removedBy' : 'ledgerView.by', { name: e.recordedBy })}`}
            </div>
            {e.enteredLate && e.recordedAt && (
              <div className="text-xs font-semibold text-warn">{t('ledgerView.enteredLate', { date: formatDateTime(e.recordedAt) })}</div>
            )}
            {e.note && (
              <div className={`text-xs ${e.kind === 'reversal' ? 'font-semibold text-due' : 'text-muted'}`}>
                {e.kind === 'reversal' ? t('ledgerView.reason', { reason: e.note }) : e.note}
              </div>
            )}
          </div>
          <div className={`shrink-0 text-right text-[17px] font-bold ${e.amount > 0 ? 'text-udhaar' : 'text-jama'}`}>
            {e.amount > 0 ? '+' : '−'}
            {formatRupees(Math.abs(e.amount))}
            <div className="text-xs font-normal text-muted">{e.runningBalance < 0 ? `${t('advance')} ${formatRupees(-e.runningBalance)}` : t('ledgerView.balanceAfter', { amount: formatRupees(e.runningBalance) })}</div>
          </div>
        </button>
        {onDispute && (
          <button className={`min-h-11 px-1 text-sm ${btn.dangerOutline}`} onClick={onDispute}>
            {t('dispute.raise')}
          </button>
        )}
        {canRemove && e.kind !== 'reversal' && !e.reversesEntryId && !e.isReversed && !e.pending && (
          <button className="min-h-11 px-1 text-sm text-muted underline" onClick={onRemove}>
            {t('remove')}
          </button>
        )}
      </div>
      {e.upiRef && (
        <div className="-mt-2 px-3 pb-2 text-xs font-semibold text-jama">{t('ledgerView.upiLocked', { ref: e.upiRef })}</div>
      )}
      {e.dispute && (
        <div className={`mx-3 mb-3 rounded-xl p-3 text-sm ${e.dispute.resolvedAt ? 'bg-[#f4f8f7]' : 'bg-warn-soft text-amber-950'}`}>
          {e.dispute.message && <div className="font-semibold">⚠ {t('dispute.customerSays', { message: e.dispute.message })}</div>}
          {e.dispute.items.length > 0 && <DisputeItems items={e.dispute.items} />}
          <div className="text-xs opacity-80">{formatDateTime(e.dispute.raisedAt)}</div>
          {e.dispute.items.length > 0 ? null : e.dispute.resolvedAt ? (
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
        <div className="mx-3 mb-3 ml-16 rounded-xl bg-[#f4f8f7] p-3 text-sm ring-1 ring-line">
          {items.map((item, n) => (
            <div
              key={n}
              className={`flex justify-between gap-2 py-0.5 ${e.dispute?.items.some((d) => d.billLineId === item.id && !d.outcome) ? '-mx-1 rounded bg-amber-100 px-1 font-semibold' : ''}`}
            >
              <span className="min-w-0 truncate">{item.name}</span>
              <span className="shrink-0 text-muted">
                {item.quantity} {t(`units.${item.unit}`, { count: item.quantity })} × {formatRupees(item.unitPrice)} = <b className="text-ink">{formatRupees(item.amount)}</b>
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

const outcomeStyle: Record<ItemOutcome, string> = {
  handed_over: 'bg-brand-soft text-brand-strong',
  refunded: 'bg-brand-soft text-brand-strong',
  disagreed: 'bg-due-soft text-due',
}

/**
 * The bill items a customer questioned: what's wrong with each, how much is in question and, once settled, what
 * the shop did. With `onResolve` (the owner's list) each open item gets the three fixes.
 */
export function DisputeItems({ items, onResolve }: { items: DisputeItemView[]; onResolve?: (item: DisputeItemView, outcome: ItemOutcome) => void }) {
  const { t } = useTranslation()
  return (
    <ul className="my-1.5 flex flex-col gap-2">
      {items.map((item) => (
        <li key={item.id} className={`rounded-lg border p-2 ${item.outcome ? 'border-line bg-white' : 'border-amber-300 bg-amber-100'}`}>
          <div className="flex justify-between gap-2 font-semibold">
            <span className="min-w-0 truncate">{item.name}</span>
            <span className="shrink-0">{formatRupees(item.disputedAmount)}</span>
          </div>
          <div className="text-xs">
            {item.problem === 'short'
              ? t('dispute.problem.short', { got: item.receivedQuantity, of: item.quantity, unit: t(`units.${item.unit}`, { count: item.quantity }) })
              : t(`dispute.problem.${item.problem}`, { qty: item.quantity, unit: t(`units.${item.unit}`, { count: item.quantity }) })}
          </div>
          {item.outcome && (
            <div className={`mt-1 rounded px-2 py-1 text-xs font-semibold ${outcomeStyle[item.outcome]}`}>
              {t(`dispute.outcome.${item.outcome}`)}
              {item.outcomeNote && `: ${item.outcomeNote}`}
              {item.resolvedAt && <span className="font-normal"> · {formatDateTime(item.resolvedAt)}</span>}
            </div>
          )}
          {!item.outcome && onResolve && (
            <div className="mt-2 grid grid-cols-3 gap-1.5 text-xs font-semibold">
              <button className={`min-h-11 px-1 rounded-xl ${btn.solid}`} onClick={() => onResolve(item, 'handed_over')}>{t('dispute.fix.handed_over')}</button>
              <button className={`min-h-11 px-1 rounded-xl ${btn.outline}`} onClick={() => onResolve(item, 'refunded')}>{t('dispute.fix.refunded')}</button>
              <button className="min-h-11 rounded-lg border border-line bg-white px-1 text-due" onClick={() => onResolve(item, 'disagreed')}>{t('dispute.fix.disagreed')}</button>
            </div>
          )}
          {!item.outcome && !onResolve && <div className="mt-1 text-xs font-semibold">{t('dispute.waiting')}</div>}
        </li>
      ))}
    </ul>
  )
}

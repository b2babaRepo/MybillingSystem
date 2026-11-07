import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { changeDue, findCustomers } from '../lib/customerFind'
import { formatRupees } from '../lib/format'
import type { Customer } from '../lib/types'

/** The bill's customer: type a name or phone digits, see what they owe, Enter takes the top match. */
export function CustomerFinder({ customers, selected, onKhata, onPick }: { customers: Customer[]; selected: Customer | null; onKhata: number; onPick: (id: string) => void }) {
  const { t } = useTranslation()
  const [text, setText] = useState('')
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const hits = findCustomers(customers, text)

  function pick(id: string) {
    onPick(id)
    setText('')
    setOpen(false)
    setActive(0)
  }

  if (selected) {
    const limit = selected.creditLimit
    const over = limit != null && selected.balance + onKhata > limit
    return (
      <div>
        <span className="mb-1 block text-sm font-medium text-muted">{t('customer')}</span>
        <div className="flex min-h-12 items-center gap-2 rounded-xl border border-line bg-white px-3 py-1">
          <span className="min-w-0 flex-1">
            <span className="block truncate text-lg font-semibold">{selected.name}</span>
            <span className="block text-sm text-muted">{[selected.phone, selected.balance > 0 ? t('billFind.owes', { amount: formatRupees(selected.balance) }) : null].filter(Boolean).join(' · ')}</span>
          </span>
          <button onClick={() => onPick('')} className="min-h-11 px-2 text-sm font-semibold text-teal-700 underline">{t('billFind.change')}</button>
        </div>
        {over && <p className="mt-1 text-sm font-semibold text-due">{t('billFind.overLimit', { limit: formatRupees(limit) })}</p>}
      </div>
    )
  }

  return (
    <div className="relative">
      <label className="block">
        <span className="mb-1 block text-sm font-medium text-muted">{t('customer')}</span>
        <input
          id="bill-customer"
          value={text}
          onChange={(e) => { setText(e.target.value); setActive(0); setOpen(true) }}
          onFocus={() => setOpen(true)}
          onBlur={() => setOpen(false)}
          onKeyDown={(e) => {
            if (e.ctrlKey || e.metaKey) return
            if (e.key === 'ArrowDown') { e.preventDefault(); setActive((i) => Math.min(i + 1, hits.length - 1)) }
            else if (e.key === 'ArrowUp') { e.preventDefault(); setActive((i) => Math.max(i - 1, 0)) }
            else if (e.key === 'Enter' && open && hits[active]) { e.preventDefault(); pick(hits[active].id) }
            else if (e.key === 'Escape') { setText(''); setOpen(false) }
          }}
          placeholder={t('billFind.placeholder')}
          autoComplete="off"
          className="h-12 w-full rounded-xl border border-line bg-white px-3 text-lg outline-none focus:border-teal-600"
        />
      </label>
      {open && (
        <div className="absolute z-20 mt-1 w-full overflow-hidden rounded-xl border border-line bg-white shadow-lg">
          {hits.map((c, i) => (
            <button
              key={c.id}
              // Before the box loses focus, which would close this list first.
              onMouseDown={(e) => { e.preventDefault(); pick(c.id) }}
              className={`flex min-h-11 w-full items-center justify-between gap-2 px-3 py-1 text-left ${i === active ? 'bg-teal-50' : ''}`}
            >
              <span className="min-w-0">
                <span className="block truncate font-semibold">{c.name}</span>
                {c.phone && <span className="block text-xs text-muted">{c.phone}</span>}
              </span>
              {c.balance > 0 && <span className="shrink-0 text-sm font-semibold text-due">{formatRupees(c.balance)}</span>}
            </button>
          ))}
          {hits.length === 0 && <p className="p-3 text-sm text-muted">{t('billFind.none')}</p>}
        </div>
      )}
    </div>
  )
}

/** Cash in hand: what to give back. Only a helper at the counter; it isn't saved on the bill. */
export function CashChange({ paying, received, onChange }: { paying: number; received: string; onChange: (value: string) => void }) {
  const { t } = useTranslation()
  const back = received.trim() ? changeDue(Number(received) || 0, paying) : null
  return (
    <label className="flex flex-wrap items-center gap-2 text-sm">
      <span className="font-medium text-muted">{t('billFind.received')}</span>
      <input id="bill-received" value={received} onChange={(e) => onChange(e.target.value)} inputMode="decimal" autoComplete="off" className="h-11 w-28 rounded-lg border border-line px-2 text-lg" />
      {back !== null && back >= 0 && <span className="text-lg font-bold">{t('billFind.giveBack', { amount: formatRupees(back) })}</span>}
      {back !== null && back < 0 && <span className="font-semibold text-due">{t('billFind.short', { amount: formatRupees(-back) })}</span>}
    </label>
  )
}

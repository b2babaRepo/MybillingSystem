import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { changeDue, findCustomers } from '../lib/customerFind'
import { formatRupees } from '../lib/format'
import type { Customer } from '../lib/types'

/** The bill's customer: type a name or phone digits, see what they owe, Enter takes the top match. */
export function CustomerFinder({ customers, selected, onKhata, onPick, onCreate, placeholder, inputId = 'bill-customer', onNameLater }: { customers: Customer[]; selected: Customer | null; onKhata: number; onPick: (id: string) => void; onCreate?: (name: string, phone: string) => Promise<string>; placeholder?: string; inputId?: string; onNameLater?: () => Promise<string> }) {
  const { t } = useTranslation()
  const [text, setText] = useState('')
  const [open, setOpen] = useState(false)
  const [active, setActive] = useState(0)
  const hits = findCustomers(customers, text)
  // Someone new: their name and mobile go on the bill (and into the khata list), so the shop has both from now on.
  const [adding, setAdding] = useState<{ name: string; phone: string } | null>(null)
  const [problem, setProblem] = useState('')
  function startAdding() {
    const typed = text.trim()
    const digits = typed.replace(/\D/g, '')
    setAdding(digits.length >= 6 && digits.length === typed.replace(/[\s+-]/g, '').length ? { name: '', phone: digits.slice(-10) } : { name: typed, phone: '' })
    setOpen(false)
    setProblem('')
  }
  async function saveNew() {
    if (!adding || !onCreate) return
    const phone = adding.phone.replace(/\D/g, '')
    if (phone && phone.length !== 10) { setProblem(t('newBuyer.badPhone')); return }
    const id = await onCreate(adding.name.trim() || phone, phone)
    setAdding(null)
    setText('')
    onPick(id)
  }

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
          <button onClick={() => onPick('')} className="min-h-11 px-2 text-sm font-semibold text-brand underline">{t('billFind.change')}</button>
        </div>
        {over && <p className="mt-1 text-sm font-semibold text-due">{t('billFind.overLimit', { limit: formatRupees(limit) })}</p>}
      </div>
    )
  }

  if (adding) {
    return (
      <div className="flex flex-col gap-2 rounded-xl border border-brand p-3">
        <span className="text-sm font-semibold">{t('newBuyer.title')}</span>
        <input value={adding.name} onChange={(e) => setAdding({ ...adding, name: e.target.value })} placeholder={t('newBuyer.name')} aria-label={t('newBuyer.name')} maxLength={80}
          className="h-12 w-full rounded-xl border border-line bg-white px-3 text-lg" />
        <input value={adding.phone} onChange={(e) => setAdding({ ...adding, phone: e.target.value })} placeholder={t('newBuyer.phone')} aria-label={t('newBuyer.phone')} inputMode="tel" maxLength={14}
          className="h-12 w-full rounded-xl border border-line bg-white px-3 text-lg" />
        <p className="text-xs text-muted">{t('newBuyer.hint')}</p>
        {problem && <p className="text-sm text-due">{problem}</p>}
        <div className="grid grid-cols-2 gap-2">
          <button type="button" disabled={!adding.name.trim() && !adding.phone.trim()} onClick={() => void saveNew()} className="min-h-11 rounded-xl bg-brand font-semibold text-white disabled:opacity-40">{t('newBuyer.save')}</button>
          <button type="button" onClick={() => setAdding(null)} className="min-h-11 rounded-xl border border-line font-semibold">{t('cancel')}</button>
        </div>
      </div>
    )
  }

  return (
    <div className="relative">
      <label className="block">
        <span className="mb-1.5 block text-sm font-semibold text-muted">{t('customer')}</span>
        <span className="relative block">
        <span aria-hidden className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-muted">👤</span>
        <input
          id={inputId}
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
          placeholder={placeholder ?? t('billFind.placeholder')}
          autoComplete="off"
          className="h-12 w-full rounded-2xl border border-line bg-[#f6faf9] pl-11 pr-3 text-lg outline-none transition focus:border-brand-line focus:bg-white focus:ring-4 focus:ring-brand-line/10"
        />
        </span>
      </label>
      {/* Always in sight: a new customer's name and mobile go straight on the bill. */}
      {onCreate && !open && (
        <span className="flex flex-wrap gap-x-2">
          <button type="button" onClick={startAdding} className="mt-2 flex min-h-10 items-center gap-1.5 rounded-full border border-brand/30 bg-brand-soft/60 px-3.5 text-sm font-semibold text-brand-strong hover:bg-brand-soft">{t('newBuyer.addBlank')}</button>
          {/* Udhaar to someone who won't give a name yet: a numbered "Name later" khata the owner names afterwards. */}
          {onNameLater && <button type="button" onClick={() => void onNameLater().then(onPick)} className="mt-2 flex min-h-10 items-center gap-1.5 rounded-full border border-amber-600/30 bg-warn-soft px-3.5 text-sm font-semibold text-warn-ink hover:bg-amber-100"><span aria-hidden>⏳</span>{t('nameLater.button')}</button>}
        </span>
      )}
      {open && (
        <div className="absolute z-20 mt-1 w-full overflow-hidden rounded-xl border border-line bg-white shadow-lg">
          {hits.map((c, i) => (
            <button
              key={c.id}
              // Before the box loses focus, which would close this list first.
              onMouseDown={(e) => { e.preventDefault(); pick(c.id) }}
              className={`flex min-h-11 w-full items-center justify-between gap-2 px-3 py-1 text-left ${i === active ? 'bg-brand-soft' : ''}`}
            >
              <span className="min-w-0">
                <span className="block truncate font-semibold">{c.name}</span>
                {c.phone && <span className="block text-xs text-muted">{c.phone}</span>}
              </span>
              {c.balance > 0 && <span className="shrink-0 text-sm font-semibold text-due">{formatRupees(c.balance)}</span>}
            </button>
          ))}
          {hits.length === 0 && !(onCreate && text.trim()) && <p className="p-3 text-sm text-muted">{t('billFind.none')}</p>}
          {onCreate && text.trim() && (
            <button onMouseDown={(e) => { e.preventDefault(); startAdding() }} className="flex min-h-11 w-full items-center border-t border-line px-3 text-left font-semibold text-brand-strong">
              {t('newBuyer.add', { text: text.trim() })}
            </button>
          )}
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
    <label className="flex flex-wrap items-center gap-3 rounded-2xl bg-[#f6faf9] p-3 text-sm">
      <span className="font-semibold text-muted">💵 {t('billFind.received')}</span>
      <span className="relative">
        <span aria-hidden className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-lg text-muted">₹</span>
        <input id="bill-received" value={received} onChange={(e) => onChange(e.target.value)} inputMode="decimal" autoComplete="off" placeholder={String(paying)} className="h-12 w-36 rounded-xl border border-line bg-white pl-8 pr-2 text-lg font-semibold outline-none focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
      </span>
      {back !== null && back >= 0 && <span className="rounded-full bg-green-100 px-3 py-1 text-base font-bold text-green-900">{t('billFind.giveBack', { amount: formatRupees(back) })}</span>}
      {back !== null && back < 0 && <span className="rounded-full bg-red-100 px-3 py-1 font-semibold text-due">{t('billFind.short', { amount: formatRupees(-back) })}</span>}
    </label>
  )
}

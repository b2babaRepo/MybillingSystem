import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import type { PaymentMode } from '../lib/types'

export type ExpenseCategory = 'tea' | 'transport' | 'rent' | 'electricity' | 'salary' | 'purchase' | 'packaging' | 'repair' | 'home' | 'other'

interface ExpenseLine {
  id: string
  occurredAt: string
  amount: number
  category: ExpenseCategory
  note: string | null
  mode: PaymentMode
  by: string | null
  struckAt: string | null
}

interface ExpensePage {
  date: string
  cash: number
  upi: number
  total: number
  lines: ExpenseLine[]
}

interface Guess {
  key: number
  amount: string
  category: ExpenseCategory
  note: string
  mode: PaymentMode
}

const ICONS: Record<ExpenseCategory, string> = {
  tea: '☕', transport: '🛺', rent: '🏠', electricity: '💡', salary: '👷', purchase: '📦', packaging: '🛍️', repair: '🔧', home: '👪', other: '📝',
}
const QUICK: ExpenseCategory[] = ['tea', 'transport', 'packaging', 'salary', 'purchase', 'electricity', 'rent', 'home']
const ALL = Object.keys(ICONS) as ExpenseCategory[]

/** Today in India as yyyy-mm-dd (the shop day the server counts by). */
function indiaToday() {
  return new Date(Date.now() + 5.5 * 3600_000).toISOString().slice(0, 10)
}

function shiftDay(day: string, by: number) {
  const date = new Date(`${day}T00:00:00Z`)
  date.setUTCDate(date.getUTCDate() + by)
  return date.toISOString().slice(0, 10)
}

/**
 * "Aaj ka panna": the shop's kharcha book as a notebook page. Write a line the way you would on paper ("chai 40,
 * auto 120, thaili 250 upi"), check what was read, save. Or tap a heading and type just the amount. A wrong line is
 * struck through, never rubbed out. Cash lines come out of the drawer in the cash book; UPI ones don't.
 */
export function ExpensesScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const today = indiaToday()
  const [day, setDay] = useState(today)
  const [text, setText] = useState('')
  const [guesses, setGuesses] = useState<Guess[]>([])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const nextKey = useRef(1)
  const page = useQuery({ queryKey: [shopId, 'expenses', day], queryFn: () => api<ExpensePage>(`/v1/expenses?date=${day}`) })
  const label = new Date(`${day}T00:00:00Z`).toLocaleDateString('en-IN', { weekday: 'short', day: 'numeric', month: 'short', timeZone: 'UTC' })
  const isToday = day === today

  const refresh = () => Promise.all([
    queryClient.invalidateQueries({ queryKey: [shopId, 'expenses'] }),
    queryClient.invalidateQueries({ queryKey: [shopId, 'cash-book'] }),
  ])

  async function run(action: () => Promise<void>) {
    setBusy(true)
    setError(null)
    try {
      await action()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  const read = () => run(async () => {
    const found = await api<{ amount: number; category: ExpenseCategory; note: string | null; mode: PaymentMode }[]>('/v1/expenses/read', 'POST', { text })
    if (found.length === 0) throw new Error(t('expense.nothingRead'))
    setGuesses([...guesses, ...found.map((g) => ({ key: nextKey.current++, amount: String(g.amount), category: g.category, note: g.note ?? '', mode: g.mode }))])
    setText('')
  })

  const quick = (category: ExpenseCategory) =>
    setGuesses([...guesses, { key: nextKey.current++, amount: '', category, note: t(`expense.cat.${category}`), mode: 'cash' }])

  const update = (key: number, change: Partial<Guess>) => setGuesses(guesses.map((g) => (g.key === key ? { ...g, ...change } : g)))
  const ready = guesses.length > 0 && guesses.every((g) => Number(g.amount) > 0)

  const save = () => run(async () => {
    for (const g of guesses) {
      // The time the shop wrote it; a past page keeps its own date.
      const occurredAt = isToday ? undefined : `${day}T12:00:00+05:30`
      await api('/v1/expenses', 'POST', { id: crypto.randomUUID(), amount: Number(g.amount), category: g.category, note: g.note.trim() || null, mode: g.mode, occurredAt })
    }
    setGuesses([])
    await refresh()
  })

  const strike = (line: ExpenseLine) =>
    confirm(t('expense.strikeAsk', { what: line.note ?? t(`expense.cat.${line.category}`), amount: formatRupees(line.amount) })) &&
    void run(async () => {
      await api(`/v1/expenses/${line.id}/strike`, 'POST', {})
      await refresh()
    })

  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex items-center justify-between rounded-[18px] bg-white p-2">
        <button onClick={() => setDay(shiftDay(day, -1))} aria-label={t('book.previous')} className="h-11 w-11 rounded-xl text-xl">‹</button>
        <span className="text-lg font-bold">{isToday ? t('expense.today') : label}</span>
        <button onClick={() => setDay(shiftDay(day, 1))} disabled={isToday} aria-label={t('book.next')} className="h-11 w-11 rounded-xl text-xl disabled:opacity-30">›</button>
      </div>

      {/* Write: one line like the notebook, or tap a heading. */}
      <section className="flex flex-col gap-3 rounded-[20px] bg-white p-4">
        <label htmlFor="expense-text" className="text-[15px] font-bold">{t('expense.writeTitle')}</label>
        <textarea id="expense-text" value={text} onChange={(e) => setText(e.target.value)} rows={2} placeholder={t('expense.placeholder')}
          className="rounded-2xl border border-line p-3 text-lg" />
        <button disabled={busy || !text.trim()} onClick={() => void read()} className="h-12 rounded-2xl bg-teal-700 font-bold text-white disabled:opacity-40">
          {t('expense.read')}
        </button>
        <div className="flex flex-wrap gap-2" aria-label={t('expense.quickTitle')}>
          {QUICK.map((c) => (
            <button key={c} onClick={() => quick(c)} className="flex min-h-11 items-center gap-1.5 rounded-full border border-line px-3 text-sm font-semibold">
              <span aria-hidden>{ICONS[c]}</span>{t(`expense.cat.${c}`)}
            </button>
          ))}
        </div>
      </section>

      {/* Check what was read before it goes on the page. */}
      {guesses.length > 0 && (
        <section className="flex flex-col gap-2 rounded-[20px] border-2 border-teal-700 bg-white p-4">
          <h2 className="font-bold">{t('expense.checkTitle')}</h2>
          {guesses.map((g) => (
            <div key={g.key} className="flex flex-col gap-2 rounded-2xl bg-ground p-3">
              <div className="flex items-center gap-2">
                <label className="sr-only" htmlFor={`cat-${g.key}`}>{t('expense.heading')}</label>
                <select id={`cat-${g.key}`} value={g.category} onChange={(e) => update(g.key, { category: e.target.value as ExpenseCategory })}
                  className="h-11 flex-1 rounded-xl border border-line bg-white px-2">
                  {ALL.map((c) => <option key={c} value={c}>{ICONS[c]} {t(`expense.cat.${c}`)}</option>)}
                </select>
                <label className="sr-only" htmlFor={`amt-${g.key}`}>{t('expense.amount')}</label>
                <input id={`amt-${g.key}`} value={g.amount} onChange={(e) => update(g.key, { amount: e.target.value.replace(/[^\d.]/g, '') })}
                  inputMode="decimal" placeholder="₹" autoFocus={!g.amount} className="h-11 w-28 rounded-xl border border-line bg-white px-3 text-right text-lg font-bold" />
              </div>
              <div className="flex items-center gap-2">
                <label className="sr-only" htmlFor={`note-${g.key}`}>{t('expense.note')}</label>
                <input id={`note-${g.key}`} value={g.note} onChange={(e) => update(g.key, { note: e.target.value })} maxLength={120}
                  className="h-11 min-w-0 flex-1 rounded-xl border border-line bg-white px-3" />
                {(['cash', 'upi'] as const).map((m) => (
                  <button key={m} onClick={() => update(g.key, { mode: m })} aria-pressed={g.mode === m}
                    className={`h-11 rounded-xl px-3 text-sm font-semibold ${g.mode === m ? 'bg-ink text-white' : 'border border-line bg-white'}`}>{t(m)}</button>
                ))}
                <button onClick={() => setGuesses(guesses.filter((x) => x.key !== g.key))} aria-label={t('expense.remove')} className="h-11 w-11 rounded-xl text-xl text-muted">×</button>
              </div>
            </div>
          ))}
          <button disabled={busy || !ready} onClick={() => void save()} className="h-12 rounded-2xl bg-teal-700 font-bold text-white disabled:opacity-40">
            {t('expense.save', { count: guesses.length })}
          </button>
        </section>
      )}

      {error && <p className="rounded-xl bg-red-50 p-3 text-due">{error}</p>}

      {/* The page itself: ruled lines and a margin, like the kharcha book. */}
      {page.data && (
        <section className="overflow-hidden rounded-[20px] border border-[#e8dfc8] bg-[#fffdf6]">
          <div className="grid grid-cols-3 gap-2 border-b border-[#e8dfc8] p-3 text-center">
            <div><div className="text-xs text-muted">{t('expense.cashOut')}</div><div className="text-lg font-extrabold text-due">{formatRupees(page.data.cash)}</div></div>
            <div><div className="text-xs text-muted">{t('expense.upiOut')}</div><div className="text-lg font-extrabold">{formatRupees(page.data.upi)}</div></div>
            <div><div className="text-xs text-muted">{t('expense.total')}</div><div className="text-lg font-extrabold">{formatRupees(page.data.total)}</div></div>
          </div>
          <ul className="border-l-2 border-red-300 pl-3" style={{ backgroundImage: 'repeating-linear-gradient(transparent 0 55px, #e8dfc8 55px 56px)' }}>
            {page.data.lines.length === 0 && <li className="flex min-h-14 items-center pr-3 text-muted">{t('expense.empty')}</li>}
            {page.data.lines.map((line) => (
              <li key={line.id}>
                <button onClick={() => !line.struckAt && strike(line)} disabled={!!line.struckAt}
                  className={`flex min-h-14 w-full items-center gap-2 pr-3 text-left ${line.struckAt ? 'text-muted line-through' : ''}`}>
                  <span aria-hidden className="text-xl">{ICONS[line.category]}</span>
                  <span className="flex min-w-0 flex-1 flex-col">
                    <span className="truncate font-semibold">{line.note ?? t(`expense.cat.${line.category}`)}</span>
                    <span className="text-xs text-muted">
                      {new Date(line.occurredAt).toLocaleTimeString('en-IN', { hour: 'numeric', minute: '2-digit' })} · {t(line.mode)}{line.by ? ` · ${line.by}` : ''}
                    </span>
                  </span>
                  <span className="font-bold">{formatRupees(line.amount)}</span>
                </button>
              </li>
            ))}
          </ul>
          <p className="p-3 text-xs text-muted">{t('expense.hint')}</p>
        </section>
      )}
    </div>
  )
}

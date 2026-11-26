import { btn } from '../components/ui'
import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { VoiceButton } from '../components/VoiceButton'
import { api, NetworkError, request } from '../lib/api'
import { addExpense, strikeExpense, type ExpenseBody } from '../lib/data'
import { formatRupees } from '../lib/format'
import { shrinkPhoto } from '../lib/images'
import { listOps, subscribeOutbox } from '../lib/outbox'
type PaymentMode = 'cash' | 'upi'
import { KharchaMonth, MissingRegular } from './KharchaMonth'
import { MeterCard } from './MeterCard'
import { navigate } from '../router'
import { confirmAsk } from '../lib/ask'
import { DataTable } from '../components/DataTable'

export type ExpenseCategory = 'tea' | 'transport' | 'rent' | 'electricity' | 'salary' | 'purchase' | 'packaging' | 'repair' | 'home' | 'other'
  | 'tax' | 'loan' | 'equipment' | 'deposit' | 'advance' | 'interest'

/** Money out that is not a running cost, so it never lowers profit. The server keeps the same list. */
export const OUTSIDE_PROFIT: ExpenseCategory[] = ['purchase', 'home', 'tax', 'loan', 'equipment', 'deposit', 'advance']

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

interface AiStatus {
  consentedAt: string | null
  canReadPhotos: boolean
  canHearVoice: boolean
  keepDays: number
}

/** A line saved on the phone that hasn't reached the server yet (no signal): shown on the page all the same. */
interface PendingLine extends ExpenseLine {
  pending: true
}

/** Kharcha lines and strikes still waiting in the outbox, kept up to date as they send. */
function usePendingExpenses(shopId: string) {
  const [pending, setPending] = useState<{ lines: PendingLine[]; struck: Set<string> }>({ lines: [], struck: new Set() })
  useEffect(() => {
    let alive = true
    const load = () => void listOps(shopId).then((ops) => {
      if (!alive) return
      const lines = ops.filter((o) => o.kind === 'expense').map((o) => {
        const body = o.body as ExpenseBody
        return { ...body, category: (body.category ?? 'other') as ExpenseCategory, by: null, struckAt: null, pending: true as const }
      })
      setPending({ lines, struck: new Set(ops.filter((o) => o.kind === 'expenseStrike').map((o) => o.id.replace(':strike', ''))) })
    })
    load()
    const stop = subscribeOutbox(load)
    return () => {
      alive = false
      stop()
    }
  }, [shopId])
  return pending
}

/** India-time day (yyyy-mm-dd) of an instant. */
const indiaDay = (iso: string) => new Date(new Date(iso).getTime() + 5.5 * 3600_000).toISOString().slice(0, 10)

/**
 * With no signal the server can't read the line, so the phone splits it simply: each comma part with a number is a
 * line (amount, the words as its note, UPI if it says so). The heading is left for the server to choose when the line
 * reaches it (the same rules as "Read it").
 */
function readOffline(text: string): Omit<Guess, 'key'>[] {
  return text.split(/,|;|\n|\baur\b|और/i).flatMap((part) => {
    const amount = part.match(/\d+(?:\.\d{1,2})?/)?.[0]
    if (!amount) return []
    const upi = /\b(upi|online|gpay|phonepe|paytm)\b/i.test(part)
    const note = part.replace(/₹|rs\.?|\d+(?:\.\d{1,2})?|\b(upi|online|gpay|phonepe|paytm)\b/gi, ' ').replace(/\s+/g, ' ').trim()
    return [{ amount, category: 'other' as ExpenseCategory, note, mode: (upi ? 'upi' : 'cash') as PaymentMode, headingLater: true }]
  })
}

interface Guess {
  key: number
  /** Read on the phone with no signal: unless the person picks a heading, the server picks it on save. */
  headingLater?: boolean
  amount: string
  category: ExpenseCategory
  note: string
  mode: PaymentMode
}

const ICONS: Record<ExpenseCategory, string> = {
  tea: '☕', transport: '🛺', rent: '🏠', electricity: '💡', salary: '👷', purchase: '📦', packaging: '🛍️', repair: '🔧', home: '👪', other: '📝', tax: '🧾', loan: '🏦', equipment: '🧊', deposit: '🏧', advance: '🤝', interest: '💸',
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
export function ExpensesScreen({ shopId, shopName, isOwner, month = false }: { shopId: string; shopName: string; isOwner: boolean; month?: boolean }) {
  const { t } = useTranslation()
  // The month shows salary and money taken home: the owner's figures.
  const [view, setView] = useState<'day' | 'month'>(month && isOwner ? 'month' : 'day')
  const queryClient = useQueryClient()
  const today = indiaToday()
  const [day, setDay] = useState(today)
  const [text, setText] = useState('')
  const [guesses, setGuesses] = useState<Guess[]>([])
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const nextKey = useRef(1)
  const page = useQuery({ queryKey: [shopId, 'expenses', day], queryFn: () => api<ExpensePage>(`/v1/expenses?date=${day}`) })
  const ai = useQuery({ queryKey: ['ai-status'], queryFn: () => api<AiStatus>('/v1/me/ai') }).data
  const [askConsent, setAskConsent] = useState<'voice' | 'photo' | null>(null)
  const pending = usePendingExpenses(shopId)
  // The page = what the server has + what is still on the phone; a line the server already has isn't shown twice.
  // A strike still waiting to send shows as struck at once (struckAt only marks it; the server keeps the real time).
  const serverLines = page.data?.lines ?? []
  const waiting = pending.lines.filter((l) => indiaDay(l.occurredAt) === day && !serverLines.some((s) => s.id === l.id))
  const lines: (ExpenseLine | PendingLine)[] = [...serverLines, ...waiting]
    .map((l) => (pending.struck.has(l.id) && !l.struckAt ? { ...l, struckAt: l.occurredAt } : l))
    .sort((a, b) => a.occurredAt.localeCompare(b.occurredAt))
  const live = lines.filter((l) => !l.struckAt)
  const cashOut = live.filter((l) => l.mode === 'cash').reduce((sum, l) => sum + l.amount, 0)
  // Cash to one payee (the same note) on the day, already written plus being added: the ₹10,000 tax line is per person per day.
  const payee = (note: string | null | undefined) => (note ?? '').trim().toLowerCase()
  const cashTo = (g: Guess) => !payee(g.note) ? Number(g.amount) || 0
    : live.filter((l) => l.mode === 'cash' && payee(l.note) === payee(g.note)).reduce((sum, l) => sum + l.amount, 0)
      + guesses.filter((x) => x.mode === 'cash' && payee(x.note) === payee(g.note)).reduce((sum, x) => sum + (Number(x.amount) || 0), 0)
  const upiOut = live.filter((l) => l.mode === 'upi').reduce((sum, l) => sum + l.amount, 0)
  const label = new Date(`${day}T00:00:00Z`).toLocaleDateString(shownLocale(), { weekday: 'short', day: 'numeric', month: 'short', timeZone: 'UTC' })
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
    let found: Omit<Guess, 'key'>[]
    try {
      found = (await api<{ amount: number; category: ExpenseCategory; note: string | null; mode: PaymentMode }[]>('/v1/expenses/read', 'POST', { text }))
        .map((g) => ({ amount: String(g.amount), category: g.category, note: g.note ?? '', mode: g.mode }))
    } catch (e) {
      if (!(e instanceof NetworkError)) throw e
      found = readOffline(text)
    }
    if (found.length === 0) throw new Error(t('expense.nothingRead'))
    setGuesses([...guesses, ...found.map((g) => ({ ...g, key: nextKey.current++ }))])
    setText('')
  })

  const quick = (category: ExpenseCategory) =>
    setGuesses([...guesses, { key: nextKey.current++, amount: '', category, note: t(`expense.cat.${category}`), mode: 'cash' }])

  const update = (key: number, change: Partial<Guess>) => setGuesses(guesses.map((g) => (g.key === key ? { ...g, ...change } : g)))
  const ready = guesses.length > 0 && guesses.every((g) => Number(g.amount) > 0)

  // Saved on the phone first (works with no signal) and sent in order when there's a connection.
  const save = () => run(async () => {
    // The time it was written; a past page keeps its own date.
    const occurredAt = isToday ? new Date().toISOString() : `${day}T12:00:00+05:30`
    for (const g of guesses) {
      const category = g.headingLater && g.category === 'other' ? null : g.category
      await addExpense(shopId, { amount: Number(g.amount), category, note: g.note.trim() || null, mode: g.mode, occurredAt })
    }
    setGuesses([])
    await refresh()
  })

  const strike = async (line: ExpenseLine) => {
    const what = line.note ?? t(`expense.cat.${line.category}`)
    if ((await confirmAsk(t('expense.strikeAsk', { what, amount: formatRupees(line.amount) })))) void run(() => strikeExpense(shopId, line.id, what))
  }

  /** Voice and photos are read by AI services: only after the person agrees (recorded on the server, as for orders). */
  const agree = () => run(async () => {
    await request('/v1/me/ai/consent', 'POST', { agree: true })
    await queryClient.invalidateQueries({ queryKey: ['ai-status'] })
    setAskConsent(null)
  })

  const readPhoto = (file: File) => run(async () => {
    const imageDataUrl = await shrinkPhoto(file, 1500, 1_200_000, false)
    const found = await api<{ amount: number; category: ExpenseCategory; note: string | null; mode: PaymentMode }[]>('/v1/expenses/photo', 'POST', { imageDataUrl })
    if (found.length === 0) throw new Error(t('expense.photoNothing'))
    setGuesses((current) => [...current, ...found.map((g) => ({ key: nextKey.current++, amount: String(g.amount), category: g.category, note: g.note ?? '', mode: g.mode }))])
  })

  const viewSwitch = (
    <>
    <button onClick={() => navigate('/team')} className="flex min-h-12 items-center justify-between rounded-card bg-brand-soft px-4 text-left font-semibold text-brand-deep">
      <span>👥 {t('teamLink')}</span><span aria-hidden="true">→</span>
    </button>
    {isOwner && <div role="tablist" className="grid grid-cols-2 gap-1 rounded-2xl bg-white p-1 font-semibold">
      {(['day', 'month'] as const).map((v) => (
        <button key={v} role="tab" aria-selected={view === v} onClick={() => setView(v)} className={`min-h-11 rounded-xl ${view === v ? 'bg-brand text-white' : ''}`}>
          {t(`kharchaMonth.tab.${v}`)}
        </button>
      ))}
    </div>}
    </>
  )
  if (view === 'month') {
    return <div className="flex flex-col gap-3 p-4">{viewSwitch}<KharchaMonth shopId={shopId} shopName={shopName} /></div>
  }

  return (
    <div className="flex flex-col gap-3 p-4 lg:mx-auto lg:max-w-3xl xl:max-w-5xl">
      {viewSwitch}
      <div className="flex items-center justify-between rounded-card bg-white p-2 shadow-sm">
        <button onClick={() => setDay(shiftDay(day, -1))} aria-label={t('book.previous')} className="h-11 w-11 rounded-xl text-xl">‹</button>
        <span className="text-lg font-bold">{isToday ? t('expense.today') : label}</span>
        <button onClick={() => setDay(shiftDay(day, 1))} disabled={isToday} aria-label={t('book.next')} className="h-11 w-11 rounded-xl text-xl disabled:opacity-30">›</button>
      </div>

      {isOwner && isToday && (
        <MissingRegular shopId={shopId} skip={[...guesses.map((g) => g.category), ...pending.lines.map((l) => l.category)]}
          onRepeat={(l) => setGuesses([...guesses, { key: nextKey.current++, amount: String(l.amount), category: l.category, note: l.note ?? '', mode: l.mode }])} />
      )}

      {isToday && <MeterCard shopId={shopId} />}

      {/* Write: one line like the notebook, or tap a heading. */}
      <section className="flex flex-col gap-3 rounded-[20px] bg-white p-4">
        <label htmlFor="expense-text" className="text-[15px] font-bold">{t('expense.writeTitle')}</label>
        <textarea id="expense-text" value={text} onChange={(e) => setText(e.target.value)} rows={2} placeholder={t('expense.placeholder')}
          className="rounded-2xl border border-line p-3 text-lg" />
        <button disabled={busy || !text.trim()} onClick={() => void read()} className={`h-12 rounded-xl ${btn.solid}`}>
          {t('expense.read')}
        </button>
        {/* Say it, or snap the bill: shown only when the server has those switched on. */}
        {ai && (ai.canHearVoice || ai.canReadPhotos) && (
          <div className="grid grid-cols-2 gap-2">
            {ai.canHearVoice && (ai.consentedAt
              ? <VoiceButton onText={(heard) => setText((current) => (current ? `${current}, ${heard}` : heard))} onError={setError} />
              : <button onClick={() => setAskConsent('voice')} className="min-h-14 rounded-2xl bg-gradient-to-r from-rose-500 to-orange-400 font-bold text-white">🎤 {t('ai.speak')}</button>)}
            {ai.canReadPhotos && (ai.consentedAt ? (
              <label className="flex min-h-14 cursor-pointer items-center justify-center gap-2 rounded-2xl border-2 border-brand font-bold text-brand-strong">
                📷 {t('expense.photo')}
                <input type="file" accept="image/*" capture="environment" className="sr-only" disabled={busy}
                  onChange={(e) => { const file = e.target.files?.[0]; e.target.value = ''; if (file) void readPhoto(file) }} />
              </label>
            ) : (
              <button onClick={() => setAskConsent('photo')} className={`min-h-14 rounded-xl ${btn.outline}`}>📷 {t('expense.photo')}</button>
            ))}
          </div>
        )}
        {askConsent && ai && (
          <div className="flex flex-col gap-2 rounded-2xl bg-warn-soft p-3 text-[15px]">
            <p className="font-bold">{t('expense.consentTitle')}</p>
            <p>{askConsent === 'voice' ? t('expense.consentVoice') : t('expense.consentPhoto')}</p>
            <p className="text-sm text-muted">{t('expense.consentKeep')}</p>
            <div className="grid grid-cols-2 gap-2">
              <button disabled={busy} onClick={() => void agree()} className={`h-12 rounded-xl ${btn.solid}`}>{t('expense.consentYes')}</button>
              <button onClick={() => setAskConsent(null)} className="h-12 rounded-xl border border-line font-semibold transition hover:bg-[#f6faf9]">{t('expense.consentNo')}</button>
            </div>
          </div>
        )}
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
        <section className="flex flex-col gap-2 rounded-[20px] border-2 border-brand bg-white p-4">
          <h2 className="font-bold">{t('expense.checkTitle')}</h2>
          {guesses.map((g) => (
            <div key={g.key} className="flex flex-col gap-2 rounded-2xl bg-ground p-3">
              <div className="flex items-center gap-2">
                <label className="sr-only" htmlFor={`cat-${g.key}`}>{t('expense.heading')}</label>
                <select id={`cat-${g.key}`} value={g.category} onChange={(e) => update(g.key, { category: e.target.value as ExpenseCategory, headingLater: false })}
                  className="h-11 flex-1 rounded-xl border border-line bg-white px-2 outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10">
                  {ALL.map((c) => <option key={c} value={c}>{ICONS[c]} {t(`expense.cat.${c}`)}</option>)}
                </select>
                <label className="sr-only" htmlFor={`amt-${g.key}`}>{t('expense.amount')}</label>
                <input id={`amt-${g.key}`} value={g.amount} onChange={(e) => update(g.key, { amount: e.target.value.replace(/[^\d.]/g, '') })}
                  inputMode="decimal" placeholder="₹" autoFocus={!g.amount} className="h-11 w-28 rounded-xl border border-line bg-white px-3 text-right text-lg font-bold" />
              </div>
              <div className="flex items-center gap-2">
                <label className="sr-only" htmlFor={`note-${g.key}`}>{t('expense.note')}</label>
                <input id={`note-${g.key}`} value={g.note} onChange={(e) => update(g.key, { note: e.target.value })} maxLength={120}
                  className="h-11 min-w-0 flex-1 rounded-xl border border-line bg-white px-3 outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
                {(['cash', 'upi'] as const).map((m) => (
                  <button key={m} onClick={() => update(g.key, { mode: m })} aria-pressed={g.mode === m}
                    className={`h-11 rounded-xl px-3 text-sm font-semibold ${g.mode === m ? 'bg-brand text-white shadow-sm' : 'border border-line bg-white'}`}>{t(m)}</button>
                ))}
                <button onClick={() => setGuesses(guesses.filter((x) => x.key !== g.key))} aria-label={t('expense.remove')} className="h-11 w-11 rounded-xl text-xl text-muted">×</button>
              </div>
              {/* A business payment above ₹10,000 in cash to one person in a day isn't allowed as an expense for tax. */}
              {g.mode === 'cash' && cashTo(g) > 10_000 && !['deposit', 'home', 'tax', 'loan', 'advance'].includes(g.category) && (
                <p role="status" className="rounded-xl bg-warn-soft p-2 text-sm text-warn">{t('cashLimit.expense')}{cashTo(g) > (Number(g.amount) || 0) && ` ${t('cashLimit.expenseDay', { total: formatRupees(cashTo(g)) })}`}</p>
              )}
            </div>
          ))}
          <button disabled={busy || !ready} onClick={() => void save()} className={`h-12 rounded-xl ${btn.solid}`}>
            {t('expense.save', { count: guesses.length })}
          </button>
        </section>
      )}

      {error && <p className="rounded-xl bg-due-soft p-3 text-due">{error}</p>}

      {/* The page itself: ruled lines and a margin, like the kharcha book. */}
      {(page.data || waiting.length > 0) && (
        <section className="overflow-hidden rounded-[20px] border border-[#e8dfc8] bg-[#fffdf6]">
          <div className="grid grid-cols-3 gap-2 border-b border-[#e8dfc8] p-3 text-center">
            <div><div className="text-xs text-muted">{t('expense.cashOut')}</div><div className="text-lg font-extrabold text-due">{formatRupees(cashOut)}</div></div>
            <div><div className="text-xs text-muted">{t('expense.upiOut')}</div><div className="text-lg font-extrabold">{formatRupees(upiOut)}</div></div>
            <div><div className="text-xs text-muted">{t('expense.total')}</div><div className="text-lg font-extrabold">{formatRupees(live.reduce((sum, l) => sum + l.amount, 0))}</div></div>
          </div>
          {/* A computer: the day's kharcha as a table to sort, filter and download; a phone keeps the notebook page. */}
          <div className="hidden p-3 lg:block">
            <DataTable label={t('expense.total')} rows={lines} rowKey={(l) => l.id} download={`kharcha-${day}`}
              onRowClick={(l) => { if (!l.struckAt) void strike(l) }}
              empty={<p className="p-3 text-muted">{t('expense.empty')}</p>}
              columns={[
                { key: 'time', header: t('cashTable.date'), width: 'w-28', sortValue: (l) => l.occurredAt,
                  render: (l) => new Date(l.occurredAt).toLocaleTimeString(shownLocale(), { hour: 'numeric', minute: '2-digit' }), csv: (l) => new Date(l.occurredAt).toLocaleString() },
                { key: 'heading', header: t('expense.heading'), sortValue: (l) => t(`expense.cat.${l.category}`),
                  filter: { kind: 'select', value: (l) => l.category, options: [...new Set(lines.map((l) => l.category))].map((c) => ({ value: c, label: t(`expense.cat.${c}`) })) },
                  render: (l) => <span className={l.struckAt ? 'text-muted line-through' : 'font-semibold'}>{ICONS[l.category]} {t(`expense.cat.${l.category}`)}</span>, csv: (l) => t(`expense.cat.${l.category}`) },
                { key: 'note', header: t('expense.note'), filter: { kind: 'text', value: (l) => l.note ?? '' },
                  render: (l) => <span className={l.struckAt ? 'text-muted line-through' : ''}>{l.note ?? '—'}{'pending' in l ? ` · ⏳ ${t('expense.waiting')}` : ''}</span>, csv: (l) => l.note ?? '' },
                { key: 'mode', header: t('loans.paidBy'), width: 'w-28', filter: { kind: 'select', value: (l) => l.mode, options: [...new Set(lines.map((l) => l.mode))].map((m) => ({ value: m, label: t(m) })) },
                  render: (l) => t(l.mode), csv: (l) => t(l.mode) },
                { key: 'by', header: t('expenseTable.by'), width: 'w-36', sortValue: (l) => l.by ?? '', render: (l) => l.by ?? '—', csv: (l) => l.by ?? '' },
                { key: 'amount', header: t('expense.amount'), align: 'right', width: 'w-32', sortValue: (l) => l.amount,
                  render: (l) => <b className={l.struckAt ? 'text-muted line-through' : ''}>{formatRupees(l.amount)}</b>, csv: (l) => (l.struckAt ? 0 : l.amount) },
              ]}
              footer={[t('yearRep.total'), '', '', '', '', formatRupees(live.reduce((sum, l) => sum + l.amount, 0))]} />
          </div>
          <ul className="border-l-2 border-red-300 pl-3 lg:hidden" style={{ backgroundImage: 'repeating-linear-gradient(transparent 0 55px, #e8dfc8 55px 56px)' }}>
            {lines.length === 0 && <li className="flex min-h-14 items-center pr-3 text-muted">{t('expense.empty')}</li>}
            {lines.map((line) => (
              <li key={line.id}>
                <button onClick={() => !line.struckAt && strike(line)} disabled={!!line.struckAt}
                  className={`flex min-h-14 w-full items-center gap-2 pr-3 text-left ${line.struckAt ? 'text-muted line-through' : ''}`}>
                  <span aria-hidden className="text-xl">{ICONS[line.category]}</span>
                  <span className="flex min-w-0 flex-1 flex-col">
                    <span className="truncate font-semibold">{line.note ?? t(`expense.cat.${line.category}`)}</span>
                    <span className="text-xs text-muted">
                      {new Date(line.occurredAt).toLocaleTimeString(shownLocale(), { hour: 'numeric', minute: '2-digit' })} · {t(line.mode)}{line.by ? ` · ${line.by}` : ''}{'pending' in line ? ` · ⏳ ${t('expense.waiting')}` : ''}
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

import { DataTable } from '../components/DataTable'
import { btn } from '../components/ui'
import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees, indiaToday } from '../lib/format'
import { uuidv7 } from '../lib/ids'
import { navigate } from '../router'
import { SortHead } from '../components/TableHead'
import { compareCells, filterBox, nextSort, type Sort } from '../lib/tableSort'

interface Line { at: string; source: string; who: string; amount: number; balance: number; moveId: string | null; billId: string | null }
interface Day { date: string; in: number; out: number; closing: number; counted: number | null; difference: number | null }
interface Ledger { from: string; to: string; opening: number; openingCountedOn: string | null; in: number; out: number; closing: number; lines: Line[]; days: Day[] }
type Kind = 'added' | 'removed' | 'to_bank' | 'from_bank'

const monthOf = (day: string) => day.slice(0, 7)
const lastDay = (month: string) => { const [y, m] = month.split('-').map(Number); return new Date(Date.UTC(y, m, 0)).toISOString().slice(0, 10) }
const shiftMonth = (month: string, by: number) => { const [y, m] = month.split('-').map(Number); return new Date(Date.UTC(y, m - 1 + by, 1)).toISOString().slice(0, 7) }
const when = (iso: string) => new Date(iso).toLocaleString(shownLocale(), { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })

/**
 * Cash in hand, a month at a time: the opening (the last galla count before it), every rupee in and out of the drawer
 * with the balance after it, each day's close against the count, and a form to move cash by hand or to and from the bank.
 */
export function CashLedgerScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const client = useQueryClient()
  const today = indiaToday()
  const [month, setMonth] = useState(monthOf(today))
  const to = month === monthOf(today) ? today : lastDay(month)
  const ledger = useQuery({ queryKey: [shopId, 'cash-ledger', month], queryFn: () => api<Ledger>(`/v1/cash-ledger?from=${month}-01&to=${to}`) })
  const accounts = useQuery({ queryKey: [shopId, 'bank-accounts'], queryFn: () => api<{ accounts: { id: string; name: string }[]; deposit: string }>('/v1/bank-book/accounts') }).data
  const [kind, setKind] = useState<Kind | null>(null)
  const [amount, setAmount] = useState('')
  const [note, setNote] = useState('')
  const [account, setAccount] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const data = ledger.data
  const label = new Date(`${month}-01T00:00:00Z`).toLocaleDateString(shownLocale(), { month: 'long', year: 'numeric', timeZone: 'UTC' })
  const bankMove = kind === 'to_bank' || kind === 'from_bank'
  const needsNote = kind === 'added' || kind === 'removed'
  const ready = !!kind && Number(amount) > 0 && (!needsNote || note.trim().length > 0)
  // Desktop cash book: sort by a column, filter by type, party and which way the money went.
  type ColKey = 'at' | 'type' | 'who' | 'in' | 'out'
  const [sort, setSort] = useState<Sort<ColKey>>({ key: 'at', up: true })
  const [filter, setFilter] = useState({ type: '', who: '', way: '' })
  const typeOf = (l: Pick<Line, 'source'>) => t(`book.sources.${l.source}`, { defaultValue: t(`cashLedger.sources.${l.source}`, { defaultValue: l.source }) })
  const rows = (data?.lines ?? []).map((l, i) => ({ ...l, n: i + 1 }))
    .filter((l) => (!filter.type || l.source === filter.type) && (!filter.who || (l.who ?? '').toLowerCase().includes(filter.who.toLowerCase()))
      && (!filter.way || (filter.way === 'in' ? l.amount > 0 : l.amount < 0)))
    .sort((a, b) => {
      const val = (l: Line) => sort.key === 'at' ? l.at : sort.key === 'type' ? typeOf(l) : sort.key === 'who' ? (l.who || null) : sort.key === 'in' ? (l.amount > 0 ? l.amount : null) : (l.amount < 0 ? -l.amount : null)
      const c = compareCells(val(a), val(b))
      return sort.up ? c : -c
    })
  const filtered = !!(filter.type || filter.who || filter.way)
  const sources = [...new Set((data?.lines ?? []).map((l) => l.source))]

  /** The month's cash book as a CSV for Excel (the BOM keeps the rupee sign and Hindi intact). */
  function download() {
    if (!data) return
    const cell = (v: string | number) => (/[",\n]/.test(String(v)) ? `"${String(v).replace(/"/g, '""')}"` : String(v))
    const table: (string | number)[][] = [[t('cashTable.date'), t('cashTable.type'), t('cashTable.party'), t('cashTable.in'), t('cashTable.out'), t('cashTable.balance')],
      [data.from, t('cashTable.openingRow'), '', '', '', data.opening],
      ...data.lines.map((l) => [new Date(l.at).toLocaleString(), typeOf(l), l.who ?? '', l.amount > 0 ? l.amount : '', l.amount < 0 ? -l.amount : '', l.balance]),
      [data.to, t('cashTable.closingRow'), '', data.in, data.out, data.closing]]
    const a = document.createElement('a')
    a.href = URL.createObjectURL(new Blob(['\ufeff' + table.map((row) => row.map(cell).join(',')).join('\n')], { type: 'text/csv' }))
    a.download = `cash-ledger-${month}.csv`
    a.click()
    setTimeout(() => URL.revokeObjectURL(a.href), 10_000)
  }

  async function save() {
    if (!ready || !kind) return
    setBusy(true)
    setError(null)
    try {
      await api('/v1/cash-moves', 'POST', { id: uuidv7(), kind, amount: Number(amount), note: note.trim() || null, bankAccount: bankMove ? account || null : null })
      setKind(null); setAmount(''); setNote('')
      await client.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  async function strike(id: string) {
    await api(`/v1/cash-moves/${id}/strike`, 'POST', {})
    await client.invalidateQueries({ queryKey: [shopId] })
  }

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-3 p-4 lg:max-w-5xl">
      <div className="flex w-fit flex-wrap gap-1 rounded-2xl bg-[#edf2f1] p-1 text-sm">
        <button onClick={() => navigate('/cashbook')} className="h-10 rounded-xl px-4 font-semibold text-muted transition hover:text-ink">{t('bankBook.cashBook')}</button>
        <span className="flex h-10 items-center rounded-xl bg-brand px-4 font-semibold text-white shadow-md shadow-teal-900/20">{t('cashLedger.title')}</span>
        <button onClick={() => navigate('/bankbook')} className="h-10 rounded-xl px-4 font-semibold text-muted transition hover:text-ink">{t('bankBook.title')}</button>
      </div>
      <div className="flex items-center justify-between rounded-card bg-white p-2 shadow-sm">
        <button onClick={() => setMonth(shiftMonth(month, -1))} aria-label={t('book.previous')} className="flex h-11 w-11 items-center justify-center rounded-xl hover:bg-[#f1f5f4]"><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M15 18l-6-6 6-6" /></svg></button>
        <span className="flex items-center gap-2 text-lg font-bold"><span className="text-brand"><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M4 5h16v15H4zM4 9h16M8 3v4M16 3v4" /></svg></span>{label}</span>
        <button onClick={() => setMonth(shiftMonth(month, 1))} disabled={month >= monthOf(today)} aria-label={t('book.next')} className="flex h-11 w-11 items-center justify-center rounded-xl hover:bg-[#f1f5f4] disabled:opacity-30"><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M9 6l6 6-6 6" /></svg></button>
      </div>
      {ledger.isError && <p className="text-due">{ledger.error.message}</p>}
      {data && (
        <section className="flex flex-col gap-2">
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            <span className="flex min-h-[76px] flex-col justify-between rounded-card bg-white p-3 shadow-sm ring-1 ring-black/5"><span className="text-xs font-semibold text-muted">{t('bankBook.opening')}</span><b className="text-xl">{formatRupees(data.opening)}</b></span>
            <span className="flex min-h-[76px] flex-col justify-between rounded-card bg-emerald-50 p-3 shadow-sm ring-1 ring-black/5"><span className="flex items-center gap-1.5 text-xs font-semibold text-emerald-800"><svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M12 19V5M5 12l7-7 7 7" /></svg>{t('bankBook.in')}</span><b className="text-xl text-jama">+{formatRupees(data.in)}</b></span>
            <span className="flex min-h-[76px] flex-col justify-between rounded-card bg-due-soft p-3 shadow-sm ring-1 ring-black/5"><span className="flex items-center gap-1.5 text-xs font-semibold text-due"><svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M12 5v14M5 12l7 7 7-7" /></svg>{t('bankBook.out')}</span><b className="text-xl text-due">−{formatRupees(data.out)}</b></span>
            <span className="flex min-h-[76px] flex-col justify-between rounded-card bg-[#0b5e4f] p-3 text-white shadow-md shadow-[#0b5e4f]/25"><span className="flex items-center gap-1.5 text-xs font-semibold text-white/85"><svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M3 7h18v13H3zM3 7l3-4h12l3 4M9 12h6" /></svg>{t('cashLedger.inHand')}</span><b className="text-xl">{formatRupees(data.closing)}</b></span>
          </div>
          <p className="px-1 text-xs text-muted">{data.openingCountedOn ? t('cashLedger.openingFrom', { date: data.openingCountedOn }) : t('cashLedger.noCount')}</p>
        </section>
      )}

      <section className="flex flex-col gap-3 rounded-[20px] bg-white p-4 text-sm shadow-sm">
        <b className="text-base">{t('cashLedger.move')}</b>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          {([
            { k: 'added', d: 'M12 5v14M5 12h14', on: 'bg-[#0b5e4f] text-white shadow-lg shadow-[#0b5e4f]/30', off: 'bg-[#e3f2ee] text-[#0b5e4f] hover:bg-[#d2ebe4]' },
            { k: 'removed', d: 'M5 12h14', on: 'bg-[#be123c] text-white shadow-lg shadow-[#be123c]/30', off: 'bg-[#fde8ec] text-[#be123c] hover:bg-[#fbd5dd]' },
            { k: 'to_bank', d: 'M3 10l9-6 9 6M5 10v8M9 10v8M15 10v8M19 10v8M3 21h18', on: 'bg-[#1d4ed8] text-white shadow-lg shadow-[#1d4ed8]/30', off: 'bg-[#e8effd] text-[#1d4ed8] hover:bg-[#dbe5fc]' },
            { k: 'from_bank', d: 'M3 7h18v13H3zM3 7l3-4h12l3 4M12 10v6M9.5 13.5L12 16l2.5-2.5', on: 'bg-[#6d28d9] text-white shadow-lg shadow-[#6d28d9]/30', off: 'bg-[#f1eafd] text-[#6d28d9] hover:bg-[#e7dcfb]' },
          ] as const).map((x) => (
            <button key={x.k} onClick={() => setKind(kind === x.k ? null : x.k)} aria-pressed={kind === x.k}
              className={`flex min-h-[52px] items-center gap-2.5 rounded-2xl px-3 text-left font-bold transition active:scale-[0.98] ${kind === x.k ? x.on : x.off}`}>
              <span aria-hidden className={`flex h-[30px] w-[30px] shrink-0 items-center justify-center rounded-full ${kind === x.k ? 'bg-white/20' : 'bg-white'}`}>
                <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d={x.d} /></svg>
              </span>
              <span className="leading-tight">{t(`cashLedger.kinds.${x.k}`)}</span>
            </button>
          ))}
        </div>
        {kind && (
          <div className="flex flex-wrap items-end gap-2">
            <label className="flex flex-col gap-1">{t('expense.amount')}
              <input value={amount} onChange={(e) => setAmount(e.target.value.replace(/[^\d.]/g, ''))} inputMode="decimal" className="h-11 w-36 rounded-xl border border-line px-3 bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /></label>
            <label className="flex min-w-48 flex-1 flex-col gap-1">{needsNote ? t('cashLedger.why') : t('expense.note')}
              <input value={note} onChange={(e) => setNote(e.target.value.slice(0, 120))} className="h-11 rounded-xl border border-line px-3 bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /></label>
            {bankMove && (accounts?.accounts.length ?? 0) > 1 && (
              <label className="flex flex-col gap-1">{t('cashLedger.account')}
                <select value={account || accounts!.deposit} onChange={(e) => setAccount(e.target.value)} className="h-11 rounded-xl border border-line px-2 bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10">
                  {accounts!.accounts.map((a) => <option key={a.id} value={a.id}>{a.name}</option>)}
                </select></label>
            )}
            <button disabled={!ready || busy} onClick={() => void save()} className={`h-11 rounded-xl px-5 ${btn.solid}`}>{t('bankBook.save')}</button>
          </div>
        )}
        {error && <p className="text-due">{error}</p>}
      </section>

      {data && data.lines.length > 0 && (
        <div className="hidden flex-col gap-2 lg:flex">
          <div className="flex items-center justify-between">
            <h2 className="px-1 font-bold">{t('cashTable.title', { month: label })}</h2>
            <button onClick={download} className={`flex min-h-9 items-center gap-1.5 rounded-full px-3 text-sm ${btn.outline}`}><svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M12 3v12M7 10l5 5 5-5M4 15v5h16v-5" /></svg>{t('billList.excel')}</button>
          </div>
          <div className="overflow-x-auto rounded-card shadow-sm">
          <table className="w-full min-w-[920px] table-fixed border-collapse bg-white text-sm">
            <thead className="bg-brand-soft text-left text-xs tracking-wide">
              <tr className="align-top">
                <th className="w-10 px-3 py-2 uppercase">#</th>
                <th className="w-36 px-2 py-2"><SortHead label={t('cashTable.date')} active={sort.key === 'at'} up={sort.up} onClick={() => setSort(nextSort(sort, 'at', true))} sortLabel={t('billList.sortBy', { col: t('cashTable.date') })} /></th>
                <th className="w-36 px-2 py-2"><SortHead label={t('cashTable.type')} active={sort.key === 'type'} up={sort.up} onClick={() => setSort(nextSort(sort, 'type', true))} sortLabel={t('billList.sortBy', { col: t('cashTable.type') })}>
                  <select value={filter.type} onChange={(e) => setFilter({ ...filter, type: e.target.value })} aria-label={t('billList.filterBy', { col: t('cashTable.type') })} className={filterBox}>
                    <option value="">{t('billList.status.all')}</option>
                    {sources.map((src) => <option key={src} value={src}>{typeOf({ source: src })}</option>)}
                  </select></SortHead></th>
                <th className="px-2 py-2"><SortHead label={t('cashTable.party')} active={sort.key === 'who'} up={sort.up} onClick={() => setSort(nextSort(sort, 'who', true))} sortLabel={t('billList.sortBy', { col: t('cashTable.party') })}>
                  <input value={filter.who} onChange={(e) => setFilter({ ...filter, who: e.target.value })} aria-label={t('billList.filterBy', { col: t('cashTable.party') })} className={filterBox} /></SortHead></th>
                <th className="w-28 px-2 py-2 text-right"><SortHead right label={t('cashTable.in')} active={sort.key === 'in'} up={sort.up} onClick={() => setSort(nextSort(sort, 'in', false))} sortLabel={t('billList.sortBy', { col: t('cashTable.in') })}>
                  <select value={filter.way} onChange={(e) => setFilter({ ...filter, way: e.target.value })} aria-label={t('billList.filterBy', { col: t('cashTable.way') })} className={filterBox}>
                    <option value="">{t('cashTable.both')}</option><option value="in">{t('cashTable.onlyIn')}</option><option value="out">{t('cashTable.onlyOut')}</option>
                  </select></SortHead></th>
                <th className="w-28 px-2 py-2 text-right"><SortHead right label={t('cashTable.out')} active={sort.key === 'out'} up={sort.up} onClick={() => setSort(nextSort(sort, 'out', false))} sortLabel={t('billList.sortBy', { col: t('cashTable.out') })} /></th>
                <th className="w-28 px-2 py-2 text-right uppercase">{t('cashTable.balance')}</th>
                <th className="w-16 px-3 py-2" />
              </tr>
            </thead>
            <tbody className="divide-y divide-line">
              {!filtered && sort.key === 'at' && sort.up && (
                <tr className="bg-[#f6faf9] font-semibold">
                  <td className="px-3 py-2" /><td className="px-2 py-2">{new Date(`${data.from}T00:00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })}</td>
                  <td className="px-2 py-2" colSpan={2}>{t('cashTable.openingRow')}</td><td /><td /><td className="px-2 py-2 text-right">{formatRupees(data.opening)}</td><td />
                </tr>
              )}
              {rows.length === 0 && <tr><td colSpan={8} className="px-3 py-3 text-center text-muted">{t('stockTable.noMatch')}</td></tr>}
              {rows.map((l) => (
                <tr key={`${l.n}-${l.at}`} className="hover:bg-[#f6faf9]">
                  <td className="px-3 py-2 text-muted">{l.n}</td>
                  <td className="whitespace-nowrap px-2 py-2">{when(l.at)}</td>
                  <td className="px-2 py-2 font-semibold">{typeOf(l)}</td>
                  <td className="truncate px-2 py-2" title={l.who}>{l.who || '—'}</td>
                  <td className="px-2 py-2 text-right font-semibold text-jama">{l.amount > 0 ? formatRupees(l.amount) : ''}</td>
                  <td className="px-2 py-2 text-right font-semibold text-udhaar">{l.amount < 0 ? formatRupees(-l.amount) : ''}</td>
                  <td className="px-2 py-2 text-right tabular-nums">{formatRupees(l.balance)}</td>
                  <td className="px-3 py-2 text-right">
                    {l.billId ? <button onClick={() => navigate(`/print/bill/${l.billId}`)} className="text-xs font-semibold text-brand underline">{t('cashTable.openBill')}</button>
                      : l.moveId ? <button onClick={() => void strike(l.moveId!)} className="text-xs text-muted underline">{t('expense.remove')}</button> : null}
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="border-t-2 border-ink font-bold">
                <td colSpan={4} className="px-3 py-2">{filtered ? t('cashTable.shown', { count: rows.length }) : t('cashTable.closingRow')}</td>
                <td className="px-2 py-2 text-right text-jama">{formatRupees(filtered ? rows.reduce((n, l) => n + Math.max(0, l.amount), 0) : data.in)}</td>
                <td className="px-2 py-2 text-right text-udhaar">{formatRupees(filtered ? rows.reduce((n, l) => n + Math.max(0, -l.amount), 0) : data.out)}</td>
                <td className="px-2 py-2 text-right">{filtered ? '' : formatRupees(data.closing)}</td>
                <td />
              </tr>
            </tfoot>
          </table>
          </div>
        </div>
      )}
      {data && (data.lines.length === 0 ? <p className="text-muted">{t('cashLedger.empty')}</p> : (
        <ul className="flex flex-col divide-y divide-line rounded-[20px] bg-white lg:hidden">
          {data.lines.map((l, i) => (
            <li key={i} className="flex items-center gap-3 p-3 text-sm">
              <div className="flex flex-1 flex-col">
                <span className="font-semibold">{t(`book.sources.${l.source}`, { defaultValue: t(`cashLedger.sources.${l.source}`, { defaultValue: l.source }) })}{l.who ? ` · ${l.who}` : ''}</span>
                <span className="text-xs text-muted">{when(l.at)}</span>
              </div>
              <span className="flex flex-col items-end">
                <b className={l.amount < 0 ? 'text-udhaar' : 'text-jama'}>{l.amount < 0 ? '−' : '+'}{formatRupees(Math.abs(l.amount))}</b>
                <span className="text-xs text-muted">{formatRupees(l.balance)}</span>
              </span>
              {l.moveId && <button onClick={() => void strike(l.moveId!)} aria-label={t('expense.remove')} className="h-11 w-11 text-xl text-muted">×</button>}
            </li>
          ))}
        </ul>
      ))}

      {data && data.days.some((d) => d.counted !== null) && (
        <section className="flex flex-col gap-2 text-sm">
          <h2 className="px-1 font-bold">{t('cashLedger.closes')}</h2>
          <DataTable label={t('cashLedger.closes')} rows={data.days.filter((d) => d.in || d.out || d.counted !== null)} rowKey={(d) => d.date}
            columns={[
              { key: 'day', header: t('rv.day'), sortValue: (d) => d.date, render: (d) => new Date(d.date).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' }) },
              { key: 'in', header: t('bankBook.in'), align: 'right', sortValue: (d) => d.in, render: (d) => formatRupees(d.in) },
              { key: 'out', header: t('bankBook.out'), align: 'right', sortValue: (d) => d.out, render: (d) => formatRupees(d.out) },
              { key: 'hand', header: t('cashLedger.inHand'), align: 'right', sortValue: (d) => d.closing, render: (d) => formatRupees(d.closing) },
              { key: 'diff', header: t('cashLedger.diff'), align: 'right', sortValue: (d) => d.difference ?? 0,
                render: (d) => <span className={(d.difference ?? 0) < 0 ? 'text-orange-700' : ''}>{d.counted === null ? '–' : formatRupees(d.difference ?? 0)}</span> },
            ]} />
        </section>
      )}
    </div>
  )
}

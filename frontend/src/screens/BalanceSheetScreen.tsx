import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { confirmAsk } from '../lib/ask'
import { ReportShell, type ReportView } from '../components/ReportShell'
import { Card, Input, Select } from '../components/ui'
import { btn } from '../lib/buttonStyles'
import { shownLocale } from '../i18n'
import { api } from '../lib/api'
import { formatRupees, indiaToday } from '../lib/format'
import { navigate } from '../router'

type Kind = 'fixed_asset' | 'other_asset' | 'loan' | 'other_liability'
interface Item { id: string; kind: Kind; name: string; amount: number; since: string; until: string | null }
interface CapitalSplit { financialYear: string; opening: number; startedWith: number; itemsNet: number; profit: number; broughtIn: number; takenHome: number; unexplained: number }
interface BalanceSheet {
  on: string
  cash: number; bank: number; stock: number; customersOwe: number; supplierAdvances: number; totalAssets: number
  suppliersOwed: number; customerAdvances: number; totalLiabilities: number
  capital: number; customersOwing: number; suppliersOwedCount: number; unvaluedItems: number
  fixedAssets: number; otherAssets: number; loans: number; otherLiabilities: number; gstToPay: number
  items: Item[]; capitalSplit: CapitalSplit | null
}

const KINDS: Kind[] = ['fixed_asset', 'other_asset', 'loan', 'other_liability']
const isAsset = (k: Kind) => k === 'fixed_asset' || k === 'other_asset'

/**
 * The balance sheet for the CA and the bank: what the shop has against what it owes at the end of a day, with the
 * owner's capital as what is left. Every line opens the screen it comes from, so each figure can be checked; things
 * the daily records don't hold (furniture, a loan, a deposit) are kept here by hand.
 */
export function BalanceSheetScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [on, setOn] = useState(indiaToday())
  const [view, setView] = useState<ReportView>('chart')
  const [form, setForm] = useState<{ id: string; kind: Kind; name: string; amount: string; since: string } | null>(null)
  const [message, setMessage] = useState('')
  const sheet = useQuery({ queryKey: [shopId, 'balance-sheet', on], queryFn: () => api<BalanceSheet>(`/v1/reports/balance-sheet?on=${on}`) }).data
  const day = new Date(`${on}T00:00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'long', year: 'numeric' })
  const refresh = () => queryClient.invalidateQueries({ queryKey: [shopId, 'balance-sheet'] })

  const row = (label: string, amount: number, path: string | null, note?: string, onClick?: () => void) => {
    const go = onClick ?? (path ? () => navigate(path) : undefined)
    return (
      <button key={label + note} onClick={go} disabled={!go} className="flex min-h-12 w-full items-center justify-between gap-3 border-t border-line py-2.5 text-left first:border-t-0">
        <span className="flex flex-col"><span className="font-semibold">{label}</span>{note && <span className="text-xs text-muted">{note}</span>}</span>
        <span className="shrink-0 font-semibold tabular-nums">{formatRupees(amount)}{go ? ' ›' : ''}</span>
      </button>
    )
  }
  const itemRow = (i: Item) => row(i.name, i.amount, null, `${t(`bs.kind.${i.kind}`)} · ${t('bs.since', { date: new Date(`${i.since}T00:00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric' }) })}`,
    () => setForm({ id: i.id, kind: i.kind, name: i.name, amount: String(i.amount), since: i.since }))

  async function save() {
    if (!form) return
    setMessage('')
    try {
      await api(`/v1/balance-items/${form.id}`, 'PUT', { id: form.id, kind: form.kind, name: form.name.trim(), amount: Number(form.amount), since: form.since })
      setForm(null)
      await refresh()
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  async function end() {
    if (!form || !(await confirmAsk(t('bs.endConfirm', { name: form.name })))) return
    await api(`/v1/balance-items/${form.id}`, 'PUT', { id: form.id, kind: form.kind, name: form.name.trim(), amount: Number(form.amount), since: form.since, until: indiaToday() })
    setForm(null)
    await refresh()
  }

  const split = sheet?.capitalSplit
  return (
    <ReportShell page="balance" title={t('bs.title')} isOwner view={view} onView={setView}
      ticks={sheet ? [{ label: t('bs.assets'), value: formatRupees(sheet.totalAssets) }, { label: t('bs.liabilities'), value: formatRupees(sheet.totalLiabilities) }, { label: t('bs.capital'), value: formatRupees(sheet.capital) }] : []}>
      <div className="mx-auto flex w-full max-w-3xl flex-col gap-3 p-4 lg:max-w-5xl">
        <label className="flex flex-col gap-1 text-sm font-semibold text-muted">{t('bs.on')}
          <Input type="date" value={on} max={indiaToday()} onChange={(e) => e.target.value && setOn(e.target.value)} className="text-base text-ink" />
        </label>
        {!sheet ? <p className="text-muted">…</p> : (
          <>
            {/* A computer: the balance sheet as it is printed, liabilities and capital on the left, assets on the right. */}
            {(() => {
              type Line = { label: string; amount: number; note?: string; go?: () => void }
              const lineFor = (label: string, amount: number, path: string | null, note?: string): Line => ({ label, amount, note, go: path ? () => navigate(path) : undefined })
              const itemLine = (i: Item): Line => ({ label: i.name, amount: i.amount, note: t(`bs.kind.${i.kind}`), go: () => setForm({ id: i.id, kind: i.kind, name: i.name, amount: String(i.amount), since: i.since }) })
              const left: Line[] = [
                { label: t('bs.capital'), amount: sheet.capital, note: t('bs.tableCapitalNote') },
                lineFor(t('bs.suppliersOwed'), sheet.suppliersOwed, '/suppliers', t('bs.suppliers', { count: sheet.suppliersOwedCount })),
                ...(sheet.customerAdvances > 0 ? [lineFor(t('bs.customerAdvances'), sheet.customerAdvances, '/khata')] : []),
                ...(sheet.gstToPay > 0 ? [lineFor(t('bs.gstToPay'), sheet.gstToPay, '/gst')] : []),
                ...(sheet.loans - sheet.items.filter((i) => i.kind === 'loan').reduce((n, i) => n + i.amount, 0) > 0.005 ? [lineFor(t('bs.loanAccounts'), sheet.loans - sheet.items.filter((i) => i.kind === 'loan').reduce((n, i) => n + i.amount, 0), '/loans')] : []),
                ...sheet.items.filter((i) => !isAsset(i.kind)).map(itemLine),
              ]
              const right: Line[] = [
                lineFor(t('bs.cash'), sheet.cash, '/cashledger'),
                lineFor(t('bs.bank'), sheet.bank, '/bankbook'),
                lineFor(t('bs.stock'), sheet.stock, '/stock-statement', sheet.unvaluedItems > 0 ? t('bs.unvalued', { count: sheet.unvaluedItems }) : t('bs.atCost')),
                lineFor(t('bs.customersOwe'), sheet.customersOwe, '/dues', t('bs.people', { count: sheet.customersOwing })),
                ...(sheet.supplierAdvances > 0 ? [lineFor(t('bs.supplierAdvances'), sheet.supplierAdvances, '/suppliers')] : []),
                ...sheet.items.filter((i) => isAsset(i.kind)).map(itemLine),
              ]
              const rows = Math.max(left.length, right.length)
              const cells = (l: Line | undefined) => l ? <>
                <td className="border-r border-line px-3 py-2">
                  {l.go ? <button onClick={l.go} className="text-left font-semibold hover:text-brand hover:underline">{l.label}</button> : <span className="font-semibold">{l.label}</span>}
                  {l.note && <span className="block text-xs text-muted">{l.note}</span>}
                </td>
                <td className="border-r border-line px-3 py-2 text-right tabular-nums">{formatRupees(l.amount)}</td>
              </> : <><td className="border-r border-line" /><td className="border-r border-line" /></>
              return (
                <table className="hidden w-full table-fixed border-collapse overflow-hidden rounded-card bg-white text-sm shadow-sm lg:table">
                  <caption className="pb-2 text-left text-sm text-muted">{t('bs.asOf', { day })}</caption>
                  <thead className="bg-brand-soft text-left text-xs uppercase tracking-wide">
                    <tr>
                      <th className="border-r border-line px-3 py-2">{t('bs.tableLeft')}</th><th className="w-36 border-r border-line px-3 py-2 text-right">{t('bs.tableAmount')}</th>
                      <th className="border-r border-line px-3 py-2">{t('bs.tableRight')}</th><th className="w-36 px-3 py-2 text-right">{t('bs.tableAmount')}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-line">
                    {Array.from({ length: rows }, (_, i) => <tr key={i}>{cells(left[i])}{cells(right[i])}</tr>)}
                  </tbody>
                  <tfoot>
                    <tr className="border-t-2 border-ink text-base font-extrabold">
                      <td className="border-r border-line px-3 py-2">{t('yearRep.total')}</td><td className="border-r border-line px-3 py-2 text-right tabular-nums">{formatRupees(sheet.totalLiabilities + sheet.capital)}</td>
                      <td className="border-r border-line px-3 py-2">{t('yearRep.total')}</td><td className="px-3 py-2 text-right tabular-nums">{formatRupees(sheet.totalAssets)}</td>
                    </tr>
                  </tfoot>
                </table>
              )
            })()}
            <Card className="lg:hidden">
              <h2 className="flex justify-between text-lg font-bold"><span>{t('bs.assets')}</span><span className="tabular-nums text-jama">{formatRupees(sheet.totalAssets)}</span></h2>
              <p className="mb-1 text-xs text-muted">{t('bs.assetsHint')}</p>
              {row(t('bs.cash'), sheet.cash, '/cashledger')}
              {row(t('bs.bank'), sheet.bank, '/bankbook')}
              {row(t('bs.stock'), sheet.stock, '/stock-statement', sheet.unvaluedItems > 0 ? t('bs.unvalued', { count: sheet.unvaluedItems }) : t('bs.atCost'))}
              {row(t('bs.customersOwe'), sheet.customersOwe, '/dues', t('bs.people', { count: sheet.customersOwing }))}
              {sheet.supplierAdvances > 0 && row(t('bs.supplierAdvances'), sheet.supplierAdvances, '/suppliers')}
              {sheet.items.filter((i) => isAsset(i.kind)).map(itemRow)}
            </Card>
            <Card className="lg:hidden">
              <h2 className="flex justify-between text-lg font-bold"><span>{t('bs.liabilities')}</span><span className="tabular-nums text-due">{formatRupees(sheet.totalLiabilities)}</span></h2>
              <p className="mb-1 text-xs text-muted">{t('bs.liabilitiesHint')}</p>
              {row(t('bs.suppliersOwed'), sheet.suppliersOwed, '/suppliers', t('bs.suppliers', { count: sheet.suppliersOwedCount }))}
              {sheet.customerAdvances > 0 && row(t('bs.customerAdvances'), sheet.customerAdvances, '/khata')}
              {sheet.gstToPay > 0 && row(t('bs.gstToPay'), sheet.gstToPay, '/gst', t('bs.gstHint'))}
              {sheet.items.filter((i) => !isAsset(i.kind)).map(itemRow)}
            </Card>

            {form ? (
              <Card tone="brand" className="flex flex-col gap-2">
                <h2 className="font-bold">{t('bs.itemTitle')}</h2>
                <Select aria-label={t('bs.itemKind')} value={form.kind} onChange={(e) => setForm({ ...form, kind: e.target.value as Kind })}>
                  {KINDS.map((k) => <option key={k} value={k}>{t(`bs.kind.${k}`)}</option>)}
                </Select>
                <Input aria-label={t('bs.itemName')} placeholder={t('bs.itemName')} value={form.name} maxLength={80} onChange={(e) => setForm({ ...form, name: e.target.value })} />
                <div className="grid grid-cols-2 gap-2">
                  <Input aria-label={t('bs.itemAmount')} placeholder={t('bs.itemAmount')} value={form.amount} inputMode="decimal" onChange={(e) => setForm({ ...form, amount: e.target.value })} />
                  <Input aria-label={t('bs.itemSince')} type="date" value={form.since} max={indiaToday()} onChange={(e) => setForm({ ...form, since: e.target.value })} />
                </div>
                <p className="text-xs text-muted">{t('bs.itemHint')}</p>
                <div className="flex flex-wrap items-center gap-3">
                  <button disabled={form.name.trim().length < 2 || !(Number(form.amount) > 0)} onClick={() => void save()} className={`min-h-11 rounded-xl px-5 ${btn.solid}`}>{t('save')}</button>
                  <button onClick={() => setForm(null)} className={`min-h-11 ${btn.quiet}`}>{t('cancel')}</button>
                  {sheet.items.some((i) => i.id === form.id) && <button onClick={() => void end()} className={`ml-auto min-h-11 text-sm ${btn.link}`}>{t('bs.end')}</button>}
                </div>
                {message && <p className="text-sm text-due">{message}</p>}
              </Card>
            ) : (
              <button onClick={() => setForm({ id: crypto.randomUUID(), kind: 'fixed_asset', name: '', amount: '', since: on })}
                className={`min-h-12 self-start ${btn.add}`}>+ {t('bs.addItem')}</button>
            )}

            <Card tone="brand">
              <p className="flex justify-between text-xl font-bold"><span>{t('bs.capital')}</span><span className="tabular-nums">{formatRupees(sheet.capital)}</span></p>
              <p className="mt-1 text-sm text-muted">{t('bs.capitalHint')}</p>
              {split && (
                <div className="mt-3 border-t border-line pt-2 text-sm">
                  <p className="mb-1 font-semibold">{t('bs.story', { fy: split.financialYear })}</p>
                  {([['opening', split.opening], ['startedWith', split.startedWith], ['itemsNet', split.itemsNet], ['profit', split.profit], ['broughtIn', split.broughtIn], ['takenHome', -split.takenHome]] as const).filter(([k, v]) => (k !== 'startedWith' && k !== 'itemsNet') || v !== 0).map(([k, v]) => (
                    <p key={k} className="flex justify-between py-0.5"><span>{t(`bs.split.${k}`)}</span><span className="tabular-nums">{v < 0 ? '− ' : ''}{formatRupees(Math.abs(v))}</span></p>
                  ))}
                  {Math.abs(split.unexplained) >= 1 && (
                    <p className="flex justify-between py-0.5 text-warn"><span>{t('bs.split.unexplained')}</span><span className="tabular-nums">{split.unexplained < 0 ? '− ' : ''}{formatRupees(Math.abs(split.unexplained))}</span></p>
                  )}
                  <p className="flex justify-between border-t border-line pt-1 font-bold"><span>{t('bs.capital')}</span><span className="tabular-nums">{formatRupees(sheet.capital)}</span></p>
                  {Math.abs(split.unexplained) >= 1 && <p className="mt-1 text-xs text-muted">{t('bs.unexplainedHint')}</p>}
                </div>
              )}
              <p className="mt-3 text-xs text-muted">{t('bs.asOf', { day })} · {t('bs.note')}</p>
            </Card>
          </>
        )}
      </div>
    </ReportShell>
  )
}

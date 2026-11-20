import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { CustomerFinder } from '../components/CustomerFinder'
import { Button, Field, btn } from '../components/ui'
import { api } from '../lib/api'
import { useCustomers } from '../lib/data'
import { formatRupees } from '../lib/format'
import { parseStatement } from '../lib/statementImport'
import { DataTable } from '../components/DataTable'

interface Receipt {
  id: string
  source: string
  amount: number
  left: number
  receivedAt: string
  utr: string | null
  payerName: string | null
  suggestion: { customerId: string; name: string; why: 'amount' | 'name' | 'claim' } | null
  already: { kind: 'entered' | 'sale'; entryId: string | null; customerName: string | null } | null
}

/**
 * Money that arrived and isn't on anyone's khata yet, including money taken back off the wrong customer. Nothing here
 * moves a balance until the shop matches it. The app only suggests a customer when exactly one fits.
 */
export function ReceiptsScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const key = [shopId, 'receipts']
  const tray = useQuery({ queryKey: key, queryFn: () => api<Receipt[]>('/v1/receipts') })
  const [amount, setAmount] = useState('')
  const [utr, setUtr] = useState('')
  const [payer, setPayer] = useState('')
  const [imported, setImported] = useState('')
  const [problems, setProblems] = useState<string[]>([])
  const [message, setMessage] = useState('')
  const [statement, setStatement] = useState('')
  // Desktop: the money waiting to be matched as a table; the one picked opens below to match it.
  const [openId, setOpenId] = useState<string | null>(null)
  const parsed = parseStatement(statement)
  const statementTotal = parsed.rows.reduce((sum, r) => sum + r.amount, 0)

  async function importStatement() {
    await run(async () => {
      const result = await api<{ added: number; known: number; confirmed: number; problems: string[] }>('/v1/receipts/import', 'POST', { rows: parsed.rows })
      setStatement('')
      setImported(t('receipts.imported', { added: result.added, known: result.known, confirmed: result.confirmed }))
      setProblems(result.problems)
    })
  }

  async function refresh() {
    await Promise.all([queryClient.invalidateQueries({ queryKey: key }), queryClient.invalidateQueries({ queryKey: [shopId, 'customers'] })])
  }

  async function run(action: () => Promise<unknown>) {
    setMessage('')
    try {
      await action()
      await refresh()
      return true
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
      return false
    }
  }

  async function note() {
    const done = await run(() => api('/v1/receipts', 'POST', { id: crypto.randomUUID(), amount: Number(amount), utr: utr.trim() || null, payerName: payer.trim() || null }))
    if (done) { setAmount(''); setUtr(''); setPayer('') }
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      <h1 className="text-lg font-bold">{t('receipts.title')}</h1>
      <p className="text-sm text-muted">{t('receipts.hint')}</p>
      <section className="flex flex-col gap-2 rounded-card bg-white p-4">
        <Field label={t('receipts.amount')} value={amount} onChange={(e) => setAmount(e.target.value)} inputMode="decimal" />
        <Field label={t('receipts.utr')} value={utr} onChange={(e) => setUtr(e.target.value)} maxLength={30} />
        <Field label={t('receipts.payer')} value={payer} onChange={(e) => setPayer(e.target.value)} maxLength={120} />
        <Button disabled={!(Number(amount) > 0)} onClick={() => void note()}>{t('receipts.add')}</Button>
      </section>
      {isOwner && (
        <section className="flex flex-col gap-2 rounded-card bg-white p-4">
          <h2 className="font-bold">{t('receipts.statementTitle')}</h2>
          <p className="text-sm text-muted">{t('receipts.statementHint')}</p>
          <label className="flex min-h-11 cursor-pointer items-center justify-center rounded-xl border border-line font-semibold">
            {t('partyImport.file')}
            <input type="file" accept=".csv,.txt,text/csv,text/plain" className="sr-only" onChange={(e) => { const file = e.target.files?.[0]; e.target.value = ''; if (file) void file.text().then(setStatement) }} />
          </label>
          <textarea aria-label={t('receipts.statementTitle')} value={statement} onChange={(e) => setStatement(e.target.value)} rows={3} placeholder={t('receipts.statementPaste')} className="w-full rounded-xl border border-line p-3 text-sm" />
          {statement.trim() && !parsed.found && <p className="text-sm font-semibold text-due">{t('receipts.statementNone')}</p>}
          {parsed.found && parsed.rows.length === 0 && <p className="text-sm font-semibold text-due">{t('receipts.statementEmpty')}</p>}
          {parsed.rows.length > 0 && (
            <>
              <p className="font-bold">{t('receipts.statementSummary', { count: parsed.rows.length, total: formatRupees(statementTotal) })}</p>
              {parsed.skipped > 0 && <p className="text-sm text-muted">{t('receipts.statementSkipped', { count: parsed.skipped })}</p>}
              <ul className="divide-y divide-[#edf2f1]">
                {parsed.rows.slice(0, 5).map((r, i) => (
                  <li key={i} className="flex justify-between gap-2 py-1 text-sm"><span className="min-w-0 truncate">{r.payerName ?? r.narration}</span><span className="shrink-0 font-semibold">{formatRupees(r.amount)}</span></li>
                ))}
              </ul>
              <Button onClick={() => void importStatement()}>{t('receipts.statementNow', { count: parsed.rows.length })}</Button>
            </>
          )}
          {imported && <p className="rounded-xl bg-emerald-50 p-2 text-sm font-semibold text-emerald-800">{imported}</p>}
          {problems.slice(0, 10).map((p) => <p key={p} className="text-sm font-semibold text-due">{p}</p>)}
        </section>
      )}
      {message && <p className="text-sm font-semibold text-due">{message}</p>}
      {tray.data?.length === 0 && <p className="py-6 text-center text-muted">{t('receipts.empty')}</p>}
      {(tray.data?.length ?? 0) > 0 && (
        <div className="hidden lg:block">
          <DataTable label={t('receipts.link')} rows={tray.data!} rowKey={(x) => x.id} selectedKey={openId} onRowClick={(x) => setOpenId(openId === x.id ? null : x.id)} download="money-received"
            columns={[
              { key: 'date', header: t('cashTable.date'), width: 'w-28', sortValue: (x) => x.receivedAt, render: (x) => new Date(x.receivedAt).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' }), csv: (x) => x.receivedAt.slice(0, 10) },
              { key: 'left', header: t('rcTable.left'), align: 'right', width: 'w-32', sortValue: (x) => x.left, render: (x) => <b>{formatRupees(x.left)}</b>, csv: (x) => x.left },
              { key: 'amount', header: t('rcTable.received'), align: 'right', width: 'w-32', sortValue: (x) => x.amount, render: (x) => <span className="text-muted">{formatRupees(x.amount)}</span>, csv: (x) => x.amount },
              { key: 'payer', header: t('rcTable.payer'), sortValue: (x) => x.payerName ?? '', filter: { kind: 'text', value: (x) => `${x.payerName ?? ''} ${x.utr ?? ''}` }, render: (x) => x.payerName ?? '—', csv: (x) => x.payerName ?? '' },
              { key: 'utr', header: 'UTR', width: 'w-40', render: (x) => <span className="font-mono text-xs">{x.utr ?? '—'}</span>, csv: (x) => x.utr ?? '' },
              { key: 'hint', header: t('rcTable.hint'), width: 'w-56',
                render: (x) => (x.suggestion ? <span className="rounded-full bg-brand-soft px-2 py-0.5 text-xs font-semibold text-brand-strong">→ {x.suggestion.name}</span>
                  : x.already ? <span className="rounded-full bg-warn-soft px-2 py-0.5 text-xs font-semibold text-warn">{x.already.kind === 'sale' ? t('rcTable.sale') : t('rcTable.entered', { name: x.already.customerName ?? '' })}</span> : <span className="text-xs text-muted">{t('rcTable.toMatch')}</span>) },
            ]}
            footer={[t('yearRep.total'), formatRupees(tray.data!.reduce((n, x) => n + x.left, 0)), formatRupees(tray.data!.reduce((n, x) => n + x.amount, 0)), '', '', '']} />
          {!openId && <p className="mt-2 px-1 text-xs text-muted">{t('rcTable.pick')}</p>}
        </div>
      )}
      {tray.data?.map((r) => <div key={r.id} className={openId === r.id ? '' : 'lg:hidden'}><ReceiptCard shopId={shopId} receipt={r} isOwner={isOwner} run={run} /></div>)}
    </div>
  )
}

function ReceiptCard({ shopId, receipt: r, isOwner, run }: { shopId: string; receipt: Receipt; isOwner: boolean; run: (action: () => Promise<unknown>) => Promise<boolean> }) {
  const { t } = useTranslation()
  const { customers } = useCustomers(shopId)
  const [customerId, setCustomerId] = useState('')
  const [part, setPart] = useState(String(r.left))
  const chosen = customers.find((c) => c.id === customerId) ?? null
  const matched = r.left < r.amount
  const allocate = (id: string, value: number) => run(() => api(`/v1/receipts/${r.id}/allocate`, 'POST', { id: crypto.randomUUID(), customerId: id, amount: value }))
  const link = (entryId: string) => run(() => api(`/v1/receipts/${r.id}/link`, 'POST', { entryId }))
  const dismiss = (as: 'counterSale' | 'notKhata' | 'notReceived') => run(() => api(`/v1/receipts/${r.id}/dismiss`, 'POST', { as }))

  return (
    <section className="flex flex-col gap-2 rounded-card bg-white p-4">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="text-xl font-bold">{formatRupees(r.left)}</div>
          {matched && <div className="text-sm text-muted">{t('receipts.leftOf', { amount: formatRupees(r.amount) })}</div>}
          <div className="truncate text-sm text-muted">{[r.payerName, r.utr && `UTR ${r.utr}`].filter(Boolean).join(' · ')}</div>
        </div>
        <span className="shrink-0 text-sm text-muted">{new Date(r.receivedAt).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })}</span>
      </div>
      {r.already?.kind === 'entered' && (
        <>
          <p className="rounded-xl bg-warn-soft p-2 text-sm font-semibold text-warn-ink">{t('receipts.alreadyEntered', { name: r.already.customerName })}</p>
          <Button onClick={() => void link(r.already!.entryId!)}>{t('receipts.linkIt')}</Button>
        </>
      )}
      {r.already?.kind === 'sale' && <p className="rounded-xl bg-warn-soft p-2 text-sm font-semibold text-warn-ink">{t('receipts.alreadySale')}</p>}
      {r.suggestion && (
        <Button onClick={() => void allocate(r.suggestion!.customerId, r.left)}>{t(`receipts.suggest_${r.suggestion.why}`, { name: r.suggestion.name })}</Button>
      )}
      <CustomerFinder customers={customers} selected={chosen} onKhata={0} onPick={setCustomerId} inputId={`receipt-customer-${r.id}`} placeholder={t('receipts.findCustomer')} />
      {chosen && (
        <div className="flex items-end gap-2">
          <div className="min-w-0 flex-1"><Field label={t('receipts.part')} value={part} onChange={(e) => setPart(e.target.value)} inputMode="decimal" /></div>
          <Button disabled={!(Number(part) > 0)} onClick={() => void allocate(chosen.id, Number(part))}>{t('receipts.match')}</Button>
        </div>
      )}
      {isOwner && r.source === 'claim' && (
        <button onClick={() => void dismiss('notReceived')} className={`min-h-11 self-start text-sm ${btn.dangerOutline}`}>{t('receipts.notReceived')}</button>
      )}
      {isOwner && r.source !== 'claim' && (
        <div className="flex flex-wrap gap-3">
          <button onClick={() => void dismiss('counterSale')} className="min-h-11 text-sm font-semibold text-brand underline">{t('receipts.counterSale')}</button>
          <button onClick={() => void dismiss('notKhata')} className="min-h-11 text-sm font-semibold text-brand underline">{t('receipts.notKhata')}</button>
        </div>
      )}
    </section>
  )
}

import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'

interface TrialLine { key: string; group: 'assets' | 'liabilities' | 'income' | 'expenses' | 'capital'; debit: number; credit: number }
interface TrialBalance { financialYear: string; on: string; lines: TrialLine[]; totalDebit: number; totalCredit: number }
interface GstMonth { month: string; taxable: number; cgst: number; sgst: number; igst: number; b2bSales: number; creditNoteValue: number; inputGst: number; paid: number }
interface HsnRow { section: string; hsn: string; rate: number | null; uqc: string; quantity: number; taxable: number; cgst: number; sgst: number; igst: number }
interface GstAnnual {
  financialYear: string; composition: boolean; months: GstMonth[]
  taxable: number; b2bSales: number; b2cSales: number; creditNoteValue: number; creditNoteTax: number
  cgst: number; sgst: number; igst: number; inputCgst: number; inputSgst: number; inputIgst: number
  paidCgst: number; paidSgst: number; paidIgst: number; compositionTurnover: number; compositionTax: number
  hsn: HsnRow[]; monthsWithIssues: number
}

/** Financial years to choose: this one and the three before it (a year runs April to March). */
function years(): number[] {
  const now = new Date()
  const fy = now.getMonth() >= 3 ? now.getFullYear() : now.getFullYear() - 1
  return [fy, fy - 1, fy - 2, fy - 3]
}
const label = (fy: number) => `${fy}-${String((fy + 1) % 100).padStart(2, '0')}`
const cell = (v: string | number) => (/[",\n]/.test(String(v)) ? `"${String(v).replace(/"/g, '""')}"` : String(v))

function download(name: string, rows: (string | number)[][]) {
  const a = document.createElement('a')
  a.href = URL.createObjectURL(new Blob(['﻿' + rows.map((r) => r.map(cell).join(',')).join('\n')], { type: 'text/csv' }))
  a.download = name
  a.click()
  setTimeout(() => URL.revokeObjectURL(a.href), 10_000)
}

function YearPicker({ fy, onChange }: { fy: number; onChange: (fy: number) => void }) {
  const { t } = useTranslation()
  return (
    <label className="flex items-center gap-2 text-sm font-semibold">{t('yearRep.year')}
      <select value={fy} onChange={(e) => onChange(Number(e.target.value))} className="h-10 rounded-xl border border-line bg-white px-2">
        {years().map((y) => <option key={y} value={y}>{label(y)}</option>)}
      </select></label>
  )
}

/** The trial balance: every balance and the year's income and expenses, debit against credit, for the CA. */
export function TrialBalanceScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const [fy, setFy] = useState(years()[0])
  const q = useQuery({ queryKey: [shopId, 'trial-balance', fy], queryFn: () => api<TrialBalance>(`/v1/reports/trial-balance?fy=${fy}`) })
  const tb = q.data
  const name = (l: TrialLine) => l.key.startsWith('expense:') ? t('yearRep.expenseOf', { what: t(`expense.cat.${l.key.slice(8).replace(/^./, (c) => c.toLowerCase())}`, { defaultValue: l.key.slice(8) }) }) : t(`yearRep.tb.${l.key}`)
  const groups = ['assets', 'liabilities', 'capital', 'income', 'expenses'] as const
  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div><h1 className="text-lg font-extrabold">{t('yearRep.tbTitle')}</h1><p className="text-sm text-muted">{t('yearRep.tbHint')}</p></div>
        <YearPicker fy={fy} onChange={setFy} />
      </div>
      {q.isError && <p className="text-sm text-due">{q.error.message}</p>}
      {tb && <>
        <p className="text-sm text-muted">{t('yearRep.asOn', { date: new Date(`${tb.on}T00:00:00`).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' }) })}</p>
        <div className="overflow-x-auto rounded-card bg-white shadow-sm">
          <table className="w-full border-collapse text-sm">
            <thead className="bg-brand-soft text-left text-xs uppercase tracking-wide">
              <tr><th className="px-3 py-2">{t('yearRep.account')}</th><th className="px-3 py-2 text-right">{t('yearRep.debit')}</th><th className="px-3 py-2 text-right">{t('yearRep.credit')}</th></tr>
            </thead>
            {groups.map((g) => {
              const rows = tb.lines.filter((l) => l.group === g)
              if (rows.length === 0) return null
              return (
                <tbody key={g} className="divide-y divide-line">
                  <tr className="bg-[#f6faf9]"><td colSpan={3} className="px-3 py-1.5 text-xs font-bold uppercase text-muted">{t(`yearRep.groups.${g}`)}</td></tr>
                  {rows.map((l) => (
                    <tr key={l.key}>
                      <td className="px-3 py-2">{name(l)}</td>
                      <td className="px-3 py-2 text-right">{l.debit ? formatRupees(l.debit) : ''}</td>
                      <td className="px-3 py-2 text-right">{l.credit ? formatRupees(l.credit) : ''}</td>
                    </tr>
                  ))}
                </tbody>
              )
            })}
            <tfoot>
              <tr className="border-t-2 border-ink font-extrabold">
                <td className="px-3 py-2">{t('yearRep.total')}</td>
                <td className="px-3 py-2 text-right">{formatRupees(tb.totalDebit)}</td>
                <td className="px-3 py-2 text-right">{formatRupees(tb.totalCredit)}</td>
              </tr>
            </tfoot>
          </table>
        </div>
        <p className={`text-sm font-semibold ${tb.totalDebit === tb.totalCredit ? 'text-jama' : 'text-due'}`}>{tb.totalDebit === tb.totalCredit ? `✓ ${t('yearRep.agrees')}` : t('yearRep.differs')}</p>
        <button onClick={() => download(`trial-balance-${tb.financialYear}.csv`, [[t('yearRep.account'), t('yearRep.debit'), t('yearRep.credit')], ...tb.lines.map((l) => [name(l), l.debit || '', l.credit || '']), [t('yearRep.total'), tb.totalDebit, tb.totalCredit]])}
          className="min-h-11 self-start text-sm font-semibold text-brand underline">⬇ {t('billList.excel')}</button>
      </>}
    </div>
  )
}

/** GSTR-9 working: the year added up from the months, as tables 4, 6, 9 and 17 of the annual return want it. */
export function GstAnnualScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const [fy, setFy] = useState(years()[0])
  const q = useQuery({ queryKey: [shopId, 'gst-annual', fy], queryFn: () => api<GstAnnual>(`/v1/gst/annual?fy=${fy}`) })
  const g = q.data
  const row = (k: string, v: number, strong = false) => <p key={k} className={`flex justify-between gap-2 py-1 ${strong ? 'font-bold' : ''}`}><span>{k}</span><span>{formatRupees(v)}</span></p>
  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div><h1 className="text-lg font-extrabold">{t('yearRep.gstTitle')}</h1><p className="text-sm text-muted">{t('yearRep.gstHint')}</p></div>
        <YearPicker fy={fy} onChange={setFy} />
      </div>
      {q.isError && <p className="text-sm text-due">{q.error.message}</p>}
      {g && <>
        {g.composition && <p className="rounded-xl bg-warn-soft p-3 text-sm font-semibold">{t('yearRep.composition', { turnover: formatRupees(g.compositionTurnover), tax: formatRupees(g.compositionTax) })}</p>}
        {g.monthsWithIssues > 0 && <p className="rounded-xl bg-due-soft p-3 text-sm font-semibold text-due">{t('yearRep.issues', { count: g.monthsWithIssues })}</p>}
        <div className="grid gap-3 md:grid-cols-3">
          <section className="rounded-card bg-white p-4 text-sm shadow-sm">
            <h2 className="mb-1 font-bold">{t('yearRep.t4')}</h2>
            {row(t('yearRep.b2b'), g.b2bSales)}{row(t('yearRep.b2c'), g.b2cSales)}{row(t('yearRep.creditNotes'), -g.creditNoteValue)}{row(t('yearRep.taxable'), g.taxable, true)}
            {row('CGST', g.cgst)}{row('SGST', g.sgst)}{row('IGST', g.igst)}
          </section>
          <section className="rounded-card bg-white p-4 text-sm shadow-sm">
            <h2 className="mb-1 font-bold">{t('yearRep.t6')}</h2>
            {row('CGST', g.inputCgst)}{row('SGST', g.inputSgst)}{row('IGST', g.inputIgst)}{row(t('yearRep.total'), g.inputCgst + g.inputSgst + g.inputIgst, true)}
          </section>
          <section className="rounded-card bg-white p-4 text-sm shadow-sm">
            <h2 className="mb-1 font-bold">{t('yearRep.t9')}</h2>
            {row('CGST', g.paidCgst)}{row('SGST', g.paidSgst)}{row('IGST', g.paidIgst)}{row(t('yearRep.total'), g.paidCgst + g.paidSgst + g.paidIgst, true)}
          </section>
        </div>

        <h2 className="mt-2 font-bold">{t('yearRep.byMonth')}</h2>
        <div className="overflow-x-auto rounded-card bg-white shadow-sm">
          <table className="w-full border-collapse text-sm">
            <thead className="bg-brand-soft text-left text-xs uppercase"><tr>
              <th className="px-3 py-2">{t('yearRep.month')}</th><th className="px-2 py-2 text-right">{t('yearRep.taxable')}</th><th className="px-2 py-2 text-right">CGST</th><th className="px-2 py-2 text-right">SGST</th><th className="px-2 py-2 text-right">IGST</th>
              <th className="px-2 py-2 text-right">{t('yearRep.b2b')}</th><th className="px-2 py-2 text-right">{t('yearRep.itc')}</th><th className="px-3 py-2 text-right">{t('yearRep.paid')}</th></tr></thead>
            <tbody className="divide-y divide-line">
              {g.months.map((m) => (
                <tr key={m.month}>
                  <td className="whitespace-nowrap px-3 py-2">{new Date(`${m.month}-01T00:00:00`).toLocaleDateString(undefined, { month: 'short', year: 'numeric' })}</td>
                  <td className="px-2 py-2 text-right">{formatRupees(m.taxable)}</td><td className="px-2 py-2 text-right">{formatRupees(m.cgst)}</td><td className="px-2 py-2 text-right">{formatRupees(m.sgst)}</td><td className="px-2 py-2 text-right">{formatRupees(m.igst)}</td>
                  <td className="px-2 py-2 text-right">{formatRupees(m.b2bSales)}</td><td className="px-2 py-2 text-right">{formatRupees(m.inputGst)}</td><td className="px-3 py-2 text-right font-semibold">{formatRupees(m.paid)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <h2 className="mt-2 font-bold">{t('yearRep.t17')}</h2>
        {g.hsn.length === 0 ? <p className="rounded-card bg-white p-4 text-sm text-muted">{t('yearRep.noHsn')}</p> : (
          <div className="overflow-x-auto rounded-card bg-white shadow-sm">
            <table className="w-full border-collapse text-sm">
              <thead className="bg-brand-soft text-left text-xs uppercase"><tr><th className="px-3 py-2">HSN</th><th className="px-2 py-2">UQC</th><th className="px-2 py-2 text-right">{t('yearRep.qty')}</th><th className="px-2 py-2 text-right">GST %</th><th className="px-2 py-2 text-right">{t('yearRep.taxable')}</th><th className="px-3 py-2 text-right">{t('yearRep.tax')}</th></tr></thead>
              <tbody className="divide-y divide-line">
                {g.hsn.map((h) => (
                  <tr key={`${h.section}-${h.hsn}-${h.rate}-${h.uqc}`}>
                    <td className="px-3 py-2">{h.hsn || '—'}</td><td className="px-2 py-2">{h.uqc}</td><td className="px-2 py-2 text-right">{h.quantity}</td><td className="px-2 py-2 text-right">{h.rate ?? '—'}</td>
                    <td className="px-2 py-2 text-right">{formatRupees(h.taxable)}</td><td className="px-3 py-2 text-right">{formatRupees(h.cgst + h.sgst + h.igst)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <button onClick={() => download(`gstr9-working-${g.financialYear}.csv`, [
          [t('yearRep.month'), t('yearRep.taxable'), 'CGST', 'SGST', 'IGST', t('yearRep.b2b'), t('yearRep.creditNotes'), t('yearRep.itc'), t('yearRep.paid')],
          ...g.months.map((m) => [m.month, m.taxable, m.cgst, m.sgst, m.igst, m.b2bSales, m.creditNoteValue, m.inputGst, m.paid]),
          [t('yearRep.total'), g.taxable, g.cgst, g.sgst, g.igst, g.b2bSales, g.creditNoteValue, g.inputCgst + g.inputSgst + g.inputIgst, g.paidCgst + g.paidSgst + g.paidIgst],
          [], ['HSN', 'UQC', t('yearRep.qty'), 'GST %', t('yearRep.taxable'), 'CGST', 'SGST', 'IGST'],
          ...g.hsn.map((h) => [h.hsn, h.uqc, h.quantity, h.rate ?? '', h.taxable, h.cgst, h.sgst, h.igst]),
        ])} className="min-h-11 self-start text-sm font-semibold text-brand underline">⬇ {t('billList.excel')}</button>
        <p className="text-xs text-muted">{t('yearRep.gstNote')}</p>
      </>}
    </div>
  )
}

import { shownLocale } from '../i18n'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { api, ApiError, request } from '../lib/api'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'
import { downloadCsv } from '../lib/reports'
import { daysUntil, ReportShell, useReportView } from '../components/ReportShell'
import { BooksCard } from '../components/BooksCard'

interface GstMonth {
  month: string; composition: boolean; bills: number; cancelled: number; firstBillNo: string | null; lastBillNo: string | null
  sales: number; taxable: number; cgst: number; sgst: number; igst: number; b2bSales: number; checks: { code: string; count: number }[]; canDownload: boolean
  creditNotes?: number; creditNoteValue?: number; creditNoteTax?: number
  receipts?: number; receiptValue?: number
  purchaseBills?: number; inputTaxable?: number; inputGst?: number
  payable?: { outputCgst: number; outputSgst: number; outputIgst: number; inputCgst: number; inputSgst: number; inputIgst: number; payCgst: number; paySgst: number; payIgst: number
    carryCgst: number; carrySgst: number; carryIgst: number; compositionTurnover: number | null; compositionTax: number | null }
}

const FIX: Record<string, string> = { noRate: '/items', noHsn: '/items', badBuyerGstin: '/khata', noGstin: '/plan' }

/** A month's GST for the CA: what to fix first, the totals, and one file to send. Download is for paid plans. */
export function GstExportScreen() {
  const { t } = useTranslation()
  const now = new Date()
  const [month, setMonth] = useState(`${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`)
  const [error, setError] = useState('')
  const data = useQuery({ queryKey: ['gst', month], queryFn: () => api<GstMonth>(`/v1/gst/month?month=${month}`), retry: false }).data
  // Return deadlines and tax to pay mean something only to a shop registered for GST.
  const registered = !!useQuery({ queryKey: ['gst-shop'], queryFn: () => api<{ gstin: string | null }>('/v1/shop'), staleTime: 300_000 }).data?.gstin

  async function send() {
    setError('')
    try {
      const response = await request(`/v1/gst/export.zip?month=${month}`)
      const file = new File([await response.blob()], `gst-tally-${month}.zip`, { type: 'application/zip' })
      // On a phone, straight to WhatsApp or email; elsewhere, a download.
      if (navigator.canShare?.({ files: [file] })) {
        await navigator.share({ files: [file], title: t('gstExport.shareTitle', { month }) })
      } else {
        const link = document.createElement('a')
        link.href = URL.createObjectURL(file)
        link.download = file.name
        link.click()
        URL.revokeObjectURL(link.href)
      }
    } catch (e) {
      if (e instanceof DOMException && e.name === 'AbortError') return
      setError(e instanceof ApiError && e.status === 402 ? t('gstExport.paid') : e instanceof Error ? e.message : t('error'))
    }
  }

  const [view, setView] = useReportView()
  const output = data ? data.cgst + data.sgst + data.igst : 0
  const credit = data?.inputGst ?? 0
  const pay = Math.max(0, Math.round((output - credit) * 100) / 100)
  const carry = Math.max(0, Math.round((credit - output) * 100) / 100)
  // Returns for a month are due the next month: GSTR-1 on the 11th, GSTR-3B (and the payment) on the 20th.
  const [y, m] = month.split('-').map(Number)
  const next = `${m === 12 ? y + 1 : y}-${String(m === 12 ? 1 : m + 1).padStart(2, '0')}`
  const dues = [{ key: 'gstr1', date: `${next}-11` }, { key: 'gstr3b', date: `${next}-20` }]
  const inputHalf = Math.round((credit / 2) * 100) / 100
  const ticks = data && registered && !data.composition ? [
    { label: t('rv.outputGst'), value: formatRupees(output) },
    { label: t('rv.credit'), value: formatRupees(credit) },
    { label: t('rv.toPay'), value: formatRupees(pay), note: t('rv.dueOn', { date: new Date(`${next}-20T00:00:00Z`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', timeZone: 'UTC' }) }) },
    { label: t('gstExport.bills'), value: String(data.bills) },
  ] : []
  const row = (label: string, value: string) => <p key={label} className="flex justify-between"><span>{label}</span><span className="font-semibold">{value}</span></p>
  return (
    <ReportShell page="gst" title={t('rv.gstTitle')} isOwner view={view} onView={setView} ticks={ticks}>
      <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('gstExport.month')}</span>
        <input type="month" value={month} onChange={(e) => setMonth(e.target.value)} className="h-12 rounded-xl border border-line bg-white px-3 text-lg outline-none transition focus:border-teal-600 focus:ring-4 focus:ring-teal-600/10" /></label>
      {data && registered && !data.composition && view === 'chart' && (
        <div className="flex flex-col gap-3 lg:grid lg:grid-cols-2 lg:items-start">
          <section className="flex flex-col gap-3 rounded-[18px] bg-white p-4 shadow-sm print:shadow-none">
            <span className="text-[13px] text-muted">{t('rv.toPay')}</span>
            <b className="text-[36px] leading-none text-teal-900">{formatRupees(pay)}</b>
            {output > 0 && (
              <div role="img" aria-label={t('rv.meterLabel', { credit: formatRupees(Math.min(credit, output)), pay: formatRupees(pay) })} className="flex h-7 overflow-hidden rounded-lg text-xs font-semibold">
                <span className="flex items-center bg-blue-600 pl-2 text-white" style={{ width: `${(Math.min(credit, output) * 100) / output}%` }}>{credit > 0 ? t('rv.creditShort', { amount: formatRupees(Math.min(credit, output)) }) : ''}</span>
                <span className="flex items-center bg-amber-400 pl-2 text-[#1c1917]" style={{ width: `${(pay * 100) / output}%` }}>{pay > 0 ? t('rv.payShort', { amount: formatRupees(pay) }) : ''}</span>
              </div>
            )}
            <p className="text-[13px] text-muted">{t('rv.meterHint', { output: formatRupees(output), credit: formatRupees(credit), count: data.purchaseBills ?? 0 })}</p>
            {carry > 0 && <p className="rounded-xl bg-teal-50 p-2 text-sm text-teal-900">{t('rv.carry', { amount: formatRupees(carry) })}</p>}
          </section>
          <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm print:shadow-none">
            {dues.map((d) => {
              const left = daysUntil(d.date)
              return (
                <div key={d.key} className={`flex items-center gap-3 rounded-xl p-3 ${left <= 3 ? 'bg-orange-50' : 'bg-teal-50'}`}>
                  <b className={`min-w-14 text-center text-[26px] ${left <= 3 ? 'text-orange-800' : 'text-teal-900'}`}>{left < 0 ? '!' : left}</b>
                  <span className="text-sm">{t(`rv.${d.key}`)}<br /><span className="text-muted">{left < 0 ? t('rv.passed', { date: d.date }) : t('rv.daysLeft', { count: left, date: new Date(`${d.date}T00:00:00Z`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', timeZone: 'UTC' }) })}</span></span>
                </div>
              )
            })}
          </section>
        </div>
      )}
      {data && !data.composition && view === 'ledger' && (
        <section className="overflow-x-auto rounded-xl border border-line bg-white">
          <table className="w-full min-w-[640px] border-collapse text-sm">
            <caption className="p-3 text-left font-bold">{t('rv.table3b')}</caption>
            <thead><tr className="bg-[#f3f6f5] text-right"><th className="border-b border-line px-3 py-2 text-left">{t('rv.head')}</th><th className="border-b border-line px-3 py-2">{t('gstExport.taxable')}</th><th className="border-b border-line px-3 py-2">CGST</th><th className="border-b border-line px-3 py-2">SGST</th><th className="border-b border-line px-3 py-2">IGST</th><th className="border-b border-line px-3 py-2">{t('rv.totalTax')}</th></tr></thead>
            <tbody className="text-right tabular-nums">
              <tr><td className="px-3 py-2 text-left">{t('rv.outward')}</td><td className="px-3 py-2">{formatRupees(data.taxable)}</td><td className="px-3 py-2">{formatRupees(data.cgst)}</td><td className="px-3 py-2">{formatRupees(data.sgst)}</td><td className="px-3 py-2">{formatRupees(data.igst)}</td><td className="px-3 py-2">{formatRupees(output)}</td></tr>
              <tr className="bg-[#fafbfb]"><td className="px-3 py-2 text-left">{t('rv.itc')}</td><td className="px-3 py-2">{formatRupees(data.inputTaxable ?? 0)}</td><td className="px-3 py-2">{formatRupees(inputHalf)}</td><td className="px-3 py-2">{formatRupees(credit - inputHalf)}</td><td className="px-3 py-2">{formatRupees(0)}</td><td className="px-3 py-2">{formatRupees(credit)}</td></tr>
              <tr className="border-t-2 border-ink font-bold"><td className="px-3 py-2 text-left">{t('rv.payable')}</td><td className="px-3 py-2"></td><td className="px-3 py-2">{formatRupees(Math.max(0, data.cgst - inputHalf))}</td><td className="px-3 py-2">{formatRupees(Math.max(0, data.sgst - (credit - inputHalf)))}</td><td className="px-3 py-2">{formatRupees(data.igst)}</td><td className="px-3 py-2 text-teal-900">{formatRupees(pay)}</td></tr>
            </tbody>
          </table>
          <p className="px-3 pb-3 text-xs text-muted">{t('rv.itcNote')}</p>
        </section>
      )}
      {data && <>
        {data.checks.length > 0 && (
          <section className="flex flex-col gap-2 rounded-[18px] bg-amber-50 p-4 text-amber-900">
            <h2 className="font-bold">{t('gstExport.fixFirst')}</h2>
            {data.checks.map((c) => (
              <div key={c.code} className="flex items-center justify-between gap-2 text-sm">
                <span>{t(`gstExport.check.${c.code}`, { count: c.count })}</span>
                {FIX[c.code] && <button onClick={() => navigate(FIX[c.code])} className="min-h-11 shrink-0 font-semibold underline">{t('gstExport.fix')}</button>}
              </div>
            ))}
          </section>
        )}
        <section className="flex flex-col gap-1 rounded-[18px] bg-white p-4 shadow-sm print:shadow-none">
          {data.composition && <p className="mb-2 text-sm font-semibold">{t('gstExport.composition')}</p>}
          {row(t('gstExport.bills'), `${data.bills}${data.cancelled ? ` · ${t('gstExport.cancelled', { count: data.cancelled })}` : ''}`)}
          {data.firstBillNo && row(t('gstExport.series'), `${data.firstBillNo} – ${data.lastBillNo}`)}
          {row(t('gstExport.sales'), formatRupees(data.sales))}
          {row(t('gstExport.taxable'), formatRupees(data.taxable))}
          {data.igst > 0 && row('IGST', formatRupees(data.igst))}
          {row('CGST', formatRupees(data.cgst))}
          {row('SGST', formatRupees(data.sgst))}
          {data.b2bSales > 0 && row(t('gstExport.b2b'), formatRupees(data.b2bSales))}
          {!!data.receipts && row(t('gstExport.receipts', { count: data.receipts }), formatRupees(data.receiptValue ?? 0))}
          {!!data.creditNotes && row(t('gstExport.creditNotes', { count: data.creditNotes }), `−${formatRupees(data.creditNoteValue ?? 0)} (${t('gstExport.tax')} ${formatRupees(data.creditNoteTax ?? 0)})`)}
        </section>
        {data.payable && registered && (
          <section className="flex flex-col gap-1 rounded-[18px] bg-white p-4 shadow-sm print:shadow-none">
            <h2 className="font-bold">{t(data.composition ? 'gstExport.cmp08' : 'gstExport.gstr3b')}</h2>
            {data.composition ? <>
              {row(t('gstExport.turnover'), formatRupees(data.payable.compositionTurnover ?? 0))}
              {row(t('gstExport.compositionTax'), formatRupees(data.payable.compositionTax ?? 0))}
            </> : (
              <table className="w-full text-sm">
                <thead><tr className="text-left text-xs text-muted"><th /><th className="text-right">CGST</th><th className="text-right">SGST</th><th className="text-right">IGST</th></tr></thead>
                <tbody>
                  {([['taxOnSales', 'output'], ['inputClaimable', 'input'], ['toPay', 'pay'], ['carry', 'carry']] as const).map(([label, key]) => (
                    <tr key={key} className={`border-t border-line ${key === 'pay' ? 'font-bold' : ''}`}>
                      <td className="py-1">{t(`gstExport.${label}`)}</td>
                      {(['Cgst', 'Sgst', 'Igst'] as const).map((h) => <td key={h} className="text-right tabular-nums">{formatRupees(data.payable![`${key}${h}`])}</td>)}
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            <p className="text-xs text-muted">{t('gstExport.payableHint')}</p>
          </section>
        )}
        {data.canDownload
          ? <>
              <Button tone="plain" className="lg:self-end lg:px-10" onClick={() => void downloadCsv(`/v1/gst/sales-register.csv?month=${month}`, `sales-register-${month}.csv`)}>{t('gstExport.salesRegister')}</Button>
              <Button className="lg:self-end lg:px-10" disabled={data.bills + data.cancelled === 0} onClick={() => void send()}>{t('gstExport.send')}</Button>
              <Button tone="plain" className="lg:self-end lg:px-10" onClick={() => void downloadCsv(`/v1/suppliers/register?month=${month}`, `purchase-register-${month}.csv`)}>{t('supplier2.register')}</Button>
            </>
          : <Button tone="plain" className="lg:self-end lg:px-10" onClick={() => navigate('/plan')}>{t('gstExport.upgrade')}</Button>}
        <p className="text-xs text-muted">{t('gstExport.hint')}</p>
        <BooksCard month={month} />
      </>}
      {error && <p className="text-sm text-due">{error}</p>}
    </ReportShell>
  )
}

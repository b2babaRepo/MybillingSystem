import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { api, ApiError, request } from '../lib/api'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'

interface GstMonth {
  month: string; composition: boolean; bills: number; cancelled: number; firstBillNo: string | null; lastBillNo: string | null
  sales: number; taxable: number; cgst: number; sgst: number; igst: number; b2bSales: number; checks: { code: string; count: number }[]; canDownload: boolean
  creditNotes?: number; creditNoteValue?: number; creditNoteTax?: number
}

const FIX: Record<string, string> = { noRate: '/items', noHsn: '/items', badBuyerGstin: '/khata', noGstin: '/plan' }

/** A month's GST for the CA: what to fix first, the totals, and one file to send. Download is for paid plans. */
export function GstExportScreen() {
  const { t } = useTranslation()
  const now = new Date()
  const [month, setMonth] = useState(`${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`)
  const [error, setError] = useState('')
  const data = useQuery({ queryKey: ['gst', month], queryFn: () => api<GstMonth>(`/v1/gst/month?month=${month}`), retry: false }).data

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

  const row = (label: string, value: string) => <p key={label} className="flex justify-between"><span>{label}</span><span className="font-semibold">{value}</span></p>
  return (
    <div className="flex flex-col gap-3 p-4">
      <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('gstExport.month')}</span>
        <input type="month" value={month} onChange={(e) => setMonth(e.target.value)} className="h-12 rounded-xl border border-line bg-white px-3 text-lg" /></label>
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
        <section className="flex flex-col gap-1 rounded-[18px] bg-white p-4">
          {data.composition && <p className="mb-2 text-sm font-semibold">{t('gstExport.composition')}</p>}
          {row(t('gstExport.bills'), `${data.bills}${data.cancelled ? ` · ${t('gstExport.cancelled', { count: data.cancelled })}` : ''}`)}
          {data.firstBillNo && row(t('gstExport.series'), `${data.firstBillNo} – ${data.lastBillNo}`)}
          {row(t('gstExport.sales'), formatRupees(data.sales))}
          {row(t('gstExport.taxable'), formatRupees(data.taxable))}
          {data.igst > 0 && row('IGST', formatRupees(data.igst))}
          {row('CGST', formatRupees(data.cgst))}
          {row('SGST', formatRupees(data.sgst))}
          {data.b2bSales > 0 && row(t('gstExport.b2b'), formatRupees(data.b2bSales))}
          {!!data.creditNotes && row(t('gstExport.creditNotes', { count: data.creditNotes }), `−${formatRupees(data.creditNoteValue ?? 0)} (${t('gstExport.tax')} ${formatRupees(data.creditNoteTax ?? 0)})`)}
        </section>
        {data.canDownload
          ? <Button disabled={data.bills + data.cancelled === 0} onClick={() => void send()}>{t('gstExport.send')}</Button>
          : <Button tone="plain" onClick={() => navigate('/plan')}>{t('gstExport.upgrade')}</Button>}
        <p className="text-xs text-muted">{t('gstExport.hint')}</p>
      </>}
      {error && <p className="text-sm text-due">{error}</p>}
    </div>
  )
}

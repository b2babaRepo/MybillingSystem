import { useQuery } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../lib/api'
import { formatRupees, whatsAppLink } from '../lib/format'
import type { ShopSettings } from '../lib/reports'
import { gstSummary } from '../lib/gst'
import type { BillView, Statement } from '../lib/types'


/**
 * Print-ready pages: the month-end khata statement and the cash memo. White, black text, no app chrome, so the
 * phone's "Print → Save as PDF" makes a clean PDF with Hindi rendered by the browser; a WhatsApp button sends a
 * short summary for customers without a printer.
 */
export function PrintScreen({ shopName, kind, id, month }: { shopName: string; kind: 'statement' | 'bill'; id: string; month?: string }) {
  return kind === 'statement' ? <StatementPrint shopName={shopName} customerId={id} month={month!} /> : <BillPrint shopName={shopName} billId={id} />
}

function Page({ title, subtitle, shareText, phone, children }: { title: string; subtitle: string; shareText: string; phone: string | null; children: ReactNode }) {
  const { t } = useTranslation()
  return (
    <div className="mx-auto min-h-dvh max-w-2xl bg-white p-6 text-black print:p-0">
      <div className="mb-4 flex flex-wrap gap-2 print:hidden">
        <button onClick={() => history.back()} className="h-11 rounded-xl border border-line px-4 font-semibold" aria-label={t('a11y.back')}>←</button>
        <button onClick={() => window.print()} className="h-11 rounded-xl bg-teal-700 px-4 font-semibold text-white">{t('print.printPdf')}</button>
        <a href={whatsAppLink(phone, shareText)} target="_blank" rel="noreferrer" className="flex h-11 items-center rounded-xl bg-[#15803d] px-4 font-semibold text-white">{t('print.share')}</a>
      </div>
      <header className="mb-4 border-b-2 border-black pb-3">
        <h1 className="text-2xl font-extrabold">{title}</h1>
        <p className="text-base">{subtitle}</p>
      </header>
      {children}
    </div>
  )
}

function useServer<T>(key: unknown[], path: string) {
  return useQuery({ queryKey: key, queryFn: () => api<T>(path), retry: false })
}

function Problem({ error }: { error: Error }) {
  const { t } = useTranslation()
  return <p className="p-6 text-due">{error instanceof ApiError && error.status === 404 ? t('print.notSynced') : error.message}</p>
}

function StatementPrint({ shopName, customerId, month }: { shopName: string; customerId: string; month: string }) {
  const { t } = useTranslation()
  const query = useServer<Statement>(['print', 'statement', customerId, month], `/v1/customers/${customerId}/statement?month=${month}`)
  if (query.isError) return <Problem error={query.error} />
  const s = query.data
  if (!s) return <p className="p-6">…</p>
  const monthLabel = new Date(`${month}-01T00:00:00Z`).toLocaleDateString('en-IN', { month: 'long', year: 'numeric', timeZone: 'UTC' })
  const share = t('print.statementShare', { shop: shopName, month: monthLabel, opening: s.openingBalance, closing: s.closingBalance })

  return (
    <Page title={shopName} subtitle={`${t('print.monthStatement')} · ${monthLabel} · ${s.customer.name}${s.customer.phone ? ` · ${s.customer.phone}` : ''}`} shareText={share} phone={s.customer.phone}>
      <table className="w-full border-collapse text-[15px]">
        <thead>
          <tr className="border-b border-black text-left">
            <th className="py-1.5">{t('print.date')}</th><th>{t('print.details')}</th><th className="text-right">{t('print.amount')}</th><th className="text-right">{t('print.balance')}</th>
          </tr>
        </thead>
        <tbody>
          <tr className="border-b border-gray-300">
            <td className="py-1.5" colSpan={3}>{t('print.openingBalance')}</td><td className="text-right font-semibold">{formatRupees(s.openingBalance)}</td>
          </tr>
          {s.entries.map((e) => (
            <tr key={e.id} className={`border-b border-gray-300 ${e.isReversed ? 'line-through' : ''}`}>
              <td className="py-1.5 pr-2 whitespace-nowrap">{new Date(e.occurredAt).toLocaleDateString('en-IN', { day: 'numeric', month: 'short' })}</td>
              <td className="pr-2">
                {e.kind === 'reversal' ? t('reversal') : e.amount > 0 ? t('creditShort') : t('paymentShort')}
                {e.paymentMode && ` (${t(e.paymentMode)})`}{e.note && ` · ${e.note}`}
              </td>
              <td className="text-right whitespace-nowrap">{e.amount > 0 ? '+' : '−'}{formatRupees(Math.abs(e.amount))}</td>
              <td className="text-right whitespace-nowrap">{formatRupees(e.runningBalance)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="mt-4 flex justify-between border-t-2 border-black pt-2 text-xl font-extrabold">
        <span>{t('print.closingBalance')}</span><span>{formatRupees(s.closingBalance)}</span>
      </p>
    </Page>
  )
}

function BillPrint({ shopName, billId }: { shopName: string; billId: string }) {
  const { t } = useTranslation()
  const query = useServer<BillView>(['print', 'bill', billId], `/v1/bills/${billId}`)
  const gstin = useServer<ShopSettings>(['print', 'shop'], '/v1/shop').data?.gstin
  if (query.isError) return <Problem error={query.error} />
  const view = query.data
  if (!view) return <p className="p-6">…</p>
  const { bill, customer } = view
  const when = new Date(bill.occurredAt).toLocaleString('en-IN', { day: 'numeric', month: 'short', year: 'numeric', hour: 'numeric', minute: '2-digit' })
  const share = t('print.billShare', { shop: shopName, total: bill.total, paid: bill.paidAmount, khata: bill.onKhata })
  const gst = gstSummary(bill.lines)

  return (
    <Page title={shopName} subtitle={`${gstin && gst.length ? t('print.taxInvoice') : t('print.bill')}${bill.billNo ? ` ${t('print.billNo', { no: bill.billNo })}` : ''} · ${when}${customer ? ` · ${customer.name}` : ''}`} shareText={share} phone={customer?.phone ?? null}>
      {(gstin || bill.site || bill.vehicleNumber) && (
        <p className="mb-3 text-sm">
          {[gstin && `GSTIN ${gstin}`, bill.site && `${t('trade.site')}: ${bill.site}`, bill.vehicleNumber && `${t('trade.vehicle')}: ${bill.vehicleNumber}`].filter(Boolean).join(' · ')}
        </p>
      )}
      <table className="w-full border-collapse text-[15px]">
        <thead>
          <tr className="border-b border-black text-left">
            <th className="py-1.5">{t('print.item')}</th><th className="text-right">{t('print.qty')}</th><th className="text-right">{t('print.rate')}</th><th className="text-right">{t('print.amount')}</th>
          </tr>
        </thead>
        <tbody>
          {bill.lines.map((l, i) => (
            <tr key={i} className="border-b border-gray-300">
              <td className="py-1.5 pr-2">{l.name}{l.hsnCode && <span className="block text-xs text-gray-600">HSN {l.hsnCode}{l.gstRate ? ` · GST ${l.gstRate}%` : ''}</span>}</td>
              <td className="text-right whitespace-nowrap">{l.quantity} {t(`units.${l.unit}`)}</td>
              <td className="text-right whitespace-nowrap">{formatRupees(l.unitPrice)}</td>
              <td className="text-right whitespace-nowrap">{formatRupees(l.amount)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <div className="mt-4 flex flex-col gap-1 border-t-2 border-black pt-2 text-lg">
        <p className="flex justify-between text-xl font-extrabold"><span>{t('print.total')}</span><span>{formatRupees(bill.total)}</span></p>
        <p className="flex justify-between"><span>{t('print.paid')}{bill.paymentMode && ` (${t(bill.paymentMode)})`}</span><span>{formatRupees(bill.paidAmount)}</span></p>
        {bill.onKhata > 0 && <p className="flex justify-between font-semibold"><span>{t('print.onKhata')}</span><span>{formatRupees(bill.onKhata)}</span></p>}
      </div>
      {gst.length > 0 && (
        <table className="mt-4 w-full border-collapse text-sm">
          <caption className="mb-1 text-left font-bold">{t('print.gstTitle')}</caption>
          <thead>
            <tr className="border-b border-black text-left">
              <th className="py-1">GST</th><th className="text-right">{t('print.taxable')}</th><th className="text-right">CGST</th><th className="text-right">SGST</th>
            </tr>
          </thead>
          <tbody>
            {gst.map((g) => (
              <tr key={g.rate} className="border-b border-gray-300">
                <td className="py-1">{g.rate}%</td>
                <td className="text-right">{formatRupees(g.taxable)}</td>
                <td className="text-right">{formatRupees(g.cgst)}</td>
                <td className="text-right">{formatRupees(g.sgst)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {gst.length > 0 && <p className="mt-1 text-xs text-gray-600">{t('print.gstIncluded')}</p>}
      <p className="mt-6 text-center">{t('print.thanks')}</p>
    </Page>
  )
}

import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { formatRupees } from '../lib/format'

interface InvoiceView {
  invoiceNo: string
  paidAt: string
  sellerName: string
  sellerGstin: string | null
  gstRegistered: boolean
  shopName: string
  shopCode: string
  shopGstin: string | null
  shopAddress: string | null
  ownerName: string | null
  ownerPhone: string | null
  planName: string
  term: 'monthly' | 'year1' | 'year3' | 'year5'
  periodStart: string
  periodEnd: string
  amount: number
  taxable: number
  gst: number
  method: 'upi' | 'cash' | 'gateway' | 'bank'
  reference: string | null
  offerCode: string | null
  refundedAt: string | null
}

/** Where a plan bill opens: the link sent on WhatsApp. Works without signing in; the token in the link is the key. */
export function planBillLink(paymentId: string, token: string) {
  return `${location.origin}/#/bill/plan/${paymentId}?t=${token}`
}

/** A Grocery Khata plan bill (/bill/plan/{id}?t=…), printable on A4 or a phone screen. */
export function PlanBillPage({ path }: { path: string }) {
  const { t } = useTranslation()
  const match = path.match(/^\/bill\/plan\/([0-9a-f-]+)\?t=([0-9a-f]+)$/)
  const bill = useQuery({
    queryKey: ['plan-bill', path],
    queryFn: async () => {
      const response = await fetch(`/api/v1/bills/plan/${match![1]}?t=${match![2]}`)
      if (!response.ok) throw new Error(String(response.status))
      return (await response.json()) as InvoiceView
    },
    enabled: !!match,
    retry: false,
  })
  if (!match || bill.isError) return <p className="p-6 text-center">{t('planBill.notFound')}</p>
  const b = bill.data
  if (!b) return <p className="p-6 text-center">…</p>
  const date = (iso: string) => new Date(iso).toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' })

  return (
    <div className="mx-auto max-w-xl bg-white p-6 text-[15px] text-black print:p-0">
      <header className="flex items-start justify-between gap-4 border-b-2 border-black pb-3">
        <div>
          <h1 className="text-2xl font-extrabold">{b.sellerName}</h1>
          {b.sellerGstin && <p>GSTIN {b.sellerGstin}</p>}
        </div>
        <div className="text-right">
          <p className="font-bold">{b.gstRegistered ? t('planBill.taxInvoice') : t('planBill.bill')}</p>
          <p className="font-mono">{b.invoiceNo}</p>
          <p>{date(b.paidAt)}</p>
        </div>
      </header>

      {b.refundedAt && <p className="mt-3 rounded bg-red-100 p-2 text-center font-bold text-red-800">{t('planBill.refunded', { date: date(b.refundedAt) })}</p>}

      <section className="mt-4">
        <p className="text-xs uppercase text-gray-600">{t('planBill.billedTo')}</p>
        <p className="text-lg font-bold">{b.shopName} <span className="font-mono text-sm">{b.shopCode}</span></p>
        {b.ownerName && <p>{b.ownerName}{b.ownerPhone ? ` · ${b.ownerPhone}` : ''}</p>}
        {b.shopAddress && <p>{b.shopAddress}</p>}
        {b.shopGstin && <p>GSTIN {b.shopGstin}</p>}
      </section>

      <table className="mt-4 w-full border-collapse">
        <thead><tr className="border-b border-black text-left"><th className="py-1.5">{t('planBill.item')}</th><th className="text-right">{t('planBill.amount')}</th></tr></thead>
        <tbody>
          <tr className="border-b border-gray-300">
            <td className="py-2">
              {t('planBill.line', { plan: b.planName, term: t(`plan2.term.${b.term}`) })}
              <span className="block text-xs text-gray-600">{date(b.periodStart)} → {date(b.periodEnd)}{b.offerCode ? ` · ${t('planBill.offer', { code: b.offerCode })}` : ''}</span>
            </td>
            <td className="text-right">{formatRupees(b.gstRegistered ? b.taxable : b.amount)}</td>
          </tr>
          {b.gstRegistered && <tr><td className="py-1 text-right">{t('planBill.gst')}</td><td className="text-right">{formatRupees(b.gst)}</td></tr>}
        </tbody>
      </table>
      <p className="mt-2 flex justify-between border-t-2 border-black pt-2 text-xl font-extrabold"><span>{t('planBill.total')}</span><span>{formatRupees(b.amount)}</span></p>
      <p className="mt-1">{t('planBill.paidBy', { method: t(`planBill.methods.${b.method}`), ref: b.reference ? ` · ${b.reference}` : '' })}</p>
      <p className="mt-6 text-center text-sm text-gray-600">{t('planBill.thanks')}</p>
      <button onClick={() => window.print()} className="mt-6 w-full rounded-xl bg-teal-700 py-3 font-bold text-white print:hidden">{t('planBill.print')}</button>
    </div>
  )
}

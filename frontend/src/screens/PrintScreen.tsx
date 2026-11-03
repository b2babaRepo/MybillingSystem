import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../lib/api'
import { formatRupees, whatsAppLink } from '../lib/format'
import { gstSummary, gstUnsetAmount, storedSummary } from '../lib/gst'
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

/** Goods taken back on this bill: earlier credit notes, and a form to make one. Owner only (the server checks). */
function CreditNotesPanel({ billId, lines }: { billId: string; lines: BillView['bill']['lines'] }) {
  const { t } = useTranslation()
  const notes = useQuery({ queryKey: ['credit-notes', billId], queryFn: () => api<{ id: string; noteNo: string; reason: string; total: number; occurredAt: string; lines: { billLineNo: number; quantity: number }[] }[]>(`/v1/bills/${billId}/credit-notes`), retry: false })
  const [open, setOpen] = useState(false)
  const [qty, setQty] = useState<Record<number, string>>({})
  const [reason, setReason] = useState('')
  const [restock, setRestock] = useState(true)
  const [message, setMessage] = useState('')
  if (notes.isError) return null
  const back = (lineNo: number) => (notes.data ?? []).flatMap((n) => n.lines).filter((l) => l.billLineNo === lineNo).reduce((s, l) => s + l.quantity, 0)

  async function save() {
    setMessage('')
    try {
      await api(`/v1/bills/${billId}/credit-notes`, 'POST', {
        id: crypto.randomUUID(), reason: reason.trim(), restock,
        lines: Object.entries(qty).filter(([, q]) => Number(q) > 0).map(([lineNo, q]) => ({ lineNo: Number(lineNo), quantity: Number(q) })),
      })
      setQty({}); setReason(''); setOpen(false)
      await notes.refetch()
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  return (
    <section className="mt-6 flex flex-col gap-2 print:hidden">
      {notes.data?.map((n) => (
        <p key={n.id} className="flex justify-between text-sm"><span>{n.noteNo} · {new Date(n.occurredAt).toLocaleDateString('en-IN')} · {n.reason}</span><span>−{formatRupees(n.total)}</span></p>
      ))}
      {!open ? (
        <button onClick={() => setOpen(true)} className="h-11 rounded-xl border border-line font-semibold">{t('creditNote.start')}</button>
      ) : (
        <div className="flex flex-col gap-2 rounded-xl border border-line p-3">
          {lines.map((l, i) => {
            const left = l.quantity - back(i + 1)
            return left > 0 ? (
              <label key={i} className="flex items-center justify-between gap-2 text-sm"><span>{l.name} ({t('creditNote.upTo', { qty: left })})</span>
                <input inputMode="decimal" value={qty[i + 1] ?? ''} onChange={(e) => setQty({ ...qty, [i + 1]: e.target.value })} className="h-11 w-24 rounded-lg border border-line px-2" /></label>
            ) : null
          })}
          <input aria-label={t('creditNote.reason')} placeholder={t('creditNote.reason')} value={reason} onChange={(e) => setReason(e.target.value)} maxLength={200} className="h-11 rounded-lg border border-line px-3" />
          <label className="flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={restock} onChange={(e) => setRestock(e.target.checked)} className="h-5 w-5" />{t('creditNote.restock')}</label>
          <button disabled={reason.trim().length < 3 || !Object.values(qty).some((q) => Number(q) > 0)} onClick={() => void save()} className="h-11 rounded-xl bg-teal-700 font-semibold text-white disabled:opacity-40">{t('creditNote.save')}</button>
          {message && <p className="text-sm text-due">{message}</p>}
        </div>
      )}
    </section>
  )
}

function BillPrint({ shopName, billId }: { shopName: string; billId: string }) {
  const { t } = useTranslation()
  const query = useServer<BillView>(['print', 'bill', billId], `/v1/bills/${billId}`)
  if (query.isError) return <Problem error={query.error} />
  const view = query.data
  if (!view) return <p className="p-6">…</p>
  const { bill, customer } = view
  const gstin = bill.gstin

  async function cancel() {
    const reason = window.prompt(t('print.cancelReason'))?.trim()
    if (!reason) return
    try {
      await api(`/v1/bills/${billId}/cancel`, 'POST', { reason })
      await query.refetch()
    } catch (err) {
      window.alert(err instanceof Error ? err.message : t('error'))
    }
  }
  const when = new Date(bill.occurredAt).toLocaleString('en-IN', { day: 'numeric', month: 'short', year: 'numeric', hour: 'numeric', minute: '2-digit' })
  const share = t('print.billShare', { shop: shopName, total: bill.total, paid: bill.paidAmount, khata: bill.onKhata })
  // No GSTIN: not registered, so no tax on the bill at all.
  // A composition shop's bill of supply shows no tax. Bills keep their tax since it was stored; older ones work it out.
  const gst = gstin && !bill.billOfSupply ? storedSummary(bill.lines) ?? gstSummary(bill.lines).map((g) => ({ ...g, igst: 0 })) : []
  const igst = gst.some((g) => g.igst > 0)
  const unset = gstin ? gstUnsetAmount(bill.lines) : 0
  const isTaxInvoice = gst.length > 0 && unset === 0

  return (
    <Page title={shopName} subtitle={`${bill.billOfSupply ? t('print.billOfSupply') : isTaxInvoice ? t('print.taxInvoice') : t('print.bill')}${bill.billNo ? ` ${t('print.billNo', { no: bill.billNo })}` : ''} · ${when}${customer ? ` · ${customer.name}` : ''}`} shareText={share} phone={customer?.phone ?? null}>
      {bill.cancelledAt && <p className="mb-3 border-2 border-black p-2 text-center font-extrabold">{t('print.cancelled')}{bill.cancelReason && ` · ${bill.cancelReason}`}</p>}
      {(gstin || bill.site || bill.vehicleNumber) && (
        <p className="mb-3 text-sm">
          {[gstin && `GSTIN ${gstin}`, bill.buyerGstin && `${t('print.buyerGstin')} ${bill.buyerGstin}`, bill.site && `${t('trade.site')}: ${bill.site}`, bill.vehicleNumber && `${t('trade.vehicle')}: ${bill.vehicleNumber}`].filter(Boolean).join(' · ')}
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
              <td className="py-1.5 pr-2">{l.name}{l.batch && <span className="block text-xs text-gray-600">Batch {l.batch}</span>}{l.hsnCode && <span className="block text-xs text-gray-600">HSN {l.hsnCode}{gstin && l.gstRate != null ? ` · GST ${l.gstRate}%` : ''}</span>}</td>
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
              <th className="py-1">GST</th><th className="text-right">{t('print.taxable')}</th>{igst ? <th className="text-right">IGST</th> : <><th className="text-right">CGST</th><th className="text-right">SGST</th></>}
            </tr>
          </thead>
          <tbody>
            {gst.map((g) => (
              <tr key={g.rate} className="border-b border-gray-300">
                <td className="py-1">{g.rate}%</td>
                <td className="text-right">{formatRupees(g.taxable)}</td>
                {igst ? <td className="text-right">{formatRupees(g.igst)}</td> : <>
                <td className="text-right">{formatRupees(g.cgst)}</td>
                <td className="text-right">{formatRupees(g.sgst)}</td></>}
              </tr>
            ))}
            {unset > 0 && (
              <tr className="border-b border-gray-300">
                <td className="py-1">{t('print.gstUnset')}</td>
                <td className="text-right">{formatRupees(unset)}</td>
                <td className="text-right">—</td>
                {!igst && <td className="text-right">—</td>}
              </tr>
            )}
          </tbody>
        </table>
      )}
      {gst.length > 0 && <p className="mt-1 text-xs text-gray-600">{t('print.gstIncluded')}</p>}
      {bill.billOfSupply && <p className="mt-2 text-sm">{t('print.compositionNote')}</p>}
      {unset > 0 && <p className="mt-1 text-sm font-semibold text-due print:hidden">{t('print.gstUnsetWarning')}</p>}
      <p className="mt-6 text-center">{t('print.thanks')}</p>
      {!bill.cancelledAt && <CreditNotesPanel billId={billId} lines={bill.lines} />}
      {!bill.cancelledAt && <button onClick={cancel} className="mt-6 h-11 w-full rounded-xl border border-line font-semibold text-due print:hidden">{t('print.cancelBill')}</button>}
    </Page>
  )
}

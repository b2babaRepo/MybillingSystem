import { SendFromShop } from '../components/ShopWhatsApp'
import { shownLocale } from '../i18n'
import { lazy, Suspense, useEffect, useState } from 'react'
import QRCode from 'qrcode'
import { AttachCustomer } from '../components/AttachCustomer'
import { Page } from '../components/PrintPage'
import type { ReceiptRow } from '../lib/escpos'
const EwayBill = lazy(() => import('../components/EwayBill').then((m) => ({ default: m.EwayBill })))
import { stateLabel } from '../lib/states'
import { navigate } from '../router'
import { startExchange } from '../lib/exchange'
import { useQuery } from '@tanstack/react-query'
import { PrescriptionPhoto } from './PharmacyAssistScreens'
import { useTranslation } from 'react-i18next'
import { api, ApiError } from '../lib/api'
import { formatRupees, whatsAppLink } from '../lib/format'
import { rupeesInWords } from '../lib/inWords'
import { guarded } from '../lib/billPasscode'
import { gstSummary, gstUnsetAmount, storedSummary } from '../lib/gst'
import { listOps } from '../lib/outbox'
import { provisionalBill } from '../lib/provisionalBill'
import type { ProvisionalBill } from '../lib/provisionalBill'
import type { BillView, Statement } from '../lib/types'
import { ask, tell } from '../components/Ask'


/**
 * Print-ready pages: the month-end khata statement and the cash memo. White, black text, no app chrome, so the
 * phone's "Print → Save as PDF" makes a clean PDF with Hindi rendered by the browser; a WhatsApp button sends a
 * short summary for customers without a printer.
 */
export function PrintScreen({ shopId, shopName, kind, id, month }: { shopId: string; shopName: string; kind: 'statement' | 'bill' | 'supplier' | 'slip'; id: string; month?: string }) {
  if (kind === 'slip') return <SlipPrint shopName={shopName} noteId={id} />
  if (kind === 'supplier') return <SupplierStatementPrint shopName={shopName} supplierId={id} />
  return kind === 'statement' ? <StatementPrint shopName={shopName} customerId={id} month={month!} /> : <BillPrint shopId={shopId} shopName={shopName} billId={id} />
}


function useServer<T>(key: unknown[], path: string) {
  return useQuery({ queryKey: key, queryFn: () => api<T>(path), retry: false })
}

function Problem({ error }: { error: Error }) {
  const { t } = useTranslation()
  return <p className="p-6 text-due">{error instanceof ApiError && error.status === 404 ? t('print.notSynced') : error.message}</p>
}

interface LedgerRow { key: string; date: string; details: string; debit: number; credit: number; balance: number | null; struck?: boolean }

/**
 * A party's account as owners and their CAs read it: money each way in its own column (Dr, Cr), never a minus sign,
 * a balance that says which way it stands, a totals row, and one closing line in words: who owes whom.
 * On a receipt roll each line takes two rows: the date and details, then the three amounts.
 */
function Ledger({ rows, debitLabel, creditLabel, balanceSide, closing }: {
  rows: LedgerRow[]; debitLabel: string; creditLabel: string; balanceSide: (balance: number) => string; closing: string
}) {
  const { t } = useTranslation()
  const money = (n: number) => (n ? formatRupees(n) : '')
  const totalDebit = rows.reduce((sum, r) => sum + r.debit, 0)
  const totalCredit = rows.reduce((sum, r) => sum + r.credit, 0)
  return (
    <>
      <table className="ledger w-full border-collapse text-[15px]">
        <thead>
          <tr className="border-b border-black text-left">
            <th className="py-1.5 pr-2">{t('ledger.date')}</th><th className="pr-2">{t('ledger.details')}</th>
            <th className="text-right">{debitLabel}</th><th className="pl-2 text-right">{creditLabel}</th><th className="pl-3 text-right">{t('ledger.balance')}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((r) => (
            <tr key={r.key} className={`border-b border-gray-300 ${r.struck ? 'line-through' : ''}`}>
              <td className="py-1.5 pr-2 whitespace-nowrap">{r.date}</td>
              <td className="pr-2">{r.details}</td>
              <td className="text-right whitespace-nowrap">{money(r.debit)}</td>
              <td className="pl-2 text-right whitespace-nowrap">{money(r.credit)}</td>
              <td className="pl-3 text-right whitespace-nowrap">{r.balance == null ? '' : r.balance === 0 ? formatRupees(0) : `${formatRupees(Math.abs(r.balance))} ${balanceSide(r.balance)}`}</td>
            </tr>
          ))}
          <tr className="border-b border-black font-bold">
            <td className="py-1.5" /><td>{t('ledger.total')}</td>
            <td className="text-right whitespace-nowrap">{formatRupees(totalDebit)}</td><td className="pl-2 text-right whitespace-nowrap">{formatRupees(totalCredit)}</td><td />
          </tr>
        </tbody>
      </table>
      <p className="mt-4 border-t-2 border-black pt-2 text-xl font-extrabold">{closing}</p>
    </>
  )
}

/** Dr and Cr after a balance, the way the books write it. */
const side = (t: (key: string) => string, dr: boolean) => t(dr ? 'ledger.dr' : 'ledger.cr')

/** Everything between the shop and a supplier, oldest first: bills on the Cr side, payments on the Dr side. */
function SupplierStatementPrint({ shopName, supplierId }: { shopName: string; supplierId: string }) {
  const { t } = useTranslation()
  const query = useServer<{
    supplier: { name: string; phone: string | null; due: number; gstin?: string | null }
    entries: { id: string; amount: number; note: string | null; occurredAt: string; kind: string; mode: string | null; reference: string | null; balance: number; cancelled: boolean }[]
  }>(['print', 'supplier', supplierId], `/v1/suppliers/${supplierId}`)
  if (query.isError) return <Problem error={query.error} />
  const s = query.data
  if (!s) return <p className="p-6">…</p>
  const name = s.supplier.name
  const due = s.supplier.due
  const rows: LedgerRow[] = [...s.entries].reverse().map((e) => {
    const kind = t(`supplier2.kind.${e.kind}`)
    // "Bill cancelled" is already in the line's kind; the note only repeats it.
    // Older credit notes stored "Credit note" in their note too.
    const text = e.note?.replace(/^Credit note\s*/, '')
    const note = text && e.kind !== 'bill_cancelled' && text !== kind ? ` ${text}` : ''
    return {
      key: e.id, date: new Date(e.occurredAt).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: '2-digit' }),
      details: `${kind}${note}${e.mode ? ` · ${t(e.mode)}` : ''}${e.reference ? ` ${e.reference}` : ''}`,
      debit: e.amount < 0 ? -e.amount : 0, credit: e.amount > 0 ? e.amount : 0, balance: e.balance, struck: e.cancelled,
    }
  })
  const closing = due > 0 ? t('ledger.weMustPay', { name, amount: formatRupees(due) }) : due < 0 ? t('ledger.theyMustPay', { name, amount: formatRupees(-due) }) : t('ledger.settled')
  return (
    <Page title={shopName} subtitle={`${t('supplier2.statementTitle')} · ${name}${s.supplier.gstin ? ` · ${s.supplier.gstin}` : ''}`} phone={s.supplier.phone}
      shareText={t('supplier2.share', { shop: shopName, name, amount: formatRupees(due) })}>
      {/* Owed to the supplier is a credit balance (Cr); owed by them, a debit (Dr). */}
      <Ledger rows={rows} debitLabel={t('ledger.paid')} creditLabel={t('ledger.bill')} balanceSide={(b) => side(t, b < 0)} closing={closing} />
    </Page>
  )
}

function StatementPrint({ shopName, customerId, month }: { shopName: string; customerId: string; month: string }) {
  const { t } = useTranslation()
  const query = useServer<Statement>(['print', 'statement', customerId, month], `/v1/customers/${customerId}/statement?month=${month}`)
  if (query.isError) return <Problem error={query.error} />
  const s = query.data
  if (!s) return <p className="p-6">…</p>
  const monthLabel = new Date(`${month}-01T00:00:00Z`).toLocaleDateString(shownLocale(), { month: 'long', year: 'numeric', timeZone: 'UTC' })
  const share = t('print.statementShare', { shop: shopName, month: monthLabel, opening: s.openingBalance, closing: s.closingBalance })
  const name = s.customer.name
  const rows: LedgerRow[] = [
    { key: 'opening', date: '', details: t('print.openingBalance'), debit: 0, credit: 0, balance: s.openingBalance },
    ...s.entries.map((e) => ({
      key: e.id, date: new Date(e.occurredAt).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' }),
      details: `${e.kind === 'reversal' ? t('reversal') : e.kind === 'opening' ? t('partyImport.openingShort') : e.amount > 0 ? t('creditShort') : t('paymentShort')}${e.paymentMode ? ` (${t(e.paymentMode)}${e.upiRef ? ` · UTR ${e.upiRef}` : ''}${e.chequeNo ? ` #${e.chequeNo}` : ''}${e.chequeStatus && e.kind !== 'reversal' ? ` · ${t(`cheques.status.${e.chequeStatus}`)}` : ''})` : ''}${e.note ? ` · ${e.note}` : ''}`,
      debit: e.amount > 0 ? e.amount : 0, credit: e.amount < 0 ? -e.amount : 0, balance: e.runningBalance, struck: e.isReversed,
    })),
  ]
  const closing = s.closingBalance > 0 ? t('ledger.owesYou', { name, amount: formatRupees(s.closingBalance) })
    : s.closingBalance < 0 ? t('ledger.advance', { name, amount: formatRupees(-s.closingBalance) }) : t('ledger.settled')
  return (
    <Page title={shopName} subtitle={`${t('print.monthStatement')} · ${monthLabel} · ${name}${s.customer.phone ? ` · ${s.customer.phone}` : ''}`} shareText={share} phone={s.customer.phone}>
      {/* The customer owes: a debit balance (Dr). Paid ahead: an advance, a credit (Cr). */}
      <Ledger rows={rows} debitLabel={t('ledger.udhaar')} creditLabel={t('ledger.jama')} balanceSide={(b) => side(t, b > 0)} closing={closing} />
    </Page>
  )
}

/** Goods taken back on this bill: earlier credit notes, and a form to make one. Owner only (the server checks). */
function CreditNotesPanel({ billId, customerId, lines }: { billId: string; customerId: string | null; lines: BillView['bill']['lines'] }) {
  const { t } = useTranslation()
  const notes = useQuery({ queryKey: ['credit-notes', billId], queryFn: () => api<{ id: string; noteNo: string; reason: string; total: number; occurredAt: string; lines: { billLineNo: number; quantity: number }[] }[]>(`/v1/bills/${billId}/credit-notes`), retry: false })
  const [open, setOpen] = useState(false)
  const [qty, setQty] = useState<Record<number, string>>({})
  const [reason, setReason] = useState('')
  // Units sold by serial: which ones are coming back, when it isn't all of them.
  const [serials, setSerials] = useState('')
  const [restock, setRestock] = useState(true)
  // Exchange: the goods' value stays as credit for a new bill instead of going back as money.
  const [exchange, setExchange] = useState(false)
  const [refundMode, setRefundMode] = useState<'cash' | 'upi' | 'card' | 'bank'>('cash')
  const [message, setMessage] = useState('')
  const [taken, setTaken] = useState<{ noteId: string; noteNo: string; total: number; customerId: string | null } | null>(null)
  if (notes.isError) return null
  const back = (lineNo: number) => (notes.data ?? []).flatMap((n) => n.lines).filter((l) => l.billLineNo === lineNo).reduce((s, l) => s + l.quantity, 0)

  async function save() {
    setMessage('')
    try {
      const body = {
        id: crypto.randomUUID(), reason: reason.trim(), restock, exchange, refundMode: !customerId && !exchange ? refundMode : undefined, serials: serials.split(/[\s,]+/).filter(Boolean),
        lines: Object.entries(qty).filter(([, q]) => Number(q) > 0).map(([lineNo, q]) => ({ lineNo: Number(lineNo), quantity: Number(q) })),
      }
      const note = await guarded(() => api<{ id: string; noteNo: string; total: number }>(`/v1/bills/${billId}/credit-notes`, 'POST', body))
      setQty({}); setReason(''); setSerials(''); setOpen(false)
      // Exchange: bill the new goods here, or print a slip the customer takes to another counter.
      if (exchange) { setTaken({ noteId: note.id, noteNo: note.noteNo, total: note.total, customerId }); await notes.refetch(); return }
      await notes.refetch()
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  return (
    <section id="bill-actions" className="mt-6 flex scroll-mt-4 flex-col gap-2 print:hidden">
      {notes.data?.map((n) => (
        <p key={n.id} className="flex justify-between text-sm"><span>{n.noteNo} · {new Date(n.occurredAt).toLocaleDateString(shownLocale())} · {n.reason}</span><span>−{formatRupees(n.total)}</span></p>
      ))}
      {taken && (
        <div className="flex flex-col gap-2 rounded-xl bg-brand-soft p-3">
          <p className="text-sm"><b>{t('slip.taken', { note: taken.noteNo, amount: formatRupees(taken.total) })}</b></p>
          <div className="grid grid-cols-2 gap-2">
            <button onClick={() => { startExchange(taken); navigate('/bill') }} className="min-h-11 rounded-xl bg-brand px-2 py-2 font-semibold leading-tight text-white">{t('slip.billHere')}</button>
            <button onClick={() => navigate(`/print/slip/${taken.noteId}`)} className="min-h-11 rounded-xl border border-line bg-white px-2 py-2 font-semibold leading-tight">🎟 {t('slip.print')}</button>
          </div>
          <p className="text-xs text-muted">{t('slip.otherCounter')}</p>
        </div>
      )}
      {lines.every((l, i) => l.quantity - back(i + 1) <= 0) ? (
        <p className="text-sm text-muted">{t('creditNote.allBack')}</p>
      ) : !open ? (
        <div className="grid grid-cols-2 gap-2">
          <button onClick={() => { setExchange(true); setReason(t('exchange.reason')); setOpen(true) }} className="min-h-11 rounded-xl bg-brand px-2 py-2 font-semibold leading-tight text-white">{t('exchange.start')}</button>
          <button onClick={() => { setExchange(false); setOpen(true) }} className="min-h-11 rounded-xl border border-line px-2 py-2 font-semibold leading-tight transition hover:bg-[#f6faf9]">{t('creditNote.start')}</button>
        </div>
      ) : (
        <div className="flex flex-col gap-2 rounded-xl border border-line p-3">
          {lines.map((l, i) => {
            const left = l.quantity - back(i + 1)
            return left > 0 ? (
              <label key={i} className="flex items-center justify-between gap-2 text-sm"><span>{l.name} ({t('creditNote.upTo', { qty: left })})</span>
                <input inputMode="decimal" value={qty[i + 1] ?? ''} onChange={(e) => setQty({ ...qty, [i + 1]: e.target.value })} className="h-11 w-24 rounded-lg border border-line px-2" /></label>
            ) : null
          })}
          {lines.some((l) => l.batch?.includes('S/N')) && <input aria-label={t('creditNote.serials')} placeholder={t('creditNote.serials')} value={serials} onChange={(e) => setSerials(e.target.value)} className="h-11 rounded-lg border border-line px-3" />}
          <input aria-label={t('creditNote.reason')} placeholder={t('creditNote.reason')} value={reason} onChange={(e) => setReason(e.target.value)} maxLength={200} className="h-11 rounded-lg border border-line px-3" />
          {!customerId && !exchange && (
            <label className="flex items-center justify-between gap-2 text-sm">{t('creditNote.refundBy')}
              <select value={refundMode} onChange={(e) => setRefundMode(e.target.value as typeof refundMode)} className="h-11 rounded-lg border border-line bg-white px-2">
                {(['cash', 'upi', 'card', 'bank'] as const).map((m) => <option key={m} value={m}>{t(m)}</option>)}
              </select>
            </label>
          )}
          <label className="flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={restock} onChange={(e) => setRestock(e.target.checked)} className="h-5 w-5" />{t('creditNote.restock')}</label>
          <button disabled={reason.trim().length < 3 || !Object.values(qty).some((q) => Number(q) > 0)} onClick={() => void save()} className="h-11 rounded-xl bg-brand font-semibold text-white disabled:opacity-40">{exchange ? t('exchange.next') : t('creditNote.save')}</button>
          {message && <p className="text-sm text-due">{message}</p>}
        </div>
      )}
    </section>
  )
}

/** A bill still on this device: the customer gets a receipt now, without a bill number or tax, which only the server gives. */
function ProvisionalPrint({ shopName, bill }: { shopName: string; bill: ProvisionalBill }) {
  const { t } = useTranslation()
  const when = new Date(bill.occurredAt).toLocaleString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric', hour: 'numeric', minute: '2-digit' })
  return (
    <Page title={shopName} subtitle={`${t('print.provisional')} · ${when}${bill.customerName ? ` · ${bill.customerName}` : ''}`} shareText={t('print.billShare', { shop: shopName, total: bill.total, paid: bill.paidAmount, khata: bill.onKhata })} phone={null}>
      <p className="mb-3 border-2 border-black p-2 text-center text-sm font-bold">{t('print.provisionalNote')}</p>
      <table className="bill-lines w-full border-collapse text-[15px]">
        <thead>
          <tr className="border-b border-black text-left">
            <th className="py-1.5">{t('print.item')}</th><th className="text-right">{t('print.qty')}</th><th className="text-right">{t('print.rate')}</th><th className="text-right">{t('print.amount')}</th>
          </tr>
        </thead>
        <tbody>
          {bill.lines.map((l, i) => (
            <tr key={i} className="border-b border-gray-300">
              <td className="py-1.5 pr-2">{l.name}</td>
              <td className="text-right whitespace-nowrap">{l.quantity} {t(`units.${l.unit}`, { count: l.quantity })}{l.bags ? <span className="block text-xs text-gray-600">{t('bag.inBags', { count: l.bags })}</span> : null}</td>
              <td className="text-right whitespace-nowrap">{formatRupees(l.unitPrice)}</td>
              <td className="text-right whitespace-nowrap">{formatRupees(l.amount)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <div className="mt-4 flex flex-col gap-1 border-t-2 border-black pt-2 text-lg">
        <p className="flex justify-between text-xl font-extrabold"><span>{t('print.total')}</span><span>{formatRupees(bill.total)}</span></p>
        {(bill.payments?.length ?? 0) > 0 ? bill.payments!.map((p) => <p key={p.mode} className="flex justify-between"><span>{t('print.paid')} ({t(p.mode)}{'reference' in p && p.reference ? ` · UTR ${p.reference}` : ''})</span><span>{formatRupees(p.amount)}</span></p>) : <p className="flex justify-between"><span>{t('print.paid')} ({t(bill.paymentMode)})</span><span>{formatRupees(bill.paidAmount)}</span></p>}
        {bill.onKhata > 0 && <p className="flex justify-between font-semibold"><span>{t('print.onKhata')}</span><span>{formatRupees(bill.onKhata)}</span></p>}
      </div>
      <p className="mt-6 text-center">{t('print.thanks')}</p>
    </Page>
  )
}

function BillPrint({ shopId, shopName, billId }: { shopId: string; shopName: string; billId: string }) {
  const { t } = useTranslation()
  const query = useServer<BillView>(['print', 'bill', billId], `/v1/bills/${billId}`)
  const [attached, setAttached] = useState(false)
  // The server doesn't have it (not sent yet, or no connection): it may still be waiting on this device.
  const waiting = useQuery({
    queryKey: ['print', 'waiting', billId],
    queryFn: async () => provisionalBill((await listOps(shopId)).find((op) => op.kind === 'bill' && op.id === billId)),
    enabled: query.isError,
    gcTime: 0,
  })
  // A link to send with the bill. Only for a bill the server has: an unsynced one has none yet.
  const link = useQuery({ queryKey: ['bill-link', billId], queryFn: () => api<{ shopSlug: string; billId: string; token: string }>(`/v1/bills/${billId}/link`, 'POST'), enabled: !!query.data, staleTime: Infinity, retry: false })
  if (query.isError) {
    if (waiting.isPending) return <p className="p-6">…</p>
    return waiting.data ? <ProvisionalPrint shopName={shopName} bill={waiting.data} /> : <Problem error={query.error} />
  }
  const view = query.data
  if (!view) return <p className="p-6">…</p>
  const cash = (view.bill.payments?.length ?? 0) > 0 ? view.bill.payments!.filter((p) => p.mode === 'cash').reduce((a, p) => a + p.amount, 0) : view.bill.paymentMode === 'cash' ? view.bill.paidAmount : 0
  async function cancel() {
    const reason = (await ask(t('print.cancelReason')))?.trim()
    if (!reason) return
    try {
      await guarded(() => api(`/v1/bills/${billId}/cancel`, 'POST', { reason }))
      await query.refetch()
    } catch (err) {
      void tell(err instanceof Error ? err.message : t('error'))
    }
  }
  const url = link.data ? `${window.location.origin}/#/b/${link.data.shopSlug}/${link.data.billId}?t=${link.data.token}` : null
  return (
    <>
      {/* A customer back with an item: the actions are under the bill, so take them straight there. */}
      {!view.bill.cancelledAt && (
        <div className="mx-auto max-w-2xl px-4 pt-3 print:hidden">
          <button onClick={() => document.getElementById('bill-actions')?.scrollIntoView({ behavior: 'smooth' })}
            className="flex min-h-11 w-full items-center justify-between rounded-xl bg-warn-soft px-4 text-left text-sm font-semibold text-warn-ink">
            <span>↩ {t('findBill.actions')}</span><span aria-hidden>↓</span>
          </button>
        </div>
      )}
      <BillDocument shopName={shopName} bill={view.bill} customerName={view.customer?.name ?? null} phone={view.customer?.phone ?? null} link={url} onCancel={() => void cancel()} />
      {!view.bill.customerId && !view.bill.cancelledAt && (
        <div className="mx-auto max-w-2xl bg-white px-4 pb-4"><AttachCustomer shopId={shopId} billId={billId} occurredAt={view.bill.occurredAt} cash={cash} onDone={() => { setAttached(true); void query.refetch() }} /></div>
      )}
      {/* Just put on their khata: offer to tell them, with the bill's link. */}
      {attached && view.customer && (
        <div className="mx-auto flex max-w-2xl flex-wrap items-center gap-3 bg-white px-4 pb-4 text-sm print:hidden">
          <span>{t('attach.done', { name: view.customer.name })}</span>
          <a href={whatsAppLink(view.customer.phone ?? null, [t('attach.message', { shop: shopName, total: view.bill.total, no: view.bill.billNo ?? '' }), url].filter(Boolean).join('\n'))} target="_blank" rel="noreferrer" className="flex h-11 items-center rounded-xl bg-[#15803d] px-4 font-semibold text-white">{t('attach.send')}</a>
        </div>
      )}
    </>
  )
}

/** The bill itself, for the shop and, with no onCancel, for whoever opens its link. */
function BillDocument({ shopName, bill, customerName, phone, link, onCancel, bare }: { shopName: string; bill: BillView['bill']; customerName: string | null; phone: string | null; link: string | null; onCancel?: () => void; bare?: boolean }) {
  const { t } = useTranslation()
  const gstin = bill.gstin
  const when = new Date(bill.occurredAt).toLocaleString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric', hour: 'numeric', minute: '2-digit' })
  const share = [t('print.billShare', { shop: shopName, total: bill.total, paid: bill.paidAmount, khata: bill.onKhata }), link].filter(Boolean).join('\n')
  // No GSTIN: not registered, so no tax on the bill at all.
  // A composition shop's bill of supply shows no tax. Bills keep their tax since it was stored; older ones work it out.
  const gst = gstin && !bill.billOfSupply ? storedSummary(bill.lines) ?? gstSummary(bill.lines).map((g) => ({ ...g, igst: 0 })) : []
  const igst = gst.some((g) => g.igst > 0)
  const unset = gstin ? gstUnsetAmount(bill.lines) : 0
  const isTaxInvoice = gst.length > 0 && unset === 0
  const title = `${bill.billOfSupply ? t('print.billOfSupply') : isTaxInvoice ? t('print.taxInvoice') : t('print.bill')}${bill.billNo ? ` ${t('print.billNo', { no: bill.billNo })}` : ''}`
  const look = bill.print
  // Which copy this is: GST rules want the buyer's original, a duplicate for the transporter and a triplicate kept by the seller.
  const [copy, setCopy] = useState<'none' | 'original' | 'duplicate' | 'triplicate'>(isTaxInvoice ? 'original' : 'none')

  /** The same bill for a Bluetooth roll printer. */
  function billReceipt(): ReceiptRow[] {
    const rows: ReceiptRow[] = [{ text: shopName, align: 'center', big: true }]
    if (bill.shopAddress) rows.push({ text: bill.shopAddress, align: 'center' })
    if (gstin) rows.push({ text: `GSTIN ${gstin}`, align: 'center' })
    rows.push({ text: title, align: 'center', bold: true }, { text: when, align: 'center' })
    if (customerName || phone) rows.push({ text: t('print.billTo', { who: [customerName, phone].filter(Boolean).join(' · ') }) })
    if (bill.buyerGstin) rows.push({ text: `${t('print.buyerGstin')} ${bill.buyerGstin}` })
    for (const f of bill.fields ?? []) rows.push({ text: `${f.label}: ${f.value}` })
    if (bill.cancelledAt) rows.push({ text: t('print.cancelled'), align: 'center', big: true })
    rows.push({ rule: true })
    for (const l of bill.lines) {
      rows.push({ left: l.printName ?? l.name, right: formatRupees(l.amount), bold: true })
      rows.push({ text: `  ${l.quantity} ${t(`units.${l.unit}`, { count: l.quantity })} × ${formatRupees(l.unitPrice)}${l.hsnCode ? ` · HSN ${l.hsnCode}` : ''}${gstin && l.gstRate != null ? ` · GST ${l.gstRate}%` : ''}` })
    }
    rows.push({ rule: true }, { left: t('print.total'), right: formatRupees(bill.total), big: true }, { text: rupeesInWords(bill.total) })
    if ((bill.exchangeCredit ?? 0) > 0) rows.push({ left: t('exchange.credit', { note: bill.exchangeNoteNo ?? '' }), right: `− ${formatRupees(bill.exchangeCredit!)}` })
    if ((bill.payments?.length ?? 0) > 0) for (const p of bill.payments!) rows.push({ left: `${t('print.paid')} (${t(p.mode)}${p.reference ? ` · UTR ${p.reference}` : ''})`, right: formatRupees(p.amount) })
    else rows.push({ left: `${t('print.paid')}${bill.paymentMode ? ` (${t(bill.paymentMode)})` : ''}`, right: formatRupees(bill.paidAmount) })
    if (bill.onKhata > 0) rows.push({ left: t('print.onKhata'), right: formatRupees(bill.onKhata), bold: true })
    if (bill.dueOn && bill.onKhata > 0) rows.push({ left: t('print.dueBy'), right: new Date(`${bill.dueOn}T00:00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric' }), bold: true })
    if (gst.length > 0) {
      rows.push({ rule: true }, { text: t('print.gstTitle'), bold: true })
      for (const g of gst) rows.push({ left: `${g.rate}% · ${formatRupees(g.taxable)}`, right: igst ? `IGST ${formatRupees(g.igst)}` : `${formatRupees(g.cgst)} + ${formatRupees(g.sgst)}` })
    }
    rows.push({ gap: true }, { text: t('btPrint.thanks'), align: 'center' })
    return rows
  }

  return (
    <Page title={shopName} subtitle={bill.billOfSupply ? t('print.billOfSupply') : isTaxInvoice ? t('print.taxInvoice') : t('print.bill')} shareText={share} phone={phone} receipt={() => billReceipt()} pdfName={`${shopName} ${bill.billNo ?? when}`}
      logo={look?.logo} theme={look?.theme} corner={copy !== 'none' ? t(`print.copy.${copy}`) : null} bare={bare}
      tools={(<>
        {bill.customerId && onCancel && <SendFromShop kind="bill" billId={bill.id} />}
        <select aria-label={t('print.copy.label')} value={copy} onChange={(e) => setCopy(e.target.value as typeof copy)} className="h-11 rounded-xl border border-line bg-white px-2 font-semibold transition hover:bg-[#f6faf9]">
          {(['none', 'original', 'duplicate', 'triplicate'] as const).map((c) => <option key={c} value={c}>{t(`print.copy.${c === 'none' ? 'noLabel' : c}`)}</option>)}
        </select>
      </>)}>
      {bill.billOfSupply && <p className="mb-2 text-sm font-semibold">{t('print.compositionNote')}</p>}
      {(bill.shopAddress || gstin) && <p className="mb-3 text-sm">{[bill.shopAddress, gstin && `GSTIN ${gstin}`].filter(Boolean).join(' · ')}</p>}
      {gstin && !bill.shopAddress && <p className="mb-2 text-sm font-semibold text-due print:hidden">{t('print.shopAddressMissing')}</p>}
      {bill.cancelledAt && <p className="mb-3 border-2 border-black p-2 text-center font-extrabold">{t('print.cancelled')}{bill.cancelReason && ` · ${bill.cancelReason}`}</p>}
      {bill.prescription && <PrescriptionPhoto billId={bill.id} />}
      {bill.prescription && <p className="mb-2 text-sm">{t('rx.prescribedBy', { doctor: [bill.prescription.doctorName, bill.prescription.doctorRegNo].filter(Boolean).join(', '), patient: bill.prescription.patientName })}</p>}
      {/* Who it is for on the left, the bill's own details on the right, as on a printed invoice. */}
      <div className="bill-parties mb-3 grid grid-cols-2 gap-4 text-sm">
        <div className="flex flex-col">
          <span className="text-xs font-bold uppercase tracking-wide text-gray-600">{t('print.billToHead')}</span>
          {customerName || phone ? <>
            {customerName && <span className="text-base font-bold">{customerName}</span>}
            {phone && <span>{phone}</span>}
            {bill.buyerGstin && <span>{t('print.buyerGstin')} {bill.buyerGstin}</span>}
            {bill.buyerAddress && <span>{bill.buyerAddress}</span>}
          </> : <span>{t('print.walkIn')}</span>}
        </div>
        <div className="bill-details flex flex-col text-right">
          <span className="text-xs font-bold uppercase tracking-wide text-gray-600">{t('print.detailsHead')}</span>
          {bill.billNo && <span className="text-base font-bold">{t('print.billNo', { no: bill.billNo })}</span>}
          <span>{when}</span>
          {gstin && !bill.billOfSupply && stateLabel(bill.placeOfSupply) && <span>{t('pos.print')}: {stateLabel(bill.placeOfSupply)}</span>}
          {bill.site && <span>{t('trade.site')}: {bill.site}</span>}
          {bill.vehicleNumber && <span>{t('trade.vehicle')}: {bill.vehicleNumber}</span>}
          {bill.fields?.map((f) => <span key={f.label}>{f.label}: {f.value}</span>)}
          {bill.counter && <span className="text-gray-600">{bill.counter}</span>}
        </div>
      </div>
      {bill.buyerGstin && !bill.buyerAddress && <p className="mb-3 text-sm font-semibold text-due print:hidden">{t('print.buyerAddressMissing')}</p>}
      {gstin && !bill.buyerGstin && bill.total >= 50000 && !bill.buyerAddress && <p className="mb-3 text-sm font-semibold text-due print:hidden">{t('print.bigBillAddress')}</p>}
      {(() => {
        // GST on each line: stored when billed, else worked out from the rate (prices include GST).
        const lineGst = (l: BillView['bill']['lines'][number]) => (l.cgst ?? 0) + (l.sgst ?? 0) + (l.igst ?? 0) || (l.gstRate ? Math.round((l.amount - l.amount / (1 + l.gstRate / 100)) * 100) / 100 : 0)
        const showTax = gst.length > 0
        const sameUnit = bill.lines.every((l) => l.unit === bill.lines[0]?.unit)
        const totalQty = Math.round(bill.lines.reduce((s, l) => s + l.quantity, 0) * 1000) / 1000
        const totalGst = Math.round(bill.lines.reduce((s, l) => s + lineGst(l), 0) * 100) / 100
        return (
          <table className="bill-lines w-full border-collapse text-[15px]">
            <thead>
              <tr className="bill-head border-b border-black text-left">
                <th className="a4-only w-8 py-1.5">#</th>
                <th className="cell-item py-1.5">{t('print.item')}</th>
                <th className="text-right">{t('print.qty')}</th>
                <th className="cell-rate text-right">{t('print.rate')}</th>
                {showTax && <th className="a4-only text-right">GST %</th>}
                {showTax && <th className="a4-only text-right">GST ₹</th>}
                <th className="text-right">{t('print.amount')}</th>
              </tr>
            </thead>
            <tbody>
              {bill.lines.map((l, i) => (
                <tr key={i} className="border-b border-gray-300 align-top">
                  <td className="a4-only py-1.5 text-gray-600">{i + 1}</td>
                  <td className="cell-item py-1.5 pr-2">{l.printName ?? l.name}{l.batch && <span className="block text-xs text-gray-600">{l.batch.startsWith('S/N') ? l.batch : `Batch ${l.batch}`}</span>}{l.hsnCode && <span className="block text-xs text-gray-600">HSN {l.hsnCode}<span className="roll-only">{gstin && l.gstRate != null ? ` · GST ${l.gstRate}%` : ''}</span></span>}</td>
                  <td className="py-1.5 text-right whitespace-nowrap">{l.quantity} {t(`units.${l.unit}`, { count: l.quantity })}{l.bags ? <span className="block text-xs text-gray-600">{t('bag.inBags', { count: l.bags })}</span> : null}</td>
                  <td className="cell-rate py-1.5 text-right whitespace-nowrap">{formatRupees(l.unitPrice)}</td>
                  {showTax && <td className="a4-only py-1.5 text-right whitespace-nowrap">{l.gstRate != null ? `${l.gstRate}%` : '—'}</td>}
                  {showTax && <td className="a4-only py-1.5 text-right whitespace-nowrap">{formatRupees(lineGst(l))}</td>}
                  <td className="py-1.5 text-right whitespace-nowrap">{formatRupees(l.amount)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="border-b-2 border-black font-bold">
                <td className="a4-only" />
                <td className="cell-item py-1.5">{t('print.total')}</td>
                <td className="py-1.5 text-right whitespace-nowrap">{sameUnit && bill.lines.length > 0 ? `${totalQty} ${t(`units.${bill.lines[0].unit}`, { count: totalQty })}` : ''}</td>
                <td className="cell-rate" />
                {showTax && <td className="a4-only" />}
                {showTax && <td className="a4-only py-1.5 text-right whitespace-nowrap">{formatRupees(totalGst)}</td>}
                <td className="py-1.5 text-right whitespace-nowrap">{formatRupees(bill.total)}</td>
              </tr>
            </tfoot>
          </table>
        )
      })()}
      {/* Amount in words on the left; what was paid and what is still due on the right. */}
      <div className="bill-sums mt-4 grid grid-cols-2 items-start gap-4">
        <div className="rounded-md bg-gray-100 p-2 text-sm">
          <p className="font-semibold">{t('print.inWords')}</p>
          <p>{rupeesInWords(bill.total)}</p>
        </div>
        <div className="flex flex-col gap-1">
          {gst.length > 0 && <p className="flex justify-between text-sm"><span>{t('print.taxable')}</span><span>{formatRupees(gst.reduce((s, g) => s + g.taxable, 0))}</span></p>}
          {gst.length > 0 && <p className="flex justify-between text-sm"><span>GST</span><span>{formatRupees(gst.reduce((s, g) => s + g.cgst + g.sgst + g.igst, 0))}</span></p>}
          {(() => {
            // What the customer saved against the items' regular prices, from the prices the server kept on each line.
            const saved = Math.round(bill.lines.reduce((sum, l) => sum + (l.listPrice != null && l.listPrice > l.unitPrice ? (l.listPrice - l.unitPrice) * l.quantity : 0), 0) * 100) / 100
            return saved > 0 ? <p className="flex justify-between text-sm font-semibold"><span>{t('discount.saved')}</span><span>{formatRupees(saved)}</span></p> : null
          })()}
          <p className="print-total flex justify-between rounded-md bg-[#e6f2f0] px-2 py-1 text-xl font-extrabold"><span>{t('print.total')}</span><span>{formatRupees(bill.total)}</span></p>
          {(bill.exchangeCredit ?? 0) > 0 && <p className="flex justify-between text-sm font-semibold"><span>{t('exchange.credit', { note: bill.exchangeNoteNo ?? '' })}</span><span>− {formatRupees(bill.exchangeCredit!)}</span></p>}
          {(bill.payments?.length ?? 0) > 0 ? bill.payments!.map((p) => <p key={p.mode} className="flex justify-between"><span>{t('print.paid')} ({t(p.mode)}{'reference' in p && p.reference ? ` · UTR ${p.reference}` : ''})</span><span>{formatRupees(p.amount)}</span></p>) : <p className="flex justify-between"><span>{t('print.paid')}{bill.paymentMode && ` (${t(bill.paymentMode)})`}</span><span>{formatRupees(bill.paidAmount)}</span></p>}
          <p className="flex justify-between font-bold"><span>{t('print.balanceDue')}</span><span>{formatRupees(bill.onKhata)}</span></p>
          {bill.dueOn && bill.onKhata > 0 && <p className="flex justify-between font-semibold"><span>{t('print.dueBy')}</span><span>{new Date(`${bill.dueOn}T00:00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric' })}</span></p>}
        </div>
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
      {isTaxInvoice && <p className="mt-2 text-sm">{t('print.reverseCharge')}</p>}
      {/* Goods over ₹50,000 moving by vehicle need an e-way bill: the shop's copy offers the portal file. */}
      {onCancel && gstin && !bill.billOfSupply && !bill.cancelledAt && bill.total >= 50000 && <Suspense fallback={null}><EwayBill shopName={shopName} bill={bill} customerName={customerName} /></Suspense>}
      {unset > 0 && <p className="mt-1 text-sm font-semibold text-due print:hidden">{t('print.gstUnsetWarning')}</p>}
      {look?.terms && (
        <div className="mt-4 text-xs">
          <p className="font-bold">{t('print.terms')}</p>
          <p className="whitespace-pre-line">{look.terms}</p>
        </div>
      )}
      {(gstin || look?.signature) && (
        <div className="mt-6 flex flex-col items-end text-sm">
          <p>{t('print.forShop', { shop: shopName })}</p>
          {look?.signature ? <img src={look.signature} alt="" className="my-1 h-14 max-w-48 object-contain" /> : <div className="h-10" />}
          <p>{t('print.signatory')}</p>
        </div>
      )}
      <p className="mt-6 text-center">{t('print.thanks')}</p>
      {onCancel && !bill.cancelledAt && <CreditNotesPanel billId={bill.id} customerId={bill.customerId} lines={bill.lines} />}
      {onCancel && !bill.cancelledAt && <button onClick={onCancel} className="mt-6 h-11 w-full rounded-xl border border-line font-semibold text-due print:hidden">{t('print.cancelBill')}</button>}
    </Page>
  )
}

/**
 * The bill being made, drawn exactly as it will print (A4, the shop's logo, style and terms), beside the bill screen on a
 * computer. No number yet: the server gives that when the bill is saved.
 */
export function BillPreview({ shopName, bill, customerName, phone }: { shopName: string; bill: BillView['bill']; customerName: string | null; phone: string | null }) {
  return <BillDocument shopName={shopName} bill={bill} customerName={customerName} phone={phone} link={null} bare />
}

/** #/b/{shop}/{bill}?t={token}: a bill anyone holding the link can open, print or save as a PDF. No account needed. */
export function BillLinkPage({ path }: { path: string }) {
  const { t } = useTranslation()
  const match = path.match(/^\/b\/([^/]+)\/([0-9a-f-]+)\?t=([0-9a-f]+)/)
  const view = useQuery({
    queryKey: ['bill-link-view', path],
    queryFn: () => api<{ shopName: string; bill: BillView['bill']; customerName: string | null }>(`/v1/bill/${match![1]}/${match![2]}?t=${match![3]}`),
    enabled: !!match,
    retry: false,
  })
  if (view.isError || !match) return <p className="p-6 text-center text-muted">{t('billLink.gone')}</p>
  if (!view.data) return <p className="p-6 text-center text-muted">…</p>
  return (
    <>
      <BillDocument shopName={view.data.shopName} bill={view.data.bill} customerName={view.data.customerName} phone={null} link={null} />
      <p className="mx-auto max-w-2xl bg-white p-4 text-center text-sm text-muted print:hidden">{t('billLink.madeWith')}</p>
    </>
  )
}

/** The exchange slip the customer carries to a billing counter: its number and QR bring the credit onto the new bill. */
function SlipPrint({ shopName, noteId }: { shopName: string; noteId: string }) {
  const { t } = useTranslation()
  const query = useServer<{ id: string; noteNo: string; amount: number; balance: number; issuedAt: string; validTill: string; status: string; billNo: string; customerName: string | null; customerPhone: string | null; usedOnBillNo: string | null; code: string | null }>(['slip', noteId], `/v1/exchange-slips/${noteId}`)
  const [qr, setQr] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const s = query.data
  // The QR carries the slip and its secret code: a scan at any counter brings it straight onto the bill.
  useEffect(() => { if (s) void QRCode.toDataURL(`GKSLIP:${s.id}:${s.code ?? ''}`, { width: 240, margin: 1 }).then(setQr) }, [s])
  async function act(path: string, body: object) {
    setBusy(true)
    try { await guarded(() => api(`/v1/exchange-slips/${noteId}/${path}`, 'POST', body)); await query.refetch() } catch (e) { await tell(e instanceof Error ? e.message : t('error')) } finally { setBusy(false) }
  }
  async function newCode() {
    const phone = s?.customerPhone ? (await ask(t('slip.askPhone')))?.trim() : ''
    if (phone === undefined || phone === null) return
    await act('new-code', { phone })
  }
  async function cancelSlip() {
    const reason = (await ask(t('slip.askCancel')))?.trim()
    if (reason) await act('cancel', { reason })
  }
  if (query.isError) return <Problem error={query.error} />
  if (!s) return <p className="p-6">…</p>
  const day = (iso: string) => new Date(iso.length === 10 ? `${iso}T00:00` : iso).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric' })
  return (
    <Page title={shopName} subtitle={`${t('slip.title')} · ${s.noteNo}`} phone={s.customerPhone}
      shareText={t('slip.share', { shop: shopName, note: s.noteNo, code: s.code ?? '', amount: s.balance, date: day(s.validTill) })}
      receipt={() => [
        { text: t('slip.title'), bold: true, align: 'center' },
        { text: s.noteNo, bold: true, align: 'center' },
        { text: `${t('slip.code')}: ${s.code ?? ''}`, bold: true, align: 'center' },
        { text: `${t('slip.amount')}: ${formatRupees(s.balance)}`, bold: true },
        { text: `${t('slip.validTill')}: ${day(s.validTill)}` },
        { text: `${t('slip.againstBill')}: ${s.billNo}` },
        ...(s.customerName ? [{ text: `${s.customerName}${s.customerPhone ? ` ${s.customerPhone}` : ''}` }] : []),
      ]}>
      <div className="flex flex-col items-center gap-2 text-center">
        <p className="text-sm font-bold uppercase tracking-wide">{t('slip.title')}</p>
        <p className="text-2xl font-extrabold">{s.noteNo}</p>
        <p className="text-lg">{t('slip.code')}: <b className="tracking-[0.3em]">{s.code}</b></p>
        <p className="text-3xl font-extrabold">{formatRupees(s.balance)}</p>
        {s.balance < s.amount && s.balance > 0 && <p className="text-sm">{t('slip.leftOf', { amount: formatRupees(s.amount), bill: s.usedOnBillNo ?? '' })}</p>}
        {qr && <img src={qr} alt={s.noteNo} className="h-40 w-40" />}
        <p>{t('slip.validTill')}: <b>{day(s.validTill)}</b></p>
        <p className="text-sm">{t('slip.againstBill')}: {s.billNo} · {day(s.issuedAt)}</p>
        {s.customerName && <p className="text-sm">{s.customerName}{s.customerPhone ? ` · ${s.customerPhone}` : ''}</p>}
        {s.status !== 'open' && <p className="font-bold text-due">{s.status === 'used' ? t('slip.used', { bill: s.usedOnBillNo ?? '' }) : s.status === 'cancelled' ? t('slip.cancelled') : t('slip.expired', { date: day(s.validTill) })}</p>}
        <p className="mt-2 text-xs">{t('slip.terms')}</p>
      </div>
      {s.status === 'open' && (
        <div className="mt-6 flex flex-col gap-2 print:hidden">
          <p className="text-sm text-muted">{t('slip.lostHint')}</p>
          <div className="grid grid-cols-2 gap-2">
            <button disabled={busy} onClick={() => void newCode()} className="min-h-11 rounded-xl border border-line px-2 py-2 font-semibold leading-tight">{t('slip.newCode')}</button>
            <button disabled={busy} onClick={() => void cancelSlip()} className="min-h-11 rounded-xl border border-line px-2 py-2 font-semibold leading-tight text-due">{t('slip.cancel')}</button>
          </div>
        </div>
      )}
    </Page>
  )
}

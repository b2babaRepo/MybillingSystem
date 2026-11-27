import { PaintQuote } from './PaintQuote'
import { NotFound } from '../components/NotFound'
import { btn } from '../lib/buttonStyles'
import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { confirmAsk } from '../lib/ask'
import { Page } from '../components/PrintPage'
import { api } from '../lib/api'
import { useCustomers } from '../lib/data'
import { formatRupees, indiaToday } from '../lib/format'
import type { BillPrint, Unit } from '../lib/types'
import { rupeesInWords } from '../lib/inWords'
import { navigate } from '../router'
import { DataTable } from '../components/DataTable'

export interface Estimate {
  id: string
  number: number
  customerId: string | null
  partyName: string | null
  lines: { productId: string | null; name: string; quantity: number; unit: Unit; unitPrice: number; amount: number }[]
  total: number
  validUntil: string
  note: string | null
  billId: string | null
  cancelledAt: string | null
  createdAt: string
  /** How much of each line has gone on delivery challans (same order). */
  delivered?: number[] | null
}

/** Where the bill screen picks up an estimate to bill: read once, then cleared. */
export const ESTIMATE_KEY = 'gk-estimate'

const day = (iso: string) => new Date(iso.length === 10 ? `${iso}T00:00:00` : iso).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric' })

/** Estimates (quotations): open ones to print, share or turn into a bill, and the ones already billed or cancelled. */
export function EstimatesScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const list = useQuery({ queryKey: [shopId, 'estimates'], queryFn: () => api<Estimate[]>('/v1/estimates') }).data ?? []
  const { customers } = useCustomers(shopId)
  const [message, setMessage] = useState('')
  const who = (e: Estimate) => customers.find((c) => c.id === e.customerId)?.name ?? e.partyName ?? t('hold.walkIn')
  const today = indiaToday()
  const state = (e: Estimate) => e.billId ? 'billed' : e.cancelledAt ? 'cancelled' : e.validUntil < today ? 'expired' : 'open'

  const [sending, setSending] = useState<Estimate | null>(null)
  const [quoting, setQuoting] = useState(false)
  const sent = (e: Estimate) => (e.delivered ?? []).reduce((a, q) => a + q, 0)
  const left = (e: Estimate) => e.lines.reduce((a, l, i) => a + Math.max(0, l.quantity - (e.delivered?.[i] ?? 0)), 0)

  function bill(e: Estimate) {
    try { sessionStorage.setItem(ESTIMATE_KEY, JSON.stringify(e)) } catch { /* no storage: nothing to carry */ }
    navigate('/bill')
  }
  async function cancel(e: Estimate) {
    if (!(await confirmAsk(t('estimate.cancelConfirm', { number: e.number })))) return
    try {
      await api(`/v1/estimates/${e.id}/cancel`, 'POST', {})
      await queryClient.invalidateQueries({ queryKey: [shopId, 'estimates'] })
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('error'))
    }
  }

  return (
    <div className="flex flex-col gap-3 p-4 pb-24 lg:mx-auto lg:w-full lg:max-w-5xl">
      <p className="text-sm text-muted">{t('estimate.hint')}</p>
      {quoting
        ? <PaintQuote shopId={shopId} onClose={() => setQuoting(false)} onSaved={async (n) => { setQuoting(false); setMessage(t('hwp7.saved', { number: n })); await queryClient.invalidateQueries({ queryKey: [shopId, 'estimates'] }) }} />
        : <button onClick={() => setQuoting(true)} className={`min-h-11 self-start rounded-xl px-4 ${btn.outline}`}>🎨 {t('hwp7.open')}</button>}
      {list.length === 0 && <p className="rounded-card bg-white p-4 text-sm text-muted">{t('estimate.none')}</p>}
      {list.length > 0 && (
        <div className="hidden lg:block">
          <DataTable label={t('estimate.link')} rows={list} rowKey={(e) => e.id} download="estimates" defaultSort={{ key: 'no', dir: -1 }}
            onRowClick={(e) => navigate(`/print/estimate/${e.id}`)}
            columns={[
              { key: 'no', header: t('estTable.no'), width: 'w-20', sortValue: (e) => e.number, render: (e) => <b>#{e.number}</b>, csv: (e) => e.number },
              { key: 'date', header: t('cashTable.date'), width: 'w-28', sortValue: (e) => e.createdAt, render: (e) => day(e.createdAt), csv: (e) => e.createdAt.slice(0, 10) },
              { key: 'who', header: t('chequeTable.customer'), sortValue: (e) => who(e), filter: { kind: 'text', value: (e) => who(e) }, render: (e) => who(e), csv: (e) => who(e) },
              { key: 'items', header: t('estTable.items'), align: 'right', width: 'w-20', sortValue: (e) => e.lines.length, render: (e) => e.lines.length, csv: (e) => e.lines.length },
              { key: 'total', header: t('estTable.total'), align: 'right', width: 'w-32', sortValue: (e) => e.total, render: (e) => <b>{formatRupees(e.total)}</b>, csv: (e) => e.total },
              { key: 'until', header: t('estTable.until'), width: 'w-28', sortValue: (e) => e.validUntil, render: (e) => day(e.validUntil), csv: (e) => e.validUntil },
              { key: 'state', header: t('chequeTable.status'), width: 'w-28', sortValue: (e) => state(e),
                filter: { kind: 'select', value: (e) => state(e), options: (['open', 'expired', 'billed', 'cancelled'] as const).map((st) => ({ value: st, label: t(`estimate.state.${st}`) })) },
                render: (e) => <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${state(e) === 'open' ? 'bg-brand-soft text-brand-strong' : 'bg-[#f1f5f4] text-muted'}`}>{t(`estimate.state.${state(e)}`)}</span>, csv: (e) => t(`estimate.state.${state(e)}`) },
              { key: 'act', header: '', width: 'w-48', render: (e) => (state(e) === 'open' || state(e) === 'expired') ? (
                <span className="flex justify-end gap-3" onClick={(ev) => ev.stopPropagation()}>
                  <button onClick={() => bill(e)} className="text-xs font-semibold text-brand underline">{t('estimate.makeBill')}</button>
                  {e.customerId && left(e) > 0 && <button onClick={() => setSending(e)} className="text-xs font-semibold text-brand underline">{t('hwp6.send')}</button>}
                  <button onClick={() => void cancel(e)} className={`min-h-9 text-xs ${btn.dangerOutline}`}>{t('estimate.cancel')}</button>
                </span>) : null },
            ]} />
        </div>
      )}
      {sending && <ChallanSheet estimate={sending} onClose={() => setSending(null)} onSent={async () => { setSending(null); setMessage(t('hwp6.sent')); await queryClient.invalidateQueries({ queryKey: [shopId] }) }} />}
      {message && <p className="rounded-xl bg-brand-soft p-3 text-sm font-semibold text-brand-deep">{message}</p>}
      {list.map((e) => (
        <section key={e.id} className="flex flex-col gap-1 rounded-card bg-white p-4 text-sm lg:hidden">
          <p className="flex items-center justify-between gap-2">
            <b className="text-base">{t('estimate.number', { number: e.number })} · {who(e)}</b>
            <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${state(e) === 'open' ? 'bg-brand-soft text-brand-strong' : 'bg-[#f1f5f4] text-muted'}`}>{t(`estimate.state.${state(e)}`)}</span>
          </p>
          <p className="text-muted">{t('estimate.line', { count: e.lines.length, total: formatRupees(e.total), date: day(e.createdAt), until: day(e.validUntil) })}</p>
          {sent(e) > 0 && <p className="text-xs font-semibold text-brand-strong">🚚 {left(e) > 0 ? t('hwp6.partly', { lines: e.lines.map((l, i) => `${l.name} ${e.delivered?.[i] ?? 0}/${l.quantity}`).join(' · ') }) : t('hwp6.all')}</p>}
          <div className="flex flex-wrap gap-3">
            <button onClick={() => navigate(`/print/estimate/${e.id}`)} className="min-h-11 font-semibold text-brand">{t('estimate.print')}</button>
            {(state(e) === 'open' || state(e) === 'expired') && <button onClick={() => bill(e)} className="min-h-11 font-semibold text-brand">{t('estimate.makeBill')}</button>}
            {(state(e) === 'open' || state(e) === 'expired') && e.customerId && left(e) > 0 && <button onClick={() => setSending(e)} className="min-h-11 font-semibold text-brand">{t('hwp6.send')}</button>}
            {(state(e) === 'open' || state(e) === 'expired') && <button onClick={() => void cancel(e)} className={`min-h-11 ${btn.dangerOutline}`}>{t('estimate.cancel')}</button>}
          </div>
        </section>
      ))}
      {message && <p className="text-sm text-due">{message}</p>}
    </div>
  )
}

/** The estimate on paper: plainly not a tax invoice, with how long the prices hold. */
export function EstimatePrint({ shopId, shopName, id }: { shopId: string; shopName: string; id: string }) {
  const { t } = useTranslation()
  const estimate = useQuery({ queryKey: [shopId, 'estimate', id], queryFn: () => api<Estimate>(`/v1/estimates/${id}`), retry: false })
  const e = estimate.data
  const { customers } = useCustomers(shopId)
  // The shop's logo, print style, terms and signature, as on its bills.
  const printLook = useQuery({ queryKey: ['print-look'], queryFn: () => api<BillPrint>('/v1/shop/print'), staleTime: 300_000, retry: false })
  if (estimate.isError) return <NotFound text={t('hwp2.estimateMissing')} back="/estimates" />
  if (!e) return <p className="p-6">…</p>
  const party = customers.find((c) => c.id === e.customerId)
  const share = [
    t('estimate.waTitle', { shop: shopName, number: e.number }),
    ...e.lines.map((l) => `• ${l.name} × ${l.quantity} @ ${formatRupees(l.unitPrice)} = ${formatRupees(l.amount)}`),
    t('estimate.waTotal', { total: formatRupees(e.total), until: day(e.validUntil) }),
  ].join('\n')
  const name = party?.name ?? e.partyName
  const look = printLook.data
  const sameUnit = e.lines.every((l) => l.unit === e.lines[0]?.unit)
  const totalQty = Math.round(e.lines.reduce((s, l) => s + l.quantity, 0) * 1000) / 1000
  // The same paper layout as a bill: who it is for and its details, the item table with a totals row, amount in words.
  return (
    <Page title={shopName} subtitle={t('estimate.printTitle', { number: e.number })} shareText={share} phone={party?.phone ?? null}
      logo={look?.logo} theme={look?.theme}>
      <div className="bill-parties mb-3 grid grid-cols-2 gap-4 text-sm">
        <div className="flex flex-col">
          <span className="text-xs font-bold uppercase tracking-wide text-gray-600">{t('print.billToHead')}</span>
          {name || party?.phone ? <>
            {name && <span className="text-base font-bold">{name}</span>}
            {party?.phone && <span>{party.phone}</span>}
          </> : <span>{t('print.walkIn')}</span>}
        </div>
        <div className="bill-details flex flex-col text-right">
          <span className="text-xs font-bold uppercase tracking-wide text-gray-600">{t('print.detailsHead')}</span>
          <span className="text-base font-bold">{t('estimate.number', { number: e.number })}</span>
          <span>{day(e.createdAt)}</span>
          <span>{t('estimate.valid', { until: day(e.validUntil) })}</span>
        </div>
      </div>
      <table className="bill-lines w-full border-collapse text-[15px]">
        <thead>
          <tr className="bill-head border-b border-black text-left">
            <th className="a4-only w-8 py-1.5">#</th>
            <th className="cell-item py-1.5">{t('print.item')}</th>
            <th className="text-right">{t('print.qty')}</th>
            <th className="cell-rate text-right">{t('print.rate')}</th>
            <th className="text-right">{t('print.amount')}</th>
          </tr>
        </thead>
        <tbody>
          {e.lines.map((l, i) => (
            <tr key={i} className="border-b border-gray-300 align-top">
              <td className="a4-only py-1.5 text-gray-600">{i + 1}</td>
              <td className="cell-item py-1.5 pr-2">{l.name}</td>
              <td className="py-1.5 text-right whitespace-nowrap">{l.quantity} {t(`units.${l.unit}`, { count: l.quantity })}</td>
              <td className="cell-rate py-1.5 text-right whitespace-nowrap">{formatRupees(l.unitPrice)}</td>
              <td className="py-1.5 text-right whitespace-nowrap">{formatRupees(l.amount)}</td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr className="border-b-2 border-black font-bold">
            <td className="a4-only" />
            <td className="cell-item py-1.5">{t('print.total')}</td>
            <td className="py-1.5 text-right whitespace-nowrap">{sameUnit && e.lines.length > 0 ? `${totalQty} ${t(`units.${e.lines[0].unit}`, { count: totalQty })}` : ''}</td>
            <td className="cell-rate" />
            <td className="py-1.5 text-right whitespace-nowrap">{formatRupees(e.total)}</td>
          </tr>
        </tfoot>
      </table>
      <div className="bill-sums mt-4 grid grid-cols-2 items-start gap-4">
        <div className="rounded-md bg-gray-100 p-2 text-sm">
          <p className="font-semibold">{t('print.inWords')}</p>
          <p>{rupeesInWords(e.total)}</p>
        </div>
        <div className="flex flex-col gap-1">
          <p className="print-total flex justify-between rounded-md bg-[#e6f2f0] px-2 py-1 text-xl font-extrabold"><span>{t('print.total')}</span><span>{formatRupees(e.total)}</span></p>
        </div>
      </div>
      {e.note && <p className="mt-3 text-sm">{e.note}</p>}
      {look?.terms && (
        <div className="mt-4 text-xs">
          <p className="font-bold">{t('print.terms')}</p>
          <p className="whitespace-pre-line">{look.terms}</p>
        </div>
      )}
      {look?.signature && (
        <div className="mt-6 flex flex-col items-end text-sm">
          <p>{t('print.forShop', { shop: shopName })}</p>
          <img src={look.signature} alt="" className="my-1 h-14 max-w-48 object-contain" />
          <p>{t('print.signatory')}</p>
        </div>
      )}
      <p className="mt-4 text-center text-xs text-gray-600">{t('estimate.notInvoice')}</p>
    </Page>
  )
}

/**
 * Send part or all of an estimate on a delivery challan: how much of each line goes now (what is left, to start), the
 * site and the vehicle. Stock leaves at once; the challans become one invoice later from the customer's page.
 */
function ChallanSheet({ estimate, onClose, onSent }: { estimate: Estimate; onClose: () => void; onSent: () => Promise<void> }) {
  const { t } = useTranslation()
  const leftOf = (i: number) => Math.max(0, estimate.lines[i].quantity - (estimate.delivered?.[i] ?? 0))
  const [qty, setQty] = useState(estimate.lines.map((_, i) => String(leftOf(i))))
  const [site, setSite] = useState('')
  const [vehicle, setVehicle] = useState('')
  const [error, setError] = useState('')
  const lines = estimate.lines.map((l, i) => ({ ...l, quantity: Number(qty[i]) || 0 })).filter((l) => l.quantity > 0)
  const bad = estimate.lines.some((_, i) => (Number(qty[i]) || 0) > leftOf(i))
  async function send() {
    setError('')
    try {
      await api('/v1/challans', 'POST', {
        id: crypto.randomUUID(), customerId: estimate.customerId, estimateId: estimate.id, site: site.trim() || null, vehicleNumber: vehicle.trim() || null,
        lines: lines.map((l) => ({ productId: l.productId, name: l.name, quantity: l.quantity, unit: l.unit, unitPrice: l.unitPrice })),
      })
      await onSent()
    } catch (e) {
      setError(e instanceof Error ? e.message : t('error'))
    }
  }
  return (
    <div role="dialog" aria-modal="true" aria-label={t('hwp6.title', { number: estimate.number })} className="fixed inset-0 z-40 flex items-end justify-center bg-black/40 sm:items-center" onClick={onClose}>
      <div className="flex max-h-[90dvh] w-full max-w-lg flex-col gap-3 overflow-y-auto rounded-t-3xl bg-white p-4 sm:rounded-3xl" onClick={(e) => e.stopPropagation()}>
        <h2 className="text-lg font-bold">{t('hwp6.title', { number: estimate.number })}</h2>
        <p className="text-xs text-muted">{t('hwp6.hint')}</p>
        {estimate.lines.map((l, i) => (
          <label key={i} className="flex items-center justify-between gap-2 text-sm">
            <span className="min-w-0 flex-1"><b>{l.name}</b><span className="block text-xs text-muted">{t('hwp6.leftOf', { left: leftOf(i), total: l.quantity })}</span></span>
            <input value={qty[i]} onChange={(e) => setQty(qty.map((q, k) => (k === i ? e.target.value.replace(/[^\d.]/g, '') : q)))} inputMode="decimal" aria-label={l.name}
              className={`h-11 w-24 rounded-xl border px-3 text-right ${(Number(qty[i]) || 0) > leftOf(i) ? 'border-due' : 'border-line'}`} />
          </label>
        ))}
        <input value={site} onChange={(e) => setSite(e.target.value)} maxLength={80} placeholder={t('trade.site')} aria-label={t('trade.site')} className="h-11 rounded-xl border border-line px-3" />
        <input value={vehicle} onChange={(e) => setVehicle(e.target.value)} maxLength={15} placeholder={t('trade.vehicle')} aria-label={t('trade.vehicle')} className="h-11 rounded-xl border border-line px-3" />
        {error && <p role="alert" className="text-sm text-due">{error}</p>}
        <div className="flex gap-2">
          <button onClick={onClose} className={`h-12 flex-1 rounded-xl ${btn.outline}`}>{t('cancel')}</button>
          <button disabled={lines.length === 0 || bad} onClick={() => void send()} className={`h-12 flex-1 rounded-xl ${btn.solid}`}>{t('hwp6.sendNow')}</button>
        </div>
      </div>
    </div>
  )
}

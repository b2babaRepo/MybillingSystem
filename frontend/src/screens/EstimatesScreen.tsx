import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { confirmAsk } from '../components/Ask'
import { Page } from '../components/PrintPage'
import { api } from '../lib/api'
import { useCustomers } from '../lib/data'
import { formatRupees, indiaToday } from '../lib/format'
import type { BillPrint, Unit } from '../lib/types'
import { rupeesInWords } from '../lib/inWords'
import { navigate } from '../router'

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
    <div className="flex flex-col gap-3 p-4 pb-24">
      <p className="text-sm text-muted">{t('estimate.hint')}</p>
      {list.length === 0 && <p className="rounded-card bg-white p-4 text-sm text-muted">{t('estimate.none')}</p>}
      {list.map((e) => (
        <section key={e.id} className="flex flex-col gap-1 rounded-card bg-white p-4 text-sm">
          <p className="flex items-center justify-between gap-2">
            <b className="text-base">{t('estimate.number', { number: e.number })} · {who(e)}</b>
            <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${state(e) === 'open' ? 'bg-brand-soft text-brand-strong' : 'bg-[#f1f5f4] text-muted'}`}>{t(`estimate.state.${state(e)}`)}</span>
          </p>
          <p className="text-muted">{t('estimate.line', { count: e.lines.length, total: formatRupees(e.total), date: day(e.createdAt), until: day(e.validUntil) })}</p>
          <div className="flex flex-wrap gap-3">
            <button onClick={() => navigate(`/print/estimate/${e.id}`)} className="min-h-11 font-semibold text-brand">{t('estimate.print')}</button>
            {(state(e) === 'open' || state(e) === 'expired') && <button onClick={() => bill(e)} className="min-h-11 font-semibold text-brand">{t('estimate.makeBill')}</button>}
            {(state(e) === 'open' || state(e) === 'expired') && <button onClick={() => void cancel(e)} className="min-h-11 text-due">{t('estimate.cancel')}</button>}
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
  const e = useQuery({ queryKey: [shopId, 'estimate', id], queryFn: () => api<Estimate>(`/v1/estimates/${id}`) }).data
  const { customers } = useCustomers(shopId)
  // The shop's logo, print style, terms and signature, as on its bills.
  const printLook = useQuery({ queryKey: ['print-look'], queryFn: () => api<BillPrint>('/v1/shop/print'), staleTime: 300_000, retry: false })
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

import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { confirmAsk } from '../components/Ask'
import { Page } from '../components/PrintPage'
import { api } from '../lib/api'
import { useCustomers } from '../lib/data'
import { formatRupees, indiaToday } from '../lib/format'
import type { Unit } from '../lib/types'
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
      {list.length === 0 && <p className="rounded-[18px] bg-white p-4 text-sm text-muted">{t('estimate.none')}</p>}
      {list.map((e) => (
        <section key={e.id} className="flex flex-col gap-1 rounded-[18px] bg-white p-4 text-sm">
          <p className="flex items-center justify-between gap-2">
            <b className="text-base">{t('estimate.number', { number: e.number })} · {who(e)}</b>
            <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${state(e) === 'open' ? 'bg-teal-50 text-teal-800' : 'bg-[#f1f5f4] text-muted'}`}>{t(`estimate.state.${state(e)}`)}</span>
          </p>
          <p className="text-muted">{t('estimate.line', { count: e.lines.length, total: formatRupees(e.total), date: day(e.createdAt), until: day(e.validUntil) })}</p>
          <div className="flex flex-wrap gap-3">
            <button onClick={() => navigate(`/print/estimate/${e.id}`)} className="min-h-11 font-semibold text-teal-700">{t('estimate.print')}</button>
            {(state(e) === 'open' || state(e) === 'expired') && <button onClick={() => bill(e)} className="min-h-11 font-semibold text-teal-700">{t('estimate.makeBill')}</button>}
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
  if (!e) return <p className="p-6">…</p>
  const party = customers.find((c) => c.id === e.customerId)
  const share = [
    t('estimate.waTitle', { shop: shopName, number: e.number }),
    ...e.lines.map((l) => `• ${l.name} × ${l.quantity} @ ${formatRupees(l.unitPrice)} = ${formatRupees(l.amount)}`),
    t('estimate.waTotal', { total: formatRupees(e.total), until: day(e.validUntil) }),
  ].join('\n')
  return (
    <Page title={shopName} subtitle={`${t('estimate.printTitle', { number: e.number })} · ${day(e.createdAt)}`} shareText={share} phone={party?.phone ?? null}>
      {(party?.name ?? e.partyName) && <p className="mb-2 text-sm">{t('estimate.to', { name: party?.name ?? e.partyName })}</p>}
      <table className="bill-lines w-full border-collapse text-[14px]">
        <thead><tr className="border-b border-black text-left"><th className="py-1">{t('print.item')}</th><th className="text-right">{t('print.qty')}</th><th className="text-right">{t('print.rate')}</th><th className="text-right">{t('print.amount')}</th></tr></thead>
        <tbody>
          {e.lines.map((l, i) => (
            <tr key={i} className="border-b border-gray-300">
              <td className="py-1">{l.name}</td>
              <td className="text-right">{l.quantity}</td>
              <td className="text-right">{formatRupees(l.unitPrice)}</td>
              <td className="text-right">{formatRupees(l.amount)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="mt-3 flex justify-between border-t-2 border-black pt-2 text-lg font-extrabold"><span>{t('print.total')}</span><span>{formatRupees(e.total)}</span></p>
      <p className="mt-2 text-sm">{t('estimate.valid', { until: day(e.validUntil) })}</p>
      {e.note && <p className="mt-1 text-sm">{e.note}</p>}
      <p className="mt-3 text-xs text-gray-600">{t('estimate.notInvoice')}</p>
    </Page>
  )
}

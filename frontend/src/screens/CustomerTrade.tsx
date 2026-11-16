import { GST_STATES } from '../lib/states'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { uuidv7 } from '../lib/ids'
import { formatDateTime, formatRupees } from '../lib/format'
import type { Customer, PaymentMode } from '../lib/types'
import { navigate } from '../router'
import { ask } from '../components/Ask'

interface SiteTotal { site: string; bills: number; billed: number; openChallans: number }
interface Challan { id: string; occurredAt: string; total: number; site: string | null; vehicleNumber: string | null; lines: { name: string; quantity: number; unit: string }[] }

/**
 * A contractor's extras on their khata (building-material shops): the credit limit, what each site has been billed,
 * and delivery challans still to bill, which the shop ticks and makes into one invoice.
 */
export function CustomerTradePanel({ shopId, customer, isOwner }: { shopId: string; customer: Customer; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const sites = useQuery({ queryKey: [shopId, 'sites', customer.id], queryFn: () => api<SiteTotal[]>(`/v1/customers/${customer.id}/sites`) }).data ?? []
  const challans = useQuery({ queryKey: [shopId, 'challans', customer.id], queryFn: () => api<Challan[]>(`/v1/challans?customerId=${customer.id}`) }).data ?? []
  const details = useQuery({ queryKey: [shopId, 'bill-details', customer.id], queryFn: () => api<{ gstin: string | null; address: string | null; state: string | null }>(`/v1/customers/${customer.id}/bill-details`) }).data
  const [limit, setLimit] = useState<string | null>(null)
  const [picked, setPicked] = useState<string[]>([])
  const [paid, setPaid] = useState('0')
  const [mode, setMode] = useState<PaymentMode>('cash')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  // The saved state while it's being changed; null when the list is shut.
  const [stateCode, setStateCode] = useState<string | null>(null)
  const pickedTotal = challans.filter((c) => picked.includes(c.id)).reduce((sum, c) => sum + c.total, 0)

  async function run(action: () => Promise<void>) {
    setBusy(true)
    setMessage(null)
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  const setGstin = () => run(async () => {
    const current = (await api<{ gstin: string | null }>(`/v1/customers/${customer.id}/gstin`)).gstin ?? ''
    const gstin = (await ask(t('trade.gstinAsk'), current))
    if (gstin === null) return
    await api(`/v1/customers/${customer.id}/gstin`, 'PUT', { gstin: gstin.trim() || null })
    setMessage(t('trade.gstinSaved'))
  })

  const openState = () => run(async () => {
    setStateCode((await api<{ state: string | null }>(`/v1/customers/${customer.id}/bill-details`)).state ?? '')
  })

  const saveState = (state: string) => run(async () => {
    await api(`/v1/customers/${customer.id}/state`, 'PUT', { state: state || null })
    setStateCode(null)
    setMessage(t('trade.stateSaved'))
  })

  const setAddress = () => run(async () => {
    const current = (await api<{ address: string | null }>(`/v1/customers/${customer.id}/address`)).address ?? ''
    const address = await ask(t('trade.addressAsk'), current)
    if (address === null) return
    await api(`/v1/customers/${customer.id}/address`, 'PUT', { address: address.trim() || null })
    setMessage(t('trade.addressSaved'))
  })

  const saveLimit = () => run(async () => {
    await api(`/v1/customers/${customer.id}/credit-limit`, 'PUT', { creditLimit: limit ? Number(limit) : null })
    setLimit(null)
  })

  const invoice = () => run(async () => {
    const bill = await api<{ id: string }>('/v1/challans/invoice', 'POST', { id: uuidv7(), challanIds: picked, paidAmount: Number(paid) || 0, paymentMode: mode })
    setPicked([])
    navigate(`/print/bill/${bill.id}`)
  })

  const over = customer.creditLimit ? customer.balance > customer.creditLimit : false
  const used = customer.creditLimit ? Math.min(100, Math.max(0, (customer.balance / customer.creditLimit) * 100)) : 0
  // One row of bill details: icon, label, what is saved (or "Not added"), and a chevron when the owner can change it.
  const row = (icon: string, label: string, value: string | null | undefined, onClick: () => void) => {
    const body = (
      <>
        <span aria-hidden className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-[#edf2f1] text-lg">{icon}</span>
        <span className="flex min-w-0 flex-1 flex-col text-left">
          <span className="text-sm text-muted">{label}</span>
          <span className={`truncate font-semibold ${value ? '' : 'text-muted/70 font-normal italic'}`}>{value || t('trade.notAdded')}</span>
        </span>
        {isOwner && <span aria-hidden className="text-xl text-muted">›</span>}
      </>
    )
    return isOwner
      ? <button key={label} onClick={onClick} className="flex min-h-16 w-full items-center gap-3 px-3 py-2 hover:bg-[#f6faf9] active:bg-[#edf2f1]">{body}</button>
      : <div key={label} className="flex min-h-16 items-center gap-3 px-3 py-2">{body}</div>
  }

  return (
    <section className="flex flex-col gap-3 rounded-[18px] bg-white p-4">
      {/* Credit limit, with how much of it is used */}
      <div className={`flex flex-col gap-2 rounded-2xl border p-3 ${over ? 'border-red-200 bg-red-50/60' : 'border-line'}`}>
        <div className="flex items-center justify-between gap-2">
          <span className="flex items-center gap-3">
            <span aria-hidden className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-50 text-lg">🛡️</span>
            <span className="flex flex-col">
              <span className="text-sm text-muted">{t('trade.creditLimit')}</span>
              <span className={`text-lg font-bold ${over ? 'text-due' : ''}`}>
                {customer.creditLimit ? formatRupees(customer.creditLimit) : t('trade.noLimit')}{over ? ` · ${t('trade.crossed')}` : ''}
              </span>
            </span>
          </span>
          {isOwner && limit === null && (
            <button onClick={() => setLimit(customer.creditLimit ? String(customer.creditLimit) : '')} className="min-h-10 rounded-full border border-teal-700/30 bg-teal-50/60 px-4 text-sm font-semibold text-teal-800 hover:bg-teal-50">{t('trade.setLimit')}</button>
          )}
        </div>
        {customer.creditLimit ? (
          <div className="flex flex-col gap-1">
            <div className="h-2 overflow-hidden rounded-full bg-[#edf2f1]" role="meter" aria-valuemin={0} aria-valuemax={customer.creditLimit} aria-valuenow={Math.max(0, customer.balance)} aria-label={t('trade.creditLimit')}>
              <div className={`h-full rounded-full ${over ? 'bg-due' : used > 80 ? 'bg-amber-500' : 'bg-teal-600'}`} style={{ width: `${over ? 100 : used}%` }} />
            </div>
            <span className="text-xs text-muted">{formatRupees(Math.max(0, customer.balance))} / {formatRupees(customer.creditLimit)}</span>
          </div>
        ) : null}
      </div>
      {limit !== null && (
        <div className="flex gap-2">
          <label className="sr-only" htmlFor="credit-limit">{t('trade.creditLimit')}</label>
          <input id="credit-limit" value={limit} onChange={(e) => setLimit(e.target.value.replace(/[^\d]/g, ''))} inputMode="numeric" placeholder="₹"
            className="h-12 min-w-0 flex-1 rounded-xl border border-line px-3 text-lg font-bold" />
          <button disabled={busy} onClick={() => void saveLimit()} className="h-12 rounded-xl bg-teal-700 px-4 font-bold text-white">{t('trade.save')}</button>
        </div>
      )}

      {/* Bill details: what prints on this customer's GST bills */}
      <div className="flex flex-col">
        <h2 className="mb-1 text-sm font-bold uppercase tracking-wide text-muted">{t('trade.detailsTitle')}</h2>
        <div className="divide-y divide-line overflow-hidden rounded-2xl border border-line">
          {row('🧾', t('trade.gstinSet'), details?.gstin, () => void setGstin())}
          {row('📍', t('trade.addressSet'), details?.address, () => void setAddress())}
          {stateCode === null && row('🗺️', t('trade.stateSet'), details?.state ? GST_STATES[details.state] ?? details.state : null, () => void openState())}
        </div>
      </div>
      {stateCode !== null && (
        <select aria-label={t('trade.stateSet')} value={stateCode} disabled={busy} onChange={(e) => void saveState(e.target.value)} className="h-12 rounded-xl border border-line bg-white px-2">
          <option value="">{t('trade.noPainter')}</option>
          {Object.entries(GST_STATES).map(([code, name]) => <option key={code} value={code}>{name}</option>)}
        </select>
      )}

      {/* Sites */}
      {sites.some((s) => s.site) && (
        <div className="flex flex-col gap-1">
          <h2 className="font-bold">{t('trade.sites')}</h2>
          {sites.map((s) => (
            <div key={s.site} className="flex justify-between text-[15px]">
              <span>{s.site || t('trade.noSite')}{s.openChallans > 0 ? ` · ${t('trade.openChallans', { count: s.openChallans })}` : ''}</span>
              <span className="font-semibold">{formatRupees(s.billed)}</span>
            </div>
          ))}
        </div>
      )}

      {/* Open challans → one invoice */}
      {challans.length > 0 && (
        <div className="flex flex-col gap-2">
          <h2 className="font-bold">{t('trade.challansTitle')}</h2>
          {challans.map((c) => (
            <label key={c.id} className="flex min-h-12 items-start gap-3 rounded-xl border border-line p-2">
              <input type="checkbox" className="mt-1 h-5 w-5" checked={picked.includes(c.id)}
                onChange={(e) => setPicked(e.target.checked ? [...picked, c.id] : picked.filter((id) => id !== c.id))} />
              <span className="flex min-w-0 flex-1 flex-col text-sm">
                <span className="font-semibold">{formatDateTime(c.occurredAt)}{c.site ? ` · ${c.site}` : ''}</span>
                <span className="truncate text-muted">{c.lines.map((l) => `${l.name} ${l.quantity} ${t(`units.${l.unit}`, { count: l.quantity })}`).join(', ')}</span>
                {c.vehicleNumber && <span className="text-xs text-muted">{c.vehicleNumber}</span>}
              </span>
              <span className="font-bold">{formatRupees(c.total)}</span>
            </label>
          ))}
          {picked.length > 0 && (
            <div className="flex flex-col gap-2 rounded-xl bg-ground p-3">
              <div className="flex items-center justify-between gap-2">
                <label htmlFor="invoice-paid" className="text-sm font-semibold">{t('paidNow')}</label>
                <input id="invoice-paid" value={paid} onChange={(e) => setPaid(e.target.value.replace(/[^\d.]/g, ''))} inputMode="decimal"
                  className="h-11 w-32 rounded-xl border border-line bg-white px-3 text-right font-bold" />
              </div>
              <div className="flex gap-2 text-sm">
                {(['cash', 'upi'] as const).map((m) => (
                  <button key={m} onClick={() => setMode(m)} className={`h-11 rounded-xl px-4 font-semibold ${mode === m ? 'bg-teal-700 text-white shadow-sm' : 'border border-line bg-white'}`}>{t(m)}</button>
                ))}
              </div>
              <button disabled={busy} onClick={() => void invoice()} className="h-12 rounded-2xl bg-teal-700 font-bold text-white">
                {t('trade.makeInvoice', { count: picked.length, total: formatRupees(pickedTotal) })}
              </button>
            </div>
          )}
        </div>
      )}
      {message && <p className="text-sm text-due">{message}</p>}
    </section>
  )
}

import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { uuidv7 } from '../lib/ids'
import { formatDateTime, formatRupees } from '../lib/format'
import type { Customer, PaymentMode } from '../lib/types'
import { navigate } from '../router'

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
  const [limit, setLimit] = useState<string | null>(null)
  const [picked, setPicked] = useState<string[]>([])
  const [paid, setPaid] = useState('0')
  const [mode, setMode] = useState<PaymentMode>('cash')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
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
    const gstin = window.prompt(t('trade.gstinAsk'), current)
    if (gstin === null) return
    await api(`/v1/customers/${customer.id}/gstin`, 'PUT', { gstin: gstin.trim() || null })
    setMessage(t('trade.gstinSaved'))
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

  return (
    <section className="flex flex-col gap-3 rounded-[18px] bg-white p-4">
      {/* Credit limit */}
      <div className="flex items-center justify-between gap-2">
        <span className="flex flex-col">
          <span className="text-sm text-muted">{t('trade.creditLimit')}</span>
          <span className={`text-lg font-bold ${over ? 'text-due' : ''}`}>
            {customer.creditLimit ? formatRupees(customer.creditLimit) : t('trade.noLimit')}{over ? ` · ${t('trade.crossed')}` : ''}
          </span>
        </span>
        {isOwner && limit === null && (
          <button onClick={() => setLimit(customer.creditLimit ? String(customer.creditLimit) : '')} className="min-h-11 px-2 text-sm text-teal-700 underline">{t('trade.setLimit')}</button>
        )}
      </div>
      {limit !== null && (
        <div className="flex gap-2">
          <label className="sr-only" htmlFor="credit-limit">{t('trade.creditLimit')}</label>
          <input id="credit-limit" value={limit} onChange={(e) => setLimit(e.target.value.replace(/[^\d]/g, ''))} inputMode="numeric" placeholder="₹"
            className="h-12 min-w-0 flex-1 rounded-xl border border-line px-3 text-lg font-bold" />
          <button disabled={busy} onClick={() => void saveLimit()} className="h-12 rounded-xl bg-teal-700 px-4 font-bold text-white">{t('trade.save')}</button>
        </div>
      )}

      {isOwner && <button onClick={() => void setGstin()} className="min-h-11 self-start text-sm text-teal-700 underline">{t('trade.gstinSet')}</button>}

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
                <span className="truncate text-muted">{c.lines.map((l) => `${l.name} ${l.quantity} ${t(`units.${l.unit}`)}`).join(', ')}</span>
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
                  <button key={m} onClick={() => setMode(m)} className={`h-11 rounded-xl px-4 font-semibold ${mode === m ? 'bg-ink text-white' : 'border border-line bg-white'}`}>{t(m)}</button>
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

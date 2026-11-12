import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'

export type Split = 'value' | 'quantity' | 'bags'
export interface Charge { label: string; amount: string; paidBy: '' | 'cash' | 'upi' }
export interface Charges { rows: Charge[]; split: Split; extraInCost: boolean }
export const noCharges: Charges = { rows: [], split: 'value', extraInCost: false }

export interface LotLine { productId: string; name: string; units: number; billRate: number; chargePerUnit: number; landedCost: number; sellingPrice: number | null; marginPerUnit: number | null; marginPercent: number | null; lotMargin: number | null; sold: number; revenue: number; profit: number; left: number }
export interface Lot { id: string; billNo: string | null; supplier: string; occurredAt: string; charges: number; chargeLines: { label: string; amount: number; paid: boolean }[]; landedTotal: number; expectedMargin: number | null; revenue: number; profit: number; lines: LotLine[] }

const input = 'h-11 min-w-0 rounded-xl border border-line bg-white px-3'
const PRESETS = ['freight', 'hamali', 'commission']
const rows = (c: Charge[]) => c.filter((r) => r.label.trim() && Number(r.amount) > 0).map((r) => ({ label: r.label.trim(), amount: Number(r.amount), paidBy: r.paidBy || null }))

/** The body a bill sends: only the charges filled in, and how to spread them. */
export const chargesBody = (c: Charges) => ({ charges: rows(c.rows), split: c.split, extraInCost: c.extraInCost })

/**
 * Charges to bring the goods in that aren't on the supplier's bill (lorry freight, hamali, commission): they go into the
 * items' cost, spread by value, units or bags. One paid now in cash or UPI is also written in the kharcha.
 */
export function ChargesEditor({ value, onChange, hasExtra, open = false }: { value: Charges; onChange: (c: Charges) => void; hasExtra: boolean; open?: boolean }) {
  const { t } = useTranslation()
  const set = (i: number, next: Partial<Charge>) => onChange({ ...value, rows: value.rows.map((r, j) => (j === i ? { ...r, ...next } : r)) })
  const add = (label: string) => onChange({ ...value, rows: [...value.rows, { label, amount: '', paidBy: '' }] })
  const pool = value.rows.reduce((s, r) => s + (Number(r.amount) || 0), 0)
  return (
    <details className="rounded-xl border border-line bg-white p-3" open={open || value.rows.length > 0 || value.extraInCost}>
      <summary className="cursor-pointer font-semibold">{t('landed.title')}{pool > 0 ? ` · ${formatRupees(pool)}` : ''}</summary>
      <p className="mt-1 text-xs text-muted">{t('landed.hint')}</p>
      <div className="mt-2 flex flex-col gap-2">
        {value.rows.map((r, i) => (
          <div key={i} className="grid grid-cols-[1fr_5.5rem_5.5rem] gap-2">
            <input aria-label={t('landed.label')} placeholder={t('landed.label')} value={r.label} maxLength={60} onChange={(e) => set(i, { label: e.target.value })} className={input} />
            <input aria-label={t('landed.amount')} placeholder="₹" value={r.amount} onChange={(e) => set(i, { amount: e.target.value })} inputMode="decimal" className={input} />
            <select aria-label={t('landed.paidBy')} value={r.paidBy} onChange={(e) => set(i, { paidBy: e.target.value as Charge['paidBy'] })} className={input}>
              <option value="">{t('landed.notPaid')}</option><option value="cash">{t('landed.cash')}</option><option value="upi">UPI</option>
            </select>
          </div>
        ))}
        <div className="flex flex-wrap gap-2 text-sm">
          {PRESETS.map((p) => <button key={p} onClick={() => add(t(`landed.preset.${p}`))} className="min-h-10 rounded-full border border-line px-3 font-semibold">+ {t(`landed.preset.${p}`)}</button>)}
          <button onClick={() => add('')} className="min-h-10 rounded-full border border-line px-3 font-semibold">+ {t('landed.other')}</button>
        </div>
        {hasExtra && (
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={value.extraInCost} onChange={(e) => onChange({ ...value, extraInCost: e.target.checked })} className="h-5 w-5" />{t('landed.extraInCost')}</label>
        )}
        {(pool > 0 || value.extraInCost) && (
          <label className="flex items-center justify-between gap-2 text-sm">{t('landed.split')}
            <select value={value.split} onChange={(e) => onChange({ ...value, split: e.target.value as Split })} className={input}>
              {(['value', 'quantity', 'bags'] as const).map((s) => <option key={s} value={s}>{t(`landed.by.${s}`)}</option>)}
            </select>
          </label>
        )}
      </div>
    </details>
  )
}

/**
 * A saved bill as a lot: each item's bill rate, its share of charges, what it landed at, and the margin at today's price;
 * what of it has really sold (oldest lot first) and the profit on that; and charges that came later can still be added.
 */
export function LotView({ shopId, purchaseId, isOwner = true }: { shopId: string; purchaseId: string; isOwner?: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const lot = useQuery({ queryKey: [shopId, 'suppliers', 'lot', purchaseId], queryFn: () => api<Lot>(`/v1/suppliers/purchases/${purchaseId}/landed`) }).data
  const [later, setLater] = useState<Charges | null>(null)
  const [message, setMessage] = useState('')
  if (!lot) return null
  const addLater = async () => {
    setMessage('')
    try {
      await api(`/v1/suppliers/purchases/${purchaseId}/charges`, 'POST', { charges: rows(later!.rows), split: later!.split })
      setLater(null)
      await queryClient.invalidateQueries({ queryKey: [shopId, 'suppliers'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  return (
    <div className="mt-2 flex flex-col gap-1 border-t border-line pt-2">
      <p className="font-semibold">{t('landed.lotTitle')}{lot.charges > 0 ? ` · ${t('landed.chargesIn', { amount: formatRupees(lot.charges) })}` : ''}</p>
      {lot.chargeLines.map((c, i) => <p key={i} className="text-muted">{c.label} {formatRupees(c.amount)}{c.paid ? ` · ${t('landed.inKharcha')}` : ''}</p>)}
      <LotTable lines={lot.lines} />
      <p className="flex flex-wrap justify-between gap-x-3 font-semibold">
        <span>{t('landed.total', { amount: formatRupees(lot.landedTotal) })}</span>
        {lot.revenue > 0 && <span className={lot.profit < 0 ? 'text-due' : 'text-teal-800'}>{t('landed.soldProfit', { revenue: formatRupees(lot.revenue), profit: formatRupees(lot.profit) })}</span>}
        {lot.expectedMargin != null && <span className="text-muted">{t('landed.expected', { amount: formatRupees(lot.expectedMargin) })}</span>}
      </p>
      {isOwner && !later && <button onClick={() => setLater({ ...noCharges, rows: [{ label: t('landed.preset.freight'), amount: '', paidBy: '' }] })} className="self-start text-xs font-semibold text-teal-700">+ {t('landed.addLater')}</button>}
      {later && (
        <div className="flex flex-col gap-2">
          <ChargesEditor value={later} onChange={setLater} hasExtra={false} open />
          <div className="grid grid-cols-2 gap-2">
            <button disabled={rows(later.rows).length === 0} onClick={() => void addLater()} className="min-h-11 rounded-xl bg-teal-700 font-semibold text-white disabled:opacity-50">{t('landed.saveLater')}</button>
            <button onClick={() => setLater(null)} className="min-h-11 rounded-xl border border-line font-semibold">{t('cancel')}</button>
          </div>
        </div>
      )}
      {message && <p className="text-due">{message}</p>}
    </div>
  )
}

/** A lot's items: bill rate, charges, landed cost, today's price and margin, and what has really sold with its profit. */
export function LotTable({ lines, showName = true }: { lines: LotLine[]; showName?: boolean }) {
  const { t } = useTranslation()
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-xs">
        <thead><tr className="text-left text-muted">
          {showName && <th className="pr-2">{t('landed.item')}</th>}
          <th className="px-1 text-right">{t('landed.billRate')}</th><th className="px-1 text-right">+</th><th className="px-1 text-right">{t('landed.landed')}</th>
          <th className="px-1 text-right">{t('landed.sells')}</th><th className="px-1 text-right">{t('landed.margin')}</th>
          <th className="px-1 text-right">{t('landed.sold')}</th><th className="pl-1 text-right">{t('landed.profit')}</th>
        </tr></thead>
        <tbody>
          {lines.map((l) => (
            <tr key={l.productId} className="border-t border-line">
              {showName && <td className="py-1 pr-2">{l.name} × {l.units}</td>}
              <td className="px-1 text-right tabular-nums">{l.billRate}</td>
              <td className="px-1 text-right tabular-nums text-muted">{l.chargePerUnit ? l.chargePerUnit : '–'}</td>
              <td className="px-1 text-right font-semibold tabular-nums">{l.landedCost}</td>
              <td className="px-1 text-right tabular-nums">{l.sellingPrice ?? '–'}</td>
              <td className={`px-1 text-right tabular-nums ${l.marginPerUnit != null && l.marginPerUnit < 0 ? 'font-semibold text-due' : ''}`}>{l.marginPercent != null ? `${l.marginPercent}%` : '–'}</td>
              <td className="px-1 text-right tabular-nums">{l.sold}{l.left > 0 ? <span className="block text-muted">{t('landed.left', { qty: l.left })}</span> : null}</td>
              <td className={`pl-1 text-right tabular-nums ${l.profit < 0 ? 'text-due' : ''}`}>{l.sold > 0 ? formatRupees(l.profit) : '–'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

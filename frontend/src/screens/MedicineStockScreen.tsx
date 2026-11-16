import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'

interface Batch { id: string; batchNo: string; expiry: string; mrp: number; costPrice: number | null; quantity: number; expired: boolean; daysLeft: number }
interface Expiring { id: string; productName: string; batchNo: string; expiry: string; quantity: number; value: number; expired: boolean; daysLeft: number }

const month = (iso: string) => new Date(`${iso}T00:00`).toLocaleDateString(shownLocale(), { month: 'short', year: 'numeric' })
const select = 'h-12 w-full rounded-xl border border-line bg-white px-3'

/** Medicines by batch: receive a batch (batch no., expiry, MRP, cost, free units), see each medicine's batches, and what is expiring. */
export function MedicineStockScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const [days, setDays] = useState(90)
  const expiring = useQuery({ queryKey: [shopId, 'batches', 'expiring', days], queryFn: () => api<Expiring[]>(`/v1/batches/expiring?days=${days}`) }).data ?? []
  const lost = expiring.filter((e) => e.expired).reduce((s, e) => s + e.value, 0)
  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <Receive shopId={shopId} />
      <section className="flex flex-col gap-2 rounded-card bg-white p-3">
        <div className="flex items-center justify-between gap-2">
          <h3 className="font-bold">{t('batches.expiring')}</h3>
          <select value={days} onChange={(e) => setDays(Number(e.target.value))} aria-label={t('batches.within')} className="h-10 rounded-xl border border-line bg-white px-2 text-sm">
            {[30, 90, 180].map((d) => <option key={d} value={d}>{t('batches.days', { count: d })}</option>)}
          </select>
        </div>
        <p className="text-xs text-muted">{t('batches.expiringHint')}</p>
        {lost > 0 && <p className="rounded-xl bg-due-soft p-2 text-sm font-semibold text-due">{t('batches.expiredValue', { value: formatRupees(lost) })}</p>}
        {expiring.length === 0 && <p className="text-sm text-muted">{t('batches.noneExpiring')}</p>}
        {expiring.map((e) => (
          <div key={e.id} className={`flex justify-between gap-2 rounded-xl p-2 text-sm ${e.expired ? 'bg-due-soft' : e.daysLeft <= 30 ? 'bg-warn-soft' : 'bg-ground'}`}>
            <span><b>{e.productName}</b><span className="block text-xs">{t('batches.batchLine', { batch: e.batchNo, month: month(e.expiry), qty: e.quantity })} · {e.expired ? t('batches.expired') : t('batches.daysLeft', { count: e.daysLeft })}</span></span>
            <span className="shrink-0 font-semibold">{formatRupees(e.value)}</span>
          </div>
        ))}
      </section>
    </div>
  )
}

function Receive({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const medicines = useProducts(shopId).products.filter((p) => !p.isService)
  const [productId, setProductId] = useState('')
  const batches = useQuery({ queryKey: [shopId, 'batches', productId], enabled: !!productId, queryFn: () => api<Batch[]>(`/v1/products/${productId}/batches`) }).data ?? []
  const blank = { batchNo: '', expiry: '', mrp: '', cost: '', quantity: '', free: '' }
  const [d, setD] = useState(blank)
  const [message, setMessage] = useState('')
  const set = (key: keyof typeof d) => (e: { target: { value: string } }) => setD({ ...d, [key]: e.target.value })
  const paid = Number(d.quantity) || 0
  const free = Number(d.free) || 0
  const realCost = Number(d.cost) > 0 && paid > 0 ? (Number(d.cost) * paid) / (paid + free) : null

  async function save() {
    setMessage('')
    try {
      await api(`/v1/products/${productId}/batches`, 'POST', {
        id: crypto.randomUUID(), batchNo: d.batchNo.trim(), expiryMonth: d.expiry, mrp: Number(d.mrp) || 0, costPrice: d.cost ? Number(d.cost) : null, quantity: paid, freeQuantity: free,
      })
      setD(blank)
      setMessage(t('batches.received'))
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  return (
    <section className="flex flex-col gap-2 rounded-card bg-white p-3">
      <h3 className="font-bold">{t('batches.receive')}</h3>
      <select value={productId} onChange={(e) => setProductId(e.target.value)} aria-label={t('batches.medicine')} className={select}>
        <option value="">{t('batches.chooseMedicine')}</option>{medicines.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
      </select>
      {productId && (
        <>
          {batches.map((b) => (
            <p key={b.id} className={`flex justify-between text-sm ${b.expired ? 'text-due' : ''}`}>
              <span>{t('batches.batchLine', { batch: b.batchNo, month: month(b.expiry), qty: b.quantity })}{b.expired ? ` · ${t('batches.expired')}` : ''}</span><span>MRP {formatRupees(b.mrp)}</span>
            </p>
          ))}
          <div className="grid grid-cols-2 gap-2">
            <Field label={t('batches.batchNo')} value={d.batchNo} onChange={set('batchNo')} />
            <Field label={t('batches.expiry')} type="month" value={d.expiry} onChange={set('expiry')} />
            <Field label="MRP" value={d.mrp} onChange={set('mrp')} inputMode="decimal" />
            <Field label={t('batches.cost')} value={d.cost} onChange={set('cost')} inputMode="decimal" />
            <Field label={t('batches.quantity')} value={d.quantity} onChange={set('quantity')} inputMode="decimal" />
            <Field label={t('batches.free')} value={d.free} onChange={set('free')} inputMode="decimal" placeholder="0" />
          </div>
          {realCost !== null && free > 0 && <p className="text-xs text-muted">{t('batches.realCost', { cost: formatRupees(Math.round(realCost * 100) / 100) })}</p>}
          <Button disabled={!d.batchNo.trim() || !d.expiry || !(Number(d.mrp) > 0) || !(paid > 0)} onClick={() => void save()}>{t('batches.save')}</Button>
        </>
      )}
      {message && <p className="text-sm font-semibold text-brand-deep">{message}</p>}
    </section>
  )
}

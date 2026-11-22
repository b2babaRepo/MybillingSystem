import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'
import { DataTable } from '../components/DataTable'

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
    <div className="flex flex-col gap-3 p-4 pb-28 lg:mx-auto lg:w-full lg:max-w-5xl">
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
        {expiring.length > 0 && (
          <div className="hidden lg:block">
            <DataTable label={t('batches.expiring')} rows={expiring} rowKey={(e) => e.id} download={`expiring-${days}-days`} defaultSort={{ key: 'expiry', dir: 1 }}
              columns={[
                { key: 'name', header: t('batches.medicine'), sortValue: (e) => e.productName, filter: { kind: 'text', value: (e) => e.productName }, render: (e) => <span className="font-semibold">{e.productName}</span>, csv: (e) => e.productName },
                { key: 'batch', header: t('batches.batchNo'), width: 'w-32', sortValue: (e) => e.batchNo, filter: { kind: 'text', value: (e) => e.batchNo }, render: (e) => <span className="font-mono text-xs">{e.batchNo}</span>, csv: (e) => e.batchNo },
                { key: 'expiry', header: t('batches.expiry'), width: 'w-28', sortValue: (e) => e.expiry, render: (e) => month(e.expiry), csv: (e) => e.expiry },
                { key: 'qty', header: t('batches.quantity'), align: 'right', width: 'w-20', sortValue: (e) => e.quantity, render: (e) => e.quantity, csv: (e) => e.quantity },
                { key: 'value', header: t('stockTable.value'), align: 'right', width: 'w-28', sortValue: (e) => e.value, render: (e) => <b>{formatRupees(e.value)}</b>, csv: (e) => e.value },
                { key: 'left', header: t('reorderTable.daysLeft'), width: 'w-36', sortValue: (e) => e.daysLeft,
                  filter: { kind: 'select', value: (e) => (e.expired ? 'expired' : e.daysLeft <= 30 ? 'soon' : 'later'), options: (['expired', 'soon', 'later'] as const).map((k) => ({ value: k, label: t(`medTable.${k}`) })) },
                  render: (e) => <span className={`rounded-full px-2 py-0.5 text-xs font-bold ${e.expired ? 'bg-due-soft text-due' : e.daysLeft <= 30 ? 'bg-warn-soft text-warn' : 'bg-[#edf2f1] text-muted'}`}>{e.expired ? t('batches.expired') : t('batches.daysLeft', { count: e.daysLeft })}</span>, csv: (e) => (e.expired ? -1 : e.daysLeft) },
              ]}
              footer={[t('supTable.items', { count: expiring.length }), '', '', String(expiring.reduce((n, e) => n + e.quantity, 0)), formatRupees(expiring.reduce((n, e) => n + e.value, 0)), '']} />
          </div>
        )}
        {expiring.map((e) => (
          <div key={e.id} className={`lg:hidden flex justify-between gap-2 rounded-xl p-2 text-sm ${e.expired ? 'bg-due-soft' : e.daysLeft <= 30 ? 'bg-warn-soft' : 'bg-ground'}`}>
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
          {batches.length > 0 && (
            <table className="w-full border-collapse overflow-hidden rounded-xl text-sm">
              <thead className="bg-brand-soft text-left text-xs uppercase tracking-wide">
                <tr><th className="px-2 py-1.5">{t('batches.batchNo')}</th><th className="px-2 py-1.5">{t('batches.expiry')}</th><th className="px-2 py-1.5 text-right">{t('batches.quantity')}</th>
                  <th className="px-2 py-1.5 text-right">MRP</th><th className="px-2 py-1.5 text-right">{t('batches.cost')}</th></tr>
              </thead>
              <tbody className="divide-y divide-line">
                {batches.map((b) => (
                  <tr key={b.id} className={b.expired ? 'bg-due-soft text-due' : b.daysLeft <= 30 ? 'bg-warn-soft' : ''}>
                    <td className="px-2 py-1.5 font-mono text-xs">{b.batchNo}</td>
                    <td className="px-2 py-1.5">{month(b.expiry)}{b.expired ? ` · ${t('batches.expired')}` : ''}</td>
                    <td className="px-2 py-1.5 text-right">{b.quantity}</td>
                    <td className="px-2 py-1.5 text-right">{formatRupees(b.mrp)}</td>
                    <td className="px-2 py-1.5 text-right">{b.costPrice != null ? formatRupees(b.costPrice) : '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
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

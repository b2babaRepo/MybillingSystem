import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api, request } from '../lib/api'
import { shrinkPhoto } from '../lib/images'
import { useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'
import { shopTypes, useShopSettings } from '../lib/reports'
import { ask, confirmAsk } from '../components/Ask'

interface Supplier { id: string; name: string; phone: string | null; due: number }
interface Detail { supplier: Supplier; entries: { id: string; amount: number; purchaseId: string | null; note: string | null; occurredAt: string; cancelled: boolean }[] }
/** Medicines also carry batch, expiry month (yyyy-mm), MRP, free units (scheme) and trade discount %. */
interface Row { productId: string; quantity: string; unitCost: string; batchNo: string; expiry: string; mrp: string; free: string; discount: string; gst: string; read?: string; problems?: string[] }

/** What the AI read off a distributor's bill photo, matched and checked by the server. Nothing is saved from it directly. */
interface InvoiceDraft {
  supplierName: string | null; billNo: string | null; grandTotal: number | null; linesTotal: number; totalDiffers: boolean
  lines: { read: { name: string; batchNo: string | null; expiryMonth: string | null; mrp: number | null; quantity: number | null; freeQuantity: number | null; rate: number | null; discountPercent: number | null; gstRate: number | null }; productId: string | null; problems: string[] }[]
}

/** Who the shop buys from and what it owes them: add a supplier's bill (stock goes up), or pay them. Needs a connection. */
export function SuppliersScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const suppliers = useQuery({ queryKey: [shopId, 'suppliers'], queryFn: () => api<Supplier[]>('/v1/suppliers') })
  const [openId, setOpenId] = useState<string | null>(null)
  const [draft, setDraft] = useState({ name: '', phone: '' })
  const [message, setMessage] = useState('')
  const refresh = () => queryClient.invalidateQueries({ queryKey: [shopId, 'suppliers'] })

  async function add() {
    setMessage('')
    try {
      const id = crypto.randomUUID()
      await api('/v1/suppliers', 'POST', { id, name: draft.name.trim(), phone: draft.phone.trim() || null })
      setDraft({ name: '', phone: '' })
      await refresh()
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  const due = (suppliers.data ?? []).reduce((sum, s) => sum + s.due, 0)
  if (openId) return <SupplierPage shopId={shopId} id={openId} onBack={() => setOpenId(null)} />
  return (
    <div className="flex flex-col gap-3 p-4">
      <section className="rounded-[18px] bg-white p-4">
        <span className="text-sm font-semibold text-muted">{t('suppliers.totalDue')}</span>
        <p className="text-2xl font-extrabold text-due">{formatRupees(due)}</p>
      </section>
      <ul className="flex flex-col gap-2">
        {suppliers.data?.map((s) => (
          <li key={s.id}>
            <button onClick={() => setOpenId(s.id)} className="flex min-h-14 w-full items-center justify-between rounded-[18px] bg-white px-4 text-left">
              <span className="font-bold">{s.name}</span><span className="font-bold text-due">{formatRupees(s.due)}</span>
            </button>
          </li>
        ))}
      </ul>
      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
        <h2 className="font-bold">{t('suppliers.add')}</h2>
        <Field label={t('suppliers.name')} value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} maxLength={120} />
        <Field label={t('suppliers.phone')} value={draft.phone} onChange={(e) => setDraft({ ...draft, phone: e.target.value })} inputMode="tel" />
        <Button disabled={draft.name.trim().length < 2} onClick={() => void add()}>{t('suppliers.add')}</Button>
        {message && <p className="text-sm text-due">{message}</p>}
      </section>
    </div>
  )
}

function SupplierPage({ shopId, id, onBack }: { shopId: string; id: string; onBack: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { products } = useProducts(shopId)
  const detail = useQuery({ queryKey: [shopId, 'suppliers', id], queryFn: () => api<Detail>(`/v1/suppliers/${id}`) })
  const empty: Row = { productId: '', quantity: '', unitCost: '', batchNo: '', expiry: '', mrp: '', free: '', discount: '', gst: '' }
  const medical = shopTypes(useShopSettings(shopId).data).includes('medical')
  const [rows, setRows] = useState<Row[]>([empty])
  const [billNo, setBillNo] = useState('')
  const [paid, setPaid] = useState('')
  const [pay, setPay] = useState('')
  const [mode, setMode] = useState<'cash' | 'upi'>('cash')
  const [message, setMessage] = useState('')
  const ai = useQuery({ queryKey: ['ai-status'], queryFn: () => api<{ consentedAt: string | null; canReadPhotos: boolean }>('/v1/me/ai') }).data
  const [askConsent, setAskConsent] = useState(false)
  const [photoNote, setPhotoNote] = useState<{ printed: number | null; lines: number; differs: boolean } | null>(null)
  const [reading, setReading] = useState(false)
  // The AI only drafts the rows; the owner checks each one (flagged rows first) and saves the bill as usual.
  const readPhoto = async (file: File) => {
    setMessage('')
    setReading(true)
    try {
      const draft = await api<InvoiceDraft>('/v1/suppliers/purchases/photo', 'POST', { imageDataUrl: await shrinkPhoto(file, 1600, 1_200_000, false) })
      if (draft.lines.length === 0) throw new Error(t('invoicePhoto.nothing'))
      const n = (v: number | null) => (v == null ? '' : String(v))
      setRows(draft.lines.map((l) => ({
        productId: l.productId ?? '', quantity: n(l.read.quantity), unitCost: n(l.read.rate), batchNo: l.read.batchNo ?? '', expiry: l.read.expiryMonth ?? '', mrp: n(l.read.mrp),
        free: l.read.freeQuantity ? String(l.read.freeQuantity) : '', discount: l.read.discountPercent ? String(l.read.discountPercent) : '', gst: n(l.read.gstRate), read: l.read.name, problems: l.problems,
      })))
      if (draft.billNo) setBillNo(draft.billNo)
      setPhotoNote({ printed: draft.grandTotal, lines: draft.linesTotal, differs: draft.totalDiffers })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    } finally {
      setReading(false)
    }
  }
  const agree = async () => {
    await request('/v1/me/ai/consent', 'POST', { agree: true })
    await queryClient.invalidateQueries({ queryKey: ['ai-status'] })
    setAskConsent(false)
  }
  const valid = rows.filter((r) => r.productId && Number(r.quantity) > 0 && r.unitCost !== '' && (!medical || !r.batchNo.trim() || (r.expiry && Number(r.mrp) > 0)))
  // Before GST (after trade discount), then with the GST the distributor charged on top.
  const taxable = (r: Row) => Math.round(Number(r.quantity) * Number(r.unitCost) * (100 - (Number(r.discount) || 0))) / 100
  const lineTotal = (r: Row) => taxable(r) + Math.round(taxable(r) * (Number(r.gst) || 0)) / 100
  const total = valid.reduce((sum, r) => sum + lineTotal(r), 0)

  async function run(action: () => Promise<unknown>) {
    setMessage('')
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: [shopId, 'suppliers'] })
      void queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
      setMessage(t('saved'))
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  const saveBill = () => run(async () => {
    await api('/v1/suppliers/purchases', 'POST', {
      id: crypto.randomUUID(), supplierId: id, billNo: billNo.trim() || null, paidAmount: Number(paid) || 0, paymentMode: mode,
      lines: valid.map((r) => ({
        productId: r.productId, quantity: Number(r.quantity), unitCost: Number(r.unitCost), freeQuantity: Number(r.free) || 0, discountPercent: Number(r.discount) || 0,
        gstRate: r.gst === '' ? null : Number(r.gst),
        ...(r.batchNo.trim() ? { batchNo: r.batchNo.trim(), expiryMonth: r.expiry, mrp: Number(r.mrp) } : {}),
      })),
    })
    setRows([empty])
    setBillNo('')
    setPaid('')
    setPhotoNote(null)
  })
  const cancelBill = async (purchaseId: string) => { if ((await confirmAsk(t('suppliers.cancelConfirm')))) void run(() => api(`/v1/suppliers/purchases/${purchaseId}/cancel`, 'POST', {})) }
  const rename = async () => {
    const name = (await ask(t('suppliers.name'), s?.name))?.trim()
    if (name) void run(() => api(`/v1/suppliers/${id}`, 'PUT', { id, name, phone: s?.phone ?? null }))
  }
  const savePayment = () => run(async () => {
    await api(`/v1/suppliers/${id}/pay`, 'POST', { id: crypto.randomUUID(), amount: Number(pay), paymentMode: mode })
    setPay('')
  })
  const set = (i: number, key: keyof Row, value: string) => setRows(rows.map((r, x) => (x === i ? { ...r, [key]: value } : r)))

  const s = detail.data?.supplier
  const input = 'h-12 min-w-0 rounded-xl border border-line bg-white px-3'
  return (
    <div className="flex flex-col gap-3 p-4">
      <button onClick={onBack} className="min-h-11 self-start font-semibold text-teal-700">← {t('suppliers.all')}</button>
      <section className="flex items-center justify-between rounded-[18px] bg-white p-4">
        <button onClick={rename} className="min-h-11 text-left text-lg font-bold underline decoration-dotted">{s?.name}</button><span className="text-xl font-extrabold text-due">{s ? formatRupees(s.due) : '–'}</span>
      </section>

      <label className="flex flex-col gap-1 rounded-[18px] bg-white p-4"><span className="text-sm font-medium text-muted">{t('suppliers.mode')}</span>
        <select value={mode} onChange={(e) => setMode(e.target.value as 'cash' | 'upi')} className={input}>
          <option value="cash">{t('cash')}</option><option value="upi">{t('upi')}</option>
        </select></label>

      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
        <h2 className="font-bold">{t('suppliers.pay')}</h2>
        <div className="flex gap-2">
          <input aria-label={t('suppliers.pay')} value={pay} onChange={(e) => setPay(e.target.value)} inputMode="decimal" className={`${input} flex-1`} />
          <Button disabled={!(Number(pay) > 0)} onClick={() => void savePayment()}>{t('save')}</Button>
        </div>
      </section>

      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
        <h2 className="font-bold">{t('suppliers.bill')}</h2>
        {medical && ai?.canReadPhotos && (ai.consentedAt ? (
          <label className={`flex min-h-12 cursor-pointer items-center justify-center gap-2 rounded-2xl border-2 border-teal-700 font-bold text-teal-800 ${reading ? 'opacity-50' : ''}`}>
            📷 {reading ? t('invoicePhoto.reading') : t('invoicePhoto.button')}
            <input type="file" accept="image/*" capture="environment" className="sr-only" disabled={reading}
              onChange={(e) => { const file = e.target.files?.[0]; e.target.value = ''; if (file) void readPhoto(file) }} />
          </label>
        ) : (
          <button onClick={() => setAskConsent(true)} className="min-h-12 rounded-2xl border-2 border-teal-700 font-bold text-teal-800">📷 {t('invoicePhoto.button')}</button>
        ))}
        {askConsent && (
          <div className="flex flex-col gap-2 rounded-2xl bg-amber-50 p-3 text-sm">
            <p className="font-bold">{t('expense.consentTitle')}</p><p>{t('invoicePhoto.consent')}</p>
            <div className="grid grid-cols-2 gap-2">
              <button onClick={() => void agree()} className="h-11 rounded-xl bg-teal-700 font-bold text-white">{t('expense.consentYes')}</button>
              <button onClick={() => setAskConsent(false)} className="h-11 rounded-xl border border-line font-semibold">{t('expense.consentNo')}</button>
            </div>
          </div>
        )}
        {photoNote && (
          <p className={`rounded-xl p-2 text-sm ${photoNote.differs ? 'bg-red-50 font-semibold text-due' : 'bg-teal-50 text-teal-900'}`}>
            {photoNote.differs && photoNote.printed != null ? t('invoicePhoto.totalDiffers', { printed: formatRupees(photoNote.printed), lines: formatRupees(photoNote.lines) }) : t('invoicePhoto.check')}
          </p>
        )}
        <Field label={t('suppliers.billNo')} value={billNo} onChange={(e) => setBillNo(e.target.value)} maxLength={40} />
        {rows.map((r, i) => (
          <div key={i} className={`grid grid-cols-2 gap-2 ${r.problems?.length ? 'rounded-xl bg-amber-50 p-2' : ''}`}>
            {r.read && <p className="col-span-2 text-xs text-muted">{t('invoicePhoto.read', { name: r.read })}{r.problems?.length ? ` · ${r.problems.map((p) => t(`invoicePhoto.problem.${p}`)).join(', ')}` : ''}</p>}
            <select aria-label={t('suppliers.item')} value={r.productId} onChange={(e) => set(i, 'productId', e.target.value)} className={`${input} col-span-2`}>
              <option value="">{t('suppliers.item')}</option>
              {products.filter((p) => !p.isService && (medical || p.id === r.productId || !rows.some((x) => x.productId === p.id))).map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
            <input aria-label={t('suppliers.qty')} placeholder={t('suppliers.qty')} value={r.quantity} onChange={(e) => set(i, 'quantity', e.target.value)} inputMode="decimal" className={input} />
            <input aria-label={t('suppliers.cost')} placeholder={t('suppliers.cost')} value={r.unitCost} onChange={(e) => set(i, 'unitCost', e.target.value)} inputMode="decimal" className={input} />
            {medical && (
              <>
                <input aria-label={t('batches.batchNo')} placeholder={t('batches.batchNo')} value={r.batchNo} onChange={(e) => set(i, 'batchNo', e.target.value)} className={input} />
                <input aria-label={t('batches.expiry')} type="month" value={r.expiry} onChange={(e) => set(i, 'expiry', e.target.value)} className={input} />
                <input aria-label="MRP" placeholder="MRP" value={r.mrp} onChange={(e) => set(i, 'mrp', e.target.value)} inputMode="decimal" className={input} />
                <input aria-label={t('batches.free')} placeholder={t('batches.free')} value={r.free} onChange={(e) => set(i, 'free', e.target.value)} inputMode="decimal" className={input} />
                <input aria-label={t('returns.discount')} placeholder={t('returns.discount')} value={r.discount} onChange={(e) => set(i, 'discount', e.target.value)} inputMode="decimal" className={input} />
                <select aria-label={t('pmonth.purchaseGst')} value={r.gst} onChange={(e) => set(i, 'gst', e.target.value)} className={input}>
                  <option value="">{t('pmonth.purchaseGst')}</option>{['0', '5', '12', '18'].map((g) => <option key={g} value={g}>GST {g}%</option>)}
                </select>
              </>
            )}
          </div>
        ))}
        <button onClick={() => setRows([...rows, empty])} className="min-h-11 font-semibold text-teal-700">+ {t('suppliers.addRow')}</button>
        <p className="flex justify-between font-bold"><span>{t('print.total')}</span><span>{formatRupees(total)}</span></p>
        <Field label={t('suppliers.paidNow')} value={paid} onChange={(e) => setPaid(e.target.value)} inputMode="decimal" />
        <Button disabled={valid.length === 0} onClick={() => void saveBill()}>{t('suppliers.saveBill')}</Button>
      </section>
      {message && <p className="text-sm text-muted">{message}</p>}

      <ul className="flex flex-col gap-1 rounded-[18px] bg-white p-4">
        {detail.data?.entries.map((e) => (
          <li key={e.id} className="flex justify-between border-b border-line py-1 text-sm">
            <span>{new Date(e.occurredAt).toLocaleDateString('en-IN')} · {e.amount > 0 ? t('suppliers.billLine') : t('suppliers.paidLine')}{e.note ? ` ${e.note}` : ''}</span>
            <span className={`font-semibold ${e.cancelled ? 'line-through' : ''}`}>{formatRupees(Math.abs(e.amount))}</span>
            {e.amount > 0 && e.purchaseId && !e.cancelled && <button onClick={() => cancelBill(e.purchaseId!)} className="min-h-11 px-2 text-xs font-semibold text-due">{t('suppliers.cancelBill')}</button>}
          </li>
        ))}
      </ul>
    </div>
  )
}

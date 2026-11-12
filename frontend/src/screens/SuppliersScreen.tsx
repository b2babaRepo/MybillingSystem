import { SupplierItemsCard } from '../components/SupplierPanels'
import { BagCheck } from './ProductScreen'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ItemPicker } from '../components/ItemPicker'
import { ListDetail } from '../components/ListDetail'
import { enqueue } from '../lib/outbox'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { NetworkError, api, request } from '../lib/api'
import { shrinkPhoto } from '../lib/images'
import { useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'
import { shopTypes, useShopSettings } from '../lib/reports'
import { confirmAsk } from '../components/Ask'
import { OrdersPanel, ReturnsPanel, ReceiptsPanel, type GoodsReceipt, SupplierImport, type PurchaseOrder } from './SupplierOrders'

interface Supplier { id: string; name: string; phone: string | null; due: number; days0To15?: number; days16To30?: number; over30?: number; oldestDays?: number; gstin?: string | null; address?: string | null; paymentDays?: number | null; isActive?: boolean }
type EntryKind = 'other' | 'opening' | 'bill' | 'payment' | 'credit_note' | 'bill_cancelled'
interface Entry { id: string; amount: number; purchaseId: string | null; note: string | null; occurredAt: string; cancelled: boolean; kind: EntryKind; mode: string | null; reference: string | null; balance: number; dueOn: string | null }
interface Detail { supplier: Supplier; entries: Entry[] }
interface BillDetail { billNo: string | null; total: number; extra: number; lines: { name: string; quantity: number; freeQuantity: number; unitCost: number; discountPercent: number; gstRate: number | null; taxable: number; gst: number }[] }
/** Medicines also carry batch, expiry month (yyyy-mm), MRP, free units (scheme) and trade discount %. */
interface Row { text?: string; productId: string; quantity: string; unitCost: string; bags?: string; basis?: 'unit' | 'quintal' | 'bag'; batchNo: string; expiry: string; mrp: string; free: string; discount: string; gst: string; read?: string; problems?: string[] }

/** What the AI read off a distributor's bill photo, matched and checked by the server. Nothing is saved from it directly. */
interface InvoiceDraft {
  supplierName: string | null; billNo: string | null; grandTotal: number | null; linesTotal: number; totalDiffers: boolean
  lines: { read: { name: string; batchNo: string | null; expiryMonth: string | null; mrp: number | null; quantity: number | null; freeQuantity: number | null; rate: number | null; discountPercent: number | null; gstRate: number | null }; productId: string | null; problems: string[] }[]
}

/** Who the shop buys from and what it owes them: add a supplier's bill (stock goes up), or pay them. Needs a connection. */
export function SuppliersScreen({ shopId, shopName = '' }: { shopId: string; shopName?: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const suppliers = useQuery({ queryKey: [shopId, 'suppliers'], queryFn: () => api<Supplier[]>('/v1/suppliers') })
  // Orders placed opens one supplier here, once.
  const [openId, setOpenId] = useState<string | null>(() => { try { const v = sessionStorage.getItem('gk-open-supplier'); sessionStorage.removeItem('gk-open-supplier'); return v } catch { return null } })
  const [draft, setDraft] = useState({ name: '', phone: '' })
  const [message, setMessage] = useState('')
  const [search, setSearch] = useState('')
  const [showOff, setShowOff] = useState(false)
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
  const sum = (key: 'days0To15' | 'days16To30' | 'over30') => (suppliers.data ?? []).reduce((total, s) => total + (s[key] ?? 0), 0)
  // Who to pay first: the oldest unpaid bills at the top, then the biggest dues.
  const anyOff = (suppliers.data ?? []).some((s) => s.isActive === false)
  const ordered = (suppliers.data ?? []).filter((s) => (showOff || s.isActive !== false) && s.name.toLowerCase().includes(search.trim().toLowerCase())).sort((a, b) => (b.due > 0 ? 1 : 0) - (a.due > 0 ? 1 : 0) || (b.oldestDays ?? 0) - (a.oldestDays ?? 0) || b.due - a.due)
  // Desktop: the supplier list stays on the left with the open supplier beside it; a phone shows one at a time.
  return (
    <ListDetail hasDetail={!!openId} detail={openId && <SupplierPage shopId={shopId} shopName={shopName} id={openId} onBack={() => setOpenId(null)} />} placeholder={t('suppliers.pick')} list={
    <div className="flex flex-col gap-3 p-4">
      <section className="rounded-[18px] bg-white p-4 shadow-sm">
        <span className="text-sm font-semibold text-muted">{t('suppliers.totalDue')}</span>
        <p className="text-2xl font-extrabold text-due">{formatRupees(due)}</p>
        {due > 0 && (
          <div className="mt-3 grid grid-cols-3 gap-2 text-center">
            <span className="rounded-xl bg-blue-50 p-2 text-blue-800"><span className="block text-xs">{t('suppliers.age0')}</span><b>{formatRupees(sum('days0To15'))}</b></span>
            <span className="rounded-xl bg-orange-50 p-2 text-orange-800"><span className="block text-xs">{t('suppliers.age16')}</span><b>{formatRupees(sum('days16To30'))}</b></span>
            <span className="rounded-xl bg-red-50 p-2 text-red-800"><span className="block text-xs">{t('suppliers.age30')}</span><b>{formatRupees(sum('over30'))}</b></span>
          </div>
        )}
      </section>
      <span className="relative block">
        <span aria-hidden className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-muted">🔍</span>
        <input type="search" aria-label={t('supplier2.search')} placeholder={t('supplier2.search')} value={search} onChange={(e) => setSearch(e.target.value)} className="h-12 w-full rounded-2xl border border-line bg-white pl-11 pr-3 shadow-sm outline-none transition focus:border-teal-600 focus:ring-4 focus:ring-teal-600/10" />
      </span>
      {anyOff && <label className="flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={showOff} onChange={(e) => setShowOff(e.target.checked)} className="h-5 w-5" />{t('supplier2.showOff')}</label>}
      <ul className="flex flex-col gap-2">
        {ordered.map((s) => (
          <li key={s.id}>
            <button onClick={() => setOpenId(s.id)} aria-current={openId === s.id ? 'true' : undefined} className={`flex min-h-16 w-full items-center gap-3 rounded-[18px] bg-white px-3 py-2 text-left shadow-sm transition hover:bg-[#f6faf9] lg:hover:shadow-md ${openId === s.id ? 'lg:ring-2 lg:ring-teal-700' : ''}`}>
              <span aria-hidden className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full text-sm font-bold ${s.due > 0 ? 'bg-red-50 text-due' : 'bg-[#edf2f1] text-muted'}`}>{s.name.split(/s+/).filter(Boolean).slice(0, 2).map((w) => w[0]!.toUpperCase()).join('')}</span>
              <span className="flex min-w-0 flex-1 flex-col">
                <span className="truncate font-bold">{s.name}{s.isActive === false ? ` · ${t('supplier2.off')}` : ''}</span>
                {s.due > 0 && (s.oldestDays ?? 0) > 0 && (
                  <span className={`text-[13px] font-semibold ${(s.oldestDays ?? 0) > 30 ? 'text-due' : (s.oldestDays ?? 0) > 15 ? 'text-udhaar' : 'text-muted'}`}>{t('suppliers.oldest', { days: s.oldestDays })}</span>
                )}
              </span>
              <span className={`shrink-0 text-right font-bold ${s.due > 0 ? 'text-due' : 'text-muted'}`}>{formatRupees(Math.abs(s.due))}{s.due !== 0 && <span className="block text-xs font-normal text-muted">{t(s.due > 0 ? 'heads.weOwe' : 'heads.theyOwe')}</span>}</span>
              <span aria-hidden className="text-muted">›</span>
            </button>
          </li>
        ))}
      </ul>
      <SupplierImport shopId={shopId} />
      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm">
        <h2 className="font-bold">{t('suppliers.add')}</h2>
        <Field label={t('suppliers.name')} value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} maxLength={120} />
        <Field label={t('suppliers.phone')} value={draft.phone} onChange={(e) => setDraft({ ...draft, phone: e.target.value })} inputMode="tel" />
        <Button disabled={draft.name.trim().length < 2} onClick={() => void add()}>{t('suppliers.add')}</Button>
        {message && <p className="text-sm text-due">{message}</p>}
      </section>
    </div>} />
  )
}

function SupplierPage({ shopId, shopName, id, onBack }: { shopId: string; shopName: string; id: string; onBack: () => void }) {
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
  const [mode, setMode] = useState<'cash' | 'upi' | 'bank'>('cash')
  const [message, setMessage] = useState('')
  const [reference, setReference] = useState('')
  const [paidOn, setPaidOn] = useState('')
  const [billDate, setBillDate] = useState('')
  const [extra, setExtra] = useState('')
  const [editing, setEditing] = useState<{ name: string; phone: string; gstin: string; address: string; days: string; active: boolean } | null>(null)
  const [opening, setOpening] = useState({ amount: '', owed: true, asOf: '' })
  const [view, setView] = useState<'all' | 'bills' | 'payments'>('all')
  const [openBill, setOpenBill] = useState<string | null>(null)
  // The order these goods came against: the bill starts as what is still to come, and saving counts it off.
  const [against, setAgainst] = useState<PurchaseOrder | null>(null)
  // Goods counted in earlier on receipts: this bill is for them, so their stock isn't added again.
  const [fromReceipts, setFromReceipts] = useState<GoodsReceipt[]>([])
  // A bill for goods that came against an order stays against it, so the order counts down.
  const billFor = (receipt: GoodsReceipt, order: PurchaseOrder | null) => {
    setAgainst(order)
    setFromReceipts([receipt])
    setRows(receipt.lines.map((l) => ({ ...empty, productId: l.productId, quantity: String(l.quantity) })))
    document.getElementById('supplier-bill')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }
  const goodsCame = (order: PurchaseOrder) => {
    setAgainst(order)
    setRows(order.lines.filter((l) => l.received < l.quantity).map((l) => ({ ...empty, productId: l.productId, quantity: String(l.quantity - l.received), unitCost: l.rate != null ? String(l.rate) : '' })))
    document.getElementById('supplier-bill')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  }
  const bill = useQuery({ queryKey: [shopId, 'suppliers', 'bill', openBill], queryFn: () => api<BillDetail>(`/v1/suppliers/purchases/${openBill}`), enabled: !!openBill }).data
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
  const costEach = (r: Row) => {
    const p = products.find((x) => x.id === r.productId)
    const rate = Number(r.unitCost)
    return r.basis === 'quintal' ? rate / 100 : r.basis === 'bag' && p?.bagSize ? rate / p.bagSize : rate
  }
  const taxable = (r: Row) => Math.round(Number(r.quantity) * costEach(r) * (100 - (Number(r.discount) || 0))) / 100
  const lineTotal = (r: Row) => taxable(r) + Math.round(taxable(r) * (Number(r.gst) || 0)) / 100
  const total = valid.reduce((sum, r) => sum + lineTotal(r), 0) + (Number(extra) || 0)

  async function run(action: () => Promise<unknown>) {
    setMessage('')
    try {
      queuedNote = null
      await action()
      await queryClient.invalidateQueries({ queryKey: [shopId, 'suppliers'] }).catch(() => undefined)
      void queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
      setMessage(queuedNote ?? t('saved'))
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  /**
   * A supplier bill or payment with no network waits in the outbox and goes when back online (the server keeps the
   * first copy of each ID, so a resend never doubles it). False when it was queued instead of saved.
   */
  // Set by an action that was queued offline, so the message says so instead of "Saved".
  let queuedNote: string | null = null

  async function send(path: string, body: unknown, label: string): Promise<boolean> {
    try {
      await api(path, 'POST', body)
      return true
    } catch (e) {
      if (!(e instanceof NetworkError)) throw e
      await enqueue({ id: crypto.randomUUID(), shopId, kind: 'supplier', path, body, label })
      return false
    }
  }

  const saveBill = async () => {
    const no = billNo.trim()
    if (no && detail.data?.entries.some((e) => e.kind === 'bill' && !e.cancelled && e.note === no) && !(await confirmAsk(t('supplier2.repeatBill', { no })))) return
    await writeBill()
  }
  const writeBill = () => run(async () => {
    const saved = await send('/v1/suppliers/purchases', {
      id: crypto.randomUUID(), supplierId: id, billNo: billNo.trim() || null, paidAmount: Number(paid) || 0, paymentMode: mode, billDate: billDate || null, extra: Number(extra) || 0, orderId: against?.id ?? null, receiptIds: fromReceipts.length > 0 ? fromReceipts.map((r) => r.id) : null,
      lines: valid.map((r) => ({
        productId: r.productId, quantity: Number(r.quantity), unitCost: Math.round(costEach(r) * 10000) / 10000, freeQuantity: Number(r.free) || 0, discountPercent: Number(r.discount) || 0,
        gstRate: r.gst === '' ? null : Number(r.gst), bags: Number(r.bags) > 0 ? Number(r.bags) : null,
        // Only medicines keep batches. A batch read off a paint bill isn't on the screen, so it can't be asked about.
        ...(medical && r.batchNo.trim() ? { batchNo: r.batchNo.trim(), expiryMonth: r.expiry, mrp: Number(r.mrp) } : {}),
      })),
    }, `${s?.name ?? ''} · ${formatRupees(total)}`)
    if (!saved) queuedNote = t('suppliers.queued')
    setRows([empty])
    setFromReceipts([])
    setBillNo('')
    setPaid('')
    setBillDate('')
    setExtra('')
    setAgainst(null)
    setPhotoNote(null)
  })
  const cancelBill = async (purchaseId: string) => { if ((await confirmAsk(t('suppliers.cancelConfirm')))) void run(() => api(`/v1/suppliers/purchases/${purchaseId}/cancel`, 'POST', {})) }
  const startEdit = () => setEditing(s ? { name: s.name, phone: s.phone ?? '', gstin: s.gstin ?? '', address: s.address ?? '', days: s.paymentDays ? String(s.paymentDays) : '', active: s.isActive !== false } : null)
  const saveDetails = () => run(async () => {
    await api(`/v1/suppliers/${id}`, 'PUT', { id, name: editing!.name.trim(), phone: editing!.phone.trim() || null, gstin: editing!.gstin.trim(), address: editing!.address.trim(), paymentDays: Number(editing!.days) || 0, isActive: editing!.active })
    setEditing(null)
  })
  const saveOpening = () => run(async () => {
    await api(`/v1/suppliers/${id}/opening`, 'POST', { id: crypto.randomUUID(), amount: (opening.owed ? 1 : -1) * Number(opening.amount), asOf: opening.asOf || null })
    setOpening({ amount: '', owed: true, asOf: '' })
  })
  const savePayment = () => run(async () => {
    const saved = await send(`/v1/suppliers/${id}/pay`, { id: crypto.randomUUID(), amount: Number(pay), paymentMode: mode, reference: mode === 'bank' ? reference.trim() || null : null, paidOn: paidOn || null }, `${s?.name ?? ''} · ${t('suppliers.paidShort')} ${formatRupees(Number(pay))}`)
    if (!saved) queuedNote = t('suppliers.queued')
    setPay('')
    setReference('')
    setPaidOn('')
  })
  const set = (i: number, key: keyof Row, value: string) => setRows(rows.map((r, x) => (x === i ? { ...r, [key]: value } : r)))
  // Typed names find an item; only an exact name sets it, so a half-typed word can't land on the wrong one.
  const choosable = products.filter((p) => !p.isService)
  const nameOf = (r: Row) => r.text ?? products.find((p) => p.id === r.productId)?.name ?? ''
  const pickItem = (i: number, text: string) => setRows(rows.map((r, x) => (x === i ? { ...r, text, productId: choosable.find((p) => p.name.toLowerCase() === text.trim().toLowerCase())?.id ?? '' } : r)))
  const shown = (detail.data?.entries ?? []).filter((e) => view === 'all' || (view === 'bills' ? e.purchaseId && e.amount > 0 : e.kind === 'payment'))

  const s = detail.data?.supplier
  const input = 'h-12 min-w-0 rounded-xl border border-line bg-white px-3'
  return (
    <div className="flex flex-col gap-3 p-4">
      <button onClick={onBack} className="min-h-11 self-start font-semibold text-teal-700">← {t('suppliers.all')}</button>
      <section className="flex items-center justify-between rounded-[18px] bg-white p-4 shadow-sm">
        <span className="flex min-w-0 flex-col"><span className="truncate text-lg font-bold">{s?.name}</span>
          <span className="flex gap-3 text-sm"><button onClick={startEdit} className="min-h-11 font-semibold text-teal-700 underline">{t('supplier2.edit')}</button>
          <button onClick={() => navigate(`/print/supplier/${id}`)} className="min-h-11 font-semibold text-teal-700 underline">{t('supplier2.statement')}</button></span></span><span className="text-right text-xl font-extrabold text-due">{s ? formatRupees(Math.abs(s.due)) : '–'}{s && s.due !== 0 && <span className="block text-xs font-normal text-muted">{t(s.due > 0 ? 'heads.weOwe' : 'heads.theyOwe')}</span>}</span>
      </section>

      {editing && (
        <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm">
          <Field label={t('suppliers.name')} value={editing.name} onChange={(e) => setEditing({ ...editing, name: e.target.value })} maxLength={120} />
          <Field label={t('suppliers.phone')} value={editing.phone} onChange={(e) => setEditing({ ...editing, phone: e.target.value })} inputMode="tel" />
          <Field label={t('supplier2.gstin')} value={editing.gstin} onChange={(e) => setEditing({ ...editing, gstin: e.target.value })} maxLength={15} autoCapitalize="characters" />
          <Field label={t('supplier2.address')} value={editing.address} onChange={(e) => setEditing({ ...editing, address: e.target.value })} maxLength={200} />
          <Field label={t('supplier2.days')} value={editing.days} onChange={(e) => setEditing({ ...editing, days: e.target.value })} inputMode="numeric" />
          <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={editing.active} onChange={(e) => setEditing({ ...editing, active: e.target.checked })} className="h-5 w-5" />{t('supplier2.active')}</label>
          <div className="grid grid-cols-2 gap-2">
            <Button disabled={editing.name.trim().length < 2} onClick={() => void saveDetails()}>{t('save')}</Button>
            <button onClick={() => setEditing(null)} className="min-h-12 rounded-2xl border border-line font-semibold">{t('cancel')}</button>
          </div>
        </section>
      )}

      {detail.data && !detail.data.entries.some((e) => e.kind === 'opening') && (
        <details className="rounded-[18px] bg-white p-4 shadow-sm">
          <summary className="min-h-11 cursor-pointer font-bold">{t('supplier2.opening')}</summary>
          <p className="mb-2 text-sm text-muted">{t('supplier2.openingHint')}</p>
          <div className="grid grid-cols-2 gap-2">
            <input aria-label={t('supplier2.opening')} placeholder="₹" value={opening.amount} onChange={(e) => setOpening({ ...opening, amount: e.target.value })} inputMode="decimal" className={input} />
            <select aria-label={t('supplier2.opening')} value={opening.owed ? 'owed' : 'owes'} onChange={(e) => setOpening({ ...opening, owed: e.target.value === 'owed' })} className={input}>
              <option value="owed">{t('supplier2.weOwe')}</option><option value="owes">{t('supplier2.theyOwe')}</option>
            </select>
            <label className="col-span-2 flex items-center justify-between gap-2 text-sm"><span className="text-muted">{t('supplier2.asOf')}</span><input type="date" value={opening.asOf} onChange={(e) => setOpening({ ...opening, asOf: e.target.value })} className={input} /></label>
          </div>
          <Button className="mt-2 w-full" disabled={!(Number(opening.amount) > 0)} onClick={() => void saveOpening()}>{t('save')}</Button>
        </details>
      )}

      <label className="flex flex-col gap-1 rounded-[18px] bg-white p-4 shadow-sm"><span className="text-sm font-medium text-muted">{t('suppliers.mode')}</span>
        <select value={mode} onChange={(e) => setMode(e.target.value as 'cash' | 'upi' | 'bank')} className={input}>
          <option value="cash">{t('cash')}</option><option value="upi">{t('upi')}</option><option value="bank">{t('bank')}</option>
        </select></label>

      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm">
        <h2 className="font-bold">{t('suppliers.pay')}</h2>
        <div className="flex gap-2">
          <input aria-label={t('suppliers.pay')} value={pay} onChange={(e) => setPay(e.target.value)} inputMode="decimal" className={`${input} flex-1`} />
          <Button disabled={!(Number(pay) > 0)} onClick={() => void savePayment()}>{t('save')}</Button>
        </div>
        {mode === 'bank' && <Field label={t('supplier2.reference')} value={reference} onChange={(e) => setReference(e.target.value)} maxLength={40} />}
        <label className="flex items-center justify-between gap-2 text-sm"><span className="text-muted">{t('supplier2.paidOn')}</span><input type="date" value={paidOn} onChange={(e) => setPaidOn(e.target.value)} className={input} /></label>
      </section>

      {s && <OrdersPanel shopId={shopId} shopName={shopName} supplierId={id} phone={s.phone} products={products} onGoodsCame={goodsCame} />}
      {s && <ReceiptsPanel shopId={shopId} supplierId={id} products={products} onBill={billFor} />}
      {s && <SupplierItemsCard shopId={shopId} supplierId={id} />}

      <section id="supplier-bill" className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm">
        <h2 className="font-bold">{t('suppliers.bill')}</h2>
        {fromReceipts.length > 0 && (
          <p className="flex items-start justify-between gap-2 rounded-xl bg-teal-50 p-2 text-sm text-teal-900">
            <span>{t('receipt.onBill', { numbers: fromReceipts.map((r) => r.number).join(', #') })}</span>
            <button onClick={() => setFromReceipts([])} className="min-h-11 shrink-0 font-semibold underline">{t('receipt.clear')}</button>
          </p>
        )}
        {against && (
          <p className="flex items-start justify-between gap-2 rounded-xl bg-teal-50 p-2 text-sm text-teal-900">
            <span>{t('supplier2.againstOrder', { number: against.number })}</span>
            <button onClick={() => setAgainst(null)} className="min-h-11 shrink-0 font-semibold underline">{t('supplier2.clearOrder')}</button>
          </p>
        )}
        {ai?.canReadPhotos && (ai.consentedAt ? (
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
        <label className="flex items-center justify-between gap-2 text-sm"><span className="text-muted">{t('supplier2.billDate')}</span><input type="date" value={billDate} onChange={(e) => setBillDate(e.target.value)} className={input} /></label>
        {rows.map((r, i) => (
          <div key={i} className={`grid grid-cols-2 gap-2 ${r.problems?.length ? 'rounded-xl bg-amber-50 p-2' : ''}`}>
            {r.read && <p className="col-span-2 text-xs text-muted">{t('invoicePhoto.read', { name: r.read })}{r.problems?.length ? ` · ${r.problems.map((p) => t(`invoicePhoto.problem.${p}`)).join(', ')}` : ''}</p>}
            {against && (() => {
              const ordered = against.lines.find((l) => l.productId === r.productId)
              return ordered && <p className="col-span-2 text-xs text-muted">{t('supplier2.ordered', { qty: ordered.quantity - ordered.received })}{ordered.rate != null && r.unitCost !== '' && Number(r.unitCost) !== ordered.rate ? <b className="text-due"> · {t('supplier2.rateDiffers', { rate: formatRupees(ordered.rate) })}</b> : null}</p>
            })()}
            {fromReceipts.length > 0 && r.productId && (() => {
              const good = fromReceipts.flatMap((x) => x.lines).filter((l) => l.productId === r.productId).reduce((s, l) => s + l.quantity - (l.damaged ?? 0), 0)
              return good > 0 && Number(r.quantity) > good ? <p className="col-span-2 text-xs font-semibold text-due">⚠ {t('gi.billedMore', { billed: Number(r.quantity), good })}</p> : null
            })()}
            <ItemPicker products={choosable} text={nameOf(r)} placeholder={t('supplier2.itemSearch')} onText={(text) => pickItem(i, text)} onPick={(p) => pickItem(i, p.name)} className="col-span-2" />
            <input aria-label={t('suppliers.qty')} placeholder={t('suppliers.qty')} value={r.quantity} onChange={(e) => set(i, 'quantity', e.target.value)} inputMode="decimal" className={input} />
            <input aria-label={t('suppliers.cost')} placeholder={t('suppliers.cost')} value={r.unitCost} onChange={(e) => set(i, 'unitCost', e.target.value)} inputMode="decimal" className={input} />
            {(() => {
              const p = products.find((x) => x.id === r.productId)
              if (!p || (p.unit !== 'kg' && !p.bagSize)) return null
              const unit = t(`units.${p.unit}`)
              return (
                <>
                  <select aria-label={t('bag.rateBasis')} value={r.basis ?? 'unit'} onChange={(e) => set(i, 'basis', e.target.value)} className={input}>
                    <option value="unit">{t('bag.perUnit', { unit })}</option>
                    {p.unit === 'kg' && <option value="quintal">{t('bag.perQuintal')}</option>}
                    {p.bagSize ? <option value="bag">{t('bag.ratePerBag')}</option> : null}
                  </select>
                  {p.bagSize ? <input aria-label={t('bag.bags')} placeholder={t('bag.bags')} value={r.bags ?? ''} onChange={(e) => { set(i, 'bags', e.target.value) }} inputMode="numeric" className={input} /> : <span />}
                  {p.bagSize && Number(r.bags) > 0 && Number(r.quantity) > 0 ? <div className="col-span-2"><BagCheck avg={Number(r.quantity) / Number(r.bags)} size={p.bagSize} bags={Number(r.bags)} unit={unit} /></div> : null}
                </>
              )
            })()}
            {medical && (
              <>
                <input aria-label={t('batches.batchNo')} placeholder={t('batches.batchNo')} value={r.batchNo} onChange={(e) => set(i, 'batchNo', e.target.value)} className={input} />
                <input aria-label={t('batches.expiry')} type="month" value={r.expiry} onChange={(e) => set(i, 'expiry', e.target.value)} className={input} />
                <input aria-label="MRP" placeholder="MRP" value={r.mrp} onChange={(e) => set(i, 'mrp', e.target.value)} inputMode="decimal" className={input} />
              </>
            )}
            <input aria-label={t('batches.free')} placeholder={t('batches.free')} value={r.free} onChange={(e) => set(i, 'free', e.target.value)} inputMode="decimal" className={input} />
                <input aria-label={t('returns.discount')} placeholder={t('returns.discount')} value={r.discount} onChange={(e) => set(i, 'discount', e.target.value)} inputMode="decimal" className={input} />
                <select aria-label={t('pmonth.purchaseGst')} value={r.gst} onChange={(e) => set(i, 'gst', e.target.value)} className={input}>
                  <option value="">{t('pmonth.purchaseGst')}</option>{['0', '5', '12', '18', '28', '40'].map((g) => <option key={g} value={g}>GST {g}%</option>)}
            </select>
          </div>
        ))}
        <button onClick={() => setRows([...rows, empty])} className="min-h-11 font-semibold text-teal-700">+ {t('suppliers.addRow')}</button>
        <Field label={t('supplier2.extra')} value={extra} onChange={(e) => setExtra(e.target.value)} inputMode="text" />
        <p className="flex justify-between font-bold"><span>{t('print.total')}</span><span>{formatRupees(total)}</span></p>
        <Field label={t('suppliers.paidNow')} value={paid} onChange={(e) => setPaid(e.target.value)} inputMode="decimal" />
        <Button disabled={valid.length === 0} onClick={() => void saveBill()}>{t('suppliers.saveBill')}</Button>
      </section>
      {message && <p className="text-sm text-muted">{message}</p>}

      <ReturnsPanel shopId={shopId} supplierId={id} products={products} />

      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm">
        <div className="flex gap-2">
          {(['all', 'bills', 'payments'] as const).map((v) => <button key={v} onClick={() => setView(v)} className={`min-h-11 rounded-xl px-3 text-sm font-semibold ${view === v ? 'bg-teal-700 text-white shadow-sm' : 'border border-line'}`}>{t(`supplier2.${v}`)}</button>)}
        </div>
        {shown.length === 0 && <p className="text-sm text-muted">{t('supplier2.noLines')}</p>}
        <ul className="flex flex-col">
          {shown.map((e) => (
            <li key={e.id} className="border-b border-line py-2 text-sm">
              <div className="flex items-center justify-between gap-2">
                <span className="flex min-w-0 flex-col">
                  <span>{new Date(e.occurredAt).toLocaleDateString('en-IN')} · {t(`supplier2.kind.${e.kind}`)}{e.note && e.kind !== 'bill_cancelled' ? ` ${e.note.replace(/^Credit note\s*/, '')}` : ''}{e.mode ? ` · ${t(e.mode)}` : ''}{e.reference ? ` ${e.reference}` : ''}</span>
                  {e.dueOn && <span className="text-xs text-muted">{t('supplier2.due', { date: new Date(e.dueOn).toLocaleDateString('en-IN') })}</span>}
                </span>
                <span className="shrink-0 text-right"><span className={`block font-semibold ${e.cancelled ? 'line-through' : ''}`}>{e.amount < 0 ? '−' : ''}{formatRupees(Math.abs(e.amount))}</span>
                  <span className="block text-xs text-muted">{t('supplier2.balance')} {formatRupees(Math.abs(e.balance))}{e.balance !== 0 ? ` · ${t(e.balance > 0 ? 'heads.weOwe' : 'heads.theyOwe')}` : ''}</span></span>
              </div>
              {e.purchaseId && e.amount > 0 && (
                <div className="flex gap-3">
                  <button onClick={() => setOpenBill(openBill === e.purchaseId ? null : e.purchaseId)} aria-expanded={openBill === e.purchaseId} className="min-h-11 text-xs font-semibold text-teal-700 underline">{t('suppliers.billLine')} {openBill === e.purchaseId ? '▴' : '▾'}</button>
                  {!e.cancelled && <button onClick={() => void cancelBill(e.purchaseId!)} className="min-h-11 text-xs font-semibold text-due">{t('suppliers.cancelBill')}</button>}
                </div>
              )}
              {openBill === e.purchaseId && e.amount > 0 && bill && (
                <div className="mb-1 rounded-xl bg-[#f1f5f4] p-2 text-xs">
                  {bill.lines.map((l, i) => (
                    <p key={i} className="flex justify-between gap-2"><span>{l.name} × {l.quantity}{l.freeQuantity > 0 ? ` + ${t('supplier2.free', { qty: l.freeQuantity })}` : ''}{l.gstRate != null ? ` · GST ${l.gstRate}%` : ''}</span><span>{formatRupees(l.taxable + l.gst)}</span></p>
                  ))}
                  {bill.extra !== 0 && <p className="flex justify-between gap-2"><span>{t('supplier2.extraLine')}</span><span>{formatRupees(bill.extra)}</span></p>}
                </div>
              )}
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}

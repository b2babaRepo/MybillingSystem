import { BrandSchemes } from '../components/BrandSchemes'
import { shownLocale } from '../i18n'
import { GodownPicker } from '../components/GodownPicker'
import { SupplierItemsCard } from '../components/SupplierPanels'
import { ChargesEditor, LotView, chargesBody, noCharges, type Charges } from '../components/LandedCost'
import { BagCheck } from './ProductScreen'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { ItemPicker } from '../components/ItemPicker'
import { ListDetail } from '../components/ListDetail'
import { enqueue } from '../lib/outbox'
import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Drawer, Field, btn } from '../components/ui'
import { NetworkError, api, request } from '../lib/api'
import { billFile } from '../lib/billFile'
import { DataTable } from '../components/DataTable'
import { useProducts } from '../lib/data'
import { formatRupees, whatsAppLink } from '../lib/format'
import { navigate } from '../router'
import { shopTypes, useShopSettings } from '../lib/reports'
import { confirmAsk } from '../lib/ask'
import { guarded } from '../lib/billPasscode'
import { OrdersPanel, ReturnsPanel, ReceiptsPanel, type GoodsReceipt, SupplierImport, type PurchaseOrder } from './SupplierOrders'

interface Supplier { id: string; name: string; phone: string | null; due: number; days0To15?: number; days16To30?: number; over30?: number; oldestDays?: number; gstin?: string | null; address?: string | null; paymentDays?: number | null; isActive?: boolean }
type EntryKind = 'other' | 'opening' | 'bill' | 'payment' | 'credit_note' | 'bill_cancelled'
interface Entry { id: string; amount: number; purchaseId: string | null; note: string | null; occurredAt: string; cancelled: boolean; kind: EntryKind; mode: string | null; reference: string | null; balance: number; dueOn: string | null }
interface Detail { supplier: Supplier; entries: Entry[] }
interface BillDetail { billNo: string | null; total: number; extra: number; lines: { name: string; quantity: number; freeQuantity: number; unitCost: number; discountPercent: number; gstRate: number | null; taxable: number; gst: number }[] }
/** Medicines also carry batch, expiry month (yyyy-mm), MRP, free units (scheme) and trade discount %. */
interface Row { text?: string; productId: string; quantity: string; unitCost: string; bags?: string; basis?: 'unit' | 'quintal' | 'bag' | 'kg'; batchNo: string; expiry: string; mrp: string; free: string; discount: string; gst: string; read?: string; problems?: string[] }

/** What the AI read off a distributor's bill photo, matched and checked by the server. Nothing is saved from it directly. */
export interface InvoiceDraft {
  supplierName: string | null; supplierId: string | null; billNo: string | null; grandTotal: number | null; linesTotal: number; totalDiffers: boolean
  lines: { read: { name: string; batchNo: string | null; expiryMonth: string | null; mrp: number | null; quantity: number | null; freeQuantity: number | null; rate: number | null; discountPercent: number | null; gstRate: number | null }; productId: string | null; problems: string[] }[]
}

/** Where the Uploaded bills page leaves a read bill for its supplier's page: read once, then cleared. */
export const UPLOAD_DRAFT_KEY = 'gk-upload-draft'

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
  const [showAdd, setShowAdd] = useState(false)
  const [showImport, setShowImport] = useState(false)
  const refresh = () => queryClient.invalidateQueries({ queryKey: [shopId, 'suppliers'] })

  async function add() {
    setMessage('')
    try {
      const id = crypto.randomUUID()
      await api('/v1/suppliers', 'POST', { id, name: draft.name.trim(), phone: draft.phone.trim() || null })
      setDraft({ name: '', phone: '' })
      setShowAdd(false)
      await refresh()
      setOpenId(id)
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  const due = (suppliers.data ?? []).reduce((sum, s) => sum + Math.max(0, s.due), 0)
  // Goods sent back whose credit note hasn't come: the due comes down by this once it does.
  const openReturns = useQuery({ queryKey: [shopId, 'suppliers', 'returns', 'open'], queryFn: () => api<{ value: number; settledAt: string | null }[]>('/v1/suppliers/returns') }).data ?? []
  const waiting = openReturns.filter((r) => !r.settledAt).reduce((sum, r) => sum + r.value, 0)
  const theyOwe = (suppliers.data ?? []).reduce((sum, s) => sum + Math.max(0, -s.due), 0)
  const sum = (key: 'days0To15' | 'days16To30' | 'over30') => (suppliers.data ?? []).reduce((total, s) => total + (s[key] ?? 0), 0)
  // Who to pay first: the oldest unpaid bills at the top, then the biggest dues.
  const anyOff = (suppliers.data ?? []).some((s) => s.isActive === false)
  const ordered = (suppliers.data ?? []).filter((s) => (showOff || s.isActive !== false) && s.name.toLowerCase().includes(search.trim().toLowerCase())).sort((a, b) => (b.due > 0 ? 1 : 0) - (a.due > 0 ? 1 : 0) || (b.oldestDays ?? 0) - (a.oldestDays ?? 0) || b.due - a.due)
  const icon = (d: string, size = 18) => <svg aria-hidden="true" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d={d} /></svg>
  // Desktop: the supplier list stays on the left with the open supplier beside it; a phone shows one at a time.
  return (
    <ListDetail hasDetail={!!openId} detail={openId && <SupplierPage shopId={shopId} shopName={shopName} id={openId} onBack={() => setOpenId(null)} />} placeholder={t('suppliers.pick')} list={
    <div className="flex flex-col gap-3 p-4">
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="mr-auto text-xl font-extrabold">{t('suppliers.title')}</h1>
        <button onClick={() => { setShowImport(!showImport); setShowAdd(false) }} aria-expanded={showImport} className={`flex min-h-11 items-center gap-1.5 rounded-xl px-3 text-sm ${btn.outline}`}>{icon('M12 15V3M7 8l5-5 5 5M4 15v5h16v-5', 16)}{t('sup3.import')}</button>
        <button onClick={() => { setShowAdd(!showAdd); setShowImport(false) }} aria-expanded={showAdd} className={`flex min-h-11 items-center gap-1.5 rounded-xl px-4 ${btn.solid}`}><span aria-hidden className="text-xl leading-none">+</span>{t('suppliers.add')}</button>
      </div>
      {showAdd && (
        <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
          <Field label={t('suppliers.name')} value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} maxLength={120} autoFocus />
          <Field label={t('suppliers.phone')} value={draft.phone} onChange={(e) => setDraft({ ...draft, phone: e.target.value })} inputMode="tel" />
          <Button disabled={draft.name.trim().length < 2} onClick={() => void add()}>{t('suppliers.add')}</Button>
          {message && <p className="text-sm text-due">{message}</p>}
        </section>
      )}
      {showImport && <SupplierImport shopId={shopId} open />}
      {/* What is owed both ways, at a glance, as on the khata list. */}
      <div className="grid grid-cols-2 gap-2">
        <section className="flex min-h-[80px] flex-col justify-between rounded-card bg-due-soft p-3 shadow-sm ring-1 ring-black/5">
          <span className="text-xs font-semibold text-due">{t('suppliers.totalDue')}</span>
          <b className="text-xl text-due">{formatRupees(due)}</b>
          {waiting > 0 && <span className="text-xs text-due">{t('hwp3.waitingCredit', { amount: formatRupees(waiting) })}</span>}
        </section>
        <section className="flex min-h-[80px] flex-col justify-between rounded-card bg-green-50 p-3 shadow-sm ring-1 ring-black/5">
          <span className="text-xs font-semibold text-emerald-800">{t('sup3.theyOwe')}</span>
          <b className="text-xl text-jama">{formatRupees(theyOwe)}</b>
          {theyOwe > 0 && <span className="text-[11px] text-emerald-800">{t('sup3.theyOweHint')}</span>}
        </section>
        <section className="flex min-h-[80px] flex-col justify-between rounded-card bg-white p-3 shadow-sm ring-1 ring-black/5">
          <span className="text-xs font-semibold text-muted">{t('sup4.owing')}</span>
          <b className="text-2xl">{(suppliers.data ?? []).filter((x) => x.due > 0).length}</b>
        </section>
        <section className="flex min-h-[80px] flex-col justify-between rounded-card bg-white p-3 shadow-sm ring-1 ring-black/5">
          <span className="text-xs font-semibold text-muted">{t('sup4.all')}</span>
          <b className="text-2xl">{(suppliers.data ?? []).filter((x) => x.isActive !== false).length}</b>
        </section>
      </div>
      <BrandSchemes shopId={shopId} />
      {/* The buying jobs, each in its own colour with a line icon on a white dot. */}
      <div className="grid grid-cols-2 gap-2">
        {[
          { to: '/purchase-uploads', label: t('upload.link'), d: 'M12 15V3M7 8l5-5 5 5M4 15v5h16v-5', tone: 'bg-[#f1eafd] text-[#6d28d9] hover:bg-[#e7dcfb]' },
          { to: '/reorder', label: t('reorder.link'), d: 'M3 4h2l2.5 11h11L21 7H6M9 20h.01M18 20h.01', tone: 'bg-[#fdf1e3] text-[#a1460a] hover:bg-[#fbe6cc]' },
          { to: '/orders-placed', label: t('po.link'), d: 'M9 3h6v3H9zM6 5h3M15 5h3v16H6V5M9 13l2 2 4-4', tone: 'bg-[#e8effd] text-[#1d4ed8] hover:bg-[#dbe5fc]' },
          { to: '/goods-to-come', label: t('gtc.link'), d: 'M2 6h13v11H2zM15 10h4l3 3v4h-7M6 20h.01M18 20h.01', tone: 'bg-[#e3f2ee] text-[#0b5e4f] hover:bg-[#d2ebe4]' },
        ].map((x) => (
          <button key={x.to} onClick={() => navigate(x.to)} className={`flex min-h-[52px] items-center gap-2.5 rounded-2xl px-3 text-left text-sm font-bold transition active:scale-[0.98] ${x.tone}`}>
            <span aria-hidden className="flex h-[30px] w-[30px] shrink-0 items-center justify-center rounded-full bg-white">{icon(x.d)}</span>
            <span className="leading-tight">{x.label}</span>
          </button>
        ))}
      </div>
      {due > 0 && (
        <div className="grid grid-cols-3 gap-2 text-center">
          <span className="rounded-xl bg-blue-50 p-2 text-blue-800"><span className="block text-xs">{t('suppliers.age0')}</span><b>{formatRupees(sum('days0To15'))}</b></span>
          <span className="rounded-xl bg-orange-50 p-2 text-orange-800"><span className="block text-xs">{t('suppliers.age16')}</span><b>{formatRupees(sum('days16To30'))}</b></span>
          <span className="rounded-xl bg-due-soft p-2 text-red-800"><span className="block text-xs">{t('suppliers.age30')}</span><b>{formatRupees(sum('over30'))}</b></span>
        </div>
      )}
      <span className="relative block">
        <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-muted"><circle cx="11" cy="11" r="7" /><path d="M20 20l-3.5-3.5" /></svg>
        <input type="search" aria-label={t('supplier2.search')} placeholder={t('supplier2.search')} value={search} onChange={(e) => setSearch(e.target.value)} className="h-12 w-full rounded-2xl border border-line bg-white pl-11 pr-3 shadow-sm outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
      </span>
      {anyOff && <label className="flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={showOff} onChange={(e) => setShowOff(e.target.checked)} className="h-5 w-5" />{t('supplier2.showOff')}</label>}
      {/* A computer: Supplier | Owed, like the customer list, with the oldest unpaid bill. */}
      {ordered.length > 0 && (
        <div className="hidden lg:block">
          <DataTable label={t('suppliers.title')} rows={ordered} rowKey={(x) => x.id} selectedKey={openId} onRowClick={(x) => setOpenId(x.id)} download="suppliers"
            columns={[
              { key: 'name', header: t('poTable.supplier'), sortValue: (x) => x.name,
                render: (x) => <span className="flex flex-col"><span className="font-semibold">{x.name}{x.isActive === false ? ` · ${t('supplier2.off')}` : ''}</span>{x.phone && <span className="text-xs text-muted">{x.phone}</span>}</span>, csv: (x) => x.name },
              { key: 'oldest', header: t('supTable.oldest'), align: 'right', width: 'w-24', sortValue: (x) => (x.due > 0 ? x.oldestDays ?? 0 : null),
                render: (x) => (x.due > 0 && (x.oldestDays ?? 0) > 0 ? <span className={(x.oldestDays ?? 0) > 30 ? 'font-semibold text-due' : (x.oldestDays ?? 0) > 15 ? 'text-udhaar' : 'text-muted'}>{t('supTable.days', { count: x.oldestDays })}</span> : '—'), csv: (x) => (x.due > 0 ? x.oldestDays ?? '' : '') },
              { key: 'due', header: t('supTable.owed'), align: 'right', width: 'w-32', sortValue: (x) => x.due,
                render: (x) => <span className={`font-bold ${x.due > 0 ? 'text-due' : x.due < 0 ? 'text-jama' : 'text-muted'}`}>{formatRupees(Math.abs(x.due))}{x.due !== 0 && <span className="block text-[11px] font-normal">{t(x.due > 0 ? 'heads.weOwe' : 'heads.theyOwe')}</span>}</span>, csv: (x) => x.due },
            ]}
            footer={[t('supTable.total', { count: ordered.length }), '', formatRupees(ordered.reduce((n, x) => n + Math.max(0, x.due), 0))]} />
        </div>
      )}
      <ul className="flex flex-col gap-2 lg:hidden">
        {ordered.map((s) => (
          <li key={s.id}>
            <button onClick={() => setOpenId(s.id)} aria-current={openId === s.id ? 'true' : undefined} className={`flex min-h-16 w-full items-center gap-3 rounded-card bg-white px-3 py-2 text-left shadow-sm transition hover:bg-[#f6faf9] lg:hover:shadow-md ${openId === s.id ? 'lg:ring-2 lg:ring-brand' : ''}`}>
              <span aria-hidden className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-full text-sm font-bold ${s.due > 0 ? 'bg-due-soft text-due' : s.due < 0 ? 'bg-green-50 text-jama' : 'bg-[#edf2f1] text-muted'}`}>{s.name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0]!.toUpperCase()).join('')}</span>
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
  const [charges, setCharges] = useState<Charges>(noCharges)
  const [godown, setGodown] = useState('')
  const [editing, setEditing] = useState<{ name: string; phone: string; gstin: string; address: string; days: string; active: boolean } | null>(null)
  const [opening, setOpening] = useState({ amount: '', owed: true, asOf: '' })
  const [view, setView] = useState<'all' | 'bills' | 'payments'>('all')
  // One view at a time: the tab below the header, and a drawer for paying or adding a bill.
  const [tab, setTab] = useState<'statement' | 'orders' | 'goods' | 'returns' | 'items'>('statement')
  const [panel, setPanel] = useState<'pay' | 'bill' | null>(null)
  // Which of the header's jobs is lit: Pay supplier until another is pressed.
  const [act, setAct] = useState<'pay' | 'bill' | 'upload' | 'order' | 'return'>('pay')
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
    setPanel('bill')
  }
  const goodsCame = (order: PurchaseOrder) => {
    setAgainst(order)
    setRows(order.lines.filter((l) => l.received < l.quantity).map((l) => ({ ...empty, productId: l.productId, quantity: String(l.quantity - l.received), unitCost: l.rate != null ? String(l.rate) : '' })))
    setPanel('bill')
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
      applyDraft(await api<InvoiceDraft>('/v1/suppliers/purchases/photo', 'POST', { imageDataUrl: await billFile(file, t('upload.pdfTooBig')) }))
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    } finally {
      setReading(false)
    }
  }
  /** The read bill's lines into the rows, for the owner to check. */
  function applyDraft(draft: InvoiceDraft) {
      if (draft.lines.length === 0) throw new Error(t('invoicePhoto.nothing'))
      const n = (v: number | null) => (v == null ? '' : String(v))
      setRows(draft.lines.map((l) => ({
        productId: l.productId ?? '', quantity: n(l.read.quantity), unitCost: n(l.read.rate), batchNo: l.read.batchNo ?? '', expiry: l.read.expiryMonth ?? '', mrp: n(l.read.mrp),
        free: l.read.freeQuantity ? String(l.read.freeQuantity) : '', discount: l.read.discountPercent ? String(l.read.discountPercent) : '', gst: n(l.read.gstRate), read: l.read.name, problems: l.problems,
      })))
      if (draft.billNo) setBillNo(draft.billNo)
      setPhotoNote({ printed: draft.grandTotal, lines: draft.linesTotal, differs: draft.totalDiffers })
  }
  // A bill uploaded from the computer or a phone, already read on the Uploaded bills page: fill this supplier's bill with it.
  const [fromUpload, setFromUpload] = useState<string | null>(null)
  const uploaded = useQuery({ queryKey: ['purchase-upload', fromUpload], queryFn: () => api<{ imageDataUrl: string }>(`/v1/suppliers/uploads/${fromUpload}`), enabled: !!fromUpload, staleTime: Infinity }).data
  // A PDF shows from a blob address: browsers won't show a data: PDF in a frame.
  const pdfUrl = useMemo(() => {
    if (!uploaded?.imageDataUrl.startsWith('data:application/pdf')) return null
    const bytes = Uint8Array.from(atob(uploaded.imageDataUrl.split(',')[1]), (c) => c.charCodeAt(0))
    return URL.createObjectURL(new Blob([bytes], { type: 'application/pdf' }))
  }, [uploaded])
  useEffect(() => () => { if (pdfUrl) URL.revokeObjectURL(pdfUrl) }, [pdfUrl])
  useEffect(() => {
    try {
      const raw = sessionStorage.getItem(UPLOAD_DRAFT_KEY)
      if (!raw) return
      const picked = JSON.parse(raw) as { uploadId: string; supplierId: string; draft: InvoiceDraft | null }
      if (picked.supplierId !== id) return
      sessionStorage.removeItem(UPLOAD_DRAFT_KEY)
      // Read by the AI: its lines go in. Not read: the bill shows beside the form to type from.
      if (picked.draft) applyDraft(picked.draft)
      setFromUpload(picked.uploadId)
      setPanel('bill')
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id])
  // A photo or PDF of this supplier's bill: kept on the Uploaded bills list, read by the AI when it is on, and shown
  // beside the bill form either way, so the owner can check or type it in.
  const uploadBill = async (file: File) => {
    setMessage('')
    setReading(true)
    try {
      const image = await billFile(file, t('upload.pdfTooBig'))
      const up = await api<{ id: string }>('/v1/suppliers/uploads', 'POST', { imageDataUrl: image })
      if (ai?.canReadPhotos && ai.consentedAt) {
        try { applyDraft(await api<InvoiceDraft>('/v1/suppliers/purchases/photo', 'POST', { imageDataUrl: image })) } catch (e) { setMessage(e instanceof Error ? e.message : t('error')) }
      }
      setFromUpload(up.id)
      setPanel('bill')
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
    return r.basis === 'quintal' ? rate / 100 : r.basis === 'bag' && p?.bagSize ? rate / p.bagSize : r.basis === 'kg' && p?.weightKg ? rate * p.weightKg : rate
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
      id: crypto.randomUUID(), supplierId: id, billNo: billNo.trim() || null, paidAmount: Number(paid) || 0, paymentMode: mode, billDate: billDate || null, extra: Number(extra) || 0, ...chargesBody(charges), godownId: godown || null, orderId: against?.id ?? null, receiptIds: fromReceipts.length > 0 ? fromReceipts.map((r) => r.id) : null,
      lines: valid.map((r) => ({
        productId: r.productId, quantity: Number(r.quantity), unitCost: Math.round(costEach(r) * 10000) / 10000, freeQuantity: Number(r.free) || 0, discountPercent: Number(r.discount) || 0,
        gstRate: r.gst === '' ? null : Number(r.gst), bags: Number(r.bags) > 0 ? Number(r.bags) : null,
        // Only medicines keep batches. A batch read off a paint bill isn't on the screen, so it can't be asked about.
        ...(medical && r.batchNo.trim() ? { batchNo: r.batchNo.trim(), expiryMonth: r.expiry, mrp: Number(r.mrp) } : {}),
      })),
    }, `${s?.name ?? ''} · ${formatRupees(total)}`)
    if (!saved) queuedNote = t('suppliers.queued')
    // The uploaded photo is now a purchase: it leaves the waiting list.
    if (fromUpload) { await api(`/v1/suppliers/uploads/${fromUpload}/used`, 'POST', { purchaseId: null }).catch(() => undefined); setFromUpload(null) }
    setRows([empty])
    setFromReceipts([])
    setBillNo('')
    setPaid('')
    setBillDate('')
    setExtra('')
    setCharges(noCharges)
    setAgainst(null)
    setPhotoNote(null)
    setPanel(null)
  })
  const cancelBill = async (purchaseId: string) => { if ((await confirmAsk(t('suppliers.cancelConfirm')))) void run(() => guarded(() => api(`/v1/suppliers/purchases/${purchaseId}/cancel`, 'POST', {}))) }
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
    setPanel(null)
  })
  const set = (i: number, key: keyof Row, value: string) => setRows(rows.map((r, x) => (x === i ? { ...r, [key]: value } : r)))
  // Typed names find an item; only an exact name sets it, so a half-typed word can't land on the wrong one.
  const choosable = products.filter((p) => !p.isService)
  const nameOf = (r: Row) => r.text ?? products.find((p) => p.id === r.productId)?.name ?? ''
  const pickItem = (i: number, text: string) => setRows(rows.map((r, x) => (x === i ? { ...r, text, productId: choosable.find((p) => p.name.toLowerCase() === text.trim().toLowerCase())?.id ?? '' } : r)))
  const shown = (detail.data?.entries ?? []).filter((e) => view === 'all' || (view === 'bills' ? e.purchaseId && e.amount > 0 : e.kind === 'payment'))

  const s = detail.data?.supplier
  const input = 'h-12 min-w-0 rounded-xl border border-line bg-white px-3'
  const modePick = (
    <div role="group" aria-label={t('suppliers.mode')} className="flex flex-col gap-1">
      <span className="text-sm font-medium text-muted">{t('suppliers.mode')}</span>
      <div className="grid grid-cols-3 gap-2">
        {(['cash', 'upi', 'bank'] as const).map((m) => (
          <button key={m} type="button" aria-pressed={mode === m} onClick={() => setMode(m)}
            className={`min-h-11 rounded-xl text-sm font-semibold ${mode === m ? 'border-2 border-brand bg-brand-soft text-brand' : 'border border-line bg-white'}`}>{t(m)}</button>
        ))}
      </div>
    </div>
  )
  const initials = (s?.name ?? '').split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0]!.toUpperCase()).join('')
  const tabs = [
    { key: 'statement', label: t('supplier2.statement') },
    { key: 'orders', label: t('supplier2.orders') },
    { key: 'goods', label: t('receipt.title') },
    { key: 'returns', label: t('supplier2.returns') },
    { key: 'items', label: t('rs.supplierItems') },
  ] as const
  const icon = (d: string) => <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d={d} /></svg>
  // Each job keeps its own colour; the one in use is filled, the rest a soft tint with the icon on a white dot.
  const ACTIONS = {
    pay: { on: 'bg-[#0b5e4f] text-white shadow-lg shadow-[#0b5e4f]/30', off: 'bg-[#e3f2ee] text-[#0b5e4f] hover:bg-[#d2ebe4]' },
    bill: { on: 'bg-[#1d4ed8] text-white shadow-lg shadow-[#1d4ed8]/30', off: 'bg-[#e8effd] text-[#1d4ed8] hover:bg-[#dbe5fc]' },
    upload: { on: 'bg-[#6d28d9] text-white shadow-lg shadow-[#6d28d9]/30', off: 'bg-[#f1eafd] text-[#6d28d9] hover:bg-[#e7dcfb]' },
    order: { on: 'bg-[#a1460a] text-white shadow-lg shadow-[#a1460a]/30', off: 'bg-[#fdf1e3] text-[#a1460a] hover:bg-[#fbe6cc]' },
    return: { on: 'bg-[#be123c] text-white shadow-lg shadow-[#be123c]/30', off: 'bg-[#fde8ec] text-[#be123c] hover:bg-[#fbd5dd]' },
  } as const
  const action = (key: keyof typeof ACTIONS) => `flex min-h-[52px] cursor-pointer items-center justify-center gap-2.5 rounded-2xl px-3 text-sm font-bold whitespace-nowrap transition active:scale-[0.98] ${act === key ? ACTIONS[key].on : ACTIONS[key].off}`
  const dot = (key: keyof typeof ACTIONS, d: string) => <span aria-hidden className={`flex h-[30px] w-[30px] shrink-0 items-center justify-center rounded-full ${act === key ? 'bg-white/20' : 'bg-white'}`}>{icon(d)}</span>
  return (
    <div className="flex flex-col gap-3 p-4">
      <button onClick={onBack} className="min-h-11 self-start font-semibold text-brand lg:hidden">← {t('suppliers.all')}</button>

      {/* Who, what is owed, and the four jobs. Each job opens on its own instead of every form stacked on the page. */}
      <section className="flex flex-col gap-4 rounded-card bg-white p-4 shadow-sm">
        <div className="flex items-center gap-3">
          <span aria-hidden className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-brand-soft text-lg font-extrabold text-brand">{initials}</span>
          <span className="flex min-w-0 flex-1 flex-col">
            <span className="truncate text-xl font-extrabold">{s?.name}</span>
            <span className="flex flex-wrap items-center gap-x-2 text-sm text-muted">{s?.phone}{s?.phone && <span aria-hidden>·</span>}<button onClick={startEdit} className={`min-h-9 ${btn.link}`}>{t('supplier2.edit')}</button></span>
          </span>
          <span className={`shrink-0 text-right text-2xl font-extrabold ${s && s.due < 0 ? 'text-jama' : s && s.due > 0 ? 'text-due' : 'text-muted'}`}>{s ? formatRupees(Math.abs(s.due)) : '–'}{s && s.due !== 0 && <span className="block text-xs font-normal text-muted">{t(s.due > 0 ? 'heads.weOwe' : 'heads.theyOwe')}</span>}</span>
        </div>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 xl:grid-cols-5">
          <button onClick={() => { setAct('pay'); setPanel('pay') }} aria-pressed={act === 'pay'} className={action('pay')}>{dot('pay', 'M3 6h18v12H3zM12 9.5a2.5 2.5 0 100 5 2.5 2.5 0 000-5')}{t('suppliers.pay')}</button>
          <button onClick={() => { setAct('bill'); setPanel('bill') }} aria-pressed={act === 'bill'} className={action('bill')}>{dot('bill', 'M6 2h9l4 4v16H6zM9 11h7M9 15h7')}{t('sup3.addBill')}</button>
          <label className={`${action('upload')} ${reading ? 'opacity-60' : ''}`}>{dot('upload', 'M12 15V3M7 8l5-5 5 5M4 15v5h16v-5')}{reading ? t('invoicePhoto.reading') : t('sup3.uploadBill')}
            <input type="file" accept="image/*,application/pdf" className="sr-only" disabled={reading} aria-label={t('sup3.uploadBill')}
              onChange={(e) => { const file = e.target.files?.[0]; e.target.value = ''; setAct('upload'); if (file) void uploadBill(file) }} /></label>
          <button onClick={() => { setAct('order'); setTab('orders') }} aria-pressed={act === 'order'} className={action('order')}>{dot('order', 'M3 4h2l2.5 11h11L21 7H6M9 20h.01M18 20h.01')}{t('sup3.placeOrder')}</button>
          <button onClick={() => { setAct('return'); setTab('returns') }} aria-pressed={act === 'return'} className={action('return')}>{dot('return', 'M9 14L4 9l5-5M4 9h11a5 5 0 010 10h-3')}{t('sup3.returnGoods')}</button>
        </div>
        {message && !panel && <p role="status" className="rounded-xl bg-brand-soft p-2 text-sm font-semibold text-brand-deep">{message}</p>}
      </section>

      {editing && (
        <Drawer title={t('supplier2.edit')} onClose={() => setEditing(null)}>
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
        </Drawer>
      )}

      <div role="tablist" aria-label={s?.name ?? ''} className="-mx-4 flex gap-1 overflow-x-auto border-b border-line px-4 [scrollbar-width:none]">
        {tabs.map((x) => (
          <button key={x.key} role="tab" aria-selected={tab === x.key} onClick={() => setTab(x.key)}
            className={`min-h-12 shrink-0 border-b-[3px] px-3 text-sm whitespace-nowrap ${tab === x.key ? 'border-brand font-bold text-brand' : 'border-transparent font-semibold text-muted hover:text-ink'}`}>{x.label}</button>
        ))}
      </div>

      {tab === 'statement' && (
      <section id="supplier-statement" className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="flex gap-2">
            {(['all', 'bills', 'payments'] as const).map((v) => <button key={v} onClick={() => setView(v)} className={`min-h-10 rounded-full px-4 text-sm font-semibold ${view === v ? 'bg-brand text-white shadow-sm' : 'border border-line'}`}>{t(`supplier2.${v}`)}</button>)}
          </div>
          <div className="inline-flex overflow-hidden rounded-full border border-line bg-white shadow-sm">
            <button onClick={() => navigate(`/print/supplier/${id}`)} className="flex min-h-11 items-center gap-2 px-4 text-sm font-semibold hover:bg-[#f6faf9]"><span className="text-brand">{icon('M6 9V3h12v6M3 9h18v8H3zM6 14h12v7H6z')}</span>{t('sup3.printPdf')}</button>
            <span aria-hidden className="w-px bg-line" />
            <a href={whatsAppLink(s?.phone ?? null, t('supplier2.share', { shop: shopName, name: s?.name ?? '', amount: formatRupees(Math.abs(s?.due ?? 0)) }))} target="_blank" rel="noreferrer"
              className="flex min-h-11 items-center gap-2 bg-[#e7f8ee] px-4 text-sm font-bold text-[#0f6a3d] hover:bg-[#d3f2e0]">{icon('M4 20l1.4-4A8 8 0 1112 20a8 8 0 01-4-1z')}WhatsApp</a>
          </div>
        </div>
        {shown.length === 0 && <p className="text-sm text-muted">{t('supplier2.noLines')}</p>}
        <ul className="flex flex-col">
          {shown.map((e) => (
            <li key={e.id} className="border-b border-line py-2 text-sm">
              <div className="flex items-center justify-between gap-2">
                <span className="flex min-w-0 flex-col">
                  <span>{new Date(e.occurredAt).toLocaleDateString(shownLocale())} · {t(`supplier2.kind.${e.kind}`)}{e.note && e.kind !== 'bill_cancelled' ? ` ${e.note.replace(/^Credit note\s*/, '')}` : ''}{e.mode ? ` · ${t(e.mode)}` : ''}{e.reference ? ` ${e.reference}` : ''}</span>
                  {e.dueOn && <span className="text-xs text-muted">{t('supplier2.due', { date: new Date(e.dueOn).toLocaleDateString(shownLocale()) })}</span>}
                </span>
                <span className="shrink-0 text-right"><span className={`block font-semibold ${e.cancelled ? 'line-through' : ''}`}>{e.amount < 0 ? '−' : ''}{formatRupees(Math.abs(e.amount))}</span>
                  <span className="block text-xs text-muted">{t('supplier2.balance')} {formatRupees(Math.abs(e.balance))}{e.balance !== 0 ? ` · ${t(e.balance > 0 ? 'heads.weOwe' : 'heads.theyOwe')}` : ''}</span></span>
              </div>
              {e.purchaseId && e.amount > 0 && (
                <div className="flex gap-3">
                  <button onClick={() => setOpenBill(openBill === e.purchaseId ? null : e.purchaseId)} aria-expanded={openBill === e.purchaseId} className={`min-h-11 text-xs ${btn.link}`}>{t('suppliers.billLine')} {openBill === e.purchaseId ? '▴' : '▾'}</button>
                  {!e.cancelled && <button onClick={() => void cancelBill(e.purchaseId!)} className={`min-h-9 self-center text-xs ${btn.dangerOutline}`}>{t('suppliers.cancelBill')}</button>}
                </div>
              )}
              {openBill === e.purchaseId && e.amount > 0 && bill && (
                <div className="mb-1 rounded-xl bg-[#f1f5f4] p-2 text-xs">
                  {bill.lines.map((l, i) => (
                    <p key={i} className="flex justify-between gap-2"><span>{l.name} × {l.quantity}{l.freeQuantity > 0 ? ` + ${t('supplier2.free', { qty: l.freeQuantity })}` : ''}{l.gstRate != null ? ` · GST ${l.gstRate}%` : ''}</span><span>{formatRupees(l.taxable + l.gst)}</span></p>
                  ))}
                  {bill.extra !== 0 && <p className="flex justify-between gap-2"><span>{t('supplier2.extraLine')}</span><span>{formatRupees(bill.extra)}</span></p>}
                  <LotView shopId={shopId} purchaseId={e.purchaseId!} />
                </div>
              )}
            </li>
          ))}
        </ul>
      </section>
      )}
      {tab === 'statement' && (
detail.data && !detail.data.entries.some((e) => e.kind === 'opening') && (
        <details className="rounded-card bg-white p-4 shadow-sm">
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
      )
      )}
      {tab === 'orders' && s && <OrdersPanel shopId={shopId} shopName={shopName} supplierId={id} phone={s.phone} products={products} onGoodsCame={goodsCame} />}
      {tab === 'goods' && s && <ReceiptsPanel shopId={shopId} supplierId={id} products={products} onBill={billFor} />}
      {tab === 'returns' && <ReturnsPanel shopId={shopId} supplierId={id} products={products} />}
      {tab === 'items' && s && (<><SupplierItemsCard shopId={shopId} supplierId={id} /><p className="px-1 text-sm text-muted">{t('sup3.itemsHint')}</p></>)}

      {panel === 'pay' && (
        <Drawer title={`${t('suppliers.pay')} · ${s?.name ?? ''}`} onClose={() => setPanel(null)}>
        <div className="flex gap-2">
          <input aria-label={t('suppliers.pay')} value={pay} onChange={(e) => setPay(e.target.value)} inputMode="decimal" className={`${input} flex-1`} />
          <Button disabled={!(Number(pay) > 0)} onClick={() => void savePayment()}>{t('save')}</Button>
        </div>
        {mode === 'bank' && <Field label={t('supplier2.reference')} value={reference} onChange={(e) => setReference(e.target.value)} maxLength={40} />}
        <label className="flex items-center justify-between gap-2 text-sm"><span className="text-muted">{t('supplier2.paidOn')}</span><input type="date" value={paidOn} onChange={(e) => setPaidOn(e.target.value)} className={input} /></label>
          {modePick}
          {message && <p role="status" className="text-sm text-muted">{message}</p>}
        </Drawer>
      )}

      {panel === 'bill' && (
        <Drawer title={`${t('suppliers.bill')} · ${s?.name ?? ''}`} onClose={() => setPanel(null)}>
      {fromReceipts.length > 0 && (
        <p className="flex items-start justify-between gap-2 rounded-xl bg-brand-soft p-2 text-sm text-brand-deep">
          <span>{t('receipt.onBill', { numbers: fromReceipts.map((r) => r.number).join(', #') })}</span>
          <button onClick={() => setFromReceipts([])} className={`min-h-11 shrink-0 ${btn.link}`}>{t('receipt.clear')}</button>
        </p>
      )}
      {against && (
        <p className="flex items-start justify-between gap-2 rounded-xl bg-brand-soft p-2 text-sm text-brand-deep">
          <span>{t('supplier2.againstOrder', { number: against.number })}</span>
          <button onClick={() => setAgainst(null)} className={`min-h-11 shrink-0 ${btn.link}`}>{t('supplier2.clearOrder')}</button>
        </p>
      )}
      {ai?.canReadPhotos && (ai.consentedAt ? (
        <label className={`flex min-h-12 cursor-pointer items-center justify-center gap-2 rounded-2xl border-2 border-brand font-bold text-brand-strong ${reading ? 'opacity-50' : ''}`}>
          📷 {reading ? t('invoicePhoto.reading') : t('invoicePhoto.button')}
          <input type="file" accept="image/*,application/pdf" capture="environment" className="sr-only" disabled={reading}
            onChange={(e) => { const file = e.target.files?.[0]; e.target.value = ''; if (file) void readPhoto(file) }} />
        </label>
      ) : (
        <button onClick={() => setAskConsent(true)} className={`min-h-12 rounded-2xl ${btn.outline}`}>📷 {t('invoicePhoto.button')}</button>
      ))}
      {askConsent && (
        <div className="flex flex-col gap-2 rounded-2xl bg-warn-soft p-3 text-sm">
          <p className="font-bold">{t('expense.consentTitle')}</p><p>{t('invoicePhoto.consent')}</p>
          <div className="grid grid-cols-2 gap-2">
            <button onClick={() => void agree()} className={`h-11 rounded-xl ${btn.solid}`}>{t('expense.consentYes')}</button>
            <button onClick={() => setAskConsent(false)} className="h-11 rounded-xl border border-line font-semibold">{t('expense.consentNo')}</button>
          </div>
        </div>
      )}
      {fromUpload && uploaded && (
        <div className="flex flex-col gap-2 rounded-xl border border-line bg-[#f6faf9] p-2 text-sm">
          <p className="font-semibold">{t('upload.billBeside')}</p>
          {pdfUrl
            ? <iframe title={t('upload.billBeside')} src={pdfUrl} className="h-[28rem] w-full rounded-lg border border-line bg-white" />
            : <a href={uploaded.imageDataUrl} target="_blank" rel="noreferrer"><img src={uploaded.imageDataUrl} alt={t('upload.billBeside')} className="max-h-[28rem] w-full rounded-lg border border-line bg-white object-contain" /></a>}
        </div>
      )}
      {photoNote && (
        <p className={`rounded-xl p-2 text-sm ${photoNote.differs ? 'bg-due-soft font-semibold text-due' : 'bg-brand-soft text-brand-deep'}`}>
          {photoNote.differs && photoNote.printed != null ? t('invoicePhoto.totalDiffers', { printed: formatRupees(photoNote.printed), lines: formatRupees(photoNote.lines) }) : t('invoicePhoto.check')}
        </p>
      )}
      <Field label={t('suppliers.billNo')} value={billNo} onChange={(e) => setBillNo(e.target.value)} maxLength={40} />
      <label className="flex items-center justify-between gap-2 text-sm"><span className="text-muted">{t('supplier2.billDate')}</span><input type="date" value={billDate} onChange={(e) => setBillDate(e.target.value)} className={input} /></label>
      {rows.map((r, i) => (
        <div key={i} className={`grid grid-cols-2 gap-2 ${r.problems?.length ? 'rounded-xl bg-warn-soft p-2' : ''}`}>
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
            if (!p || (p.unit !== 'kg' && !p.bagSize && !p.weightKg)) return null
            const unit = t(`units.${p.unit}`)
            const pack = t(`units.${p.purchaseUnit ?? 'bag'}`)
            return (
              <>
                <select aria-label={t('bag.rateBasis')} value={r.basis ?? 'unit'} onChange={(e) => set(i, 'basis', e.target.value)} className={input}>
                  <option value="unit">{t('bag.perUnit', { unit })}</option>
                  {p.unit === 'kg' && <option value="quintal">{t('bag.perQuintal')}</option>}
                  {p.unit !== 'kg' && p.weightKg ? <option value="kg">{t('bag.perUnit', { unit: t('units.kg') })}</option> : null}
                  {p.bagSize ? <option value="bag">{p.purchaseUnit && p.purchaseUnit !== 'bag' ? t('bag.perUnit', { unit: pack }) : t('bag.ratePerBag')}</option> : null}
                </select>
                {p.bagSize ? <input aria-label={pack} placeholder={pack} value={r.bags ?? ''} onChange={(e) => { set(i, 'bags', e.target.value) }} inputMode="numeric" className={input} /> : <span />}
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
      <button onClick={() => setRows([...rows, empty])} className={`min-h-11 self-start ${btn.add}`}>+ {t('suppliers.addRow')}</button>
      <Field label={t('supplier2.extra')} value={extra} onChange={(e) => setExtra(e.target.value)} inputMode="text" />
      <ChargesEditor value={charges} onChange={setCharges} hasExtra={Number(extra) > 0} />
      <GodownPicker shopId={shopId} value={godown} onChange={setGodown} label="in" />
      <p className="flex justify-between font-bold"><span>{t('print.total')}</span><span>{formatRupees(total)}</span></p>
      <Field label={t('suppliers.paidNow')} value={paid} onChange={(e) => setPaid(e.target.value)} inputMode="decimal" />
      <Button disabled={valid.length === 0} onClick={() => void saveBill()}>{t('suppliers.saveBill')}</Button>
          {modePick}
          {message && <p role="status" className="text-sm text-muted">{message}</p>}
        </Drawer>
      )}
    </div>
  )
}

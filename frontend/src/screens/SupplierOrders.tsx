import { BagCheck } from './ProductScreen'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { confirmAsk } from '../components/Ask'
import { ItemPicker } from '../components/ItemPicker'
import { api } from '../lib/api'
import { formatRupees, whatsAppLink } from '../lib/format'
import { parseSuppliers } from '../lib/supplierImport'
import type { Product } from '../lib/types'

export interface OrderLine { productId: string; name: string; quantity: number; rate: number | null; received: number }
export interface PurchaseOrder { id: string; number: number; supplierId: string; supplierName: string; status: 'open' | 'closed' | 'cancelled'; note: string | null; expectedOn: string | null; createdAt: string; closedAt?: string | null; pending: number; lines: OrderLine[] }
interface ReturnLine { productName: string; batchNo: string; quantity: number; value: number }
interface SupplierReturn { id: string; number: number; value: number; daysOpen: number; settledAt: string | null; lines: ReturnLine[] }
interface Pick { text: string; productId: string; quantity: string; rate: string; bags?: string }
export interface GoodsReceipt { id: string; number: number; supplierId: string; purchaseOrderId?: string | null; receivedAt: string; lines: { productId: string; name: string; quantity: number; bags?: number | null; bagSize?: number | null }[]; note: string | null; purchaseId: string | null; by: string | null
  vehicleNo?: string | null; bilty?: string | null; transporter?: string | null; grossWeight?: number | null; tareWeight?: number | null }

const input = 'h-12 min-w-0 rounded-xl border border-line bg-white px-3'

/** Today's date as YYYY-MM-DD in the shop's own day, to compare with an order's expected date. */
export const todayIso = () => new Date(Date.now() - new Date().getTimezoneOffset() * 60_000).toISOString().slice(0, 10)
export const isLate = (o: PurchaseOrder) => o.status === 'open' && o.pending > 0 && !!o.expectedOn && o.expectedOn < todayIso()
const blank: Pick = { text: '', productId: '', quantity: '', rate: '' }

/** Rows of item + quantity (+ rate), found by typing. Only an exact name picks an item. */
function PickRows({ products, rows, onChange, withRate, listId, withBags = false }: { products: Product[]; rows: Pick[]; onChange: (rows: Pick[]) => void; withRate: boolean; listId: string; withBags?: boolean }) {
  const { t } = useTranslation()
  const choosable = products.filter((p) => !p.isService)
  const set = (i: number, next: Partial<Pick>) => onChange(rows.map((r, x) => (x === i ? { ...r, ...next } : r)))
  return (
    <>
      {rows.map((r, i) => (
        <div key={i} className={`grid gap-2 ${withRate ? 'grid-cols-[1fr_5rem_5rem]' : 'grid-cols-[1fr_5rem]'}`}>
          <ItemPicker products={choosable} text={r.text} placeholder={t('supplier2.itemSearch')} key={`${listId}-${i}`}
            onText={(text) => set(i, { text, productId: choosable.find((p) => p.name.toLowerCase() === text.trim().toLowerCase())?.id ?? '' })}
            onPick={(p) => set(i, { text: p.name, productId: p.id })} />
          <input aria-label={t('suppliers.qty')} placeholder={t('suppliers.qty')} value={r.quantity} onChange={(e) => set(i, { quantity: e.target.value })} inputMode="decimal" className={input} />
          {withRate && <input aria-label={t('supplier2.rate')} placeholder={t('supplier2.rate')} value={r.rate} onChange={(e) => set(i, { rate: e.target.value })} inputMode="decimal" className={input} />}
          {withBags && (() => {
            const p = choosable.find((x) => x.id === r.productId)
            if (!p?.bagSize) return null
            const avg = Number(r.bags) > 0 && Number(r.quantity) > 0 ? Number(r.quantity) / Number(r.bags) : null
            return (
              <div className="col-span-full flex flex-col gap-1">
                <input aria-label={t('bag.bags')} placeholder={`${t('bag.bags')} (${t('bag.bagsOf', { size: p.bagSize, unit: t(`units.${p.unit}`) })})`} value={r.bags ?? ''} onChange={(e) => set(i, { bags: e.target.value })} inputMode="numeric" className={input} />
                {avg !== null && <BagCheck avg={avg} size={p.bagSize} bags={Number(r.bags)} unit={t(`units.${p.unit}`)} />}
              </div>
            )
          })()}
        </div>
      ))}
      <button onClick={() => onChange([...rows, blank])} className="min-h-11 font-semibold text-teal-700">+ {t('suppliers.addRow')}</button>
    </>
  )
}

/** The message a supplier gets: the shop's order, item by item. */
export function orderText(t: (key: string, values?: Record<string, unknown>) => string, shopName: string, number: number, lines: { name: string; quantity: number }[]) {
  return t('supplier2.orderMessage', { shop: shopName, number, lines: lines.map((l) => `${l.name} × ${l.quantity}`).join('\n') })
}

/**
 * What the shop has asked this supplier for: open orders with what has come and what is still to come, a new order,
 * and "Goods came", which fills the bill below from what is still to come.
 */
export function OrdersPanel({ shopId, shopName, supplierId, phone, products, onGoodsCame }: {
  shopId: string; shopName: string; supplierId: string; phone: string | null; products: Product[]; onGoodsCame: (order: PurchaseOrder) => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const orders = useQuery({ queryKey: [shopId, 'suppliers', 'orders', supplierId], queryFn: () => api<PurchaseOrder[]>(`/v1/suppliers/orders?supplierId=${supplierId}`) }).data ?? []
  const [rows, setRows] = useState<Pick[] | null>(null)
  const [expectedOn, setExpectedOn] = useState('')
  const [note, setNote] = useState('')
  const [message, setMessage] = useState('')
  const valid = (rows ?? []).filter((r) => r.productId && Number(r.quantity) > 0)
  const refresh = () => queryClient.invalidateQueries({ queryKey: [shopId, 'suppliers'] })

  async function run(action: () => Promise<unknown>) {
    setMessage('')
    try {
      await action()
      await refresh()
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  const save = () => run(async () => {
    await api('/v1/suppliers/orders', 'POST', { id: crypto.randomUUID(), supplierId, lines: valid.map((r) => ({ productId: r.productId, quantity: Number(r.quantity), rate: r.rate === '' ? null : Number(r.rate) })), expectedOn: expectedOn || null, note: note.trim() || null })
    setRows(null)
    setExpectedOn('')
    setNote('')
  })
  const finish = async (order: PurchaseOrder, how: 'close' | 'cancel') => {
    if (await confirmAsk(t(how === 'close' ? 'supplier2.closeConfirm' : 'supplier2.cancelConfirm', { number: order.number }))) void run(() => api(`/v1/suppliers/orders/${order.id}/${how}`, 'POST', {}))
  }

  return (
    <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between">
        <h2 className="font-bold">{t('supplier2.orders')}</h2>
        {!rows && <button onClick={() => setRows([blank])} className="min-h-11 font-semibold text-teal-700">+ {t('supplier2.newOrder')}</button>}
      </div>
      {orders.length === 0 && !rows && <p className="text-sm text-muted">{t('supplier2.noOrders')}</p>}
      {orders.map((o) => {
        const left = o.lines.filter((l) => l.received < l.quantity)
        return (
          <div key={o.id} className="flex flex-col gap-1 rounded-xl border border-line p-3 text-sm">
            <p className="flex justify-between font-semibold"><span>{t('supplier2.orderNo', { number: o.number })} · {new Date(o.createdAt).toLocaleDateString('en-IN')}</span><span>{t('supplier2.stillToCome', { qty: o.pending })}</span></p>
            {(o.expectedOn || o.note) && <p className={`text-xs ${isLate(o) ? 'font-semibold text-due' : 'text-muted'}`}>{o.expectedOn && t(isLate(o) ? 'po.late' : 'po.expected', { date: new Date(o.expectedOn).toLocaleDateString('en-IN', { day: 'numeric', month: 'short' }) })}{o.expectedOn && o.note ? ' · ' : ''}{o.note}</p>}
            {o.lines.map((l) => (
              <p key={l.productId} className={`flex justify-between gap-2 ${l.received >= l.quantity ? 'text-muted' : ''}`}>
                <span>{l.name}{l.rate != null ? ` · ${formatRupees(l.rate)}` : ''}</span>
                <span>{t('supplier2.cameOf', { came: l.received, ordered: l.quantity })}</span>
              </p>
            ))}
            <div className="mt-1 flex flex-wrap gap-2">
              <Button className="flex-1" onClick={() => onGoodsCame(o)}>{t('supplier2.goodsCame')}</Button>
              {phone && <a href={whatsAppLink(phone, orderText(t, shopName, o.number, left.map((l) => ({ name: l.name, quantity: l.quantity - l.received }))))} target="_blank" rel="noreferrer"
                className="flex min-h-12 items-center rounded-2xl border border-line px-3 font-semibold">{t('supplier2.sendOrder')}</a>}
              <button onClick={() => void finish(o, o.lines.some((l) => l.received > 0) ? 'close' : 'cancel')} className="min-h-12 px-2 text-xs font-semibold text-due">
                {t(o.lines.some((l) => l.received > 0) ? 'supplier2.closeShort' : 'supplier2.cancelOrder')}
              </button>
            </div>
          </div>
        )
      })}
      {rows && (
        <div className="flex flex-col gap-2 rounded-xl bg-[#f1f5f4] p-3">
          <PickRows products={products} rows={rows} onChange={setRows} withRate listId="order-items" />
          <div className="grid grid-cols-2 gap-2">
            <label className="flex flex-col gap-1 text-xs text-muted">{t('po.expectedOn')}<input type="date" value={expectedOn} min={todayIso()} onChange={(e) => setExpectedOn(e.target.value)} className={input} /></label>
            <label className="flex flex-col gap-1 text-xs text-muted">{t('po.note')}<input value={note} maxLength={200} onChange={(e) => setNote(e.target.value)} placeholder={t('po.notePlaceholder')} className={input} /></label>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <Button disabled={valid.length === 0} onClick={() => void save()}>{t('supplier2.saveOrder')}</Button>
            <button onClick={() => setRows(null)} className="min-h-12 rounded-2xl border border-line font-semibold">{t('cancel')}</button>
          </div>
        </div>
      )}
      {message && <p className="text-sm text-due">{message}</p>}
    </section>
  )
}

/**
 * Goods going back to this supplier, any trade: stock leaves now, and what the shop owes comes down when their credit
 * note arrives. Medicines kept by batch go back from expiry returns instead.
 */
export function ReturnsPanel({ shopId, supplierId, products }: { shopId: string; supplierId: string; products: Product[] }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const open = useQuery({ queryKey: [shopId, 'suppliers', 'returns', supplierId], queryFn: () => api<SupplierReturn[]>(`/v1/suppliers/returns?supplierId=${supplierId}`) }).data ?? []
  const [rows, setRows] = useState<Pick[] | null>(null)
  const [credit, setCredit] = useState<Record<string, { no: string; amount: string }>>({})
  const [message, setMessage] = useState('')
  const valid = (rows ?? []).filter((r) => r.productId && Number(r.quantity) > 0)

  async function run(action: () => Promise<unknown>) {
    setMessage('')
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: [shopId, 'suppliers'] })
      void queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  const send = () => run(async () => {
    await api('/v1/suppliers/returns', 'POST', { id: crypto.randomUUID(), supplierId, lines: valid.map((r) => ({ productId: r.productId, quantity: Number(r.quantity) })) })
    setRows(null)
  })

  return (
    <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between">
        <h2 className="font-bold">{t('supplier2.returns')}</h2>
        {!rows && <button onClick={() => setRows([blank])} className="min-h-11 font-semibold text-teal-700">+ {t('supplier2.returnGoods')}</button>}
      </div>
      {open.map((r) => {
        const c = credit[r.id] ?? { no: '', amount: String(r.value) }
        return (
          <div key={r.id} className="flex flex-col gap-1 rounded-xl border border-line p-3 text-sm">
            <p className="flex justify-between font-semibold"><span>{t('supplier2.returnNo', { number: r.number, days: r.daysOpen })}</span><span>{formatRupees(r.value)}</span></p>
            {r.lines.map((l, i) => <p key={i} className="text-xs">{l.productName}{l.batchNo ? ` · ${l.batchNo}` : ''} × {l.quantity}</p>)}
            <div className="grid grid-cols-2 gap-2">
              <input aria-label={t('returns.creditNo')} placeholder={t('returns.creditNo')} value={c.no} onChange={(e) => setCredit({ ...credit, [r.id]: { ...c, no: e.target.value } })} className={input} />
              <input aria-label={t('returns.creditAmount')} placeholder={t('returns.creditAmount')} value={c.amount} onChange={(e) => setCredit({ ...credit, [r.id]: { ...c, amount: e.target.value } })} inputMode="decimal" className={input} />
            </div>
            <Button tone="plain" onClick={() => void run(() => api(`/v1/suppliers/returns/${r.id}/credit-note`, 'POST', { creditNoteNo: c.no.trim() || null, amount: Number(c.amount) || 0 }))}>{t('returns.settle')}</Button>
          </div>
        )
      })}
      {rows && (
        <div className="flex flex-col gap-2 rounded-xl bg-[#f1f5f4] p-3">
          <p className="text-xs text-muted">{t('supplier2.returnHint')}</p>
          <PickRows products={products} rows={rows} onChange={setRows} withRate={false} listId="return-items" />
          <div className="grid grid-cols-2 gap-2">
            <Button disabled={valid.length === 0} onClick={() => void send()}>{t('returns.send')}</Button>
            <button onClick={() => setRows(null)} className="min-h-12 rounded-2xl border border-line font-semibold">{t('cancel')}</button>
          </div>
        </div>
      )}
      {message && <p className="text-sm text-due">{message}</p>}
    </section>
  )
}

/**
 * Goods that came before their bill: counted onto the shelf now, nothing owed. When the bill comes, "make the bill"
 * fills the bill form with these goods and links them, so the stock isn't added twice.
 */
export function ReceiptsPanel({ shopId, supplierId, products, onBill }: { shopId: string; supplierId: string; products: Product[]; onBill: (receipt: GoodsReceipt, order: PurchaseOrder | null) => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const open = useQuery({ queryKey: [shopId, 'suppliers', 'receipts', supplierId], queryFn: () => api<GoodsReceipt[]>(`/v1/goods-receipts?supplierId=${supplierId}&open=true`) }).data ?? []
  const [rows, setRows] = useState<Pick[] | null>(null)
  const [message, setMessage] = useState('')
  const [trip, setTrip] = useState({ vehicleNo: '', bilty: '', transporter: '', gross: '', tare: '' })
  // Staff can't read orders (owner only), so the picker simply doesn't show for them.
  const orders = useQuery({ queryKey: [shopId, 'suppliers', 'orders', supplierId], queryFn: () => api<PurchaseOrder[]>(`/v1/suppliers/orders?supplierId=${supplierId}`), retry: false }).data ?? []
  const [orderId, setOrderId] = useState('')
  const order = orders.find((o) => o.id === orderId) ?? null
  const pickOrder = (id: string) => {
    setOrderId(id)
    const o = orders.find((x) => x.id === id)
    if (o) setRows(o.lines.filter((l) => l.received < l.quantity).map((l) => ({ ...blank, text: l.name, productId: l.productId, quantity: String(l.quantity - l.received) })))
  }
  // Against the order: goods not on it, and more than was still to come, are shown before saving (not blocked).
  const valid = (rows ?? []).filter((r) => r.productId && Number(r.quantity) > 0)
  const flags = order ? valid.map((r) => {
    const line = order.lines.find((l) => l.productId === r.productId)
    const name = products.find((p) => p.id === r.productId)?.name ?? r.text
    if (!line) return t('po.notOnOrder', { name })
    const over = Number(r.quantity) - Math.max(0, line.quantity - line.received)
    return over > 0 ? t('po.moreThanOrdered', { name, qty: Math.round(over * 1000) / 1000 }) : null
  }).filter((x): x is string => !!x) : []
  const net = Number(trip.gross) > 0 && Number(trip.tare) >= 0 && Number(trip.gross) > Number(trip.tare) ? Number(trip.gross) - Number(trip.tare) : null
  const linesKg = valid.reduce((sum, r) => sum + (products.find((p) => p.id === r.productId)?.unit === 'kg' ? Number(r.quantity) : 0), 0)

  async function run(action: () => Promise<unknown>) {
    setMessage('')
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: [shopId, 'suppliers'] })
      void queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  const save = () => run(async () => {
    await api('/v1/goods-receipts', 'POST', {
      id: crypto.randomUUID(), supplierId, orderId: order?.id ?? null, lines: valid.map((r) => ({ productId: r.productId, quantity: Number(r.quantity), bags: Number(r.bags) > 0 ? Number(r.bags) : null })),
      vehicleNo: trip.vehicleNo || null, bilty: trip.bilty || null, transporter: trip.transporter || null,
      grossWeight: net !== null ? Number(trip.gross) : null, tareWeight: net !== null ? Number(trip.tare) : null,
    })
    setRows(null)
    setOrderId('')
    setTrip({ vehicleNo: '', bilty: '', transporter: '', gross: '', tare: '' })
  })
  const cancel = async (r: GoodsReceipt) => { if (await confirmAsk(t('receipt.cancelConfirm', { number: r.number }))) void run(() => api(`/v1/goods-receipts/${r.id}/cancel`, 'POST', {})) }

  return (
    <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between">
        <h2 className="font-bold">{t('receipt.title')}</h2>
        {!rows && <button onClick={() => setRows([blank])} className="min-h-11 font-semibold text-teal-700">{t('receipt.add')}</button>}
      </div>
      {open.length === 0 && !rows && <p className="text-sm text-muted">{t('receipt.none')}</p>}
      {open.map((r) => (
        <div key={r.id} className="flex flex-col gap-1 rounded-xl border border-line p-3 text-sm">
          <p className="font-semibold">{t('receipt.number', { number: r.number, date: new Date(r.receivedAt).toLocaleDateString('en-IN', { day: 'numeric', month: 'short' }) })}{r.by ? ` · ${t('receipt.by', { name: r.by })}` : ''}{orders.find((o) => o.id === r.purchaseOrderId) ? ` · ${t('supplier2.orderNo', { number: orders.find((o) => o.id === r.purchaseOrderId)!.number })}` : ''}</p>
          {r.lines.map((l) => <p key={l.productId} className="text-xs">{l.name} × {l.quantity}{l.bags ? ` · ${t('bag.inBags', { count: l.bags })}` : ''}</p>)}
          {(r.vehicleNo || r.bilty) && <p className="text-xs text-muted">🚚 {[r.vehicleNo, r.bilty && `${t('bag.bilty')} ${r.bilty}`, r.transporter].filter(Boolean).join(' · ')}</p>}
          {r.grossWeight != null && r.tareWeight != null && <p className="text-xs text-muted">⚖ {t('bag.net', { net: r.grossWeight - r.tareWeight })}</p>}
          <div className="flex flex-wrap gap-3">
            <button onClick={() => onBill(r, orders.find((o) => o.id === r.purchaseOrderId) ?? null)} className="min-h-11 font-semibold text-teal-700">{t('receipt.makeBill')}</button>
            <button onClick={() => void cancel(r)} className="min-h-11 text-due">{t('receipt.cancel')}</button>
          </div>
        </div>
      ))}
      {rows && (
        <div className="flex flex-col gap-2 rounded-xl bg-[#f1f5f4] p-3">
          <p className="text-xs text-muted">{t('receipt.hint')}</p>
          {orders.some((o) => o.status === 'open') && (
            <label className="flex flex-col gap-1 text-xs text-muted">{t('po.againstOrder')}
              <select value={orderId} onChange={(e) => pickOrder(e.target.value)} className={input}>
                <option value="">{t('supplier2.clearOrder')}</option>
                {orders.filter((o) => o.status === 'open').map((o) => <option key={o.id} value={o.id}>{t('supplier2.orderNo', { number: o.number })} · {t('supplier2.stillToCome', { qty: o.pending })}</option>)}
              </select>
            </label>
          )}
          <PickRows products={products} rows={rows} onChange={setRows} withRate={false} listId="receipt-items" withBags />
          {flags.length > 0 && <ul className="rounded-xl bg-amber-50 p-2 text-xs text-amber-900">{flags.map((f) => <li key={f}>⚠ {f}</li>)}</ul>}
          <details className="rounded-xl bg-white p-3 text-sm">
            <summary className="cursor-pointer font-semibold">🚚 {t('bag.transport')}</summary>
            <div className="mt-2 grid grid-cols-2 gap-2">
              <input aria-label={t('bag.vehicle')} placeholder={t('bag.vehicle')} value={trip.vehicleNo} onChange={(e) => setTrip({ ...trip, vehicleNo: e.target.value })} className={input} />
              <input aria-label={t('bag.bilty')} placeholder={t('bag.bilty')} value={trip.bilty} onChange={(e) => setTrip({ ...trip, bilty: e.target.value })} className={input} />
              <input aria-label={t('bag.transporter')} placeholder={t('bag.transporter')} value={trip.transporter} onChange={(e) => setTrip({ ...trip, transporter: e.target.value })} className={`${input} col-span-2`} />
              <input aria-label={t('bag.gross')} placeholder={t('bag.gross')} value={trip.gross} onChange={(e) => setTrip({ ...trip, gross: e.target.value })} inputMode="decimal" className={input} />
              <input aria-label={t('bag.tare')} placeholder={t('bag.tare')} value={trip.tare} onChange={(e) => setTrip({ ...trip, tare: e.target.value })} inputMode="decimal" className={input} />
            </div>
            {net !== null && <p className="mt-2 font-semibold">⚖ {t('bag.net', { net })}{linesKg > 0 && Math.abs(linesKg - net) >= 1 ? <span className="text-due"> · {t('bag.netDiffers', { lines: linesKg, diff: Math.round(Math.abs(linesKg - net) * 100) / 100 })}</span> : null}</p>}
          </details>
          <div className="grid grid-cols-2 gap-2">
            <Button disabled={valid.length === 0} onClick={() => void save()}>{t('receipt.save')}</Button>
            <button onClick={() => setRows(null)} className="min-h-12 rounded-2xl border border-line font-semibold">{t('cancel')}</button>
          </div>
        </div>
      )}
      {message && <p className="text-sm text-due">{message}</p>}
    </section>
  )
}

/** Suppliers from a file or a paste: name, phone, GSTIN and what is owed, checked before anything is saved. */
export function SupplierImport({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [text, setText] = useState('')
  const [message, setMessage] = useState('')
  const parsed = parseSuppliers(text)

  async function save() {
    setMessage('')
    try {
      const result = await api<{ created: number; skipped: number; problems: string[] }>('/v1/suppliers/import', 'POST', { rows: parsed.rows })
      setText('')
      setMessage([t('supplier2.imported', { created: result.created, skipped: result.skipped }), ...result.problems].join('\n'))
      await queryClient.invalidateQueries({ queryKey: [shopId, 'suppliers'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  return (
    <details className="rounded-[18px] bg-white p-4 shadow-sm">
      <summary className="min-h-11 cursor-pointer font-bold">{t('supplier2.import')}</summary>
      <p className="mb-2 text-sm text-muted">{t('supplier2.importHint')}</p>
      <input type="file" accept=".csv,text/csv,text/plain" aria-label={t('supplier2.import')} className="mb-2 text-sm"
        onChange={(e) => { const file = e.target.files?.[0]; e.target.value = ''; if (file) void file.text().then(setText) }} />
      <textarea aria-label={t('supplier2.import')} value={text} onChange={(e) => setText(e.target.value)} rows={5} placeholder="Supplier name, Mobile, GSTIN, Balance" className="w-full rounded-xl border border-line p-3 text-sm" />
      {text.trim() && <p className="text-sm">{t('supplier2.importCount', { count: parsed.rows.length })}{parsed.errors.length > 0 ? ` · ${t('supplier2.importSkipped', { lines: parsed.errors.join(', ') })}` : ''}</p>}
      <Button className="mt-2 w-full" disabled={parsed.rows.length === 0} onClick={() => void save()}>{t('supplier2.importSave', { count: parsed.rows.length })}</Button>
      {message && <p className="mt-2 text-sm whitespace-pre-line">{message}</p>}
    </details>
  )
}

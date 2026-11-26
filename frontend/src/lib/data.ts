import { useQuery, type QueryClient } from '@tanstack/react-query'
import { currentShift } from './shift'
import { useEffect, useMemo, useState, useSyncExternalStore } from 'react'
import { api, NetworkError } from './api'
import { readCache, writeCache } from './db'
import { uuidv7 } from './ids'
import { mergeCustomers, mergeProducts, mergeStatement, mergeToday, round2, type LocalCustomer } from './local'
import { cancelPending, enqueue, getSyncState, listOps, setSyncedHandler, subscribeOutbox, type OutboxOp } from './outbox'
import { getSession, subscribeSession } from './session'
import type { Customer, KhataEntryType, PaymentMode, Product, Statement, StockReason, Today, Unit } from './types'

export function useSession() {
  return useSyncExternalStore(subscribeSession, getSession)
}

/** This shop's outbox, kept current as ops are added and synced. */
export function useOutbox(shopId: string) {
  const [ops, setOps] = useState<OutboxOp[]>([])
  const state = useSyncExternalStore(subscribeOutbox, getSyncState)
  useEffect(() => {
    let active = true
    const load = () => void listOps(shopId).then((next) => active && setOps(next))
    load()
    const unsubscribe = subscribeOutbox(load)
    return () => {
      active = false
      unsubscribe()
    }
  }, [shopId])
  return { ops, state, pending: ops.filter((o) => o.status === 'pending').length, failed: ops.filter((o) => o.status === 'failed').length }
}

/** From the server when online (and saved for later); the saved copy when offline. */
async function cachedGet<T>(shopId: string, key: string, path: string): Promise<T> {
  const cacheKey = `${shopId}:${key}`
  try {
    const data = await api<T>(path)
    await writeCache(cacheKey, data)
    return data
  } catch (error) {
    if (error instanceof NetworkError) {
      const cached = await readCache<T>(cacheKey)
      if (cached !== undefined) return cached
    }
    throw error
  }
}

export function useCustomers(shopId: string) {
  const query = useQuery({
    queryKey: [shopId, 'customers'],
    queryFn: () => cachedGet<Customer[]>(shopId, 'customers', '/v1/customers'),
  })
  const { ops } = useOutbox(shopId)
  const customers = useMemo(() => mergeCustomers(query.data ?? [], ops), [query.data, ops])
  return { ...query, customers }
}

export function useToday(shopId: string) {
  const query = useQuery({
    queryKey: [shopId, 'today'],
    queryFn: () => cachedGet<Today>(shopId, 'today', '/v1/dashboard/today'),
  })
  const { ops } = useOutbox(shopId)
  const today = useMemo(() => mergeToday(query.data, ops), [query.data, ops])
  return { ...query, today }
}

/** Product ID -> rank by sales in the last 30 days (0 = most sold). Empty offline until first loaded. */
export function usePopularOrder(shopId: string) {
  const query = useQuery({
    queryKey: [shopId, 'products', 'popular'],
    queryFn: () => cachedGet<Product[]>(shopId, 'popular', '/v1/products?sort=popular'),
  })
  return useMemo(() => new Map((query.data ?? []).map((p, i) => [p.id, i])), [query.data])
}

export function useProducts(shopId: string) {
  const query = useQuery({
    queryKey: [shopId, 'products'],
    queryFn: () => cachedGet<Product[]>(shopId, 'products', '/v1/products'),
  })
  const { ops } = useOutbox(shopId)
  const products = useMemo(() => mergeProducts(query.data ?? [], ops), [query.data, ops])
  return { ...query, products }
}

export function useStatement(shopId: string, customer: LocalCustomer | undefined) {
  const customerId = customer?.id
  const query = useQuery({
    queryKey: [shopId, 'statement', customerId],
    queryFn: () => cachedGet<Statement>(shopId, `statement:${customerId}`, `/v1/customers/${customerId}/statement`),
    // A customer added offline isn't on the server yet: nothing to fetch.
    enabled: customer !== undefined && !customer.isNew,
    retry: false,
  })
  const { ops } = useOutbox(shopId)
  const merged = useMemo(() => mergeStatement(query.data, customer, ops), [query.data, customer, ops])
  return useMemo(() => ({ ...merged, confirmed: query.data?.confirmed ?? null }), [merged, query.data])
}

/**
 * After the server accepts an op, folds its reply into the cached lists, so the screen doesn't jump
 * while the fresh copy loads, then refetches.
 */
export function installSyncHandler(queryClient: QueryClient) {
  setSyncedHandler((op, reply) => {
    const customersKey = [op.shopId, 'customers']
    const balance = (reply as { customerBalance?: number } | null)?.customerBalance
    if (op.kind === 'customer') {
      queryClient.setQueryData<Customer[]>(customersKey, (list = []) =>
        list.some((c) => c.id === op.id) ? list : [...list, reply as Customer])
    } else if (op.customerId && typeof balance === 'number') {
      queryClient.setQueryData<Customer[]>(customersKey, (list = []) =>
        list.map((c) => (c.id === op.customerId ? { ...c, balance } : c)))
    } else if (op.kind === 'product') {
      queryClient.setQueryData<Product[]>([op.shopId, 'products'], (list = []) =>
        list.some((p) => p.id === op.id) ? list : [...list, reply as Product])
    }

    void writeCache(`${op.shopId}:customers`, queryClient.getQueryData(customersKey))
    void queryClient.invalidateQueries({ queryKey: [op.shopId] })
  })
}

// ---- Writes: all go through the outbox, so they work the same online and offline. ----

export async function addCustomer(shopId: string, name: string, phone: string, nameLater = false) {
  const id = uuidv7()
  // "Name later": udhaar to someone who gave no name yet; the server numbers it ("Name later 3").
  await enqueue({ id, shopId, kind: 'customer', path: '/v1/customers', body: { id, name: nameLater ? 'Name later' : name, phone: phone || null, nameLater }, label: `+ ${nameLater ? 'Name later' : name}` })
  return id
}

export async function addKhataEntry(
  shopId: string,
  customer: { id: string; name: string },
  type: KhataEntryType,
  amount: number,
  paymentMode: PaymentMode,
  note: string,
  upiRef = '',
  cheque?: { no: string; bank: string; date: string },
) {
  const id = uuidv7()
  const isCheque = type === 'payment' && paymentMode === 'cheque' && !!cheque
  await enqueue({
    id,
    shopId,
    kind: 'khata',
    path: '/v1/khata-entries',
    body: {
      id,
      customerId: customer.id,
      type,
      amount,
      paymentMode: type === 'payment' ? paymentMode : null,
      note: note || null,
      occurredAt: new Date().toISOString(),
      upiRef: type === 'payment' && paymentMode === 'upi' && upiRef.trim() ? upiRef.trim() : null,
      chequeNo: isCheque ? cheque.no.trim() : null,
      chequeBank: isCheque && cheque.bank.trim() ? cheque.bank.trim() : null,
      chequeDate: isCheque && cheque.date ? cheque.date : null,
    },
    customerId: customer.id,
    delta: type === 'credit' ? amount : -amount,
    label: `${customer.name} ₹${amount} ${type === 'credit' ? 'udhaar' : 'jama'}`,
  })
  return id
}

/** Removes an entry by adding its reversal. After 15 minutes the server wants the owner and a reason (`note`). */
export async function reverseEntry(shopId: string, entry: { id: string; amount: number }, customer: { id: string; name: string }, note?: string) {
  const id = uuidv7()
  await enqueue({
    id,
    shopId,
    kind: 'reverse',
    path: `/v1/ledger-entries/${entry.id}/reverse`,
    body: note ? { id, note } : { id },
    customerId: customer.id,
    delta: -entry.amount,
    reversesEntryId: entry.id,
    label: `${customer.name} ₹${Math.abs(entry.amount)} hataya`,
  })
}

/** Undo right after saving: drops the entry if it hasn't synced yet, otherwise posts a reversal. */
export async function undoEntry(shopId: string, entry: { id: string; amount: number }, customer: { id: string; name: string }) {
  if (!(await cancelPending(shopId, entry.id))) await reverseEntry(shopId, entry, customer)
}

export interface CartLine {
  productId: string | null
  name: string
  quantity: number
  unit: Unit
  unitPrice: number
  /** Who sold this line (staff sales and commission); none: whoever makes the bill. */
  salespersonId?: string | null
  /** The planned offer applied to this line, and its name for the tag. */
  offerId?: string | null
  offerName?: string | null
  /** Sold as whole sealed bags (quantity is their weight). */
  bags?: number | null
}

export async function saveBill(
  shopId: string,
  lines: CartLine[],
  customer: { id: string; name: string } | null,
  paidAmount: number,
  paymentMode: PaymentMode,
  /** Other trades: the site, the vehicle, the painter. */
  extras: { withoutGst?: boolean; payments?: { mode: PaymentMode; amount: number; reference?: string | null }[]; site?: string | null; vehicleNumber?: string | null; painterId?: string | null; prescription?: { doctorName: string; doctorAddress: string | null; doctorRegNo: string | null; patientName: string; patientAddress: string | null } | null; serials?: { productId: string; serialNo: string }[]; exchangeNoteId?: string | null; exchangeCredit?: number; placeOfSupply?: string | null; entrySeconds?: number | null; godownId?: string | null; fields?: { label: string; value: string }[] } = {},
) {
  const id = uuidv7()
  const total = round2(lines.reduce((sum, l) => sum + round2(l.quantity * l.unitPrice), 0))
  await enqueue({
    id,
    shopId,
    kind: 'bill',
    // extras.withoutGst rides along in the body: a bill the shop chose to make without GST.
    path: '/v1/bills',
    body: { id, customerId: customer?.id ?? null, lines, paidAmount, paymentMode, occurredAt: new Date().toISOString(), counter: counterName() || null, shiftId: currentShift(shopId)?.id ?? null, ...extras, exchangeCredit: undefined },
    customerId: customer?.id,
    delta: customer ? round2(total - paidAmount - Math.min(extras.exchangeCredit ?? 0, total)) : undefined,
    customerName: customer?.name,
    label: `Bill ₹${total}${customer ? ` · ${customer.name}` : ''}`,
  })
  return { id, total }
}

export interface NewProduct {
  /** A barcode for the new item (from a scan no item had yet). */
  barcode?: string | null
  name: string
  unit: Unit
  sellingPrice: number
  costPrice: number | null
  category: string | null
  reorderLevel: number | null
  openingStock: number | null
  gstRate?: number | null
  hsnCode?: string | null
  isService?: boolean
  durationMinutes?: number | null
  mrp?: number | null
  billName?: string | null
  styleName?: string | null
  size?: string | null
  colour?: string | null
}

export async function addProduct(shopId: string, product: NewProduct) {
  const id = uuidv7()
  await enqueue({ id, shopId, kind: 'product', path: '/v1/products', body: { id, ...product }, label: `+ ${product.name}` })
  return id
}

/** Goods in from a supplier (+), or an adjustment (±) with a reason. Works offline. */
export async function addStockMovement(
  shopId: string,
  product: { id: string; name: string },
  kind: 'purchase' | 'adjustment',
  quantity: number,
  reason: StockReason | null,
  costPrice: number | null,
  note: string | null,
  bags: number | null = null,
) {
  const id = uuidv7()
  await enqueue({
    id,
    shopId,
    kind: 'stock',
    path: '/v1/stock-movements',
    body: { id, productId: product.id, kind, quantity, reason, costPrice, note, bags },
    label: `${product.name} ${quantity > 0 ? '+' : ''}${quantity}`,
  })
}

/** A kharcha line as the phone keeps it until it reaches the server. */
export interface ExpenseBody {
  id: string
  amount: number
  /** Null: the server chooses the heading from the note (a line read on the phone with no signal). */
  category: string | null
  note: string | null
  mode: 'cash' | 'upi'
  /** When it was written (the phone's time), so a line sent later still lands on its own day. */
  occurredAt: string
}

/** Saves a kharcha line on the phone first and sends it when there's a connection (safe to resend: same ID). */
export async function addExpense(shopId: string, line: Omit<ExpenseBody, 'id'>) {
  const id = uuidv7()
  await enqueue({ id, shopId, kind: 'expense', path: '/v1/expenses', body: { id, ...line }, label: `− ${line.note ?? line.category ?? ''} ₹${line.amount}` })
  return id
}

/** Strikes a kharcha line out (it stays on the page, crossed). Queued like every other write. */
export async function strikeExpense(shopId: string, id: string, label: string) {
  await enqueue({ id: `${id}:strike`, shopId, kind: 'expenseStrike', path: `/v1/expenses/${id}/strike`, body: {}, label: `✕ ${label}` })
}

/** This device's counter name ("Counter 2"), set by the shop on the device; bills made here carry it. */
export function counterName(): string {
  try { return localStorage.getItem('gk-counter') ?? '' } catch { return '' }
}

export function setCounterName(name: string) {
  try { if (name.trim()) localStorage.setItem('gk-counter', name.trim().slice(0, 30)); else localStorage.removeItem('gk-counter') } catch { /* no storage */ }
}

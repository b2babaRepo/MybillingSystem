import { useQuery } from '@tanstack/react-query'
import { useMemo } from 'react'
import { api, NetworkError } from './api'
import { useOutbox } from './data'
import { readCache, writeCache } from './db'
import { uuidv7 } from './ids'
import { enqueue, type OutboxOp } from './outbox'
import type { Unit } from './types'

// The household side. A household's outbox and cache are keyed "home:<userId>" instead of a shop ID,
// so the same offline machinery (save on the phone first, sync in order) works for lists and orders.

export type ListKind = 'monthly' | 'weekly'
export type Fulfilment = 'delivery' | 'pickup'
export type OrderPayment = 'cash' | 'upi' | 'khata'
export type OrderStatus = 'placed' | 'packed' | 'delivered' | 'cancelled'

export interface ListItem {
  id: string
  name: string
  quantity: number
  unit: Unit
  checked: boolean
  note: string | null
  /** Picked from the common catalogue; null when typed in by hand. */
  catalogueItemId?: number | null
  pending?: boolean
}

/** The common catalogue every shop and household shares. Each shop sets its own price. */
export interface CatalogueItem {
  id: number
  name: string
  nameEn: string
  unit: Unit
  category: string
  typicalPrice: number
}

export interface ShopPrice {
  catalogueItemId: number
  productId: string
  name: string
  unit: Unit
  price: number
  inStock: boolean
}

export interface ShoppingList {
  kind: ListKind
  items: ListItem[]
}

export interface ShopEntry {
  id: string
  name: string
  area: string | null
  address: string | null
}

export interface OrderLine {
  id: string
  lineNo: number
  name: string
  quantity: number
  unit: Unit
  note: string | null
  packedQuantity: number | null
  unitPrice: number | null
  removed: boolean
  shopNote: string | null
  amount: number | null
  productId?: string | null
  /** The exact option the household picked; null when the shop chooses. */
  requestedProductId?: string | null
  /** What the household asked for, as they asked ("आटा 5 kg"). */
  requestedName?: string | null
  preference?: ReplacePreference
  /** The line's price when sent; a packed price far from it waits for the household's OK. */
  expectedAmount?: number | null
  approval?: LineApproval
}

/** What the shop may do when packing a line: choose any option, ask first, or not replace. */
export type ReplacePreference = 'shop_chooses' | 'ask_me' | 'dont_replace'
export type LineApproval = 'none' | 'pending' | 'approved' | 'declined'

/** One of a shop's options for a common-list item (a brand, a pack size, loose), at the shop's price. */
export interface ShopOption {
  catalogueItemId: number
  productId: string
  name: string
  unit: Unit
  packSize: number | null
  packUnit: Unit | null
  price: number
  inStock: boolean
  isDefault: boolean
}

/** A household's choice for one list line at the chosen shop. productId null: the shop chooses. */
export interface LineChoice {
  productId: string | null
  preference: ReplacePreference
}

export interface Order {
  id: string
  shopId: string
  shopName: string
  customerName: string
  customerPhone: string
  listKind: ListKind
  fulfilment: Fulfilment
  slot: string | null
  payment: OrderPayment
  note: string | null
  status: OrderStatus
  finalAmount: number | null
  createdAt: string
  packedAt: string | null
  deliveredAt: string | null
  cancelledAt: string | null
  lines: OrderLine[]
  /** A household planner list, or a guest's cart from the online store. */
  source?: 'list' | 'store'
  address?: string | null
  /** Sent from this phone, not on the server yet. */
  pending?: boolean
}

export const homeKey = (userId: string) => `home:${userId}`

async function cachedGet<T>(key: string, name: string, path: string): Promise<T> {
  try {
    const data = await api<T>(path)
    await writeCache(`${key}:${name}`, data)
    return data
  } catch (error) {
    if (error instanceof NetworkError) {
      const cached = await readCache<T>(`${key}:${name}`)
      if (cached !== undefined) return cached
    }
    throw error
  }
}

/** Server lists + this phone's unsynced edits. Pure, for tests. */
export function mergeLists(server: ShoppingList[], ops: OutboxOp[]): ShoppingList[] {
  const lists: ShoppingList[] = (['monthly', 'weekly'] as const).map((kind) => ({
    kind,
    items: (server.find((l) => l.kind === kind)?.items ?? []).map((i) => ({ ...i })),
  }))
  for (const op of ops.filter((o) => o.status === 'pending')) {
    const list = lists.find((l) => l.kind === op.listKind)
    if (op.kind === 'listItem' && list) {
      const body = op.body as Omit<ListItem, 'id'>
      const index = list.items.findIndex((i) => i.id === op.id)
      const item = { id: op.id, ...body, note: body.note ?? null, pending: true }
      if (index >= 0) list.items[index] = item
      else list.items.push(item)
    } else if (op.kind === 'listItemDelete' && list) {
      list.items = list.items.filter((i) => i.id !== op.id)
    } else if (op.kind === 'order' && list) {
      // Sending clears the ticks (the server does the same when the order arrives).
      list.items = list.items.map((i) => ({ ...i, checked: false }))
    }
  }
  return lists
}

export function useLists(userId: string) {
  const key = homeKey(userId)
  const query = useQuery({ queryKey: [key, 'lists'], queryFn: () => cachedGet<ShoppingList[]>(key, 'lists', '/v1/me/lists') })
  const { ops } = useOutbox(key)
  const lists = useMemo(() => mergeLists(query.data ?? [], ops), [query.data, ops])
  return { ...query, lists }
}

export function useMyOrders(userId: string) {
  const key = homeKey(userId)
  const query = useQuery({ queryKey: [key, 'orders'], queryFn: () => cachedGet<Order[]>(key, 'orders', '/v1/me/orders') })
  const { ops } = useOutbox(key)
  const orders = useMemo(() => {
    const server = query.data ?? []
    const sending = ops
      .filter((o) => o.kind === 'order' && o.status === 'pending' && !server.some((s) => s.id === o.id))
      .map((o) => ({ ...(o.body as { draft: Order }).draft, pending: true }))
    return [...sending, ...server]
  }, [query.data, ops])
  return { ...query, orders }
}

/** Kept on the phone too, so items can be picked offline. */
export function useCatalogue(userId: string) {
  const key = homeKey(userId)
  return useQuery({ queryKey: ['catalogue'], queryFn: () => cachedGet<CatalogueItem[]>(key, 'catalogue', '/v1/catalogue'), staleTime: 3_600_000 })
}

/** One shop's prices for catalogue items, to estimate the list's total before sending. */
export function useShopPrices(shopId: string | undefined) {
  return useQuery({
    queryKey: ['shop-prices', shopId],
    queryFn: () => api<ShopPrice[]>(`/v1/shops/${shopId}/prices`),
    enabled: !!shopId,
    staleTime: 60_000,
  })
}

/** A shop's options for every common-list item, default first. */
export function useShopOptions(shopId: string | undefined) {
  return useQuery({
    queryKey: ['shop-options', shopId],
    queryFn: () => api<ShopOption[]>(`/v1/shops/${shopId}/options`),
    enabled: !!shopId,
    staleTime: 60_000,
  })
}

const BASE: Partial<Record<Unit, [number, number]>> = { kg: [1000, 1], g: [1, 1], litre: [1000, 2], ml: [1, 2], piece: [1, 3], packet: [1, 3] }

/**
 * How much of an option covers what was asked (same rule as the server's OrderLines.QuantityIn): the same unit as
 * is, or whole packs (5 kg asked, 5 kg bags: 1). Null when the units don't convert.
 */
export function quantityIn(quantity: number, unit: Unit, option: Pick<ShopOption, 'unit' | 'packSize' | 'packUnit'>): number | null {
  if (option.unit === unit) return quantity
  const asked = BASE[unit]
  if (option.packSize && option.packUnit) {
    const pack = BASE[option.packUnit]
    if (asked && pack && asked[1] === pack[1]) return Math.max(1, Math.ceil((quantity * asked[0]) / (option.packSize * pack[0])))
  }
  const per = BASE[option.unit]
  return asked && per && asked[1] === per[1] ? Math.round(((quantity * asked[0]) / per[0]) * 1000) / 1000 : null
}

/** The option a line would get at a shop: the household's pick, else the shop's default for that item. */
export function optionFor(item: ListItem, options: ShopOption[], choice: LineChoice | undefined) {
  const mine = options.filter((o) => o.catalogueItemId === item.catalogueItemId)
  return mine.find((o) => o.productId === choice?.productId) ?? mine.find((o) => o.isDefault) ?? mine[0]
}

/** What the list would cost at a shop with these choices, and how many lines the shop will price itself. */
export function estimateAtShop(items: ListItem[], options: ShopOption[], choices: Record<string, LineChoice> = {}) {
  let total = 0
  let priced = 0
  for (const item of items) {
    const option = optionFor(item, options, choices[item.id])
    const quantity = option ? quantityIn(item.quantity, item.unit, option) : null
    if (option && quantity !== null) {
      total += option.price * quantity
      priced++
    }
  }
  return { total: Math.round(total * 100) / 100, priced, unpriced: items.length - priced }
}

/** The household's answer to a line the shop changed: OK, or leave it out. Needs the network. */
export async function answerChangedLine(orderId: string, lineId: string, approve: boolean) {
  return api<Order>(`/v1/me/orders/${orderId}/lines/${lineId}/approve`, 'POST', { approve })
}

/** Catalogue items whose Hindi or English name contains the text. */
export function searchCatalogue(catalogue: CatalogueItem[], text: string) {
  const q = text.trim().toLowerCase()
  if (!q) return []
  return catalogue.filter((i) => i.name.toLowerCase().includes(q) || i.nameEn.toLowerCase().includes(q)).slice(0, 8)
}

/** Shops to send a list to. Needs the network: there is nothing useful to show from yesterday's search. */
export function useShopSearch(search: string) {
  return useQuery({
    queryKey: ['shops', search.trim()],
    queryFn: () => api<ShopEntry[]>(`/v1/shops?search=${encodeURIComponent(search.trim())}`),
    staleTime: 60_000,
  })
}

export async function saveItem(userId: string, kind: ListKind, item: Omit<ListItem, 'pending'>) {
  const { id, ...body } = item
  await enqueue({
    id,
    shopId: homeKey(userId),
    kind: 'listItem',
    method: 'PUT',
    listKind: kind,
    path: `/v1/me/lists/${kind}/items/${id}`,
    body,
    label: `${item.name} ${item.quantity}`,
  })
}

export async function addItem(userId: string, kind: ListKind, name: string, quantity: number, unit: Unit, catalogueItemId: number | null = null) {
  await saveItem(userId, kind, { id: uuidv7(), name, quantity, unit, checked: false, note: null, catalogueItemId })
}

export async function removeItem(userId: string, kind: ListKind, item: ListItem) {
  await enqueue({
    id: item.id,
    shopId: homeKey(userId),
    kind: 'listItemDelete',
    method: 'DELETE',
    listKind: kind,
    path: `/v1/me/lists/${kind}/items/${item.id}`,
    body: null,
    label: `− ${item.name}`,
  })
}

export interface SendChoice {
  shop: ShopEntry
  kind: ListKind
  fulfilment: Fulfilment
  slot: string
  payment: OrderPayment
  note: string
}

/**
 * Queues the order. The body carries a draft copy of the order too, so the Orders screen can show it as
 * "sending" while offline; the server ignores fields it doesn't know.
 */
export async function sendList(userId: string, userName: string, items: ListItem[], choice: SendChoice, lineChoices: Record<string, LineChoice> = {}) {
  const id = uuidv7()
  const now = new Date().toISOString()
  const draft: Order = {
    id,
    shopId: choice.shop.id,
    shopName: choice.shop.name,
    customerName: userName,
    customerPhone: '',
    listKind: choice.kind,
    fulfilment: choice.fulfilment,
    slot: choice.slot || null,
    payment: choice.payment,
    note: choice.note || null,
    status: 'placed',
    finalAmount: null,
    createdAt: now,
    packedAt: null,
    deliveredAt: null,
    cancelledAt: null,
    lines: items.map((i, index) => ({
      id: i.id,
      lineNo: index + 1,
      name: i.name,
      quantity: i.quantity,
      unit: i.unit,
      note: i.note,
      packedQuantity: null,
      unitPrice: null,
      removed: false,
      shopNote: null,
      amount: null,
    })),
  }
  await enqueue({
    id,
    shopId: homeKey(userId),
    kind: 'order',
    listKind: choice.kind,
    path: '/v1/me/orders',
    body: {
      id,
      shopId: choice.shop.id,
      kind: choice.kind,
      fulfilment: choice.fulfilment,
      slot: choice.slot || null,
      payment: choice.payment,
      note: choice.note || null,
      choices: Object.entries(lineChoices).map(([listItemId, c]) => ({ listItemId, productId: c.productId, preference: c.preference })),
      draft,
    },
    label: `${choice.shop.name} · ${items.length}`,
  })
  return id
}

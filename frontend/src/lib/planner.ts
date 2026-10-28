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

/** What the list would cost at a shop: priced lines in the same unit, and how many the shop will price itself. */
export function estimateAtShop(items: ListItem[], prices: ShopPrice[]) {
  const byItem = new Map(prices.map((p) => [p.catalogueItemId, p]))
  let total = 0
  let priced = 0
  for (const item of items) {
    const price = item.catalogueItemId ? byItem.get(item.catalogueItemId) : undefined
    if (price && price.unit === item.unit) {
      total += price.price * item.quantity
      priced++
    }
  }
  return { total: Math.round(total * 100) / 100, priced, unpriced: items.length - priced }
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
export async function sendList(userId: string, userName: string, items: ListItem[], choice: SendChoice) {
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
      draft,
    },
    label: `${choice.shop.name} · ${items.length}`,
  })
  return id
}

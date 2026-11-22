import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './api'
import type { ShopEntry } from './planner'
import type { Unit } from './types'

// A signed-in customer's account data: carts kept on the server (one per shop) and saved shops.

export interface CartItem { productId: string; name: string; unit: Unit; price: number; quantity: number; available: number; imageAt: string | null }
export interface Cart { shopId: string; shopName: string; shopSlug: string; items: CartItem[]; total: number; updatedAt: string }
export interface SavedShop { id: string; name: string; slug: string; area: string | null; address: string | null; type: string; number: number; savedAt: string }
export type CartLines = Record<string, number>

export const useCarts = (enabled = true) => useQuery({ queryKey: ['me', 'carts'], queryFn: () => api<Cart[]>('/v1/me/carts'), enabled, retry: false })
export const useSavedShops = (enabled = true) => useQuery({ queryKey: ['me', 'shops'], queryFn: () => api<SavedShop[]>('/v1/me/shops'), enabled, retry: false })

/** Replaces one shop's cart on the server (empty removes it). */
export async function putCart(shopId: string, lines: CartLines) {
  await api(`/v1/me/carts/${shopId}`, 'PUT', { lines: Object.entries(lines).filter(([, q]) => q > 0).map(([productId, quantity]) => ({ productId, quantity })) })
}

/** Adds items to a shop's cart on the server, keeping what is already in it (the larger quantity wins). */
export async function addToCart(shopId: string, add: CartLines) {
  const carts = await api<Cart[]>('/v1/me/carts')
  const current: CartLines = Object.fromEntries((carts.find((c) => c.shopId === shopId)?.items ?? []).map((i) => [i.productId, i.quantity]))
  for (const [id, q] of Object.entries(add)) current[id] = Math.max(current[id] ?? 0, q)
  await putCart(shopId, current)
}

export function useSaveShop() {
  const queryClient = useQueryClient()
  return async (shop: { id: string }, save: boolean) => {
    await api(`/v1/me/shops/${shop.id}`, save ? 'PUT' : 'DELETE', save ? {} : undefined)
    await queryClient.invalidateQueries({ queryKey: ['me', 'shops'] })
  }
}

/** A saved shop as the shop pickers and lists use it. */
export const asShopEntry = (s: SavedShop): ShopEntry => ({ id: s.id, name: s.name, area: s.area, address: s.address, type: s.type, slug: s.slug, number: s.number })

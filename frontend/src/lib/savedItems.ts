import { useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from './api'

// Hearts: items a signed-in customer saved at a shop. One out of stock gets a notification when it is back.

export interface SavedItem { shopId: string; shopName: string; shopSlug: string; productId: string; name: string; price: number; available: number; imageAt: string | null; savedAt: string }

export const useSavedItems = (enabled = true) =>
  useQuery({ queryKey: ['me', 'saved-items'], queryFn: () => api<SavedItem[]>('/v1/me/saved-items'), enabled, retry: false })

/** Saves or removes a heart, showing the change at once and putting it back if the server says no. */
export function useHeart() {
  const queryClient = useQueryClient()
  return async (shopId: string, productId: string, save: boolean) => {
    const key = ['me', 'saved-items']
    const before = queryClient.getQueryData<SavedItem[]>(key)
    queryClient.setQueryData<SavedItem[]>(key, (all = []) => save
      ? [...all, { shopId, productId, shopName: '', shopSlug: '', name: '', price: 0, available: 1, imageAt: null, savedAt: new Date().toISOString() }]
      : all.filter((x) => !(x.shopId === shopId && x.productId === productId)))
    try {
      await api(`/v1/me/saved-items/${shopId}/${productId}`, save ? 'PUT' : 'DELETE', save ? {} : undefined)
    } catch {
      queryClient.setQueryData(key, before)
    }
    void queryClient.invalidateQueries({ queryKey: key })
  }
}

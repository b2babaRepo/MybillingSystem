import { useQuery } from '@tanstack/react-query'
import { api } from './api'

/** Whether the shop's own WhatsApp Business number is connected (anyone at the counter may ask). */
export function useShopWhatsApp() {
  return useQuery({ queryKey: ['wa-business-on'], queryFn: () => api<{ connected: boolean }>('/v1/whatsapp/business/on'), staleTime: 5 * 60_000, retry: false }).data?.connected ?? false
}

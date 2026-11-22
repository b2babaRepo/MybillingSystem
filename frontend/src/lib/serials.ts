import { useQuery } from '@tanstack/react-query'
import { api } from './api'

export interface Serial {
  id: string; productId: string; productName: string; serialNo: string; status: 'in_stock' | 'sold'; receivedOn: string; billNo: string | null
  customerName: string | null; soldOn: string | null; warrantyUntil: string | null; inWarranty: boolean; warrantyRegistered: boolean
}

/** In-stock serials of one item, for the bill's picker. Here, not on the serials screen, so the bill doesn't load that screen. */
export function useSerialsInStock(shopId: string, productId: string | null) {
  return useQuery({
    queryKey: [shopId, 'serials', productId, 'in_stock'], enabled: !!productId,
    queryFn: () => api<Serial[]>(`/v1/serials?productId=${productId}&status=in_stock`),
  }).data ?? []
}

import { useQuery } from '@tanstack/react-query'
import { api } from './api'

/** A market (a bazaar or area) on the platform's list, with how many shops are in it. */
export interface MarketRow { id: string; name: string; state: string | null; district: string | null; city: string | null; village: string | null; isActive: boolean; shops: number }

/** Where a market is: village, city, district, state, whichever are known. */
export const marketPlace = (m: MarketRow) => [m.village, m.city, m.district, m.state].filter(Boolean).join(', ')

/** The platform's markets (the admin screens: the market list and opening a shop). */
export function useMarkets() {
  return useQuery({ queryKey: ['platform', 'markets'], queryFn: () => api<MarketRow[]>('/v1/platform/markets') })
}

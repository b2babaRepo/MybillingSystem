import { useQuery } from '@tanstack/react-query'
import { api } from './api'

// Salon visits for a signed-in customer: book from the salon's page, see what's coming, book the last visit again.

export interface VisitService { productId: string; name: string; price: number; minutes: number }
export interface MyVisit {
  id: string; shopId: string; shopName: string; shopSlug: string | null; date: string; start: string; end: string
  status: string; services: VisitService[]; total: number; upcoming: boolean
}

export const useVisits = (enabled = true) =>
  useQuery({ queryKey: ['me', 'salon-visits'], queryFn: () => api<MyVisit[]>('/v1/me/salon/visits'), enabled, retry: false })

export const bookVisit = (shopId: string, booking: { id: string; date: string; start: string; serviceIds: string[]; note: string }) =>
  api(`/v1/me/salon/${shopId}/appointments`, 'POST', booking)

export const cancelVisit = (shopId: string, id: string) => api(`/v1/me/salon/${shopId}/appointments/${id}/cancel`, 'POST', {})

/** "Book again" from Home: the services to tick when the salon's page opens. */
const againKey = (slug: string) => `salon.again.${slug}`
export function setBookAgain(slug: string, serviceIds: string[]) {
  try { sessionStorage.setItem(againKey(slug), JSON.stringify(serviceIds)) } catch { /* storage off: opens unticked */ }
}
export function takeBookAgain(slug: string): string[] | null {
  try {
    const raw = sessionStorage.getItem(againKey(slug))
    sessionStorage.removeItem(againKey(slug))
    return raw ? (JSON.parse(raw) as string[]) : null
  } catch { return null }
}

/** "11:00" from "11:00:00". */
export const hhmm = (t: string) => t.slice(0, 5)

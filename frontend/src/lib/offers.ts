import { useQuery } from '@tanstack/react-query'
import { api } from './api'
import { round2 } from './local'
import { runsAt, type PromotionKind } from './promotions'

export interface SaleOffer {
  id: string
  name: string
  kind: PromotionKind
  value: number
  startsAt: string
  endsAt: string
  scope: 'all' | 'category' | 'items'
  category: string | null
  productIds: string[]
  stoppedAt: string | null
  stats: { bills: number; quantity: number; sales: number; saved: number } | null
  buyQty?: number | null
  getQty?: number | null
  minQty?: number | null
  minBill?: number | null
  comboPrice?: number | null
  days?: number | null
  fromMinute?: number | null
  toMinute?: number | null
}

/** A price per piece the shelf can show (percent or rupees off); the other kinds work on the whole bill (lib/promotions). */
export const perPiece = (o: Pick<SaleOffer, 'kind'>) => o.kind === 'percent' || o.kind === 'amount'

/** One piece's price under the offer, from the item's regular price; never below zero. The server works it out the same way. */
export function offerPrice(offer: Pick<SaleOffer, 'kind' | 'value'>, regular: number) {
  return Math.max(0, offer.kind === 'percent' ? round2(regular * (1 - offer.value / 100)) : round2(regular - offer.value))
}

/** The offer is running at this moment (started, not ended, not stopped, on its days and hours). */
export function isRunning(offer: Pick<SaleOffer, 'startsAt' | 'endsAt' | 'stoppedAt' | 'days' | 'fromMinute' | 'toMinute'>, now: number) {
  return runsAt(offer, new Date(now))
}

/** Of the offers running now that cover this item, the one that gives the customer the lowest price. */
export function bestOffer(offers: SaleOffer[], product: { id: string; category: string | null; sellingPrice: number }, now = Date.now()) {
  const covering = offers.filter((o) => perPiece(o) && isRunning(o, now)
    && (o.scope === 'all' || (o.scope === 'category' && !!product.category && o.category?.toLowerCase() === product.category.toLowerCase()) || (o.scope === 'items' && o.productIds.includes(product.id))))
  return covering.sort((a, b) => offerPrice(a, product.sellingPrice) - offerPrice(b, product.sellingPrice))[0] ?? null
}

/** The shop's offers, kept on the phone too, so the bill applies them offline. */
export function useOffers(shopId: string) {
  const key = `offers:${shopId}`
  return useQuery({
    queryKey: [shopId, 'offers'],
    queryFn: async () => {
      try {
        const offers = await api<SaleOffer[]>('/v1/offers')
        try { localStorage.setItem(key, JSON.stringify(offers)) } catch { /* storage full or blocked: they still work while online */ }
        return offers
      } catch (e) {
        try { const saved = localStorage.getItem(key); if (saved) return JSON.parse(saved) as SaleOffer[] } catch { /* nothing saved */ }
        throw e
      }
    },
    staleTime: 60_000,
  })
}

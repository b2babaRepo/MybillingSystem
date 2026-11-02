import { useQuery } from '@tanstack/react-query'
import { api } from './api'

export type SubscriptionStatus = 'trial' | 'active' | 'grace' | 'complimentary' | 'on_hold' | 'free'
export type PlanTerm = 'monthly' | 'year1' | 'year3' | 'year5'

export interface PlanInfo {
  code: string
  name: string
  priceMonthly: number | null
  priceYear1: number | null
  priceYear3: number | null
  /** The 5-year price; paid a fifth each year. */
  priceYear5: number | null
  features: string[]
  maxCustomers: number | null
  maxStaff: number
  /** Owners allowed (full access). */
  maxOwners?: number
  whatsAppPerMonth: number
  /** On sale (admins see plans taken off sale too). */
  isActive?: boolean
  /** Admins: how many shops are on it today. */
  shops?: number
}

export const FEATURES = ['reports', 'store', 'whatsapp', 'staff', 'bill_photo', 'gst', 'trade', 'ai'] as const

/** Every plan, for admins: on sale or not, with its shop count. */
export function usePlans() {
  return useQuery({ queryKey: ['platform', 'plans'], queryFn: () => api<PlanInfo[]>('/v1/platform/plans'), staleTime: 60_000 })
}

export interface Payment {
  id: string
  planCode: string
  term: PlanTerm
  amount: number
  method: 'upi' | 'cash' | 'gateway' | 'bank'
  reference: string | null
  offerCode: string | null
  paidAt: string
  periodStart: string
  periodEnd: string
  invoiceNo: string
  refundedAt: string | null
  /** Opens the bill without signing in: /bill/plan/{id}?t={billToken}. */
  billToken?: string | null
}

export interface Claim {
  id: string
  shopId: string
  shopName: string | null
  planCode: string
  term: PlanTerm
  amount: number
  upiRef: string
  offerCode: string | null
  status: 'pending' | 'confirmed' | 'rejected'
  createdAt: string
  note: string | null
}

export interface SubscriptionView {
  status: SubscriptionStatus
  planCode: string
  planName: string
  endsAt: string | null
  daysLeft: number | null
  features: string[]
  maxCustomers: number | null
  maxStaff: number
  whatsAppPerMonth: number
  compReason: string | null
  holdReason: string | null
  chosenPlanCode?: string | null
  chosenTerm?: PlanTerm | null
  onlinePay?: boolean
  priceLockUntil: string | null
  plans: PlanInfo[]
  payments: Payment[]
  pendingClaim: Claim | null
  payToUpi: string | null
  payeeName: string
  monthlyAvailable: boolean
  gstRegistered: boolean
  referralCode: string
  referralBonusDays: number
  /** The shop's registration ID, "GKS-000042". */
  shopCode?: string | null
}

export interface Quote { planCode: string; term: PlanTerm; amount: number; offerCode: string | null; error: string | null }

/** The price a plan shows for a term (5 years: the yearly payment of the locked rate). */
export function priceFor(plan: PlanInfo, term: PlanTerm): number | null {
  if (term === 'monthly') return plan.priceMonthly
  if (term === 'year1') return plan.priceYear1
  if (term === 'year3') return plan.priceYear3
  return plan.priceYear5 === null ? null : Math.round(plan.priceYear5 / 5)
}

/** GST billing is on the shop's plan. True until the plan is known (offline), so nothing disappears by mistake. */
export function useGstBilling(shopId: string) {
  const features = useSubscription(shopId).data?.features
  return !features || features.includes('gst')
}

export function useSubscription(shopId: string, enabled = true) {
  return useQuery({ queryKey: [shopId, 'subscription'], queryFn: () => api<SubscriptionView>('/v1/subscription'), enabled, staleTime: 60_000 })
}

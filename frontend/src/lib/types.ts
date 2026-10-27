// Shapes of the API's JSON (camelCase, enums in snake_case). Keep in step with backend/src/GroceryKhata.Api.

export type ShopRole = 'owner' | 'staff'
export type Unit = 'piece' | 'kg' | 'g' | 'litre' | 'ml' | 'packet'
export type PaymentMode = 'cash' | 'upi'
export type LedgerEntryKind = 'credit' | 'payment' | 'reversal'
export type KhataEntryType = 'credit' | 'payment'

export interface MembershipSummary {
  membershipId: string
  shopId: string
  shopName: string
  shopSlug: string
  role: ShopRole
}

export interface AuthResponse {
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
  user: { id: string; fullName: string; preferredLanguage: string }
  memberships: MembershipSummary[]
  shopId: string | null
  role: ShopRole | null
}

export interface Customer {
  id: string
  name: string
  phone: string | null
  balance: number
}

export interface Product {
  id: string
  name: string
  unit: Unit
  sellingPrice: number
  costPrice: number | null
}

export interface StatementLine {
  id: string
  kind: LedgerEntryKind
  amount: number
  paymentMode: PaymentMode | null
  billId: string | null
  reversesEntryId: string | null
  note: string | null
  occurredAt: string
  runningBalance: number
  isReversed: boolean
}

export interface Statement {
  customer: Customer
  entries: StatementLine[]
}

export interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

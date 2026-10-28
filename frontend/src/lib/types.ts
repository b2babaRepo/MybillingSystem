// Shapes of the API's JSON (camelCase, enums in snake_case). Keep in step with backend/src/GroceryKhata.Api.

export type ShopRole = 'owner' | 'staff'
export type Unit = 'piece' | 'kg' | 'g' | 'litre' | 'ml' | 'packet'
export type PaymentMode = 'cash' | 'upi'
export type LedgerEntryKind = 'credit' | 'payment' | 'reversal' | 'return'
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
  category: string | null
  unit: Unit
  /** 0 = price not set yet. */
  sellingPrice: number
  costPrice: number | null
  stock: number
  reorderLevel: number | null
  /** Out of stock, or at/below the reorder level. */
  isLow: boolean
  /** Held for online orders not yet delivered. */
  reserved?: number
  /** The common-list item this is (for its picture, and so households find it). */
  catalogueItemId?: number | null
  /** What one pack holds, for an option sold by the pack (5 kg bag). */
  packSize?: number | null
  packUnit?: Unit | null
  /** Given when a household leaves the choice to the shop. */
  isDefaultOption?: boolean
  /** Has stock changes on this phone not yet on the server. */
  pending?: boolean
}

export type StockMovementKind = 'opening' | 'purchase' | 'sale' | 'adjustment'
export type StockReason = 'damage' | 'wastage' | 'count_correction' | 'return' | 'other'

export interface StockMovement {
  id: string
  productId: string
  kind: StockMovementKind
  quantity: number
  reason: StockReason | null
  billId: string | null
  costPrice: number | null
  note: string | null
  occurredAt: string
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
  /** The items behind a bill's udhaar. */
  bill?: BillSummary | null
  /** Who wrote the line; on a removal, who removed it. */
  recordedBy?: string | null
  /** The bank reference of a UPI payment: such a payment can't be removed. */
  upiRef?: string | null
  /** The latest dispute the customer raised on this line. */
  dispute?: DisputeView | null
}

export interface DisputeView {
  id: string
  message: string
  raisedAt: string
  resolution: string | null
  resolvedAt: string | null
  items: DisputeItemView[]
}

export type ItemProblem = 'not_received' | 'short' | 'wrong'
export type ItemOutcome = 'handed_over' | 'refunded' | 'disagreed'

/** One bill item the customer questioned, and what the shop did. */
export interface DisputeItemView {
  id: string
  billLineId: string
  name: string
  quantity: number
  unit: Unit
  unitPrice: number
  problem: ItemProblem
  receivedQuantity: number | null
  disputedAmount: number
  outcome: ItemOutcome | null
  outcomeNote: string | null
  resolvedAt: string | null
}

export interface BillSummary {
  total: number
  paid: number
  items: { id: string; name: string; quantity: number; unit: Unit; unitPrice: number; amount: number }[]
}

export interface Statement {
  customer: Customer
  entries: StatementLine[]
  /** Owed before the month (previous dues carried forward); 0 for the whole khata. */
  openingBalance: number
  closingBalance: number
  /** "2026-10", or null for everything. */
  month: string | null
}

export interface CashBookLine {
  occurredAt: string
  source: 'bill' | 'khata_payment' | 'reversal'
  who: string
  mode: PaymentMode
  amount: number
  billId: string | null
}

export interface CashBook {
  date: string
  cash: number
  upi: number
  total: number
  salesTotal: number
  udhaarGiven: number
  lines: CashBookLine[]
}

export interface BillLineView {
  productId: string | null
  name: string
  quantity: number
  unit: Unit
  unitPrice: number
  amount: number
}

export interface BillView {
  bill: {
    id: string
    customerId: string | null
    total: number
    paidAmount: number
    onKhata: number
    paymentMode: PaymentMode | null
    occurredAt: string
    lines: BillLineView[]
    customerBalance: number | null
  }
  customer: Customer | null
}

export interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

export interface Today {
  date: string
  sales: number
  bills: number
  cash: number
  upi: number
  udhaar: number
  outstanding: number
  customersOwing: number
  topDues: Customer[]
}

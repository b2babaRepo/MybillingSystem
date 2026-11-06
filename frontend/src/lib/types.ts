// Shapes of the API's JSON (camelCase, enums in snake_case). Keep in step with backend/src/GroceryKhata.Api.

export type ShopRole = 'owner' | 'staff'
/** A platform admin's role: super (everything), approver (approve and block shops), support (notes, unlocking). */
export type AdminRole = 'super' | 'approver' | 'support' | 'billing' | 'agent_manager'

export type Unit = 'piece' | 'kg' | 'g' | 'litre' | 'ml' | 'packet' | 'pair' | 'dozen' | 'box' | 'jar' | 'bag' | 'tonne' | 'brass' | 'cft' | 'metre' | 'sq_ft' | 'running_ft'
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
  /** phone and isPlatformAdmin: missing on sessions saved before they were added. */
  user: { id: string; fullName: string; preferredLanguage: string; phone?: string; isPlatformAdmin?: boolean; platformRole?: AdminRole | null }
  memberships: MembershipSummary[]
  shopId: string | null
  role: ShopRole | null
  /** Shops of this person that a platform admin blocked, and why. */
  blockedShops?: { shopName: string; reason: string }[] | null
}

export interface Customer {
  id: string
  name: string
  phone: string | null
  balance: number
  /** The most they may owe: a bill that would cross it warns the shop. */
  creditLimit?: number | null
}

export interface Product {
  id: string
  name: string
  category: string | null
  unit: Unit
  /** GST % on this item (prices include it); null when not set. */
  gstRate?: number | null
  hsnCode?: string | null
  /** 0 = price not set yet. */
  sellingPrice: number
  costPrice: number | null
  stock: number
  reorderLevel: number | null
  /** Out of stock, or at/below the reorder level. */
  isLow: boolean
  /** A service (haircut): sold without stock. */
  isService?: boolean
  durationMinutes?: number | null
  /** Medicines: the drug schedule; H, H1 and X need a prescription. Null: over the counter. */
  schedule?: DrugSchedule | null
  /** Medicines: tablets in a strip; a loose sale is that fraction of a strip. */
  unitsPerPack?: number | null
  /** Sales & service: each unit has a serial number, picked on the bill. */
  tracksSerials?: boolean
  warrantyMonths?: number | null
  mrp?: number | null
  /** Printed on the customer's bill instead of the name ("Innerwear"); see lib/billNames.ts. */
  billName?: string | null
  /** Clothes: the design this item is one size/colour of ("पुरुष जींस"); see lib/styles.ts. */
  styleName?: string | null
  size?: string | null
  colour?: string | null
  barcode?: string | null
  imageCredit?: string | null
  photoReports?: number
  photoReportNote?: string | null
  /** Held for online orders not yet delivered. */
  reserved?: number
  /** The common-list item this is (for its picture, and so households find it). */
  catalogueItemId?: number | null
  /** What one pack holds, for an option sold by the pack (5 kg bag). */
  packSize?: number | null
  packUnit?: Unit | null
  /** Given when a household leaves the choice to the shop. */
  isDefaultOption?: boolean
  /** When the shop added its photo; none: the app draws a picture. */
  imageAt?: string | null
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
  /** Written more than a day after the date it carries. */
  enteredLate?: boolean
  /** When it was actually written down. */
  recordedAt?: string | null
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
  source: 'bill' | 'khata_payment' | 'reversal' | 'expense' | 'technician'
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
  spentCash?: number
  spentUpi?: number
  netCash?: number
}

export interface BillLineView {
  /** Medicines: the batches it came from, "B123 exp 05/27". */
  batch?: string | null
  productId: string | null
  name: string
  quantity: number
  unit: Unit
  unitPrice: number
  amount: number
  gstRate?: number | null
  hsnCode?: string | null
  taxableValue?: number | null
  cgst?: number | null
  sgst?: number | null
  igst?: number | null
  /** The item's regular price when the line was sold below it (a discount). */
  listPrice?: number | null
  /** What the customer's bill shows instead of the name ("Innerwear"). */
  printName?: string | null
}

export type DrugSchedule = 'g' | 'h' | 'h1' | 'x'

export interface Prescription { doctorName: string; doctorAddress: string | null; doctorRegNo: string | null; patientName: string; patientAddress: string | null; prescribedOn: string | null }

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
    site?: string | null
    vehicleNumber?: string | null
    painterId?: string | null
    /** "2026-27/0457": the bill's serial number in its financial year. */
    billNo?: string | null
    gstin?: string | null
    cancelledAt?: string | null
    cancelReason?: string | null
    buyerGstin?: string | null
    billOfSupply?: boolean
    prescription?: Prescription | null
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
  /** Khata money received today. */
  collected?: number
}

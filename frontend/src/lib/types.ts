// Shapes of the API's JSON (camelCase, enums in snake_case). Keep in step with backend/src/GroceryKhata.Api.

export type ShopRole = 'owner' | 'staff' | 'accountant'
/** A platform admin's role: super (everything), approver (approve and block shops), support (notes, unlocking). */
export type AdminRole = 'super' | 'approver' | 'support' | 'billing' | 'agent_manager'

export type Unit = 'piece' | 'kg' | 'g' | 'litre' | 'ml' | 'packet' | 'pair' | 'dozen' | 'box' | 'jar' | 'bag' | 'tonne' | 'brass' | 'cft' | 'metre' | 'sq_ft' | 'running_ft' | 'quintal' | 'carton' | 'bottle' | 'set' | 'strip'
/** Card and cheque reach the bank, not the drawer. A cheque is for khata payments, and counts once cleared. */
export type PaymentMode = 'cash' | 'upi' | 'card' | 'cheque' | 'voucher'
export type LedgerEntryKind = 'credit' | 'payment' | 'reversal' | 'return' | 'opening'
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
  /** Days the customer has to pay what they take on credit. Null: no terms. */
  creditDays?: number | null
  /** Price group: a contractor or dealer pays the shop's percent off the list price; null is retail. */
  priceGroup?: 'contractor' | 'dealer' | null
  /** A placeholder khata ("Name later 3") for udhaar to someone not named yet. */
  nameLater?: boolean
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
  /** Average sold a day over the last 30 days; stock over it is the days of stock left. */
  dailySales?: number | null
  /** Kept in bags of this much of its unit (50 = a 50 kg bag); bags is the sealed bags on hand. */
  bagSize?: number | null
  /** What bagSize counts when buying (carton, strip); null: a bag. */
  purchaseUnit?: Unit | null
  /** Kilograms in one unit, for load weight and freight by weight. */
  weightKg?: number | null
  /** The item's English and Hindi names from the master list or common catalogue (see lib/itemName). */
  nameEn?: string | null
  nameHi?: string | null
  bags?: number
  /** Promised on open sale orders, not yet delivered. */
  promised?: number
  /** Out of stock, or at/below the reorder level. */
  isLow: boolean
  /** The shop doesn't count this item: never out or low. */
  uncounted?: boolean
  /** "2026-11-07": the day the shop marked it sold out. */
  outOn?: string | null
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
  /** From wholesaleMinQty on a bill, the price per unit is wholesalePrice. */
  wholesalePrice?: number | null
  wholesaleMinQty?: number | null
  /** The price of one whole sealed bag (bagSize of the unit); none: the loose rate times the bag. */
  bagPrice?: number | null
  /** Printed on the customer's bill instead of the name ("Innerwear"); see lib/billNames.ts. */
  billName?: string | null
  /** Clothes: the design this item is one size/colour of ("पुरुष जींस"); see lib/styles.ts. */
  styleName?: string | null
  size?: string | null
  colour?: string | null
  barcode?: string | null
  /** More codes that scan as this item (a new pack design, a carton). */
  extraBarcodes?: string[] | null
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
  /** More photos beyond the main one, oldest first. */
  extraPhotos?: string[] | null
  /** Has stock changes on this phone not yet on the server. */
  pending?: boolean
}

export type StockMovementKind = 'opening' | 'purchase' | 'sale' | 'adjustment'
export type StockReason = 'damage' | 'wastage' | 'count_correction' | 'return' | 'other' | 'lost' | 'stolen' | 'destroyed' | 'written_off' | 'gift' | 'free_sample' | 'weight_loss'

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
  /** The bank reference of a UPI payment: removing it needs a reason, and frees the money to be matched again. */
  upiRef?: string | null
  chequeNo?: string | null
  chequeStatus?: 'pending' | 'cleared' | 'bounced' | null
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
  /** The bill's printed number, e.g. 2026-27/0001. */
  billNo?: string | null
}

export interface Statement {
  customer: Customer
  entries: StatementLine[]
  /** Owed before the month (previous dues carried forward); 0 for the whole khata. */
  openingBalance: number
  closingBalance: number
  /** "2026-10", or null for everything. */
  month: string | null
  /** The customer's latest confirmation of their balance, from their own app. */
  confirmed?: { balance: number; at: string } | null
}

export interface CashBookLine {
  occurredAt: string
  source: 'bill' | 'khata_payment' | 'reversal' | 'expense' | 'technician' | 'bill_cancelled' | 'cash_added' | 'cash_removed' | 'to_bank' | 'from_bank'
  who: string
  mode: PaymentMode
  amount: number
  billId: string | null
  /** A cheque counted the day it came in, not yet cleared at the bank. */
  chequePending?: boolean
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
  /** Whole bags sold on this line. */
  bags?: number | null
}

export type DrugSchedule = 'g' | 'h' | 'h1' | 'x'

export interface Prescription { doctorName: string; doctorAddress: string | null; doctorRegNo: string | null; patientName: string; patientAddress: string | null; prescribedOn: string | null }

export type PrintTheme = 'classic' | 'modern' | 'compact'

export interface BillPrint { logo: string | null; signature: string | null; theme: PrintTheme; terms: string | null }

export interface BillView {
  bill: {
    id: string
    customerId: string | null
    total: number
    paidAmount: number
    onKhata: number
    paymentMode: PaymentMode | null
    payments?: { mode: PaymentMode; amount: number; reference?: string | null }[] | null
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
    placeOfSupply?: string | null
    shopAddress?: string | null
    buyerAddress?: string | null
    prescription?: Prescription | null
    /** An exchange: what the returned goods (credit note exchangeNoteNo) paid of this bill. */
    exchangeCredit?: number
    exchangeNoteNo?: string | null
    /** The counter (device) it was made on. */
    counter?: string | null
    /** How the shop prints its bills: logo and signature (data URLs), style and terms. */
    print?: BillPrint | null
    /** The khata part is due on this day ("2026-12-01"). */
    dueOn?: string | null
    /** The shop's own extra fields as filled on this bill. */
    fields?: { label: string; value: string }[] | null
  }
  customer: Customer | null
}

export interface ProblemDetails {
  /** What kind of problem: "bill-passcode" when an action needs the shop's bill passcode. */
  type?: string
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
  card?: number
  /** Cheques cleared that were received today. */
  cheque?: number
  /** Cheques still waiting on the bank, shop-wide. */
  chequesPending?: number
  chequesPendingCount?: number
  /** Udhaar to people not named yet. */
  nameLaterDue?: number
  nameLaterCount?: number
}

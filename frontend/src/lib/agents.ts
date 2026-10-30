export type FieldAgentStatus = 'applied' | 'approved' | 'rejected' | 'blocked'

/** A field agent: visits shops and signs them up under their agent code (GKA-0042). */
export interface FieldAgent {
  id: string
  /** Given on approval. */
  code: string | null
  fullName: string
  phone: string
  address: string
  market: string
  /** Only the last 4 digits are kept. */
  aadhaarLast4: string
  kycChecked: boolean
  upiId: string | null
  status: FieldAgentStatus
  /** Why it was rejected or blocked. */
  statusReason: string | null
  appliedAt: string
  approvedAt: string | null
  approvedBy: string | null
}

/** Aadhaar: 12 digits, not starting with 0 or 1, the last a Verhoeff check digit. Catches a mistyped digit before sending. */
export function isValidAadhaar(text: string): boolean {
  const d = [[0, 1, 2, 3, 4, 5, 6, 7, 8, 9], [1, 2, 3, 4, 0, 6, 7, 8, 9, 5], [2, 3, 4, 0, 1, 7, 8, 9, 5, 6], [3, 4, 0, 1, 2, 8, 9, 5, 6, 7], [4, 0, 1, 2, 3, 9, 5, 6, 7, 8],
    [5, 9, 8, 7, 6, 0, 4, 3, 2, 1], [6, 5, 9, 8, 7, 1, 0, 4, 3, 2], [7, 6, 5, 9, 8, 2, 1, 0, 4, 3], [8, 7, 6, 5, 9, 3, 2, 1, 0, 4], [9, 8, 7, 6, 5, 4, 3, 2, 1, 0]]
  const p = [[0, 1, 2, 3, 4, 5, 6, 7, 8, 9], [1, 5, 7, 6, 2, 8, 3, 0, 9, 4], [5, 8, 0, 3, 7, 9, 6, 1, 4, 2], [8, 9, 1, 6, 0, 4, 3, 5, 2, 7],
    [9, 4, 5, 3, 1, 2, 6, 8, 7, 0], [4, 2, 8, 6, 5, 7, 3, 9, 0, 1], [2, 7, 9, 3, 8, 0, 6, 4, 1, 5], [7, 0, 4, 6, 9, 1, 3, 2, 5, 8]]
  const digits = text.replace(/[\s-]/g, '')
  if (!/^[2-9]\d{11}$/.test(digits)) return false
  let c = 0
  for (let i = 0; i < 12; i++) c = d[c][p[i % 8][Number(digits[11 - i])]]
  return c === 0
}

export type RegistrationStatus = 'submitted' | 'approved' | 'rejected'
export type RegistrationPayment = 'upi' | 'cash' | 'later'
export type RegistrationPaymentStatus = 'none' | 'received' | 'confirmed'

/** A shop an agent signed up on a visit, as the agent and the reviewing admin see it. */
export interface Registration {
  id: string
  status: RegistrationStatus
  shopName: string
  type: string
  address: string
  market: string
  latitude: number
  longitude: number
  accuracyMetres: number | null
  ownerName: string
  ownerPhone: string
  altPhone: string | null
  gstin: string | null
  planCode: string
  term: 'monthly' | 'year1' | 'year3' | 'year5'
  amount: number
  paymentMode: RegistrationPayment
  paymentRef: string | null
  paymentStatus: RegistrationPaymentStatus
  submittedAt: string
  consentAt: string
  rejectReason: string | null
  shopId: string | null
  agentCode: string
  agentName: string
  photos: { id: string; kind: 'front' | 'inside' | 'owner' }[]
  /** For the reviewer: same owner number, a sign-up within 30 m, rough GPS, a reused payment reference. */
  warnings: string[]
}

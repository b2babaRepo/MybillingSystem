// This device's open cashier shift (see the server's CashierShift): bills made while it is open carry its id, so their
// cash counts toward this drawer. Kept on the device so billing works offline; the Shifts screen checks the server.

export interface CashierShift {
  id: string
  counter: string
  cashierUserId: string
  cashier: string | null
  openedAt: string
  openingFloat: number
  closedAt: string | null
  bills: number
  sales: number
  /** Hidden from a cashier until the shift is closed (a blind count); the owner sees them. */
  cashSales: number | null
  expected: number | null
  counted: number | null
  difference: number | null
  note: string | null
}

const key = (shopId: string) => `gk-shift:${shopId}`
const listeners = new Set<() => void>()

export function currentShift(shopId: string): CashierShift | null {
  try {
    const raw = localStorage.getItem(key(shopId))
    return raw ? (JSON.parse(raw) as CashierShift) : null
  } catch {
    return null
  }
}

/** Remember (an open shift) or forget (closed, or none on the server) this device's shift. */
export function rememberShift(shopId: string, shift: CashierShift | null) {
  try {
    if (shift && !shift.closedAt) localStorage.setItem(key(shopId), JSON.stringify(shift))
    else localStorage.removeItem(key(shopId))
  } catch { /* no storage: bills go without a shift */ }
  listeners.forEach((l) => l())
}

export function subscribeShift(listener: () => void) {
  listeners.add(listener)
  return () => { listeners.delete(listener) }
}

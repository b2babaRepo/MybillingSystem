import type { Charge, Charges } from '../components/LandedCost'

export const noCharges: Charges = { rows: [], split: 'value', extraInCost: false }

export const rows = (c: Charge[]) => c.filter((r) => r.label.trim() && Number(r.amount) > 0).map((r) => ({ label: r.label.trim(), amount: Number(r.amount), paidBy: r.paidBy || null }))

/** The body a bill sends: only the charges filled in, and how to spread them. */
export const chargesBody = (c: Charges) => ({ charges: rows(c.rows), split: c.split, extraInCost: c.extraInCost })

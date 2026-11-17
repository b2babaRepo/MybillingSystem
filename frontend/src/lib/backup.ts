/**
 * Reading a backup file (the shop's download from /shop/export) to restore it into a new, empty shop. Restore uses
 * the same imports as moving from a notebook: customers and suppliers with what they owe today as an opening balance,
 * and items with what is on the shelf as opening stock. Bills and day-to-day entries stay in the file and in the old
 * shop: a new shop starts from today's position, the way a shop moving from another app does.
 */
import type { ImportRow } from './catalogue'
import type { PartyRow } from './partyImport'

/** An item as the import takes it: the CSV columns plus the settings a backup also carries. */
export type ProductRow = Omit<ImportRow, 'unit'> & {
  unit: string; gstRate: number | null; hsnCode: string | null; packSize: number | null; packUnit: string | null; isService: boolean
  durationMinutes: number | null; schedule: string | null; unitsPerPack: number | null; tracksSerials: boolean; warrantyMonths: number | null
  styleName: string | null; size: string | null
}

export const BACKUP_FORMAT = 'grocery-khata-backup'

export interface SupplierRow { name: string; phone: string | null; gstin: string | null; balance: number }

export interface Backup {
  shopName: string
  exportedAt: string
  customers: PartyRow[]
  suppliers: SupplierRow[]
  products: ProductRow[]
  /** Records kept in the file but not brought over: bills, khata entries, expenses. */
  history: { bills: number; entries: number; expenses: number }
}

type Json = Record<string, unknown>
const str = (v: unknown) => (typeof v === 'string' && v.trim() ? v.trim() : null)
const num = (v: unknown) => (typeof v === 'number' && Number.isFinite(v) ? v : null)
const list = (v: unknown): Json[] => (Array.isArray(v) ? v.filter((x): x is Json => !!x && typeof x === 'object') : [])

/** The backup in `text`, or an error key (backup.notJson, backup.notBackup) when it isn't one. */
export function parseBackup(text: string): Backup | { error: string } {
  let data: Json
  try { data = JSON.parse(text) as Json } catch { return { error: 'backup.notJson' } }
  if (!data || typeof data !== 'object') return { error: 'backup.notJson' }
  // Files from before the format mark still have the shop and its customers.
  const shop = data.shop as Json | undefined
  if (data.format !== undefined ? data.format !== BACKUP_FORMAT : !(shop && Array.isArray(data.customers))) return { error: 'backup.notBackup' }

  const customers: PartyRow[] = list(data.customers).flatMap((c) => {
    const name = str(c.name)
    return name ? [{ name, phone: str(c.phone), balance: num(c.balance) ?? 0 }] : []
  })
  const suppliers: SupplierRow[] = list(data.suppliers).flatMap((s) => {
    const name = str(s.name)
    return name ? [{ name, phone: str(s.phone), gstin: str(s.gstin), balance: num(s.balance) ?? 0 }] : []
  })
  const products: ProductRow[] = list(data.products).filter((p) => p.isActive !== false).flatMap((p) => {
    const name = str(p.name)
    const unit = str(p.unit)
    if (!name || !unit) return []
    const stock = num(p.stock)
    return [{
      name, unit, sellingPrice: num(p.sellingPrice) ?? 0, costPrice: num(p.costPrice), category: str(p.category),
      // What was on the shelf comes in as opening stock; a shelf in the minus starts at nothing.
      openingStock: p.isService || p.uncounted || stock == null || stock <= 0 ? null : stock,
      reorderLevel: num(p.reorderLevel), gstRate: num(p.gstRate), hsnCode: str(p.hsnCode), packSize: num(p.packSize), packUnit: str(p.packUnit),
      isService: p.isService === true, durationMinutes: num(p.durationMinutes), schedule: str(p.schedule), unitsPerPack: num(p.unitsPerPack),
      tracksSerials: p.tracksSerials === true, warrantyMonths: num(p.warrantyMonths), styleName: str(p.styleName), size: str(p.size),
    }]
  })
  return {
    shopName: str(shop?.name) ?? '',
    exportedAt: str(data.exportedAt) ?? '',
    customers, suppliers, products,
    history: { bills: list(data.bills).length, entries: list(data.ledgerEntries).length, expenses: list(data.expenses).length },
  }
}

/** `rows` in pieces of at most `size`: each import takes up to 2,000 rows at a time. */
export function chunks<T>(rows: T[], size = 1000): T[][] {
  const out: T[][] = []
  for (let i = 0; i < rows.length; i += size) out.push(rows.slice(i, i + size))
  return out
}

/** Days since the last backup, or null for never. */
export function daysSince(iso: string | null, now = new Date()): number | null {
  if (!iso) return null
  return Math.floor((now.getTime() - new Date(iso).getTime()) / 86_400_000)
}

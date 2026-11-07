import { nameMatches } from './nameMatch'

interface Findable {
  name: string
  phone: string | null
}

/**
 * The customers the bill's finder shows: by name (Hindi or Latin), or by any run of phone digits, so the last
 * four digits find them. Nothing typed: the first few, as the list already orders them.
 */
export function findCustomers<T extends Findable>(customers: T[], text: string, limit = 6): T[] {
  const query = text.trim()
  if (!query) return customers.slice(0, limit)
  const digits = /^[\d\s+-]+$/.test(query) ? query.replace(/\D/g, '') : ''
  return customers.filter((c) => (digits ? (c.phone ?? '').replace(/\D/g, '').includes(digits) : nameMatches(c.name, query))).slice(0, limit)
}

/** What to hand back from the cash received; negative when it falls short. */
export function changeDue(received: number, paying: number): number {
  return Math.round((received - paying) * 100) / 100
}

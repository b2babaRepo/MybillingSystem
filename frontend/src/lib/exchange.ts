/** An exchange in progress: goods taken back on a credit note, whose value pays the next bill. Kept for this tab. */
export interface Exchange { noteId: string; noteNo: string; total: number; customerId?: string | null }

const KEY = 'exchange'

export function startExchange(exchange: Exchange) {
  try { sessionStorage.setItem(KEY, JSON.stringify(exchange)) } catch { /* storage blocked: the shop applies it by hand */ }
}

export function currentExchange(): Exchange | null {
  try { const raw = sessionStorage.getItem(KEY); return raw ? (JSON.parse(raw) as Exchange) : null } catch { return null }
}

export function endExchange() {
  try { sessionStorage.removeItem(KEY) } catch { /* nothing to clear */ }
}

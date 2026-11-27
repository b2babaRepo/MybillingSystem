import { NetworkError, problemMessage, readProblem, request } from './api'
import { getSession } from './session'
import { db } from './db'

export type OutboxKind = 'customer' | 'product' | 'khata' | 'bill' | 'reverse' | 'listItem' | 'listItemDelete' | 'order' | 'stock' | 'expense' | 'expenseStrike' | 'orderAction' | 'supplier' | 'estimateBilled' | 'saleOrderDelivered' | 'creditNote'

/**
 * One write waiting to reach the server. Every write in the app goes through here, online or not:
 * it is saved on the phone first, shown at once, and sent in order when there is a connection.
 * The body carries the record's own ID, so sending it twice never makes a second record.
 */
export interface OutboxOp {
  seq?: number
  /** The ID of the record this op creates (the idempotency key). */
  id: string
  shopId: string
  kind: OutboxKind
  path: string
  body: unknown
  /** For khata ops: whose balance moves, and by how much (+ owes more, − paid). */
  customerId?: string
  delta?: number
  /** For a bill: the customer's name, for printing it before it syncs. */
  customerName?: string
  /** For a reverse op: the entry it cancels. */
  reversesEntryId?: string
  /** PUT and DELETE for household list edits (both safe to repeat); POST otherwise. */
  method?: 'POST' | 'PUT' | 'DELETE'
  /** For household list ops: which list. */
  listKind?: 'monthly' | 'weekly'
  /** A return made offline: the bill passcode typed then, sent with it (the server checks it). Gone once sent. */
  passcode?: string
  /** A supervisor's PIN approving a cashier's price below their limit, sent with it. Gone once sent. */
  supervisorPin?: string
  /** Short text for the sync screen, e.g. "Ramesh ₹340 udhaar". */
  label: string
  createdAt: string
  attempts: number
  status: 'pending' | 'failed'
  error?: string
}

export type SyncState = 'idle' | 'syncing' | 'offline' | 'signed-out'

const listeners = new Set<() => void>()
let syncState: SyncState = 'idle'
/** The run in progress or queued, per shop. Runs go one at a time, so another shop's turn waits instead of being skipped. */
const flushing = new Map<string, Promise<void>>()
let lastFlush: Promise<unknown> = Promise.resolve()
let onSynced: (op: OutboxOp, response: unknown) => void = () => {}

function notify() {
  listeners.forEach((listener) => listener())
}

/** Called after each op the server accepts, with its JSON reply (used to refresh cached data). */
export function setSyncedHandler(handler: (op: OutboxOp, response: unknown) => void) {
  onSynced = handler
}

export function subscribeOutbox(listener: () => void) {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

export function getSyncState() {
  return syncState
}

export async function listOps(shopId: string): Promise<OutboxOp[]> {
  const ops = await (await db()).getAllFromIndex('outbox', 'byShop', shopId)
  return ops.sort((a, b) => a.seq! - b.seq!)
}

export async function enqueue(op: Omit<OutboxOp, 'seq' | 'createdAt' | 'attempts' | 'status'>) {
  await (await db()).add('outbox', { ...op, createdAt: new Date().toISOString(), attempts: 0, status: 'pending' })
  notify()
  void flush(op.shopId)
}

/** Takes back an op that hasn't been sent yet (Undo before sync). False when it already reached the server. */
export async function cancelPending(shopId: string, id: string): Promise<boolean> {
  const database = await db()
  const op = (await listOps(shopId)).find((o) => o.id === id)
  if (!op || flushing) {
    // While a flush runs the op may be in flight; let it land and undo with a reversal instead.
    return false
  }
  await database.delete('outbox', op.seq!)
  notify()
  return true
}

export async function discardFailed(seq: number) {
  await (await db()).delete('outbox', seq)
  notify()
}

export async function retryFailed(shopId: string) {
  const database = await db()
  for (const op of await listOps(shopId)) {
    if (op.status === 'failed') await database.put('outbox', { ...op, status: 'pending', error: undefined })
  }
  notify()
  await flush(shopId)
}

/**
 * Sends this shop's pending ops one by one, oldest first. Stops at the first network failure (still
 * offline) or server error (try later); an op the server refuses (400/404/409) is marked failed for
 * the shopkeeper to see, and the rest carry on.
 */
export function flush(shopId: string): Promise<void> {
  const running = flushing.get(shopId)
  if (running) return running
  const run = lastFlush.then(() => flushShop(shopId)).finally(() => {
    flushing.delete(shopId)
    notify()
  })
  flushing.set(shopId, run)
  lastFlush = run.catch(() => {})
  return run
}

async function flushShop(shopId: string): Promise<void> {
    const database = await db()
    syncState = 'syncing'
    notify()
    try {
      for (;;) {
        // Switched to another shop: the sign-in now speaks for that shop, so this one's writes wait for its next turn.
        const active = getSession()?.shopId
        const op = active && active !== shopId ? undefined : (await listOps(shopId)).find((o) => o.status === 'pending')
        if (!op) {
          syncState = 'idle'
          return
        }

        let response: Response
        try {
          response = await request(op.path, op.method ?? 'POST', op.method === 'DELETE' ? undefined : op.body, op.passcode || op.supervisorPin ? { ...(op.passcode ? { 'X-Bill-Passcode': op.passcode } : {}), ...(op.supervisorPin ? { 'X-Supervisor-Pin': op.supervisorPin } : {}) } : undefined)
        } catch (error) {
          if (error instanceof NetworkError) {
            syncState = 'offline'
            return
          }
          throw error
        }

        if (response.ok) {
          const reply: unknown = await response.json().catch(() => null)
          await database.delete('outbox', op.seq!)
          onSynced(op, reply)
          notify()
        } else if (response.status === 401) {
          syncState = 'signed-out'
          return
        } else if (response.status >= 500 || response.status === 429) {
          await database.put('outbox', { ...op, attempts: op.attempts + 1 })
          syncState = 'offline'
          return
        } else {
          const message = problemMessage(await readProblem(response))
          await database.put('outbox', { ...op, status: 'failed', attempts: op.attempts + 1, error: message ?? `Error ${response.status}` })
          notify()
        }
      }
    } finally {
      notify()
    }
}

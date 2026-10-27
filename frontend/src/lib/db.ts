import { openDB, type DBSchema, type IDBPDatabase } from 'idb'
import type { OutboxOp } from './outbox'

interface KhataDB extends DBSchema {
  /** Writes made on this phone that the server hasn't confirmed yet, in the order they were made. */
  outbox: { key: number; value: OutboxOp; indexes: { byShop: string } }
  /** The last copy of server data (customers, products, statements), for opening the app offline. */
  cache: { key: string; value: unknown }
}

let dbPromise: Promise<IDBPDatabase<KhataDB>> | null = null

export function db() {
  dbPromise ??= openDB<KhataDB>('grocery-khata', 1, {
    upgrade(database) {
      const outbox = database.createObjectStore('outbox', { keyPath: 'seq', autoIncrement: true })
      outbox.createIndex('byShop', 'shopId')
      database.createObjectStore('cache')
    },
  })
  return dbPromise
}

export async function readCache<T>(key: string): Promise<T | undefined> {
  return (await (await db()).get('cache', key)) as T | undefined
}

export async function writeCache(key: string, value: unknown) {
  await (await db()).put('cache', value, key)
}

/**
 * Asks the browser not to clear our data when the phone runs low on space. Without this, Chrome may
 * evict IndexedDB, and unsynced bills with it.
 */
export async function requestPersistentStorage(): Promise<boolean> {
  try {
    return (await navigator.storage?.persisted?.()) || ((await navigator.storage?.persist?.()) ?? false)
  } catch {
    return false
  }
}

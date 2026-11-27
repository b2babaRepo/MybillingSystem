// The registered till this device is (see the server's Shops.Terminal): bills made here carry its id, and its name
// becomes the counter on them. Kept on the device; the owner registers it once from the Tills screen.

export interface Till { id: string; code: string; name: string }

const key = (shopId: string) => `gk-till:${shopId}`

export function thisTill(shopId: string): Till | null {
  try {
    const raw = localStorage.getItem(key(shopId))
    return raw ? (JSON.parse(raw) as Till) : null
  } catch {
    return null
  }
}

export function setThisTill(shopId: string, till: Till | null) {
  try {
    if (till) localStorage.setItem(key(shopId), JSON.stringify({ id: till.id, code: till.code, name: till.name }))
    else localStorage.removeItem(key(shopId))
  } catch { /* no storage: bills carry the typed counter name instead */ }
}

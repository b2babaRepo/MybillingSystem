// The customer-facing display: the till sends what the customer should see (items, total, offers, change) to a second
// window on the same computer (the /display page on a screen facing the customer). Same browser, no server, so it
// works offline too.

export interface DisplayState {
  shop: string
  lines: { name: string; quantity: number; unit: string; amount: number }[]
  total: number
  saved: number
  offers: { name: string; saved: number }[]
  /** After saving: what was paid and the change to give. */
  done: { total: number; paid: number; change: number } | null
  upiId: string | null
}

const channel = (shopId: string) => `gk-display:${shopId}`

let open: { shopId: string; port: BroadcastChannel } | null = null

export function showOnDisplay(shopId: string, state: DisplayState) {
  if (typeof BroadcastChannel === 'undefined') return
  if (open?.shopId !== shopId) {
    open?.port.close()
    open = { shopId, port: new BroadcastChannel(channel(shopId)) }
  }
  open.port.postMessage(state)
}

export function watchDisplay(shopId: string, onState: (state: DisplayState) => void) {
  if (typeof BroadcastChannel === 'undefined') return () => undefined
  const port = new BroadcastChannel(channel(shopId))
  port.onmessage = (e: MessageEvent<DisplayState>) => onState(e.data)
  // Ask the till to send what it has now (the display opened after the bill started).
  port.postMessage({ hello: true })
  return () => port.close()
}

/** The till answers a newly opened display with the current bill. */
export function onDisplayHello(shopId: string, send: () => void) {
  if (typeof BroadcastChannel === 'undefined') return () => undefined
  const port = new BroadcastChannel(channel(shopId))
  port.onmessage = (e: MessageEvent<{ hello?: boolean }>) => { if (e.data?.hello) send() }
  return () => port.close()
}

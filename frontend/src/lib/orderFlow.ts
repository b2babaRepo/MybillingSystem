import { useQuery } from '@tanstack/react-query'
import i18n from 'i18next'
import { api } from './api'
import { round2 } from './local'
import type { Fulfilment, Order, OrderStatus } from './planner'

// Customer app phase 2: how a shop takes online orders (charge, minimum, time chips), what checkout shows before
// confirming (UPI, khata balance), the order's steps, and the customer's order notifications.

export interface StoreSettings { deliveryFee: number | null; freeDeliveryAbove: number | null; minOrder: number | null; slots: string[]; theme?: string | null }
export interface KhataAtCheckout { allowed: boolean; balance: number; limit: number | null }
export interface CheckoutInfo { settings: StoreSettings; upiId: string | null; khata: KhataAtCheckout | null }

export const noSettings: StoreSettings = { deliveryFee: null, freeDeliveryAbove: null, minOrder: null, slots: [] }

/** Signed in: the shop's rules plus this person's khata there. */
export const useCheckoutInfo = (shopId: string | undefined, enabled = true) =>
  useQuery({ queryKey: ['me', 'checkout', shopId], queryFn: () => api<CheckoutInfo>(`/v1/me/checkout/${shopId}`), enabled: !!shopId && enabled, retry: false })

/** Same rule as the server: none for pickup, none at or above the free-delivery amount. */
export function deliveryFee(settings: StoreSettings, fulfilment: Fulfilment, itemTotal: number) {
  if (fulfilment !== 'delivery' || !settings.deliveryFee) return 0
  if (settings.freeDeliveryAbove && itemTotal >= settings.freeDeliveryAbove) return 0
  return settings.deliveryFee
}

/** The honest bill before checkout: item total, saved against MRP or the regular price, the charge, and what is left to reach the minimum. */
export function billOf(lines: { price: number; quantity: number; was?: number | null }[], settings: StoreSettings, fulfilment: Fulfilment) {
  const items = round2(lines.reduce((s, l) => s + round2(l.price * l.quantity), 0))
  const saved = round2(lines.reduce((s, l) => s + (l.was && l.was > l.price ? round2((l.was - l.price) * l.quantity) : 0), 0))
  const fee = deliveryFee(settings, fulfilment, items)
  const short = settings.minOrder && items < settings.minOrder ? round2(settings.minOrder - items) : 0
  const toFree = fulfilment === 'delivery' && settings.deliveryFee && settings.freeDeliveryAbove && items < settings.freeDeliveryAbove ? round2(settings.freeDeliveryAbove - items) : 0
  return { items, saved, fee, total: round2(items + fee), short, toFree }
}

/** The steps an order goes through, its path depending on pickup or delivery. */
export function stepsOf(order: Pick<Order, 'fulfilment'>): OrderStatus[] {
  return ['placed', 'accepted', 'packed', order.fulfilment === 'pickup' ? 'ready_for_pickup' : 'out_for_delivery', 'delivered']
}

/** How far along: the index of the current step (an order that skipped "accepted" still counts as past it). */
export function reachedStep(order: Pick<Order, 'fulfilment' | 'status'>) {
  if (order.status === 'cancelled') return -1
  const steps = stepsOf(order)
  const at = steps.indexOf(order.status === 'ready_for_pickup' || order.status === 'out_for_delivery' ? steps[3] : order.status)
  return at
}

/** Still open: the shop may change it, and the list keeps refreshing. */
export const isOpen = (status: OrderStatus) => status !== 'delivered' && status !== 'cancelled'

/** The customer may add items or cancel: the shop hasn't packed yet. */
export const canChange = (status: OrderStatus) => status === 'placed' || status === 'accepted'

// ---- Order notifications on this phone ---------------------------------------------------------------------------

const ASKED = 'gk-order-alerts-asked'
export const alertsPossible = () => typeof window !== 'undefined' && 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window

function keyBytes(base64: string) {
  const padded = (base64 + '='.repeat((4 - (base64.length % 4)) % 4)).replace(/-/g, '+').replace(/_/g, '/')
  return Uint8Array.from(atob(padded), (c) => c.charCodeAt(0))
}

/** Whether to offer alerts now: possible here, not yet answered, and not asked before on this phone. */
export function shouldAskAlerts() {
  try {
    return alertsPossible() && Notification.permission === 'default' && localStorage.getItem(ASKED) !== '1'
  } catch {
    return false
  }
}

export function markAlertsAsked() {
  try { localStorage.setItem(ASKED, '1') } catch { /* private mode */ }
}

/** Asks the browser, then signs this phone up for order alerts. True when it is on. */
export async function turnOnAlerts() {
  markAlertsAsked()
  if (!alertsPossible() || (await Notification.requestPermission()) !== 'granted') return false
  const reg = await navigator.serviceWorker.ready
  const { key } = await api<{ key: string }>('/v1/me/push/key')
  const sub = (await reg.pushManager.getSubscription()) ?? (await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: keyBytes(key) }))
  const json = sub.toJSON()
  await api('/v1/me/push/subscribe', 'POST', { endpoint: json.endpoint, p256dh: json.keys?.p256dh, auth: json.keys?.auth, language: i18n.language })
  return true
}

export async function alertsOn() {
  if (!alertsPossible() || Notification.permission !== 'granted') return false
  const reg = await navigator.serviceWorker.getRegistration()
  return !!(await reg?.pushManager.getSubscription())
}

export async function turnOffAlerts() {
  const reg = await navigator.serviceWorker.getRegistration()
  const sub = await reg?.pushManager.getSubscription()
  if (sub) await api('/v1/me/push/unsubscribe', 'POST', { endpoint: sub.endpoint })
}

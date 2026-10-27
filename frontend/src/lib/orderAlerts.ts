import { useQuery } from '@tanstack/react-query'
import { useEffect, useRef } from 'react'
import { api } from './api'
import type { Order } from './planner'

// New-order alerts while the shop app is open: a count badge on the Orders tab always, and, when the
// shopkeeper has turned it on, a notification with vibration for each new order. (Alerts with the app closed
// need Web Push and a push server: a later step.)

const KEY = 'grocery-khata.order-alerts'

export function alertsEnabled(): boolean {
  try {
    return localStorage.getItem(KEY) === '1' && 'Notification' in window && Notification.permission === 'granted'
  } catch {
    return false
  }
}

export async function enableAlerts(): Promise<boolean> {
  if (!('Notification' in window)) return false
  const permission = Notification.permission === 'granted' ? 'granted' : await Notification.requestPermission()
  try {
    localStorage.setItem(KEY, permission === 'granted' ? '1' : '0')
  } catch {
    // Not remembered; asks again next time.
  }
  return permission === 'granted'
}

/** Orders waiting to be packed, checked every 30 seconds. Notifies once for each order it hasn't seen. */
export function useNewOrders(shopId: string, notify: (order: Order) => string) {
  const query = useQuery({
    queryKey: [shopId, 'orders', 'placed'],
    queryFn: () => api<Order[]>('/v1/orders?status=placed'),
    refetchInterval: 30_000,
    retry: false,
  })
  const seen = useRef<Set<string> | null>(null)

  useEffect(() => {
    const orders = query.data
    if (!orders) return
    if (seen.current === null) {
      // First load: these were already waiting; only orders arriving from now on alert.
      seen.current = new Set(orders.map((o) => o.id))
      return
    }
    for (const order of orders) {
      if (seen.current.has(order.id)) continue
      seen.current.add(order.id)
      if (alertsEnabled()) {
        new Notification(notify(order), { tag: order.id, icon: '/pwa-192x192.png' })
        navigator.vibrate?.([200, 100, 200])
      }
    }
  }, [query.data, notify])

  return query.data?.length ?? 0
}

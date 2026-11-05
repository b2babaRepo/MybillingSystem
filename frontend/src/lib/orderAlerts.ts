import { useQuery, useQueryClient } from '@tanstack/react-query'
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

/**
 * Orders waiting to be packed. While the app is on screen it keeps one "wait" request open: the server answers the
 * moment an order arrives (else after 25 s), and the orders are fetched again at once, so a new order shows within a
 * second. A slow check every 2 minutes stays as a backstop. Notifies once for each order it hasn't seen.
 */
export function useNewOrders(shopId: string, notify: (order: Order) => string) {
  const queryClient = useQueryClient()
  const query = useQuery({
    queryKey: [shopId, 'orders', 'placed'],
    queryFn: () => api<Order[]>('/v1/orders?status=placed'),
    refetchInterval: 120_000,
    retry: false,
  })
  const seen = useRef<Set<string> | null>(null)

  useEffect(() => {
    let stopped = false
    async function listen() {
      while (!stopped) {
        if (document.visibilityState !== 'visible') {
          await new Promise((resolve) => document.addEventListener('visibilitychange', resolve, { once: true }))
          // Back on screen: catch up on anything that came in while away.
          if (!stopped) void queryClient.invalidateQueries({ queryKey: [shopId, 'orders'] })
          continue
        }
        try {
          const { changed } = await api<{ changed: boolean }>('/v1/orders/wait')
          if (changed && !stopped) void queryClient.invalidateQueries({ queryKey: [shopId, 'orders'] })
        } catch {
          // Offline or the server restarting: try again shortly.
          await new Promise((resolve) => setTimeout(resolve, 10_000))
        }
      }
    }
    void listen()
    return () => { stopped = true }
  }, [shopId, queryClient])

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

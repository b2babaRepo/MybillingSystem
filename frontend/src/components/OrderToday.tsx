import { itemName } from '../lib/itemName'
import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { navigate } from '../router'

interface Item { productId: string; name: string; stock: number; daysLeft: number | null; suggested: number; leadDays: number; nameEn?: string | null; nameHi?: string | null }

const KEY = 'gk-push-on'

/** Web Push needs a service worker and the Push API (Chrome on Android and computers; iPhone only once added to the home screen). */
const pushPossible = () => typeof window !== 'undefined' && 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window

function keyBytes(base64: string) {
  const padded = (base64 + '='.repeat((4 - (base64.length % 4)) % 4)).replace(/-/g, '+').replace(/_/g, '/')
  return Uint8Array.from(atob(padded), (c) => c.charCodeAt(0))
}

/**
 * Home: what to order today, from each item's sales and its supplier's delivery time, the most urgent first, and a
 * switch to get it as a phone notification every morning at 8.
 */
export function OrderToday() {
  const { t, i18n } = useTranslation()
  const today = useQuery({ queryKey: ['order-today'], queryFn: () => api<{ count: number; items: Item[] }>('/v1/stock/order-today'), staleTime: 300_000 }).data
  const [on, setOn] = useState(() => { try { return localStorage.getItem(KEY) === '1' } catch { return false } })
  const [note, setNote] = useState<string | null>(null)

  // The morning note comes in the app's language: tell the server again whenever it changes.
  useEffect(() => {
    if (!on || !('serviceWorker' in navigator)) return
    void (async () => {
      try {
        const sub = await (await navigator.serviceWorker.ready).pushManager.getSubscription()
        const json = sub?.toJSON()
        if (json?.endpoint) await api('/v1/push/subscribe', 'POST', { endpoint: json.endpoint, p256dh: json.keys?.p256dh, auth: json.keys?.auth, language: i18n.language })
      } catch { /* tried again next time */ }
    })()
  }, [on, i18n.language])

  async function turnOn() {
    setNote(null)
    try {
      if ((await Notification.requestPermission()) !== 'granted') { setNote(t('orderToday.denied')); return }
      const { key } = await api<{ key: string }>('/v1/push/key')
      const reg = await navigator.serviceWorker.ready
      const sub = (await reg.pushManager.getSubscription()) ?? await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: keyBytes(key) })
      const json = sub.toJSON()
      await api('/v1/push/subscribe', 'POST', { endpoint: json.endpoint, p256dh: json.keys?.p256dh, auth: json.keys?.auth, language: i18n.language })
      try { localStorage.setItem(KEY, '1') } catch { /* no storage */ }
      setOn(true)
      setNote(t('orderToday.onNote'))
    } catch (e) {
      setNote(e instanceof Error ? e.message : t('error'))
    }
  }

  async function turnOff() {
    try {
      const reg = await navigator.serviceWorker.ready
      const sub = await reg.pushManager.getSubscription()
      if (sub) { await api('/v1/push/unsubscribe', 'POST', { endpoint: sub.endpoint }); await sub.unsubscribe() }
    } catch { /* already off */ }
    try { localStorage.removeItem(KEY) } catch { /* no storage */ }
    setOn(false)
    setNote(null)
  }

  async function test() {
    setNote(null)
    try {
      const r = await api<{ sent: number }>('/v1/push/test', 'POST', {})
      setNote(r.sent > 0 ? t('orderToday.testSent') : t('orderToday.testNone'))
    } catch (e) {
      setNote(e instanceof Error ? e.message : t('error'))
    }
  }

  if (!today) return null
  return (
    <section className={`mx-4 mt-4 flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm ${today.count > 0 ? 'ring-1 ring-orange-200' : ''}`}>
      <div className="flex items-center justify-between gap-2">
        <h2 className="flex items-center gap-2 font-bold"><span aria-hidden>🛒</span>{today.count > 0 ? t('orderToday.title', { count: today.count }) : t('orderToday.none')}</h2>
        {today.count > 0 && <button onClick={() => navigate('/reorder')} className="min-h-10 text-sm font-semibold text-brand underline">{t('orderToday.open')}</button>}
      </div>
      {today.items.slice(0, 5).map((i) => (
        <button key={i.productId} onClick={() => navigate(`/p/${i.productId}`)} className="flex min-h-11 items-center justify-between gap-2 border-b border-[#edf2f1] text-left last:border-0">
          <span className="min-w-0 truncate font-semibold">{itemName(i)}</span>
          <span className="flex shrink-0 items-center gap-2 text-sm">
            <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${i.stock <= 0 || (i.daysLeft ?? 99) <= i.leadDays ? 'bg-due-soft text-due' : 'bg-warn-soft text-warn'}`}>
              {i.stock <= 0 ? t('orderToday.out') : i.daysLeft != null ? t('orderToday.daysLeft', { count: Math.floor(i.daysLeft) }) : t('orderToday.low')}
            </span>
            <span className="text-muted">{t('orderToday.order', { qty: i.suggested })}</span>
          </span>
        </button>
      ))}
      {pushPossible() && (
        <div className="mt-1 flex flex-wrap items-center gap-2 rounded-xl bg-[#f6faf9] p-2 text-sm">
          <span className="flex-1">🔔 {t('orderToday.remind')}</span>
          {on
            ? <><button onClick={() => void test()} className="h-9 rounded-lg border border-line bg-white px-3 font-semibold">{t('orderToday.test')}</button>
              <button onClick={() => void turnOff()} className="h-9 rounded-lg px-3 font-semibold text-muted underline">{t('orderToday.off')}</button></>
            : <button onClick={() => void turnOn()} className="h-9 rounded-lg bg-brand px-3 font-semibold text-white">{t('orderToday.turnOn')}</button>}
        </div>
      )}
      {note && <p className="text-xs text-muted">{note}</p>}
    </section>
  )
}

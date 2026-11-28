import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { shownLocale } from '../i18n'
import { api } from '../lib/api'
import { EVERY_DAY, indiaDay, pausePlan, startPlan, stopPlan, useDeliveryPlans, type DeliveryPlan } from '../lib/deliveries'
import { formatRupees } from '../lib/format'
import type { Unit } from '../lib/types'
import { confirmAsk } from '../lib/ask'
import { btn } from '../lib/buttonStyles'
import { shownError } from '../lib/notify'

interface PlanItem { id: string; name: string; unit: Unit; price: number }

/** Monday-first weekday names in the shown language, for the day chips. */
const weekdays = () => Array.from({ length: 7 }, (_, i) => new Date(2024, 0, 1 + i).toLocaleDateString(shownLocale(), { weekday: 'short' }))
const dayLabel = (iso: string) => new Date(`${iso}T00:00`).toLocaleDateString(shownLocale(), { weekday: 'short', day: 'numeric', month: 'short' })

/** Start a daily delivery from a shop's page: item, how much, which days, from when, where. */
export function StartDeliverySheet({ shopId, shopName, items, onClose }: { shopId: string; shopName: string; items: PlanItem[]; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const home = useQuery({ queryKey: ['addresses'], queryFn: () => api<{ text: string; landmark: string | null }[]>('/v1/addresses'), retry: false }).data?.[0]
  const [productId, setProductId] = useState(items[0]?.id ?? '')
  const [quantity, setQuantity] = useState(1)
  const [days, setDays] = useState(EVERY_DAY)
  const [startOn, setStartOn] = useState(indiaDay(1))
  const [address, setAddress] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [done, setDone] = useState(false)
  const where = address ?? (home ? [home.text, home.landmark].filter(Boolean).join(', ') : '')
  const item = items.find((i) => i.id === productId)
  const perDay = item ? item.price * quantity : 0

  async function start() {
    setBusy(true)
    setError('')
    try {
      await startPlan(shopId, { id: crypto.randomUUID(), productId, quantity, days, startOn, address: where.trim() })
      await queryClient.invalidateQueries({ queryKey: ['me', 'deliveries'] })
      await queryClient.invalidateQueries({ queryKey: ['me', 'shops'] })
      setDone(true)
    } catch (e) {
      setError(shownError(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="fixed inset-0 z-30 flex items-end bg-black/40 lg:items-center lg:justify-center lg:p-6" onClick={onClose}>
      <div role="dialog" aria-label={t('cust5.dailyTitle')} onClick={(e) => e.stopPropagation()} className="flex max-h-[90dvh] w-full flex-col gap-3 overflow-y-auto rounded-t-3xl bg-white p-4 lg:max-w-lg lg:rounded-3xl">
        <h2 className="text-lg font-extrabold">{t('cust5.dailyTitle')} · {shopName}</h2>
        {done ? (
          <>
            <p className="rounded-xl bg-brand-soft p-3 font-semibold text-brand-deep">{t('cust5.dailyStarted', { day: dayLabel(startOn) })}</p>
            <button onClick={onClose} className={`h-12 rounded-xl ${btn.solid}`}>{t('a11y.close')}</button>
          </>
        ) : <>
          <label className="flex flex-col gap-1.5">
            <span className="text-sm font-semibold text-muted">{t('cust5.dailyItem')}</span>
            <select value={productId} onChange={(e) => setProductId(e.target.value)} className="h-12 rounded-xl border-2 border-line bg-white px-3">
              {items.map((i) => <option key={i.id} value={i.id}>{i.name} · {formatRupees(i.price)}</option>)}
            </select>
          </label>
          <div className="flex items-center justify-between gap-3">
            <span className="text-sm font-semibold text-muted">{t('cust5.dailyQty')}</span>
            <span className="flex h-12 items-center rounded-xl border-2 border-line">
              <button type="button" onClick={() => setQuantity(Math.max(1, quantity - 1))} aria-label={t('a11y.less')} className="h-full w-12 text-xl font-bold">−</button>
              <b className="w-10 text-center">{quantity}</b>
              <button type="button" onClick={() => setQuantity(Math.min(20, quantity + 1))} aria-label={t('a11y.more')} className="h-full w-12 text-xl font-bold">+</button>
            </span>
          </div>
          <fieldset className="flex flex-col gap-1.5">
            <legend className="mb-1 text-sm font-semibold text-muted">{t('cust5.dailyDays')}</legend>
            <div className="flex flex-wrap gap-1.5">
              <button type="button" onClick={() => setDays(EVERY_DAY)} aria-pressed={days === EVERY_DAY} className={`h-10 rounded-full px-3 text-sm font-bold ${days === EVERY_DAY ? 'bg-brand text-white' : 'border-2 border-line'}`}>{t('cust5.everyDay')}</button>
              {weekdays().map((name, i) => {
                const on = (days & (1 << i)) !== 0
                return <button key={name} type="button" onClick={() => setDays(on && days !== (1 << i) ? days & ~(1 << i) : days | (1 << i))} aria-pressed={on}
                  className={`h-10 min-w-11 rounded-full px-2 text-sm font-bold ${on && days !== EVERY_DAY ? 'bg-brand text-white' : on ? 'bg-brand-soft text-brand-deep' : 'border-2 border-line'}`}>{name}</button>
              })}
            </div>
          </fieldset>
          <label className="flex flex-col gap-1.5">
            <span className="text-sm font-semibold text-muted">{t('cust5.dailyFrom')}</span>
            <input type="date" value={startOn} min={indiaDay(1)} max={indiaDay(30)} onChange={(e) => setStartOn(e.target.value)} className="h-12 rounded-xl border-2 border-line px-3" />
          </label>
          <label className="flex flex-col gap-1.5">
            <span className="text-sm font-semibold text-muted">{t('store.address')}</span>
            <textarea value={where} onChange={(e) => setAddress(e.target.value)} rows={2} maxLength={300} className="rounded-xl border-2 border-line px-3 py-2" />
          </label>
          {error && <p className="text-sm text-due">{error}</p>}
          <button onClick={() => void start()} disabled={busy || !item || !where.trim()} className={`h-14 rounded-xl text-lg disabled:opacity-50 ${btn.solid}`}>
            {t('cust5.dailyStart', { amount: formatRupees(perDay) })}
          </button>
          <p className="text-center text-xs text-muted">{t('cust5.dailyNote')}</p>
        </>}
      </div>
    </div>
  )
}

/** Home: the customer's daily deliveries, with the next day, and pause tomorrow, resume or stop. */
export function DeliveriesCard() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const plans = useDeliveryPlans().data ?? []
  if (plans.length === 0) return null
  const names = weekdays()
  const daysText = (d: number) => (d === EVERY_DAY ? t('cust5.everyDay') : names.filter((_, i) => (d & (1 << i)) !== 0).join(', '))
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['me', 'deliveries'] })
  const tomorrow = indiaDay(1)
  const paused = (p: DeliveryPlan) => !!p.pauseFrom && (p.pauseTo ?? p.pauseFrom) >= indiaDay(0)

  async function stop(p: DeliveryPlan) {
    if (!(await confirmAsk(t('cust5.dailyStopAsk', { item: p.productName, shop: p.shopName })))) return
    await stopPlan(p.shopId, p.id).catch(() => {})
    await refresh()
  }

  return (
    <section aria-label={t('cust5.dailyCard')} className="flex flex-col gap-2 rounded-card border border-[#cde9e1] bg-[#f0faf7] p-3.5">
      <h2 className="font-extrabold text-brand-deep">{t('cust5.dailyCard')}</h2>
      {plans.map((p) => (
        <div key={p.id} className="flex flex-col gap-2 rounded-xl bg-white p-2.5">
          <span className="flex flex-col">
            <b>{p.quantity} × {p.productName}</b>
            <span className="text-xs text-muted">{daysText(p.days)} · {p.shopName}</span>
            <span className={`text-xs font-semibold ${paused(p) ? 'text-udhaar' : 'text-brand'}`}>
              {paused(p) ? t('cust5.pausedTill', { day: dayLabel(p.pauseTo ?? p.pauseFrom!) }) : p.nextOn ? t('cust5.nextOn', { day: dayLabel(p.nextOn) }) : ''}
            </span>
          </span>
          <span className="grid grid-cols-2 gap-2">
            {paused(p)
              ? <button onClick={() => void pausePlan(p.shopId, p.id, null, null).then(refresh)} className="h-10 rounded-xl border-2 border-brand text-sm font-bold text-brand">{t('cust5.resume')}</button>
              : <button onClick={() => void pausePlan(p.shopId, p.id, tomorrow, tomorrow).then(refresh)} className="h-10 rounded-xl border-2 border-brand text-sm font-bold text-brand">{t('cust5.skipTomorrow')}</button>}
            <button onClick={() => void stop(p)} className="h-10 rounded-xl border-2 border-line text-sm font-bold text-due">{t('cust5.stop')}</button>
          </span>
        </div>
      ))}
    </section>
  )
}

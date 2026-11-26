import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, useSyncExternalStore } from 'react'
import { useTranslation } from 'react-i18next'
import { btn, controlSm } from '../components/ui'
import { shownLocale } from '../i18n'
import { api } from '../lib/api'
import { counterName, setCounterName, useOutbox } from '../lib/data'
import { formatRupees } from '../lib/format'
import { uuidv7 } from '../lib/ids'
import { currentShift, rememberShift, subscribeShift, type CashierShift } from '../lib/shift'

const time = (iso: string) => new Date(iso).toLocaleTimeString(shownLocale(), { hour: 'numeric', minute: '2-digit' })

/**
 * Cashier shifts: open one on this counter with the cash put in the drawer (the float); bills made while it is open
 * count toward it. At the end the cashier counts the drawer without being told what should be there, and then sees
 * whether it is over or short. Below: the day's shifts (every cashier's for the owner).
 */
export function ShiftsScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const shift = useSyncExternalStore(subscribeShift, () => { try { return localStorage.getItem(`gk-shift:${shopId}`) } catch { return null } })
  const open = shift ? currentShift(shopId) : null
  const { pending } = useOutbox(shopId)
  // The server's word on this cashier's open shift (opened on another device, or closed by the owner).
  useQuery({
    queryKey: [shopId, 'shift-mine'],
    queryFn: async () => {
      const mine = await api<CashierShift | undefined>('/v1/shifts/mine')
      rememberShift(shopId, mine ?? null)
      return mine ?? null
    },
    retry: false,
  })
  const today = useQuery({ queryKey: [shopId, 'shifts'], queryFn: () => api<CashierShift[]>('/v1/shifts'), retry: false })
  const [counter, setCounter] = useState(counterName)
  const [float, setFloat] = useState('')
  const [counted, setCounted] = useState('')
  const [note, setNote] = useState('')
  const [closed, setClosed] = useState<CashierShift | null>(null)
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)

  async function run(work: () => Promise<void>) {
    setMessage('')
    setBusy(true)
    try {
      await work()
      void queryClient.invalidateQueries({ queryKey: [shopId, 'shifts'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    } finally {
      setBusy(false)
    }
  }
  const openShift = () => run(async () => {
    const opened = await api<CashierShift>('/v1/shifts', 'POST', { id: uuidv7(), counter: counter.trim(), openingFloat: Number(float) || 0 })
    setCounterName(counter)
    rememberShift(shopId, opened)
    setClosed(null)
    setFloat('')
  })
  const closeShift = () => open && run(async () => {
    const result = await api<CashierShift>(`/v1/shifts/${open.id}/close`, 'POST', { counted: Number(counted) || 0, note: note.trim() || null })
    rememberShift(shopId, null)
    setClosed(result)
    setCounted('')
    setNote('')
  })

  return (
    <div className="flex flex-col gap-4 p-4">
      <h1 className="text-xl font-bold">{t('shift.title')}</h1>
      {closed && <Result shift={closed} />}
      {!open ? (
        <section className="flex flex-col gap-3 rounded-card bg-white p-4">
          <h2 className="font-semibold">{t('shift.openTitle')}</h2>
          <p className="text-sm text-muted">{t('shift.openHint')}</p>
          <label className="flex flex-col gap-1 text-sm">
            <span className="font-semibold text-muted">{t('counter.label')}</span>
            <input value={counter} onChange={(e) => setCounter(e.target.value)} maxLength={30} placeholder="Counter 1" className={controlSm} />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span className="font-semibold text-muted">{t('shift.float')}</span>
            <input value={float} onChange={(e) => setFloat(e.target.value.replace(/[^\d.]/g, ''))} inputMode="decimal" placeholder="0" className={`${controlSm} w-40`} />
          </label>
          <button type="button" disabled={busy || !counter.trim()} onClick={() => void openShift()} className={`min-h-12 rounded-xl ${btn.solid}`}>{t('shift.open')}</button>
        </section>
      ) : (
        <section className="flex flex-col gap-3 rounded-card bg-white p-4">
          <h2 className="font-semibold">{t('shift.openNow', { counter: open.counter, time: time(open.openedAt) })}</h2>
          <dl className="grid grid-cols-2 gap-2 text-sm">
            <dt className="text-muted">{t('shift.float')}</dt><dd className="text-right font-semibold">{formatRupees(open.openingFloat)}</dd>
          </dl>
          <p className="text-sm text-muted">{t('shift.countHint')}</p>
          <label className="flex flex-col gap-1 text-sm">
            <span className="font-semibold text-muted">{t('shift.counted')}</span>
            <input value={counted} onChange={(e) => setCounted(e.target.value.replace(/[^\d.]/g, ''))} inputMode="decimal" className={`${controlSm} w-40`} />
          </label>
          <input value={note} onChange={(e) => setNote(e.target.value)} maxLength={200} placeholder={t('shift.note')} aria-label={t('shift.note')} className={controlSm} />
          {pending > 0 && <p className="rounded-xl bg-warn-soft p-2 text-sm text-warn">{t('shift.waitSync', { count: pending })}</p>}
          <button type="button" disabled={busy || counted === '' || pending > 0} onClick={() => void closeShift()} className={`min-h-12 rounded-xl ${btn.solid}`}>{t('shift.close')}</button>
        </section>
      )}
      {message && <p className="text-sm text-due">{message}</p>}
      <section className="flex flex-col gap-2">
        <h2 className="font-semibold">{isOwner ? t('shift.todayAll') : t('shift.todayMine')}</h2>
        {today.data?.length === 0 && <p className="text-sm text-muted">{t('shift.none')}</p>}
        <ul className="flex flex-col gap-2">
          {today.data?.map((s) => (
            <li key={s.id} className="flex flex-wrap items-baseline justify-between gap-2 rounded-xl bg-white p-3 text-sm">
              <span><b>{s.counter}</b> · {s.cashier ?? ''} · {time(s.openedAt)}–{s.closedAt ? time(s.closedAt) : t('shift.stillOpen')}</span>
              <span className="text-muted">{t('shift.billsSales', { count: s.bills, sales: formatRupees(s.sales) })}</span>
              {s.difference != null && <Difference amount={s.difference} />}
              {s.difference == null && s.expected != null && <span className="text-muted">{t('shift.expectedNow', { amount: formatRupees(s.expected) })}</span>}
            </li>
          ))}
        </ul>
      </section>
    </div>
  )
}

function Difference({ amount }: { amount: number }) {
  const { t } = useTranslation()
  if (amount === 0) return <span className="font-semibold text-brand-deep">{t('shift.exact')}</span>
  return amount < 0
    ? <span className="font-semibold text-due">{t('shift.short', { amount: formatRupees(-amount) })}</span>
    : <span className="font-semibold text-warn">{t('shift.over', { amount: formatRupees(amount) })}</span>
}

function Result({ shift }: { shift: CashierShift }) {
  const { t } = useTranslation()
  return (
    <section className="flex flex-col gap-2 rounded-card border-2 border-brand bg-white p-4" aria-live="polite">
      <h2 className="font-semibold">{t('shift.closedTitle', { counter: shift.counter })}</h2>
      <dl className="grid grid-cols-2 gap-1 text-sm">
        <dt className="text-muted">{t('shift.float')}</dt><dd className="text-right">{formatRupees(shift.openingFloat)}</dd>
        <dt className="text-muted">{t('shift.cashSales', { count: shift.bills })}</dt><dd className="text-right">{formatRupees(shift.cashSales ?? 0)}</dd>
        <dt className="text-muted">{t('shift.expected')}</dt><dd className="text-right font-semibold">{formatRupees(shift.expected ?? 0)}</dd>
        <dt className="text-muted">{t('shift.counted')}</dt><dd className="text-right font-semibold">{formatRupees(shift.counted ?? 0)}</dd>
      </dl>
      <Difference amount={shift.difference ?? 0} />
    </section>
  )
}

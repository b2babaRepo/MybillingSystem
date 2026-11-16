import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { indiaToday as todayIso } from '../lib/format'

interface Reading {
  id: string
  day: string
  reading: number
  factor: number
  reset: 'new_meter' | 'went_round' | null
  note: string | null
  units: number | null
  days: number | null
  perDay: number | null
  by: string | null
}

const day = (iso: string) => new Date(`${iso}T00:00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })
const num = (n: number) => n.toLocaleString('en-IN', { maximumFractionDigits: 2 })

/**
 * The bijli meter as a small log: last reading, today's, and units a day. It never works out rupees, since a bill has
 * fixed charges and slabs; the bill's amount goes in the kharcha as printed. Owners and helpers both write a reading.
 */
export function MeterCard({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const key = [shopId, 'meter']
  const history = useQuery({ queryKey: key, queryFn: () => api<Reading[]>('/v1/meter') }).data ?? []
  const [value, setValue] = useState('')
  const [reset, setReset] = useState<'' | 'new_meter' | 'went_round'>('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const last = history[0]
  const typed = Number(value)
  const lower = !!last && value !== '' && typed < last.reading
  // A soft line when use a day rises by a fifth: summer does this too, so it is a note, never an alarm.
  const rise = history[0]?.perDay && history[1]?.perDay ? Math.round((history[0].perDay / history[1].perDay - 1) * 100) : 0

  async function save() {
    setBusy(true)
    setError('')
    try {
      const next = await api<Reading[]>('/v1/meter', 'POST', { id: crypto.randomUUID(), day: todayIso(), reading: typed, reset: lower ? reset || null : null })
      queryClient.setQueryData(key, next)
      setValue('')
      setReset('')
    } catch (e) {
      setError(e instanceof Error ? e.message : t('error'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <details className="rounded-[20px] bg-white p-4">
      <summary className="flex min-h-11 cursor-pointer items-center justify-between font-bold">
        <span>⚡ {t('meter.title')}</span>
        {last && <span className="text-sm font-normal text-muted">{t('meter.lastShort', { reading: num(last.reading), day: day(last.day) })}</span>}
      </summary>
      <div className="mt-3 flex flex-col gap-2 text-sm">
        <p className="text-muted">{t('meter.hint')}</p>
        {last && <p>{t('meter.last', { reading: num(last.reading), day: day(last.day) })}</p>}
        <div className="grid grid-cols-[1fr_auto] gap-2">
          <input aria-label={t('meter.today')} placeholder={t('meter.today')} value={value} onChange={(e) => setValue(e.target.value.replace(/[^\d.]/g, ''))} inputMode="decimal"
            className="h-12 min-w-0 rounded-xl border border-line px-3 text-lg bg-white outline-none transition focus:border-teal-600 focus:ring-4 focus:ring-teal-600/10" />
          <button disabled={busy || value === '' || !(typed >= 0) || (lower && !reset)} onClick={() => void save()} className="h-12 rounded-xl bg-teal-700 px-4 font-bold text-white disabled:opacity-40">{t('meter.save')}</button>
        </div>
        {last && value !== '' && !lower && typed > last.reading && (
          <p className="font-semibold text-teal-800">{t('meter.preview', { units: num((typed - last.reading) * last.factor), days: Math.max(1, Math.round((Date.parse(todayIso()) - Date.parse(last.day)) / 86_400_000)) })}</p>
        )}
        {lower && (
          <div className="flex flex-col gap-1 rounded-xl bg-amber-50 p-3">
            <p className="font-semibold">{t('meter.lower', { last: num(last.reading) })}</p>
            <select aria-label={t('meter.why')} value={reset} onChange={(e) => setReset(e.target.value as typeof reset)} className="h-11 rounded-xl border border-line bg-white px-2 outline-none transition focus:border-teal-600 focus:ring-4 focus:ring-teal-600/10">
              <option value="">{t('meter.whyTypo')}</option>
              <option value="new_meter">{t('meter.newMeter')}</option>
              <option value="went_round">{t('meter.wentRound')}</option>
            </select>
          </div>
        )}
        {rise >= 20 && <p className="rounded-xl bg-ground p-2 text-muted">{t('meter.rise', { percent: rise })}</p>}
        {error && <p className="text-due">{error}</p>}
        {history.length > 0 && (
          <ul className="mt-1 divide-y divide-[#edf2f1]">
            {history.slice(0, 6).map((r) => (
              <li key={r.id} className="flex justify-between gap-2 py-1.5">
                <span>{day(r.day)} · {num(r.reading)}{r.reset ? ` · ${t(r.reset === 'new_meter' ? 'meter.newMeter' : 'meter.wentRound')}` : ''}</span>
                <span className="text-muted">{r.units != null && r.days === 0 ? t('meter.sameDay', { units: num(r.units) }) : r.units != null && r.days != null ? t('meter.used', { units: num(r.units), days: r.days, perDay: r.perDay != null ? num(r.perDay) : '–' }) : ''}</span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </details>
  )
}

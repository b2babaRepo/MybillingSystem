import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ask } from './Ask'
import { btn } from './ui'
import { api } from '../lib/api'
import { formatRupees, indiaToday } from '../lib/format'

interface Scheme {
  id: string; name: string; brand: string; from: string; to: string; basis: 'value' | 'quantity'; target: number; rebatePercent: number
  boughtValue: number; boughtQuantity: number; progress: number; toGo: number; met: boolean; rebateDue: number; received: number | null; receivedOn: string | null; note: string | null; daysLeft: number
}

/**
 * Company schemes (dealer rebates): buy this much of a brand in these dates and get a percent back. Progress comes from
 * the shop's supplier bills; once the company pays (usually a credit note), the amount is written down and any shortfall shows.
 */
export function BrandSchemes({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const list = useQuery({ queryKey: [shopId, 'brand-schemes'], queryFn: () => api<Scheme[]>('/v1/brand-schemes') }).data ?? []
  const [adding, setAdding] = useState(false)
  const today = indiaToday()
  const [form, setForm] = useState({ name: '', brand: '', from: today, to: today.slice(0, 8) + '28', basis: 'value', target: '', rebate: '' })
  const [error, setError] = useState('')

  async function run(action: () => Promise<unknown>) {
    setError('')
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: [shopId, 'brand-schemes'] })
    } catch (e) {
      setError(e instanceof Error ? e.message : t('error'))
    }
  }
  const add = () => run(async () => {
    await api('/v1/brand-schemes', 'POST', { id: crypto.randomUUID(), name: form.name.trim(), brand: form.brand.trim(), from: form.from, to: form.to, basis: form.basis, target: Number(form.target) || 0, rebatePercent: Number(form.rebate) || 0 })
    setAdding(false)
  })
  const received = (s: Scheme) => run(async () => {
    const amount = await ask(t('hwp9.receivedAsk', { due: formatRupees(s.rebateDue) }), String(s.rebateDue))
    if (amount === null) return
    await api(`/v1/brand-schemes/${s.id}/received`, 'POST', { amount: Number(amount) || 0 })
  })
  const show = (s: Scheme, n: number) => (s.basis === 'value' ? formatRupees(n) : `${n}`)
  const input = 'h-11 w-full rounded-xl border border-line px-3'

  return (
    <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between">
        <h2 className="font-bold">{t('hwp9.title')}</h2>
        {!adding && <button onClick={() => setAdding(true)} className={`min-h-11 ${btn.add}`}>+ {t('hwp9.add')}</button>}
      </div>
      <p className="text-xs text-muted">{t('hwp9.hint')}</p>
      {adding && (
        <div className="grid grid-cols-2 gap-2 rounded-xl border border-line p-3 text-xs text-muted">
          <label className="col-span-2">{t('hwp9.name')}<input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} maxLength={80} placeholder="Diwali dhamaka 2026" className={input} /></label>
          <label className="col-span-2">{t('hwp9.brand')}<input value={form.brand} onChange={(e) => setForm({ ...form, brand: e.target.value })} maxLength={40} placeholder="Asian Paints, Polycab…" className={input} /></label>
          <label>{t('hwp9.from')}<input type="date" value={form.from} onChange={(e) => setForm({ ...form, from: e.target.value })} className={input} /></label>
          <label>{t('hwp9.to')}<input type="date" value={form.to} onChange={(e) => setForm({ ...form, to: e.target.value })} className={input} /></label>
          <label>{t('hwp9.basis')}
            <select value={form.basis} onChange={(e) => setForm({ ...form, basis: e.target.value })} className={`${input} bg-white`}>
              <option value="value">{t('hwp9.byValue')}</option><option value="quantity">{t('hwp9.byQuantity')}</option>
            </select></label>
          <label>{t('hwp9.target')}<input value={form.target} onChange={(e) => setForm({ ...form, target: e.target.value.replace(/[^\d.]/g, '') })} inputMode="decimal" className={input} /></label>
          <label>{t('hwp9.rebate')}<input value={form.rebate} onChange={(e) => setForm({ ...form, rebate: e.target.value.replace(/[^\d.]/g, '') })} inputMode="decimal" className={input} /></label>
          <div className="col-span-2 flex gap-2">
            <button onClick={() => setAdding(false)} className={`h-11 flex-1 rounded-xl ${btn.outline}`}>{t('cancel')}</button>
            <button disabled={form.name.trim().length < 2 || form.brand.trim().length < 2 || !Number(form.target) || !Number(form.rebate)} onClick={() => void add()} className={`h-11 flex-1 rounded-xl ${btn.solid}`}>{t('hwp9.save')}</button>
          </div>
        </div>
      )}
      {error && <p role="alert" className="text-sm text-due">{error}</p>}
      {list.length === 0 && !adding && <p className="text-sm text-muted">{t('hwp9.none')}</p>}
      {list.map((s) => {
        const pct = Math.min(100, Math.round((s.progress / s.target) * 100))
        const short = s.received != null ? Math.round((s.rebateDue - s.received) * 100) / 100 : 0
        return (
          <div key={s.id} className="flex flex-col gap-1 rounded-xl border border-line p-3 text-sm">
            <p className="flex justify-between gap-2"><b>{s.name}</b><span className="text-xs text-muted">{s.from} → {s.to}</span></p>
            <p className="text-xs text-muted">{t('hwp9.line', { brand: s.brand, bought: show(s, s.progress), target: show(s, s.target), rebate: s.rebatePercent })}</p>
            <div className="h-2 overflow-hidden rounded-full bg-[#edf2f1]" role="meter" aria-valuemin={0} aria-valuemax={100} aria-valuenow={pct} aria-label={s.name}>
              <div className={`h-full rounded-full ${s.met ? 'bg-teal-600' : 'bg-amber-500'}`} style={{ width: `${pct}%` }} />
            </div>
            {s.met
              ? <p className="font-semibold text-jama">{t('hwp9.met', { due: formatRupees(s.rebateDue) })}</p>
              : <p className="font-semibold text-warn-ink">{t('hwp9.toGo', { toGo: show(s, s.toGo), days: s.daysLeft })}</p>}
            {s.received != null
              ? <p className={short > 0 ? 'font-semibold text-due' : 'text-jama'}>{short > 0 ? t('hwp9.short', { received: formatRupees(s.received), short: formatRupees(short) }) : t('hwp9.paid', { received: formatRupees(s.received) })}</p>
              : s.met && <button onClick={() => void received(s)} className={`min-h-11 self-start ${btn.link}`}>{t('hwp9.recordReceived')}</button>}
          </div>
        )
      })}
    </section>
  )
}

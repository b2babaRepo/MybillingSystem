import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { btn } from '../lib/buttonStyles'
import { api } from '../lib/api'
import { decimalText } from '../lib/decimalText'
import { formatRupees } from '../lib/format'
import type { Metal, MetalRates, OldMetal } from '../lib/jewellery'

export interface OldMetalVoucher {
  id: string; number: number; sellerName: string; sellerPhone: string | null; pan: string | null; mode: string; total: number
  oldMetal: OldMetal[]; problems: string[]; createdAt: string
}

type Row = { description: string; metal: Metal; gross: string; reading: string; deduction: string }
const blank: Row = { description: '', metal: 'gold', gross: '', reading: '', deduction: '' }
const box = 'h-11 min-w-0 rounded-xl border border-line bg-white px-3 outline-none focus:border-brand-line'

/** Worked out here as the server does: fine (gross × reading) at the pure buying rate, less the deduction. */
function estimate(r: Row, rates: MetalRates | undefined) {
  const pure = rates?.current.find((x) => x.metal === r.metal && x.purity === 999)
  const rate = pure ? pure.buyPerGram ?? pure.sellPerGram : 0
  const fine = Math.round(Number(r.gross || 0) * Number(r.reading || 0) * 10) / 1000
  const value = fine * rate
  return { rate, fine, value: Math.round((value - value * Number(r.deduction || 0) / 100) * 100) / 100 }
}

/** Old gold or silver bought from someone without a sale: a numbered purchase voucher, paid in cash, UPI or bank. */
export function OldGoldPanel({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const rates = useQuery({ queryKey: [shopId, 'jewellery', 'rates'], queryFn: () => api<MetalRates>('/v1/jewellery/rates') }).data
  const vouchers = useQuery({ queryKey: [shopId, 'jewellery', 'old-metal-vouchers'], queryFn: () => api<OldMetalVoucher[]>('/v1/jewellery/old-metal-vouchers') }).data ?? []
  const [rows, setRows] = useState<Row[]>([{ ...blank }])
  const [name, setName] = useState('')
  const [phone, setPhone] = useState('')
  const [address, setAddress] = useState('')
  const [pan, setPan] = useState('')
  const [mode, setMode] = useState<'cash' | 'upi' | 'bank'>('upi')
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const worked = rows.map((r) => estimate(r, rates))
  const total = Math.round(worked.reduce((s, w) => s + w.value, 0) * 100) / 100
  const noRate = rows.some((r, i) => Number(r.gross) > 0 && worked[i].rate <= 0)
  const ready = name.trim() && total > 0 && !noRate && (total < 200_000 || pan.trim())
  const set = (i: number, patch: Partial<Row>) => setRows((all) => all.map((r, j) => (j === i ? { ...r, ...patch } : r)))

  async function save() {
    setBusy(true); setMessage(null)
    try {
      const v = await api<OldMetalVoucher>('/v1/jewellery/old-metal-vouchers', 'POST', {
        id: crypto.randomUUID(), sellerName: name.trim(), sellerPhone: phone.trim() || null, sellerAddress: address.trim() || null, pan: pan.trim() || null, mode,
        oldMetal: rows.filter((r) => Number(r.gross) > 0).map((r) => ({ description: r.description.trim() || null, metal: r.metal, gross: Number(r.gross), reading: Number(r.reading), deductionPercent: Number(r.deduction || 0) })),
      })
      setMessage(t('jwl5.saved', { number: v.number, amount: formatRupees(v.total), name: v.sellerName }))
      setRows([{ ...blank }]); setName(''); setPhone(''); setAddress(''); setPan('')
      void qc.invalidateQueries({ queryKey: [shopId, 'jewellery'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    } finally { setBusy(false) }
  }

  return (
    <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_minmax(0,22rem)]">
      <section className="flex flex-col gap-3 rounded-card bg-white p-4 shadow-sm">
        <p className="text-sm text-muted">{t('jwl5.hint')}</p>
        <div className="grid gap-2 sm:grid-cols-2">
          <label className="flex flex-col gap-1 text-sm font-semibold">{t('jwl5.sellerName')}<input value={name} onChange={(e) => setName(e.target.value)} maxLength={80} className={box} /></label>
          <label className="flex flex-col gap-1 text-sm font-semibold">{t('jwl5.sellerPhone')}<input value={phone} onChange={(e) => setPhone(e.target.value)} inputMode="tel" maxLength={20} className={box} /></label>
          <label className="flex flex-col gap-1 text-sm font-semibold sm:col-span-2">{t('jwl5.sellerAddress')}<input value={address} onChange={(e) => setAddress(e.target.value)} maxLength={200} className={box} /></label>
        </div>
        {rows.map((r, i) => (
          <div key={i} className="grid grid-cols-2 gap-2 rounded-xl bg-[#fdf6ec] p-2 sm:grid-cols-[minmax(0,1.5fr)_6rem_repeat(3,minmax(0,1fr))]">
            <input aria-label={t('jwl5.what')} placeholder={t('jwl5.what')} value={r.description} onChange={(e) => set(i, { description: e.target.value })} maxLength={120} className={`${box} col-span-2 sm:col-span-1`} />
            <select aria-label={t('jwl.metal')} value={r.metal} onChange={(e) => set(i, { metal: e.target.value as Metal })} className={box}>
              <option value="gold">{t('jwl.gold')}</option><option value="silver">{t('jwl.silver')}</option>
            </select>
            <input aria-label={t('jwl5.gross')} placeholder={t('jwl5.gross')} value={r.gross} onChange={(e) => set(i, { gross: decimalText(e.target.value) })} inputMode="decimal" className={box} />
            <input aria-label={t('jwl5.reading')} placeholder={t('jwl5.reading')} value={r.reading} onChange={(e) => set(i, { reading: decimalText(e.target.value) })} inputMode="decimal" className={box} />
            <input aria-label={t('jwl5.deduction')} placeholder={t('jwl5.deduction')} value={r.deduction} onChange={(e) => set(i, { deduction: decimalText(e.target.value) })} inputMode="decimal" className={box} />
            {worked[i].fine > 0 && <p className="col-span-full text-right text-sm">{t('jwl5.fine', { fine: worked[i].fine.toFixed(3) })} · <b>{formatRupees(worked[i].value)}</b></p>}
          </div>
        ))}
        <button onClick={() => setRows((all) => [...all, { ...blank }])} disabled={rows.length >= 20} className={`h-11 rounded-xl border-2 border-dashed border-brand-line font-bold text-brand`}>{t('jwl5.addPiece')}</button>
        {noRate && <p className="text-sm font-semibold text-due">{t('jwl5.setRate')}</p>}
        {total >= 200_000 && (
          <label className="flex flex-col gap-1 text-sm font-semibold">{t('jwl5.pan')}<input value={pan} onChange={(e) => setPan(e.target.value.toUpperCase())} maxLength={10} className={box} />
            <span className="font-normal text-due">{t('jwl5.panNeeded')}</span></label>
        )}
        <div role="radiogroup" aria-label={t('jwl5.payBy')} className="grid grid-cols-3 gap-2">
          {(['cash', 'upi', 'bank'] as const).map((m) => (
            <button key={m} role="radio" aria-checked={mode === m} onClick={() => setMode(m)} className={`h-11 rounded-xl font-bold ${mode === m ? btn.solid : btn.outline}`}>{t(`jwl5.${m}`)}</button>
          ))}
        </div>
        {mode === 'cash' && total > 10_000 && <p className="rounded-xl bg-due-soft p-2 text-sm text-due">{t('jwl5.cashWarn')}</p>}
        <button onClick={() => void save()} disabled={!ready || busy} className={`h-12 rounded-xl ${btn.solid}`}>{t('jwl5.pay', { amount: formatRupees(total) })}</button>
        {message && <p role="status" className="rounded-xl bg-[#e3f2ee] p-2 text-sm font-semibold text-[#0b5e4f]">{message}</p>}
      </section>
      <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
        <h2 className="font-extrabold">{t('jwl5.recent')}</h2>
        {vouchers.length === 0 && <p className="text-sm text-muted">{t('jwl5.none')}</p>}
        {vouchers.map((v) => (
          <div key={v.id} className="flex items-start justify-between gap-2 border-b border-line py-2 text-sm last:border-0">
            <div className="min-w-0">
              <b>{t('jwl5.voucher', { number: v.number })} · {v.sellerName}</b>
              <p className="text-muted">{v.oldMetal.map((o) => `${o.description ?? o.metal} ${o.gross.toFixed(3)} g @ ${o.reading}%`).join(', ')}</p>
            </div>
            <span className="shrink-0 text-right font-bold">{formatRupees(v.total)}<span className="block text-xs font-normal uppercase text-muted">{v.mode}</span></span>
          </div>
        ))}
      </section>
    </div>
  )
}

import { shownLocale } from '../i18n'
import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Button, control } from '../components/ui'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'

type Kind = 'opening' | 'came_with_goods' | 'bought' | 'sold' | 'returned_to_supplier' | 'used' | 'damaged' | 'counted'
interface BagType { id: string; name: string; saleRate: number | null; isActive: boolean; onHand: number; value: number | null; fromGoods?: boolean }
interface SupplierBags { supplierId: string; supplier: string; came: number; returned: number; holding: number }
interface Summary { types: BagType[]; suppliers: SupplierBags[]; soldThisMonth: number; boughtThisMonth: number }
interface Move { id: string; bagType: string; kind: Kind; quantity: number; rate: number | null; amount: number | null; mode: string | null; supplier: string | null; party: string | null; note: string | null; occurredAt: string; struck: boolean; by: string | null }
interface Supplier { id: string; name: string }

const input = control
const KINDS: Kind[] = ['came_with_goods', 'sold', 'returned_to_supplier', 'used', 'damaged', 'bought', 'counted', 'opening']
const blank = { kind: 'came_with_goods' as Kind, quantity: '', rate: '', mode: 'cash', supplierId: '', party: '', note: '' }

/**
 * Bardana: the empty-bag account. Bags come in with goods (or are bought), go out to the bardana dealer, back to the
 * supplier, into use or the bin. Sales and purchases land in the day's cash book; a count fixes the book.
 */
export function BardanaScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const summary = useQuery({ queryKey: [shopId, 'bardana'], queryFn: () => api<Summary>('/v1/bardana') }).data
  const moves = useQuery({ queryKey: [shopId, 'bardana', 'moves'], queryFn: () => api<Move[]>('/v1/bardana/moves') }).data ?? []
  const suppliers = useQuery({ queryKey: [shopId, 'suppliers', 'list'], queryFn: () => api<Supplier[]>('/v1/suppliers') }).data ?? []
  const [typeName, setTypeName] = useState('')
  const [typeRate, setTypeRate] = useState('')
  const [typeId, setTypeId] = useState<string | null>(null)
  const [form, setForm] = useState(blank)
  const [message, setMessage] = useState('')
  const types = (summary?.types ?? []).filter((x) => x.isActive)
  const type = types.find((x) => x.id === typeId)
  const paid = form.kind === 'sold' || form.kind === 'bought'
  const withSupplier = form.kind === 'came_with_goods' || form.kind === 'returned_to_supplier'
  const qty = Number(form.quantity)

  async function run(action: () => Promise<unknown>) {
    setMessage('')
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: [shopId, 'bardana'] })
      return true
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
      return false
    }
  }

  const addType = () => run(async () => {
    await api('/v1/bardana/types', 'POST', { id: crypto.randomUUID(), name: typeName.trim(), saleRate: typeRate === '' ? null : Number(typeRate) })
    setTypeName('')
    setTypeRate('')
  })
  const save = () => run(async () => {
    await api('/v1/bardana/moves', 'POST', {
      id: crypto.randomUUID(), bagTypeId: typeId, kind: form.kind, quantity: qty, rate: paid ? Number(form.rate) : null, mode: paid ? form.mode : null,
      supplierId: withSupplier && form.supplierId ? form.supplierId : null, party: form.party.trim() || null, note: form.note.trim() || null,
    })
    setTypeId(null)
    setForm(blank)
  })
  const open = (x: BagType, kind: Kind) => { setTypeId(x.id); setForm({ ...blank, kind, rate: kind === 'sold' && x.saleRate != null ? String(x.saleRate) : '' }) }
  const ok = !!typeId && form.quantity !== '' && Number.isInteger(qty) && (form.kind === 'counted' ? qty >= 0 : qty > 0)
    && (!paid || Number(form.rate) > 0) && (form.kind !== 'returned_to_supplier' || !!form.supplierId)

  return (
    <div className="flex flex-col gap-3 p-4">
      <header>
        <h1 className="text-xl font-bold">{t('bardana.title')}</h1>
        <p className="text-sm text-muted">{t('bardana.hint')}</p>
      </header>

      {summary && (summary.soldThisMonth > 0 || summary.boughtThisMonth > 0) && (
        <p className="rounded-xl bg-brand-soft p-3 text-sm text-brand-deep">{t('bardana.month', { sold: formatRupees(summary.soldThisMonth), bought: formatRupees(summary.boughtThisMonth) })}</p>
      )}

      <section className="grid grid-cols-1 gap-2 sm:grid-cols-2">
        {types.map((x) => (
          <div key={x.id} className="flex flex-col gap-1 rounded-card bg-white p-3 shadow-sm">
            <p className="flex justify-between font-bold"><span>👜 {x.name}</span><span className="tabular-nums">{x.onHand}</span></p>
            {isOwner && (
              <label className="flex items-center gap-2 text-xs">
                <input type="checkbox" checked={!!x.fromGoods} className="h-4 w-4"
                  onChange={(e) => void run(() => api('/v1/bardana/types', 'POST', { id: x.id, name: x.name, saleRate: x.saleRate, isActive: true, fromGoods: e.target.checked }))} />
                {t('bardana.fromGoods')}
              </label>
            )}
            <p className="text-xs text-muted">{x.saleRate != null ? t('bardana.rateEach', { rate: formatRupees(x.saleRate) }) : t('bardana.noRate')}{x.value != null ? ` · ${t('bardana.worth', { value: formatRupees(x.value) })}` : ''}</p>
            <div className="mt-1 flex flex-wrap gap-2 text-xs font-semibold">
              <button onClick={() => open(x, 'came_with_goods')} className="min-h-9 rounded-xl border border-line px-2">+ {t('bardana.kind.came_with_goods')}</button>
              <button onClick={() => open(x, 'sold')} className="min-h-9 rounded-xl border border-line px-2">₹ {t('bardana.kind.sold')}</button>
              <button onClick={() => open(x, 'counted')} className="min-h-9 rounded-xl border border-line px-2">🔢 {t('bardana.kind.counted')}</button>
            </div>
          </div>
        ))}
        {isOwner && (
          <div className="flex flex-col gap-2 rounded-card border border-dashed border-line p-3">
            <div className="grid grid-cols-[1fr_6rem] gap-2">
              <input aria-label={t('bardana.typeName')} placeholder={t('bardana.typeName')} value={typeName} maxLength={60} onChange={(e) => setTypeName(e.target.value)} className={input} />
              <input aria-label={t('bardana.saleRate')} placeholder={t('bardana.saleRate')} value={typeRate} onChange={(e) => setTypeRate(e.target.value)} inputMode="decimal" className={input} />
            </div>
            <Button disabled={!typeName.trim()} onClick={() => void addType()}>+ {t('bardana.addType')}</Button>
          </div>
        )}
      </section>

      {type && (
        <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
          <h2 className="font-bold">{type.name} · {t('bardana.inBook', { qty: type.onHand })}</h2>
          <div className="flex flex-wrap gap-2">
            {KINDS.map((k) => (
              <button key={k} onClick={() => setForm({ ...form, kind: k, rate: k === 'sold' && type.saleRate != null ? String(type.saleRate) : form.rate })}
                className={`min-h-10 rounded-full border px-3 text-sm font-semibold ${form.kind === k ? 'border-brand bg-brand text-white' : 'border-line'}`}>{t(`bardana.kind.${k}`)}</button>
            ))}
          </div>
          <input aria-label={t('bardana.qty')} placeholder={form.kind === 'counted' ? t('bardana.countedQty') : t('bardana.qty')} value={form.quantity}
            onChange={(e) => setForm({ ...form, quantity: e.target.value })} inputMode="numeric" className={input} />
          {form.kind === 'counted' && form.quantity !== '' && <p className="text-xs text-muted">{t('bardana.diff', { diff: qty - type.onHand })}</p>}
          {paid && (
            <div className="grid grid-cols-2 gap-2">
              <input aria-label={t('bardana.rate')} placeholder={t('bardana.rate')} value={form.rate} onChange={(e) => setForm({ ...form, rate: e.target.value })} inputMode="decimal" className={input} />
              <select aria-label={t('bardana.mode')} value={form.mode} onChange={(e) => setForm({ ...form, mode: e.target.value })} className={input}>
                <option value="cash">{t('bardana.cash')}</option><option value="upi">UPI</option>
              </select>
              {qty > 0 && Number(form.rate) > 0 && <p className="col-span-2 text-sm font-semibold">{t('bardana.amount', { amount: formatRupees(qty * Number(form.rate)) })}</p>}
            </div>
          )}
          {withSupplier && (
            <select aria-label={t('bardana.supplier')} value={form.supplierId} onChange={(e) => setForm({ ...form, supplierId: e.target.value })} className={input}>
              <option value="">{form.kind === 'came_with_goods' ? t('bardana.anySupplier') : t('bardana.pickSupplier')}</option>
              {suppliers.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
            </select>
          )}
          {paid && <input aria-label={t('bardana.party')} placeholder={t('bardana.party')} value={form.party} maxLength={80} onChange={(e) => setForm({ ...form, party: e.target.value })} className={input} />}
          <input aria-label={t('bardana.note')} placeholder={t('bardana.note')} value={form.note} maxLength={200} onChange={(e) => setForm({ ...form, note: e.target.value })} className={input} />
          <div className="grid grid-cols-2 gap-2">
            <Button disabled={!ok} onClick={() => void save()}>{t('bardana.save')}</Button>
            <button onClick={() => setTypeId(null)} className="min-h-12 rounded-2xl border border-line font-semibold">{t('cancel')}</button>
          </div>
        </section>
      )}
      {message && <p className="text-sm text-due">{message}</p>}

      {(summary?.suppliers.length ?? 0) > 0 && (
        <section className="flex flex-col gap-1 rounded-card bg-white p-4 shadow-sm">
          <h2 className="font-bold">{t('bardana.suppliersTitle')}</h2>
          {summary!.suppliers.map((s) => (
            <p key={s.supplierId} className="flex justify-between gap-2 border-t border-line py-1.5 text-sm">
              <span>{s.supplier}</span>
              <span className="text-right text-xs text-muted">{t('bardana.supplierLine', { came: s.came, returned: s.returned })} · <b className="text-ink">{t('bardana.holding', { qty: s.holding })}</b></span>
            </p>
          ))}
        </section>
      )}

      {moves.length > 0 && (
        <section className="flex flex-col gap-1 rounded-card bg-white p-4 shadow-sm">
          <h2 className="font-bold">{t('bardana.moves')}</h2>
          {moves.map((m) => (
            <div key={m.id} className={`flex items-start justify-between gap-2 border-t border-line py-1.5 text-sm ${m.struck ? 'text-muted line-through' : ''}`}>
              <div>
                <p className="font-semibold">{t(`bardana.kind.${m.kind}`)} · {m.bagType}</p>
                <p className="text-xs text-muted">{new Date(m.occurredAt).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })}{[m.supplier, m.party, m.note, m.by].filter(Boolean).map((x) => ` · ${x}`).join('')}{m.amount != null ? ` · ${formatRupees(m.amount)} ${m.mode === 'upi' ? 'UPI' : t('bardana.cash')}` : ''}</p>
              </div>
              <div className="flex flex-col items-end">
                <span className={`font-semibold tabular-nums ${m.quantity < 0 ? 'text-due' : 'text-brand'}`}>{m.quantity > 0 ? '+' : ''}{m.quantity}</span>
                {isOwner && !m.struck && <button onClick={() => void run(() => api(`/v1/bardana/moves/${m.id}/strike`, 'POST', {}))} className="text-xs font-semibold text-due">{t('bardana.strike')}</button>}
              </div>
            </div>
          ))}
        </section>
      )}
    </div>
  )
}

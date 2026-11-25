import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { btn } from '../components/ui'
import { api } from '../lib/api'
import { useCustomers, useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'
import { litresFor, packLitres, packMix, roomArea, type Room } from '../lib/paintQuote'

const blankRoom = (n: number): Room => ({ name: `Room ${n}`, length: 12, width: 10, height: 10, openings: 40, ceiling: false })

/**
 * Paint quotation: rooms measured in feet, doors and windows taken off, coats and coverage → litres → the cheapest
 * tins of the chosen paint (its 1 L, 4 L, 10 L, 20 L items) → saved as an estimate the customer can approve.
 */
export function PaintQuote({ shopId, onClose, onSaved }: { shopId: string; onClose: () => void; onSaved: (number: number) => void }) {
  const { t } = useTranslation()
  const { products } = useProducts(shopId)
  const { customers } = useCustomers(shopId)
  const [rooms, setRooms] = useState<Room[]>([blankRoom(1)])
  const [coats, setCoats] = useState('2')
  const [coverage, setCoverage] = useState('120')
  const [word, setWord] = useState('')
  const [customerId, setCustomerId] = useState('')
  const [party, setParty] = useState('')
  const [error, setError] = useState('')

  const area = rooms.reduce((a, r) => a + roomArea(r), 0)
  const litres = litresFor(area, Number(coats) || 0, Number(coverage) || 0)
  // The paint's tins: items whose name has the word typed and a size in litres.
  const tins = word.trim().length >= 2
    ? products.filter((p) => p.name.toLowerCase().includes(word.trim().toLowerCase()) && packLitres(p.name) && p.sellingPrice > 0)
      .map((p) => ({ id: p.id, name: p.name, litres: packLitres(p.name)!, price: p.sellingPrice, unit: p.unit }))
    : []
  const mix = packMix(litres, tins)
  const total = mix.reduce((a, x) => a + x.count * x.pack.price, 0)
  const set = (i: number, patch: Partial<Room>) => setRooms(rooms.map((r, k) => (k === i ? { ...r, ...patch } : r)))
  const num = (v: string) => Number(v.replace(/[^\d.]/g, '')) || 0

  async function save() {
    setError('')
    try {
      const note = rooms.map((r) => `${r.name}: ${r.length}×${r.width}×${r.height} ft${r.ceiling ? ' + ceiling' : ''}`).join('; ')
        + ` · ${Math.round(area)} sq ft · ${coats} coats · ${litres} L`
      const saved = await api<{ number: number }>('/v1/estimates', 'POST', {
        id: crypto.randomUUID(), customerId: customerId || null, partyName: customerId ? null : party.trim() || null, note: note.slice(0, 300),
        lines: mix.map((x) => ({ productId: x.pack.id, quantity: x.count, unitPrice: x.pack.price })),
      })
      onSaved(saved.number)
    } catch (e) {
      setError(e instanceof Error ? e.message : t('error'))
    }
  }

  const field = 'h-11 w-full rounded-xl border border-line px-2 text-right'
  return (
    <section className="flex flex-col gap-3 rounded-card bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-bold">🎨 {t('hwp7.title')}</h2>
        <button onClick={onClose} className={`min-h-11 ${btn.link}`}>{t('cancel')}</button>
      </div>
      <p className="text-xs text-muted">{t('hwp7.hint')}</p>
      {rooms.map((r, i) => (
        <div key={i} className="flex flex-col gap-2 rounded-xl border border-line p-3">
          <div className="flex items-center gap-2">
            <input value={r.name} onChange={(e) => set(i, { name: e.target.value })} aria-label={t('hwp7.room')} className="h-11 min-w-0 flex-1 rounded-xl border border-line px-3 font-semibold" />
            {rooms.length > 1 && <button onClick={() => setRooms(rooms.filter((_, k) => k !== i))} aria-label={t('hwp7.remove')} className="h-11 w-11 rounded-xl border border-line">✕</button>}
          </div>
          <div className="grid grid-cols-4 gap-2 text-xs text-muted">
            {(['length', 'width', 'height', 'openings'] as const).map((k) => (
              <label key={k}>{t(`hwp7.${k}`)}
                <input value={String(r[k])} onChange={(e) => set(i, { [k]: num(e.target.value) })} inputMode="decimal" className={field} /></label>
            ))}
          </div>
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={r.ceiling} onChange={(e) => set(i, { ceiling: e.target.checked })} className="h-5 w-5" />{t('hwp7.ceiling')}</label>
          <p className="text-xs text-muted">{t('hwp7.area', { area: Math.round(roomArea(r)) })}</p>
        </div>
      ))}
      <button onClick={() => setRooms([...rooms, blankRoom(rooms.length + 1)])} className={`min-h-11 self-start ${btn.add}`}>+ {t('hwp7.addRoom')}</button>
      <div className="grid grid-cols-2 gap-2 text-xs text-muted">
        <label>{t('hwp7.coats')}<input value={coats} onChange={(e) => setCoats(e.target.value.replace(/[^\d]/g, ''))} inputMode="numeric" className={field} /></label>
        <label>{t('hwp7.coverage')}<input value={coverage} onChange={(e) => setCoverage(e.target.value.replace(/[^\d.]/g, ''))} inputMode="decimal" className={field} /></label>
      </div>
      <p className="rounded-xl bg-brand-soft p-3 font-semibold text-brand-deep">{t('hwp7.need', { area: Math.round(area), litres })}</p>
      <label className="text-xs text-muted">{t('hwp7.paint')}
        <input value={word} onChange={(e) => setWord(e.target.value)} placeholder={t('hwp7.paintPlaceholder')} className="h-11 w-full rounded-xl border border-line px-3 text-base" /></label>
      {word.trim().length >= 2 && tins.length === 0 && <p className="text-sm text-due">{t('hwp7.noTins')}</p>}
      {mix.length > 0 && (
        <ul className="flex flex-col gap-1 text-sm">
          {mix.map((x) => <li key={x.pack.id} className="flex justify-between"><span>{x.count} × {x.pack.name}</span><b>{formatRupees(x.count * x.pack.price)}</b></li>)}
          <li className="flex justify-between border-t border-line pt-1 font-bold"><span>{t('hwp7.total', { litres: mix.reduce((a, x) => a + x.count * x.pack.litres, 0) })}</span><span>{formatRupees(total)}</span></li>
        </ul>
      )}
      <div className="grid grid-cols-2 gap-2">
        <select value={customerId} onChange={(e) => setCustomerId(e.target.value)} aria-label={t('hwp7.customer')} className="h-11 rounded-xl border border-line bg-white px-2">
          <option value="">{t('hwp7.noCustomer')}</option>
          {customers.filter((c) => !c.nameLater).map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
        {!customerId && <input value={party} onChange={(e) => setParty(e.target.value)} maxLength={80} placeholder={t('hwp7.party')} aria-label={t('hwp7.party')} className="h-11 rounded-xl border border-line px-3" />}
      </div>
      {error && <p role="alert" className="text-sm text-due">{error}</p>}
      <button disabled={mix.length === 0} onClick={() => void save()} className={`h-12 rounded-xl ${btn.solid}`}>{t('hwp7.save')}</button>
    </section>
  )
}

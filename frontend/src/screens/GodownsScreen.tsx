import { DataTable } from '../components/DataTable'
import { shownLocale } from '../i18n'
import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ItemPicker } from '../components/ItemPicker'
import { Button, control } from '../components/ui'
import { api } from '../lib/api'
import { useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'

interface Godown { id: string; name: string; address: string | null; isActive: boolean; items: number; units: number; value: number }
interface PlaceStock { productId: string; name: string; category: string; unit: string; total: number; shelf: number; godowns: Record<string, number> }
interface Transfer { id: string; number: number; from: string; to: string; occurredAt: string; note: string | null; cancelled: boolean; by: string | null; lines: { productId: string; name: string; quantity: number; bags: number | null }[] }
interface Row { text: string; productId: string; qty: string }

const input = control
const SHOP = ''
const blank: Row = { text: '', productId: '', qty: '' }

/**
 * Godowns: where the shop's stock is kept. Each item's stock is split into the shop shelf and each godown, and a move
 * takes goods from one place to another without changing the shop's total.
 */
export function GodownsScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const godowns = useQuery({ queryKey: [shopId, 'godowns'], queryFn: () => api<Godown[]>('/v1/godowns') }).data ?? []
  const stock = useQuery({ queryKey: [shopId, 'godowns', 'stock'], queryFn: () => api<PlaceStock[]>('/v1/godowns/stock') }).data ?? []
  const transfers = useQuery({ queryKey: [shopId, 'godowns', 'transfers'], queryFn: () => api<Transfer[]>('/v1/godowns/transfers') }).data ?? []
  const { products } = useProducts(shopId)
  const [name, setName] = useState('')
  const [from, setFrom] = useState(SHOP)
  const [to, setTo] = useState('')
  const [rows, setRows] = useState<Row[] | null>(null)
  const [note, setNote] = useState('')
  const [search, setSearch] = useState('')
  const [message, setMessage] = useState('')
  const active = godowns.filter((g) => g.isActive)
  const place = (id: string) => (id === SHOP ? t('godown.shop') : godowns.find((g) => g.id === id)?.name ?? '')
  const thereOf = (productId: string) => {
    const s = stock.find((x) => x.productId === productId)
    return s ? (from === SHOP ? s.shelf : s.godowns[from] ?? 0) : 0
  }
  const valid = (rows ?? []).filter((r) => r.productId && Number(r.qty) > 0)

  async function run(action: () => Promise<unknown>) {
    setMessage('')
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: [shopId, 'godowns'] })
      return true
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
      return false
    }
  }

  const addGodown = () => run(async () => { await api('/v1/godowns', 'POST', { id: crypto.randomUUID(), name: name.trim() }); setName('') })
  const move = () => run(async () => {
    await api('/v1/godowns/transfers', 'POST', { id: crypto.randomUUID(), fromGodownId: from || null, toGodownId: to || null, note: note.trim() || null, lines: valid.map((r) => ({ productId: r.productId, quantity: Number(r.qty) })) })
    setRows(null)
    setNote('')
  })
  const startMove = (fromId: string, toId: string) => { setFrom(fromId); setTo(toId); setRows([blank]) }
  const shown = stock.filter((s) => (s.total !== 0 || Object.keys(s.godowns).length > 0) && s.name.toLowerCase().includes(search.trim().toLowerCase()))

  return (
    <div className="flex flex-col gap-3 p-4">
      <header>
        <h1 className="text-xl font-bold">{t('godown.title')}</h1>
        <p className="text-sm text-muted">{t('godown.hint')}</p>
      </header>

      <section className="grid grid-cols-2 gap-2 sm:grid-cols-3">
        {active.map((g) => (
          <div key={g.id} className="flex flex-col gap-1 rounded-card bg-white p-3 shadow-sm">
            <p className="font-bold">🏬 {g.name}</p>
            <p className="text-xs text-muted">{t('godown.holds', { items: g.items })}{isOwner ? ` · ${formatRupees(g.value)}` : ''}</p>
            <div className="mt-1 flex gap-2 text-xs font-semibold text-brand">
              <button onClick={() => startMove(SHOP, g.id)} className="min-h-9">{t('godown.sendHere')}</button>
              <button onClick={() => startMove(g.id, SHOP)} className="min-h-9">{t('godown.bringToShop')}</button>
            </div>
          </div>
        ))}
        {isOwner && (
          <div className="flex flex-col gap-2 rounded-card border border-dashed border-line p-3">
            <input aria-label={t('godown.name')} placeholder={t('godown.name')} value={name} maxLength={60} onChange={(e) => setName(e.target.value)} className={input} />
            <Button disabled={!name.trim()} onClick={() => void addGodown()}>+ {t('godown.add')}</Button>
          </div>
        )}
      </section>

      {active.length > 0 && !rows && <Button onClick={() => startMove(SHOP, active[0].id)}>⇄ {t('godown.move')}</Button>}

      {rows && (
        <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
          <h2 className="font-bold">{t('godown.move')}</h2>
          <div className="grid grid-cols-2 gap-2">
            <label className="flex flex-col gap-1 text-xs text-muted">{t('godown.from')}
              <select value={from} onChange={(e) => setFrom(e.target.value)} className={input}>
                <option value={SHOP}>{t('godown.shop')}</option>
                {active.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
              </select>
            </label>
            <label className="flex flex-col gap-1 text-xs text-muted">{t('godown.to')}
              <select value={to} onChange={(e) => setTo(e.target.value)} className={input}>
                <option value={SHOP}>{t('godown.shop')}</option>
                {active.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
              </select>
            </label>
          </div>
          {rows.map((r, i) => {
            const set = (next: Partial<Row>) => setRows(rows.map((x, j) => (j === i ? { ...x, ...next } : x)))
            const there = r.productId ? thereOf(r.productId) : null
            return (
              <div key={i} className="grid grid-cols-[1fr_5rem] gap-2">
                <ItemPicker products={products.filter((p) => !p.isService)} text={r.text} placeholder={t('supplier2.itemSearch')}
                  onText={(text) => set({ text, productId: products.find((p) => p.name.toLowerCase() === text.trim().toLowerCase())?.id ?? '' })}
                  onPick={(p) => set({ text: p.name, productId: p.id })} />
                <input aria-label={t('suppliers.qty')} placeholder={t('suppliers.qty')} value={r.qty} onChange={(e) => set({ qty: e.target.value })} inputMode="decimal" className={input} />
                {there !== null && <p className={`col-span-2 text-xs ${Number(r.qty) > there ? 'font-semibold text-due' : 'text-muted'}`}>{t('godown.there', { qty: there, place: place(from) })}</p>}
              </div>
            )
          })}
          <button onClick={() => setRows([...rows, blank])} className="min-h-11 font-semibold text-brand">+ {t('suppliers.addRow')}</button>
          <input aria-label={t('po.note')} placeholder={t('godown.note')} value={note} maxLength={200} onChange={(e) => setNote(e.target.value)} className={input} />
          <div className="grid grid-cols-2 gap-2">
            <Button disabled={valid.length === 0 || from === to} onClick={() => void move()}>{t('godown.save')}</Button>
            <button onClick={() => setRows(null)} className="min-h-12 rounded-2xl border border-line font-semibold">{t('cancel')}</button>
          </div>
        </section>
      )}
      {message && <p className="text-sm text-due">{message}</p>}

      {active.length > 0 && (
        <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
          <h2 className="font-bold">{t('godown.whereTitle')}</h2>
          <input aria-label={t('godown.search')} placeholder={t('godown.search')} value={search} onChange={(e) => setSearch(e.target.value)} className={input} />
          <DataTable label={t('godown.item')} rows={shown.slice(0, 300)} rowKey={(s) => s.productId}
            columns={[
              { key: 'item', header: t('godown.item'), sortValue: (s) => s.name, render: (s) => s.name },
              { key: 'shop', header: t('godown.shop'), align: 'right', sortValue: (s) => s.shelf, render: (s) => <span className={`tabular-nums ${s.shelf < 0 ? 'font-semibold text-due' : ''}`}>{s.shelf}</span> },
              ...active.map((g) => ({ key: g.id, header: g.name, align: 'right' as const, sortValue: (s: typeof shown[number]) => s.godowns[g.id] ?? 0, render: (s: typeof shown[number]) => <span className="tabular-nums">{s.godowns[g.id] ?? '–'}</span> })),
              { key: 'total', header: t('godown.total'), align: 'right', sortValue: (s) => s.total, render: (s) => <span className="font-semibold tabular-nums">{s.total}</span> },
            ]} />
          {shown.some((s) => s.shelf < 0) && <p className="text-xs text-due">{t('godown.shelfShort')}</p>}
        </section>
      )}

      {transfers.length > 0 && (
        <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
          <h2 className="font-bold">{t('godown.moves')}</h2>
          {transfers.map((m) => (
            <div key={m.id} className={`flex flex-col gap-0.5 rounded-xl border border-line p-3 text-sm ${m.cancelled ? 'text-muted line-through' : ''}`}>
              <p className="flex justify-between font-semibold">
                <span>#{m.number} · {m.from === 'Shop' ? t('godown.shop') : m.from} → {m.to === 'Shop' ? t('godown.shop') : m.to}</span>
                <span className="text-xs text-muted">{new Date(m.occurredAt).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })}{m.by ? ` · ${m.by}` : ''}</span>
              </p>
              <p className="text-xs">{m.lines.map((l) => `${l.name} × ${l.quantity}`).join(', ')}{m.note ? ` · ${m.note}` : ''}</p>
              {isOwner && !m.cancelled && (
                <button onClick={() => void run(() => api(`/v1/godowns/transfers/${m.id}/cancel`, 'POST', {}))} className="self-start text-xs font-semibold text-due">{t('godown.undo')}</button>
              )}
            </div>
          ))}
        </section>
      )}
    </div>
  )
}

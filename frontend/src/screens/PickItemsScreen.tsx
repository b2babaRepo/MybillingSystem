import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { api } from '../lib/api'
import type { Unit } from '../lib/types'

interface MasterChange { productId: string; productName: string; gstFrom: number | null; gstTo: number | null; hsnFrom: string | null; hsnTo: string | null }

interface MasterItem { id: number; trade: string; category: string; name: string; nameHi: string | null; unit: Unit; gstRate: number | null; packSizes: string[] }

/** Pick items for the shop from its trades' master lists: search, a category at a time, or all of them. */
export function PickItemsScreen({ shopId }: { shopId: string }) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const list = useQuery({ queryKey: ['master-items'], queryFn: () => api<MasterItem[]>('/v1/master-items') })
  const updates = useQuery({ queryKey: [shopId, 'master-updates'], queryFn: () => api<{ changes: MasterChange[]; newItems: MasterItem[] }>('/v1/master-items/updates') })
  const newIds = new Set((updates.data?.newItems ?? []).map((i) => i.id))
  const changes = updates.data?.changes ?? []
  const items = useMemo(() => list.data ?? [], [list.data])
  const trades = [...new Set(items.map((i) => i.trade))]
  const [trade, setTrade] = useState('')
  const [category, setCategory] = useState('')
  const [q, setQ] = useState('')
  const [chosen, setChosen] = useState<Set<number>>(new Set())
  const [message, setMessage] = useState('')
  const [idea, setIdea] = useState({ name: '', category: '', unit: 'piece' as Unit })
  const activeTrade = trade || trades[0] || ''
  const categories = [...new Set(items.filter((i) => i.trade === activeTrade).map((i) => i.category))]
  const label = (i: MasterItem) => (i18n.language === 'hi' && i.nameHi ? i.nameHi : i.name)
  const shown = items.filter((i) => i.trade === activeTrade && (!category || i.category === category)
    && (!q.trim() || `${i.name} ${i.nameHi ?? ''}`.toLowerCase().includes(q.trim().toLowerCase())))
  const allShown = shown.length > 0 && shown.every((i) => chosen.has(i.id))

  const toggle = (id: number) => setChosen((c) => { const next = new Set(c); if (next.has(id)) next.delete(id); else next.add(id); return next })
  const toggleAll = () => setChosen((c) => { const next = new Set(c); shown.forEach((i) => (allShown ? next.delete(i.id) : next.add(i.id))); return next })

  async function add() {
    setMessage('')
    try {
      const result = await api<{ created: number; skipped: number }>('/v1/products/from-master', 'POST', { ids: [...chosen] })
      setMessage(t('pick.added', result))
      setChosen(new Set())
      void queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  async function applyChanges() {
    setMessage('')
    try {
      const r = await api<{ applied: number }>('/v1/master-items/updates/apply', 'POST', { productIds: changes.map((c) => c.productId) })
      setMessage(t('pick.applied', r))
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  async function suggest() {
    setMessage('')
    try {
      await api('/v1/master-items/suggest', 'POST', { trade: activeTrade || null, name: idea.name.trim(), category: idea.category.trim() || 'Other', unit: idea.unit })
      setIdea({ name: '', category: '', unit: 'piece' })
      setMessage(t('pick.suggested'))
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  const chip = (on: boolean) => `min-h-11 shrink-0 rounded-full px-3 text-sm font-semibold ${on ? 'bg-teal-700 text-white' : 'border border-line bg-white'}`
  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      {changes.length > 0 && (
        <div className="rounded-[18px] bg-amber-50 p-3">
          <h2 className="font-bold">{t('pick.changes', { count: changes.length })}</h2>
          <p className="text-xs text-muted">{t('pick.changesHint')}</p>
          <ul className="my-2 flex flex-col gap-1 text-sm">
            {changes.map((c) => (
              <li key={c.productId}>{c.productName}: {c.gstFrom !== c.gstTo ? `GST ${c.gstFrom ?? '–'}% → ${c.gstTo}%` : ''}{c.gstFrom !== c.gstTo && c.hsnFrom !== c.hsnTo ? ' · ' : ''}{c.hsnFrom !== c.hsnTo ? `HSN ${c.hsnFrom ?? '–'} → ${c.hsnTo}` : ''}</li>
            ))}
          </ul>
          <Button onClick={() => void applyChanges()}>{t('pick.apply', { count: changes.length })}</Button>
        </div>
      )}
      {newIds.size > 0 && <p className="rounded-xl bg-teal-50 p-3 text-sm font-semibold text-teal-900">{t('pick.newCount', { count: newIds.size })}</p>}
      <p className="text-sm text-muted">{t('pick.hint')}</p>
      {trades.length > 1 && (
        <div className="flex gap-2 overflow-x-auto">
          {trades.map((x) => <button key={x} onClick={() => { setTrade(x); setCategory('') }} className={chip(x === activeTrade)}>{t(`shopType.${x}`)}</button>)}
        </div>
      )}
      <input value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('pick.search')} aria-label={t('pick.search')} className="h-12 rounded-xl border border-line bg-white px-3" />
      <div className="flex gap-2 overflow-x-auto">
        <button onClick={() => setCategory('')} className={chip(!category)}>{t('pick.all')}</button>
        {categories.map((c) => <button key={c} onClick={() => setCategory(c)} className={chip(c === category)}>{c}</button>)}
      </div>
      {list.isLoading && <p className="text-muted">…</p>}
      {shown.length > 0 && (
        <label className="flex min-h-11 items-center gap-2 font-semibold">
          <input type="checkbox" checked={allShown} onChange={toggleAll} className="h-5 w-5" />{t('pick.chooseAll', { count: shown.length })}
        </label>
      )}
      <ul className="flex flex-col gap-1 rounded-[18px] bg-white p-2">
        {shown.map((i) => (
          <li key={i.id}>
            <label className="flex min-h-12 items-center gap-3 px-2">
              <input type="checkbox" checked={chosen.has(i.id)} onChange={() => toggle(i.id)} className="h-5 w-5" />
              <span className="min-w-0 flex-1">{label(i)}{newIds.has(i.id) && <span className="ml-2 rounded-full bg-teal-700 px-2 text-xs font-bold text-white">{t('pick.new')}</span>}<span className="block text-xs text-muted">{i.category} · {t(`units.${i.unit}`)}{i.gstRate != null ? ` · GST ${i.gstRate}%` : ''}{i.packSizes.length ? ` · ${i.packSizes.join(' / ')}` : ''}</span></span>
            </label>
          </li>
        ))}
      </ul>
      <details className="rounded-[18px] bg-white p-3">
        <summary className="min-h-11 cursor-pointer font-semibold">{t('pick.suggest')}</summary>
        <p className="text-xs text-muted">{t('pick.suggestHint')}</p>
        <div className="mt-2 grid grid-cols-2 gap-2">
          <input value={idea.name} onChange={(e) => setIdea({ ...idea, name: e.target.value })} placeholder={t('pick.suggestName')} aria-label={t('pick.suggestName')} className="col-span-2 h-12 rounded-xl border border-line px-3" />
          <input value={idea.category} onChange={(e) => setIdea({ ...idea, category: e.target.value })} placeholder={t('pick.suggestCategory')} aria-label={t('pick.suggestCategory')} className="h-12 rounded-xl border border-line px-3" />
          <select value={idea.unit} onChange={(e) => setIdea({ ...idea, unit: e.target.value as Unit })} aria-label={t('pick.suggestUnit')} className="h-12 rounded-xl border border-line px-3">
            {(['piece', 'kg', 'g', 'litre', 'ml', 'packet', 'bag'] as Unit[]).map((u) => <option key={u} value={u}>{t(`units.${u}`)}</option>)}
          </select>
          <Button className="col-span-2" disabled={idea.name.trim().length < 2} onClick={() => void suggest()}>{t('pick.suggestSend')}</Button>
        </div>
      </details>
      {message && <p className="rounded-xl bg-teal-50 p-3 text-sm font-semibold text-teal-900">{message}</p>}
      <div className="fixed inset-x-0 bottom-[68px] z-10 mx-auto max-w-xl border-t border-line bg-white px-4 py-3">
        <Button className="w-full" disabled={chosen.size === 0} onClick={() => void add()}>{t('pick.add', { count: chosen.size })}</Button>
      </div>
    </div>
  )
}

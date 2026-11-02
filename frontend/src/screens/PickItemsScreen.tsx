import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { api } from '../lib/api'
import type { Unit } from '../lib/types'

interface MasterItem { id: number; trade: string; category: string; name: string; nameHi: string | null; unit: Unit; gstRate: number | null }

/** Pick items for the shop from its trades' master lists: search, a category at a time, or all of them. */
export function PickItemsScreen({ shopId }: { shopId: string }) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const list = useQuery({ queryKey: ['master-items'], queryFn: () => api<MasterItem[]>('/v1/master-items') })
  const items = useMemo(() => list.data ?? [], [list.data])
  const trades = [...new Set(items.map((i) => i.trade))]
  const [trade, setTrade] = useState('')
  const [category, setCategory] = useState('')
  const [q, setQ] = useState('')
  const [chosen, setChosen] = useState<Set<number>>(new Set())
  const [message, setMessage] = useState('')
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

  const chip = (on: boolean) => `min-h-11 shrink-0 rounded-full px-3 text-sm font-semibold ${on ? 'bg-teal-700 text-white' : 'border border-line bg-white'}`
  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
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
              <span className="min-w-0 flex-1">{label(i)}<span className="block text-xs text-muted">{i.category} · {t(`units.${i.unit}`)}{i.gstRate != null ? ` · GST ${i.gstRate}%` : ''}</span></span>
            </label>
          </li>
        ))}
      </ul>
      {message && <p className="rounded-xl bg-teal-50 p-3 text-sm font-semibold text-teal-900">{message}</p>}
      <div className="fixed inset-x-0 bottom-[68px] z-10 mx-auto max-w-xl border-t border-line bg-white px-4 py-3">
        <Button className="w-full" disabled={chosen.size === 0} onClick={() => void add()}>{t('pick.add', { count: chosen.size })}</Button>
      </div>
    </div>
  )
}

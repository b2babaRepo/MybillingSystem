import { btn } from '../components/ui'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'

/**
 * Today's rates: cement, sand and steel prices move daily, so the owner changes many at once instead of opening each
 * item. Items are grouped by category; a changed price is highlighted until saved. A new shop of another trade
 * starts here with its usual items (prices to fill in).
 */
export function RatesScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { products } = useProducts(shopId)
  const [draft, setDraft] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const changed = products.filter((p) => draft[p.id] !== undefined && draft[p.id] !== '' && Number(draft[p.id]) !== p.sellingPrice)
  const groups = [...new Set(products.map((p) => p.category ?? ''))].sort()
  // Items whose price changed since their label was printed: print them again (a sheet's worth at a time).
  const queue = useQuery({ queryKey: [shopId, 'label-queue'], queryFn: () => api<string[]>('/v1/products/label-queue'), retry: false })
  // Change many at once by a rule: a category (or all) up or down by a percent, rounded; then check and save.
  const [rule, setRule] = useState({ category: '*', percent: '', round: '1' })
  function fillByRule() {
    const percent = Number(rule.percent)
    if (!percent) return
    const step = Number(rule.round)
    const next = { ...draft }
    for (const p of products) {
      if (p.sellingPrice <= 0 || (rule.category !== '*' && (p.category ?? '') !== rule.category)) continue
      const raw = p.sellingPrice * (1 + percent / 100)
      next[p.id] = String(step > 0 ? Math.max(step, Math.round(raw / step) * step) : Math.round(raw * 100) / 100)
    }
    setDraft(next)
  }

  async function run(action: () => Promise<string>) {
    setBusy(true)
    setMessage(null)
    try {
      setMessage(await action())
      await queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  const save = () => run(async () => {
    await api('/v1/products/rates', 'PUT', { rates: changed.map((p) => ({ productId: p.id, sellingPrice: Number(draft[p.id]) })) })
    setDraft({})
    void queue.refetch()
    return t('rates.saved')
  })

  const starter = () => run(async () => {
    const result = await api<{ created: number }>('/v1/products/starter', 'POST', {})
    return t('rates.starterDone', { count: result.created })
  })

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <p className="text-sm text-muted">{t('rates.hint')}</p>
      {message && <p className="rounded-[14px] bg-brand-soft p-3 text-sm font-semibold text-brand-deep">{message}</p>}
      {(queue.data?.length ?? 0) > 0 && (
        <button onClick={() => navigate(`/tags/${queue.data!.slice(0, 150).join(',')}`)} className={`min-h-12 rounded-xl px-3 ${btn.outline}`}>
          🏷 {t('bulk.reprint', { count: queue.data!.length })}
        </button>
      )}
      {products.length > 0 && (
        <section className="flex flex-wrap items-end gap-2 rounded-card bg-white p-3 text-sm">
          <label className="flex flex-col gap-1"><span className="text-muted">{t('bulk.which')}</span>
            <select value={rule.category} onChange={(e) => setRule({ ...rule, category: e.target.value })} className="h-11 rounded-xl border border-line bg-white px-2">
              <option value="*">{t('bulk.all')}</option>{groups.filter(Boolean).map((g) => <option key={g} value={g}>{g}</option>)}
            </select></label>
          <label className="flex flex-col gap-1"><span className="text-muted">{t('bulk.percent')}</span>
            <input value={rule.percent} onChange={(e) => setRule({ ...rule, percent: e.target.value.replace(/[^\d.-]/g, '') })} inputMode="decimal" placeholder="+5 / -10" className="h-11 w-24 rounded-xl border border-line px-2" /></label>
          <label className="flex flex-col gap-1"><span className="text-muted">{t('bulk.round')}</span>
            <select value={rule.round} onChange={(e) => setRule({ ...rule, round: e.target.value })} className="h-11 rounded-xl border border-line bg-white px-2">
              <option value="1">₹1</option><option value="0.5">₹0.50</option><option value="5">₹5</option><option value="0">{t('bulk.noRound')}</option>
            </select></label>
          <button type="button" disabled={!Number(rule.percent)} onClick={fillByRule} className={`min-h-11 rounded-xl px-4 ${btn.solid}`}>{t('bulk.fill')}</button>
          <p className="w-full text-xs text-muted">{t('bulk.hint')}</p>
        </section>
      )}
      {products.length < 5 && (
        <button disabled={busy} onClick={() => void starter()} className={`min-h-12 rounded-xl ${btn.outline}`}>
          {t('rates.starter')}
        </button>
      )}
      {products.length === 0 && <p className="text-muted">{t('rates.empty')}</p>}

      {groups.map((group) => (
        <section key={group} className="rounded-card bg-white">
          {group && <h2 className="px-4 pt-3 text-sm font-bold text-muted">{group}</h2>}
          <ul className="divide-y divide-[#edf2f1]">
            {products.filter((p) => (p.category ?? '') === group).map((p) => {
              const value = draft[p.id] ?? (p.sellingPrice ? String(p.sellingPrice) : '')
              const isChanged = changed.includes(p)
              return (
                <li key={p.id} className="flex items-center gap-3 px-4 py-2">
                  <label htmlFor={`rate-${p.id}`} className="flex min-w-0 flex-1 flex-col">
                    <span className="truncate font-semibold">{p.name}</span>
                    <span className="text-xs text-muted">
                      {t('perUnit', { defaultValue: 'per {{unit}}', unit: t(`units.${p.unit}`) })}
                      {p.gstRate != null ? ` · ${t('rates.gst', { rate: p.gstRate })}` : ''}
                      {isChanged && p.sellingPrice > 0 ? ` · ${formatRupees(p.sellingPrice)} →` : ''}
                    </span>
                  </label>
                  <input id={`rate-${p.id}`} value={value} inputMode="decimal" placeholder="₹"
                    onChange={(e) => setDraft({ ...draft, [p.id]: e.target.value.replace(/[^\d.]/g, '') })}
                    className={`h-12 w-28 rounded-xl border px-3 text-right text-lg font-bold ${isChanged ? 'border-2 border-udhaar bg-orange-50' : 'border-line'}`} />
                </li>
              )
            })}
          </ul>
        </section>
      ))}

      {changed.length > 0 && (
        <button disabled={busy} onClick={() => void save()}
          className={`fixed inset-x-0 bottom-[calc(68px+env(safe-area-inset-bottom))] z-10 mx-auto h-14 w-[calc(100%-2rem)] max-w-[34rem] shadow-lg rounded-xl ${btn.solid}`}>
          {t('rates.save', { count: changed.length })}
        </button>
      )}
    </div>
  )
}

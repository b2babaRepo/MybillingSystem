import { matchPrices, readPriceList } from '../lib/priceList'
import { btn } from '../lib/buttonStyles'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'
import { shownError, shownDone } from '../lib/notify'

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
  const [word, setWord] = useState('')
  const [group, setGroup] = useState('')
  const [percent, setPercent] = useState('')
  const matching = products.filter((p) => p.sellingPrice > 0 && (!group || (p.category ?? '') === group)
    && (!word.trim() || p.name.toLowerCase().includes(word.trim().toLowerCase())))

  const [unmatched, setUnmatched] = useState<string[]>([])
  // A company's price list (CSV): matched items get the new price as a change to check, like the percent change.
  async function loadList(file: File) {
    const { rows, skipped } = readPriceList(await file.text())
    const { matched, unmatched: missed } = matchPrices(rows, products)
    const next = { ...draft }
    for (const m of matched) next[m.item.id] = String(m.price)
    setDraft(next)
    setUnmatched(missed)
    setMessage(shownDone(t('hwp8.loaded', { matched: matched.length, missed: missed.length, skipped })))
  }

  // Fills the new prices in as changes (whole rupees); nothing is saved until the owner checks and taps save.
  function applyPercent() {
    const by = Number(percent)
    if (!by || matching.length === 0) return
    if (by < -90 || by > 200) { setMessage(t('hwp2.percentRange')); return }
    const next = { ...draft }
    for (const p of matching) next[p.id] = String(Math.max(1, Math.round(p.sellingPrice * (1 + by / 100))))
    setDraft(next)
    setMessage(t('hwp1.rates.percentDone', { count: matching.length }))
  }

  // Items whose price changed since their label was printed: print them again (a sheet's worth at a time).
  const queue = useQuery({ queryKey: [shopId, 'label-queue'], queryFn: () => api<string[]>('/v1/products/label-queue'), retry: false })

  async function run(action: () => Promise<string>) {
    setBusy(true)
    setMessage(null)
    try {
      setMessage(await action())
      await queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
    } catch (e) {
      setMessage(shownError(e))
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
      {products.length < 5 && (
        <button disabled={busy} onClick={() => void starter()} className={`min-h-12 rounded-xl ${btn.outline}`}>
          {t('rates.starter')}
        </button>
      )}
      {products.length === 0 && <p className="text-muted">{t('rates.empty')}</p>}
      {products.length > 0 && (
        <section className="flex flex-col gap-2 rounded-card bg-white p-4">
          <h2 className="font-bold">{t('hwp8.title')}</h2>
          <p className="text-xs text-muted">{t('hwp8.hint')}</p>
          <label className={`flex min-h-11 cursor-pointer items-center justify-center self-start rounded-xl px-4 ${btn.outline}`}>
            📄 {t('hwp8.pick')}
            <input type="file" accept=".csv,.txt,text/csv" className="sr-only" onChange={(e) => { const f = e.target.files?.[0]; if (f) void loadList(f); e.target.value = '' }} />
          </label>
          {unmatched.length > 0 && <p className="text-xs text-due">{t('hwp8.notFound', { names: unmatched.slice(0, 8).join(', ') + (unmatched.length > 8 ? '…' : '') })}</p>}
        </section>
      )}
      {products.length > 0 && (
        <section className="flex flex-col gap-2 rounded-card bg-white p-4">
          <h2 className="font-bold">{t('hwp1.rates.percentTitle')}</h2>
          <p className="text-xs text-muted">{t('hwp1.rates.percentHint')}</p>
          <div className="grid grid-cols-2 gap-2">
            <input value={word} onChange={(e) => setWord(e.target.value)} placeholder={t('hwp1.rates.percentWord')} aria-label={t('hwp1.rates.percentWord')}
              className="h-12 rounded-xl border border-line px-3" />
            <select value={group} onChange={(e) => setGroup(e.target.value)} aria-label={t('hwp1.rates.percentGroup')} className="h-12 rounded-xl border border-line bg-white px-3">
              <option value="">{t('hwp1.rates.percentAll')}</option>
              {groups.filter(Boolean).map((g) => <option key={g} value={g}>{g}</option>)}
            </select>
          </div>
          <div className="flex items-center gap-2">
            <input value={percent} onChange={(e) => setPercent(e.target.value.replace(/[^\d.-]/g, ''))} inputMode="decimal" placeholder="+5 / -3" aria-label={t('hwp1.rates.percentBy')}
              className="h-12 w-28 rounded-xl border border-line px-3 text-right text-lg font-bold" />
            <span>%</span>
            <button disabled={!Number(percent) || matching.length === 0} onClick={applyPercent} className={`h-12 flex-1 rounded-xl ${btn.outline}`}>
              {t('hwp1.rates.percentApply', { count: matching.length })}
            </button>
          </div>
        </section>
      )}

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

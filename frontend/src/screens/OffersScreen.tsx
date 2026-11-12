import { orderedNames, useCategories } from '../lib/categories'
import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { confirmAsk } from '../components/Ask'
import { Button, Field } from '../components/ui'
import { ItemPicker } from '../components/ItemPicker'
import { api } from '../lib/api'
import { useProducts } from '../lib/data'
import { formatRupees, indiaToday as today } from '../lib/format'
import { isRunning, useOffers, type SaleOffer } from '../lib/offers'

const input = 'h-12 min-w-0 rounded-xl border border-line bg-white px-3'
const day = (iso: string) => new Date(iso).toLocaleDateString('en-IN', { day: 'numeric', month: 'short' })

/**
 * Sales the owner plans before billing: a name, a percent or rupees off, the days it runs and what it is on. The bill
 * applies a running offer by itself, so the counter never picks the discount.
 */
export function OffersScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const offers = useOffers(shopId).data ?? []
  const { products } = useProducts(shopId)
  const categories = orderedNames(useCategories(shopId).data, products.map((p) => p.category))
  const blank = { name: '', kind: 'percent' as SaleOffer['kind'], value: '', startsOn: today(), endsOn: today(), scope: 'all' as SaleOffer['scope'], category: '', items: [] as string[], itemText: '' }
  const [draft, setDraft] = useState<typeof blank | null>(null)
  const [message, setMessage] = useState('')
  const now = Date.now()
  const state = (o: SaleOffer) => (isRunning(o, now) ? 'running' : Date.parse(o.startsAt) > now ? 'coming' : 'ended')

  async function run(action: () => Promise<unknown>) {
    setMessage('')
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: [shopId, 'offers'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  const save = () => run(async () => {
    const d = draft!
    await api('/v1/offers', 'POST', {
      id: crypto.randomUUID(), name: d.name.trim(), kind: d.kind, value: Number(d.value), startsOn: d.startsOn, endsOn: d.endsOn, scope: d.scope,
      category: d.scope === 'category' ? d.category : null, productIds: d.scope === 'items' ? d.items : null,
    })
    setDraft(null)
  })
  const stop = async (o: SaleOffer) => { if (await confirmAsk(t('offers.stopConfirm', { name: o.name }))) void run(() => api(`/v1/offers/${o.id}/stop`, 'POST', {})) }
  const what = (o: SaleOffer) => o.scope === 'all' ? t('offers.onAll') : o.scope === 'category' ? t('offers.onCategory', { category: o.category }) : t('offers.onItems', { count: o.productIds.length })

  return (
    <div className="flex flex-col gap-3 p-4 pb-24">
      <p className="text-sm text-muted">{t('offers.hint')}</p>
      {!draft && <Button onClick={() => setDraft(blank)}>+ {t('offers.new')}</Button>}
      {draft && (
        <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
          <Field label={t('offers.name')} value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} maxLength={60} placeholder={t('offers.namePlaceholder')} />
          <div className="grid grid-cols-2 gap-2">
            <input aria-label={t('offers.value')} placeholder={t('offers.value')} value={draft.value} onChange={(e) => setDraft({ ...draft, value: e.target.value })} inputMode="decimal" className={input} />
            <select aria-label={t('offers.kind')} value={draft.kind} onChange={(e) => setDraft({ ...draft, kind: e.target.value as SaleOffer['kind'] })} className={input}>
              <option value="percent">{t('offers.percent')}</option><option value="amount">{t('offers.amount')}</option>
            </select>
            <label className="flex flex-col gap-1 text-sm"><span className="text-muted">{t('offers.from')}</span><input type="date" value={draft.startsOn} onChange={(e) => setDraft({ ...draft, startsOn: e.target.value })} className={input} /></label>
            <label className="flex flex-col gap-1 text-sm"><span className="text-muted">{t('offers.to')}</span><input type="date" value={draft.endsOn} onChange={(e) => setDraft({ ...draft, endsOn: e.target.value })} className={input} /></label>
          </div>
          <select aria-label={t('offers.scope')} value={draft.scope} onChange={(e) => setDraft({ ...draft, scope: e.target.value as SaleOffer['scope'] })} className={input}>
            <option value="all">{t('offers.onAll')}</option><option value="category">{t('offers.scopeCategory')}</option><option value="items">{t('offers.scopeItems')}</option>
          </select>
          {draft.scope === 'category' && (
            <select aria-label={t('offers.scopeCategory')} value={draft.category} onChange={(e) => setDraft({ ...draft, category: e.target.value })} className={input}>
              <option value="">{t('offers.chooseCategory')}</option>{categories.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
          )}
          {draft.scope === 'items' && (
            <>
              <ItemPicker products={products.filter((p) => !draft.items.includes(p.id))} text={draft.itemText} placeholder={t('supplier2.itemSearch')}
                onText={(text) => setDraft({ ...draft, itemText: text })} onPick={(p) => setDraft({ ...draft, items: [...draft.items, p.id], itemText: '' })} />
              <div className="flex flex-wrap gap-1">
                {draft.items.map((id) => (
                  <button key={id} onClick={() => setDraft({ ...draft, items: draft.items.filter((x) => x !== id) })} className="min-h-9 rounded-full bg-teal-50 px-3 text-sm text-teal-900">
                    {products.find((p) => p.id === id)?.name} ×
                  </button>
                ))}
              </div>
            </>
          )}
          <div className="grid grid-cols-2 gap-2">
            <Button disabled={draft.name.trim().length < 2 || !(Number(draft.value) > 0) || (draft.scope === 'category' && !draft.category) || (draft.scope === 'items' && draft.items.length === 0)} onClick={() => void save()}>{t('offers.save')}</Button>
            <button onClick={() => setDraft(null)} className="min-h-12 rounded-2xl border border-line font-semibold">{t('cancel')}</button>
          </div>
        </section>
      )}
      {offers.length === 0 && !draft && <p className="rounded-[18px] bg-white p-4 text-sm text-muted">{t('offers.none')}</p>}
      {offers.map((o) => (
        <section key={o.id} className="flex flex-col gap-1 rounded-[18px] bg-white p-4 text-sm">
          <p className="flex items-center justify-between gap-2">
            <b className="text-base">{o.name} · {o.kind === 'percent' ? `${o.value}%` : formatRupees(o.value)} {t('offers.off')}</b>
            <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${state(o) === 'running' ? 'bg-teal-50 text-teal-800' : 'bg-[#f1f5f4] text-muted'}`}>{t(`offers.state.${state(o)}`)}</span>
          </p>
          <p className="text-muted">{what(o)} · {day(o.startsAt)} – {day(new Date(Date.parse(o.endsAt) - 1).toISOString())}{o.stoppedAt ? ` · ${t('offers.stoppedOn', { date: day(o.stoppedAt) })}` : ''}</p>
          {o.stats && <p>{t('offers.stats', { bills: o.stats.bills, sales: formatRupees(o.stats.sales), saved: formatRupees(o.stats.saved) })}</p>}
          {state(o) !== 'ended' && <button onClick={() => void stop(o)} className="min-h-11 self-start text-sm font-semibold text-due">{t('offers.stop')}</button>}
        </section>
      ))}
      {message && <p className="text-sm text-due">{message}</p>}
    </div>
  )
}

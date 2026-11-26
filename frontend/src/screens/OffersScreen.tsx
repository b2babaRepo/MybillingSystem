import { shownLocale } from '../i18n'
import { orderedNames, useCategories } from '../lib/categories'
import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { confirmAsk } from '../components/Ask'
import { Button, Field, control, btn } from '../components/ui'
import { ItemPicker } from '../components/ItemPicker'
import { api } from '../lib/api'
import { useProducts } from '../lib/data'
import { formatRupees, indiaToday as today } from '../lib/format'
import { isRunning, useOffers, type SaleOffer } from '../lib/offers'

const input = control
const day = (iso: string) => new Date(iso).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })

/**
 * Sales the owner plans before billing: a name, a percent or rupees off, the days it runs and what it is on. The bill
 * applies a running offer by itself, so the counter never picks the discount.
 */
const KINDS: SaleOffer['kind'][] = ['percent', 'amount', 'buy_get', 'slab', 'combo', 'bill_percent', 'bill_amount']
const billWide = (k: SaleOffer['kind']) => k === 'bill_percent' || k === 'bill_amount'
const needsValue = (k: SaleOffer['kind']) => k !== 'buy_get' && k !== 'combo'

export function OffersScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const offers = useOffers(shopId).data ?? []
  const { products } = useProducts(shopId)
  const categories = orderedNames(useCategories(shopId).data, products.map((p) => p.category))
  const blank = {
    name: '', kind: 'percent' as SaleOffer['kind'], value: '', startsOn: today(), endsOn: today(), scope: 'all' as SaleOffer['scope'], category: '', items: [] as string[], itemText: '',
    buyQty: '2', getQty: '1', minQty: '', minBill: '', comboPrice: '', days: 127, from: '', to: '',
  }
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
    const minute = (hhmm: string) => (hhmm ? Number(hhmm.slice(0, 2)) * 60 + Number(hhmm.slice(3, 5)) : null)
    const combo = d.kind === 'combo'
    const scope = combo ? 'items' : billWide(d.kind) ? 'all' : d.scope
    await api('/v1/offers', 'POST', {
      id: crypto.randomUUID(), name: d.name.trim(), kind: d.kind, value: needsValue(d.kind) ? Number(d.value) : 0, startsOn: d.startsOn, endsOn: d.endsOn, scope,
      category: scope === 'category' ? d.category : null, productIds: scope === 'items' ? d.items : null,
      buyQty: d.kind === 'buy_get' ? Number(d.buyQty) : null, getQty: d.kind === 'buy_get' ? Number(d.getQty) : null,
      minQty: d.kind === 'slab' ? Number(d.minQty) : null, minBill: billWide(d.kind) ? Number(d.minBill) : null, comboPrice: combo ? Number(d.comboPrice) : null,
      days: d.days, fromMinute: d.from && d.to ? minute(d.from) : null, toMinute: d.from && d.to ? minute(d.to) : null,
    })
    setDraft(null)
  })
  const ready = (d: typeof blank) => (!needsValue(d.kind) || Number(d.value) > 0)
    && (d.kind !== 'buy_get' || (Number(d.buyQty) >= 1 && Number(d.getQty) >= 1))
    && (d.kind !== 'slab' || Number(d.minQty) >= 2)
    && (d.kind !== 'combo' || (Number(d.comboPrice) > 0 && d.items.length >= 2))
    && (!billWide(d.kind) || Number(d.minBill) > 0)
    && (billWide(d.kind) || d.kind === 'combo' || ((d.scope !== 'category' || !!d.category) && (d.scope !== 'items' || d.items.length > 0)))
    && (!d.from === !d.to)
  const hours = (o: SaleOffer) => (o.fromMinute != null && o.toMinute != null
    ? ` · ${[o.fromMinute, o.toMinute].map((m) => `${String(Math.floor(m / 60)).padStart(2, '0')}:${String(m % 60).padStart(2, '0')}`).join('–')}` : '')
  const describe = (o: SaleOffer) => ({
    percent: t('offers.d.percent', { value: o.value }), amount: t('offers.d.amount', { amount: formatRupees(o.value) }),
    buy_get: t('offers.d.buy_get', { buy: o.buyQty, get: o.getQty }), slab: t('offers.d.slab', { value: o.value, qty: o.minQty }),
    combo: t('offers.d.combo', { amount: formatRupees(o.comboPrice ?? 0) }),
    bill_percent: t('offers.d.bill_percent', { value: o.value, bill: formatRupees(o.minBill ?? 0) }), bill_amount: t('offers.d.bill_amount', { amount: formatRupees(o.value), bill: formatRupees(o.minBill ?? 0) }),
  })[o.kind] + hours(o)
  const stop = async (o: SaleOffer) => { if (await confirmAsk(t('offers.stopConfirm', { name: o.name }))) void run(() => api(`/v1/offers/${o.id}/stop`, 'POST', {})) }
  const what = (o: SaleOffer) => o.scope === 'all' ? t('offers.onAll') : o.scope === 'category' ? t('offers.onCategory', { category: o.category }) : t('offers.onItems', { count: o.productIds.length })

  return (
    <div className="flex flex-col gap-3 p-4 pb-24">
      <p className="text-sm text-muted">{t('offers.hint')}</p>
      {!draft && <Button onClick={() => setDraft(blank)}>+ {t('offers.new')}</Button>}
      {draft && (
        <section className="flex flex-col gap-2 rounded-card bg-white p-4">
          <Field label={t('offers.name')} value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} maxLength={60} placeholder={t('offers.namePlaceholder')} />
          <select aria-label={t('offers.kind')} value={draft.kind} onChange={(e) => setDraft({ ...draft, kind: e.target.value as SaleOffer['kind'] })} className={input}>
            {KINDS.map((k) => <option key={k} value={k}>{t(`promo.kinds.${k}`)}</option>)}
          </select>
          <div className="grid grid-cols-2 gap-2">
            {needsValue(draft.kind) && (
              <input aria-label={t('offers.value')} placeholder={draft.kind === 'amount' || draft.kind === 'bill_amount' ? t('offers.rupeesOff') : t('offers.percentOff')} value={draft.value}
                onChange={(e) => setDraft({ ...draft, value: e.target.value })} inputMode="decimal" className={input} />
            )}
            {draft.kind === 'buy_get' && <>
              <label className="flex flex-col gap-1 text-sm"><span className="text-muted">{t('offers.buy')}</span><input value={draft.buyQty} onChange={(e) => setDraft({ ...draft, buyQty: e.target.value })} inputMode="numeric" className={input} /></label>
              <label className="flex flex-col gap-1 text-sm"><span className="text-muted">{t('offers.getFree')}</span><input value={draft.getQty} onChange={(e) => setDraft({ ...draft, getQty: e.target.value })} inputMode="numeric" className={input} /></label>
            </>}
            {draft.kind === 'slab' && <input aria-label={t('offers.fromQty')} placeholder={t('offers.fromQty')} value={draft.minQty} onChange={(e) => setDraft({ ...draft, minQty: e.target.value })} inputMode="decimal" className={input} />}
            {draft.kind === 'combo' && <input aria-label={t('offers.comboPrice')} placeholder={t('offers.comboPrice')} value={draft.comboPrice} onChange={(e) => setDraft({ ...draft, comboPrice: e.target.value })} inputMode="decimal" className={input} />}
            {billWide(draft.kind) && <input aria-label={t('offers.fromBill')} placeholder={t('offers.fromBill')} value={draft.minBill} onChange={(e) => setDraft({ ...draft, minBill: e.target.value })} inputMode="decimal" className={input} />}
          </div>
          <div className="grid grid-cols-2 gap-2">
            <label className="flex flex-col gap-1 text-sm"><span className="text-muted">{t('offers.from')}</span><input type="date" value={draft.startsOn} onChange={(e) => setDraft({ ...draft, startsOn: e.target.value })} className={input} /></label>
            <label className="flex flex-col gap-1 text-sm"><span className="text-muted">{t('offers.to')}</span><input type="date" value={draft.endsOn} onChange={(e) => setDraft({ ...draft, endsOn: e.target.value })} className={input} /></label>
          </div>
          {/* Weekdays and hours: a weekend offer, happy hours. */}
          <div className="flex flex-wrap items-center gap-1" role="group" aria-label={t('offers.days')}>
            {[0, 1, 2, 3, 4, 5, 6].map((d) => (
              <button key={d} type="button" aria-pressed={((draft.days >> d) & 1) === 1} onClick={() => setDraft({ ...draft, days: draft.days ^ (1 << d) || draft.days })}
                className={`min-h-9 min-w-11 rounded-full border px-2 text-sm ${((draft.days >> d) & 1) === 1 ? 'border-brand bg-brand-soft font-semibold text-brand-deep' : 'border-line text-muted'}`}>
                {new Date(Date.UTC(2026, 10, 22 + d)).toLocaleDateString(shownLocale(), { weekday: 'short', timeZone: 'UTC' })}
              </button>
            ))}
          </div>
          <div className="grid grid-cols-2 gap-2">
            <label className="flex flex-col gap-1 text-sm"><span className="text-muted">{t('offers.hoursFrom')}</span><input type="time" value={draft.from} onChange={(e) => setDraft({ ...draft, from: e.target.value })} className={input} /></label>
            <label className="flex flex-col gap-1 text-sm"><span className="text-muted">{t('offers.hoursTo')}</span><input type="time" value={draft.to} onChange={(e) => setDraft({ ...draft, to: e.target.value })} className={input} /></label>
          </div>
          {!billWide(draft.kind) && draft.kind !== 'combo' && (
            <select aria-label={t('offers.scope')} value={draft.scope} onChange={(e) => setDraft({ ...draft, scope: e.target.value as SaleOffer['scope'] })} className={input}>
              <option value="all">{t('offers.onAll')}</option><option value="category">{t('offers.scopeCategory')}</option><option value="items">{t('offers.scopeItems')}</option>
            </select>
          )}
          {draft.kind === 'combo' && <p className="text-sm text-muted">{t('offers.comboItems')}</p>}
          {draft.scope === 'category' && !billWide(draft.kind) && draft.kind !== 'combo' && (
            <select aria-label={t('offers.scopeCategory')} value={draft.category} onChange={(e) => setDraft({ ...draft, category: e.target.value })} className={input}>
              <option value="">{t('offers.chooseCategory')}</option>{categories.map((c) => <option key={c} value={c}>{c}</option>)}
            </select>
          )}
          {((draft.scope === 'items' && !billWide(draft.kind)) || draft.kind === 'combo') && (
            <>
              <ItemPicker products={products.filter((p) => !draft.items.includes(p.id))} text={draft.itemText} placeholder={t('supplier2.itemSearch')}
                onText={(text) => setDraft({ ...draft, itemText: text })} onPick={(p) => setDraft({ ...draft, items: [...draft.items, p.id], itemText: '' })} />
              <div className="flex flex-wrap gap-1">
                {draft.items.map((id) => (
                  <button key={id} onClick={() => setDraft({ ...draft, items: draft.items.filter((x) => x !== id) })} className="min-h-9 rounded-full bg-brand-soft px-3 text-sm text-brand-deep">
                    {products.find((p) => p.id === id)?.name} ×
                  </button>
                ))}
              </div>
            </>
          )}
          <div className="grid grid-cols-2 gap-2">
            <Button disabled={draft.name.trim().length < 2 || !ready(draft)} onClick={() => void save()}>{t('offers.save')}</Button>
            <button onClick={() => setDraft(null)} className="min-h-12 rounded-2xl border border-line font-semibold">{t('cancel')}</button>
          </div>
        </section>
      )}
      {offers.length === 0 && !draft && <p className="rounded-card bg-white p-4 text-sm text-muted">{t('offers.none')}</p>}
      {offers.map((o) => (
        <section key={o.id} className="flex flex-col gap-1 rounded-card bg-white p-4 text-sm">
          <p className="flex items-center justify-between gap-2">
            <b className="text-base">{o.name} · {describe(o)}</b>
            <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${state(o) === 'running' ? 'bg-brand-soft text-brand-strong' : 'bg-[#f1f5f4] text-muted'}`}>{t(`offers.state.${state(o)}`)}</span>
          </p>
          <p className="text-muted">{billWide(o.kind) ? t('offers.onBill') : what(o)} · {day(o.startsAt)} – {day(new Date(Date.parse(o.endsAt) - 1).toISOString())}{o.stoppedAt ? ` · ${t('offers.stoppedOn', { date: day(o.stoppedAt) })}` : ''}</p>
          {o.stats && <p>{t('offers.stats', { bills: o.stats.bills, sales: formatRupees(o.stats.sales), saved: formatRupees(o.stats.saved) })}</p>}
          {state(o) !== 'ended' && <button onClick={() => void stop(o)} className={`min-h-11 self-start text-sm ${btn.dangerOutline}`}>{t('offers.stop')}</button>}
        </section>
      ))}
      {message && <p className="text-sm text-due">{message}</p>}
    </div>
  )
}

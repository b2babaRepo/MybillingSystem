import { btn } from '../components/ui'
import { shownLocale } from '../i18n'
import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ItemPicker } from '../components/ItemPicker'
import { api } from '../lib/api'
import { useProducts } from '../lib/data'
import { navigate } from '../router'

interface Hold { source: 'online' | 'sale_order' | 'counter'; sourceId: string; reference: string; productId: string; product: string; unit: string; quantity: number; party: string; until: string | null; overdue: boolean; shortBy: number; since: string; note: string | null }

const short = (iso: string) => new Date(iso.length === 10 ? `${iso}T00:00` : iso).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })

/**
 * Promised stock: every hold on the shelf in one list, whatever made it (an online order, a customer's sale order, or
 * stock kept aside at the counter), who it's for and until when, and which ones the shelf can't cover (oldest first).
 */
export function HoldsScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const holds = useQuery({ queryKey: [shopId, 'holds'], queryFn: () => api<Hold[]>('/v1/holds') })
  const { products } = useProducts(shopId)
  const [adding, setAdding] = useState(false)
  const [draft, setDraft] = useState({ text: '', productId: '', qty: '', party: '', until: '', note: '' })
  const [message, setMessage] = useState('')
  const data = holds.data ?? []

  async function run(action: () => Promise<unknown>) {
    setMessage('')
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: [shopId, 'holds'] })
      void queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
      return true
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
      return false
    }
  }

  async function add() {
    const ok = await run(() => api('/v1/holds', 'POST', { id: crypto.randomUUID(), productId: draft.productId, quantity: Number(draft.qty), partyName: draft.party.trim(), until: draft.until || null, note: draft.note.trim() || null }))
    if (ok) { setAdding(false); setDraft({ text: '', productId: '', qty: '', party: '', until: '', note: '' }) }
  }

  const open = (h: Hold) => { if (h.source === 'sale_order') navigate('/sale-orders'); else if (h.source === 'online') navigate(`/o/${h.sourceId}`); else navigate(`/p/${h.productId}`) }
  const shortCount = data.filter((h) => h.shortBy > 0).length

  return (
    <div className="flex flex-col gap-3 p-4">
      <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
        <div className="flex items-center justify-between gap-2">
          <h1 className="text-xl font-bold">🔖 {t('holds.title')}</h1>
          {!adding && <button onClick={() => setAdding(true)} className={`h-10 px-3 text-sm rounded-xl ${btn.solid}`}>+ {t('holds.add')}</button>}
        </div>
        <p className="text-sm text-muted">{t('holds.hint')}</p>
        {data.length > 0 && <p className="text-sm font-semibold">{t('holds.summary', { count: data.length })}{shortCount > 0 && <span className="text-due"> · {t('holds.shortCount', { count: shortCount })}</span>}</p>}
        {adding && (
          <div className="flex flex-col gap-2 rounded-xl bg-[#f6faf9] p-3">
            <ItemPicker products={products.filter((p) => !p.isService)} text={draft.text} placeholder={t('supplier2.itemSearch')}
              onText={(text) => setDraft({ ...draft, text, productId: products.find((p) => p.name === text)?.id ?? '' })} onPick={(p) => setDraft({ ...draft, text: p.name, productId: p.id })} />
            <div className="grid grid-cols-2 gap-2">
              <input aria-label={t('holds.qty')} placeholder={t('holds.qty')} value={draft.qty} inputMode="decimal" onChange={(e) => setDraft({ ...draft, qty: e.target.value })} className="h-11 rounded-xl border border-line bg-white px-3" />
              <input aria-label={t('holds.for')} placeholder={t('holds.for')} value={draft.party} maxLength={80} onChange={(e) => setDraft({ ...draft, party: e.target.value })} className="h-11 rounded-xl border border-line bg-white px-3" />
              <label className="flex flex-col gap-1 text-xs text-muted">{t('holds.until')}<input type="date" value={draft.until} onChange={(e) => setDraft({ ...draft, until: e.target.value })} className="h-11 rounded-xl border border-line bg-white px-3" /></label>
              <label className="flex flex-col gap-1 text-xs text-muted">{t('holds.note')}<input value={draft.note} maxLength={200} onChange={(e) => setDraft({ ...draft, note: e.target.value })} className="h-11 rounded-xl border border-line bg-white px-3" /></label>
            </div>
            <div className="grid grid-cols-2 gap-2">
              <button disabled={!draft.productId || !(Number(draft.qty) > 0) || !draft.party.trim()} onClick={() => void add()} className={`h-11 rounded-xl ${btn.solid}`}>{t('holds.save')}</button>
              <button onClick={() => setAdding(false)} className="h-11 rounded-xl border border-line font-semibold">{t('cancel')}</button>
            </div>
          </div>
        )}
      </section>
      {message && <p className="text-sm text-due">{message}</p>}
      {holds.data && data.length === 0 && <p className="py-6 text-center text-muted">{t('holds.none')}</p>}
      {data.map((h) => (
        <div key={`${h.source}-${h.sourceId}-${h.productId}`} className={`flex flex-col gap-1 rounded-2xl bg-white p-3 shadow-sm ${h.shortBy > 0 ? 'ring-1 ring-red-200' : ''}`}>
          <div className="flex items-start justify-between gap-2">
            <button onClick={() => open(h)} className="flex min-w-0 flex-col text-left">
              <b className="truncate">{h.product} · {h.quantity} {t(`units.${h.unit}`, { count: h.quantity })}</b>
              <span className="text-xs text-muted">{t(`holds.source.${h.source}`, { number: h.reference })} · {h.party} · {t('holds.since', { date: short(h.since) })}</span>
            </button>
            {h.until && <span className={`shrink-0 rounded-full px-2 py-0.5 text-[11px] font-semibold ${h.overdue ? 'bg-due-soft text-due' : 'bg-brand-soft text-brand-strong'}`}>{t(h.overdue ? 'holds.overdue' : 'holds.till', { date: short(h.until) })}</span>}
          </div>
          {h.shortBy > 0 && <p className="text-xs font-semibold text-due">{t('holds.short', { qty: h.shortBy })}</p>}
          {h.note && <p className="text-xs text-muted">{h.note}</p>}
          {h.source === 'counter' && (
            <div className="flex gap-2">
              <button onClick={() => void run(() => api(`/v1/holds/${h.sourceId}/done`, 'POST', {}))} className="h-10 flex-1 rounded-xl border border-line text-sm font-semibold hover:bg-[#f6faf9]">✓ {t('holds.done')}</button>
              <button onClick={() => void run(() => api(`/v1/holds/${h.sourceId}/release`, 'POST', {}))} className={`h-10 flex-1 text-sm hover:bg-due-soft ${btn.dangerOutline}`}>{t('holds.release')}</button>
            </div>
          )}
        </div>
      ))}
    </div>
  )
}

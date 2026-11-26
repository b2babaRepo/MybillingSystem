import { shownLocale } from '../i18n'
import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'
import { pct } from '../lib/supplierStats'

interface ItemSupplier { supplierId: string; name: string; phone: string | null; ordered: number; received: number; pending: number; damaged: number; lastRate: number | null; lastAt: string | null; deliveries: number; leadDays: number | null; fillRate: number | null; preferred: boolean; bestRate: boolean; caseQty?: number | null; minQty?: number | null }
interface SupplierProduct { productId: string; name: string; category: string | null; unit: string; received: number; pending: number; lastRate: number | null; lastAt: string | null; deliveries: number; preferred: boolean }

const short = (iso: string) => new Date(iso).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: '2-digit' })
/**
 * "Suppliers of this item", on the item page (owner only: it shows rates). Who it came from, how much, at what rate,
 * how fast; the best recent rate is marked, and the owner picks the supplier to order it from.
 */
export function ItemSuppliersCard({ shopId, productId, unit }: { shopId: string; productId: string; unit: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const key = [shopId, 'item-suppliers', productId]
  const rows = useQuery({ queryKey: key, queryFn: () => api<ItemSupplier[]>(`/v1/products/${productId}/suppliers`), retry: false })
  if (rows.isError || !rows.data || rows.data.length === 0) return null

  async function prefer(s: ItemSupplier) {
    await api(`/v1/products/${productId}/suppliers/${s.supplierId}/preferred`, 'PUT', { preferred: !s.preferred })
    await queryClient.invalidateQueries({ queryKey: key })
    void queryClient.invalidateQueries({ queryKey: [shopId, 'reorder'] })
  }

  return (
    <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
      <h2 className="font-bold">🚚 {t('rs.itemSuppliers')}</h2>
      {rows.data.map((s) => (
        <div key={s.supplierId} className={`flex flex-col gap-1 rounded-xl border p-3 text-sm ${s.preferred ? 'border-brand-line bg-brand-soft/40' : 'border-line'}`}>
          <div className="flex items-start justify-between gap-2">
            <b>{s.name}{s.bestRate && <span className="ml-1 rounded-full bg-emerald-50 px-2 text-[11px] font-semibold text-emerald-800">{t('rs.bestRate')}</span>}</b>
            <button onClick={() => void prefer(s)} aria-pressed={s.preferred} className={`shrink-0 rounded-full px-2.5 py-1 text-xs font-semibold ${s.preferred ? 'bg-brand text-white' : 'border border-line text-muted'}`}>
              {s.preferred ? `★ ${t('rs.preferred')}` : `☆ ${t('rs.makePreferred')}`}
            </button>
          </div>
          <span className="text-xs text-muted">
            {t('rs.came', { qty: s.received, unit: t(`units.${unit}`, { count: s.received }), count: s.deliveries })}{s.ordered > 0 && ` · ${t('rs.ordered', { qty: s.ordered })}`}
            {s.pending > 0 && <b className="text-warn"> · {t('supplier2.stillToCome', { qty: s.pending })}</b>}
          </span>
          <span className="text-xs text-muted">
            {s.lastRate != null ? `${t('rs.lastRate')} ${formatRupees(s.lastRate)}` : t('rs.noRate')}{s.lastAt && ` · ${short(s.lastAt)}`}
            {s.leadDays != null && ` · ${t('orderToday.lead', { count: s.leadDays })}`}{s.fillRate != null && ` · ${t('rs.fill')} ${pct(s.fillRate)}`}
            {s.damaged > 0 && ` · ${t('rs.damaged', { qty: s.damaged })}`}
          </span>
          <PackEditor productId={productId} supplier={s} onSaved={() => { void queryClient.invalidateQueries({ queryKey: key }); void queryClient.invalidateQueries({ queryKey: [shopId, 'reorder'] }) }} />
        </div>
      ))}
    </section>
  )
}

/** How the item comes from this supplier: units per case and the least per order. Orders round up to both. */
function PackEditor({ productId, supplier, onSaved }: { productId: string; supplier: ItemSupplier; onSaved: () => void }) {
  const { t } = useTranslation()
  const [caseQty, setCaseQty] = useState(supplier.caseQty != null ? String(supplier.caseQty) : '')
  const [minQty, setMinQty] = useState(supplier.minQty != null ? String(supplier.minQty) : '')
  const [note, setNote] = useState<string | null>(null)
  const changed = caseQty !== (supplier.caseQty != null ? String(supplier.caseQty) : '') || minQty !== (supplier.minQty != null ? String(supplier.minQty) : '')

  async function save() {
    setNote(null)
    try {
      await api(`/v1/products/${productId}/suppliers/${supplier.supplierId}/pack`, 'PUT', { caseQty: caseQty.trim() ? Number(caseQty) : null, minQty: minQty.trim() ? Number(minQty) : null })
      setNote(t('rs.packSaved'))
      onSaved()
    } catch (e) {
      setNote(e instanceof Error ? e.message : t('error'))
    }
  }

  return (
    <div className="mt-1 flex flex-wrap items-end gap-2 text-xs" title={t('rs.packHint')}>
      <label className="flex flex-col gap-0.5 text-muted">{t('rs.caseQty')}
        <input value={caseQty} onChange={(e) => setCaseQty(e.target.value)} inputMode="decimal" className="h-9 w-20 rounded-lg border border-line px-2 text-ink" />
      </label>
      <label className="flex flex-col gap-0.5 text-muted">{t('rs.minQty')}
        <input value={minQty} onChange={(e) => setMinQty(e.target.value)} inputMode="decimal" className="h-9 w-20 rounded-lg border border-line px-2 text-ink" />
      </label>
      {changed && <button onClick={() => void save()} className="h-9 rounded-lg bg-brand px-3 font-semibold text-white">{t('rs.packSave')}</button>}
      {note && <span className="text-muted">{note}</span>}
    </div>
  )
}

/** "Items from this supplier", on the supplier page: what came from them, what is still to come, last rate. */
export function SupplierItemsCard({ shopId, supplierId }: { shopId: string; supplierId: string }) {
  const { t } = useTranslation()
  const rows = useQuery({ queryKey: [shopId, 'suppliers', 'items', supplierId], queryFn: () => api<SupplierProduct[]>(`/v1/suppliers/${supplierId}/items`), retry: false }).data
  if (!rows || rows.length === 0) return null
  return (
    <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
      <h2 className="font-bold">📦 {t('rs.supplierItems')}</h2>
      <ul className="flex flex-col">
        {rows.map((p) => (
          <li key={p.productId}>
            <button onClick={() => navigate(`/p/${p.productId}`)} className="flex w-full items-center justify-between gap-2 border-t border-[#edf2f1] py-2 text-left text-sm first:border-t-0 hover:bg-[#f6faf9]">
              <span className="flex min-w-0 flex-col">
                <b className="truncate">{p.preferred && '★ '}{p.name}</b>
                <span className="text-xs text-muted">{t('rs.came', { qty: p.received, unit: t(`units.${p.unit}`, { count: p.received }), count: p.deliveries })}{p.lastAt && ` · ${short(p.lastAt)}`}{p.pending > 0 && ` · ${t('supplier2.stillToCome', { qty: p.pending })}`}</span>
              </span>
              <span className="shrink-0 font-semibold">{p.lastRate != null ? formatRupees(p.lastRate) : '—'}</span>
            </button>
          </li>
        ))}
      </ul>
    </section>
  )
}

import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api, request } from '../lib/api'
import { formatRupees, whatsAppLink } from '../lib/format'
import { shrinkPhoto } from '../lib/images'
import { pct } from '../components/SupplierPanels'

interface Refill { customerId: string; customerName: string; phone: string | null; productId: string; medicine: string; lastQuantity: number; lastBought: string; everyDays: number; dueOn: string; daysLate: number }
interface ReorderOption { supplierId: string; name: string; phone: string | null; lastRate: number | null; leadDays: number | null; fillRate: number | null; preferred: boolean }
interface ReorderGroup { supplierId: string | null; supplierName: string | null; phone: string | null; lines: { productId: string; medicine: string; stock: number; soldLast30Days: number; suggested: number; lastRate: number | null; daysLeft?: number | null; leadDays?: number; options?: ReorderOption[] }[] }
interface Part { supplierId: string | null; qty: string }

const day = (iso: string) => new Date(`${iso}T00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })

/** Regular patients due a refill, worked out from how often they buy each medicine. A reminder goes only when the shop taps it. */
export function RefillsScreen({ shopId, shopName }: { shopId: string; shopName: string }) {
  const { t } = useTranslation()
  const refills = useQuery({ queryKey: [shopId, 'refills'], queryFn: () => api<Refill[]>('/v1/pharmacy/refills') }).data
  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <p className="text-sm text-muted">{t('refills.hint')}</p>
      {refills?.length === 0 && <p className="rounded-card bg-white p-3 text-sm text-muted">{t('refills.none')}</p>}
      {refills?.map((r) => (
        <div key={`${r.customerId}-${r.productId}`} className={`flex flex-col gap-1 rounded-card p-3 ${r.daysLate > 0 ? 'bg-warn-soft' : 'bg-white'}`}>
          <p className="font-bold">{r.customerName} · {r.medicine}</p>
          <p className="text-sm">{t('refills.line', { every: r.everyDays, last: day(r.lastBought), qty: r.lastQuantity })} · {r.daysLate > 0 ? t('refills.late', { count: r.daysLate }) : t('refills.dueOn', { date: day(r.dueOn) })}</p>
          {r.phone
            ? <a href={whatsAppLink(r.phone, t('refills.message', { name: r.customerName, medicine: r.medicine, shop: shopName }))} target="_blank" rel="noreferrer"
                className="flex h-11 items-center justify-center rounded-xl bg-[#15803d] font-semibold text-white">{t('tables.remind')}</a>
            : <p className="text-xs text-muted">{t('refills.noPhone')}</p>}
        </div>
      ))}
    </div>
  )
}

/**
 * What to order, grouped by supplier (the preferred one, else the best recent rate). The owner edits quantities, can
 * move an item to another supplier or split it across two (seeing each one's rate, delivery days and fill rate), then
 * saves each supplier's order or sends it on WhatsApp.
 */
export function ReorderScreen({ shopId, shopName, general = false }: { shopId: string; shopName: string; general?: boolean }) {
  // general: any trade's shop (items and suppliers); otherwise a medical shop's wording (medicines and distributors).
  const { t } = useTranslation()
  const groups = useQuery({ queryKey: [shopId, 'reorder'], queryFn: () => api<ReorderGroup[]>('/v1/pharmacy/reorder') }).data
  // Per item, who gets how much. Unset: all of the suggestion to the group's supplier.
  const [alloc, setAlloc] = useState<Record<string, Part[]>>({})
  const [splitting, setSplitting] = useState<string | null>(null)
  const queryClient = useQueryClient()
  const [saved, setSaved] = useState<Record<string, string>>({})

  const lines = (groups ?? []).flatMap((g) => g.lines.map((l) => ({ ...l, home: g.supplierId })))
  const people = new Map<string, { name: string; phone: string | null }>()
  for (const g of groups ?? []) if (g.supplierId) people.set(g.supplierId, { name: g.supplierName ?? '', phone: g.phone })
  for (const l of lines) for (const o of l.options ?? []) people.set(o.supplierId, { name: o.name, phone: o.phone })
  const partsOf = (l: (typeof lines)[number]): Part[] => alloc[l.productId] ?? [{ supplierId: l.home, qty: String(l.suggested) }]
  const setParts = (productId: string, parts: Part[]) => setAlloc({ ...alloc, [productId]: parts })

  // Regroup by who each part now goes to.
  const bySupplier = new Map<string, { supplierId: string | null; rows: { line: (typeof lines)[number]; part: number; qty: string }[] }>()
  for (const l of lines) partsOf(l).forEach((p, i) => {
    const key = p.supplierId ?? 'none'
    if (!bySupplier.has(key)) bySupplier.set(key, { supplierId: p.supplierId, rows: [] })
    bySupplier.get(key)!.rows.push({ line: l, part: i, qty: p.qty })
  })
  const shown = [...bySupplier.values()].sort((a, b) => (a.supplierId == null ? 1 : b.supplierId == null ? -1 : (people.get(a.supplierId)?.name ?? '').localeCompare(people.get(b.supplierId)?.name ?? '')))

  // Saved, an order counts as on its way: it leaves this list, and its goods match it when they come.
  async function saveOrder(supplierId: string, rows: { productId: string; order: number; rate: number | null }[]) {
    try {
      const order = await api<{ number: number }>('/v1/suppliers/orders', 'POST', { id: crypto.randomUUID(), supplierId, lines: rows.map((l) => ({ productId: l.productId, quantity: l.order, rate: l.rate })) })
      setSaved({ ...saved, [supplierId]: t('supplier2.savedOrder', { number: order.number }) })
      setAlloc(Object.fromEntries(Object.entries(alloc).filter(([id]) => !rows.some((r) => r.productId === id))))
      await queryClient.invalidateQueries({ queryKey: [shopId, 'reorder'] })
    } catch (e) {
      setSaved({ ...saved, [supplierId]: e instanceof Error ? e.message : t('error') })
    }
  }

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <p className="text-sm text-muted">{t(general ? 'reorder.hintAll' : 'reorder.hint')}</p>
      {Object.values(saved).map((m) => <p key={m} className="rounded-card bg-brand-soft p-3 text-sm text-brand-deep">{m}</p>)}
      {groups?.length === 0 && <p className="rounded-card bg-white p-3 text-sm text-muted">{t('reorder.none')}</p>}
      {shown.map((g) => {
        const who = g.supplierId ? people.get(g.supplierId) : undefined
        const rateOf = (l: (typeof lines)[number]) => (l.options ?? []).find((o) => o.supplierId === g.supplierId)?.lastRate ?? (l.home === g.supplierId ? l.lastRate : null)
        const toOrder = g.rows.map((r) => ({ productId: r.line.productId, medicine: r.line.medicine, order: Number(r.qty) || 0, rate: rateOf(r.line) })).filter((l) => l.order > 0)
        const message = t('reorder.message', { shop: shopName, lines: toOrder.map((l) => `${l.medicine} × ${l.order}`).join('\n') })
        return (
          <section key={g.supplierId ?? 'none'} className="flex flex-col gap-2 rounded-card bg-white p-3">
            <h3 className="font-bold">{who?.name ?? t(general ? 'reorder.noSupplierAll' : 'reorder.noSupplier')}</h3>
            {g.rows.map(({ line: l, part, qty }) => {
              const parts = partsOf(l)
              const rate = rateOf(l)
              return (
                <div key={`${l.productId}-${part}`} className="flex flex-col gap-1 border-t border-[#edf2f1] pt-2 text-sm first-of-type:border-t-0">
                  <div className="grid grid-cols-[1fr_5rem] items-center gap-2">
                    <span><b>{l.medicine}</b>{parts.length > 1 && <span className="ml-1 rounded-full bg-warn-soft px-1.5 text-[11px] font-semibold text-warn">{t('rs.splitOf', { total: l.suggested })}</span>}
                      <span className="block text-xs text-muted">{t('reorder.line', { stock: l.stock, sold: l.soldLast30Days })}{rate != null ? ` · ${formatRupees(rate)}` : ''}{l.daysLeft != null ? ` · ${t('orderToday.daysLeft', { count: Math.floor(l.daysLeft) })}` : ''}</span>
                    </span>
                    <input aria-label={`${t('reorder.qty')}: ${l.medicine}`} value={qty} inputMode="decimal"
                      onChange={(e) => setParts(l.productId, parts.map((p, i) => (i === part ? { ...p, qty: e.target.value } : p)))} className="h-11 rounded-lg border border-line px-2" />
                  </div>
                  {(l.options?.length ?? 0) > 0 && (
                    splitting === `${l.productId}-${part}` ? (
                      <div className="flex flex-col gap-1 rounded-xl bg-[#f6faf9] p-2">
                        {l.options!.map((o) => (
                          <div key={o.supplierId} className="flex flex-wrap items-center justify-between gap-2 text-xs">
                            <span>{o.preferred && '★ '}<b>{o.name}</b> · {o.lastRate != null ? formatRupees(o.lastRate) : t('rs.noRate')}{o.leadDays != null && ` · ${t('orderToday.lead', { count: o.leadDays })}`}{o.fillRate != null && ` · ${t('rs.fill')} ${pct(o.fillRate)}`}</span>
                            {o.supplierId !== g.supplierId && (
                              <span className="flex gap-1">
                                <button onClick={() => { setParts(l.productId, parts.map((p, i) => (i === part ? { ...p, supplierId: o.supplierId } : p))); setSplitting(null) }} className="h-8 rounded-lg border border-line bg-white px-2 font-semibold">{t('rs.moveHere')}</button>
                                {!parts.some((p) => p.supplierId === o.supplierId) && <button onClick={() => {
                                  const half = Math.floor((Number(qty) || 0) / 2)
                                  setParts(l.productId, [...parts.map((p, i) => (i === part ? { ...p, qty: String((Number(qty) || 0) - half) } : p)), { supplierId: o.supplierId, qty: String(half) }])
                                  setSplitting(null)
                                }} className="h-8 rounded-lg border border-line bg-white px-2 font-semibold">{t('rs.splitHalf')}</button>}
                              </span>
                            )}
                          </div>
                        ))}
                        <button onClick={() => setSplitting(null)} className="self-end text-xs font-semibold text-muted">{t('cancel')}</button>
                      </div>
                    ) : <button onClick={() => setSplitting(`${l.productId}-${part}`)} className="self-start text-xs font-semibold text-brand">⇄ {t('rs.otherSuppliers', { count: l.options!.length })}</button>
                  )}
                </div>
              )
            })}
            {g.supplierId && toOrder.length > 0 && <button onClick={() => void saveOrder(g.supplierId!, toOrder)} className="h-11 rounded-xl border-2 border-brand font-semibold text-brand-strong">{t('supplier2.saveAsOrder')}</button>}
            {(who?.phone || !g.supplierId) && toOrder.length > 0
              ? <a href={whatsAppLink(who?.phone ?? null, message)} target="_blank" rel="noreferrer" className="flex h-11 items-center justify-center rounded-xl bg-[#15803d] font-semibold text-white">{t('reorder.send')}</a>
              : <p className="text-xs text-muted">{t(general ? 'reorder.noPhoneAll' : 'reorder.noPhone')}</p>}
          </section>
        )
      })}
    </div>
  )
}

/** On a bill sold on a prescription: its photo (shop only), or a button to attach one. */
export function PrescriptionPhoto({ billId }: { billId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const photo = useQuery({
    queryKey: ['rx-photo', billId], retry: false,
    queryFn: async () => { const r = await request(`/v1/bills/${billId}/prescription-photo`); return r.ok ? URL.createObjectURL(await r.blob()) : null },
  }).data
  const [error, setError] = useState('')
  useEffect(() => () => { if (photo) URL.revokeObjectURL(photo) }, [photo])
  const attach = async (file: File) => {
    setError('')
    try {
      await api(`/v1/bills/${billId}/prescription-photo`, 'PUT', { dataUrl: await shrinkPhoto(file, 1400, 400_000, false) })
      void queryClient.invalidateQueries({ queryKey: ['rx-photo', billId] })
    } catch (e) {
      setError(e instanceof Error ? e.message : t('error'))
    }
  }
  return (
    <div className="mb-3 print:hidden">
      {photo ? <img src={photo} alt={t('rxPhoto.alt')} className="max-h-80 rounded-lg border border-line" /> : (
        <label className="flex min-h-11 cursor-pointer items-center justify-center rounded-xl border-2 border-dashed border-brand font-semibold text-brand-strong">
          📷 {t('rxPhoto.attach')}
          <input type="file" accept="image/*" capture="environment" className="sr-only" onChange={(e) => { const f = e.target.files?.[0]; e.target.value = ''; if (f) void attach(f) }} />
        </label>
      )}
      {error && <p className="text-sm text-due">{error}</p>}
    </div>
  )
}

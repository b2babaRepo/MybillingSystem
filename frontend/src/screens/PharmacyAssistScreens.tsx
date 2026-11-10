import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api, request } from '../lib/api'
import { formatRupees, whatsAppLink } from '../lib/format'
import { shrinkPhoto } from '../lib/images'

interface Refill { customerId: string; customerName: string; phone: string | null; productId: string; medicine: string; lastQuantity: number; lastBought: string; everyDays: number; dueOn: string; daysLate: number }
interface ReorderGroup { supplierId: string | null; supplierName: string | null; phone: string | null; lines: { productId: string; medicine: string; stock: number; soldLast30Days: number; suggested: number; lastRate: number | null }[] }

const day = (iso: string) => new Date(`${iso}T00:00`).toLocaleDateString('en-IN', { day: 'numeric', month: 'short' })

/** Regular patients due a refill, worked out from how often they buy each medicine. A reminder goes only when the shop taps it. */
export function RefillsScreen({ shopId, shopName }: { shopId: string; shopName: string }) {
  const { t } = useTranslation()
  const refills = useQuery({ queryKey: [shopId, 'refills'], queryFn: () => api<Refill[]>('/v1/pharmacy/refills') }).data
  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <p className="text-sm text-muted">{t('refills.hint')}</p>
      {refills?.length === 0 && <p className="rounded-[18px] bg-white p-3 text-sm text-muted">{t('refills.none')}</p>}
      {refills?.map((r) => (
        <div key={`${r.customerId}-${r.productId}`} className={`flex flex-col gap-1 rounded-[18px] p-3 ${r.daysLate > 0 ? 'bg-amber-50' : 'bg-white'}`}>
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

/** What to order, grouped by distributor: the owner edits the quantities, then sends the order on WhatsApp. */
export function ReorderScreen({ shopId, shopName, general = false }: { shopId: string; shopName: string; general?: boolean }) {
  // general: any trade's shop (items and suppliers); otherwise a medical shop's wording (medicines and distributors).
  const { t } = useTranslation()
  const groups = useQuery({ queryKey: [shopId, 'reorder'], queryFn: () => api<ReorderGroup[]>('/v1/pharmacy/reorder') }).data
  const [qty, setQty] = useState<Record<string, string>>({})
  const queryClient = useQueryClient()
  const [saved, setSaved] = useState<Record<string, string>>({})
  // Saved, an order counts as on its way: it leaves this list, and its goods match it when they come.
  async function saveOrder(supplierId: string, lines: { productId: string; order: number; lastRate: number | null }[]) {
    try {
      const order = await api<{ number: number }>('/v1/suppliers/orders', 'POST', { id: crypto.randomUUID(), supplierId, lines: lines.map((l) => ({ productId: l.productId, quantity: l.order, rate: l.lastRate })) })
      setSaved({ ...saved, [supplierId]: t('supplier2.savedOrder', { number: order.number }) })
      await queryClient.invalidateQueries({ queryKey: [shopId, 'reorder'] })
    } catch (e) {
      setSaved({ ...saved, [supplierId]: e instanceof Error ? e.message : t('error') })
    }
  }
  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <p className="text-sm text-muted">{t(general ? 'reorder.hintAll' : 'reorder.hint')}</p>
      {Object.values(saved).map((m) => <p key={m} className="rounded-[18px] bg-teal-50 p-3 text-sm text-teal-900">{m}</p>)}
      {groups?.length === 0 && <p className="rounded-[18px] bg-white p-3 text-sm text-muted">{t('reorder.none')}</p>}
      {groups?.map((g) => {
        const lines = g.lines.map((l) => ({ ...l, order: Number(qty[l.productId] ?? l.suggested) || 0 })).filter((l) => l.order > 0)
        const message = t('reorder.message', { shop: shopName, lines: lines.map((l) => `${l.medicine} × ${l.order}`).join('\n') })
        return (
          <section key={g.supplierId ?? 'none'} className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
            <h3 className="font-bold">{g.supplierName ?? t(general ? 'reorder.noSupplierAll' : 'reorder.noSupplier')}</h3>
            {g.lines.map((l) => (
              <div key={l.productId} className="grid grid-cols-[1fr_5rem] items-center gap-2 text-sm">
                <span><b>{l.medicine}</b><span className="block text-xs text-muted">{t('reorder.line', { stock: l.stock, sold: l.soldLast30Days })}{l.lastRate != null ? ` · ${formatRupees(l.lastRate)}` : ''}</span></span>
                <input aria-label={`${t('reorder.qty')}: ${l.medicine}`} value={qty[l.productId] ?? String(l.suggested)} inputMode="decimal"
                  onChange={(e) => setQty({ ...qty, [l.productId]: e.target.value })} className="h-11 rounded-lg border border-line px-2" />
              </div>
            ))}
            {g.supplierId && lines.length > 0 && <button onClick={() => void saveOrder(g.supplierId!, lines)} className="h-11 rounded-xl border-2 border-teal-700 font-semibold text-teal-800">{t('supplier2.saveAsOrder')}</button>}
            {(g.phone || !g.supplierId) && lines.length > 0
              ? <a href={whatsAppLink(g.phone ?? null, message)} target="_blank" rel="noreferrer" className="flex h-11 items-center justify-center rounded-xl bg-[#15803d] font-semibold text-white">{t('reorder.send')}</a>
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
        <label className="flex min-h-11 cursor-pointer items-center justify-center rounded-xl border-2 border-dashed border-teal-700 font-semibold text-teal-800">
          📷 {t('rxPhoto.attach')}
          <input type="file" accept="image/*" capture="environment" className="sr-only" onChange={(e) => { const f = e.target.files?.[0]; e.target.value = ''; if (f) void attach(f) }} />
        </label>
      )}
      {error && <p className="text-sm text-due">{error}</p>}
    </div>
  )
}

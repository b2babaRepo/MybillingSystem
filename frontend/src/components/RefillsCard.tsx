import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { addToCart } from '../lib/customer'
import { navigate } from '../router'

interface MyRefill { shopId: string; shopName: string; shopSlug: string | null; productId: string; medicine: string; lastQuantity: number; everyDays: number; daysLate: number; inStock: boolean }

/**
 * Medicines the customer buys regularly at a chemist and that are due again (from their own bills there, only after
 * they confirmed that khata). One tap puts the usual quantity in that chemist's cart.
 */
export function RefillsCard() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const refills = useQuery({ queryKey: ['me', 'refills'], queryFn: () => api<MyRefill[]>('/v1/me/refills'), retry: false, staleTime: 10 * 60_000 }).data ?? []
  const [busy, setBusy] = useState<string | null>(null)
  if (refills.length === 0) return null

  async function order(r: MyRefill) {
    if (!r.shopSlug) return
    if (!r.inStock) { navigate(`/s/${r.shopSlug}`); return }
    setBusy(r.productId)
    try {
      await addToCart(r.shopId, { [r.productId]: r.lastQuantity })
      await queryClient.invalidateQueries({ queryKey: ['me', 'carts'] })
      navigate(`/s/${r.shopSlug}/cart`)
    } finally {
      setBusy(null)
    }
  }
  const when = (late: number) => (late < 0 ? t('cust5.dueIn', { count: -late }) : late === 0 ? t('cust5.dueToday') : t('cust5.dueLate', { count: late }))

  return (
    <section aria-label={t('cust5.refillsTitle')} className="flex flex-col gap-2 rounded-card border border-[#cfe0f7] bg-[#f2f7ff] p-3.5">
      <h2 className="font-extrabold text-[#1e3a8a]">{t('cust5.refillsTitle')}</h2>
      {refills.slice(0, 4).map((r) => (
        <div key={`${r.shopId}-${r.productId}`} className="flex items-center gap-3 rounded-xl bg-white p-2.5">
          <span className="flex min-w-0 flex-1 flex-col">
            <b className="truncate">{r.medicine}</b>
            <span className={`text-xs font-semibold ${r.daysLate > 0 ? 'text-udhaar' : 'text-[#1d4ed8]'}`}>{when(r.daysLate)}</span>
            <span className="truncate text-xs text-muted">{t('cust5.lastTime', { qty: r.lastQuantity, every: r.everyDays })} · {r.shopName}</span>
          </span>
          {r.shopSlug && (
            <button onClick={() => void order(r)} disabled={busy === r.productId} className="h-10 shrink-0 rounded-xl bg-[#1d4ed8] px-3 text-sm font-bold text-white disabled:opacity-50">
              {r.inStock ? t('cust5.orderRefill') : t('cust.openShop')}
            </button>
          )}
        </div>
      ))}
      <span className="text-[11px] text-[#33415f]">{t('cust5.refillNote')}</span>
    </section>
  )
}

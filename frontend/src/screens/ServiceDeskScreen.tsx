import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type ChangeEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { shrinkPhoto } from '../lib/images'
import { shownError, shownDone } from '../lib/notify'

interface Flag { kind: string; text: string; days: number; amount: number | null }
interface PriceLine { name: string; mrp: number | null; dealerPrice: number | null; productId: string | null; productName: string | null; sellingPrice: number | null; costPrice: number | null }

/**
 * The owner's service checks: where money is slipping (from the shop's own records, no AI), and a company price list
 * read from a photo, matched to the shop's items, with new rates to confirm before anything changes.
 */
export function ServiceDeskScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const flags = useQuery({ queryKey: [shopId, 'service-flags'], queryFn: () => api<Flag[]>('/v1/service/flags') })
  const [lines, setLines] = useState<(PriceLine & { newPrice: string; use: boolean })[]>([])
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')

  async function read(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setBusy(true)
    setMessage('')
    try {
      const found = await api<PriceLine[]>('/v1/service/price-list/photo', 'POST', { imageDataUrl: await shrinkPhoto(file, 1600, 1_200_000, false) })
      setLines(found.map((l) => ({ ...l, newPrice: l.mrp != null ? String(l.mrp) : '', use: l.productId != null && l.mrp != null && l.mrp !== l.sellingPrice })))
      if (found.length === 0) setMessage(t('desk.nothingRead'))
    } catch (err) {
      setMessage(shownError(err))
    } finally {
      setBusy(false)
      e.target.value = ''
    }
  }

  async function save() {
    const rates = lines.filter((l) => l.use && l.productId && Number(l.newPrice) > 0).map((l) => ({ productId: l.productId, sellingPrice: Number(l.newPrice) }))
    setMessage('')
    try {
      await api('/v1/products/rates', 'PUT', { rates })
      setLines([])
      setMessage(shownDone(t('desk.ratesSaved', { count: rates.length })))
      void queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
    } catch (err) {
      setMessage(shownError(err))
    }
  }

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <section className="flex flex-col gap-1 rounded-card bg-white p-3 text-sm">
        <h3 className="font-bold">{t('desk.flagsTitle')}</h3>
        <p className="text-xs text-muted">{t('desk.flagsHint')}</p>
        {flags.data?.length === 0 && <p className="text-brand-strong">✓ {t('desk.allClear')}</p>}
        {flags.data?.map((f, i) => (
          <div key={i} className="flex justify-between gap-2 border-b border-line py-1.5">
            <span><b>{t(`desk.flag.${f.kind}`)}</b> · {f.text}</span>
            <span className="shrink-0 text-right text-due">{t('desk.days', { days: f.days })}{f.amount != null ? ` · ${formatRupees(f.amount)}` : ''}</span>
          </div>
        ))}
      </section>

      <section className="flex flex-col gap-2 rounded-card bg-white p-3 text-sm">
        <h3 className="font-bold">{t('desk.priceTitle')}</h3>
        <p className="text-xs text-muted">{t('desk.priceHint')}</p>
        <label className="flex min-h-12 cursor-pointer items-center justify-center rounded-2xl border border-dashed border-line font-semibold text-brand">
          {busy ? '…' : t('desk.pickPhoto')}
          <input type="file" accept="image/*" capture="environment" onChange={(e) => void read(e)} disabled={busy} className="sr-only" />
        </label>
        {lines.map((l, i) => (
          <div key={i} className="grid grid-cols-[auto_1fr_6rem] items-center gap-2 border-b border-line py-1.5">
            <input type="checkbox" checked={l.use} disabled={!l.productId} aria-label={l.name}
              onChange={(e) => setLines(lines.map((x, j) => (j === i ? { ...x, use: e.target.checked } : x)))} className="h-5 w-5" />
            <span>
              <span className="block font-semibold">{l.name}</span>
              <span className="block text-xs text-muted">
                {l.productName ? t('desk.matched', { name: l.productName, price: l.sellingPrice != null ? formatRupees(l.sellingPrice) : '—' }) : t('desk.noMatch')}
                {l.dealerPrice != null ? ` · ${t('desk.dealer', { price: formatRupees(l.dealerPrice) })}` : ''}
              </span>
            </span>
            <input value={l.newPrice} inputMode="decimal" aria-label={t('desk.newPrice')} disabled={!l.productId}
              onChange={(e) => setLines(lines.map((x, j) => (j === i ? { ...x, newPrice: e.target.value } : x)))} className="h-10 rounded-lg border border-line px-2" />
          </div>
        ))}
        {lines.some((l) => l.use) && <Button onClick={() => void save()}>{t('desk.saveRates', { count: lines.filter((l) => l.use).length })}</Button>}
        {message && <p className="font-semibold text-brand-deep">{message}</p>}
      </section>
    </div>
  )
}

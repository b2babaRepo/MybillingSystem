import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { useProducts } from '../lib/data'
import type { Serial } from '../lib/serials'
import { navigate } from '../router'

const day = (iso: string) => new Date(`${iso}T00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric' })

/** On a sales & service shop's home. */
export function ServiceHomeCard({ isOwner }: { isOwner: boolean }) {
  const { t } = useTranslation()
  const link = (path: string, label: string) => (
    <button onClick={() => navigate(path)} className="mx-4 mt-3 flex min-h-12 w-[calc(100%-2rem)] items-center justify-between rounded-[18px] bg-white px-4 text-left font-semibold">
      <span>{label}</span><span aria-hidden="true">→</span>
    </button>
  )
  return <>{link('/serials', `🔌 ${t('serials.link')}`)}{link('/trips', `🚐 ${t('trips.link')}`)}{link('/amc', `📅 ${t('amc.link')}`)}{isOwner && link('/claims', `🧾 ${t('claims.link')}`)}{isOwner && link('/service-desk', `🔎 ${t('desk.link')}`)}</>
}

/** Serial numbers: note them when units come in, and look any unit up (who bought it, warranty till when). */
export function SerialsScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const units = useProducts(shopId).products.filter((p) => p.tracksSerials)
  const [productId, setProductId] = useState('')
  const [text, setText] = useState('')
  const [message, setMessage] = useState('')
  const [find, setFind] = useState('')
  const [found, setFound] = useState<Serial[] | null>(null)
  const list = useQuery({ queryKey: [shopId, 'serials', productId], enabled: !!productId, queryFn: () => api<Serial[]>(`/v1/serials?productId=${productId}`) }).data ?? []
  const serials = text.split(/[\n,]+/).map((s) => s.trim()).filter(Boolean)

  async function run(work: () => Promise<unknown>, done: string) {
    setMessage('')
    try {
      await work()
      setMessage(done)
      void queryClient.invalidateQueries({ queryKey: [shopId, 'serials'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  const lookup = () => run(async () => setFound(await api<Serial[]>(`/v1/serials/lookup?serial=${encodeURIComponent(find.trim())}`)), '').catch(() => setFound([]))

  const row = (s: Serial) => (
    <div key={s.id} className="flex flex-col gap-1 border-b border-line py-2 text-sm">
      <p className="flex justify-between gap-2"><b>{s.serialNo}</b><span>{s.productName}</span></p>
      {s.status === 'in_stock'
        ? <p className="text-teal-800">{t('serials.inStock', { date: day(s.receivedOn) })}</p>
        : <p>{t('serials.sold', { date: s.soldOn ? day(s.soldOn) : '', bill: s.billNo ?? '', customer: s.customerName ?? t('walkIn') })}</p>}
      {s.warrantyUntil && <p className={s.inWarranty ? 'text-teal-800' : 'text-due'}>{t(s.inWarranty ? 'serials.warrantyTill' : 'serials.warrantyOver', { date: day(s.warrantyUntil) })}</p>}
      {s.status === 'sold' && (s.warrantyRegistered
        ? <p className="text-xs text-teal-800">✓ {t('serials.registered')}</p>
        : <Button tone="plain" className="h-9 self-start px-3 text-xs" onClick={() => void run(() => api(`/v1/serials/${s.id}/registered`, 'POST', {}), t('saved'))}>{t('serials.markRegistered')}</Button>)}
    </div>
  )

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
        <h3 className="font-bold">{t('serials.lookupTitle')}</h3>
        <div className="grid grid-cols-[1fr_auto] items-end gap-2">
          <Field label={t('serials.serial')} value={find} onChange={(e) => setFind(e.target.value)} />
          <Button className="h-12 px-4" disabled={!find.trim()} onClick={() => void lookup()}>{t('serials.find')}</Button>
        </div>
        {found?.length === 0 && <p className="text-sm text-due">{t('serials.notFound')}</p>}
        {found?.map(row)}
      </section>

      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
        <h3 className="font-bold">{t('serials.receiveTitle')}</h3>
        <p className="text-xs text-muted">{t('serials.receiveHint')}</p>
        <select value={productId} onChange={(e) => setProductId(e.target.value)} aria-label={t('serials.item')} className="h-12 w-full rounded-xl border border-line bg-white px-3">
          <option value="">{t('serials.chooseItem')}</option>{units.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
        </select>
        {units.length === 0 && <p className="text-xs text-muted">{t('serials.noUnits')}</p>}
        <textarea value={text} onChange={(e) => setText(e.target.value)} rows={4} placeholder={t('serials.placeholder')} className="rounded-xl border border-line p-2 font-mono text-sm" />
        <Button disabled={!productId || serials.length === 0} onClick={() => void run(async () => {
          const r = await api<{ added: number; skipped: number }>('/v1/serials/receive', 'POST', { productId, serials })
          setText('')
          setMessage(t('serials.added', { added: r.added, skipped: r.skipped }))
        }, '')}>{t('serials.save', { count: serials.length })}</Button>
        {message && <p className="text-sm font-semibold text-teal-900">{message}</p>}
        {productId && list.map(row)}
      </section>
    </div>
  )
}

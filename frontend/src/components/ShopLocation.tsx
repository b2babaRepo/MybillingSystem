import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { currentPoint } from '../lib/location'
import { CustIcon } from './CustIcons'

/**
 * The shop's place on the map: the owner stands in the shop and taps once. Customers who look for shops near them,
 * or near their saved address, then find this shop (within 5 km).
 */
export function ShopLocation({ isOwner }: { isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const location = useQuery({ queryKey: ['shop', 'location'], queryFn: () => api<{ latitude: number | null; longitude: number | null }>('/v1/shop/location') }).data
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const set = location?.latitude != null

  async function save(point: { lat: number; lng: number } | null) {
    setBusy(true)
    setMessage('')
    try {
      await api('/v1/shop/location', 'PUT', { latitude: point?.lat ?? null, longitude: point?.lng ?? null })
      await queryClient.invalidateQueries({ queryKey: ['shop', 'location'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    } finally {
      setBusy(false)
    }
  }
  async function here() {
    setBusy(true)
    setMessage('')
    try {
      await save(await currentPoint())
    } catch (e) {
      setMessage(t(`cust12.loc_${e instanceof Error ? e.message : 'unavailable'}`))
      setBusy(false)
    }
  }

  return (
    <section className="mt-3 flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
      <div className="flex items-center gap-2">
        <CustIcon name="pin" className="text-brand" />
        <h2 className="flex-1 font-bold">{t('shopLoc.title')}</h2>
        {set && <span className="rounded-full bg-brand-soft px-2 py-0.5 text-xs font-bold text-brand-deep">{t('shopLoc.set')}</span>}
      </div>
      <p className="text-sm text-muted">{set ? t('shopLoc.setHint') : t('shopLoc.hint')}</p>
      {isOwner && (
        <div className="flex flex-wrap gap-2">
          <button onClick={() => void here()} disabled={busy} className="flex h-11 items-center gap-1.5 rounded-xl bg-brand px-4 font-bold text-white disabled:opacity-50">
            <CustIcon name="pin" size={16} />{busy ? t('home2.locating') : set ? t('shopLoc.update') : t('shopLoc.useHere')}
          </button>
          {set && <button onClick={() => void save(null)} disabled={busy} className="h-11 rounded-xl border border-line px-4 font-semibold text-due">{t('remove')}</button>}
        </div>
      )}
      {message && <p className="text-sm text-due">{message}</p>}
    </section>
  )
}

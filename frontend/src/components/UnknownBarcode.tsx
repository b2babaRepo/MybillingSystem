import { controlSm } from './ui'
import { btn } from '../lib/buttonStyles'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { addProduct } from '../lib/data'
import type { Product } from '../lib/types'
import { ItemPicker } from './ItemPicker'
import { shownError } from '../lib/notify'

const input = controlSm

/**
 * A scanned code no item has yet: put it on an item the shop already has (a pack whose code was never saved, or a
 * new pack design of an item that has a code: it scans as either), or
 * make a new item with it. Either way the item goes on the bill, and next time the scan finds it.
 */
export function UnknownBarcode({ shopId, code, products, onItem, onClose }: {
  shopId: string
  code: string
  products: Product[]
  onItem: (product: { id: string; name: string; unit: Product['unit']; sellingPrice: number }) => void
  onClose: () => void
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [mode, setMode] = useState<'choose' | 'attach' | 'new'>('choose')
  const [text, setText] = useState('')
  const [name, setName] = useState('')
  const [price, setPrice] = useState('')
  const [message, setMessage] = useState('')
  // A packaged product's brand, name and pack from Open Food Facts, to fill the new item's name. Nothing found: type it.
  const lookup = useQuery({
    queryKey: ['barcode-details', code],
    queryFn: () => api<{ name: string; brand: string | null; pack: string | null } | undefined>(`/v1/barcodes/${code}`),
    enabled: mode === 'new', staleTime: Infinity, retry: false,
  })
  const [filled, setFilled] = useState(false)
  const [lookedUp, setLookedUp] = useState(lookup.data)
  if (lookedUp !== lookup.data) {
    setLookedUp(lookup.data)
    if (lookup.data?.name && !name) { setName(lookup.data.name); setFilled(true) }
  }
  const goods = products.filter((p) => !p.isService)
  const picked = goods.find((p) => p.name.toLowerCase() === text.trim().toLowerCase())

  async function attach() {
    if (!picked) return
    setMessage('')
    try {
      // An item with no code gets this as its code; one that has a code gets it as another (a new pack design).
      await api(`/v1/products/${picked.id}/barcodes`, 'POST', { barcode: code })
      await queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
      onItem(picked)
    } catch (e) {
      setMessage(shownError(e))
    }
  }

  async function create() {
    setMessage('')
    const sellingPrice = Number(price)
    try {
      // Saved on the phone first like any new item, so it works with no signal too.
      const id = await addProduct(shopId, { name: name.trim(), unit: 'piece', sellingPrice, costPrice: null, category: null, reorderLevel: null, openingStock: null, barcode: code })
      onItem({ id, name: name.trim(), unit: 'piece', sellingPrice })
      // Its pack photo too, when the barcode database has one (best effort: an item still waiting to sync just goes without).
      if (filled) setTimeout(() => void api(`/v1/products/${id}/image/barcode`, 'POST', { barcode: code }).then(() => queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })).catch(() => undefined), 1500)
    } catch (e) {
      setMessage(shownError(e))
    }
  }

  return (
    <div className="flex flex-col gap-2 rounded-xl bg-due-soft p-3 text-sm">
      <p className="flex items-start justify-between gap-2 font-semibold text-due">
        <span>{t('scan.notFound', { code })}</span>
        <button type="button" onClick={onClose} aria-label={t('cancel')} className="min-h-8 shrink-0 px-2 text-muted">×</button>
      </p>
      {mode === 'choose' && (
        <div className="grid grid-cols-2 gap-2">
          <button type="button" onClick={() => setMode('attach')} className="min-h-11 rounded-xl border border-brand bg-white font-semibold text-brand-strong">{t('scan.attach')}</button>
          <button type="button" onClick={() => setMode('new')} className="min-h-11 rounded-xl border border-brand bg-white font-semibold text-brand-strong">{t('scan.newItem')}</button>
        </div>
      )}
      {mode === 'attach' && (
        <div className="flex flex-col gap-2">
          <ItemPicker products={goods} text={text} onText={setText} onPick={(p) => setText(p.name)} placeholder={t('scan.whichItem')} />
          <button type="button" disabled={!picked} onClick={() => void attach()} className={`min-h-11 rounded-xl ${btn.solid}`}>{t('scan.attachSave')}</button>
        </div>
      )}
      {mode === 'new' && (
        <div className="grid grid-cols-[1fr_6rem] gap-2">
          {lookup.isFetching && <p className="col-span-2 text-xs text-muted">{t('scan.lookingUp')}</p>}
          {filled && <p className="col-span-2 text-xs font-semibold text-brand-deep">✓ {t('scan.filled')}</p>}
          <input value={name} onChange={(e) => { setName(e.target.value); setFilled(false) }} placeholder={t('scan.itemName')} aria-label={t('scan.itemName')} maxLength={120} className={input} />
          <input value={price} onChange={(e) => setPrice(e.target.value)} placeholder={t('price')} aria-label={t('price')} inputMode="decimal" className={input} />
          <button type="button" disabled={name.trim().length < 2 || !(Number(price) > 0)} onClick={() => void create()} className="col-span-2 min-h-11 rounded-xl bg-brand font-semibold text-white disabled:opacity-40">{t('scan.newSave')}</button>
        </div>
      )}
      {message && <p className="text-due">{message}</p>}
    </div>
  )
}

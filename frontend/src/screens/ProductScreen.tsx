import { useQuery, useQueryClient } from '@tanstack/react-query'
import { billNameFor } from '../lib/billNames'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field, Sheet } from '../components/ui'
import { api, request } from '../lib/api'
import { addProduct, addStockMovement, useProducts } from '../lib/data'
import { ask } from '../components/Ask'
import { useGstBilling } from '../lib/billing'
import { shopTypes, useShopSettings } from '../lib/reports'
import { GST_RATES } from '../lib/gst'
import { formatDateTime, formatRupees } from '../lib/format'
import type { Product, StockMovement, StockReason, Unit } from '../lib/types'
import { UnitSelect } from './BillScreen'
import { StockBadge } from './ItemsScreen'
import { ItemCard } from '../components/ItemCard'
import { imageUrl, itemPhotos } from '../lib/images'
import { navigate } from '../router'
import { confirmAsk } from '../components/Ask'

const REASONS: StockReason[] = ['damage', 'wastage', 'return', 'count_correction', 'other']

/** One item: shelf count with "goods in" and "reduce", its details, and every stock change. */
export function ProductScreen({ shopId, productId }: { shopId: string; productId: string }) {
  const { t } = useTranslation()
  const { products } = useProducts(shopId)
  const product = products.find((p) => p.id === productId)
  const history = useQuery({
    queryKey: [shopId, 'movements', productId],
    queryFn: () => api<StockMovement[]>(`/v1/products/${productId}/movements`),
    enabled: !!product && !product.pending,
  })
  const [moving, setMoving] = useState<'purchase' | 'adjustment' | null>(null)

  if (!product) return <p className="p-4 text-muted">…</p>

  return (
    <div className="flex flex-col gap-3 p-4">
      <section className="flex items-start justify-between gap-3 rounded-[18px] bg-white p-4">
        <span className="flex min-w-0 flex-col">
          <span className="text-2xl font-bold">{product.name}</span>
          <span className="text-sm text-muted">
            {product.sellingPrice > 0 ? `${formatRupees(product.sellingPrice)}/${t(`units.${product.unit}`)}` : t('stock.noPrice')}
            {product.category && ` · ${product.category}`}
          </span>
        </span>
        <StockBadge product={product} large />
      </section>

      <PhotoCard shopId={shopId} product={product} />

      <div className="grid grid-cols-2 gap-2">
        <Button tone="payment" className="h-14" onClick={() => setMoving('purchase')}>+ {t('stock.goodsIn')}</Button>
        <Button tone="credit" className="h-14" onClick={() => setMoving('adjustment')}>− {t('stock.reduce')}</Button>
      </div>

      {/* Keyed by the saved values, so the form shows a new cost price once a purchase syncs. */}
      <DetailsForm
        key={`${product.id}:${product.name}:${product.sellingPrice}:${product.costPrice}:${product.reorderLevel}:${product.category}`}
        shopId={shopId}
        product={product}
      />

      <section className="rounded-[18px] bg-white px-4 py-3">
        <h2 className="pb-1 text-base font-bold">{t('stock.history')}</h2>
        <ul className="divide-y divide-[#edf2f1]">
          {history.data?.map((m) => (
            <li key={m.id} className="flex items-center justify-between gap-2 py-2.5">
              <span className="flex flex-col">
                <span className="font-semibold">
                  {t(`stock.kinds.${m.kind}`)}
                  {m.reason && ` · ${t(`stock.reasons.${m.reason}`)}`}
                </span>
                <span className="text-xs text-muted">
                  {formatDateTime(m.occurredAt)}
                  {m.costPrice !== null && ` · ${formatRupees(m.costPrice)}`}
                  {m.note && ` · ${m.note}`}
                </span>
              </span>
              <span className={`font-bold ${m.quantity > 0 ? 'text-jama' : 'text-udhaar'}`}>
                {m.quantity > 0 ? '+' : ''}{m.quantity} {t(`units.${product.unit}`)}
              </span>
            </li>
          ))}
        </ul>
      </section>

      {moving && <MoveSheet shopId={shopId} product={product} kind={moving} onClose={() => setMoving(null)} />}
    </div>
  )
}

function MoveSheet({ shopId, product, kind, onClose }: { shopId: string; product: Product; kind: 'purchase' | 'adjustment'; onClose: () => void }) {
  const { t } = useTranslation()
  const [quantity, setQuantity] = useState('')
  const [cost, setCost] = useState('')
  const [reason, setReason] = useState<StockReason>('damage')
  const [note, setNote] = useState('')
  const value = Number(quantity)

  async function save() {
    await addStockMovement(
      shopId,
      product,
      kind,
      kind === 'purchase' ? value : -value,
      kind === 'adjustment' ? reason : null,
      kind === 'purchase' && cost ? Number(cost) : null,
      note.trim() || null,
    )
    onClose()
  }

  return (
    <Sheet title={kind === 'purchase' ? t('stock.goodsIn') : t('stock.reduce')} onClose={onClose}>
      <div className="space-y-3">
        <Field
          label={`${kind === 'purchase' ? t('stock.quantityIn') : t('stock.quantityOut')} (${t(`units.${product.unit}`)})`}
          value={quantity}
          onChange={(e) => setQuantity(e.target.value)}
          inputMode="decimal"
          autoFocus
        />
        {kind === 'purchase' && <Field label={`${t('stock.costPrice')} / ${t(`units.${product.unit}`)}`} value={cost} onChange={(e) => setCost(e.target.value)} inputMode="decimal" />}
        {kind === 'adjustment' && (
          <div>
            <span className="mb-1 block text-sm font-semibold text-muted">{t('stock.reason')}</span>
            <div className="flex flex-wrap gap-2">
              {REASONS.map((r) => (
                <button key={r} onClick={() => setReason(r)} aria-pressed={reason === r}
                  className={`h-11 rounded-full px-4 font-semibold ${reason === r ? 'bg-ink text-white' : 'border border-line bg-white'}`}>
                  {t(`stock.reasons.${r}`)}
                </button>
              ))}
            </div>
          </div>
        )}
        <Field label={t('note')} value={note} onChange={(e) => setNote(e.target.value)} maxLength={200} />
        <Button tone={kind === 'purchase' ? 'payment' : 'credit'} className="w-full" disabled={!(value > 0)} onClick={save}>
          {t('save')}
        </Button>
      </div>
    </Sheet>
  )
}

/** Name, price, category, reorder level. Saved online; stock never changes here. */
function DetailsForm({ shopId, product }: { shopId: string; product: Product }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  async function addVariant() {
    const size = (await ask(t('styles.askSize')))?.trim()
    if (size === undefined || size === null) return
    const colour = (await ask(t('styles.askColour')))?.trim() ?? ''
    if (!size && !colour) return
    const style = product.styleName?.trim() || product.name.replace(new RegExp(`\\s+${(product.size ?? '').replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}$`), '').trim()
    const name = [style, size, colour].filter(Boolean).join(' ')
    await addProduct(shopId, {
      name, unit: product.unit, sellingPrice: product.sellingPrice, costPrice: product.costPrice ?? null, category: product.category ?? null,
      reorderLevel: product.reorderLevel ?? null, openingStock: null, gstRate: product.gstRate ?? null, hsnCode: product.hsnCode ?? null,
      mrp: product.mrp ?? null, billName: product.billName ?? null, styleName: style, size: size || null, colour: colour || null,
    })
    // This item joins the style too, if it wasn't in one yet.
    if (!product.styleName?.trim()) setForm((f) => ({ ...f, styleName: style }))
    setMessage(t('styles.added', { name }))
  }

  const [form, setForm] = useState({
    name: product.name,
    unit: product.unit as Unit,
    price: product.sellingPrice ? String(product.sellingPrice) : '',
    cost: product.costPrice === null ? '' : String(product.costPrice),
    category: product.category ?? '',
    reorder: product.reorderLevel === null ? '' : String(product.reorderLevel),
    gst: product.gstRate == null ? '' : String(product.gstRate),
    hsn: product.hsnCode ?? '',
    schedule: product.schedule ?? '',
    perPack: product.unitsPerPack ? String(product.unitsPerPack) : '',
    serials: !!product.tracksSerials,
    warranty: product.warrantyMonths ? String(product.warrantyMonths) : '',
    mrp: product.mrp ? String(product.mrp) : '',
    billName: product.billName ?? '',
    styleName: product.styleName ?? '',
    size: product.size ?? '',
    colour: product.colour ?? '',
  })
  const gstBilling = useGstBilling(shopId)
  const types = shopTypes(useShopSettings(shopId).data)
  const medical = types.includes('medical')
  const service = types.includes('sales_service')
  const [message, setMessage] = useState('')
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value })

  const body = (isActive?: boolean) => ({
    name: form.name.trim(),
    unit: form.unit,
    sellingPrice: Number(form.price) || 0,
    costPrice: form.cost ? Number(form.cost) : null,
    category: form.category.trim() || null,
    reorderLevel: form.reorder ? Number(form.reorder) : null,
    gstRate: form.gst === '' ? null : Number(form.gst),
    hsnCode: form.hsn.trim() || null,
    replaceTax: true,
    mrp: Number(form.mrp) || 0,
    billName: form.billName.trim(),
    styleName: form.styleName.trim(),
    size: form.size.trim(),
    colour: form.colour.trim(),
    ...(medical ? { schedule: form.schedule || null, replaceSchedule: true, unitsPerPack: Number(form.perPack) || 0 } : {}),
    ...(service ? { tracksSerials: form.serials, warrantyMonths: Number(form.warranty) || 0 } : {}),
    isActive,
  })

  /** Takes the item off the list and the bill screen. Never deleted: past bills, stock history and reports keep it. */
  async function remove() {
    if (!(await confirmAsk(t('stock.removeConfirm', { name: product.name })))) return
    setMessage('')
    try {
      await api(`/v1/products/${product.id}`, 'PUT', { ...body(false), name: product.name, unit: product.unit })
      void queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
      navigate('/items')
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('error'))
    }
  }

  async function save() {
    setMessage('')
    try {
      await api(`/v1/products/${product.id}`, 'PUT', body())
      setMessage(t('saved'))
      void queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('error'))
    }
  }

  return (
    <section className="flex flex-col gap-3 rounded-[18px] bg-white p-4">
      <Field label={t('itemName')} value={form.name} onChange={set('name')} />
      <div className="grid grid-cols-2 gap-2">
        <UnitSelect value={form.unit} onChange={(unit) => setForm({ ...form, unit })} />
        <Field label={t('stock.sellingPrice')} value={form.price} onChange={set('price')} inputMode="decimal" />
      </div>
      {!product.isService && <Field label={t('stock.mrp')} value={form.mrp} onChange={set('mrp')} inputMode="decimal" placeholder={t('stock.mrpHint')} />}
      <Field label={t('billName.label')} value={form.billName} onChange={set('billName')} maxLength={60} placeholder={billNameFor({ billName: null, category: product.category }) ?? t('billName.hint')} />
      {/* Clothes: which design this is one size/colour of, so its sizes show together on the bill and the item list. */}
      {!product.isService && (
        <div className="grid grid-cols-[2fr_1fr_1fr] gap-2">
          <Field label={t('styles.style')} value={form.styleName} onChange={set('styleName')} maxLength={120} />
          <Field label={t('styles.size')} value={form.size} onChange={set('size')} maxLength={20} />
          <Field label={t('styles.colour')} value={form.colour} onChange={set('colour')} maxLength={30} />
        </div>
      )}
      {!product.isService && <BarcodeRow product={product} />}
      {!product.isService && (
        <button type="button" onClick={() => void addVariant()} className="min-h-11 self-start rounded-xl border-2 border-dashed border-teal-700 px-3 font-semibold text-teal-800">{t('styles.addVariant')}</button>
      )}
      <div className="grid grid-cols-2 gap-2">
        <Field label={t('stock.buyingCostShort')} value={form.cost} onChange={set('cost')} inputMode="decimal" />
        <Field label={t('stock.reorderLevel')} value={form.reorder} onChange={set('reorder')} inputMode="decimal" />
      </div>
      <Field label={t('stock.category')} value={form.category} onChange={set('category')} />
      {medical && !product.isService && (
        <label className="flex flex-col gap-1">
          <span className="text-sm font-medium text-muted">{t('rx.schedule')}</span>
          <select value={form.schedule} onChange={set('schedule')} className="h-12 rounded-xl border border-line bg-white px-3 text-lg">
            <option value="">{t('rx.otc')}</option>
            {(['g', 'h', 'h1', 'x'] as const).map((s) => <option key={s} value={s}>{t(`rx.schedule_${s}`)}</option>)}
          </select>
        </label>
      )}
      {service && !product.isService && (
        <div className="grid grid-cols-2 items-end gap-2">
          <label className="flex min-h-12 items-center gap-2 text-sm"><input type="checkbox" checked={form.serials} onChange={(e) => setForm({ ...form, serials: e.target.checked })} className="h-5 w-5" />{t('serials.tracks')}</label>
          <Field label={t('serials.warrantyMonths')} value={form.warranty} onChange={set('warranty')} inputMode="numeric" placeholder="12" />
        </div>
      )}
      {medical && !product.isService && <Field label={t('loose.perPack')} value={form.perPack} onChange={set('perPack')} inputMode="numeric" placeholder="10" />}
      {gstBilling && <div className="grid grid-cols-2 gap-2">
        <label className="flex flex-col gap-1">
          <span className="text-sm font-medium text-muted">{t('stock.gst')}</span>
          <select value={form.gst} onChange={set('gst')} className="h-12 rounded-xl border border-line bg-white px-3 text-lg">
            <option value="">{t('stock.gstNotSet')}</option>
            {[...new Set([...GST_RATES, ...(form.gst ? [form.gst] : [])])].sort((a, b) => Number(a) - Number(b)).map((rate) => <option key={rate} value={rate}>{rate}%</option>)}
          </select>
        </label>
        <Field label={t('stock.hsn')} value={form.hsn} onChange={set('hsn')} inputMode="numeric" maxLength={8} />
      </div>}
      <Button onClick={save} disabled={!form.name.trim() || product.pending}>{t('stock.saveDetails')}</Button>
      {message && <p className="text-sm text-muted">{message}</p>}
      <button type="button" onClick={() => void remove()} disabled={product.pending} className="min-h-11 text-sm font-semibold text-due disabled:opacity-50">{t('stock.remove')}</button>
    </section>
  )
}

/**
 * The item's photo, shown on billing tiles, households' screens and the online store. Taken with the camera or
 * picked from the gallery; the phone shrinks it to a small square first. Without one, the app draws a picture.
 */
function PhotoCard({ shopId, product }: { shopId: string; product: Product }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [barcode, setBarcode] = useState(product.barcode ?? '')
  const [viewing, setViewing] = useState(false)
  const url = imageUrl(shopId, product.id, product.imageAt)

  async function run(action: () => Promise<unknown>) {
    setBusy(true)
    setError(null)
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  const upload = (file: File) => run(async () => api(`/v1/products/${product.id}/image`, 'PUT', await itemPhotos(file)))

  return (
    <section className="flex items-center gap-3 rounded-[18px] bg-white p-3">
      <div className="w-24 shrink-0">
        <ItemCard item={product} title="" compact imageUrl={url} onClick={url ? () => setViewing(true) : undefined} />
        {url && <p className="mt-1 text-center text-[11px] text-muted">{t('photo.tapToView')}</p>}
      </div>
      {viewing && url && (
        <Sheet title={product.name} onClose={() => setViewing(false)}>
          <img src={url} alt={product.name} className="mx-auto aspect-square w-full max-w-md rounded-2xl bg-white object-contain" />
          {product.imageCredit && <p className="mt-2 text-center text-xs text-muted">{t('photo.credit', { source: product.imageCredit })}</p>}
        </Sheet>
      )}
      <div className="flex flex-1 flex-col gap-2">
        <span className="text-sm text-muted">{url ? t('photo.has') : t('photo.none')}</span>
        <label className={`flex min-h-11 cursor-pointer items-center justify-center rounded-xl bg-teal-700 px-3 font-semibold text-white ${busy ? 'opacity-50' : ''}`}>
          📷 {url ? t('photo.change') : t('photo.add')}
          <input type="file" accept="image/*" capture="environment" disabled={busy} className="sr-only"
            onChange={(e) => { const file = e.target.files?.[0]; if (file) void upload(file); e.target.value = '' }} />
        </label>
        {url && product.imageCredit !== 'Representative image' && (
          <button disabled={busy} onClick={() => void run(() => request(`/v1/products/${product.id}/image`, 'DELETE'))} className="text-sm text-muted underline">
            {t('photo.remove')}
          </button>
        )}
        {product.imageCredit && <p className="text-xs text-muted">{t('photo.credit', { source: product.imageCredit })}</p>}
        {(product.photoReports ?? 0) > 0 && (
          <div className="rounded-xl bg-amber-50 p-2 text-sm text-amber-900">
            ⚑ {t('photo.reported', { count: product.photoReports })}{product.photoReportNote ? `: "${product.photoReportNote}"` : ''}
            <button disabled={busy} onClick={() => void run(() => request(`/v1/products/${product.id}/image/reports`, 'DELETE'))} className="ml-2 font-semibold underline">{t('photo.keep')}</button>
          </div>
        )}
        <div className="grid grid-cols-[1fr_auto] gap-2">
          <input value={barcode} onChange={(e) => setBarcode(e.target.value.replace(/\D/g, ''))} inputMode="numeric" maxLength={14} placeholder={t('photo.barcode')}
            aria-label={t('photo.barcode')} className="h-11 min-w-0 rounded-xl border border-line px-3" />
          <button disabled={busy || barcode.length < 8} onClick={() => void run(() => api(`/v1/products/${product.id}/image/barcode`, 'POST', { barcode }))}
            className="h-11 rounded-xl border-2 border-teal-700 px-3 text-sm font-semibold text-teal-800 disabled:opacity-40">{t('photo.findByBarcode')}</button>
        </div>
        <p className="text-xs text-muted">{t('photo.barcodeHint')}</p>
        {error && <p className="text-sm text-due">{error}</p>}
      </div>
    </section>
  )
}

/** The item's barcode for billing by scan: typed from the pack, or a shop code made for loose clothes; then tags. */
function BarcodeRow({ product }: { product: Product }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [code, setCode] = useState(product.barcode ?? '')
  const [message, setMessage] = useState('')
  async function save(path: string, method: 'PUT' | 'POST', body: unknown) {
    setMessage('')
    try {
      const r = await api<{ barcode: string | null }>(path, method, body)
      setCode(r.barcode ?? '')
      setMessage(t('saved'))
      void queryClient.invalidateQueries()
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  return (
    <div className="flex flex-col gap-2 rounded-xl border border-line p-3">
      <span className="text-sm font-semibold text-muted">{t('barcodeBill.label')}</span>
      <div className="grid grid-cols-[1fr_auto] gap-2">
        <input value={code} onChange={(e) => setCode(e.target.value)} inputMode="numeric" maxLength={30} aria-label={t('barcodeBill.label')} className="h-11 min-w-0 rounded-xl border border-line px-3" />
        <button type="button" onClick={() => void save(`/v1/products/${product.id}/barcode`, 'PUT', { barcode: code })} className="h-11 rounded-xl border border-line px-3 font-semibold">{t('save')}</button>
      </div>
      <div className="grid grid-cols-2 gap-2">
        {!product.barcode && <button type="button" onClick={() => void save(`/v1/products/${product.id}/barcode/new`, 'POST', {})} className="min-h-11 rounded-xl border-2 border-teal-700 px-2 text-sm font-semibold text-teal-800">{t('barcodeBill.make')}</button>}
        <button type="button" onClick={() => navigate(`/tags/${product.id}`)} className="min-h-11 rounded-xl border border-line px-2 text-sm font-semibold">{t('tags.open')}</button>
      </div>
      {message && <p className="text-xs text-muted">{message}</p>}
    </div>
  )
}

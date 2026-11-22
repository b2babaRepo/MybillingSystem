import { PhotoGallery } from '../components/PhotoGallery'
import { itemName } from '../lib/itemName'
import { coverText } from '../lib/cover'
import { ItemSuppliersCard } from '../components/SupplierPanels'
import { CategoryInput } from '../components/CategoryInput'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { isOutToday } from '../lib/local'
import { billNameFor } from '../lib/billNames'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Drawer, Field, Select, btn } from '../components/ui'
import { api, request } from '../lib/api'
import { addProduct, addStockMovement, useProducts } from '../lib/data'
import { ask, tell } from '../components/Ask'
import { shopTypes, useShopSettings } from '../lib/reports'
import { GST_RATES } from '../lib/gst'
import { formatRupees } from '../lib/format'
import type { Product, StockReason, Unit } from '../lib/types'
import { UnitSelect } from './BillScreen'
import { StockBadge } from './ItemsScreen'
import { ItemCard } from '../components/ItemCard'
import { imageUrl, itemPhotos, allPhotos, extraPhotoUrl } from '../lib/images'

/** Photos an item may have besides its main one (the server checks the same). */
const MAX_EXTRA_PHOTOS = 5
import { navigate } from '../router'
import { confirmAsk } from '../components/Ask'
import { lazy, Suspense } from 'react'
import { cameraPossible } from '../lib/barcode'
const ScanSheet = lazy(() => import('../components/ScanSheet').then((module) => ({ default: module.ScanSheet })))

// The losses GST rule 56 asks the stock account to show, then the everyday ones.
const REASONS: StockReason[] = ['damage', 'wastage', 'lost', 'stolen', 'destroyed', 'written_off', 'gift', 'free_sample', 'weight_loss', 'return', 'count_correction', 'other']
const ItemLedger = lazy(() => import('../components/ItemLedger').then((module) => ({ default: module.ItemLedger })))

/** One item: shelf count with coloured jobs (goods in, reduce, edit, order), then tabs for its history, details, suppliers and photo. */
export function ProductScreen({ shopId, productId }: { shopId: string; productId: string }) {
  const { t } = useTranslation()
  const { products } = useProducts(shopId)
  const product = products.find((p) => p.id === productId)
  const [moving, setMoving] = useState<'purchase' | 'adjustment' | null>(null)
  const [tab, setTab] = useState<'history' | 'details' | 'suppliers' | 'photo'>('history')
  // Which coloured job is lit: Goods in until another is pressed.
  const [act, setAct] = useState<'in' | 'out' | 'edit' | 'order'>('in')

  if (!product) return <p className="p-4 text-muted">…</p>

  const icon = (d: string) => <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d={d} /></svg>
  const ACTIONS = {
    in: { on: 'bg-[#0b5e4f] text-white shadow-lg shadow-[#0b5e4f]/30', off: 'bg-[#e3f2ee] text-[#0b5e4f] hover:bg-[#d2ebe4]' },
    out: { on: 'bg-[#be123c] text-white shadow-lg shadow-[#be123c]/30', off: 'bg-[#fde8ec] text-[#be123c] hover:bg-[#fbd5dd]' },
    edit: { on: 'bg-[#1d4ed8] text-white shadow-lg shadow-[#1d4ed8]/30', off: 'bg-[#e8effd] text-[#1d4ed8] hover:bg-[#dbe5fc]' },
    order: { on: 'bg-[#a1460a] text-white shadow-lg shadow-[#a1460a]/30', off: 'bg-[#fdf1e3] text-[#a1460a] hover:bg-[#fbe6cc]' },
  } as const
  type Act = keyof typeof ACTIONS
  const action = (key: Act) => `flex min-h-[52px] cursor-pointer items-center justify-center gap-2.5 rounded-2xl px-3 text-sm font-bold whitespace-nowrap transition active:scale-[0.98] ${act === key ? ACTIONS[key].on : ACTIONS[key].off}`
  const dot = (key: Act, d: string) => <span aria-hidden className={`flex h-[30px] w-[30px] shrink-0 items-center justify-center rounded-full ${act === key ? 'bg-white/20' : 'bg-white'}`}>{icon(d)}</span>
  const photo = imageUrl(shopId, product.id, product.imageAt)
  const tabs = [
    ...(!product.pending ? [{ key: 'history', label: t('item3.history') }] : []),
    { key: 'details', label: t('item3.details') },
    ...(!product.pending && !product.isService ? [{ key: 'suppliers', label: t('item3.suppliers') }] : []),
    { key: 'photo', label: t('item3.photo') },
  ] as { key: 'history' | 'details' | 'suppliers' | 'photo'; label: string }[]
  const shown = tabs.some((x) => x.key === tab) ? tab : tabs[0].key

  return (
    <div className="flex flex-col gap-3 p-4">
      {/* What it is, how much is on the shelf, and the jobs. Each opens on its own instead of every card stacked on the page. */}
      <section className="flex flex-col gap-4 rounded-card bg-white p-4 shadow-sm">
        <div className="flex items-start gap-3">
          {photo
            ? <img src={photo} alt="" className="h-16 w-16 shrink-0 rounded-2xl border border-line object-cover" />
            : <span aria-hidden className="flex h-16 w-16 shrink-0 items-center justify-center rounded-2xl bg-brand-soft text-xl font-extrabold text-brand">{itemName(product).split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0]!.toUpperCase()).join('')}</span>}
          <span className="flex min-w-0 flex-1 flex-col gap-1">
            <span className="text-2xl leading-tight font-extrabold">{itemName(product)}</span>
            <span className="text-sm text-muted">
              {product.sellingPrice > 0 ? `${formatRupees(product.sellingPrice)}/${t(`units.${product.unit}`)}` : t('stock.noPrice')}
              {product.category && ` · ${product.category}`}
            </span>
            {(product.dailySales ?? 0) > 0 && !product.isService && product.stock > 0 && (() => {
              const days = Math.floor(product.stock / product.dailySales!)
              return <span className={`w-fit rounded-full px-2.5 py-0.5 text-xs font-semibold ${days <= 3 ? 'bg-due-soft text-due' : days <= 10 ? 'bg-warn-soft text-warn' : 'bg-emerald-50 text-emerald-800'}`}>
                {coverText(t, product.stock / product.dailySales!)} · {t('sl.perDay', { qty: Math.round(product.dailySales! * 10) / 10, unit: t(`units.${product.unit}`, { count: Math.round(product.dailySales! * 10) / 10 }) })}
              </span>
            })()}
          </span>
          <span className={`flex shrink-0 flex-col items-end gap-1 rounded-2xl px-3 py-2 ${product.isService ? '' : product.stock <= 0 ? 'bg-due-soft' : product.isLow ? 'bg-warn-soft' : 'bg-[#edf2f1]'}`}>
            <StockBadge product={product} large />
            {(product.promised ?? 0) > 0 && (
              <button onClick={() => navigate('/holds')} className="text-right text-xs font-semibold text-warn underline">
                {t('saleOrder.promised', { qty: product.promised })} · {(() => { const free = Math.round((product.stock - (product.reserved ?? 0) - product.promised!) * 1000) / 1000; return free >= 0 ? t('saleOrder.free', { qty: free }) : <span className="text-due">{t('saleOrder.short', { qty: -free })}</span> })()}
              </button>
            )}
          </span>
        </div>
        {/* Medicines sold loose: stock is in strips, so 9.5 strips of 10 reads as 9 strips and 5 tablets, as a chemist counts it. */}
        {product.unitsPerPack && product.stock > 0 && (() => {
          const tablets = Math.round(product.stock * product.unitsPerPack)
          return <p className="rounded-xl bg-[#f6faf9] px-3 py-2 font-semibold">{t('loose.inHand', { strips: Math.floor(tablets / product.unitsPerPack), tablets: tablets % product.unitsPerPack })}</p>
        })()}
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          {!product.isService && <button onClick={() => { setAct('in'); setMoving('purchase') }} aria-pressed={act === 'in'} className={action('in')}>{dot('in', 'M12 5v14M5 12h14')}{t('stock.goodsIn')}</button>}
          {!product.isService && <button onClick={() => { setAct('out'); setMoving('adjustment') }} aria-pressed={act === 'out'} className={action('out')}>{dot('out', 'M5 12h14')}{t('stock.reduce')}</button>}
          <button onClick={() => { setAct('edit'); setTab('details') }} aria-pressed={act === 'edit'} className={action('edit')}>{dot('edit', 'M4 20h4L19 9l-4-4L4 16zM14 6l4 4')}{t('item3.edit')}</button>
          {!product.isService && <button onClick={() => { setAct('order'); navigate('/reorder') }} className={action('order')}>{dot('order', 'M3 4h2l2.5 11h11L21 7H6M9 20h.01M18 20h.01')}{t('item3.order')}</button>}
        </div>
      </section>

      {product.bagSize ? <BagCard shopId={shopId} product={product} /> : null}

      <div role="tablist" aria-label={itemName(product)} className="-mx-4 flex gap-1 overflow-x-auto border-b border-line px-4 [scrollbar-width:none]">
        {tabs.map((x) => (
          <button key={x.key} role="tab" aria-selected={shown === x.key} onClick={() => setTab(x.key)}
            className={`min-h-12 shrink-0 border-b-[3px] px-3 text-sm whitespace-nowrap ${shown === x.key ? 'border-brand font-bold text-brand' : 'border-transparent font-semibold text-muted hover:text-ink'}`}>{x.label}</button>
        ))}
      </div>

      {shown === 'history' && <Suspense fallback={null}><ItemLedger productId={product.id} /></Suspense>}
      {/* Keyed by the saved values, so the form shows a new cost price once a purchase syncs. */}
      {shown === 'details' && (
        <DetailsForm
          key={`${product.id}:${product.name}:${product.sellingPrice}:${product.costPrice}:${product.reorderLevel}:${product.category}`}
          shopId={shopId}
          product={product}
        />
      )}
      {shown === 'suppliers' && <ItemSuppliersCard shopId={shopId} productId={product.id} unit={product.unit} />}
      {shown === 'photo' && <PhotoCard shopId={shopId} product={product} />}

      {moving && <MoveSheet shopId={shopId} product={product} kind={moving} onClose={() => { setMoving(null); setAct('in') }} />}
    </div>
  )
}

function MoveSheet({ shopId, product, kind, onClose }: { shopId: string; product: Product; kind: 'purchase' | 'adjustment'; onClose: () => void }) {
  const { t } = useTranslation()
  const [quantity, setQuantity] = useState('')
  const [cost, setCost] = useState('')
  const [reason, setReason] = useState<StockReason>('damage')
  const [note, setNote] = useState('')
  const [bags, setBags] = useState('')
  const [supplierId, setSupplierId] = useState('')
  const [error, setError] = useState('')
  const queryClient = useQueryClient()
  // Stock coming in can be put on a supplier: it is then their goods received today, waiting for their bill. Helpers
  // can't see suppliers (owner only), so for them the list is empty and stock goes in by hand as before.
  const suppliers = useQuery({ queryKey: [shopId, 'suppliers', 'pick'], queryFn: () => api<{ id: string; name: string; isActive?: boolean }[]>('/v1/suppliers'), enabled: kind === 'purchase', retry: false }).data ?? []
  const value = Number(quantity)
  const unitName = t(`units.${product.unit}`)
  const avg = Number(bags) > 0 && value > 0 ? value / Number(bags) : null

  async function save() {
    if (kind === 'purchase' && supplierId) {
      setError('')
      try {
        await api('/v1/goods-receipts', 'POST', { id: crypto.randomUUID(), supplierId, note: note.trim() || null,
          lines: [{ productId: product.id, quantity: value, bags: Number(bags) > 0 ? Number(bags) : null }] })
        await queryClient.invalidateQueries({ queryKey: [shopId] })
        onClose()
      } catch (e) {
        setError(e instanceof Error ? e.message : t('error'))
      }
      return
    }
    await addStockMovement(
      shopId,
      product,
      kind,
      kind === 'purchase' ? value : -value,
      kind === 'adjustment' ? reason : null,
      kind === 'purchase' && cost ? Number(cost) : null,
      note.trim() || null,
      Number(bags) > 0 ? (kind === 'purchase' ? Number(bags) : -Number(bags)) : null,
    )
    onClose()
  }

  return (
    <Drawer title={`${kind === 'purchase' ? t('stock.goodsIn') : t('stock.reduce')} · ${itemName(product)}`} onClose={onClose}>
      <div className="space-y-3">
        <Field
          label={`${kind === 'purchase' ? t('stock.quantityIn') : t('stock.quantityOut')} (${t(`units.${product.unit}`)})`}
          value={quantity}
          onChange={(e) => setQuantity(e.target.value)}
          inputMode="decimal"
          autoFocus
        />
        {product.bagSize ? (
          <div className="flex flex-col gap-1">
            <Field label={t('bag.bags')} value={bags} onChange={(e) => { setBags(e.target.value); if (!quantity && Number(e.target.value) > 0) setQuantity(String(Number(e.target.value) * product.bagSize!)) }} inputMode="numeric" />
            {avg !== null && <BagCheck avg={avg} size={product.bagSize} bags={Number(bags)} unit={unitName} />}
          </div>
        ) : null}
        {kind === 'purchase' && (
          <>
            <p className="rounded-xl bg-white px-3 py-2 text-sm"><span className="text-muted">{t('stock.inCategory')}: </span><b>{product.category || t('stock.noCategory')}</b></p>
            {suppliers.length > 0 && (
              <label className="block">
                <span className="mb-1 block text-sm font-semibold text-muted">{t('stock.fromSupplier')}</span>
                <Select value={supplierId} onChange={(e) => setSupplierId(e.target.value)} className="w-full text-base">
                  <option value="">{t('stock.noSupplier')}</option>
                  {suppliers.filter((x) => x.isActive !== false).map((x) => <option key={x.id} value={x.id}>{x.name}</option>)}
                </Select>
                <span className="mt-1 block text-xs text-muted">{t(supplierId ? 'stock.supplierHint' : 'stock.byHandHint')}</span>
              </label>
            )}
            {!supplierId && <Field label={`${t('stock.costPrice')} / ${t(`units.${product.unit}`)}`} value={cost} onChange={(e) => setCost(e.target.value)} inputMode="decimal" />}
          </>
        )}
        {kind === 'adjustment' && (
          <div>
            <span className="mb-1 block text-sm font-semibold text-muted">{t('stock.reason')}</span>
            <div className="flex flex-wrap gap-2">
              {REASONS.map((r) => (
                <button key={r} onClick={() => setReason(r)} aria-pressed={reason === r}
                  className={`h-11 rounded-full px-4 font-semibold ${reason === r ? 'bg-brand text-white shadow-sm' : 'border border-line bg-white'}`}>
                  {t(`stock.reasons.${r}`)}
                </button>
              ))}
            </div>
          </div>
        )}
        <Field label={t('note')} value={note} onChange={(e) => setNote(e.target.value)} maxLength={200} />
        {error && <p className="text-sm text-due">{error}</p>}
        <Button tone={kind === 'purchase' ? 'payment' : 'credit'} className="w-full" disabled={!(value > 0)} onClick={save}>
          {t('save')}
        </Button>
      </div>
    </Drawer>
  )
}

/** Name, price, category, reorder level. Saved online; stock never changes here. */
function DetailsForm({ shopId, product }: { shopId: string; product: Product }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  async function setOutToday(value: boolean) {
    await api(`/v1/products/${product.id}/out-today`, 'PUT', { out: value })
    await queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
  }

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
    uncounted: !!product.uncounted,
    bagSize: product.bagSize ? String(product.bagSize) : '',
    purchaseUnit: product.purchaseUnit ?? 'bag',
    weightKg: product.weightKg ? String(product.weightKg) : '',
    wholesale: product.wholesalePrice ? String(product.wholesalePrice) : '',
    wholesaleMin: product.wholesaleMinQty ? String(product.wholesaleMinQty) : '',
  })
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
    uncounted: form.uncounted,
    bagSize: Number(form.bagSize) || 0,
    purchaseUnit: form.purchaseUnit,
    weightKg: Number(form.weightKg) || 0,
    // 0 clears the wholesale price.
    wholesalePrice: Number(form.wholesale) || 0,
    wholesaleMinQty: Number(form.wholesale) ? Number(form.wholesaleMin) || null : null,
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
    <section className="flex flex-col gap-3 rounded-card bg-white p-4">
      <Field label={t('itemName')} value={form.name} onChange={set('name')} />
      <div className="grid grid-cols-2 gap-2">
        <UnitSelect value={form.unit} onChange={(unit) => setForm({ ...form, unit })} />
        <Field label={t('stock.sellingPrice')} value={form.price} onChange={set('price')} inputMode="decimal" />
      </div>
      {!product.isService && <Field label={t('stock.mrp')} value={form.mrp} onChange={set('mrp')} inputMode="decimal" placeholder={t('stock.mrpHint')} />}
      {/* Wholesale: a lower price per unit from a quantity, applied on the bill by itself. */}
      <div>
        <div className="grid grid-cols-2 gap-2">
          <Field label={t('wholesale.price')} value={form.wholesale} onChange={set('wholesale')} inputMode="decimal" placeholder={t('wholesale.pricePlaceholder')} />
          <Field label={t('wholesale.minQty', { unit: t(`units.${form.unit}`) })} value={form.wholesaleMin} onChange={set('wholesaleMin')} inputMode="decimal" placeholder="12" />
        </div>
        <p className="mt-1 text-sm text-muted">{t('wholesale.hint')}</p>
      </div>
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
        <button type="button" onClick={() => void setOutToday(!isOutToday(product.outOn))} className={`min-h-11 self-start px-3 rounded-xl ${btn.outline}`}>
          {isOutToday(product.outOn) ? t('stock.backOnSale') : t('stock.markOutToday')}
        </button>
      )}
      {!product.isService && (
        <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={form.uncounted} onChange={(e) => setForm({ ...form, uncounted: e.target.checked })} className="h-5 w-5" />{t('stock.dontCount')}</label>
      )}
      {!product.isService && (
        <button type="button" onClick={() => void addVariant()} className={`min-h-11 self-start px-3 rounded-xl ${btn.outline}`}>{t('styles.addVariant')}</button>
      )}
      <div className="grid grid-cols-2 gap-2">
        <Field label={t('stock.buyingCostShort')} value={form.cost} onChange={set('cost')} inputMode="decimal" />
        <Field label={t('stock.reorderLevel')} value={form.reorder} onChange={set('reorder')} inputMode="decimal" />
      </div>
      <CategoryInput shopId={shopId} value={form.category} onChange={(category) => setForm((f) => ({ ...f, category }))} />
      {!product.isService && (
        <label className="block">
          <span className="mb-1 block text-sm font-semibold text-muted">🛍️ {t('bag.sizeLabel')}</span>
          <select aria-label={t('bag.buyIn')} value={form.purchaseUnit} onChange={set('purchaseUnit')} className="mr-2 h-12 rounded-2xl border-[1.5px] border-line bg-white px-2">
            {(['bag', 'carton', 'box', 'strip', 'bottle', 'packet', 'jar', 'set', 'dozen', 'quintal'] as const).map((u) => <option key={u} value={u}>{t(`units.${u}`)}</option>)}
          </select>
          <input value={form.bagSize} onChange={set('bagSize')} inputMode="decimal" placeholder="50" className="h-12 w-32 rounded-2xl border-[1.5px] border-line bg-white px-3 text-lg outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /> {t(`units.${form.unit}`)}
          <span className="mt-1 block text-xs text-muted">{t('bag.sizeHint', { unit: t(`units.${form.unit}`) })}</span>
        </label>
      )}
      {!product.isService && !['kg', 'g', 'tonne', 'quintal'].includes(form.unit) && (
        <label className="block">
          <span className="mb-1 block text-sm font-semibold text-muted">⚖️ {t('weigh.label', { unit: t(`units.${form.unit}`) })}</span>
          <input value={form.weightKg} onChange={set('weightKg')} inputMode="decimal" placeholder="0.5" className="h-12 w-32 rounded-2xl border-[1.5px] border-line bg-white px-3 text-lg outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /> kg
          <span className="mt-1 block text-xs text-muted">{t('weigh.hint')}</span>
        </label>
      )}
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
      <div className="grid grid-cols-2 gap-2">
        <label className="flex flex-col gap-1">
          <span className="text-sm font-medium text-muted">{t('stock.gst')}</span>
          <select value={form.gst} onChange={set('gst')} className="h-12 rounded-xl border border-line bg-white px-3 text-lg">
            <option value="">{t('stock.gstNotSet')}</option>
            {[...new Set([...GST_RATES, ...(form.gst ? [form.gst] : [])])].sort((a, b) => Number(a) - Number(b)).map((rate) => <option key={rate} value={rate}>{rate}%</option>)}
          </select>
        </label>
        <Field label={t('stock.hsn')} value={form.hsn} onChange={set('hsn')} inputMode="numeric" maxLength={8} />
      </div>
      <Button onClick={save} disabled={!form.name.trim() || product.pending}>{t('stock.saveDetails')}</Button>
      {message && <p className="text-sm text-muted">{message}</p>}
      <button type="button" onClick={() => void remove()} disabled={product.pending} className={`min-h-11 text-sm ${btn.dangerOutline}`}>{t('stock.remove')}</button>
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
  const [viewing, setViewing] = useState<number | null>(null)
  const url = imageUrl(shopId, product.id, product.imageAt)
  const photos = allPhotos(shopId, product)
  const extras = product.extraPhotos ?? []
  const room = MAX_EXTRA_PHOTOS - extras.length

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
  // Several at once, as many as there is room for; each is shrunk on the phone first.
  const addMore = (files: File[]) => run(async () => {
    for (const file of files.slice(0, room)) await api(`/v1/products/${product.id}/photos`, 'POST', { id: crypto.randomUUID(), ...(await itemPhotos(file)) })
  })

  return (
    <section className="flex items-center gap-3 rounded-card bg-white p-3">
      <div className="w-24 shrink-0">
        <ItemCard item={product} title="" compact imageUrl={url} onClick={photos.length > 0 ? () => setViewing(0) : undefined} />
        {photos.length > 0 && <p className="mt-1 text-center text-[11px] text-muted">{photos.length > 1 ? t('gallery.count', { count: photos.length }) : t('photo.tapToView')}</p>}
      </div>
      {viewing !== null && photos.length > 0 && <PhotoGallery title={product.name} photos={photos} start={viewing} onClose={() => setViewing(null)} />}
      <div className="flex flex-1 flex-col gap-2">
        <span className="text-sm text-muted">{url ? t('photo.has') : t('photo.none')}</span>
        <label className={`flex min-h-11 cursor-pointer items-center justify-center rounded-xl bg-brand px-3 font-semibold text-white ${busy ? 'opacity-50' : ''}`}>
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
        {url && (
          <div className="flex flex-col gap-1.5">
            <span className="text-xs font-semibold text-muted">{t('photo.more', { count: extras.length, max: MAX_EXTRA_PHOTOS })}</span>
            <div className="flex flex-wrap gap-2">
              {extras.map((id, i) => (
                <span key={id} className="relative">
                  <button onClick={() => setViewing(i + 1)} aria-label={t('gallery.photo', { n: i + 2 })} className="h-14 w-14 overflow-hidden rounded-lg border border-line bg-white">
                    <img src={extraPhotoUrl(shopId, product.id, id, 'thumb')} alt="" className="h-full w-full object-contain" />
                  </button>
                  <button disabled={busy} onClick={() => void run(() => request(`/v1/products/${product.id}/photos/${id}`, 'DELETE'))} aria-label={t('photo.removeOne', { n: i + 2 })}
                    className="absolute -right-1.5 -top-1.5 h-6 w-6 rounded-full bg-due text-xs font-bold text-white shadow">✕</button>
                </span>
              ))}
              {room > 0 && (
                <label className={`flex h-14 w-14 cursor-pointer items-center justify-center rounded-lg border-2 border-dashed border-brand-line text-2xl font-bold text-brand ${busy ? 'opacity-50' : ''}`} aria-label={t('photo.addMore')}>
                  +
                  <input type="file" accept="image/*" multiple disabled={busy} className="sr-only"
                    onChange={(e) => { const files = [...(e.target.files ?? [])]; if (files.length) void addMore(files); e.target.value = '' }} />
                </label>
              )}
            </div>
          </div>
        )}
        {(product.photoReports ?? 0) > 0 && (
          <div className="rounded-xl bg-warn-soft p-2 text-sm text-warn-ink">
            ⚑ {t('photo.reported', { count: product.photoReports })}{product.photoReportNote ? `: "${product.photoReportNote}"` : ''}
            <button disabled={busy} onClick={() => void run(() => request(`/v1/products/${product.id}/image/reports`, 'DELETE'))} className="ml-2 font-semibold underline">{t('photo.keep')}</button>
          </div>
        )}
        <div className="grid grid-cols-[1fr_auto] gap-2">
          <input value={barcode} onChange={(e) => setBarcode(e.target.value.replace(/\D/g, ''))} inputMode="numeric" maxLength={14} placeholder={t('photo.barcode')}
            aria-label={t('photo.barcode')} className="h-11 min-w-0 rounded-xl border border-line px-3" />
          <button disabled={busy || barcode.length < 8} onClick={() => void run(() => api(`/v1/products/${product.id}/image/barcode`, 'POST', { barcode }))}
            className="h-11 rounded-xl border-2 border-brand px-3 text-sm font-semibold text-brand-strong disabled:opacity-40">{t('photo.findByBarcode')}</button>
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
  const [scanning, setScanning] = useState(false)
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
      <div className={`grid gap-2 ${cameraPossible() ? 'grid-cols-[1fr_auto_auto]' : 'grid-cols-[1fr_auto]'}`}>
        <input value={code} onChange={(e) => setCode(e.target.value)} inputMode="numeric" maxLength={30} aria-label={t('barcodeBill.label')} className="h-11 min-w-0 rounded-xl border border-line px-3" />
        {cameraPossible() && <button type="button" onClick={() => setScanning(true)} className={`h-11 px-3 rounded-xl ${btn.outline}`}>{t('scan.scanIt')}</button>}
        <button type="button" onClick={() => void save(`/v1/products/${product.id}/barcode`, 'PUT', { barcode: code })} className="h-11 rounded-xl border border-line px-3 font-semibold">{t('save')}</button>
      </div>
      <div className="grid grid-cols-2 gap-2">
        {!product.barcode && <button type="button" onClick={() => void save(`/v1/products/${product.id}/barcode/new`, 'POST', {})} className={`min-h-11 px-2 text-sm rounded-xl ${btn.outline}`}>{t('barcodeBill.make')}</button>}
        <button type="button" onClick={() => navigate(`/tags/${product.id}`)} className="min-h-11 rounded-xl border border-line px-2 text-sm font-semibold">{t('tags.open')}</button>
      </div>
      {message && <p className="text-xs text-muted">{message}</p>}
      {/* Read the code off the pack and save it straight away. */}
      {scanning && (
        <Suspense fallback={null}>
          <ScanSheet onCode={(read) => { setScanning(false); setCode(read); void save(`/v1/products/${product.id}/barcode`, 'PUT', { barcode: read }) }} onClose={() => setScanning(false)} />
        </Suspense>
      )}
    </div>
  )
}

/** Average weight a bag against its size: short bags are a supplier or handling question. */
export function BagCheck({ avg, size, bags, unit }: { avg: number; size: number; bags: number; unit: string }) {
  const { t } = useTranslation()
  const diff = Math.round((avg - size) * 100) / 100
  const total = Math.round((size - avg) * bags * 100) / 100
  const tone = Math.abs(diff) < size * 0.005 ? 'bg-emerald-50 text-emerald-800' : diff < 0 ? 'bg-due-soft text-red-800' : 'bg-warn-soft text-warn'
  return (
    <p className={`rounded-xl px-3 py-2 text-sm font-semibold ${tone}`}>
      ⚖ {t('bag.perBag', { avg: Math.round(avg * 100) / 100, unit })}
      {diff < 0 && ` · ${t('bag.short', { diff: -diff, unit, total })}`}
      {diff > 0 && ` · ${t('bag.over', { diff, unit })}`}
    </p>
  )
}

/** Sealed bags and loose weight, and opening a bag to sell loose. */
function BagCard({ shopId, product }: { shopId: string; product: Product }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const bags = product.bags ?? 0
  const loose = Math.round((product.stock - bags * product.bagSize!) * 1000) / 1000
  const average = bags > 0 ? Math.round((product.stock / bags) * 100) / 100 : null
  const unit = t(`units.${product.unit}`)
  async function open() {
    const n = Number(await ask(t('bag.openAsk'), '1'))
    if (!(n > 0)) return
    try {
      await api(`/v1/products/${product.id}/open-bag`, 'POST', { id: crypto.randomUUID(), bags: n })
      await queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
      void queryClient.invalidateQueries({ queryKey: ['item-ledger', product.id] })
    } catch (e) {
      void tell(e instanceof Error ? e.message : t('error'))
    }
  }
  return (
    <section className="flex flex-wrap items-center gap-3 rounded-card bg-white p-4 shadow-sm">
      <span aria-hidden className="flex h-12 w-12 items-center justify-center rounded-2xl bg-warn-soft text-2xl">🛍️</span>
      <span className="flex min-w-0 flex-1 flex-col">
        <b className="text-lg">{t('bag.sealed', { count: bags })}</b>
        <span className="text-sm text-muted">{t('bag.bagsOf', { size: product.bagSize, unit })} · {loose >= 0 ? t('bag.loose', { qty: loose, unit }) : t('bag.perBag', { avg: average, unit })}</span>
      </span>
      <button onClick={() => void open()} disabled={bags <= 0} className="h-11 rounded-xl border-2 border-brand px-4 font-bold text-brand-strong hover:bg-brand-soft disabled:opacity-40">✂ {t('bag.open')}</button>
    </section>
  )
}

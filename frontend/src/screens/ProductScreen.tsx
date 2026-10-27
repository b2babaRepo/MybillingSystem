import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field, Sheet } from '../components/ui'
import { api } from '../lib/api'
import { addStockMovement, useProducts } from '../lib/data'
import { formatDateTime, formatRupees } from '../lib/format'
import type { Product, StockMovement, StockReason, Unit } from '../lib/types'
import { UnitSelect } from './BillScreen'
import { StockBadge } from './ItemsScreen'

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
  const [form, setForm] = useState({
    name: product.name,
    unit: product.unit as Unit,
    price: product.sellingPrice ? String(product.sellingPrice) : '',
    cost: product.costPrice === null ? '' : String(product.costPrice),
    category: product.category ?? '',
    reorder: product.reorderLevel === null ? '' : String(product.reorderLevel),
  })
  const [message, setMessage] = useState('')
  const set = (key: keyof typeof form) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value })

  async function save() {
    setMessage('')
    try {
      await api(`/v1/products/${product.id}`, 'PUT', {
        name: form.name.trim(),
        unit: form.unit,
        sellingPrice: Number(form.price) || 0,
        costPrice: form.cost ? Number(form.cost) : null,
        category: form.category.trim() || null,
        reorderLevel: form.reorder ? Number(form.reorder) : null,
      })
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
        <Field label={t('price')} value={form.price} onChange={set('price')} inputMode="decimal" />
      </div>
      <div className="grid grid-cols-2 gap-2">
        <Field label={t('stock.costPrice')} value={form.cost} onChange={set('cost')} inputMode="decimal" />
        <Field label={t('stock.reorderLevel')} value={form.reorder} onChange={set('reorder')} inputMode="decimal" />
      </div>
      <Field label={t('stock.category')} value={form.category} onChange={set('category')} />
      <Button tone="plain" onClick={save} disabled={!form.name.trim() || product.pending}>{t('stock.saveDetails')}</Button>
      {message && <p className="text-sm text-muted">{message}</p>}
    </section>
  )
}

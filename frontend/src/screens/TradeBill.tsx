import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field, Sheet } from '../components/ui'
import { api } from '../lib/api'
import { uuidv7 } from '../lib/ids'
import type { CartLine } from '../lib/data'
import { formatRupees } from '../lib/format'
import { round2 } from '../lib/local'
import type { ShopType } from '../lib/reports'
import type { Customer, Product } from '../lib/types'

/** What other trades add to a bill: where it went, which vehicle took it, which painter brought the customer. */
export interface TradeExtras {
  site: string
  vehicleNumber: string
  painterId: string
}

/** Goods over this, moved by road, need an e-way bill (GST rule for inter- and most intra-state movement). */
const EWAY_LIMIT = 50_000

interface Painter { id: string; name: string; phone: string | null; commissionPercent: number }

/**
 * The bill's extra panel for non-kirana shops (loaded only for them). Building material: the site (earlier sites of
 * this customer offered), vehicle number, an e-way bill reminder over ₹50,000, the credit-limit warning, and "Save as
 * delivery challan". Paint: the painter, and a tinted line (base tin + shade + tinting charge).
 */
export function TradeBillPanel({ shopId, shopType, customer, total, onKhata, cart, products, extras, onExtras, onAddLine, onChallanSaved }: {
  shopId: string
  shopType: ShopType
  customer: Customer | null
  total: number
  onKhata: number
  cart: CartLine[]
  products: Product[]
  extras: TradeExtras
  onExtras: (extras: TradeExtras) => void
  onAddLine: (line: CartLine) => void
  onChallanSaved: (total: number) => void
}) {
  const { t } = useTranslation()
  const building = shopType === 'building_materials' || shopType === 'hardware' || shopType === 'electrical' || shopType === 'woodwork'
  const paint = shopType === 'paint'
  // Painters bring paint customers, electricians bring electrical ones: the same commission.
  const fitters = paint || shopType === 'electrical' || shopType === 'woodwork'
  const sites = useQuery({
    queryKey: [shopId, 'sites', customer?.id],
    queryFn: () => api<{ site: string }[]>(`/v1/customers/${customer!.id}/sites`),
    enabled: building && !!customer,
  }).data
  const painters = useQuery({ queryKey: [shopId, 'painters'], queryFn: () => api<Painter[]>('/v1/painters'), enabled: fitters }).data
  const [tinting, setTinting] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const overLimit = customer?.creditLimit ? customer.balance + onKhata > customer.creditLimit : false

  async function saveChallan() {
    setBusy(true)
    setError(null)
    try {
      await api('/v1/challans', 'POST', {
        id: uuidv7(),
        customerId: customer!.id,
        lines: cart.map((l) => ({ productId: l.productId, name: l.name, quantity: l.quantity, unit: l.unit, unitPrice: l.unitPrice })),
        site: extras.site.trim() || null,
        vehicleNumber: extras.vehicleNumber.trim() || null,
      })
      onChallanSaved(total)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex flex-col gap-2">
      {overLimit && (
        <p role="alert" className="rounded-xl bg-red-50 p-3 text-sm font-semibold text-red-800">
          {t('trade.overLimit', { limit: formatRupees(customer!.creditLimit!), after: formatRupees(round2(customer!.balance + onKhata)) })}
        </p>
      )}

      {building && (
        <>
          <Field label={t('trade.site')} value={extras.site} onChange={(e) => onExtras({ ...extras, site: e.target.value })} list="bill-sites" maxLength={80}
            placeholder={t('trade.sitePlaceholder')} />
          <datalist id="bill-sites">{sites?.filter((s) => s.site).map((s) => <option key={s.site} value={s.site} />)}</datalist>
          <Field label={t('trade.vehicle')} value={extras.vehicleNumber} onChange={(e) => onExtras({ ...extras, vehicleNumber: e.target.value })} maxLength={15}
            placeholder="RJ14 GB 1234" autoCapitalize="characters" />
          {total > EWAY_LIMIT && (
            <p className="rounded-xl bg-amber-50 p-3 text-sm text-amber-900">{t('trade.eway', { amount: formatRupees(EWAY_LIMIT) })}</p>
          )}
          {customer && (
            <button disabled={busy || cart.length === 0} onClick={() => void saveChallan()}
              className="min-h-12 rounded-2xl border-2 border-teal-700 font-bold text-teal-800 disabled:opacity-40">
              {t('trade.saveChallan')}
            </button>
          )}
          {!customer && <p className="text-xs text-muted">{t('trade.challanNeedsCustomer')}</p>}
        </>
      )}

      {fitters && (
        <>
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-muted">{t('trade.painter')}</span>
            <select value={extras.painterId} onChange={(e) => onExtras({ ...extras, painterId: e.target.value })}
              className="h-12 w-full rounded-xl border border-line bg-white px-3 text-lg">
              <option value="">{t('trade.noPainter')}</option>
              {painters?.map((p) => <option key={p.id} value={p.id}>{p.name} ({p.commissionPercent}%)</option>)}
            </select>
          </label>
          {paint && <button onClick={() => setTinting(true)} className="min-h-12 rounded-2xl border-2 border-dashed border-udhaar font-bold text-udhaar">
            🎨 {t('trade.addTinted')}
          </button>}
        </>
      )}

      {error && <p className="text-sm text-due">{error}</p>}
      {tinting && <TintSheet products={products} onClose={() => setTinting(false)} onAdd={onAddLine} />}
    </div>
  )
}

/**
 * A tinted tin: pick the base tin (it comes off stock), type the shade code from the tinting machine and the tinting
 * charge; the line's rate is base price + tinting.
 */
function TintSheet({ products, onClose, onAdd }: { products: Product[]; onClose: () => void; onAdd: (line: CartLine) => void }) {
  const { t } = useTranslation()
  const [baseId, setBaseId] = useState('')
  const [shade, setShade] = useState('')
  const [tint, setTint] = useState('')
  const [quantity, setQuantity] = useState('1')
  const base = products.find((p) => p.id === baseId)
  const rate = round2((base?.sellingPrice ?? 0) + (Number(tint) || 0))
  const valid = base && shade.trim() && rate > 0 && Number(quantity) > 0

  return (
    <Sheet title={t('trade.addTinted')} onClose={onClose}>
      <div className="space-y-3">
        <label className="block">
          <span className="mb-1 block text-sm font-medium text-muted">{t('trade.base')}</span>
          <select value={baseId} onChange={(e) => setBaseId(e.target.value)} className="h-12 w-full rounded-xl border border-line bg-white px-3 text-lg">
            <option value="">—</option>
            {products.map((p) => <option key={p.id} value={p.id}>{p.name} · {formatRupees(p.sellingPrice)}</option>)}
          </select>
        </label>
        <div className="grid grid-cols-3 gap-2">
          <Field label={t('trade.shade')} value={shade} onChange={(e) => setShade(e.target.value)} placeholder="7B23" maxLength={20} />
          <Field label={t('trade.tintCharge')} value={tint} onChange={(e) => setTint(e.target.value)} inputMode="decimal" />
          <Field label={t('quantity')} value={quantity} onChange={(e) => setQuantity(e.target.value)} inputMode="decimal" />
        </div>
        {base && <p className="text-sm text-muted">{t('trade.tintRate', { rate: formatRupees(rate) })}</p>}
        <Button className="w-full" disabled={!valid} onClick={() => {
          onAdd({ productId: base!.id, name: `${base!.name} · ${t('trade.shade')} ${shade.trim()}`.slice(0, 120), quantity: Number(quantity), unit: base!.unit, unitPrice: rate })
          onClose()
        }}>
          {t('addItem')}
        </Button>
      </div>
    </Sheet>
  )
}

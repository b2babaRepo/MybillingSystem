import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field, Sheet } from '../components/ui'
import { btn } from '../lib/buttonStyles'
import { api } from '../lib/api'
import { uuidv7 } from '../lib/ids'
import type { CartLine } from '../lib/data'
import { formatRupees } from '../lib/format'
import { round2 } from '../lib/local'
import { useSubscription } from '../lib/billing'
import type { ShopType } from '../lib/reports'
import type { Customer, Product } from '../lib/types'
import { shownError } from '../lib/notify'

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
 * The delivery part of a bill for shops that send goods to a site (loaded only for them, once delivery is opened): the
 * site (earlier sites of this customer offered), vehicle number, an e-way bill reminder and "Save as delivery challan".
 */
export function TradeDelivery({ shopId, customer, total, cart, extras, onExtras, onChallanSaved, placeOfSupply = null }: {
  shopId: string
  customer: Customer | null
  total: number
  cart: CartLine[]
  extras: TradeExtras
  onExtras: (extras: TradeExtras) => void
  onChallanSaved: () => void
  /** The state the goods go to, when it isn't the shop's own: the challan keeps it for the invoice. */
  placeOfSupply?: string | null
}) {
  const { t } = useTranslation()
  const sites = useQuery({
    queryKey: [shopId, 'sites', customer?.id],
    queryFn: () => api<{ site: string }[]>(`/v1/customers/${customer!.id}/sites`),
    enabled: !!customer,
  }).data
  // Challans are a trade feature: true until the plan is known (offline), and the server has the last word.
  const features = useSubscription(shopId).data?.features
  const canChallan = !features || features.includes('trade')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

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
        placeOfSupply,
      })
      onChallanSaved()
    } catch (e) {
      setError(shownError(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <Field label={t('trade.site')} value={extras.site} onChange={(e) => onExtras({ ...extras, site: e.target.value })} list="bill-sites" maxLength={80}
        placeholder={t('trade.sitePlaceholder')} />
      <datalist id="bill-sites">{sites?.filter((s) => s.site).map((s) => <option key={s.site} value={s.site} />)}</datalist>
      <Field label={t('trade.vehicle')} value={extras.vehicleNumber} onChange={(e) => onExtras({ ...extras, vehicleNumber: e.target.value })} maxLength={15}
        placeholder="RJ14 GB 1234" autoCapitalize="characters" />
      {total > EWAY_LIMIT && (
        <p className="rounded-xl bg-warn-soft p-3 text-sm text-warn-ink">{t('trade.eway', { amount: formatRupees(EWAY_LIMIT) })}</p>
      )}
      {customer && canChallan && (
        <button disabled={busy || cart.length === 0} onClick={() => void saveChallan()}
          className={`min-h-12 rounded-xl ${btn.outline}`}>
          {t('trade.saveChallan')}
        </button>
      )}
      {!customer && canChallan && <p className="text-xs text-muted">{t('trade.challanNeedsCustomer')}</p>}
      {error && <p className="text-sm text-due">{error}</p>}
    </>
  )
}

/** The rest of the bill's extras for trades: the credit-limit warning, and the painter with a tinted line (base tin + shade + tinting charge). */
export function TradeBillPanel({ shopId, shopType, customer, onKhata, products, extras, onExtras, onAddLine }: {
  shopId: string
  shopType: ShopType
  customer: Customer | null
  onKhata: number
  products: Product[]
  extras: TradeExtras
  onExtras: (extras: TradeExtras) => void
  onAddLine: (line: CartLine) => void
}) {
  const { t } = useTranslation()
  const paint = shopType === 'paint'
  // Painters bring paint customers, electricians bring electrical ones: the same commission.
  const fitters = paint || shopType === 'electrical' || shopType === 'woodwork'
  const painters = useQuery({ queryKey: [shopId, 'painters'], queryFn: () => api<Painter[]>('/v1/painters'), enabled: fitters }).data
  const [tinting, setTinting] = useState(false)
  const overLimit = customer?.creditLimit ? customer.balance + onKhata > customer.creditLimit : false

  return (
    <div className="flex flex-col gap-2">
      {overLimit && (
        <p role="alert" className="rounded-xl bg-due-soft p-3 text-sm font-semibold text-red-800">
          {t('trade.overLimit', { limit: formatRupees(customer!.creditLimit!), after: formatRupees(round2(customer!.balance + onKhata)) })}
        </p>
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

      {tinting && <TintSheet shopId={shopId} customerId={customer?.id ?? null} products={products} onClose={() => setTinting(false)} onAdd={onAddLine} />}
    </div>
  )
}

/**
 * A tinted tin: pick the base tin (it comes off stock), type the shade code from the tinting machine and the tinting
 * charge; the line's rate is base price + tinting.
 */
interface ShadeBefore { productId: string; name: string; shade: string; unitPrice: number; quantity: number; boughtAt: string }

function TintSheet({ shopId, customerId, products, onClose, onAdd }: { shopId: string; customerId: string | null; products: Product[]; onClose: () => void; onAdd: (line: CartLine) => void }) {
  const before = useQuery({ queryKey: [shopId, 'shades', customerId], queryFn: () => api<ShadeBefore[]>(`/v1/customers/${customerId}/shades`), enabled: !!customerId }).data ?? []
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
      {before.length > 0 && (
        <div className="mb-3 flex flex-col gap-1">
          <span className="text-sm font-semibold text-muted">{t('hwp1.trade.shadesBefore')}</span>
          <div className="flex flex-wrap gap-2">
            {before.map((b) => {
              const product = products.find((x) => x.id === b.productId)
              // The tinting charge then, on today's base price.
              const tintThen = product ? Math.max(0, round2(b.unitPrice - product.sellingPrice)) : 0
              return (
                <button key={`${b.productId}-${b.shade}`} disabled={!product}
                  onClick={() => { setBaseId(b.productId); setShade(b.shade); setTint(tintThen ? String(tintThen) : '') }}
                  className="min-h-11 rounded-xl border border-udhaar px-3 text-left text-sm disabled:opacity-40">
                  <b>🎨 {b.shade}</b> <span className="text-muted">{b.name} · {b.boughtAt.slice(0, 10)}</span>
                </button>
              )
            })}
          </div>
        </div>
      )}
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

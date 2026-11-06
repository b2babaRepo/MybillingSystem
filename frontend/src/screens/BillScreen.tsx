import { lazy, Suspense, useEffect, useMemo, useRef, useState } from 'react'
import { groupStyles, sortVariants, variantLabel } from '../lib/styles'
import { billNameFor } from '../lib/billNames'
import { garmentRate } from '../lib/gst'
import { useTranslation } from 'react-i18next'
import { ItemCard } from '../components/ItemCard'
import { imageUrl } from '../lib/images'
import { Button, Field, Sheet } from '../components/ui'
import { saveBill, useCustomers, usePopularOrder, useProducts, type CartLine } from '../lib/data'
import { formatDateTime, formatRupees, whatsAppLink } from '../lib/format'
import { round2 } from '../lib/local'
import type { PaymentMode, Unit } from '../lib/types'
import { useShopSettings, shopTypes, isTrade, upiUri } from '../lib/reports'
import QRCode from 'qrcode'
import { navigate } from '../router'
import { useSerialsInStock } from './SerialsScreen'
import { ask } from '../components/Ask'

// Cement, paint and hardware extras load only for those shops, keeping the kirana counter's first load small.
const TradeBillPanel = lazy(() => import('./TradeBill').then((module) => ({ default: module.TradeBillPanel })))

/** Item billing: tap tiles, adjust quantities, choose who pays and how much now. The rest goes on khata. */
export function BillScreen({ shopId, shopName, isOwner = true }: { shopId: string; shopName: string; isOwner?: boolean }) {
  const { t } = useTranslation()
  const { products } = useProducts(shopId)
  const rank = usePopularOrder(shopId)
  const [search, setSearch] = useState('')
  const query = search.trim().toLowerCase()
  // Most-sold items first (the tiles the thumb needs), then the rest by name; typing filters.
  const tiles = products
    .filter((p) => !query || p.name.toLowerCase().includes(query) || (p.styleName ?? '').toLowerCase().includes(query))
    .sort((a, b) => (rank.get(a.id) ?? 1e9) - (rank.get(b.id) ?? 1e9) || a.name.localeCompare(b.name))
    .slice(0, query ? 30 : 12)
  const { customers } = useCustomers(shopId)
  const [cart, setCart] = useState<CartLine[]>([])
  // The style whose sizes/colours are being picked (a tile for a style opens this instead of adding straight away).
  const [stylePick, setStylePick] = useState<string | null>(null)
  const [customerId, setCustomerId] = useState('')
  const [paid, setPaid] = useState<string | null>(null)
  const [mode, setMode] = useState<PaymentMode>('cash')
  // A loose item, or a charge line ("Transport") with its name filled in.
  const [loose, setLoose] = useState<false | { name: string; unit: Unit }>(false)
  const settings = useShopSettings(shopId).data
  const shopType = settings?.type ?? 'kirana'
  const types = shopTypes(settings)
  const [extras, setExtras] = useState({ site: '', vehicleNumber: '', painterId: '' })
  // Schedule H, H1 and X medicines need the doctor and patient on the bill; H1 and X also the doctor's address (the register).
  const [rx, setRx] = useState({ doctorName: '', doctorAddress: '', doctorRegNo: '', patientName: '', patientAddress: '' })
  const [saved, setSaved] = useState<{ id: string; total: number; lines: CartLine[]; paid: number; mode: 'cash' | 'upi'; customer: { name: string; phone: string | null } | null } | null>(null)

  // On a phone the bill sits below the item grid: a bar above the tabs shows the total until the bill is in view.
  // Shown only while the bill is still below the screen: once scrolled to it, or past it down to the payment and
  // save button (a UPI QR makes that box tall), the bar would only cover them.
  const billRef = useRef<HTMLDivElement>(null)
  const [billBelow, setBillBelow] = useState(true)
  const billInView = !billBelow
  useEffect(() => {
    const element = billRef.current
    if (!element || typeof IntersectionObserver === 'undefined') return
    const observer = new IntersectionObserver(
      ([entry]) => setBillBelow(!entry.isIntersecting && entry.boundingClientRect.top > 0),
      { rootMargin: '0px 0px -68px 0px' })
    observer.observe(element)
    return () => observer.disconnect()
  }, [])
  // A line with no price would go out free: the bill waits until every line has one.
  const unpriced = cart.some((l) => !(l.unitPrice > 0))
  // Discounts: a line sold below its item's regular price. Helpers have a limit the owner sets (the server checks it too).
  const staffLimit = useShopSettings(shopId).data?.staffMaxDiscountPercent ?? 10
  const regularOf = (l: CartLine) => products.find((p) => p.id === l.productId)?.sellingPrice ?? 0
  const offOf = (l: CartLine) => { const r = regularOf(l); return r > 0 && l.unitPrice > 0 && l.unitPrice < r ? { amount: round2((r - l.unitPrice) * l.quantity), percent: Math.round(((r - l.unitPrice) / r) * 1000) / 10 } : null }
  const discountTotal = round2(cart.reduce((sum, l) => sum + (offOf(l)?.amount ?? 0), 0))
  const overLimit = !isOwner && cart.some((l) => (offOf(l)?.percent ?? 0) > staffLimit)
  const total = useMemo(() => round2(cart.reduce((sum, l) => sum + round2(l.quantity * l.unitPrice), 0)), [cart])
  const customer = customers.find((c) => c.id === customerId) ?? null
  const paidAmount = paid === null || !customer ? total : Math.min(Number(paid) || 0, total)
  const schedules = cart.map((l) => products.find((p) => p.id === l.productId)?.schedule).filter((s) => s === 'h' || s === 'h1' || s === 'x')
  const needsAddress = schedules.some((s) => s === 'h1' || s === 'x')
  // Serialised units go out by serial number, one per unit (the warranty runs from this bill).
  const [picked, setPicked] = useState<Record<string, string[]>>({})
  const serialLines = cart.filter((l) => l.productId && products.find((p) => p.id === l.productId)?.tracksSerials)
  const serialsMissing = serialLines.some((l) => (picked[l.productId!] ?? []).length !== l.quantity)
  const rxMissing = schedules.length > 0 && (rx.doctorName.trim().length < 2 || rx.patientName.trim().length < 2 || (needsAddress && rx.doctorAddress.trim().length < 3))

  function add(line: CartLine) {
    setSaved(null)
    setCart((current) => {
      // A tinted tin shares its base's product but is its own line (its own shade and rate).
      const index = line.productId ? current.findIndex((l) => l.productId === line.productId && l.name === line.name) : -1
      if (index < 0) return [...current, line]
      return current.map((l, i) => (i === index ? { ...l, quantity: round2(l.quantity + 1) } : l))
    })
  }

  function setQuantity(index: number, quantity: number) {
    setCart((current) => (quantity <= 0 ? current.filter((_, i) => i !== index) : current.map((l, i) => (i === index ? { ...l, quantity } : l))))
  }

  /** A medicine sold loose: so many tablets of a strip, as that fraction of the strip at the strip's price. */
  async function sellLoose(index: number, perPack: number) {
    const tablets = Math.round(Number((await ask(t('loose.ask', { count: perPack }))) || 0))
    if (!(tablets > 0)) return
    setCart((current) => current.map((l, i) => {
      if (i !== index) return l
      const base = l.name.replace(/ \(\d+ of \d+\)$/, '')
      return { ...l, name: `${base} (${tablets} of ${perPack})`, quantity: Math.round((tablets / perPack) * 1000) / 1000 }
    }))
  }

  /** Prices change, and starter-catalogue items have none yet: the counter price is editable per line. */
  function setPrice(index: number, unitPrice: number) {
    setCart((current) => current.map((l, i) => (i === index ? { ...l, unitPrice } : l)))
  }

  async function save() {
    const result = await saveBill(shopId, cart, customer, paidAmount, mode, {
      site: extras.site.trim() || null, vehicleNumber: extras.vehicleNumber.trim() || null, painterId: extras.painterId || null,
      serials: Object.entries(picked).flatMap(([productId, list]) => list.map((serialNo) => ({ productId, serialNo }))),
      prescription: schedules.length > 0 ? { doctorName: rx.doctorName.trim(), doctorAddress: rx.doctorAddress.trim() || null, doctorRegNo: rx.doctorRegNo.trim() || null, patientName: rx.patientName.trim(), patientAddress: rx.patientAddress.trim() || null } : null,
    })
    setRx({ doctorName: '', doctorAddress: '', doctorRegNo: '', patientName: '', patientAddress: '' })
    setPicked({})
    setExtras({ ...extras, site: '', vehicleNumber: '' })
    setSaved({ ...result, lines: cart, paid: paidAmount, mode, customer: customer && { name: customer.name, phone: customer.phone } })
    setCart([])
    setPaid(null)
    setCustomerId('')
    setSearch('')
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      <input
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder={t('search')}
        aria-label={t('search')}
        className="h-12 w-full rounded-[14px] border border-line bg-white px-4 text-[17px] outline-none focus:border-teal-600"
      />
      <div className="grid grid-cols-3 gap-2">
        {groupStyles(tiles).map((entry) => entry.kind === 'style' ? (
          <ItemCard
            key={`style-${entry.name}`}
            compact
            item={entry.items[0]}
            imageUrl={imageUrl(shopId, entry.items[0].id, entry.items[0].imageAt, 'thumb')}
            title={entry.name}
            subtitle={t('styles.count', { count: entry.items.length })}
            badge={cart.filter((l) => entry.items.some((p) => p.id === l.productId)).reduce((n, l) => n + l.quantity, 0) || undefined}
            onClick={() => setStylePick(entry.name)}
          />
        ) : ((p) => (
          <ItemCard
            key={p.id}
            compact
            item={p}
            imageUrl={imageUrl(shopId, p.id, p.imageAt, 'thumb')}
            title={p.name}
            subtitle={p.sellingPrice > 0
              ? <span><b className="text-ink">{formatRupees(p.sellingPrice)}</b>/{t(`units.${p.unit}`)}</span>
              : t('stock.noPrice')}
            badge={cart.filter((l) => l.productId === p.id).reduce((n, l) => n + l.quantity, 0) || undefined}
            onClick={() => add({ productId: p.id, name: p.name, quantity: 1, unit: p.unit, unitPrice: p.sellingPrice })}
          />
        ))(entry.product))}
        {/* Building-material bills carry transport and loading as their own lines. */}
        {types.includes('building_materials') && (['transport', 'loading'] as const).map((charge) => (
          <button key={charge} onClick={() => setLoose({ name: t(`charge.${charge}`), unit: 'piece' })}
            className="min-h-20 rounded-xl border-2 border-dashed border-udhaar p-2 font-semibold text-udhaar">
            + {t(`charge.${charge}`)}
          </button>
        ))}
        <button onClick={() => setLoose({ name: '', unit: 'kg' })} className="min-h-20 rounded-xl border-2 border-dashed border-line p-2 font-semibold text-muted">
          + {t('addLooseItem')}
        </button>
      </div>

      {stylePick && (
        <Sheet title={stylePick} onClose={() => setStylePick(null)}>
          <p className="-mt-1 mb-2 text-sm text-muted">{t('styles.pick')}</p>
          <div className="grid grid-cols-3 gap-2">
            {sortVariants(products.filter((p) => p.styleName?.trim() === stylePick)).map((p) => {
              const out = !p.isService && p.stock <= 0
              return (
                <button key={p.id} onClick={() => { add({ productId: p.id, name: p.name, quantity: 1, unit: p.unit, unitPrice: p.sellingPrice }); setStylePick(null) }}
                  className={`flex min-h-16 flex-col items-center justify-center rounded-xl border-2 px-1 py-2 ${out ? 'border-line opacity-60' : 'border-teal-700'}`}>
                  <span className="text-lg font-extrabold">{variantLabel(p) || p.name}</span>
                  <span className="text-xs text-muted">{p.sellingPrice > 0 ? formatRupees(p.sellingPrice) : t('stock.noPrice')} · {out ? t('stock.out') : t('styles.left', { count: p.stock })}</span>
                </button>
              )
            })}
          </div>
        </Sheet>
      )}

      <div ref={billRef} className="rounded-[18px] bg-white p-3">
        {cart.length === 0 && (
          <div className="flex flex-col items-center gap-2 py-4 text-center text-muted">
            <p>{saved ? `${t('saved')} ${formatRupees(saved.total)}` : t('emptyCart')}</p>
            {saved && saved.paid > 0 && (
              <p className={`rounded-full px-3 py-1 text-sm font-semibold ${saved.mode === 'upi' ? 'bg-teal-50 text-teal-800' : 'bg-amber-50 text-amber-900'}`}>
                {t(saved.mode === 'upi' ? 'upiPay.paidUpi' : 'upiPay.paidCash', { amount: formatRupees(saved.paid) })}
              </p>
            )}
            {saved && (
              <a
                className="flex h-12 items-center justify-center rounded-2xl bg-[#15803d] px-5 font-bold text-white"
                href={whatsAppLink(saved.customer?.phone ?? null, billReceipt(t, shopName, saved, (l) => billNameFor(products.find((p) => p.id === l.productId)) ?? l.name))}
                target="_blank"
                rel="noreferrer"
              >
                {t('billReceipt.send')}
              </a>
            )}
            {saved && (
              <button onClick={() => navigate(`/print/bill/${saved.id}`)} className="min-h-11 font-semibold text-teal-700 underline">{t('print.viewBill')}</button>
            )}
          </div>
        )}
        {cart.map((l, i) => (
          <div key={`${l.productId ?? l.name}-${i}`} className="flex items-center gap-2 border-b border-[#edf2f1] py-2 last:border-0">
            <div className="flex-1">
              <div className="font-semibold">{l.name}{(() => { const s = products.find((p) => p.id === l.productId)?.schedule; return s && s !== 'g' ? <span className="ml-1 whitespace-nowrap rounded bg-red-50 px-1 text-xs text-due">{t('rx.badge', { schedule: s.toUpperCase() })}</span> : null })()}</div>
              <label className="flex items-center gap-1 text-xs text-muted">
                ₹
                <input
                  value={l.unitPrice || ''}
                  onChange={(e) => setPrice(i, Number(e.target.value) || 0)}
                  inputMode="decimal"
                  aria-label={`${t('price')}: ${l.name}`}
                  placeholder={t('price')}
                  className={`h-11 w-16 rounded-md border px-1 ${l.unitPrice > 0 ? 'border-line' : 'border-udhaar'}`}
                />
                /{t(`units.${l.unit}`)}
              </label>
              {regularOf(l) > 0 && (
                <div className="mt-1 flex flex-wrap items-center gap-1.5">
                  {/* One tap for the usual shop discounts, from the item's regular price; tap the price box for any other. */}
                  {[5, 10].map((pct) => (
                    <button key={pct} type="button" onClick={() => setPrice(i, round2(regularOf(l) * (1 - pct / 100)))}
                      className="h-8 rounded-full border border-line px-2.5 text-xs font-semibold">{t('discount.chip', { percent: pct })}</button>
                  ))}
                  {offOf(l) && (
                    <>
                      <span className={`text-xs font-semibold ${!isOwner && offOf(l)!.percent > staffLimit ? 'text-due' : 'text-teal-800'}`}>
                        {t('discount.off', { amount: formatRupees(offOf(l)!.amount), percent: offOf(l)!.percent })}
                      </span>
                      <button type="button" onClick={() => setPrice(i, regularOf(l))} className="h-8 px-1 text-xs text-muted underline">{t('discount.undo')}</button>
                    </>
                  )}
                </div>
              )}
              {!isOwner && (offOf(l)?.percent ?? 0) > staffLimit && <p className="text-xs font-semibold text-due">{t('discount.overLimit', { percent: staffLimit })}</p>}
              {(() => {
                // Medicines run on thin margins: warn when this price, before GST, is at or under what the item cost.
                const p = products.find((x) => x.id === l.productId)
                if (!p || p.costPrice == null || !(l.unitPrice > 0)) return null
                const net = l.unitPrice / (1 + (garmentRate(p.hsnCode, p.gstRate, l.unitPrice) ?? 0) / 100)
                const margin = ((net - p.costPrice) / net) * 100
                return margin < 5 ? <p className={`text-xs font-semibold ${margin <= 0 ? 'text-due' : 'text-amber-800'}`}>{t(margin <= 0 ? 'pmonth.belowCost' : 'pmonth.thinMargin', { percent: Math.round(margin * 10) / 10 })}</p> : null
              })()}
              {(() => {
                // A clothes line priced above ₹2,500 a piece is billed at 18%: say so, so the higher total isn't a surprise.
                const p = products.find((x) => x.id === l.productId)
                return p && l.unitPrice > 0 && garmentRate(p.hsnCode, p.gstRate, l.unitPrice) === 18 && p.gstRate !== 18
                  ? <p className="text-xs font-semibold text-amber-800">{t('garmentGst.high')}</p> : null
              })()}
              {(() => { const per = products.find((p) => p.id === l.productId)?.unitsPerPack; return per ? (
                <button onClick={() => sellLoose(i, per)} className="min-h-11 text-xs font-semibold text-teal-700 underline">{t('loose.button')}</button>
              ) : null })()}
              {tmtKgPerBar(l.name) && l.unit === 'kg' && (
                <button onClick={async () => { const bars = Number((await ask(t('bars.ask', { name: l.name }))) || 0); if (bars > 0) setQuantity(i, Math.round(bars * tmtKgPerBar(l.name)! * 1000) / 1000) }}
                  className="min-h-11 text-xs font-semibold text-teal-700 underline">{t('bars.button', { kg: tmtKgPerBar(l.name) })}</button>
              )}
            </div>
            <button className="h-11 w-11 rounded-lg bg-[#edf2f1] text-xl" onClick={() => setQuantity(i, round2(l.quantity - 1))}>−</button>
            <QuantityInput value={l.quantity} onChange={(q) => setQuantity(i, q)} label={`${t('quantity')}: ${l.name}`} />
            <button className="h-11 w-11 rounded-lg bg-[#edf2f1] text-xl" onClick={() => setQuantity(i, round2(l.quantity + 1))}>+</button>
            <div className="w-20 text-right font-semibold">{formatRupees(round2(l.quantity * l.unitPrice))}</div>
          </div>
        ))}
        {cart.length > 0 && (<>
          {discountTotal > 0 && (
            <div className="mt-2 flex justify-between text-sm font-semibold text-teal-800">
              <span>{t('discount.total')}</span><span>− {formatRupees(discountTotal)}</span>
            </div>
          )}
          <div className="mt-2 flex justify-between text-xl font-bold">
            <span>{t('total')}</span>
            <span>{formatRupees(total)}</span>
          </div>
        </>)}
      </div>

      {cart.length > 0 && (
        <div className="space-y-3 rounded-[18px] bg-white p-3">
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-muted">{t('customer')}</span>
            <select value={customerId} onChange={(e) => setCustomerId(e.target.value)} className="h-12 w-full rounded-xl border border-line bg-white px-3 text-lg">
              <option value="">{t('walkIn')}</option>
              {customers.map((c) => (
                <option key={c.id} value={c.id}>{c.name}</option>
              ))}
            </select>
          </label>
          {customer && (
            <Field label={t('paidNow')} value={paid ?? String(total)} onChange={(e) => setPaid(e.target.value)} inputMode="decimal" />
          )}
          {customer && total - paidAmount > 0 && (
            <p className="font-semibold text-due">
              {t('onKhata')}: {formatRupees(round2(total - paidAmount))}
            </p>
          )}
          {isTrade(types) && (
            <Suspense fallback={null}>
              <TradeBillPanel shopId={shopId} shopType={shopType} customer={customer} total={total} onKhata={customer ? round2(total - paidAmount) : 0}
                cart={cart} products={products} extras={extras} onExtras={setExtras} onAddLine={add}
                onChallanSaved={() => { setCart([]); setPaid(null); setCustomerId(''); setExtras({ ...extras, site: '', vehicleNumber: '' }) }} />
            </Suspense>
          )}
          {serialLines.map((l) => (
            <SerialPicker key={l.productId} shopId={shopId} productId={l.productId!} name={l.name} quantity={l.quantity}
              chosen={picked[l.productId!] ?? []} onChange={(list) => setPicked({ ...picked, [l.productId!]: list })} />
          ))}
          {schedules.length > 0 && (
            <div className="flex flex-col gap-2 rounded-xl bg-red-50 p-2">
              <p className="text-sm font-semibold text-due">{t(needsAddress ? 'rx.neededH1' : 'rx.needed')}</p>
              <div className="grid grid-cols-2 gap-2">
                <Field label={t('rx.doctor')} value={rx.doctorName} onChange={(e) => setRx({ ...rx, doctorName: e.target.value })} />
                <Field label={t('rx.regNo')} value={rx.doctorRegNo} onChange={(e) => setRx({ ...rx, doctorRegNo: e.target.value })} />
              </div>
              <Field label={t(needsAddress ? 'rx.doctorAddressRequired' : 'rx.doctorAddress')} value={rx.doctorAddress} onChange={(e) => setRx({ ...rx, doctorAddress: e.target.value })} />
              <div className="grid grid-cols-2 gap-2">
                <Field label={t('rx.patient')} value={rx.patientName} onChange={(e) => setRx({ ...rx, patientName: e.target.value })} />
                <Field label={t('rx.patientAddress')} value={rx.patientAddress} onChange={(e) => setRx({ ...rx, patientAddress: e.target.value })} />
              </div>
            </div>
          )}
          <div className="flex gap-2 text-sm">
            {(['cash', 'upi'] as const).map((m) => (
              <button key={m} onClick={() => setMode(m)} className={`h-11 rounded-xl px-4 font-semibold ${mode === m ? 'bg-ink text-white' : 'border border-line bg-white text-ink'}`}>
                {t(m)}
              </button>
            ))}
          </div>
          {mode === 'upi' && paidAmount > 0 && <UpiCollect upiId={settings?.upiId ?? null} shop={shopName} amount={paidAmount} />}
          {unpriced && <p className="text-sm font-semibold text-due">{t('bars.noPrice')}</p>}
          <Button className="w-full" onClick={save} disabled={total <= 0 || unpriced || rxMissing || serialsMissing || overLimit}>
            {mode === 'upi' && paidAmount > 0 ? t('upiPay.save', { amount: formatRupees(total) }) : `${t('saveBill')} · ${formatRupees(total)}`}
          </Button>
        </div>
      )}

      {cart.length > 0 && !billInView && (
        <button onClick={() => billRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })}
          className="fixed inset-x-0 bottom-[calc(68px+env(safe-area-inset-bottom))] z-10 mx-auto flex h-14 w-[calc(100%-2rem)] max-w-[34rem] items-center justify-between rounded-2xl bg-teal-700 px-4 font-bold text-white shadow-lg">
          <span>{t('billBar', { count: cart.length })}</span>
          <span>{formatRupees(total)} · {t('checkout')} ↓</span>
        </button>
      )}

      {loose && <LooseItemSheet onClose={() => setLoose(false)} onAdd={add} initialName={loose.name} initialUnit={loose.unit} />}
    </div>
  )
}

function LooseItemSheet({ onClose, onAdd, initialName = '', initialUnit = 'kg' }: {
  onClose: () => void
  onAdd: (line: CartLine) => void
  initialName?: string
  initialUnit?: Unit
}) {
  const { t } = useTranslation()
  const [name, setName] = useState(initialName)
  const [price, setPrice] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [unit, setUnit] = useState<Unit>(initialUnit)
  const valid = name.trim() && Number(price) > 0 && Number(quantity) > 0

  return (
    <Sheet title={t('addLooseItem')} onClose={onClose}>
      <div className="space-y-3">
        <Field label={t('itemName')} value={name} onChange={(e) => setName(e.target.value)} autoFocus />
        <div className="grid grid-cols-3 gap-2">
          <Field label={t('quantity')} value={quantity} onChange={(e) => setQuantity(e.target.value)} inputMode="decimal" />
          <UnitSelect value={unit} onChange={setUnit} />
          <Field label={t('price')} value={price} onChange={(e) => setPrice(e.target.value)} inputMode="decimal" />
        </div>
        <Button
          className="w-full"
          disabled={!valid}
          onClick={() => {
            onAdd({ productId: null, name: name.trim(), quantity: Number(quantity), unit, unitPrice: Number(price) })
            onClose()
          }}
        >
          {t('addItem')}
        </Button>
      </div>
    </Sheet>
  )
}

const UNITS: Unit[] = ['piece', 'kg', 'g', 'litre', 'ml', 'packet', 'pair', 'dozen', 'box', 'jar', 'bag', 'tonne', 'brass', 'cft', 'metre', 'sq_ft', 'running_ft']

export function UnitSelect({ value, onChange }: { value: Unit; onChange: (unit: Unit) => void }) {
  const { t } = useTranslation()
  return (
    <label className="block">
      <span className="mb-1 block text-sm font-medium text-muted">{t('unit')}</span>
      <select value={value} onChange={(e) => onChange(e.target.value as Unit)} className="h-12 w-full rounded-xl border border-line bg-white px-2 text-lg">
        {UNITS.map((u) => (
          <option key={u} value={u}>{t(`units.${u}`)}</option>
        ))}
      </select>
    </label>
  )
}

/**
 * The bill as a WhatsApp message, every item listed, so the customer checks it at home the same day: anything
 * left on the counter comes up while the shop still remembers.
 */
/**
 * Choosing UPI at the counter: a QR for exactly what is paid now, which any UPI app scans (GPay, PhonePe, Paytm,
 * BHIM), with the shop's UPI ID and the amount filled in. The shopkeeper saves the bill once the money shows on
 * their phone; the app can't see the payment itself. Without a UPI ID in settings it says where to add one.
 */
function UpiCollect({ upiId, shop, amount }: { upiId: string | null; shop: string; amount: number }) {
  const { t } = useTranslation()
  const [qr, setQr] = useState('')
  const uri = upiId ? upiUri(upiId, shop, amount, t('upiPay.note', { shop })) : ''
  useEffect(() => {
    if (!uri) return
    let live = true
    void QRCode.toDataURL(uri, { width: 360, margin: 1, color: { dark: '#10201d', light: '#ffffff' } }).then((url) => { if (live) setQr(url) })
    return () => { live = false }
  }, [uri])
  if (!upiId) {
    return (
      <button onClick={() => navigate('/settings')} className="rounded-xl bg-amber-50 p-3 text-left text-sm font-semibold text-amber-900">
        {t('upiPay.noId')} →
      </button>
    )
  }
  return (
    <div className="flex flex-col items-center gap-1 rounded-xl border border-line p-3 text-center">
      <p className="font-bold">{t('upiPay.scan', { amount: formatRupees(amount) })}</p>
      {qr && <img src={qr} alt={t('upiPay.qrAlt', { amount: formatRupees(amount) })} className="h-48 w-48" />}
      <p className="text-sm text-muted">{upiId}</p>
      <p className="text-xs text-muted">{t('upiPay.check')}</p>
    </div>
  )
}

function billReceipt(
  t: (key: string, options?: Record<string, unknown>) => string,
  shopName: string,
  bill: { total: number; lines: CartLine[]; paid: number; mode?: 'cash' | 'upi'; customer: { name: string } | null },
  nameOf: (line: CartLine) => string = (l) => l.name,
) {
  const items = bill.lines.map((l) => `• ${nameOf(l)} ${l.quantity} ${t(`units.${l.unit}`)} × ${formatRupees(l.unitPrice)} = ${formatRupees(Math.round(l.quantity * l.unitPrice * 100) / 100)}`)
  const due = Math.round((bill.total - bill.paid) * 100) / 100
  return [
    t('billReceipt.header', { shop: shopName, date: formatDateTime(new Date().toISOString()) }),
    ...items,
    t('billReceipt.total', { total: formatRupees(bill.total) }),
    bill.paid > 0 ? t(bill.mode === 'upi' ? 'billReceipt.paidUpi' : 'billReceipt.paid', { paid: formatRupees(bill.paid) }) : '',
    bill.customer && due > 0 ? t('billReceipt.onKhata', { due: formatRupees(due) }) : '',
    t('billReceipt.check'),
  ].filter(Boolean).join('\n')
}

/**
 * Typed quantities stay as typed ("0.", "" while typing), so weights like 0.5 kg or 245.5 kg can be entered; the line
 * only changes once the text is a number above 0.
 */
function QuantityInput({ value, onChange, label }: { value: number; onChange: (q: number) => void; label: string }) {
  const [text, setText] = useState(String(value))
  useEffect(() => { if (Number(text) !== value) setText(String(value)) }, [value]) // eslint-disable-line react-hooks/exhaustive-deps
  return (
    <input
      value={text}
      onChange={(e) => { const next = e.target.value.replace(/[^\d.]/g, ''); setText(next); if (Number(next) > 0) onChange(Number(next)) }}
      inputMode="decimal"
      aria-label={label}
      className="h-11 w-16 rounded-lg border border-line text-center"
    />
  )
}

/** A 12 m TMT bar's standard weight in kg (d² ÷ 162 kg a metre), read from a name like "TMT bar 16 mm". */
function tmtKgPerBar(name: string) {
  const mm = /TMT.*?(\d+)\s*mm/i.exec(name)?.[1]
  return mm ? Math.round((Number(mm) ** 2 / 162) * 12 * 100) / 100 : null
}

/** Picks which units of a serialised item are going out: one serial per unit, from those in stock. */
function SerialPicker({ shopId, productId, name, quantity, chosen, onChange }: { shopId: string; productId: string; name: string; quantity: number; chosen: string[]; onChange: (list: string[]) => void }) {
  const { t } = useTranslation()
  const inStock = useSerialsInStock(shopId, productId)
  const toggle = (serial: string) => onChange(chosen.includes(serial) ? chosen.filter((s) => s !== serial) : chosen.length < quantity ? [...chosen, serial] : chosen)
  return (
    <div className={`flex flex-col gap-1 rounded-xl p-2 ${chosen.length === quantity ? 'bg-teal-50' : 'bg-amber-50'}`}>
      <p className="text-sm font-semibold">{t('serials.pick', { name, count: quantity, picked: chosen.length })}</p>
      {inStock.length === 0 && <p className="text-xs text-due">{t('serials.noneInStock')}</p>}
      <div className="flex flex-wrap gap-1">
        {inStock.map((s) => (
          <button key={s.id} onClick={() => toggle(s.serialNo)} className={`min-h-10 rounded-lg px-2 font-mono text-sm ${chosen.includes(s.serialNo) ? 'bg-teal-700 text-white' : 'border border-line bg-white'}`}>{s.serialNo}</button>
        ))}
      </div>
    </div>
  )
}

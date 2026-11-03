import { lazy, Suspense, useEffect, useMemo, useRef, useState } from 'react'
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

// Cement, paint and hardware extras load only for those shops, keeping the kirana counter's first load small.
const TradeBillPanel = lazy(() => import('./TradeBill').then((module) => ({ default: module.TradeBillPanel })))

/** Item billing: tap tiles, adjust quantities, choose who pays and how much now. The rest goes on khata. */
export function BillScreen({ shopId, shopName }: { shopId: string; shopName: string }) {
  const { t } = useTranslation()
  const { products } = useProducts(shopId)
  const rank = usePopularOrder(shopId)
  const [search, setSearch] = useState('')
  const query = search.trim().toLowerCase()
  // Most-sold items first (the tiles the thumb needs), then the rest by name; typing filters.
  const tiles = products
    .filter((p) => !query || p.name.toLowerCase().includes(query))
    .sort((a, b) => (rank.get(a.id) ?? 1e9) - (rank.get(b.id) ?? 1e9) || a.name.localeCompare(b.name))
    .slice(0, query ? 30 : 12)
  const { customers } = useCustomers(shopId)
  const [cart, setCart] = useState<CartLine[]>([])
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
  const total = useMemo(() => round2(cart.reduce((sum, l) => sum + round2(l.quantity * l.unitPrice), 0)), [cart])
  const customer = customers.find((c) => c.id === customerId) ?? null
  const paidAmount = paid === null || !customer ? total : Math.min(Number(paid) || 0, total)
  const schedules = cart.map((l) => products.find((p) => p.id === l.productId)?.schedule).filter((s) => s === 'h' || s === 'h1' || s === 'x')
  const needsAddress = schedules.some((s) => s === 'h1' || s === 'x')
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
  function sellLoose(index: number, perPack: number) {
    const tablets = Math.round(Number(window.prompt(t('loose.ask', { count: perPack })) || 0))
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
      prescription: schedules.length > 0 ? { doctorName: rx.doctorName.trim(), doctorAddress: rx.doctorAddress.trim() || null, doctorRegNo: rx.doctorRegNo.trim() || null, patientName: rx.patientName.trim(), patientAddress: rx.patientAddress.trim() || null } : null,
    })
    setRx({ doctorName: '', doctorAddress: '', doctorRegNo: '', patientName: '', patientAddress: '' })
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
        {tiles.map((p) => (
          <ItemCard
            key={p.id}
            compact
            item={p}
            imageUrl={imageUrl(shopId, p.id, p.imageAt)}
            title={p.name}
            subtitle={p.sellingPrice > 0 ? `${formatRupees(p.sellingPrice)}/${t(`units.${p.unit}`)}` : t('stock.noPrice')}
            badge={cart.filter((l) => l.productId === p.id).reduce((n, l) => n + l.quantity, 0) || undefined}
            onClick={() => add({ productId: p.id, name: p.name, quantity: 1, unit: p.unit, unitPrice: p.sellingPrice })}
          />
        ))}
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
                href={whatsAppLink(saved.customer?.phone ?? null, billReceipt(t, shopName, saved))}
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
              {(() => {
                // Medicines run on thin margins: warn when this price, before GST, is at or under what the item cost.
                const p = products.find((x) => x.id === l.productId)
                if (!p || p.costPrice == null || !(l.unitPrice > 0)) return null
                const net = l.unitPrice / (1 + (p.gstRate ?? 0) / 100)
                const margin = ((net - p.costPrice) / net) * 100
                return margin < 5 ? <p className={`text-xs font-semibold ${margin <= 0 ? 'text-due' : 'text-amber-800'}`}>{t(margin <= 0 ? 'pmonth.belowCost' : 'pmonth.thinMargin', { percent: Math.round(margin * 10) / 10 })}</p> : null
              })()}
              {(() => { const per = products.find((p) => p.id === l.productId)?.unitsPerPack; return per ? (
                <button onClick={() => sellLoose(i, per)} className="min-h-11 text-xs font-semibold text-teal-700 underline">{t('loose.button')}</button>
              ) : null })()}
              {tmtKgPerBar(l.name) && l.unit === 'kg' && (
                <button onClick={() => { const bars = Number(window.prompt(t('bars.ask', { name: l.name })) || 0); if (bars > 0) setQuantity(i, Math.round(bars * tmtKgPerBar(l.name)! * 1000) / 1000) }}
                  className="min-h-11 text-xs font-semibold text-teal-700 underline">{t('bars.button', { kg: tmtKgPerBar(l.name) })}</button>
              )}
            </div>
            <button className="h-11 w-11 rounded-lg bg-[#edf2f1] text-xl" onClick={() => setQuantity(i, round2(l.quantity - 1))}>−</button>
            <QuantityInput value={l.quantity} onChange={(q) => setQuantity(i, q)} label={`${t('quantity')}: ${l.name}`} />
            <button className="h-11 w-11 rounded-lg bg-[#edf2f1] text-xl" onClick={() => setQuantity(i, round2(l.quantity + 1))}>+</button>
            <div className="w-20 text-right font-semibold">{formatRupees(round2(l.quantity * l.unitPrice))}</div>
          </div>
        ))}
        {cart.length > 0 && (
          <div className="mt-2 flex justify-between text-xl font-bold">
            <span>{t('total')}</span>
            <span>{formatRupees(total)}</span>
          </div>
        )}
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
          <Button className="w-full" onClick={save} disabled={total <= 0 || unpriced || rxMissing}>
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
) {
  const items = bill.lines.map((l) => `• ${l.name} ${l.quantity} ${t(`units.${l.unit}`)} × ${formatRupees(l.unitPrice)} = ${formatRupees(Math.round(l.quantity * l.unitPrice * 100) / 100)}`)
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

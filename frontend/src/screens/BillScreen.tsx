import { lazy, Suspense, useEffect, useMemo, useRef, useState } from 'react'
import { getSession } from '../lib/session'
import { api } from '../lib/api'
import { useQuery } from '@tanstack/react-query'
import { currentExchange, endExchange, type Exchange } from '../lib/exchange'
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
  // Who is selling: everyone with access to the shop. The bill's choice goes on every line; a line can name another.
  const sellers = useQuery({ queryKey: [shopId, 'sellers'], queryFn: () => api<{ userId: string; name: string }[]>('/v1/shop/sellers'), staleTime: 300_000, retry: false }).data ?? []
  const [soldBy, setSoldBy] = useState<string>(() => getSession()?.user.id ?? '')
  // Barcodes: a scanner types the code and Enter; a typed code works the same. A camera scan where the phone can.
  const [scanning, setScanning] = useState(false)
  const [scanMiss, setScanMiss] = useState<string | null>(null)
  const canScan = typeof window !== 'undefined' && 'BarcodeDetector' in window
  function addByBarcode(code: string) {
    const clean = code.replace(/\s/g, '')
    const p = clean.length >= 4 ? products.find((x) => x.barcode && x.barcode === clean) : undefined
    if (!p) return false
    add({ productId: p.id, name: p.name, quantity: 1, unit: p.unit, unitPrice: p.sellingPrice })
    setSearch('')
    setScanMiss(null)
    return true
  }
  // A scanner types fast and ends with Enter; a full code typed in also adds straight away.
  function onSearch(value: string) {
    setScanMiss(null)
    if (/^\d{8,14}$/.test(value.trim()) && addByBarcode(value.trim())) return
    setSearch(value)
  }
  // The style whose sizes/colours are being picked (a tile for a style opens this instead of adding straight away).
  const [stylePick, setStylePick] = useState<string | null>(null)
  const [customerId, setCustomerId] = useState('')
  // Held bills: park this customer's bill, serve the next one, pick it up again. Kept on this device only.
  const heldKey = `held-bills:${shopId}`
  const [held, setHeld] = useState<{ cart: CartLine[]; customerId: string; at: number }[]>(() => {
    try { return JSON.parse(localStorage.getItem(heldKey) ?? '[]') } catch { return [] }
  })
  function keepHeld(next: typeof held) {
    setHeld(next)
    try { localStorage.setItem(heldKey, JSON.stringify(next)) } catch { /* private window: held bills last until reload */ }
  }
  function holdBill() {
    if (cart.length === 0) return
    keepHeld([...held, { cart, customerId, at: Date.now() }])
    setCart([]); setCustomerId(''); setPaid(null); setSaved(null)
  }
  function resumeBill(index: number) {
    const pick = held[index]
    const rest = held.filter((_, i) => i !== index)
    keepHeld(cart.length > 0 ? [...rest, { cart, customerId, at: Date.now() }] : rest)
    setCart(pick.cart); setCustomerId(pick.customerId); setPaid(null); setSaved(null)
  }
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
  const searchRef = useRef<HTMLInputElement>(null)
  const [keysHelp, setKeysHelp] = useState(false)
  // A counter PC with a keyboard (no touch): the search box stays ready for the next item or scan.
  const keyboardDesk = typeof window !== 'undefined' && window.matchMedia?.('(min-width: 1024px) and (pointer: fine)').matches
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
  // An exchange started from an old bill: its credit note pays this much of the new bill (the rest, or change back).
  const [exchange, setExchangeState] = useState<Exchange | null>(() => currentExchange())
  const exchangeCredit = exchange ? Math.min(exchange.total, total) : 0
  const due = round2(total - exchangeCredit)
  const paidAmount = paid === null || !customer ? due : Math.min(Number(paid) || 0, due)
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
    if (keyboardDesk) setTimeout(() => searchRef.current?.focus(), 0)
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
    const result = await saveBill(shopId, cart.map((l) => ({ ...l, salespersonId: l.salespersonId || soldBy || null })), customer, paidAmount, mode, {
      site: extras.site.trim() || null, vehicleNumber: extras.vehicleNumber.trim() || null, painterId: extras.painterId || null,
      serials: Object.entries(picked).flatMap(([productId, list]) => list.map((serialNo) => ({ productId, serialNo }))),
      exchangeNoteId: exchange?.noteId ?? null, exchangeCredit,
      prescription: schedules.length > 0 ? { doctorName: rx.doctorName.trim(), doctorAddress: rx.doctorAddress.trim() || null, doctorRegNo: rx.doctorRegNo.trim() || null, patientName: rx.patientName.trim(), patientAddress: rx.patientAddress.trim() || null } : null,
    })
    setRx({ doctorName: '', doctorAddress: '', doctorRegNo: '', patientName: '', patientAddress: '' })
    setPicked({})
    setExtras({ ...extras, site: '', vehicleNumber: '' })
    setSaved({ ...result, lines: cart, paid: paidAmount, mode, customer: customer && { name: customer.name, phone: customer.phone } })
    setCart([])
    if (exchange) { endExchange(); setExchangeState(null) }
    setPaid(null)
    setCustomerId('')
    setSearch('')
    if (keyboardDesk) setTimeout(() => searchRef.current?.focus(), 0)
  }

  const canSave = total > 0 && !unpriced && !rxMissing && !serialsMissing && !overLimit
  // Desktop keys: F2 search, F4 amount paid, Ctrl+Enter save, Esc clear search, ? the key list.
  const keys = useRef({ save, canSave })
  keys.current = { save, canSave }
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      const typing = e.target instanceof HTMLElement && /^(INPUT|TEXTAREA|SELECT)$/.test(e.target.tagName)
      if (e.key === 'F2') { e.preventDefault(); searchRef.current?.focus(); searchRef.current?.select() }
      else if (e.key === 'F4') { e.preventDefault(); const el = document.getElementById('bill-paid') as HTMLInputElement | null; if (el) { el.focus(); el.select() } else document.getElementById('bill-save')?.focus() }
      else if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); if (keys.current.canSave) void keys.current.save() }
      else if (e.key === '?' && !typing) { e.preventDefault(); setKeysHelp((v) => !v) }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  return (
    <div className="flex flex-col gap-3 p-4 lg:grid lg:grid-cols-[minmax(0,1fr)_420px] lg:items-start lg:gap-4">
      {/* Desktop: items on the left, the bill on the right (always in view). A phone shows them one under the other. */}
      <div className="flex flex-col gap-3 lg:col-start-1">
      {exchange && (
        <div className="flex items-center gap-2 rounded-[14px] bg-amber-50 p-3 text-sm text-amber-950">
          <span className="flex-1">{t('exchange.banner', { note: exchange.noteNo, amount: formatRupees(exchange.total) })}</span>
          <button onClick={() => { endExchange(); setExchangeState(null) }} className="min-h-9 px-2 text-xs font-semibold underline">{t('exchange.cancel')}</button>
        </div>
      )}
      <div className="flex gap-2">
        <input
          ref={searchRef}
          autoFocus={keyboardDesk}
          value={search}
          onChange={(e) => onSearch(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Escape') { onSearch(''); return }
            if (e.key !== 'Enter' || e.ctrlKey || e.metaKey) return
            e.preventDefault()
            // A scanner's code (digits) that matches no item says so, instead of leaving the code sitting in the box.
            if (!addByBarcode(search) && /^\d{8,14}$/.test(search.trim())) setScanMiss(search.trim())
          }}
          placeholder={t('barcodeBill.search')}
          aria-label={t('search')}
          className="h-12 min-w-0 flex-1 rounded-[14px] border border-line bg-white px-4 text-[17px] outline-none focus:border-teal-600"
        />
        <button onClick={() => setKeysHelp((v) => !v)} aria-label={t('keys.title')} title={t('keys.title')} className="hidden h-12 w-12 shrink-0 rounded-[14px] border border-line bg-white font-bold text-muted lg:block">?</button>
        {canScan && <button onClick={() => setScanning(true)} aria-label={t('barcodeBill.scan')} className="h-12 shrink-0 rounded-[14px] border-2 border-teal-700 px-3 font-bold text-teal-800">{t('barcodeBill.scan')}</button>}
      </div>
      {keysHelp && (
        <div className="hidden rounded-[14px] bg-white p-3 text-sm lg:block">
          <p className="mb-1 font-bold">{t('keys.title')}</p>
          <ul className="grid grid-cols-2 gap-x-4 gap-y-1 text-muted">
            {[['F2', t('keys.search')], ['Enter', t('keys.add')], ['F4', t('keys.paid')], ['Ctrl + Enter', t('keys.save')], ['Esc', t('keys.clear')], ['?', t('keys.toggle')]].map(([k, label]) => (
              <li key={k}><kbd className="mr-2 rounded border border-line bg-[#f1f5f4] px-1.5 font-mono text-xs text-ink">{k}</kbd>{label}</li>
            ))}
          </ul>
        </div>
      )}
      {scanning && <ScanSheet onCode={(code) => { setScanning(false); if (!addByBarcode(code)) setScanMiss(code) }} onClose={() => setScanning(false)} />}
      {scanMiss && <p className="rounded-xl bg-red-50 p-2 text-sm font-semibold text-due">{t('barcodeBill.notFound', { code: scanMiss })}</p>}
      <div className="grid grid-cols-3 gap-2 lg:grid-cols-4 xl:grid-cols-5">
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

      </div>
      <div className="flex flex-col gap-3 lg:sticky lg:top-20 lg:col-start-2 lg:row-start-1 lg:max-h-[calc(100dvh-6rem)] lg:overflow-y-auto">
      {held.length > 0 && (
        <div className="flex flex-wrap items-center gap-2 rounded-[14px] bg-amber-50 p-2 text-sm">
          <span className="font-semibold text-amber-900">{t('hold.held', { count: held.length })}</span>
          {held.map((h, i) => (
            <button key={h.at} onClick={() => resumeBill(i)} className="min-h-9 rounded-full border border-amber-300 bg-white px-3 font-semibold">
              {(customers.find((c) => c.id === h.customerId)?.name ?? t('hold.walkIn'))} · {formatRupees(round2(h.cart.reduce((n, l) => n + round2(l.quantity * l.unitPrice), 0)))}
            </button>
          ))}
        </div>
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
          <div key={`${l.productId ?? l.name}-${i}`} className="flex items-center gap-2 border-b border-[#edf2f1] py-2 last:border-0 lg:py-1.5 lg:[&>button]:h-8 lg:[&>button]:w-8">
            <div className="min-w-0 flex-1 lg:flex lg:flex-wrap lg:items-center lg:gap-x-2 lg:[&>div:not(:first-child)]:basis-full lg:[&>p]:basis-full">
              <div className="font-semibold">{l.name}{(() => { const s = products.find((p) => p.id === l.productId)?.schedule; return s && s !== 'g' ? <span className="ml-1 whitespace-nowrap rounded bg-red-50 px-1 text-xs text-due">{t('rx.badge', { schedule: s.toUpperCase() })}</span> : null })()}</div>
              <label className="flex items-center gap-1 text-xs text-muted">
                ₹
                <input
                  value={l.unitPrice || ''}
                  onChange={(e) => setPrice(i, Number(e.target.value) || 0)}
                  inputMode="decimal"
                  aria-label={`${t('price')}: ${l.name}`}
                  placeholder={t('price')}
                  className={`h-11 w-16 rounded-md border px-1 lg:h-8 ${l.unitPrice > 0 ? 'border-line' : 'border-udhaar'}`}
                />
                /{t(`units.${l.unit}`)}
              </label>
              {sellers.length > 1 && (
                <select value={l.salespersonId || soldBy} onChange={(e) => setCart((c) => c.map((x, j) => (j === i ? { ...x, salespersonId: e.target.value === soldBy ? null : e.target.value } : x)))}
                  aria-label={`${t('sales.soldBy')}: ${l.name}`} className="mt-1 h-8 max-w-40 rounded-md border border-line bg-white px-1 text-xs text-muted">
                  {sellers.map((s) => <option key={s.userId} value={s.userId}>{t('sales.by', { name: s.name })}</option>)}
                </select>
              )}
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
          {sellers.length > 1 && (
            <label className="mt-2 flex items-center justify-between gap-2 text-sm">
              <span className="font-semibold text-muted">{t('sales.soldBy')}</span>
              <select value={soldBy} onChange={(e) => setSoldBy(e.target.value)} className="h-11 rounded-xl border border-line bg-white px-2">
                {sellers.map((s) => <option key={s.userId} value={s.userId}>{s.name}</option>)}
              </select>
            </label>
          )}
          {exchangeCredit > 0 && (
            <div className="mt-2 flex justify-between text-sm font-semibold text-amber-900">
              <span>{t('exchange.credit', { note: exchange!.noteNo })}</span><span>− {formatRupees(exchangeCredit)}</span>
            </div>
          )}
          {exchange && exchange.total > total && total > 0 && <p className="mt-1 text-sm font-semibold text-amber-900">{t('exchange.change', { amount: formatRupees(round2(exchange.total - total)) })}</p>}
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
            <Field id="bill-paid" label={t('paidNow')} value={paid ?? String(total)} onChange={(e) => setPaid(e.target.value)} inputMode="decimal" />
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
          <div className="flex gap-2">
            <button type="button" onClick={holdBill} className="min-h-12 shrink-0 rounded-2xl border-2 border-teal-700 px-4 font-bold text-teal-800">{t('hold.button')}</button>
            <Button className="flex-1" id="bill-save" onClick={save} disabled={!canSave}>
              {mode === 'upi' && paidAmount > 0 ? t('upiPay.save', { amount: formatRupees(total) }) : `${t('saveBill')} · ${formatRupees(total)}`}
            </Button>
          </div>
        </div>
      )}

      </div>
      {cart.length > 0 && !billInView && (
        <button onClick={() => billRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })}
          className="fixed inset-x-0 bottom-[calc(68px+env(safe-area-inset-bottom))] z-10 mx-auto flex h-14 lg:hidden w-[calc(100%-2rem)] max-w-[34rem] items-center justify-between rounded-2xl bg-teal-700 px-4 font-bold text-white shadow-lg">
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

/** The phone camera reading a barcode (BarcodeDetector, in Chrome on Android and some others). */
function ScanSheet({ onCode, onClose }: { onCode: (code: string) => void; onClose: () => void }) {
  const { t } = useTranslation()
  const video = useRef<HTMLVideoElement>(null)
  const [error, setError] = useState('')
  useEffect(() => {
    let stream: MediaStream | null = null
    let timer = 0
    let done = false
    ;(async () => {
      try {
        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment' } })
        if (!video.current) return
        video.current.srcObject = stream
        await video.current.play()
        const Detector = (window as unknown as { BarcodeDetector: new (o: { formats: string[] }) => { detect: (v: HTMLVideoElement) => Promise<{ rawValue: string }[]> } }).BarcodeDetector
        const detector = new Detector({ formats: ['ean_13', 'ean_8', 'upc_a', 'upc_e', 'code_128'] })
        timer = window.setInterval(async () => {
          if (done || !video.current) return
          const found = await detector.detect(video.current).catch(() => [])
          if (found[0]?.rawValue) { done = true; onCode(found[0].rawValue) }
        }, 300)
      } catch {
        setError(t('barcodeBill.noCamera'))
      }
    })()
    return () => { done = true; window.clearInterval(timer); stream?.getTracks().forEach((track) => track.stop()) }
  }, [onCode, t])
  return (
    <Sheet title={t('barcodeBill.scan')} onClose={onClose}>
      {error ? <p className="text-sm text-due">{error}</p> : <video ref={video} muted playsInline className="w-full rounded-xl bg-black" />}
    </Sheet>
  )
}

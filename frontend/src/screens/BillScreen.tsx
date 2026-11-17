import { shownLocale } from '../i18n'
import { GodownPicker } from '../components/GodownPicker'
import { lazy, Suspense, useEffect, useMemo, useRef, useState } from 'react'
import { CashChange, CustomerFinder } from '../components/CustomerFinder'
import { CashLimitNote } from '../components/CashLimitNote'
import { billSeconds } from '../lib/billTimer'
import { GST_STATES } from '../lib/states'
import { nameMatches, splitQuantity } from '../lib/nameMatch'
import { getSession } from '../lib/session'
import { ApiError, api } from '../lib/api'
import { useQuery } from '@tanstack/react-query'
import { currentExchange, endExchange, startExchange, type Exchange } from '../lib/exchange'
import { groupStyles, sortVariants, variantLabel } from '../lib/styles'
import { billNameFor } from '../lib/billNames'
import { billTax, garmentRate } from '../lib/gst'
import { bestOffer, isRunning, offerPrice, useOffers } from '../lib/offers'
import { cameraPossible, normBarcode } from '../lib/barcode'
const UnknownBarcode = lazy(() => import('../components/UnknownBarcode').then((module) => ({ default: module.UnknownBarcode })))
const ScanSheet = lazy(() => import('../components/ScanSheet').then((module) => ({ default: module.ScanSheet })))
import { enqueue } from '../lib/outbox'
import { useTranslation } from 'react-i18next'
import { ItemCard } from '../components/ItemCard'
import { imageUrl } from '../lib/images'
import { Button, Field, Input, Sheet, btn } from '../components/ui'
import { addCustomer, saveBill, useCustomers, usePopularOrder, useProducts, type CartLine } from '../lib/data'
import { formatDateTime, formatRupees, whatsAppLink } from '../lib/format'
import { round2 } from '../lib/local'
import type { PaymentMode, Unit } from '../lib/types'
import { useShopSettings, shopTypes, isTrade, sendsToSites, upiUri } from '../lib/reports'
import { navigate } from '../router'
import { useSerialsInStock } from '../lib/serials'
import { ask, tell } from '../components/Ask'

// Cement, paint and hardware extras load only for those shops, keeping the kirana counter's first load small.
const TradeBillPanel = lazy(() => import('./TradeBill').then((module) => ({ default: module.TradeBillPanel })))
const TradeDelivery = lazy(() => import('./TradeBill').then((module) => ({ default: module.TradeDelivery })))

/** What a bill's delivery section holds, kept with a held bill. */
interface Delivery { deliveredTo: string; site: string; vehicleNumber: string }

/** Item billing: tap tiles, adjust quantities, choose who pays and how much now. The rest goes on khata. */
export function BillScreen({ shopId, shopName, isOwner = true }: { shopId: string; shopName: string; isOwner?: boolean }) {
  const { t } = useTranslation()
  const { products } = useProducts(shopId)
  const rank = usePopularOrder(shopId)
  const [search, setSearch] = useState('')
  // "2*aata" or "2*" then a scan: add two. The rest is matched in either script ("aata" finds "आटा").
  const { quantity: pendingQty, rest: searchText } = splitQuantity(search)
  const query = searchText.trim().toLowerCase()
  // Most-sold items first (the tiles the thumb needs), then the rest by name; typing filters.
  const tiles = products
    .filter((p) => !query || p.name.toLowerCase().includes(query) || (p.styleName ?? '').toLowerCase().includes(query) || nameMatches(p.name, query) || (!!p.styleName && nameMatches(p.styleName, query)))
    .sort((a, b) => (rank.get(a.id) ?? 1e9) - (rank.get(b.id) ?? 1e9) || a.name.localeCompare(b.name))
  // Cut after grouping sizes into one tile, so a clothes shop isn't left with five tiles. A wide screen has room for more.
  const wide = typeof window !== 'undefined' && window.matchMedia('(min-width: 1024px)').matches
  const groups = groupStyles(tiles).slice(0, query || wide ? 30 : 12)
  const { customers } = useCustomers(shopId)
  const [cart, setCart] = useState<CartLine[]>([])
  // When this bill's first item went in. 0: picked up from hold, so its time can't be known.
  const startedAt = useRef<number | null>(null)
  useEffect(() => {
    if (cart.length === 0) startedAt.current = null
    else startedAt.current ??= Date.now()
  }, [cart.length])
  // Who is selling: everyone with access to the shop. The bill's choice goes on every line; a line can name another.
  const sellers = useQuery({ queryKey: [shopId, 'sellers'], queryFn: () => api<{ userId: string; name: string }[]>('/v1/shop/sellers'), staleTime: 300_000, retry: false }).data ?? []
  const [soldBy, setSoldBy] = useState<string>(() => getSession()?.user.id ?? '')
  // Barcodes: a scanner types the code and Enter; a typed code works the same. A camera scan where the phone can.
  const [scanning, setScanning] = useState(false)
  const [scanMiss, setScanMiss] = useState<string | null>(null)
  // Every phone with a camera: its own barcode reader where it has one, else the app's backup reader.
  const canScan = cameraPossible()
  function addByBarcode(code: string) {
    const clean = normBarcode(code)
    const p = clean.length >= 4 ? products.find((x) => x.barcode && normBarcode(x.barcode) === clean) : undefined
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
  // An exchange's credit is that customer's, so the new bill starts on their name.
  const [customerId, setCustomerId] = useState(() => currentExchange()?.customerId ?? '')
  // Held bills: park this customer's bill, serve the next one, pick it up again. Kept on this device only.
  const heldKey = `held-bills:${shopId}`
  const [held, setHeld] = useState<{ cart: CartLine[]; customerId: string; at: number; delivery?: Delivery }[]>(() => {
    try { return JSON.parse(localStorage.getItem(heldKey) ?? '[]') } catch { return [] }
  })
  function keepHeld(next: typeof held) {
    setHeld(next)
    try { localStorage.setItem(heldKey, JSON.stringify(next)) } catch { /* private window: held bills last until reload */ }
  }
  // An estimate the customer agreed to: its lines come into this bill (read once), and saving the bill marks it billed.
  const [fromEstimate, setFromEstimate] = useState<{ id: string; number: number } | null>(null)
  const [estimateSaved, setEstimateSaved] = useState<{ id: string; number: number } | null>(null)
  // A sale order being delivered: what it still has to go fills the cart (change it for a part delivery).
  const [fromOrder, setFromOrder] = useState<{ id: string; number: number } | null>(null)
  const [orderSaved, setOrderSaved] = useState<number | null>(null)
  useEffect(() => {
    let raw: string | null = null
    try { raw = sessionStorage.getItem('gk-sale-order'); sessionStorage.removeItem('gk-sale-order') } catch { /* no storage */ }
    if (!raw) return
    const o = JSON.parse(raw) as { id: string; number: number; customerId: string | null; lines: { productId: string | null; name: string; quantity: number; unit: Unit; unitPrice: number }[] }
    startedAt.current = 0
    setCart(o.lines.map((l) => ({ productId: l.productId, name: l.name, quantity: l.quantity, unit: l.unit, unitPrice: l.unitPrice })))
    setCustomerId(o.customerId ?? '')
    setFromOrder({ id: o.id, number: o.number })
  }, [])
  async function saveOrder() {
    if (cart.length === 0 || !customerId) return
    try {
      const o = await api<{ number: number }>('/v1/sale-orders', 'POST', {
        id: crypto.randomUUID(), customerId,
        lines: cart.map((l) => ({ productId: l.productId, name: l.name, quantity: l.quantity, unit: l.unit, unitPrice: l.unitPrice })),
      })
      setOrderSaved(o.number)
      setCart([]); setCustomerId(''); setPaid(null); setSaved(null); setFromEstimate(null)
    } catch (err) {
      await ask(err instanceof Error ? err.message : String(err))
    }
  }
  useEffect(() => {
    let raw: string | null = null
    try { raw = sessionStorage.getItem('gk-estimate'); sessionStorage.removeItem('gk-estimate') } catch { /* no storage */ }
    if (!raw) return
    const e = JSON.parse(raw) as { id: string; number: number; customerId: string | null; lines: { productId: string | null; name: string; quantity: number; unit: Unit; unitPrice: number }[] }
    startedAt.current = 0
    setCart(e.lines.map((l) => ({ productId: l.productId, name: l.name, quantity: l.quantity, unit: l.unit, unitPrice: l.unitPrice })))
    setCustomerId(e.customerId ?? '')
    setFromEstimate({ id: e.id, number: e.number })
  }, [])
  async function saveEstimate() {
    if (cart.length === 0) return
    try {
      const e = await api<{ id: string; number: number }>('/v1/estimates', 'POST', {
        id: crypto.randomUUID(), customerId: customerId || null,
        lines: cart.map((l) => ({ productId: l.productId, name: l.name, quantity: l.quantity, unit: l.unit, unitPrice: l.unitPrice })),
      })
      setEstimateSaved({ id: e.id, number: e.number })
      setCart([]); setCustomerId(''); setPaid(null); setSaved(null); setFromEstimate(null)
    } catch (err) {
      await ask(err instanceof Error ? err.message : String(err))
    }
  }
  function holdBill() {
    if (cart.length === 0) return
    keepHeld([...held, { cart, customerId, at: Date.now(), delivery: currentDelivery() }])
    setCart([]); setCustomerId(''); setPaid(null); setSaved(null)
  }
  function resumeBill(index: number) {
    const pick = held[index]
    startedAt.current = 0
    const rest = held.filter((_, i) => i !== index)
    keepHeld(cart.length > 0 ? [...rest, { cart, customerId, at: Date.now(), delivery: currentDelivery() }] : rest)
    setCart(pick.cart); setCustomerId(pick.customerId); setPaid(null); setSaved(null)
    setDelivery(pick.delivery)
  }
  const [paid, setPaid] = useState<string | null>(null)
  const [mode, setMode] = useState<PaymentMode>('cash')
  const [received, setReceived] = useState('')
  // A loose item, or a charge line ("Transport") with its name filled in.
  const [loose, setLoose] = useState<false | { name: string; unit: Unit }>(false)
  const settings = useShopSettings(shopId).data
  const shopType = settings?.type ?? 'kirana'
  const types = shopTypes(settings)
  const [extras, setExtras] = useState({ site: '', vehicleNumber: '', painterId: '' })
  // A GST shop delivering to another state: the place of supply is that state, so the bill charges IGST.
  const [deliveredTo, setDeliveredTo] = useState('')
  const shopState = settings?.gstin && settings.gstin.length >= 2 ? settings.gstin.slice(0, 2) : null
  // Most sales leave over the counter, so delivery stays shut until the bill needs it. Every bill starts without it:
  // a state left over from the last sale would change this bill's tax without anyone seeing.
  const [deliveryOpen, setDeliveryOpen] = useState(false)
  const picksState = !!shopState && !settings?.gstComposition
  const sendsGoods = sendsToSites(shopType)
  const deliversGoods = settings?.deliversGoods ?? sendsGoods
  function currentDelivery(): Delivery | undefined {
    return deliveryOpen ? { deliveredTo, site: extras.site, vehicleNumber: extras.vehicleNumber } : undefined
  }
  function setDelivery(next?: Delivery) {
    setDeliveryOpen(!!next)
    setDeliveredTo(next?.deliveredTo ?? '')
    setExtras((current) => ({ ...current, site: next?.site ?? '', vehicleNumber: next?.vehicleNumber ?? '' }))
  }
  // A saved, held or emptied bill takes its delivery with it.
  useEffect(() => {
    if (cart.length > 0) return
    setDelivery()
    stateOffered.current = ''
  }, [cart.length])
  // Another customer's bill isn't going to this one's site.
  function pickCustomer(id: string) {
    if (customerId && id !== customerId) setDelivery()
    stateOffered.current = ''
    setCustomerId(id)
  }
  // Where this customer's tax goes, whatever this shop has chosen about delivery. Online only: offline it's the shop's state.
  const buyer = useQuery({
    queryKey: [shopId, 'bill-details', customerId],
    queryFn: () => api<{ gstin: string | null; address: string | null; state: string | null }>(`/v1/customers/${customerId}/bill-details`),
    enabled: !!shopState && !!customerId,
  }).data
  // The server follows a registered buyer's GSTIN over anything chosen here, so the screen must say the same.
  const buyerState = buyer?.gstin && buyer.gstin.length === 15 && GST_STATES[buyer.gstin.slice(0, 2)] ? buyer.gstin.slice(0, 2) : null
  const stateOffered = useRef('')
  useEffect(() => {
    if (!buyer || stateOffered.current === customerId) return
    stateOffered.current = customerId
    if (picksState && !buyerState && buyer.state && buyer.state !== shopState && !deliveryOpen) {
      setDeliveryOpen(true)
      setDeliveredTo(buyer.state)
    }
  }, [buyer, customerId, picksState, buyerState, shopState, deliveryOpen])
  const earlierSites = useQuery({
    queryKey: [shopId, 'sites', customerId],
    queryFn: () => api<{ site: string }[]>(`/v1/customers/${customerId}/sites`),
    enabled: sendsGoods && deliversGoods && !!customerId,
  }).data?.filter((s) => s.site).slice(0, 2).map((s) => s.site).join(', ')
  // Schedule H, H1 and X medicines need the doctor and patient on the bill; H1 and X also the doctor's address (the register).
  const [rx, setRx] = useState({ doctorName: '', doctorAddress: '', doctorRegNo: '', patientName: '', patientAddress: '' })
  // A link to go with the bill on WhatsApp. It waits for the sync, and offline the message simply goes without one.
  const [savedId, setSavedId] = useState<string | null>(null)
  const billLink = useQuery({
    queryKey: ['bill-link', savedId],
    queryFn: () => api<{ shopSlug: string; billId: string; token: string }>(`/v1/bills/${savedId}/link`, 'POST'),
    enabled: !!savedId, staleTime: Infinity, retry: 3, retryDelay: 1500,
  }).data
  const [saved, setSaved] = useState<{ id: string; total: number; lines: CartLine[]; paid: number; mode: PaymentMode; customer: { name: string; phone: string | null } | null; split?: boolean } | null>(null)

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
  // The owner's planned offers. A line under one is the owner's price, so the helper's limit doesn't apply to it.
  const offers = useOffers(shopId).data ?? []
  // Only while the offer still runs: a held bill picked up after the offer ended goes back under the helper's limit, as on the server.
  const underOffer = (l: CartLine) => !!l.offerId && offers.some((o) => o.id === l.offerId && isRunning(o, Date.now()))
  const overLimit = !isOwner && cart.some((l) => !underOffer(l) && (offOf(l)?.percent ?? 0) > staffLimit)
  const total = useMemo(() => round2(cart.reduce((sum, l) => sum + round2(l.quantity * l.unitPrice), 0)), [cart])
  // A discount on the whole bill, if the shop wants one: spread over the lines by their share, so each line's price, its
  // GST and the helper's limit all stay right. "remove" puts the prices back as they were.
  const [billOff, setBillOff] = useState<{ before: CartLine[]; amount: number } | null>(null)
  const [offForm, setOffForm] = useState<{ value: string; percent: boolean } | null>(null)
  useEffect(() => { if (billOff && cart.length !== billOff.before.length) setBillOff(null) }, [cart, billOff])
  function applyBillOff() {
    if (!offForm) return
    const value = Number(offForm.value)
    const off = round2(offForm.percent ? (total * value) / 100 : value)
    if (!(off > 0) || off >= total) { void ask(t('billOff.tooMuch')); return }
    const factor = (total - off) / total
    const next = cart.map((l) => ({ ...l, unitPrice: round2(l.unitPrice * factor) }))
    // Rounding can leave a few paise: the line with the most money takes them, if that keeps its price whole.
    const gap = round2(total - off - next.reduce((sum, l) => sum + round2(l.quantity * l.unitPrice), 0))
    const big = next.reduce((best, l, i) => (round2(l.quantity * l.unitPrice) > round2(next[best].quantity * next[best].unitPrice) ? i : best), 0)
    if (gap !== 0 && next[big].quantity === 1) next[big] = { ...next[big], unitPrice: round2(next[big].unitPrice + gap) }
    setBillOff({ before: cart, amount: round2(total - next.reduce((sum, l) => sum + round2(l.quantity * l.unitPrice), 0)) })
    setCart(next)
    setOffForm(null)
  }
  const customer = customers.find((c) => c.id === customerId) ?? null
  // An exchange started from an old bill: its credit note pays this much of the new bill (the rest, or change back).
  const [exchange, setExchangeState] = useState<Exchange | null>(() => currentExchange())
  const [askCustomer, setAskCustomer] = useState(false)
  // An exchange slip from another counter: its number (or QR) brings its credit and its customer onto this bill.
  const [slipOpen, setSlipOpen] = useState(false)
  const [slipNo, setSlipNo] = useState('')
  const [slipError, setSlipError] = useState('')
  const [slipBusy, setSlipBusy] = useState(false)
  async function applySlip() {
    setSlipBusy(true)
    setSlipError('')
    try {
      const s = await api<{ id: string; noteNo: string; amount: number; balance: number; status: string; validTill: string; customerId: string | null; usedOnBillNo: string | null }>(`/v1/exchange-slips/find?no=${encodeURIComponent(slipNo.trim())}`)
      if (s.status === 'used') setSlipError(t('slip.used', { bill: s.usedOnBillNo ?? '' }))
      else if (s.status === 'expired') setSlipError(t('slip.expired', { date: new Date(`${s.validTill}T00:00`).toLocaleDateString(shownLocale()) }))
      else {
        const ex = { noteId: s.id, noteNo: s.noteNo, total: s.balance, customerId: s.customerId }
        startExchange(ex)
        setExchangeState(ex)
        if (s.customerId) setCustomerId(s.customerId)
        setSlipOpen(false)
        setSlipNo('')
      }
    } catch (e) {
      setSlipError(e instanceof ApiError && e.status === 404 ? t('slip.notFound') : e instanceof Error ? e.message : t('error'))
    } finally {
      setSlipBusy(false)
    }
  }
  const exchangeCredit = exchange ? Math.min(exchange.total, total) : 0
  const due = round2(total - exchangeCredit)
  const paidAmount = paid === null || !customer ? due : Math.min(Number(paid) || 0, due)
  // Paid two ways: the UPI part typed, the rest of what is paid now in cash. null: one way, by mode.
  const [upiPart, setUpiPart] = useState<string | null>(null)
  const upiPaid = upiPart === null ? 0 : Math.min(Math.max(Number(upiPart) || 0, 0), paidAmount)
  const cashPaid = round2(paidAmount - upiPaid)
  const split = upiPart !== null && upiPaid > 0 && cashPaid > 0
  // The other way of a split: UPI or card. And the UPI reference (UTR) the customer's app shows, if typed: proof on the bill.
  const [splitWith, setSplitWith] = useState<'upi' | 'card'>('upi')
  const [utr, setUtr] = useState('')
  const upiNow = upiPart === null ? mode === 'upi' && paidAmount > 0 : split && splitWith === 'upi'
  const reference = upiNow && utr.length >= 6 ? utr : null
  const schedules = cart.map((l) => products.find((p) => p.id === l.productId)?.schedule).filter((s) => s === 'h' || s === 'h1' || s === 'x')
  const needsAddress = schedules.some((s) => s === 'h1' || s === 'x')
  // Serialised units go out by serial number, one per unit (the warranty runs from this bill).
  const [picked, setPicked] = useState<Record<string, string[]>>({})
  const serialLines = cart.filter((l) => l.productId && products.find((p) => p.id === l.productId)?.tracksSerials)
  const serialsMissing = serialLines.some((l) => (picked[l.productId!] ?? []).length !== l.quantity)
  const rxMissing = schedules.length > 0 && (rx.doctorName.trim().length < 2 || rx.patientName.trim().length < 2 || (needsAddress && rx.doctorAddress.trim().length < 3))

  function add(line: CartLine) {
    setSaved(null)
    // At its regular price, an item a running offer covers goes in at the offer's price, with the offer's name.
    const item = line.productId ? products.find((p) => p.id === line.productId) : undefined
    const offer = item && line.unitPrice === item.sellingPrice ? bestOffer(offers, item) : null
    if (offer && item) line = { ...line, unitPrice: offerPrice(offer, item.sellingPrice), offerId: offer.id, offerName: offer.name }
    const qty = pendingQty ?? 1
    if (pendingQty) { line = { ...line, quantity: pendingQty }; setSearch('') }
    setCart((current) => {
      // A tinted tin shares its base's product but is its own line (its own shade and rate).
      const index = line.productId ? current.findIndex((l) => l.productId === line.productId && l.name === line.name) : -1
      if (index < 0) return [...current, line]
      return current.map((l, i) => (i === index ? { ...l, quantity: round2(l.quantity + qty) } : l))
    })
    if (keyboardDesk) setTimeout(() => searchRef.current?.focus(), 0)
  }

  // A USB or serial weighing scale (Chrome on computers): weigh a kg or g line instead of typing it.
  const hasScale = typeof navigator !== 'undefined' && 'serial' in navigator
  const [weighing, setWeighing] = useState<number | null>(null)
  async function weigh(index: number, unit: Unit, choose = false) {
    setWeighing(index)
    try {
      const { readScale } = await import('../lib/scale')
      const kg = await readScale(choose)
      setQuantity(index, unit === 'g' ? Math.round(kg * 1000) : kg)
    } catch (e) {
      if (!(e instanceof Error && e.name === 'NotFoundError')) void tell(`${t('scale.failed')} ${e instanceof Error ? e.message : String(e)}`)
    } finally {
      setWeighing(null)
    }
  }

  function setQuantity(index: number, quantity: number) {
    // A typed weight is no longer whole bags.
    setCart((current) => (quantity <= 0 ? current.filter((_, i) => i !== index) : current.map((l, i) => (i === index ? { ...l, quantity, bags: null } : l))))
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
    // A price changed by hand is the counter's, not the offer's: offers and counter discounts don't add up.
    setCart((current) => current.map((l, i) => (i === index ? { ...l, unitPrice, offerId: null, offerName: null } : l)))
  }

  async function save() {
    const result = await saveBill(shopId, cart.map((l) => ({ ...l, salespersonId: l.salespersonId || soldBy || null })), customer, paidAmount, mode, {
      site: extras.site.trim() || null, vehicleNumber: extras.vehicleNumber.trim() || null, painterId: extras.painterId || null,
      serials: Object.entries(picked).flatMap(([productId, list]) => list.map((serialNo) => ({ productId, serialNo }))),
      exchangeNoteId: exchange?.noteId ?? null, exchangeCredit,
      payments: split ? [{ mode: 'cash', amount: cashPaid }, { mode: splitWith, amount: upiPaid, reference: splitWith === 'upi' ? reference : null }]
        : reference ? [{ mode: 'upi', amount: paidAmount, reference }] : undefined, placeOfSupply: (picksState && deliveredTo) || null, entrySeconds: billSeconds(startedAt.current, Date.now()), withoutGst: picksState && withoutGst, godownId: fromGodown || null,
      prescription: schedules.length > 0 ? { doctorName: rx.doctorName.trim(), doctorAddress: rx.doctorAddress.trim() || null, doctorRegNo: rx.doctorRegNo.trim() || null, patientName: rx.patientName.trim(), patientAddress: rx.patientAddress.trim() || null } : null,
    })
    setRx({ doctorName: '', doctorAddress: '', doctorRegNo: '', patientName: '', patientAddress: '' })
    setPicked({})
    setSavedId(result.id)
    setUpiPart(null)
    setUtr('')
    setBillOff(null)
    setOffForm(null)
    setWithoutGst(false)
    // In the same queue as the bill, after it: sent when the bill is, offline too.
    if (fromEstimate) {
      await enqueue({ id: `${fromEstimate.id}:billed`, shopId, kind: 'estimateBilled', path: `/v1/estimates/${fromEstimate.id}/billed`, body: { billId: result.id }, label: t('estimate.number', { number: fromEstimate.number }) })
      setFromEstimate(null)
    }
    setEstimateSaved(null)
    if (fromOrder) {
      await enqueue({ id: `${fromOrder.id}:${result.id}`, shopId, kind: 'saleOrderDelivered', path: `/v1/sale-orders/${fromOrder.id}/delivered`, body: { billId: result.id }, label: t('saleOrder.number', { number: fromOrder.number }) })
      setFromOrder(null)
    }
    setOrderSaved(null)
    setSaved({ ...result, lines: cart, paid: paidAmount, mode, split, customer: customer && { name: customer.name, phone: customer.phone } })
    setCart([])
    if (exchange) { endExchange(); setExchangeState(null) }
    setPaid(null)
    setReceived('')
    setCustomerId('')
    setSearch('')
    if (keyboardDesk) setTimeout(() => searchRef.current?.focus(), 0)
  }

  function printSaved() {
    if (!saved) return
    try { sessionStorage.setItem('gk-autoprint', '1') } catch { /* the print page still has its Print button */ }
    navigate(`/print/bill/${saved.id}`)
  }

  // Rule 46: a tax bill of this size to an unregistered buyer carries their address. Said before saving, not after.
  const needsBuyerAddress = !!shopState && total >= 50000 && (customer ? !!buyer && !buyer.gstin && !buyer.address : true)
  // What this bill will be, shown before it's saved: the server decides, and the screen must say the same.
  // A registered shop picks the kind of bill: a GST bill (the usual), or a bill without GST. Every new bill starts as a GST bill.
  const [withoutGst, setWithoutGst] = useState(false)
  const [fromGodown, setFromGodown] = useState('')
  const taxInvoice = picksState && !withoutGst
  const taxState = buyerState ?? (deliveredTo || shopState)
  const tax = useMemo(() => billTax(cart.map((l) => {
    const item = products.find((p) => p.id === l.productId)
    return { amount: round2(l.quantity * l.unitPrice), gstRate: item ? garmentRate(item.hsnCode, item.gstRate, l.unitPrice) : null }
  }), taxState !== shopState), [cart, products, taxState, shopState])
  const [taxOpen, setTaxOpen] = useState(false)
  const canSave = total > 0 && !unpriced && !rxMissing && !serialsMissing && !overLimit
  // Desktop keys: F2 search, F4 amount paid, Ctrl+Enter save, Esc clear search, ? the key list.
  function allOnKhata() {
    if (customer) setPaid('0')
    else document.getElementById('bill-customer')?.focus()
  }
  const keys = useRef({ save, canSave, printSaved, canPrint: false, allOnKhata })
  keys.current = { save, canSave, printSaved, canPrint: !!saved && cart.length === 0, allOnKhata }
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      const typing = e.target instanceof HTMLElement && /^(INPUT|TEXTAREA|SELECT)$/.test(e.target.tagName)
      if (e.key === 'F2') { e.preventDefault(); searchRef.current?.focus(); searchRef.current?.select() }
      else if (e.key === 'F3') { e.preventDefault(); document.getElementById('bill-customer')?.focus() }
      else if (e.key === 'F6') { e.preventDefault(); keys.current.allOnKhata() }
      else if (e.key === 'F4') { e.preventDefault(); const el = document.getElementById('bill-paid') as HTMLInputElement | null; if (el) { el.focus(); el.select() } else document.getElementById('bill-save')?.focus() }
      else if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); if (keys.current.canSave) void keys.current.save() }
      else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'p' && keys.current.canPrint) { e.preventDefault(); keys.current.printSaved() }
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
        <div className="flex items-center gap-2 rounded-[14px] bg-warn-soft p-3 text-sm text-amber-950">
          <span className="flex-1">{t('exchange.banner', { note: exchange.noteNo, amount: formatRupees(exchange.total) })}</span>
          <button onClick={() => { endExchange(); setExchangeState(null) }} className={`min-h-9 px-2 text-xs ${btn.link}`}>{t('exchange.cancel')}</button>
        </div>
      )}
      {!exchange && (
        <div className="-my-1 flex flex-wrap items-center gap-x-4">
          <button type="button" onClick={() => navigate('/find-bill')} className={`min-h-10 text-sm ${btn.link}`}>↩ {t('findBill.link')}</button>
          <button type="button" onClick={() => { setSlipOpen(!slipOpen); setSlipError('') }} className={`min-h-10 text-sm ${btn.link}`}>🎟 {t('slip.use')}</button>
        </div>
      )}
      {!exchange && slipOpen && (
        <form onSubmit={(e) => { e.preventDefault(); void applySlip() }} className="flex flex-col gap-2 rounded-[14px] bg-brand-soft p-3">
          <span className="text-sm text-muted">{t('slip.useHint')}</span>
          <div className="flex gap-2">
            <Input autoFocus value={slipNo} onChange={(e) => setSlipNo(e.target.value)} placeholder="CN/2026-27/0003" aria-label={t('slip.number')} className="min-w-0 flex-1" />
            <Button type="submit" busy={slipBusy} disabled={!slipNo.trim()}>{t('slip.apply')}</Button>
          </div>
          {slipError && <p className="text-sm text-due">{slipError}</p>}
        </form>
      )}
      <div className="flex gap-2">
        <span className="relative min-w-0 flex-1">
        <span aria-hidden className="pointer-events-none absolute left-4 top-1/2 z-10 -translate-y-1/2 text-muted">🔍</span>
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
            const code = searchText.trim()
            if (addByBarcode(code)) return
            if (/^\d{8,14}$/.test(code)) { setScanMiss(code); return }
            // A name typed: Enter adds the best match (a style opens its sizes).
            const top = query ? groups[0] : undefined
            if (top?.kind === 'style') setStylePick(top.name)
            else if (top) { add({ productId: top.product.id, name: top.product.name, quantity: 1, unit: top.product.unit, unitPrice: top.product.sellingPrice }); setSearch('') }
          }}
          placeholder={t('barcodeBill.search')}
          aria-label={t('search')}
          className="h-12 w-full rounded-2xl border border-line bg-white pl-11 pr-4 text-[17px] shadow-sm outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10"
        />
        </span>
        <button onClick={() => setKeysHelp((v) => !v)} aria-label={t('keys.title')} title={t('keys.title')} className="hidden h-12 w-12 shrink-0 rounded-[14px] border border-line bg-white font-bold text-muted lg:block">?</button>
        {canScan && <button onClick={() => setScanning(true)} aria-label={t('barcodeBill.scan')} className={`flex h-12 shrink-0 items-center gap-1.5 rounded-2xl px-4 ${btn.solid}`}><span aria-hidden>📷</span>{t('barcodeBill.scan')}</button>}
      </div>
      {keysHelp && (
        <div className="hidden rounded-[14px] bg-white p-3 text-sm lg:block">
          <p className="mb-1 font-bold">{t('keys.title')}</p>
          <ul className="grid grid-cols-2 gap-x-4 gap-y-1 text-muted">
            {[['F2', t('keys.search')], ['Enter', t('keys.add')], ['F3', t('keys.customer')], ['F6', t('keys.khata')], ['F4', t('keys.paid')], ['Ctrl + Enter', t('keys.save')], ['Ctrl + P', t('keys.print')], ['2*', t('keys.qty')], ['Esc', t('keys.clear')], ['?', t('keys.toggle')]].map(([k, label]) => (
              <li key={k}><kbd className="mr-2 rounded border border-line bg-[#f1f5f4] px-1.5 font-mono text-xs text-ink">{k}</kbd>{label}</li>
            ))}
          </ul>
        </div>
      )}
      {/* Scanning stays open for the next item; a code no item has waits below, to be put on an item or made one. */}
      {scanning && (
        <Suspense fallback={null}>
          <ScanSheet continuous onCode={(code) => { const ok = addByBarcode(code); if (!ok) setScanMiss(normBarcode(code)); return ok }} onClose={() => setScanning(false)} />
        </Suspense>
      )}
      {scanMiss && (
        <Suspense fallback={null}><UnknownBarcode shopId={shopId} code={scanMiss} products={products} onClose={() => setScanMiss(null)}
          onItem={(p) => { add({ productId: p.id, name: p.name, quantity: 1, unit: p.unit, unitPrice: p.sellingPrice }); setScanMiss(null); setSearch('') }} /></Suspense>
      )}
      <div className="grid grid-cols-3 gap-2 lg:grid-cols-4 xl:grid-cols-5 2xl:grid-cols-6">
        {groups.map((entry) => entry.kind === 'style' ? (
          <ItemCard
            key={`style-${entry.name}`}
            compact
            item={entry.items[0]}
            imageUrl={imageUrl(shopId, entry.items[0].id, entry.items[0].imageAt, 'thumb')}
            title={entry.name}
            subtitle={(() => {
              const offer = entry.items.map((p) => bestOffer(offers, p)).find(Boolean)
              return offer
                ? <span><span className="mr-1 rounded-full bg-amber-100 px-1.5 text-[11px] font-bold text-warn-ink">🏷 {offer.kind === 'percent' ? `${offer.value}%` : formatRupees(offer.value)} {t('offers.off')}</span>{t('styles.count', { count: entry.items.length })}</span>
                : t('styles.count', { count: entry.items.length })
            })()}
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
              ? ((offer) => offer
                ? <span><s className="text-muted">{formatRupees(p.sellingPrice)}</s> <b className="text-brand-strong">{formatRupees(offerPrice(offer, p.sellingPrice))}</b> <span className="rounded-full bg-amber-100 px-1.5 text-[11px] font-bold text-warn-ink">🏷 {offer.name}</span></span>
                : <span><b className="text-ink">{formatRupees(p.sellingPrice)}</b>/{t(`units.${p.unit}`)}</span>)(bestOffer(offers, p))
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
        <button onClick={() => setLoose({ name: '', unit: 'kg' })} className="flex min-h-20 flex-col items-center justify-center gap-1 rounded-card border-2 border-dashed border-brand/30 bg-brand-soft/40 p-2 font-semibold text-brand-strong hover:bg-brand-soft">
          + {t('addLooseItem')}
        </button>
      </div>

      {stylePick && (
        <Sheet title={stylePick} onClose={() => setStylePick(null)}>
          <p className="-mt-1 mb-2 text-sm text-muted">{t('styles.pick')}</p>
          <div className="grid grid-cols-3 gap-2">
            {sortVariants(products.filter((p) => p.styleName?.trim() === stylePick)).map((p) => {
              const out = !p.isService && !p.uncounted && p.stock <= 0
              return (
                <button key={p.id} onClick={() => { add({ productId: p.id, name: p.name, quantity: 1, unit: p.unit, unitPrice: p.sellingPrice }); setStylePick(null) }}
                  className={`flex min-h-16 flex-col items-center justify-center rounded-xl border-2 px-1 py-2 ${out ? 'border-line opacity-60' : 'border-brand'}`}>
                  <span className="text-lg font-extrabold">{variantLabel(p) || p.name}</span>
                  <span className="text-xs text-muted">{(() => {
                    // A running offer shows here too, so the counter sees the price the bill will use.
                    const offer = p.sellingPrice > 0 ? bestOffer(offers, p) : null
                    return p.sellingPrice <= 0 ? t('stock.noPrice') : offer ? <><s>{formatRupees(p.sellingPrice)}</s> <b className="text-brand-strong">{formatRupees(offerPrice(offer, p.sellingPrice))}</b></> : formatRupees(p.sellingPrice)
                  })()} · {p.uncounted ? t('stock.notCounted') : out ? t('stock.out') : t('styles.left', { count: p.stock })}</span>
                </button>
              )
            })}
          </div>
        </Sheet>
      )}

      </div>
      <div className="flex flex-col gap-3 lg:sticky lg:top-20 lg:col-start-2 lg:row-start-1 lg:max-h-[calc(100dvh-6rem)] lg:overflow-y-auto">
      {held.length > 0 && (
        <div className="flex flex-wrap items-center gap-2 rounded-[14px] bg-warn-soft p-2 text-sm">
          <span className="font-semibold text-warn-ink">{t('hold.held', { count: held.length })}</span>
          {held.map((h, i) => (
            <button key={h.at} onClick={() => resumeBill(i)} className="min-h-9 rounded-full border border-amber-300 bg-white px-3 font-semibold">
              {(customers.find((c) => c.id === h.customerId)?.name ?? t('hold.walkIn'))} · {formatRupees(round2(h.cart.reduce((n, l) => n + round2(l.quantity * l.unitPrice), 0)))}
            </button>
          ))}
        </div>
      )}
      <div ref={billRef} className="rounded-card bg-white p-3">
        {cart.length === 0 && (
          <div className="flex flex-col items-center gap-2 py-4 text-center text-muted">
            <p>{saved ? `${t('saved')} ${formatRupees(saved.total)}` : t('emptyCart')}</p>
          {estimateSaved && (
            <p className="flex items-center justify-between gap-2 rounded-xl bg-brand-soft p-2 text-sm text-brand-deep">
              <span>{t('estimate.saved', { number: estimateSaved.number })}</span>
              <button type="button" onClick={() => navigate(`/print/estimate/${estimateSaved.id}`)} className={`min-h-11 shrink-0 ${btn.link}`}>{t('estimate.open')}</button>
            </p>
          )}
            {saved && saved.paid > 0 && (
              <p className={`rounded-full px-3 py-1 text-sm font-semibold ${saved.mode === 'upi' ? 'bg-brand-soft text-brand-strong' : 'bg-warn-soft text-warn-ink'}`}>
                {saved.split || saved.mode === 'card' ? `${t('print.paid')} ${formatRupees(saved.paid)}${saved.split ? '' : ` (${t('card')})`}`
                  : t(saved.mode === 'upi' ? 'upiPay.paidUpi' : 'upiPay.paidCash', { amount: formatRupees(saved.paid) })}
              </p>
            )}
            {saved && (
              <a
                className="flex h-12 items-center justify-center rounded-2xl bg-[#15803d] px-5 font-bold text-white"
                href={whatsAppLink(saved.customer?.phone ?? null, [
                  billReceipt(t, shopName, saved, (l) => billNameFor(products.find((p) => p.id === l.productId)) ?? l.name),
                  billLink?.billId === saved.id ? `${window.location.origin}/#/b/${billLink.shopSlug}/${billLink.billId}?t=${billLink.token}` : null,
                ].filter(Boolean).join('\n'))}
                target="_blank"
                rel="noreferrer"
              >
                {t('billReceipt.send')}
              </a>
            )}
            {saved && (
              <span className="flex flex-wrap items-center justify-center gap-3">
                <button onClick={() => printSaved()} className={`min-h-11 rounded-xl px-4 ${btn.outline}`}>{t('print.printNow')}</button>
                <button onClick={() => navigate(`/print/bill/${saved.id}`)} className={`min-h-11 ${btn.link}`}>{t('print.viewBill')}</button>
              </span>
            )}
          </div>
        )}
        {cart.map((l, i) => (
          <div key={`${l.productId ?? l.name}-${i}`} className="flex items-center gap-2 border-b border-[#edf2f1] py-2 last:border-0 lg:py-1.5 lg:[&>button]:h-8 lg:[&>button]:w-8">
            <div className="min-w-0 flex-1 lg:flex lg:flex-wrap lg:items-center lg:gap-x-2 lg:[&>div:not(:first-child)]:basis-full lg:[&>p]:basis-full">
              <div className="font-semibold">{l.name}{(() => { const s = products.find((p) => p.id === l.productId)?.schedule; return s && s !== 'g' ? <span className="ml-1 whitespace-nowrap rounded bg-due-soft px-1 text-xs text-due">{t('rx.badge', { schedule: s.toUpperCase() })}</span> : null })()}</div>
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
                  {offOf(l) && (
                    <>
                      <span className={`text-xs font-semibold ${!isOwner && offOf(l)!.percent > staffLimit ? 'text-due' : 'text-brand-strong'}`}>
                        {t('discount.off', { amount: formatRupees(offOf(l)!.amount), percent: offOf(l)!.percent })}
                      </span>
                      <button type="button" onClick={() => setPrice(i, regularOf(l))} className={`h-8 px-1 text-xs ${btn.quiet}`}>{t('discount.undo')}</button>
                    </>
                  )}
                </div>
              )}
              {l.offerId && (
                <p className="mt-1 flex items-center gap-2 text-xs font-semibold text-brand-strong">
                  <span className="rounded-full bg-brand-soft px-2 py-0.5">🏷 {t('offers.tag', { name: l.offerName })}</span>
                  <button type="button" onClick={() => setPrice(i, regularOf(l))} className={`min-h-8 ${btn.quiet}`}>{t('offers.remove')}</button>
                </p>
              )}
              {!isOwner && !underOffer(l) && (offOf(l)?.percent ?? 0) > staffLimit && <p className="text-xs font-semibold text-due">{t('discount.overLimit', { percent: staffLimit })}</p>}
              {(() => {
                // Medicines run on thin margins: warn when this price, before GST, is at or under what the item cost.
                const p = products.find((x) => x.id === l.productId)
                if (!p || p.costPrice == null || !(l.unitPrice > 0)) return null
                const net = l.unitPrice / (1 + (garmentRate(p.hsnCode, p.gstRate, l.unitPrice) ?? 0) / 100)
                const margin = ((net - p.costPrice) / net) * 100
                return margin < 5 ? <p className={`text-xs font-semibold ${margin <= 0 ? 'text-due' : 'text-warn'}`}>{t(margin <= 0 ? 'pmonth.belowCost' : 'pmonth.thinMargin', { percent: Math.round(margin * 10) / 10 })}</p> : null
              })()}
              {(() => {
                // A clothes line priced above ₹2,500 a piece is billed at 18%: say so, so the higher total isn't a surprise.
                const p = products.find((x) => x.id === l.productId)
                return p && l.unitPrice > 0 && garmentRate(p.hsnCode, p.gstRate, l.unitPrice) === 18 && p.gstRate !== 18
                  ? <p className="text-xs font-semibold text-warn">{t('garmentGst.high')}</p> : null
              })()}
              {(() => { const per = products.find((p) => p.id === l.productId)?.unitsPerPack; return per ? (
                <button onClick={() => sellLoose(i, per)} className={`min-h-11 text-xs ${btn.link}`}>{t('loose.button')}</button>
              ) : null })()}
              {(() => {
                const bagSize = products.find((p) => p.id === l.productId)?.bagSize
                if (!bagSize) return null
                return (
                  <span className="flex flex-wrap items-center gap-2">
                    {l.bags ? <span className="rounded-full bg-warn-soft px-2 text-xs font-semibold text-warn">🛍️ {t('bag.inBags', { count: l.bags })}</span> : null}
                    <button onClick={async () => { const n = Math.round(Number(await ask(t('bag.wholeAsk', { size: bagSize, unit: t(`units.${l.unit}`) }))) || 0); if (n > 0) setCart((c) => c.map((x, k) => (k === i ? { ...x, quantity: n * bagSize, bags: n } : x))) }}
                      className={`min-h-11 text-xs ${btn.link}`}>{t('bag.wholeBags')}</button>
                  </span>
                )
              })()}
              {hasScale && (l.unit === 'kg' || l.unit === 'g') && (
                <span className="flex gap-3">
                  <button disabled={weighing !== null} onClick={() => void weigh(i, l.unit)} className={`min-h-11 text-xs ${btn.link}`}>{weighing === i ? t('scale.reading') : t('scale.button')}</button>
                  <button disabled={weighing !== null} onClick={() => void weigh(i, l.unit, true)} className={`min-h-11 text-xs ${btn.quiet}`}>{t('scale.change')}</button>
                </span>
              )}
              {tmtKgPerBar(l.name) && l.unit === 'kg' && (
                <button onClick={async () => { const bars = Number((await ask(t('bars.ask', { name: l.name }))) || 0); if (bars > 0) setQuantity(i, Math.round(bars * tmtKgPerBar(l.name)! * 1000) / 1000) }}
                  className={`min-h-11 text-xs ${btn.link}`}>{t('bars.button', { kg: tmtKgPerBar(l.name) })}</button>
              )}
            </div>
            <span className="flex shrink-0 items-center gap-0.5 rounded-xl bg-[#edf2f1] p-0.5">
              <button aria-label="−" className={`h-10 w-10 rounded-lg text-xl active:scale-95 lg:h-8 lg:w-8 ${btn.outline}`} onClick={() => setQuantity(i, round2(l.quantity - 1))}>−</button>
              <QuantityInput value={l.quantity} onChange={(q) => setQuantity(i, q)} label={`${t('quantity')}: ${l.name}`} />
              <button aria-label="+" className={`h-10 w-10 rounded-lg text-xl active:scale-95 lg:h-8 lg:w-8 ${btn.solid}`} onClick={() => setQuantity(i, round2(l.quantity + 1))}>+</button>
            </span>
            <div className="w-20 text-right font-bold">{formatRupees(round2(l.quantity * l.unitPrice))}</div>
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
            <div className="mt-2 flex justify-between text-sm font-semibold text-warn-ink">
              <span>{t('exchange.credit', { note: exchange!.noteNo })}</span><span>− {formatRupees(exchangeCredit)}</span>
            </div>
          )}
          {exchange && exchange.total > total && total > 0 && <p className="mt-1 text-sm font-semibold text-brand-deep">{t('exchange.leftOnSlip', { amount: formatRupees(round2(exchange.total - total)), note: exchange.noteNo })}</p>}
          {discountTotal > 0 && (
            <div className="mt-2 flex justify-between text-sm font-semibold text-brand-strong">
              <span>{t('discount.total')}</span><span>− {formatRupees(discountTotal)}</span>
            </div>
          )}
          {/* Optional: the shopkeeper's own discount on the whole bill. */}
          {billOff ? (
            <p className="mt-1 flex items-center justify-between gap-2 text-sm font-semibold text-brand-strong">
              <span>{t('billOff.applied', { amount: formatRupees(billOff.amount) })}</span>
              <button type="button" onClick={() => { setCart(billOff.before); setBillOff(null) }} className={`min-h-9 text-xs ${btn.quiet}`}>{t('billOff.remove')}</button>
            </p>
          ) : offForm ? (
            <div className="mt-2 flex flex-wrap items-center gap-2 text-sm">
              <input value={offForm.value} onChange={(e) => setOffForm({ ...offForm, value: e.target.value })} inputMode="decimal" placeholder={t('billOff.amount')} aria-label={t('billOff.amount')} autoFocus
                className="h-11 w-24 rounded-xl border border-line bg-white px-2" />
              <div className="flex rounded-xl border border-line">
                {([false, true] as const).map((pct) => (
                  <button key={String(pct)} type="button" onClick={() => setOffForm({ ...offForm, percent: pct })} aria-pressed={offForm.percent === pct}
                    className={`min-h-11 px-3 font-semibold ${offForm.percent === pct ? 'bg-brand text-white' : ''} ${pct ? 'rounded-r-xl' : 'rounded-l-xl'}`}>{t(pct ? 'billOff.percent' : 'billOff.rupees')}</button>
                ))}
              </div>
              <button type="button" onClick={applyBillOff} disabled={!(Number(offForm.value) > 0)} className={`min-h-11 rounded-xl px-4 ${btn.solid}`}>{t('billOff.apply')}</button>
              <button type="button" onClick={() => setOffForm(null)} className={`min-h-11 px-2 ${btn.quiet}`}>{t('cancel')}</button>
            </div>
          ) : (
            <button type="button" onClick={() => setOffForm({ value: '', percent: false })} className="mt-1 min-h-11 text-sm font-semibold text-brand">{t('billOff.add')}</button>
          )}
          {settings && (
            <div className="mt-2 text-sm">
              {picksState && (
                <label className="mb-1 flex items-center justify-between gap-2">
                  <span className="font-semibold text-muted">{t('gstLine.kind')}</span>
                  <select value={withoutGst ? 'none' : 'gst'} onChange={(e) => setWithoutGst(e.target.value === 'none')} className="h-11 min-w-0 rounded-xl border border-line bg-white px-2 font-semibold">
                    <option value="gst">{t('gstLine.gstBill')}</option>
                    <option value="none">{t('gstLine.noGstBill')}</option>
                  </select>
                </label>
              )}
              {picksState && withoutGst && <p className="mb-1 rounded-xl bg-warn-soft p-2 text-xs text-warn-ink">{t('gstLine.noGstHint')}</p>}
              <GodownPicker shopId={shopId} value={fromGodown} onChange={setFromGodown} label="out" />
              <p className="font-semibold text-muted">
                {t(taxInvoice ? 'gstLine.invoice' : picksState && withoutGst ? 'gstLine.withoutGst' : shopState ? 'gstLine.supply' : 'gstLine.plain')}
                {!shopState && isOwner && <button type="button" onClick={() => navigate('/settings')} className={`ml-2 min-h-11 ${btn.link}`}>{t('gstLine.addGstin')}</button>}
              </p>
              {/* Prices include GST: show what the total is made of, so it is plain the GST is inside it, not on top. */}
              {taxInvoice && tax.tax > 0 && (
                <p className="flex justify-between gap-2 text-muted"><span>{t('gstLine.beforeGst')}</span><span>{formatRupees(total - tax.tax)}</span></p>
              )}
              {taxInvoice && (
                <button type="button" onClick={() => setTaxOpen(!taxOpen)} aria-expanded={taxOpen} className="flex min-h-11 w-full items-center justify-between text-left font-semibold">
                  <span>{t('gstLine.inBill')} {taxOpen ? '▴' : '▾'}</span><span>{formatRupees(tax.tax)}</span>
                </button>
              )}
              {taxInvoice && taxOpen && tax.rows.map((r) => (
                <p key={r.rate} className="flex justify-between gap-2 text-muted">
                  <span>{r.rate}% · {t('gstLine.taxable', { amount: formatRupees(r.taxable) })}</span>
                  <span>{r.igst > 0 ? `IGST ${formatRupees(r.igst)}` : `CGST ${formatRupees(r.cgst)} · SGST ${formatRupees(r.sgst)}`}</span>
                </p>
              ))}
              {taxInvoice && tax.unset > 0 && <p className="font-semibold text-warn">{t('gstLine.noRate', { amount: formatRupees(tax.unset) })}</p>}
            </div>
          )}
          <div className="mt-2 flex justify-between text-xl font-bold">
            <span>{t('total')}</span>
            <span>{formatRupees(total)}</span>
          </div>
          {/* The exchange credit pays part of the bill: say plainly what is left to pay now. */}
          {exchangeCredit > 0 && (
            <div className="flex justify-between text-lg font-bold text-brand-strong">
              <span>{t('exchange.toPay')}</span>
              <span>{formatRupees(due)}</span>
            </div>
          )}
        </>)}
      </div>

      {cart.length > 0 && (
        <div className="space-y-4 rounded-card bg-white p-4 shadow-sm">
          {/* From an exchange slip: the customer is the slip's (or a walk-in), so don't ask again unless the shop wants a name. */}
          {exchange && !customer && !askCustomer ? (
            <p className="flex flex-wrap items-center justify-between gap-2 rounded-xl bg-brand-soft px-3 py-2 text-sm">
              <span><span className="text-muted">{t('slip.customer')}: </span><b>{t('slip.walkIn', { note: exchange.noteNo })}</b></span>
              <button type="button" onClick={() => setAskCustomer(true)} className={`min-h-9 text-sm ${btn.link}`}>{t('slip.addCustomer')}</button>
            </p>
          ) : exchange?.customerId && customer ? (
            <p className="rounded-xl bg-brand-soft px-3 py-2 text-sm"><span className="text-muted">{t('slip.customer')}: </span><b>{customer.name}</b>{customer.phone ? ` · ${customer.phone}` : ''} <span className="text-muted">({t('slip.fromSlip', { note: exchange.noteNo })})</span></p>
          ) : (
          <CustomerFinder customers={customers} selected={customer} onKhata={customer ? round2(due - paidAmount) : 0} onPick={pickCustomer} onNameLater={() => addCustomer(shopId, '', '', true)} onCreate={(name, phone) => addCustomer(shopId, name, phone)} />
          )}
          {customer && (
            <>
              <Field id="bill-paid" label={t('paidNow')} value={paid ?? String(due)} onChange={(e) => setPaid(e.target.value)} inputMode="decimal" />
              {paidAmount > 0 && <button onClick={allOnKhata} className={`min-h-11 w-full rounded-xl ${btn.outline}`}>{t('billFind.allKhata')}</button>}
            </>
          )}
          {customer && total - paidAmount > 0 && (
            <p className="font-semibold text-due">
              {t('onKhata')}: {formatRupees(round2(due - paidAmount))}
            </p>
          )}
          {isTrade(types) && (
            <Suspense fallback={null}>
              <TradeBillPanel shopId={shopId} shopType={shopType} customer={customer} onKhata={customer ? round2(due - paidAmount) : 0}
                products={products} extras={extras} onExtras={setExtras} onAddLine={add} />
            </Suspense>
          )}
          {picksState && buyerState && buyerState !== shopState && <p className="text-xs font-semibold text-warn">{t('pos.byGstin', { state: GST_STATES[buyerState] })}</p>}
          {deliversGoods && (picksState || sendsGoods) && !deliveryOpen && (
            <button type="button" onClick={() => setDeliveryOpen(true)} className="min-h-11 w-full rounded-xl border border-dashed border-line px-3 py-2 text-left font-semibold text-brand-strong">
              + {t('delivery.add')}
              {earlierSites && <span className="block text-xs font-normal text-muted">{t('delivery.earlier', { sites: earlierSites })}</span>}
            </button>
          )}
          {deliveryOpen && (
            <section aria-label={t('delivery.title')} className="flex flex-col gap-2 rounded-xl border border-line p-3">
              <div className="flex items-center justify-between">
                <h3 className="font-bold">{t('delivery.title')}</h3>
                <button type="button" onClick={() => setDelivery()} className={`min-h-11 px-1 text-sm ${btn.link}`}>{t('delivery.remove')}</button>
              </div>
              {picksState && buyerState && <p className="text-sm text-muted">{t('pos.followsGstin', { state: GST_STATES[buyerState] })}</p>}
              {picksState && !buyerState && (
                <label className="flex items-center justify-between gap-2 text-sm">
                  <span className="font-semibold text-muted">{t('pos.label')}</span>
                  <select value={deliveredTo} onChange={(e) => setDeliveredTo(e.target.value === shopState ? '' : e.target.value)} className="h-11 max-w-56 rounded-xl border border-line bg-white px-2">
                    <option value="">{t('pos.here', { state: GST_STATES[shopState] ?? shopState })}</option>
                    {Object.entries(GST_STATES).filter(([code]) => code !== shopState).map(([code, name]) => <option key={code} value={code}>{name}</option>)}
                  </select>
                </label>
              )}
              {picksState && !buyerState && deliveredTo && <p className="text-xs font-semibold text-warn">{t('pos.igst', { state: GST_STATES[deliveredTo] })}</p>}
              {sendsGoods && (
                <Suspense fallback={null}>
                  <TradeDelivery shopId={shopId} customer={customer} total={total} cart={cart} extras={extras} onExtras={setExtras} placeOfSupply={(picksState && deliveredTo) || null}
                    onChallanSaved={() => { setCart([]); setPaid(null); setCustomerId('') }} />
                </Suspense>
              )}
            </section>
          )}
          {serialLines.map((l) => (
            <SerialPicker key={l.productId} shopId={shopId} productId={l.productId!} name={l.name} quantity={l.quantity}
              chosen={picked[l.productId!] ?? []} onChange={(list) => setPicked({ ...picked, [l.productId!]: list })} />
          ))}
          {schedules.length > 0 && (
            <div className="flex flex-col gap-2 rounded-xl bg-due-soft p-2">
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
          <div className={`grid gap-1 rounded-2xl bg-[#edf2f1] p-1 text-sm ${paidAmount > 0 ? 'grid-cols-4' : 'grid-cols-3'}`}>
            {(['cash', 'upi', 'card'] as const).map((m) => {
              const on = upiPart === null && mode === m
              return (
                <button key={m} onClick={() => { setMode(m); setUpiPart(null) }} aria-pressed={on}
                  className={`flex h-14 flex-col items-center justify-center rounded-xl font-semibold transition ${on ? 'bg-white text-brand-strong shadow-sm ring-1 ring-brand/20' : 'text-muted hover:text-ink'}`}>
                  <span aria-hidden className="text-lg leading-none">{m === 'cash' ? '💵' : m === 'upi' ? '📱' : '💳'}</span>{t(m)}
                </button>
              )
            })}
            {paidAmount > 0 && (
              <button onClick={() => { setMode('cash'); setUpiPart(upiPart === null ? '' : null) }} aria-pressed={upiPart !== null}
                className={`flex h-14 flex-col items-center justify-center rounded-xl font-semibold transition ${upiPart !== null ? 'bg-white text-brand-strong shadow-sm ring-1 ring-brand/20' : 'text-muted hover:text-ink'}`}>
                <span aria-hidden className="text-lg leading-none">➗</span>{t('split.button')}
              </button>
            )}
          </div>
          {upiPart !== null && paidAmount > 0 && (
            <div className="grid grid-cols-2 gap-2 rounded-xl bg-[#f1f5f4] p-2 text-sm">
              <label className="flex flex-col gap-1"><span className="flex gap-1">{(['upi', 'card'] as const).map((m) => (
                <button key={m} type="button" onClick={() => setSplitWith(m)} aria-pressed={splitWith === m} className={`rounded-md px-2 font-semibold ${splitWith === m ? 'bg-ink text-white' : 'text-muted'}`}>{t(m)}</button>))}</span>
                <input value={upiPart} onChange={(e) => setUpiPart(e.target.value)} inputMode="decimal" autoFocus placeholder="0" className="h-11 rounded-lg border border-line bg-white px-2 text-lg" /></label>
              <span className="flex flex-col gap-1"><span className="text-muted">{t('cash')}</span><b className="flex h-11 items-center text-lg">{formatRupees(cashPaid)}</b></span>
              <span className="col-span-2 text-xs text-muted">{t('split.hint', { total: formatRupees(paidAmount) })}</span>
            </div>
          )}
          <CashLimitNote customerId={customer?.id ?? null} cash={upiPart === null ? (mode === 'cash' ? paidAmount : 0) : cashPaid} />
          {(upiPart === null ? mode === 'cash' : cashPaid > 0) && paidAmount > 0 && <CashChange paying={upiPart === null ? paidAmount : cashPaid} received={received} onChange={setReceived} />}
          {upiNow && (
            <input value={utr} onChange={(e) => setUtr(e.target.value.replace(/[^0-9a-zA-Z]/g, '').toUpperCase().slice(0, 30))} placeholder={t('split.utr')} aria-label={t('split.utr')}
              className="h-11 rounded-xl border border-line bg-white px-3 text-sm outline-none focus:border-brand-line" />
          )}
          {(upiPart === null ? mode === 'upi' : upiPaid > 0 && splitWith === 'upi') && paidAmount > 0 && <UpiCollect upiId={settings?.upiId ?? null} shop={shopName} amount={upiPart === null ? paidAmount : upiPaid} />}
          {unpriced && <p className="text-sm font-semibold text-due">{t('bars.noPrice')}</p>}
          {needsBuyerAddress && <p className="text-sm font-semibold text-due">{t('print.bigBillAddress')}</p>}
          {fromEstimate && <p className="rounded-xl bg-brand-soft p-2 text-sm text-brand-deep">{t('estimate.fromEstimate', { number: fromEstimate.number })}</p>}
          {fromOrder && <p className="rounded-xl bg-brand-soft p-2 text-sm text-brand-deep">🚚 {t('saleOrder.fromOrder', { number: fromOrder.number })}</p>}
          {orderSaved !== null && <p className={`rounded-xl p-2 text-sm text-emerald-800 ${btn.link}`}>{t('saleOrder.saved', { number: orderSaved })} <button onClick={() => navigate('/sale-orders')} className="underline">{t('saleOrder.open')}</button></p>}
          <button type="button" onClick={() => void saveEstimate()} disabled={cart.length === 0 || !!fromEstimate} className="flex min-h-10 items-center gap-1.5 self-start rounded-full border border-dashed border-brand/40 px-3.5 text-sm font-semibold text-brand-strong hover:bg-brand-soft disabled:opacity-40"><span aria-hidden>📝</span>{t('estimate.save')}</button>
          <button type="button" onClick={() => void saveOrder()} disabled={cart.length === 0 || !customerId || !!fromOrder || !!fromEstimate} title={!customerId ? t('saleOrder.needCustomer') : undefined} className="flex min-h-10 items-center gap-1.5 self-start rounded-full border border-dashed border-brand/40 px-3.5 text-sm font-semibold text-brand-strong hover:bg-brand-soft disabled:opacity-40"><span aria-hidden>📦</span>{t('saleOrder.save')}</button>
          <div className="flex gap-2 border-t border-line pt-4">
            <button type="button" onClick={holdBill} className={`flex min-h-14 shrink-0 items-center gap-1.5 rounded-2xl px-4 ${btn.outline}`}><span aria-hidden>⏸</span>{t('hold.button')}</button>
            <Button className="h-14 flex-1 text-lg shadow-md shadow-teal-900/15" id="bill-save" onClick={save} disabled={!canSave}>
              {mode === 'upi' && paidAmount > 0 ? t('upiPay.save', { amount: formatRupees(due) }) : `${t('saveBill')} · ${formatRupees(due)}`}
            </Button>
          </div>
        </div>
      )}

      </div>
      {cart.length > 0 && !billInView && (
        <button onClick={() => billRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })}
          className={`fixed inset-x-0 bottom-[calc(68px+env(safe-area-inset-bottom))] z-10 mx-auto flex h-14 lg:hidden w-[calc(100%-2rem)] max-w-[34rem] items-center justify-between rounded-2xl px-4 shadow-lg ${btn.solid}`}>
          <span>{t('billBar', { count: cart.length })}</span>
          <span>{formatRupees(due)} · {t('checkout')} ↓</span>
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

const UNITS: Unit[] = ['piece', 'kg', 'g', 'litre', 'ml', 'packet', 'pair', 'dozen', 'box', 'jar', 'bag', 'tonne', 'brass', 'cft', 'metre', 'sq_ft', 'running_ft', 'quintal', 'carton', 'bottle', 'set', 'strip']

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
    // On demand: most bills never show a QR, so the library stays out of the first download.
    void import('qrcode').then(({ default: QRCode }) => QRCode.toDataURL(uri, { width: 360, margin: 1, color: { dark: '#10201d', light: '#ffffff' } })).then((url) => { if (live) setQr(url) })
    return () => { live = false }
  }, [uri])
  if (!upiId) {
    return (
      <button onClick={() => navigate('/settings')} className="rounded-xl bg-warn-soft p-3 text-left text-sm font-semibold text-warn-ink">
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
  bill: { total: number; lines: CartLine[]; paid: number; mode?: PaymentMode; customer: { name: string } | null },
  nameOf: (line: CartLine) => string = (l) => l.name,
) {
  const items = bill.lines.map((l) => `• ${nameOf(l)} ${l.quantity} ${t(`units.${l.unit}`, { count: l.quantity })} × ${formatRupees(l.unitPrice)} = ${formatRupees(Math.round(l.quantity * l.unitPrice * 100) / 100)}`)
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
      className="h-10 w-14 rounded-lg bg-transparent text-center text-lg font-bold outline-none focus:bg-white focus:ring-2 focus:ring-brand-line/30 lg:h-8"
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
    <div className={`flex flex-col gap-1 rounded-xl p-2 ${chosen.length === quantity ? 'bg-brand-soft' : 'bg-warn-soft'}`}>
      <p className="text-sm font-semibold">{t('serials.pick', { name, count: quantity, picked: chosen.length })}</p>
      {inStock.length === 0 && <p className="text-xs text-due">{t('serials.noneInStock')}</p>}
      <div className="flex flex-wrap gap-1">
        {inStock.map((s) => (
          <button key={s.id} onClick={() => toggle(s.serialNo)} className={`min-h-10 rounded-lg px-2 font-mono text-sm ${chosen.includes(s.serialNo) ? 'bg-brand text-white' : 'border border-line bg-white'}`}>{s.serialNo}</button>
        ))}
      </div>
    </div>
  )
}

/** The phone camera reading a barcode (BarcodeDetector, in Chrome on Android and some others). */

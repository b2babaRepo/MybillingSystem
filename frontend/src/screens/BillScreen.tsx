import { groupOff, groupPrice } from '../lib/priceGroup'
import { shownLocale } from '../i18n'
import { GodownPicker } from '../components/GodownPicker'
import { lazy, Suspense, useEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react'
import { currentShift, subscribeShift } from '../lib/shift'
import type { InputHTMLAttributes } from 'react'
import { decimalText } from '../lib/decimalText'
import { CashChange, CustomerFinder } from '../components/CustomerFinder'
import { CashLimitNote } from '../components/CashLimitNote'
import { LinePriceHints } from '../components/LastPrices'
import { fitPrice, priceFor } from '../lib/wholesale'
import { billSeconds } from '../lib/billTimer'
import { GST_STATES } from '../lib/states'
import { nameMatches, splitQuantity } from '../lib/nameMatch'
import { score } from '../lib/search'
import { getSession } from '../lib/session'
import { ApiError, api } from '../lib/api'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { currentExchange, endExchange, startExchange, type Exchange } from '../lib/exchange'
import { groupStyles, sortVariants, variantLabel } from '../lib/styles'
import { billNameFor } from '../lib/billNames'
import { billTax, garmentRate } from '../lib/gst'
import { bestOffer, isRunning, offerPrice, perPiece, useOffers } from '../lib/offers'
import { promoteCart, spreadOff, type Promotion } from '../lib/promotions'
import { onDisplayHello, showOnDisplay, type DisplayState } from '../lib/customerDisplay'
import { barcodeIndex, cameraPossible, normBarcode, readScaleLabel, sameItemCode } from '../lib/barcode'
const UnknownBarcode = lazy(() => import('../components/UnknownBarcode').then((module) => ({ default: module.UnknownBarcode })))
const CategoryMenu = lazy(() => import('../components/CategoryMenu').then((module) => ({ default: module.CategoryMenu })))
const ScanSheet = lazy(() => import('../components/ScanSheet').then((module) => ({ default: module.ScanSheet })))
// The printed bill's own code, loaded only when a computer turns the live preview on.
const BillPreview = lazy(() => import('./PrintScreen').then((module) => ({ default: module.BillPreview })))
import { enqueue } from '../lib/outbox'
import { useTranslation } from 'react-i18next'
import { ItemCard } from '../components/ItemCard'
import { imageUrl } from '../lib/images'
import { Button, Field, Input, Sheet } from '../components/ui'
import { btn } from '../lib/buttonStyles'
import { addCustomer, counterName, saveBill, useCustomers, usePopularOrder, useProducts, type CartLine } from '../lib/data'
import { HeldBillGone, useHeldBills } from '../lib/heldBills'
import { formatDateTime, formatRupees, whatsAppLink, indiaToday } from '../lib/format'
import { round2 } from '../lib/local'
import type { BillPrint, BillView, PaymentMode, Product, Unit } from '../lib/types'
import { useShopSettings, shopTypes, isTrade, sendsToSites, upiUri } from '../lib/reports'
import { navigate } from '../router'
import { useSerialsInStock } from '../lib/serials'
import { ask, askSecret, tell } from '../lib/ask'
import { notify, shownError } from '../lib/notify'

// Cement, paint and hardware extras load only for those shops, keeping the kirana counter's first load small.
const TradeBillPanel = lazy(() => import('./TradeBill').then((module) => ({ default: module.TradeBillPanel })))
const TradeDelivery = lazy(() => import('./TradeBill').then((module) => ({ default: module.TradeDelivery })))

/** What a bill's delivery section holds, kept with a held bill. */
interface Delivery { deliveredTo: string; site: string; vehicleNumber: string }

/** Item billing: tap tiles, adjust quantities, choose who pays and how much now. The rest goes on khata. */
export function BillScreen({ shopId, shopName, isOwner = true }: { shopId: string; shopName: string; isOwner?: boolean }) {
  const { t } = useTranslation()
  const { products } = useProducts(shopId)
  const queryClient = useQueryClient()
  // An item with no price yet (picked from the common list): ask once, keep it on the item (owner), then bill it.
  async function addFromMenu(p: (typeof products)[number]) {
    let price = p.sellingPrice
    if (price <= 0) {
      const typed = await ask(t('catMenu.askPrice', { name: p.name, unit: t(`units.${p.unit}`) }))
      const value = Number((typed ?? '').replace(/[^0-9.]/g, ''))
      if (!typed || !(value > 0)) return
      price = value
      if (isOwner) {
        try {
          await api('/v1/products/rates', 'PUT', { rates: [{ productId: p.id, sellingPrice: value }] })
          void queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
        } catch { /* offline or refused: the bill still takes this price */ }
      }
    }
    add({ productId: p.id, name: p.name, quantity: 1, unit: p.unit, unitPrice: price })
  }
  const rank = usePopularOrder(shopId)
  const [search, setSearch] = useState('')
  // "2*aata" or "2*" then a scan: add two. The rest is matched in either script ("aata" finds "आटा").
  const { quantity: pendingQty, rest: searchText } = splitQuantity(search)
  const query = searchText.trim().toLowerCase()
  // Most-sold items first (the tiles the thumb needs), then the rest by name; typing filters.
  const tiles = products
    .filter((p) => !query || p.name.toLowerCase().includes(query) || (p.styleName ?? '').toLowerCase().includes(query) || nameMatches(p.name, query) || (!!p.styleName && nameMatches(p.styleName, query)))
    // A typed word at the start of a word ("dal" → "Moong dal") comes before the same letters inside one ("Sandal").
    .sort((a, b) => (query ? score(b.name, query) - score(a.name, query) : 0) || (rank.get(a.id) ?? 1e9) - (rank.get(b.id) ?? 1e9) || a.name.localeCompare(b.name))
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
  const settings = useShopSettings(shopId).data
  // This counter's open cashier shift: shown so the cashier knows bills are counting toward it.
  const shiftKey = useSyncExternalStore(subscribeShift, () => { try { return localStorage.getItem(`gk-shift:${shopId}`) } catch { return null } })
  const openShift = shiftKey ? currentShift(shopId) : null
  // Every phone with a camera: its own barcode reader where it has one, else the app's backup reader.
  const canScan = cameraPossible()
  const byBarcode = useMemo(() => barcodeIndex(products), [products])
  function addByBarcode(code: string) {
    const clean = normBarcode(code)
    const p = clean.length >= 4 ? byBarcode.get(clean) : undefined
    // A weighing scale's label: the item's code with its weight (or price) in the barcode. Each label is its own line.
    const label = p ? null : readScaleLabel(clean, settings?.scaleLabel)
    const weighed = label ? products.find((x) => sameItemCode(x.barcode, label.itemCode)) : undefined
    if (label && weighed) {
      const quantity = label.kg ?? (weighed.sellingPrice > 0 ? Math.round(((label.price ?? 0) / weighed.sellingPrice) * 1000) / 1000 : 0)
      if (quantity <= 0) return false
      add({ productId: weighed.id, name: weighed.name, quantity, unit: weighed.unit, unitPrice: weighed.sellingPrice }, true)
      setSearch('')
      setScanMiss(null)
      return true
    }
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
  // Held bills: park this customer's bill, serve the next one, pick it up again, on this till or any other.
  const { held, hold, take } = useHeldBills<{ cart: CartLine[]; customerId: string; delivery?: Delivery }>(shopId)
  const heldName = (h: (typeof held)[number]) => h.label ?? customers.find((c) => c.id === h.payload.customerId)?.name ?? t('hold.walkIn')
  const heldTotal = (h: (typeof held)[number]) => h.total || round2(h.payload.cart.reduce((n, l) => n + round2(l.quantity * l.unitPrice), 0))
  // Another till's bill says which till.
  const heldWhere = (h: (typeof held)[number]) => (h.counter && h.counter !== counterName() ? ` (${h.counter})` : '')
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
    const o = JSON.parse(raw) as { id: string; number: number; customerId: string | null; lines: { productId: string | null; name: string; quantity: number; unit: Unit; unitPrice: number; bags?: number | null }[] }
    startedAt.current = 0
    // Read once from storage another screen left (and removed here): an effect, as a state initializer runs twice in development.
    // eslint-disable-next-line react/set-state-in-effect
    setCart(o.lines.map((l) => ({ productId: l.productId, name: l.name, quantity: l.quantity, unit: l.unit, unitPrice: l.unitPrice, bags: l.bags ?? null })))
    setCustomerId(o.customerId ?? '')
    setFromOrder({ id: o.id, number: o.number })
  }, [])
  async function saveOrder() {
    if (cart.length === 0 || !customerId) return
    try {
      const o = await api<{ number: number }>('/v1/sale-orders', 'POST', {
        id: crypto.randomUUID(), customerId,
        lines: cart.map((l) => ({ productId: l.productId, name: l.name, quantity: l.quantity, unit: l.unit, unitPrice: l.unitPrice, bags: l.bags ?? null })),
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
    const e = JSON.parse(raw) as { id: string; number: number; customerId: string | null; lines: { productId: string | null; name: string; quantity: number; unit: Unit; unitPrice: number; bags?: number | null }[] }
    startedAt.current = 0
    // Read once from storage, as the sale order above.
    // eslint-disable-next-line react/set-state-in-effect
    setCart(e.lines.map((l) => ({ productId: l.productId, name: l.name, quantity: l.quantity, unit: l.unit, unitPrice: l.unitPrice, bags: l.bags ?? null })))
    setCustomerId(e.customerId ?? '')
    setFromEstimate({ id: e.id, number: e.number })
  }, [])
  async function saveEstimate() {
    if (cart.length === 0) return
    try {
      const e = await api<{ id: string; number: number }>('/v1/estimates', 'POST', {
        id: crypto.randomUUID(), customerId: customerId || null,
        lines: cart.map((l) => ({ productId: l.productId, name: l.name, quantity: l.quantity, unit: l.unit, unitPrice: l.unitPrice, bags: l.bags ?? null })),
      })
      setEstimateSaved({ id: e.id, number: e.number })
      setCart([]); setCustomerId(''); setPaid(null); setSaved(null); setFromEstimate(null)
    } catch (err) {
      await ask(err instanceof Error ? err.message : String(err))
    }
  }
  /** Park the bill being made (with its customer and delivery); nothing to park: nothing happens. */
  function parkCurrent() {
    if (cart.length === 0) return
    const name = customers.find((c) => c.id === customerId)?.name ?? null
    void hold({ cart, customerId, delivery: currentDelivery() }, { label: name, total: round2(cart.reduce((n, l) => n + round2(l.quantity * l.unitPrice), 0)), lines: cart.length })
  }
  function holdBill() {
    if (cart.length === 0) return
    parkCurrent()
    setCart([]); setCustomerId(''); setPaid(null); setSaved(null)
  }
  async function resumeBill(bill: (typeof held)[number]) {
    let pick: Awaited<ReturnType<typeof take>>
    try {
      pick = await take(bill)
    } catch (err) {
      await ask(err instanceof HeldBillGone ? t('hold.gone') : err instanceof Error ? err.message : String(err))
      return
    }
    startedAt.current = 0
    // The bill being made is parked in its place.
    parkCurrent()
    setCart(pick.cart); setCustomerId(pick.customerId); setPaid(null); setSaved(null)
    setDelivery(pick.delivery)
  }
  const [paid, setPaid] = useState<string | null>(null)
  const [mode, setMode] = useState<PaymentMode>('cash')
  const [received, setReceived] = useState('')
  // A loose item, or a charge line ("Transport") with its name filled in.
  const [loose, setLoose] = useState<false | { name: string; unit: Unit }>(false)
  // A contractor's or dealer's percent off the list price (0 for retail and walk-ins). Defined early: totals use it.
  const shopSettings = settings
  const customerOff = groupOff(customers.find((c) => c.id === customerId)?.priceGroup, shopSettings)
  const shopType = settings?.type ?? 'kirana'
  // The shop's own extra bill fields and what was typed in them for this bill.
  const billFieldLabels = settings?.billFields ?? []
  const [fieldValues, setFieldValues] = useState<Record<string, string>>({})
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
  // The customer whose place of supply was last offered (once per customer, when their details arrive).
  const stateOffered = useRef('')
  // A saved, held or emptied bill takes its delivery with it.
  useEffect(() => {
    if (cart.length > 0) return
    setDelivery()
    stateOffered.current = ''
  }, [cart.length])
  // Another customer's bill isn't going to this one's site.
  function pickCustomer(id: string) {
    if (customerId && id !== customerId) setDelivery()
    // Lines at the old customer's group price (or the list price) move to the new customer's group price.
    const before = customerOff
    const after = groupOff(customers.find((c) => c.id === id)?.priceGroup, shopSettings)
    if (before !== after) {
      setCart((current) => current.map((l) => {
        const p = products.find((x) => x.id === l.productId)
        if (!p || l.offerId || (l.unitPrice !== p.sellingPrice && l.unitPrice !== groupPrice(p.sellingPrice, before))) return l
        return { ...l, unitPrice: groupPrice(p.sellingPrice, after) }
      }))
    }
    stateOffered.current = ''
    setCustomerId(id)
    // Credit was chosen first: the customer just picked takes the whole bill on khata.
    if (id && creditAsked) { setPaid('0'); setCreditAsked(false) }
    if (!id) setCreditAsked(false)
  }
  /** Credit chosen before a customer: the bill waits for one (Save is held), then goes fully on their khata. */
  const [creditAsked, setCreditAsked] = useState(false)
  /** The top switch: Cash (paid now) or Credit (on the customer's khata). */
  function chooseCredit(credit: boolean) {
    if (!credit) { setPaid(null); setCreditAsked(false); return }
    if (customerId) setPaid('0')
    else { setCreditAsked(true); setTimeout(() => document.getElementById('bill-customer')?.focus(), 0) }
  }
  // Where this customer's tax goes, whatever this shop has chosen about delivery. Online only: offline it's the shop's state.
  const buyer = useQuery({
    queryKey: [shopId, 'bill-details', customerId],
    queryFn: () => api<{ gstin: string | null; address: string | null; state: string | null }>(`/v1/customers/${customerId}/bill-details`),
    enabled: !!shopState && !!customerId,
  }).data
  // The server follows a registered buyer's GSTIN over anything chosen here, so the screen must say the same.
  const buyerState = buyer?.gstin && buyer.gstin.length === 15 && GST_STATES[buyer.gstin.slice(0, 2)] ? buyer.gstin.slice(0, 2) : null
  useEffect(() => {
    if (!buyer || stateOffered.current === customerId) return
    stateOffered.current = customerId
    if (picksState && !buyerState && buyer.state && buyer.state !== shopState && !deliveryOpen) {
      // Responds to the customer's details arriving from the server, once per customer.
      // eslint-disable-next-line react/set-state-in-effect
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
  // The item's price for the line's quantity: at its wholesale quantity, the wholesale price is the regular one, not a discount.
  const regularOf = (l: CartLine) => { const p = products.find((x) => x.id === l.productId); return p ? Math.min(priceFor(p, l.quantity), groupPrice(p.sellingPrice, customerOff)) : 0 }
  const offOf = (l: CartLine) => { const r = regularOf(l); return r > 0 && l.unitPrice > 0 && l.unitPrice < r ? { amount: round2((r - l.unitPrice) * l.quantity), percent: Math.round(((r - l.unitPrice) / r) * 1000) / 10 } : null }
  const discountTotal = round2(cart.reduce((sum, l) => sum + (offOf(l)?.amount ?? 0), 0))
  // The owner's planned offers. A line under one is the owner's price, so the helper's limit doesn't apply to it.
  const offerData = useOffers(shopId).data
  const offers = useMemo(() => offerData ?? [], [offerData])
  // Only while the offer still runs: a held bill picked up after the offer ended goes back under the helper's limit, as on the server.
  const underOffer = (l: CartLine) => !!l.offerId && offers.some((o) => o.id === l.offerId && isRunning(o, Date.now()))
  // A supervisor's PIN lets the cashier keep a price below their limit (checked when the bill reaches the server).
  const [approval, setApproval] = useState<string | null>(null)
  const overLimit = !isOwner && !approval && cart.some((l) => !underOffer(l) && (offOf(l)?.percent ?? 0) > staffLimit)
  async function askSupervisor() {
    const pin = (await askSecret(t('super.ask')))?.trim()
    if (pin) setApproval(pin)
  }
  const [billOff, setBillOff] = useState<{ before: CartLine[]; amount: number } | null>(null)
  const [offForm, setOffForm] = useState<{ value: string; percent: boolean } | null>(null)
  // Promotions over the whole cart (buy-get, slabs, combos, bill offers), as the server prices them: the cart stays as
  // typed; the bill is billed as these lines. A whole-bill discount typed by hand replaces them.
  const [clock, setClock] = useState(() => Date.now())
  useEffect(() => { const tick = setInterval(() => setClock(Date.now()), 60_000); return () => clearInterval(tick) }, [])
  const promotions = useMemo(() => offers.filter((o) => !perPiece(o)) as Promotion[], [offers])
  const billed = useMemo(() => promoteCart(cart, products, billOff ? [] : promotions, new Date(clock)), [cart, products, promotions, clock, billOff])
  const cartTotal = round2(cart.reduce((sum, l) => sum + round2(l.quantity * l.unitPrice), 0))
  // Loyalty points: the customer's balance, and the points they chose to spend with the code sent to their mobile.
  const loyaltyOn = !!settings?.loyaltyEarnPer
  const loyalty = useQuery({
    queryKey: [shopId, 'loyalty', customerId], enabled: loyaltyOn && !!customerId, retry: false,
    queryFn: () => api<{ points: number; value: number; pointValue: number }>(`/v1/customers/${customerId}/loyalty`),
  })
  const [spend, setSpend] = useState<{ customerId: string; points: number; code: string } | null>(null)
  const [pointsForm, setPointsForm] = useState<{ points: string; code: string } | null>(null)
  const promoTotal = round2(billed.lines.reduce((sum, l) => sum + round2(l.quantity * l.unitPrice), 0))
  // Only for the customer they belong to, and never the whole bill.
  const pointsOff = spend && spend.customerId === customerId ? Math.min(round2(spend.points * (loyalty.data?.pointValue ?? settings?.loyaltyPointValue ?? 1)), round2(promoTotal - 0.01)) : 0
  const billedLines = useMemo(() => spreadOff(billed.lines, pointsOff), [billed, pointsOff])
  const total = useMemo(() => round2(billedLines.reduce((sum, l) => sum + round2(l.quantity * l.unitPrice), 0)), [billedLines])
  async function askCustomerCode() {
    try {
      await api(`/v1/customers/${customerId}/loyalty/code`, 'POST')
      setPointsForm({ points: String(loyalty.data?.points ?? 0), code: '' })
    } catch (e) {
      void ask(e instanceof Error ? e.message : t('error'))
    }
  }
  // A discount on the whole bill, if the shop wants one: spread over the lines by their share, so each line's price, its
  // GST and the helper's limit all stay right. "remove" puts the prices back as they were.
  // A line added or taken off ends the whole-bill discount (its spread no longer fits): decided while rendering, not after.
  if (billOff && cart.length !== billOff.before.length) setBillOff(null)
  function applyBillOff() {
    if (!offForm) return
    const value = Number(offForm.value)
    const off = round2(offForm.percent ? (cartTotal * value) / 100 : value)
    if (!(off > 0) || off >= cartTotal) { void ask(t('billOff.tooMuch')); return }
    const factor = (cartTotal - off) / cartTotal
    const next = cart.map((l) => ({ ...l, unitPrice: round2(l.unitPrice * factor) }))
    // Rounding can leave a few paise: the line with the most money takes them, if that keeps its price whole.
    const gap = round2(cartTotal - off - next.reduce((sum, l) => sum + round2(l.quantity * l.unitPrice), 0))
    const big = next.reduce((best, l, i) => (round2(l.quantity * l.unitPrice) > round2(next[best].quantity * next[best].unitPrice) ? i : best), 0)
    if (gap !== 0 && next[big].quantity === 1) next[big] = { ...next[big], unitPrice: round2(next[big].unitPrice + gap) }
    setBillOff({ before: cart, amount: round2(cartTotal - next.reduce((sum, l) => sum + round2(l.quantity * l.unitPrice), 0)) })
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
  const [slipCode, setSlipCode] = useState('')
  const [slipError, setSlipError] = useState('')
  const [slipBusy, setSlipBusy] = useState(false)
  async function applySlip() {
    setSlipBusy(true)
    setSlipError('')
    try {
      const s = await api<{ id: string; noteNo: string; amount: number; balance: number; status: string; validTill: string; customerId: string | null; usedOnBillNo: string | null }>(`/v1/exchange-slips/find?no=${encodeURIComponent(slipNo.trim())}&code=${encodeURIComponent(slipCode.trim())}`)
      if (s.status === 'used') setSlipError(shownError(t('slip.used', { bill: s.usedOnBillNo ?? '' })))
      else if (s.status === 'cancelled') setSlipError(shownError(t('slip.cancelled')))
      else if (s.status === 'expired') setSlipError(t('slip.expired', { date: new Date(`${s.validTill}T00:00`).toLocaleDateString(shownLocale()) }))
      else {
        const ex = { noteId: s.id, noteNo: s.noteNo, total: s.balance, customerId: s.customerId }
        startExchange(ex)
        setExchangeState(ex)
        if (s.customerId) setCustomerId(s.customerId)
        setSlipOpen(false)
        setSlipNo('')
        setSlipCode('')
      }
    } catch (e) {
      setSlipError(e instanceof ApiError && e.status === 404 ? t('slip.notFound') : shownError(e))
    } finally {
      setSlipBusy(false)
    }
  }
  const exchangeCredit = exchange ? Math.min(exchange.total, total) : 0
  // A gift voucher paying part of the bill: its code and how much it takes (never more than its balance or the bill).
  const [voucherPay, setVoucherPay] = useState<{ code: string; balance: number } | null>(null)
  const voucherAmt = voucherPay ? Math.min(voucherPay.balance, round2(total - exchangeCredit)) : 0
  const due = round2(total - exchangeCredit - voucherAmt)
  async function applyVoucher() {
    const code = (await ask(t('gift.askCode')))?.trim()
    if (!code) return
    try {
      const v = await api<{ code: string; balance: number; expiresOn: string; cancelledAt: string | null }>(`/v1/gift-vouchers/${encodeURIComponent(code)}`)
      if (v.cancelledAt || v.balance <= 0 || v.expiresOn < indiaToday()) { void ask(t('gift.notUsable')); return }
      setVoucherPay({ code: v.code, balance: v.balance })
    } catch {
      void ask(t('gift.notFound'))
    }
  }
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

  /** separate: a line of its own at its quantity (a weighed packet), not added onto the same item's line. */
  function add(line: CartLine, separate = false) {
    setSaved(null)
    // At its regular price, an item a running offer covers goes in at the offer's price, with the offer's name.
    const item = line.productId ? products.find((p) => p.id === line.productId) : undefined
    const offer = item && line.unitPrice === item.sellingPrice ? bestOffer(offers, item) : null
    if (offer && item) line = { ...line, unitPrice: offerPrice(offer, item.sellingPrice), offerId: offer.id, offerName: offer.name }
    // A contractor or dealer pays their group's price, unless an offer is lower.
    else if (item && line.unitPrice === item.sellingPrice && customerOff > 0) line = { ...line, unitPrice: groupPrice(item.sellingPrice, customerOff) }
    if (separate) {
      setCart((current) => [...current, { ...line, unitPrice: fitPrice(item, line.quantity, line.unitPrice) }])
      if (keyboardDesk) setTimeout(() => searchRef.current?.focus(), 0)
      return
    }
    const qty = pendingQty ?? 1
    if (pendingQty) { line = { ...line, quantity: pendingQty, unitPrice: fitPrice(item, pendingQty, line.unitPrice) }; setSearch('') }
    setCart((current) => {
      // A tinted tin shares its base's product but is its own line (its own shade and rate).
      const index = line.productId ? current.findIndex((l) => l.productId === line.productId && l.name === line.name && !l.bags) : -1
      if (index < 0) return [...current, line]
      // Quantities keep three decimals, as the server does (1 kg + 0.125 kg is 1.125, not 1.13).
      const quantity = Math.round((current[index].quantity + qty) * 1000) / 1000
      return current.map((l, i) => (i === index ? { ...l, quantity, unitPrice: fitPrice(item, quantity, l.unitPrice) } : l))
    })
    if (keyboardDesk) setTimeout(() => searchRef.current?.focus(), 0)
  }

  /** One unit's price on a whole-bag line: the bag price spread over the bag, else the loose (or wholesale) rate. */
  function bagUnitPrice(item: Product) {
    const size = item.bagSize ?? 1
    return item.bagPrice ? Math.round((item.bagPrice / size) * 100) / 100 : fitPrice(item, size, item.sellingPrice)
  }

  /** "+1 बोरी": a whole sealed bag at the bag price, on its own line (one bill can carry bags and loose of one item). */
  function addBag(item: Product) {
    const size = item.bagSize
    if (!size) return
    setSaved(null)
    setCart((current) => {
      const index = current.findIndex((l) => l.productId === item.id && !!l.bags)
      if (index >= 0) return current.map((l, i) => (i === index ? { ...l, bags: (l.bags ?? 0) + 1, quantity: Math.round(((l.bags ?? 0) + 1) * size * 1000) / 1000 } : l))
      return [...current, { productId: item.id, name: item.name, quantity: size, unit: item.unit, unitPrice: bagUnitPrice(item), bags: 1 }]
    })
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
    // A whole-bag line: + and − add or take one bag (its price stays the bag price).
    const bagLine = cart[index]
    const bagItem = bagLine?.bags ? products.find((p) => p.id === bagLine.productId) : undefined
    if (bagLine?.bags && bagItem?.bagSize && Math.abs(Math.round((quantity - bagLine.quantity) * 1000) / 1000) === 1) {
      const bags = bagLine.bags + Math.sign(quantity - bagLine.quantity)
      const size = bagItem.bagSize
      setCart((current) => (bags <= 0 ? current.filter((_, i) => i !== index) : current.map((l, i) => (i === index ? { ...l, bags, quantity: Math.round(bags * size * 1000) / 1000 } : l))))
      return
    }
    // A typed weight is no longer whole bags, and goes back to the loose rate.
    if (bagLine?.bags && bagItem) {
      setCart((current) => (quantity <= 0 ? current.filter((_, i) => i !== index) : current.map((l, i) => (i === index ? { ...l, quantity, bags: null, unitPrice: fitPrice(bagItem, quantity, bagItem.sellingPrice) } : l))))
      return
    }
    // Crossing the wholesale quantity moves the price to the wholesale one, or back.
    setCart((current) => (quantity <= 0 ? current.filter((_, i) => i !== index) : current.map((l, i) => (i === index
      ? { ...l, quantity, bags: null, unitPrice: fitPrice(products.find((p) => p.id === l.productId), quantity, l.unitPrice) } : l))))
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
    // An event handler (Save), not render: the bill is timed from its first item to now.
    // eslint-disable-next-line react/purity
    const savedAt = Date.now()
    // The voucher is one way of paying: it joins the parts, and what is paid includes it.
    const restParts: { mode: PaymentMode; amount: number; reference?: string | null }[] | undefined = split ? [{ mode: 'cash', amount: cashPaid }, { mode: splitWith, amount: upiPaid, reference: splitWith === 'upi' ? reference : null }]
      : reference ? [{ mode: 'upi', amount: paidAmount, reference }] : undefined
    const payments = voucherAmt > 0
      ? [{ mode: 'voucher' as PaymentMode, amount: voucherAmt, reference: voucherPay!.code }, ...(restParts ?? (paidAmount > 0 ? [{ mode, amount: paidAmount }] : []))]
      : restParts
    const result = await saveBill(shopId, billedLines.map((l) => ({ ...l, salespersonId: l.salespersonId || soldBy || null })), customer, round2(paidAmount + voucherAmt), voucherAmt > 0 && paidAmount === 0 ? 'cash' : mode, {
      billOfferId: billed.billOfferId,
      loyaltyPoints: pointsOff > 0 ? spend!.points : null, loyaltyCode: pointsOff > 0 ? spend!.code : null, supervisorPin: approval,
      site: extras.site.trim() || null, vehicleNumber: extras.vehicleNumber.trim() || null, painterId: extras.painterId || null,
      serials: Object.entries(picked).flatMap(([productId, list]) => list.map((serialNo) => ({ productId, serialNo }))),
      exchangeNoteId: exchange?.noteId ?? null, exchangeCredit,
      payments, placeOfSupply: (picksState && deliveredTo) || null, entrySeconds: billSeconds(startedAt.current, savedAt), withoutGst: picksState && withoutGst, godownId: fromGodown || null,
      prescription: schedules.length > 0 ? { doctorName: rx.doctorName.trim(), doctorAddress: rx.doctorAddress.trim() || null, doctorRegNo: rx.doctorRegNo.trim() || null, patientName: rx.patientName.trim(), patientAddress: rx.patientAddress.trim() || null } : null,
      fields: billFieldLabels.map((label) => ({ label, value: (fieldValues[label] ?? '').trim() })).filter((f) => f.value),
    })
    setFieldValues({})
    setCreditAsked(false)
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
    notify.success(customer ? t('notice.billSavedFor', { amount: formatRupees(total), name: customer.name }) : t('notice.billSaved', { amount: formatRupees(total) }))
    setCart([]); setSpend(null); setApproval(null); setVoucherPay(null)
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
  const tax = useMemo(() => billTax(billedLines.map((l) => {
    const item = products.find((p) => p.id === l.productId)
    return { amount: round2(l.quantity * l.unitPrice), gstRate: item ? garmentRate(item.hsnCode, item.gstRate, l.unitPrice) : null }
  }), taxState !== shopState), [billedLines, products, taxState, shopState])
  const [taxOpen, setTaxOpen] = useState(false)
  // A computer's live preview of the bill as it will print, in place of the item tiles while on. Remembered on this device.
  const [preview, setPreview] = useState(() => { try { return localStorage.getItem('gk-bill-preview') === '1' } catch { return false } })
  const togglePreview = () => setPreview((on) => { try { localStorage.setItem('gk-bill-preview', on ? '0' : '1') } catch { /* no storage */ } return !on })
  const previewLook = useQuery({ queryKey: ['print-look'], queryFn: () => api<BillPrint>('/v1/shop/print'), enabled: preview, staleTime: 300_000, retry: false })
  const [previewAt] = useState(() => new Date().toISOString())
  const previewBill = useMemo((): BillView['bill'] => {
    const gstin = settings?.gstin && !withoutGst ? settings.gstin : null
    return {
      id: 'preview', customerId: customer?.id ?? null, total, paidAmount, onKhata: customer ? round2(due - paidAmount) : 0, paymentMode: paidAmount > 0 ? mode : null,
      occurredAt: previewAt, customerBalance: null, billNo: null, gstin, billOfSupply: !!gstin && !!settings?.gstComposition,
      placeOfSupply: taxState, shopAddress: settings?.address ?? null, print: previewLook.data ?? null, exchangeCredit,
      site: extras.site.trim() || null, vehicleNumber: extras.vehicleNumber.trim() || null,
      fields: billFieldLabels.map((label) => ({ label, value: (fieldValues[label] ?? '').trim() })).filter((f) => f.value),
      // What goes on khata is due the customer's credit days from today, as the saved bill will say.
      dueOn: customer?.creditDays && due - paidAmount > 0
        ? (() => { const d = new Date(previewAt); d.setDate(d.getDate() + customer.creditDays!); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}` })() : null,
      lines: billedLines.map((l) => {
        const p = products.find((x) => x.id === l.productId)
        const regular = regularOf(l)
        return {
          productId: l.productId, name: l.name, quantity: l.quantity, unit: l.unit, unitPrice: l.unitPrice, amount: round2(l.quantity * l.unitPrice),
          gstRate: gstin && p ? garmentRate(p.hsnCode, p.gstRate, l.unitPrice) : null, hsnCode: p?.hsnCode ?? null,
          printName: billNameFor(p), listPrice: regular > l.unitPrice ? regular : null, bags: l.bags ?? null,
        }
      }),
    }
  }, [billedLines, products, customer, total, paidAmount, due, mode, settings, withoutGst, taxState, previewLook.data, exchangeCredit, extras, billFieldLabels, fieldValues, previewAt]) // eslint-disable-line react-hooks/exhaustive-deps
  // Credit with no customer yet can't be saved: whose khata would it go on?
  const canSave = total > 0 && !unpriced && !rxMissing && !serialsMissing && !overLimit && !(creditAsked && !customer)
  // Desktop keys: F2 search, F4 amount paid, Ctrl+Enter save, Esc clear search, ? the key list.
  function allOnKhata() {
    if (customer) setPaid('0')
    else document.getElementById('bill-customer')?.focus()
  }
  // F9: a discount on the whole bill. F8: take off the item added last. F7: keep this bill in its tab and start another.
  function billDiscount() { if (cart.length > 0 && !billOff) setOffForm((f) => f ?? { value: '', percent: false }) }
  function removeLast() { setCart((current) => current.slice(0, -1)) }
  // The customer-facing display (a second window on this computer): what the customer should see, as it changes.
  const displayState = (): DisplayState => ({
    shop: shopName, upiId: settings?.upiId ?? null, total, saved: round2(discountTotal + billed.applied.reduce((n, a) => n + a.saved, 0) + pointsOff),
    lines: billedLines.map((l) => ({ name: l.name, quantity: l.quantity, unit: l.unit, amount: round2(l.quantity * l.unitPrice) })),
    offers: billed.applied.map((a) => ({ name: a.offerName, saved: a.saved })),
    done: cart.length === 0 && saved ? { total: saved.total, paid: saved.paid, change: round2(Math.max(0, (Number(received) || 0) - saved.paid)) } : null,
  })
  const sendDisplay = useRef(displayState)
  useEffect(() => { sendDisplay.current = displayState })
  useEffect(() => { showOnDisplay(shopId, sendDisplay.current()) }, [shopId, billedLines, total, saved]) // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => onDisplayHello(shopId, () => showOnDisplay(shopId, sendDisplay.current())), [shopId])
  const keys = useRef({ save, canSave, printSaved, canPrint: false, allOnKhata, billDiscount, removeLast, holdBill })
  // The latest handlers for the keyboard shortcuts, swapped in after each render (never during one).
  useEffect(() => { keys.current = { save, canSave, printSaved, canPrint: !!saved && cart.length === 0, allOnKhata, billDiscount, removeLast, holdBill } })
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      const typing = e.target instanceof HTMLElement && /^(INPUT|TEXTAREA|SELECT)$/.test(e.target.tagName)
      if (e.key === 'F2') { e.preventDefault(); searchRef.current?.focus(); searchRef.current?.select() }
      else if (e.key === 'F3') { e.preventDefault(); document.getElementById('bill-customer')?.focus() }
      else if (e.key === 'F6') { e.preventDefault(); keys.current.allOnKhata() }
      else if (e.key === 'F9') { e.preventDefault(); keys.current.billDiscount() }
      else if (e.key === 'F8') { e.preventDefault(); keys.current.removeLast() }
      else if (e.key === 'F7') { e.preventDefault(); keys.current.holdBill() }
      else if (e.key === 'F4') { e.preventDefault(); const el = document.getElementById('bill-paid') as HTMLInputElement | null; if (el) { el.focus(); el.select() } else document.getElementById('bill-save')?.focus() }
      else if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) { e.preventDefault(); if (keys.current.canSave) void keys.current.save() }
      else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'p' && keys.current.canPrint) { e.preventDefault(); keys.current.printSaved() }
      else if (e.key === '?' && !typing) { e.preventDefault(); setKeysHelp((v) => !v) }
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [])

  return (
    <div className="flex flex-col gap-3 p-4 lg:grid lg:grid-cols-[minmax(0,1fr)_400px] lg:items-start lg:gap-4 xl:grid-cols-[minmax(0,1fr)_minmax(440px,40%)]">
      {/* Desktop: items on the left, the bill on the right (always in view). A phone shows them one under the other. */}
      <div className="flex flex-col gap-3 lg:col-start-1">
      {exchange && (
        <div className="flex items-center gap-2 rounded-[14px] bg-warn-soft p-3 text-sm text-amber-950">
          <span className="flex-1">{t('exchange.banner', { note: exchange.noteNo, amount: formatRupees(exchange.total) })}</span>
          <button onClick={() => { endExchange(); setExchangeState(null) }} className={`min-h-9 px-2 text-xs ${btn.link}`}>{t('exchange.cancel')}</button>
        </div>
      )}
      {!exchange && (
        <div className="flex flex-wrap items-center gap-2">
          {openShift && <button type="button" onClick={() => navigate('/shifts')} className="flex min-h-10 items-center rounded-full bg-brand-soft px-3.5 text-sm font-bold text-brand-deep">{t('shift.chipOpen', { counter: openShift.counter })}</button>}
          <button type="button" onClick={() => navigate('/find-bill')} className="flex min-h-10 items-center gap-2 rounded-full bg-[#fde8ec] px-3.5 text-sm font-bold text-[#be123c] transition hover:bg-[#fbd5dd] active:scale-[0.98]"><svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M9 14L4 9l5-5M4 9h11a5 5 0 010 10h-3" /></svg>{t('findBill.link')}</button>
          <button type="button" onClick={() => { setSlipOpen(!slipOpen); setSlipError('') }} aria-pressed={slipOpen} className={`flex min-h-10 items-center gap-2 rounded-full px-3.5 text-sm font-bold transition active:scale-[0.98] ${slipOpen ? 'bg-[#6d28d9] text-white shadow-md shadow-[#6d28d9]/30' : 'bg-[#f1eafd] text-[#6d28d9] hover:bg-[#e7dcfb]'}`}><svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M3 7h18v4a2 2 0 000 4v4H3v-4a2 2 0 000-4zM13 7v12" /></svg>{t('slip.use')}</button>
        </div>
      )}
      {!exchange && slipOpen && (
        <form onSubmit={(e) => { e.preventDefault(); void applySlip() }} className="flex flex-col gap-2 rounded-[14px] bg-brand-soft p-3">
          <span className="text-sm text-muted">{t('slip.useHint')}</span>
          <div className="flex gap-2">
            <Input autoFocus value={slipNo} onChange={(e) => setSlipNo(e.target.value)} placeholder="CN/2026-27/0003" aria-label={t('slip.number')} className="min-w-0 flex-1" />
            {!slipNo.trim().toUpperCase().startsWith('GKSLIP:') && (
              <Input value={slipCode} onChange={(e) => setSlipCode(e.target.value.replace(/\D/g, '').slice(0, 4))} inputMode="numeric" placeholder={t('slip.code')} aria-label={t('slip.code')} className="w-20" />
            )}
            <Button type="submit" busy={slipBusy} disabled={!slipNo.trim() || (!slipNo.trim().toUpperCase().startsWith('GKSLIP:') && slipCode.length !== 4)}>{t('slip.apply')}</Button>
          </div>
          {slipError && <p className="text-sm text-due">{slipError}</p>}
        </form>
      )}
      <div className="flex gap-2">
        <span className="relative min-w-0 flex-1">
        <span aria-hidden className="pointer-events-none absolute left-4 top-1/2 z-10 -translate-y-1/2 text-muted"><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M11 4a7 7 0 100 14 7 7 0 000-14zM20 20l-3.5-3.5" /></svg></span>
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
        <button onClick={togglePreview} aria-pressed={preview} title={t('billPreview.toggle')}
          className={`hidden h-12 shrink-0 items-center gap-1.5 rounded-[14px] border px-3 font-semibold xl:flex ${preview ? 'border-[#1d4ed8] bg-[#1d4ed8] text-white shadow-md shadow-[#1d4ed8]/30' : 'border-transparent bg-[#e8effd] text-[#1d4ed8] hover:bg-[#dbe5fc]'}`}><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12zM12 9a3 3 0 100 6 3 3 0 000-6" /></svg>{t('billPreview.toggle')}</button>
        <button onClick={() => setKeysHelp((v) => !v)} aria-pressed={keysHelp} title={`${t('keys.title')} (?)`}
          className={`hidden h-12 shrink-0 items-center gap-1.5 rounded-[14px] border px-3 font-semibold lg:flex ${keysHelp ? 'border-ink bg-ink text-white shadow-md' : 'border-transparent bg-[#edf2f1] text-ink hover:bg-[#e1e8e5]'}`}><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M3 6h18v12H3zM7 10h.01M11 10h.01M15 10h.01M7 14h10" /></svg>{t('keys.button')}</button>
        {canScan && <button onClick={() => setScanning(true)} aria-label={t('barcodeBill.scan')} className={`flex h-12 shrink-0 items-center gap-1.5 rounded-2xl px-4 ${btn.solid}`}><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M4 7h3l2-3h6l2 3h3v13H4zM12 10a3.5 3.5 0 100 7 3.5 3.5 0 000-7" /></svg>{t('barcodeBill.scan')}</button>}
      </div>
      {keysHelp && (
        <div className="hidden rounded-[14px] bg-white p-3 text-sm lg:block">
          <p className="mb-1 font-bold">{t('keys.title')}</p>
          <ul className="grid grid-cols-2 gap-x-4 gap-y-1 text-muted">
            {[['F2', t('keys.search')], ['Enter', t('keys.add')], ['F3', t('keys.customer')], ['F6', t('keys.khata')], ['F4', t('keys.paid')], ['F9', t('keys.discount')], ['F8', t('keys.removeLast')], ['F7', t('keys.newBill')], ['Ctrl + Enter', t('keys.save')], ['Ctrl + P', t('keys.print')], ['2*', t('keys.qty')], ['Esc', t('keys.clear')], ['?', t('keys.toggle')]].map(([k, label]) => (
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
      {/* The bill as it will print, live, in place of the tiles (computer only). The search box above still adds items. */}
      {preview && (
        <div className="hidden xl:block">
          <p className="mb-2 text-sm font-semibold text-muted">{cart.length > 0 ? t('billPreview.note') : t('billPreview.empty')}</p>
          {cart.length > 0 && (
            <div className="overflow-hidden rounded-card shadow-md ring-1 ring-line">
              <Suspense fallback={<p className="p-6 text-muted">…</p>}>
                <BillPreview shopName={shopName} bill={previewBill} customerName={customer?.name || null} phone={customer?.phone ?? null} />
              </Suspense>
            </div>
          )}
        </div>
      )}
      {/* One layout at every size (layout A): department rail, its category tiles and best sellers, and typed matches, all in
          this one frame; the bill sits beside it on a wide screen and below it on a phone. */}
      {(
        <div className={preview ? 'xl:hidden' : ''}>
          <Suspense fallback={null}>
            <CategoryMenu embedded search={searchText} products={products} rank={rank} billItems={cart.length} onClose={() => {}}
              quantities={new Map(cart.filter((l) => l.productId && !l.bags).map((l) => [l.productId!, l.quantity]))}
              bagCounts={new Map(cart.filter((l) => l.productId && l.bags).map((l) => [l.productId!, l.bags!]))}
              onAddBag={addBag}
              onAdd={(p) => void addFromMenu(p)}
              onLess={(p) => { const i = cart.findIndex((l) => l.productId === p.id && !l.bags); if (i >= 0) setQuantity(i, round2(cart[i].quantity - 1)) }} />
          </Suspense>
        </div>
      )}
      <div className={`grid grid-cols-3 gap-2 lg:grid-cols-4 xl:grid-cols-5 2xl:grid-cols-6 ${preview ? 'xl:hidden' : ''} hidden`}>
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
      {/* On a computer every open bill is a tab: the one being made, the held ones, and + for another customer. */}
      <div role="tablist" aria-label={t('billTabs.label')} className="hidden flex-wrap items-end gap-1 border-b-2 border-brand xl:flex">
        <span role="tab" aria-selected className="max-w-56 truncate rounded-t-xl bg-brand px-4 py-2 text-sm font-bold text-white">
          {(customers.find((c) => c.id === customerId)?.name ?? t('billTabs.current'))}{cart.length > 0 ? ` · ${formatRupees(total)}` : ''}
        </span>
        {held.map((h) => (
          <button key={h.id} role="tab" aria-selected={false} onClick={() => void resumeBill(h)} className="max-w-56 truncate rounded-t-xl border border-b-0 border-line bg-white px-4 py-2 text-sm font-semibold hover:bg-brand-soft">
            {heldName(h)} · {formatRupees(heldTotal(h))}{heldWhere(h)}
          </button>
        ))}
        <button type="button" onClick={holdBill} disabled={cart.length === 0} title={t('billTabs.newHint')} aria-label={t('billTabs.new')} className="rounded-t-xl px-3 py-2 text-lg font-bold text-brand hover:bg-brand-soft disabled:opacity-40">+</button>
      </div>
      {held.length > 0 && (
        <div className="flex flex-wrap items-center gap-2 rounded-[14px] bg-warn-soft p-2 text-sm xl:hidden">
          <span className="font-semibold text-warn-ink">{t('hold.held', { count: held.length })}</span>
          {held.map((h) => (
            <button key={h.id} onClick={() => void resumeBill(h)} className="min-h-9 rounded-full border border-amber-300 bg-white px-3 font-semibold">
              {heldName(h)} · {formatRupees(heldTotal(h))}{heldWhere(h)}
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
        {/* On a wide screen the bill is a table, like a printed invoice: one row per item, every number in its column. */}
        {cart.length > 0 && (
          <div className={`hidden border-b border-line pb-1.5 text-xs font-semibold uppercase tracking-wide text-muted xl:grid ${DESK_COLS}`}>
            <span>#</span><span>{t('print.item')}</span><span className="text-center">{t('print.qty')}</span><span className="text-right">{t('print.rate')}</span>
            <span className="text-right">{t('billTable.disc')}</span><span className="text-right">GST %</span><span className="text-right">{t('print.amount')}</span>
          </div>
        )}
        {cart.map((l, i) => (
          <div key={`${l.productId ?? l.name}-${i}`} className={`flex items-center gap-2 border-b border-[#edf2f1] py-2 last:border-0 lg:py-1.5 lg:[&>button]:h-8 lg:[&>button]:w-8 xl:grid xl:items-start ${DESK_COLS}`}>
            <span className="hidden pt-1.5 text-sm text-muted xl:block">{i + 1}</span>
            <div className="min-w-0 flex-1 lg:flex lg:flex-wrap lg:items-center lg:gap-x-2 xl:pt-1 lg:[&>div:not(:first-child)]:basis-full lg:[&>p]:basis-full">
              <div className="font-semibold">{l.name}{(() => { const s = products.find((p) => p.id === l.productId)?.schedule; return s && s !== 'g' ? <span className="ml-1 whitespace-nowrap rounded bg-due-soft px-1 text-xs text-due">{t('rx.badge', { schedule: s.toUpperCase() })}</span> : null })()}</div>
              <label className="flex items-center gap-1 text-xs text-muted xl:hidden">
                ₹
                <PriceInput
                  value={l.unitPrice}
                  onChange={(p) => setPrice(i, p)}
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
              {!isOwner && !underOffer(l) && (offOf(l)?.percent ?? 0) > staffLimit && (approval
                ? <p className="text-xs font-semibold text-brand-deep">✓ {t('super.approved')}</p>
                : <p className="flex flex-wrap items-center gap-2 text-xs font-semibold text-due">{t('discount.overLimit', { percent: staffLimit })}
                  <button type="button" onClick={() => void askSupervisor()} className="min-h-9 rounded-lg border border-due px-2">{t('super.approve')}</button></p>)}
              {(() => {
                // Medicines run on thin margins: warn when this price, before GST, is at or under what the item cost.
                const p = products.find((x) => x.id === l.productId)
                if (!p || p.costPrice == null || !(l.unitPrice > 0)) return null
                const net = l.unitPrice / (1 + (garmentRate(p.hsnCode, p.gstRate, l.unitPrice) ?? 0) / 100)
                // A cost several times the price was entered for something else (a whole piece or a lot instead of one gram or
                // one unit): say so and point at the item, not a margin like -34784%.
                if (p.costPrice > net * 3) return (
                  <p className="text-xs font-semibold text-due">
                    {t('pmonth.costLooksWrong', { cost: formatRupees(p.costPrice), unit: t(`units.${p.unit}`) })}{' '}
                    <button type="button" onClick={() => navigate(`/p/${p.id}`)} className="underline">{t('pmonth.checkCost')}</button>
                  </p>
                )
                const margin = ((net - p.costPrice) / net) * 100
                return margin < 5 ? <p className={`text-xs font-semibold ${margin <= 0 ? 'text-due' : 'text-warn'}`}>{t(margin <= 0 ? 'pmonth.belowCost' : 'pmonth.thinMargin', { percent: Math.round(margin * 10) / 10 })}</p> : null
              })()}
              {(() => {
                // Wholesale: say when the line is at it, or how many more bring it.
                const p = l.productId ? products.find((x) => x.id === l.productId) : null
                if (!p?.wholesalePrice || !p.wholesaleMinQty) return null
                const at = l.quantity >= p.wholesaleMinQty && l.unitPrice === p.wholesalePrice
                return (
                  <p className={`mt-1 text-xs font-semibold ${at ? 'text-brand-strong' : 'text-muted'}`}>
                    {at ? <span className="rounded-full bg-brand-soft px-2 py-0.5">📦 {t('wholesale.applied', { price: formatRupees(p.wholesalePrice) })}</span>
                      : t('wholesale.from', { price: formatRupees(p.wholesalePrice), qty: p.wholesaleMinQty, unit: t(`units.${l.unit}`, { count: p.wholesaleMinQty }) })}
                  </p>
                )
              })()}
              {(() => {
                const p = l.productId ? products.find((x) => x.id === l.productId) : null
                return p ? (
                  <LinePriceHints productId={p.id} customerId={customerId} isOwner={isOwner} price={l.unitPrice} cost={p.costPrice}
                    gstRate={garmentRate(p.hsnCode, p.gstRate, l.unitPrice)} unitLabel={t(`units.${l.unit}`)} onPick={(price) => setPrice(i, price)} />
                ) : null
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
                // A packet item with a known weight (atta in 0.5 kg packets): type kg, get that many packets.
                const item = products.find((p) => p.id === l.productId)
                if (!item?.weightKg || ['kg', 'g', 'tonne', 'quintal'].includes(l.unit) || l.bags) return null
                const perKg = item.weightKg
                async function byKg() {
                  const kg = Number(await ask(t('bag.kgAsk', { unit: t(`units.${l.unit}`), kg: perKg })))
                  if (kg > 0) setQuantity(i, Math.round((kg / perKg) * 1000) / 1000)
                }
                return <button onClick={() => void byKg()} className={`min-h-11 text-xs ${btn.link}`}>{t('bag.byKg')} · {Math.round(l.quantity * perKg * 1000) / 1000} kg</button>
              })()}
              {(() => {
                const item = products.find((p) => p.id === l.productId)
                if (!item?.bagSize) return null
                const bagSize = item.bagSize
                // Cartons and strips are whole packs bought and sold by count; grain bags are sold by weight.
                const pack = item.purchaseUnit ?? null
                async function sellWhole() {
                  const n = Math.round(Number(await ask(pack ? t('hwp5.wholeAsk', { size: bagSize, unit: t(`units.${l.unit}`, { count: bagSize }), pack: t(`units.${pack}`, { count: 1 }) }) : t('bag.wholeAsk', { size: bagSize, unit: t(`units.${l.unit}`) }))) || 0)
                  if (!(n > 0)) return
                  setCart((c) => {
                    const line = c[i]
                    // A line just tapped in (1 loose piece) or already in whole packs becomes the packs; otherwise the packs
                    // go on their own line, so one bill can carry "2 cartons" and "5 pieces" of the same item.
                    if (line.bags || line.quantity === 1 || !pack) return c.map((x, k) => (k === i ? { ...x, quantity: n * bagSize, bags: n, unitPrice: bagUnitPrice(item!), offerId: null, offerName: null } : x))
                    return [...c, { ...line, name: `${item!.name} (${n} ${t(`units.${pack}`, { count: n })})`.slice(0, 120), quantity: n * bagSize, bags: n, unitPrice: bagUnitPrice(item!), offerId: null, offerName: null }]
                  })
                }
                return (
                  <span className="flex flex-wrap items-center gap-2">
                    {l.bags ? <>
                      <span className="rounded-full bg-warn-soft px-2 py-0.5 text-xs font-semibold text-warn-ink">{pack ? '📦' : '🛍️'} {t('bag.bagLine', { count: l.bags, size: bagSize, unit: t(`units.${l.unit}`) })} · {t('bag.bagRate', { price: item.bagPrice ?? Math.round(l.unitPrice * bagSize) })}</span>
                      <button onClick={() => setCart((c) => c.map((x, k) => (k === i ? { ...x, bags: null, unitPrice: fitPrice(item, x.quantity, item.sellingPrice) } : x)))} className={`min-h-11 text-xs ${btn.link}`}>{t('bag.toLoose')}</button>
                    </> : <button onClick={() => addBag(item)} className="min-h-9 rounded-lg border border-brand px-2.5 text-xs font-bold text-brand-strong">{t('bag.sellBag')}{item.bagPrice ? ` · ₹${item.bagPrice}` : ''}</button>}
                    <button onClick={() => void sellWhole()} className={`min-h-11 text-xs ${btn.link}`}>{pack ? t('hwp5.wholePacks', { pack: t(`units.${pack}`, { count: 2 }) }) : t('bag.wholeBags')}</button>
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
            <span className="flex shrink-0 items-center gap-0.5 rounded-xl bg-[#edf2f1] p-0.5 xl:bg-transparent xl:p-0">
              <button aria-label="−" disabled={l.bags ? l.bags <= 1 : l.quantity <= 1} className={`h-10 w-10 rounded-lg text-xl active:scale-95 disabled:opacity-35 disabled:active:scale-100 lg:h-8 lg:w-8 xl:hidden ${btn.outline}`}
                onClick={() => setQuantity(i, l.bags ? round2(l.quantity - 1) : Math.max(1, round2(l.quantity - 1)))}>−</button>
              <QuantityInput value={l.quantity} onChange={(q) => { if (q > 0) setQuantity(i, q) }} label={`${t('quantity')}: ${l.name}`} />
              <button aria-label="+" className={`h-10 w-10 rounded-lg text-xl active:scale-95 lg:h-8 lg:w-8 xl:hidden ${btn.solid}`} onClick={() => setQuantity(i, round2(l.quantity + 1))}>+</button>
            </span>
            {/* Desk columns: rate, discount % off the regular price, GST %. Enter goes back to the item search for the next one. */}
            <PriceInput value={l.unitPrice} onChange={(p) => setPrice(i, p)} aria-label={`${t('print.rate')}: ${l.name}`}
              onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); searchRef.current?.focus() } }}
              className={`hidden h-8 w-full rounded-md border px-1.5 text-right xl:block ${l.unitPrice > 0 ? 'border-line' : 'border-udhaar'}`} />
            <PercentInput percent={offOf(l)?.percent ?? 0} disabled={!(regularOf(l) > 0)} label={`${t('billTable.disc')}: ${l.name}`}
              onChange={(pct) => setPrice(i, round2(regularOf(l) * (1 - pct / 100)))} onEnter={() => searchRef.current?.focus()} />
            <span className="hidden pt-1.5 text-right text-sm xl:block">{(() => { const p = products.find((x) => x.id === l.productId); const r = p ? garmentRate(p.hsnCode, p.gstRate, l.unitPrice) : null; return r == null ? '—' : `${r}%` })()}</span>
            <div className="flex w-20 items-start justify-end gap-1 xl:w-auto xl:pt-1.5">
              <span className="font-bold">{formatRupees(round2(l.quantity * l.unitPrice))}</span>
              {/* Not wanted on this bill: take the line off (− stops at 1). */}
              <button onClick={() => setCart((c) => c.filter((_, k) => k !== i))} aria-label={`${t('remove')}: ${l.name}`} title={t('remove')}
                className="-mr-1 flex h-7 w-7 shrink-0 items-center justify-center rounded-lg text-muted hover:bg-due-soft hover:text-due">
                <svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.2" strokeLinecap="round"><path d="M6 6l12 12M18 6L6 18" /></svg>
              </button>
            </div>
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
          {total > 0 && (voucherAmt > 0 ? (
            <div className="mt-2 flex justify-between text-sm font-semibold text-warn-ink">
              <span>🎁 {t('gift.paying', { code: voucherPay!.code })} <button type="button" onClick={() => setVoucherPay(null)} className={`min-h-9 text-xs ${btn.quiet}`}>{t('billOff.remove')}</button></span><span>− {formatRupees(voucherAmt)}</span>
            </div>
          ) : (
            <button type="button" onClick={() => void applyVoucher()} className="mt-1 min-h-11 text-sm font-semibold text-brand">🎁 {t('gift.use')}</button>
          ))}
          {exchange && exchange.total > total && total > 0 && <p className="mt-1 text-sm font-semibold text-brand-deep">{t('exchange.leftOnSlip', { amount: formatRupees(round2(exchange.total - total)), note: exchange.noteNo })}</p>}
          {discountTotal > 0 && (
            <>
              {/* What the items come to at their usual prices, so the discount below plainly adds up to the total. */}
              <div className="mt-2 flex justify-between text-sm text-muted">
                <span>{t('discount.before')}</span><span>{formatRupees(round2(total + discountTotal))}</span>
              </div>
              <div className="flex justify-between text-sm font-semibold text-brand-strong">
                <span>{t('discount.total')}</span><span>− {formatRupees(discountTotal)}</span>
              </div>
            </>
          )}
          {/* Offers the bill took by itself (buy-get, combos, slabs, bill offers): what each saved. */}
          {billed.applied.length > 0 && (
            <ul aria-label={t('promo.taken')} className="mt-1 flex flex-col gap-1 rounded-xl bg-amber-50 p-2 text-sm">
              {billed.applied.map((a) => (
                <li key={a.offerId} className="flex justify-between gap-2"><span className="font-semibold text-warn-ink">🏷 {a.offerName}</span><span className="font-semibold">−{formatRupees(a.saved)}</span></li>
              ))}
            </ul>
          )}
          {/* The customer's loyalty points: spend them with the code their mobile gets. */}
          {loyaltyOn && customerId && (loyalty.data?.points ?? 0) > 0 && (
            <div className="mt-1 flex flex-col gap-2 rounded-xl bg-brand-soft p-2 text-sm">
              <p className="flex items-center justify-between gap-2">
                <span>⭐ {t('points.have', { points: loyalty.data!.points, value: formatRupees(loyalty.data!.value) })}</span>
                {pointsOff > 0
                  ? <span className="flex items-center gap-2 font-semibold">−{formatRupees(pointsOff)} <button type="button" onClick={() => setSpend(null)} className={`min-h-9 text-xs ${btn.quiet}`}>{t('billOff.remove')}</button></span>
                  : !pointsForm && <button type="button" onClick={() => void askCustomerCode()} className={`min-h-9 rounded-xl px-3 font-semibold ${btn.outline}`}>{t('points.use')}</button>}
              </p>
              {pointsForm && pointsOff === 0 && (
                <div className="flex flex-wrap items-end gap-2">
                  <label className="flex flex-col text-xs">{t('points.howMany')}
                    <input value={pointsForm.points} onChange={(e) => setPointsForm({ ...pointsForm, points: e.target.value.replace(/\D/g, '') })} inputMode="numeric" className="h-11 w-24 rounded-xl border border-line bg-white px-2" /></label>
                  <label className="flex flex-col text-xs">{t('points.code')}
                    <input value={pointsForm.code} onChange={(e) => setPointsForm({ ...pointsForm, code: e.target.value.replace(/\D/g, '').slice(0, 6) })} inputMode="numeric" autoComplete="one-time-code" className="h-11 w-28 rounded-xl border border-line bg-white px-2" /></label>
                  <button type="button" disabled={pointsForm.code.length !== 6 || !(Number(pointsForm.points) > 0) || Number(pointsForm.points) > (loyalty.data?.points ?? 0)}
                    onClick={() => { setSpend({ customerId, points: Number(pointsForm.points), code: pointsForm.code }); setPointsForm(null) }} className={`min-h-11 rounded-xl px-3 ${btn.solid}`}>{t('billOff.apply')}</button>
                  <p className="w-full text-xs text-muted">{t('points.codeHint')}</p>
                </div>
              )}
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
          {/* The first choice on a bill, as on a paper bill book: paid now, or on the customer's khata. */}
          {(() => {
            const credit = creditAsked || (!!customer && paidAmount < due)
            const seg = (on: boolean, tone: string) => `flex min-h-12 flex-1 items-center justify-center gap-2 rounded-xl text-base font-bold transition ${on ? tone : 'text-muted hover:bg-white'}`
            return (
              <div role="radiogroup" aria-label={t('creditSwitch.label')} className="flex gap-1 rounded-2xl bg-[#edf2f1] p-1">
                <button role="radio" aria-checked={!credit} onClick={() => chooseCredit(false)} className={seg(!credit, 'bg-[#0b5e4f] text-white shadow-md shadow-[#0b5e4f]/30')}><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M3 6h18v12H3zM12 9.5a2.5 2.5 0 100 5 2.5 2.5 0 000-5" /></svg>{t('creditSwitch.cash')}</button>
                <button role="radio" aria-checked={credit} onClick={() => chooseCredit(true)} className={seg(credit, 'bg-[#b42318] text-white shadow-md shadow-[#b42318]/30')}><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M4 4h11a3 3 0 013 3v13H7a3 3 0 01-3-3zM4 17a3 3 0 013-3h11" /></svg>{t('creditSwitch.credit')}</button>
              </div>
            )
          })()}
          {creditAsked && !customer && <p className="text-sm font-semibold text-due">{t('creditSwitch.needCustomer')}</p>}
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
          {/* The shop's own bill fields (Settings): PO no., doctor, delivery date… printed on the bill. */}
          {billFieldLabels.length > 0 && (
            <div className="grid grid-cols-2 gap-2">
              {billFieldLabels.map((label) => (
                <label key={label} className="flex flex-col gap-1 text-xs font-semibold text-muted">{label}
                  <input value={fieldValues[label] ?? ''} maxLength={100} onChange={(e) => setFieldValues({ ...fieldValues, [label]: e.target.value })}
                    className="h-11 rounded-xl border border-line bg-white px-3 text-base font-normal text-ink outline-none focus:border-brand-line" />
                </label>
              ))}
            </div>
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
          {(() => {
            // Selling more than is in stock is allowed (the purchase may not be entered yet), but the counter should know.
            const short = [...cart.reduce((m, l) => l.productId ? m.set(l.productId, (m.get(l.productId) ?? 0) + l.quantity) : m, new Map<string, number>())]
              .map(([id, qty]) => ({ p: products.find((x) => x.id === id), qty }))
              .filter(({ p, qty }) => p && !p.isService && !p.uncounted && qty > p.stock)
            return short.length > 0 && (
              <p role="status" className="rounded-xl bg-warn-soft p-3 text-sm font-semibold text-warn-ink">
                {t('hwp3.belowZero')} {short.map(({ p, qty }) => t('hwp3.belowZeroItem', { name: p!.name, stock: Math.max(0, p!.stock), qty })).join(' · ')}
              </p>
            )
          })()}
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
                  className={`flex h-14 flex-col items-center justify-center rounded-xl font-semibold transition ${on ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted hover:text-ink'}`}>
                  {m === 'cash' ? <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M3 6h18v12H3zM12 9.5a2.5 2.5 0 100 5 2.5 2.5 0 000-5" /></svg> : m === 'upi' ? <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M7 2h10v20H7zM11 18h2" /></svg> : <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M3 6h18v12H3zM3 10h18M7 15h3" /></svg>}{t(m)}
                </button>
              )
            })}
            {paidAmount > 0 && (
              <button onClick={() => { setMode('cash'); setUpiPart(upiPart === null ? '' : null) }} aria-pressed={upiPart !== null}
                className={`flex h-14 flex-col items-center justify-center rounded-xl font-semibold transition ${upiPart !== null ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted hover:text-ink'}`}>
                <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M5 12h14M12 5h.01M12 19h.01" /></svg>{t('split.button')}
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
          {fromOrder && <p className="rounded-xl bg-brand-soft p-2 text-sm text-brand-deep">{t('saleOrder.fromOrder', { number: fromOrder.number })}</p>}
          {orderSaved !== null && <p className={`rounded-xl p-2 text-sm text-emerald-800 ${btn.link}`}>{t('saleOrder.saved', { number: orderSaved })} <button onClick={() => navigate('/sale-orders')} className="underline">{t('saleOrder.open')}</button></p>}
          <div className="grid grid-cols-2 gap-2">
            <button type="button" onClick={() => void saveEstimate()} disabled={cart.length === 0 || !!fromEstimate} className="flex min-h-12 items-center justify-center gap-2 rounded-2xl bg-[#e8effd] px-3 text-sm font-bold text-[#1d4ed8] transition hover:bg-[#dbe5fc] active:scale-[0.98] disabled:opacity-45"><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M14 3H6v18h12V7zM14 3v4h4M9 13h6M9 17h4" /></svg>{t('estimate.save')}</button>
            <button type="button" onClick={() => void saveOrder()} disabled={cart.length === 0 || !customerId || !!fromOrder || !!fromEstimate} title={!customerId ? t('saleOrder.needCustomer') : undefined} className="flex min-h-12 items-center justify-center gap-2 rounded-2xl bg-[#fdf1e3] px-3 text-sm font-bold text-[#a1460a] transition hover:bg-[#fbe6cc] active:scale-[0.98] disabled:opacity-45"><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M3 7l9-4 9 4v10l-9 4-9-4zM3 7l9 4 9-4M12 11v10" /></svg>{t('saleOrder.save')}</button>
          </div>
          <div className="flex gap-2 border-t border-line pt-4">
            <button type="button" onClick={holdBill} className={`flex min-h-14 shrink-0 items-center gap-1.5 rounded-2xl px-4 ${btn.outline}`}><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M8 5v14M16 5v14" /></svg>{t('hold.button')}</button>
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
/** The desk table's columns: #, item (with its notes under it), qty stepper, rate, disc %, GST %, amount. */
const DESK_COLS = 'xl:grid-cols-[1.25rem_minmax(0,1fr)_4rem_4.5rem_3.5rem_3rem_5.5rem] xl:gap-x-2'

/**
 * A discount % box (desk table only): its own text while typing, so "12.5" can be typed although the line's price is
 * rounded to paise each time. Shows the line's current discount when not being typed in.
 */
function PercentInput({ percent, onChange, onEnter, label, disabled }: { percent: number; onChange: (pct: number) => void; onEnter: () => void; label: string; disabled: boolean }) {
  const [text, setText] = useState<string | null>(null)
  return (
    <input
      value={text ?? (percent ? String(percent) : '')}
      disabled={disabled}
      onFocus={() => setText(percent ? String(percent) : '')}
      onBlur={() => setText(null)}
      onChange={(e) => { const next = e.target.value.replace(/[^\d.]/g, ''); setText(next); const pct = Number(next); if (pct >= 0 && pct < 100) onChange(pct) }}
      onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); onEnter() } }}
      inputMode="decimal"
      aria-label={label}
      placeholder="0"
      className="hidden h-8 w-full rounded-md border border-line px-1.5 text-right disabled:opacity-40 xl:block"
    />
  )
}

/** A line's rate: its own text while typing, so "12." and "12.50" can be typed (a number would drop the point). */
function PriceInput({ value, onChange, ...rest }: { value: number; onChange: (price: number) => void } & Omit<InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange'>) {
  const [text, setText] = useState(value ? String(value) : '')
  // A new price from outside (a pick, a discount) replaces what is typed, unless it already says that ("12." for 12).
  const [shown, setShown] = useState(value)
  if (value !== shown) {
    setShown(value)
    if ((Number(text) || 0) !== value) setText(value ? String(value) : '')
  }
  return <input {...rest} value={text} inputMode="decimal" onChange={(e) => { const next = decimalText(e.target.value); setText(next); onChange(Number(next) || 0) }} />
}

function QuantityInput({ value, onChange, label }: { value: number; onChange: (q: number) => void; label: string }) {
  const [text, setText] = useState(String(value))
  // As for the price: a new quantity from outside replaces what is typed, unless it already says that.
  const [shown, setShown] = useState(value)
  if (value !== shown) {
    setShown(value)
    if (Number(text) !== value) setText(String(value))
  }
  return (
    <input
      value={text}
      onChange={(e) => { const next = decimalText(e.target.value); setText(next); if (Number(next) > 0) onChange(Number(next)) }}
      inputMode="decimal"
      aria-label={label}
      className="h-10 w-14 rounded-lg bg-transparent text-center text-lg font-bold outline-none focus:bg-white focus:ring-2 focus:ring-brand-line/30 lg:h-8 xl:w-full xl:border xl:border-line xl:bg-white xl:text-base"
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

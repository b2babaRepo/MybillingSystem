import { useReorder } from '../../lib/reorder'
import { CustIcon } from '../../components/CustIcons'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Drawer, PendingDot, btn } from '../../components/ui'
import { AlertsPrompt, OrderSteps, UpiPay } from '../../components/OrderPay'
import { formatDateTime, formatRupees, whatsAppLink } from '../../lib/format'
import { api } from '../../lib/api'
import { imageUrl } from '../../lib/images'
import { round2 } from '../../lib/local'
import { canChange, isOpen } from '../../lib/orderFlow'
import { confirmAsk } from '../../components/Ask'
import { navigate } from '../../router'
import { answerChangedLine, homeKey, useMyOrders, type Order, type OrderLine } from '../../lib/planner'
import type { Unit } from '../../lib/types'

const code = (o: Order) => `#${o.id.slice(-4).toUpperCase()}`

/** After delivery: paid in full, the rest on the khata, or still to pay. Null before delivery or for older orders. */
function PaidChip({ order }: { order: Order }) {
  const { t } = useTranslation()
  if (order.status !== 'delivered' || order.paidAmount == null || order.finalAmount == null) return null
  const rest = round2(order.finalAmount - order.paidAmount)
  const [text, tone] = rest <= 0 ? [t('cust7.paidFull'), 'bg-[#e3f2ee] text-brand-deep']
    : order.payment === 'khata' ? [t('cust7.onKhata', { amount: formatRupees(rest) }), 'bg-amber-100 text-amber-900']
    : [t('cust7.due', { amount: formatRupees(rest) }), 'bg-due-soft text-due']
  return <span className={`self-start rounded-lg px-2 py-0.5 text-xs font-bold ${tone}`}>{text}</span>
}
/** Lines the shop changed that wait for the customer's answer. */
const waitingLines = (o: Order) => (o.status === 'packed' ? o.lines.filter((l) => l.approval === 'pending') : [])

/** Mockup 6: each order's progress, what the shop changed, and the final amount once packed. */
type OrderFilter = 'all' | 'open' | 'delivered' | 'cancelled'

export function MyOrdersScreen({ userId }: { userId: string }) {
  const { t } = useTranslation()
  const { orders: recent, isLoading } = useMyOrders(userId)
  // Older history, a page at a time, before the oldest order already shown.
  const [older, setOlder] = useState<Order[]>([])
  const [more, setMore] = useState(true)
  const [loadingMore, setLoadingMore] = useState(false)
  const [q, setQ] = useState('')
  const [filter, setFilter] = useState<OrderFilter>('all')
  const [shopId, setShopId] = useState('')
  const all = [...recent, ...older.filter((o) => !recent.some((r) => r.id === o.id))]
  const canMore = more && recent.filter((o) => !o.pending).length >= 30
  async function loadOlder() {
    const oldest = all.filter((o) => !o.pending).at(-1)
    if (!oldest) return
    setLoadingMore(true)
    try {
      const page = await api<Order[]>(`/v1/me/orders?before=${encodeURIComponent(oldest.createdAt)}`)
      setOlder((o) => [...o, ...page])
      if (page.length < 30) setMore(false)
    } finally {
      setLoadingMore(false)
    }
  }
  const shops = [...new Map(all.map((o) => [o.shopId, o.shopName])).entries()]
  const term = q.trim().toLowerCase()
  const shown = all.filter((o) => (filter === 'all' || (filter === 'open' ? isOpen(o.status) : o.status === filter))
    && (!shopId || o.shopId === shopId)
    && (!term || o.shopName.toLowerCase().includes(term) || o.lines.some((l) => l.name.toLowerCase().includes(term)) || o.id.slice(-4).toLowerCase() === term.replace('#', '')))
  const chip = (value: OrderFilter, label: string) => (
    <button key={value} onClick={() => setFilter(value)} aria-pressed={filter === value} className={`h-10 shrink-0 rounded-full px-4 text-sm font-bold ${filter === value ? 'bg-brand text-white' : 'border border-line bg-white'}`}>{label}</button>
  )

  return (
    <div className="flex flex-col gap-3 p-4">
      {all.length > 0 && <>
        <input value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('cust6.searchOrders')} aria-label={t('cust6.searchOrders')} className="h-12 rounded-xl border border-line bg-white px-4" />
        <div className="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1">
          {chip('all', t('store.all'))}{chip('open', t('cust6.open'))}{chip('delivered', t('plan.status.delivered'))}{chip('cancelled', t('plan.status.cancelled'))}
          {shops.length > 1 && (
            <select value={shopId} onChange={(e) => setShopId(e.target.value)} aria-label={t('cust.shops')} className="h-10 shrink-0 rounded-full border border-line bg-white px-3 text-sm font-bold">
              <option value="">{t('cust6.allShops')}</option>
              {shops.map(([id, name]) => <option key={id} value={id}>{name}</option>)}
            </select>
          )}
        </div>
      </>}
      {!isLoading && all.length === 0 && <p className="py-8 text-center text-muted">{t('plan.noOrders')}</p>}
      {all.length > 0 && shown.length === 0 && <p className="py-6 text-center text-muted">{t('cust6.noMatch')}</p>}
      {shown.map((order) => (
        <OrderCard key={order.id} order={order} />
      ))}
      {canMore && <button onClick={() => void loadOlder()} disabled={loadingMore} className={`h-12 rounded-xl disabled:opacity-50 ${btn.outline}`}>{loadingMore ? '…' : t('cust6.olderOrders')}</button>}
    </div>
  )
}

function OrderCard({ order }: { order: Order }) {
  const { t } = useTranslation()
  const waiting = waitingLines(order)
  const changed = order.lines.filter((l) => l.removed || l.shopNote || (l.packedQuantity !== null && l.packedQuantity !== l.quantity))

  return (
    <section className="flex flex-col gap-3 rounded-card bg-white p-4">
      <div className="flex items-start justify-between gap-2">
        <button onClick={() => navigate(`/orders/${order.id}`)} className="flex flex-col text-left">
          <span className="text-[17px] font-bold">{order.shopName}</span>
          <span className="text-[13px] text-muted">
            {order.source === 'store' ? t('orders2.online') : t(`plan.${order.listKind}List`)} · {t('plan.itemsCount', { count: order.lines.length })} · {formatDateTime(order.createdAt)}
          </span>
          <span className="text-[13px] font-bold text-brand">{t('cust.viewOrder')} ›</span>
        </button>
        <span className={`flex items-center gap-1.5 rounded-lg px-2 py-0.5 text-xs font-bold ${order.status === 'cancelled' ? 'bg-due-soft text-due' : 'bg-[#e0efec] text-brand-strong'}`}>
          {order.pending && <PendingDot />}
          {order.pending ? t('plan.sending') : t(`plan.status.${order.status}`)}
        </span>
      </div>

      <OrderSteps order={order} />
      <PaidChip order={order} />

      {waiting.length > 0 && (
        <button onClick={() => navigate(`/orders/${order.id}`)} className="flex items-center justify-between gap-2 rounded-xl bg-warn-soft p-3 text-left text-sm text-amber-950 ring-1 ring-amber-300">
          <span><b>⚠ {t('cust2.updatedTitle')}</b> · {t('plan.itemsCount', { count: waiting.length })}</span>
          <span className="shrink-0 font-bold">{t('cust2.review')} ›</span>
        </button>
      )}

      {changed.length > 0 && waiting.length === 0 && (
        <div className="flex flex-col gap-1 rounded-xl bg-[#f1f5f4] p-3 text-sm">
          <span className="font-bold">{t('plan.shopChanged')}</span>
          {changed.map((l) => (
            <span key={l.id} className={l.removed ? 'text-due' : ''}>
              {l.name}: {l.removed ? t('plan.removedItem') : `${l.packedQuantity ?? l.quantity} ${t(`units.${l.unit}`, { count: l.packedQuantity ?? l.quantity })}`}
              {l.shopNote && ` · ${l.shopNote}`}
            </span>
          ))}
        </div>
      )}

      {order.finalAmount !== null && (
        <div className="flex justify-between text-lg font-extrabold">
          <span>{t('plan.finalAmount')}</span>
          <span>{formatRupees(order.finalAmount)}</span>
        </div>
      )}
    </section>
  )
}

/**
 * "Shop updated your order": each change side by side (what was asked, what the shop has, the ₹ difference), with
 * OK, Leave out and Call shop. The shop delivers once every change has an answer.
 */
function ChangesReview({ order, userId }: { order: Order; userId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [busy, setBusy] = useState<string | null>(null)
  const [message, setMessage] = useState('')
  const waiting = waitingLines(order)
  if (waiting.length === 0) return null
  async function answer(lineId: string, approve: boolean) {
    setBusy(lineId)
    setMessage('')
    try {
      await answerChangedLine(order.id, lineId, approve)
      await queryClient.invalidateQueries({ queryKey: [homeKey(userId), 'orders'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    } finally {
      setBusy(null)
    }
  }
  const qty = (l: OrderLine, q: number) => `${q} ${t(`units.${l.unit}`, { count: q })}`
  return (
    <section className="flex flex-col gap-3 rounded-card bg-warn-soft p-4 text-amber-950 ring-1 ring-amber-300" aria-labelledby="changes-title">
      <div>
        <h2 id="changes-title" className="text-lg font-extrabold">⚠ {t('cust2.updatedTitle')}</h2>
        <p className="text-sm">{t('cust2.updatedHint')}</p>
      </div>
      {waiting.map((l) => {
        const diff = l.expectedAmount != null && l.amount != null ? round2(l.amount - l.expectedAmount) : null
        return (
          <div key={l.id} className="flex flex-col gap-2 rounded-xl bg-white p-3 text-ink">
            <div className="grid grid-cols-2 gap-2 text-sm">
              <div className="flex flex-col rounded-lg bg-[#f1f5f4] p-2">
                <span className="text-xs text-muted">{t('cust2.asked')}</span>
                <b className="line-through decoration-due/60">{l.requestedName ?? l.name}</b>
                <span>{qty(l, l.quantity)}</span>
                {l.expectedAmount != null && <span className="text-muted">{formatRupees(l.expectedAmount)}</span>}
              </div>
              <div className="flex flex-col rounded-lg bg-brand-soft p-2">
                <span className="text-xs text-muted">{t('cust2.shopGives')}</span>
                <b>{l.name}</b>
                <span>{qty(l, l.packedQuantity ?? l.quantity)}</span>
                {l.amount != null && <span className="font-semibold">{formatRupees(l.amount)}</span>}
              </div>
            </div>
            {diff !== null && diff !== 0 && <span className={`text-sm font-bold ${diff > 0 ? 'text-due' : 'text-green-700'}`}>{t('cust2.diff')}: {diff > 0 ? '+' : '−'} {formatRupees(Math.abs(diff))}</span>}
            {l.shopNote && <span className="text-sm text-muted">“{l.shopNote}”</span>}
            <div className="grid grid-cols-2 gap-2">
              <button disabled={busy === l.id} onClick={() => void answer(l.id, true)} className={`min-h-11 rounded-xl ${btn.solid}`}>{t('options.ok')}</button>
              <button disabled={busy === l.id} onClick={() => void answer(l.id, false)} className={`min-h-11 ${btn.dangerOutline}`}>{t('options.leaveOut')}</button>
            </div>
          </div>
        )
      })}
      {order.shopPhone && <a href={`tel:${order.shopPhone}`} className="flex h-11 items-center justify-center rounded-xl border border-amber-400 bg-white font-bold">📞 {t('cust.callShop')}</a>}
      {message && <p className="text-sm text-due">{message}</p>}
    </section>
  )
}

interface StoreItemLite { id: string; name: string; unit: Unit; price: number; available: number; imageAt?: string | null }
interface StoreLite { shopId: string; upiId?: string | null; items: StoreItemLite[] }

/** Adding to an order the shop hasn't packed yet: the shop's items with steppers, no extra charge. */
function AddItems({ order, userId, onClose }: { order: Order; userId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const store = useQuery({ queryKey: ['store', order.shopSlug], queryFn: () => fetch(`/api/v1/store/${order.shopSlug}`).then((r) => (r.ok ? (r.json() as Promise<StoreLite>) : null)), enabled: !!order.shopSlug })
  const [q, setQ] = useState('')
  const [picked, setPicked] = useState<Record<string, number>>({})
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const term = q.trim().toLowerCase()
  const items = (store.data?.items ?? []).filter((i) => i.available > 0 && (!term || i.name.toLowerCase().includes(term))).slice(0, 60)
  const count = Object.values(picked).filter((n) => n > 0).length
  const step = (i: StoreItemLite, d: number) => setPicked((p) => ({ ...p, [i.id]: Math.max(0, Math.min(i.available, round2((p[i.id] ?? 0) + d))) }))
  async function add() {
    setBusy(true)
    setMessage('')
    try {
      await api(`/v1/me/orders/${order.id}/add`, 'POST', { lines: Object.entries(picked).filter(([, n]) => n > 0).map(([productId, quantity]) => ({ productId, quantity })) })
      await queryClient.invalidateQueries({ queryKey: [homeKey(userId), 'orders'] })
      onClose()
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
      setBusy(false)
    }
  }
  return (
    <Drawer title={t('cust2.addItems')} onClose={onClose}>
      <div className="flex flex-col gap-3">
        <p className="text-sm text-muted">{t('cust2.addItemsHint')}</p>
        <input value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('cust2.searchItems')} aria-label={t('cust2.searchItems')} className="h-12 rounded-xl border border-line bg-white px-4" />
        <ul className="flex flex-col divide-y divide-[#edf2f1] rounded-card bg-white">
          {items.map((i) => (
            <li key={i.id} className="flex items-center gap-3 px-3 py-2">
              <span className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-[#f1f5f4]">
                {i.imageAt && store.data ? <img src={imageUrl(store.data.shopId, i.id, i.imageAt, 'thumb')} alt="" className="h-full w-full object-contain" /> : <span aria-hidden>🛍️</span>}
              </span>
              <span className="flex min-w-0 flex-1 flex-col">
                <span className="truncate font-semibold">{i.name}</span>
                <span className="text-sm text-muted">{formatRupees(i.price)} / {t(`units.${i.unit}`)}</span>
              </span>
              {(picked[i.id] ?? 0) === 0 ? (
                <button onClick={() => step(i, 1)} aria-label={`${t('store.add')} ${i.name}`} className={`h-10 rounded-xl px-4 ${btn.outline}`}>{t('store.add')}</button>
              ) : (
                <span className="flex items-center gap-1">
                  <button onClick={() => step(i, -1)} aria-label={t('a11y.less')} className="h-10 w-10 rounded-xl bg-[#edf2f1] text-xl">−</button>
                  <b className="min-w-8 text-center">{picked[i.id]}</b>
                  <button onClick={() => step(i, 1)} aria-label={t('a11y.more')} className="h-10 w-10 rounded-xl bg-[#edf2f1] text-xl">+</button>
                </span>
              )}
            </li>
          ))}
        </ul>
        {message && <p className="text-sm text-due">{message}</p>}
        <button disabled={busy || count === 0} onClick={() => void add()} className={`sticky bottom-0 h-14 rounded-xl text-lg ${btn.solid} disabled:opacity-50`}>{t('cust2.addTo', { count })}</button>
      </div>
    </Drawer>
  )
}

interface MyKhataRow { shopId: string; customerId: string; confirmed: boolean }

/** One order in full: every line, the progress, what the shop changed, how and where, the bill, and Call / WhatsApp / Cancel. */
/** The order's state in words, big: what is happening and what (if anything) waits on the customer. */
function StatusHero({ order }: { order: Order }) {
  const { t } = useTranslation()
  const waiting = waitingLines(order).length
  const key = order.pending ? 'sending' : waiting > 0 ? 'waiting' : order.status
  const tone = order.status === 'cancelled' ? 'bg-due text-white' : waiting > 0 ? 'bg-amber-500 text-amber-950' : 'bg-brand text-white'
  return (
    <section className={`flex flex-col gap-1 rounded-card p-4 ${tone}`}>
      <span className="text-sm font-semibold opacity-90">#{order.id.slice(-4).toUpperCase()} · {order.shopName}</span>
      <h2 className="text-2xl font-extrabold leading-tight">{order.pending ? t('plan.sending') : waiting > 0 ? t('cust2.updatedTitle') : t(`plan.status.${order.status}`)}</h2>
      <p className="text-sm opacity-90">{t(`cust5.hint.${key}`, { count: waiting })}</p>
    </section>
  )
}

export function OrderDetail({ userId, userName = '', orderId }: { userId: string; userName?: string; orderId: string }) {
  const { t } = useTranslation()
  const { reorder, busy: reordering, label: reorderLabel } = useReorder(userId, userName)
  const [openingBill, setOpeningBill] = useState(false)
  const queryClient = useQueryClient()
  const { orders, isLoading } = useMyOrders(userId)
  const [message, setMessage] = useState('')
  const [adding, setAdding] = useState(false)
  const order = orders.find((o) => o.id === orderId)
  const store = useQuery({ queryKey: ['store', order?.shopSlug], queryFn: () => fetch(`/api/v1/store/${order!.shopSlug}`).then((r) => (r.ok ? (r.json() as Promise<StoreLite>) : null)), enabled: !!order?.shopSlug && order.payment === 'upi', staleTime: 60_000 })
  const khatas = useQuery({ queryKey: ['my-khata'], queryFn: () => api<MyKhataRow[]>('/v1/me/khata'), enabled: order?.payment === 'khata', retry: false }).data ?? []
  if (!order) return <p className="p-6 text-center text-muted">{isLoading ? '…' : t('cust.orderGone')}</p>

  async function cancel() {
    if (!order || !(await confirmAsk(t('cust.cancelConfirm', { shop: order.shopName })))) return
    setMessage('')
    try {
      await api(`/v1/me/orders/${order.id}/cancel`, 'POST', {})
      await queryClient.invalidateQueries({ queryKey: [homeKey(userId), 'orders'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  // The shop's bill for a delivered order: the same signed page the shop sends, to print or save as a PDF.
  async function openBill() {
    if (!order) return
    setOpeningBill(true)
    setMessage('')
    try {
      const link = await api<{ shopSlug: string; billId: string; token: string }>(`/v1/me/orders/${order.id}/bill-link`)
      navigate(`/b/${link.shopSlug}/${link.billId}?t=${link.token}`)
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    } finally {
      setOpeningBill(false)
    }
  }

  const fee = order.deliveryFee ?? 0
  const total = order.finalAmount ?? round2(fee + order.lines.reduce((s, l) => s + (l.amount ?? 0), 0))
  const khata = khatas.find((k) => k.shopId === order.shopId && k.confirmed)
  const upiId = store.data?.upiId
  return (
    <div className="flex flex-col gap-3 p-4">
      <StatusHero order={order} />
      <OrderCard order={order} />
      <ChangesReview order={order} userId={userId} />
      {order.payment === 'upi' && upiId && isOpen(order.status) && !order.pending && total > 0 && <UpiPay upiId={upiId} shopName={order.shopName} amount={total} note={`${order.shopName} ${code(order)}`} />}
      <AlertsPrompt show={!order.pending && isOpen(order.status)} />
      <section className="flex flex-col gap-1 rounded-card bg-white p-4">
        <h2 className="mb-1 font-bold">{t('cust.items')}</h2>
        {order.lines.map((l) => (
          <div key={l.id} className={`flex items-start justify-between gap-2 border-t border-[#edf2f1] py-2 text-sm first-of-type:border-t-0 ${l.removed ? 'text-muted line-through' : ''}`}>
            <span className="flex min-w-0 flex-col">
              <b className="truncate">{l.name}</b>
              <span className="text-xs text-muted">{l.packedQuantity ?? l.quantity} {t(`units.${l.unit}`, { count: l.packedQuantity ?? l.quantity })}{l.unitPrice != null ? ` × ${formatRupees(l.unitPrice)}` : ''}</span>
            </span>
            <span className="shrink-0 font-semibold">{l.amount != null ? formatRupees(l.amount) : '—'}</span>
          </div>
        ))}
        {fee > 0 && <div className="flex justify-between border-t border-[#edf2f1] py-2 text-sm"><span>{t('cust2.delivery')}</span><span className="font-semibold">{formatRupees(fee)}</span></div>}
        {total > 0 && <div className="mt-1 flex justify-between border-t border-line pt-2 text-lg font-extrabold"><span>{order.finalAmount === null ? t('store.estimate') : t('plan.finalAmount')}</span><span>{formatRupees(total)}</span></div>}
        {canChange(order.status) && !order.pending && order.shopSlug && (
          <button onClick={() => setAdding(true)} className={`mt-2 h-11 rounded-xl ${btn.outline}`}>+ {t('cust2.addItems')}</button>
        )}
      </section>
      <section className="flex flex-col gap-1 rounded-card bg-white p-4 text-sm">
        <span><b>{t(`plan.${order.fulfilment}`)}</b>{order.slot ? ` · ${order.slot}` : ''}</span>
        {order.address && <span className="text-muted">📍 {order.address}</span>}
        <span className="text-muted">{t('cust.payment')}: {t(`cust.pay.${order.payment}`)}</span>
        {order.note && <span className="text-muted">“{order.note}”</span>}
        {/* Paid on khata: the bill is that khata's entry, one tap away. */}
        {order.payment === 'khata' && order.status === 'delivered' && khata && (
          <button onClick={() => navigate(`/khata/${khata.shopId}/${khata.customerId}`)} className="mt-1 flex min-h-11 items-center justify-between rounded-xl bg-brand-soft px-3 font-semibold text-brand-deep">
            <span>📒 {t('cust2.onKhata', { shop: order.shopName })}</span><span>{t('cust2.openKhata')} ›</span>
          </button>
        )}
      </section>
      {(order.billId || (!isOpen(order.status) && !order.pending)) && (
        <div className="grid grid-cols-2 gap-2">
          {order.billId && <button onClick={() => void openBill()} disabled={openingBill} className={`flex h-12 items-center justify-center gap-2 rounded-xl font-bold disabled:opacity-50 ${btn.solid}`}><CustIcon name="orders" size={18} />{t('cust6.viewBill')}</button>}
          {!isOpen(order.status) && !order.pending && order.lines.some((l) => !l.removed) && (
            <button onClick={() => void reorder(order)} disabled={reordering} className={`h-12 rounded-xl font-bold disabled:opacity-50 ${order.billId ? 'border-2 border-brand text-brand' : `col-span-2 ${btn.solid}`}`}>↻ {reorderLabel(order)}</button>
          )}
        </div>
      )}
      <section className="flex flex-col gap-0.5 rounded-card bg-white p-4 text-sm">
        <span className="text-xs font-semibold uppercase tracking-wide text-muted">{t('cust6.soldBy')}</span>
        <b className="text-base">{order.shopName}</b>
        {order.shopAddress && <span className="text-muted">{order.shopAddress}</span>}
        <span className="mt-1 text-xs text-muted">{t('cust6.problemHint')}</span>
      </section>
      <div className="grid grid-cols-2 gap-2">
        {order.shopPhone && <a href={`tel:${order.shopPhone}`} className="flex h-12 items-center justify-center gap-2 rounded-xl border-2 border-brand font-bold text-brand"><CustIcon name="phone" size={18} />{t('cust.callShop')}</a>}
        {order.shopPhone && <a href={whatsAppLink(order.shopPhone, t('cust.aboutOrder', { shop: order.shopName, id: order.id.slice(-4).toUpperCase() }))} target="_blank" rel="noreferrer" className="flex h-12 items-center justify-center gap-2 rounded-xl bg-[#e3f2ee] font-bold text-brand-deep"><CustIcon name="chat" size={18} />WhatsApp</a>}
        {order.shopSlug && <button onClick={() => navigate(`/s/${order.shopSlug}`)} className="col-span-2 h-12 rounded-xl border border-line font-semibold">{t('cust.openShop')}</button>}
        {canChange(order.status) && !order.pending && <button onClick={() => void cancel()} className="col-span-2 h-12 rounded-xl border border-due font-bold text-due">{t('cust.cancelOrder')}</button>}
      </div>
      {message && <p className="text-sm text-due">{message}</p>}
      {adding && <AddItems order={order} userId={userId} onClose={() => setAdding(false)} />}
    </div>
  )
}

/** On a khata page: the app orders delivered on this khata, each opening its order. */
export function KhataOrders({ userId, shopId }: { userId: string; shopId: string }) {
  const { t } = useTranslation()
  const { orders } = useMyOrders(userId)
  const onKhata = orders.filter((o) => o.shopId === shopId && o.payment === 'khata' && o.status === 'delivered')
  if (onKhata.length === 0) return null
  return (
    <section className="flex flex-col gap-1 rounded-card bg-white p-4">
      <h2 className="font-bold">{t('cust2.ordersOnKhata')}</h2>
      {onKhata.map((o) => (
        <button key={o.id} onClick={() => navigate(`/orders/${o.id}`)} className="flex min-h-11 items-center justify-between border-t border-[#edf2f1] text-left text-sm first-of-type:border-t-0">
          <span>📦 {t('cust2.fromOrder', { code: code(o) })} · {formatDateTime(o.deliveredAt ?? o.createdAt)}</span>
          <b>{o.finalAmount != null ? formatRupees(o.finalAmount) : ''} ›</b>
        </button>
      ))}
    </section>
  )
}

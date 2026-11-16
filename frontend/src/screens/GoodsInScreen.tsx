import { GodownPicker } from '../components/GodownPicker'
import { lazy, Suspense, useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ItemPicker } from '../components/ItemPicker'
import { api } from '../lib/api'
import { normBarcode } from '../lib/barcode'
import { useProducts } from '../lib/data'
import { whatsAppLink } from '../lib/format'

const ScanSheet = lazy(() => import('../components/ScanSheet').then((m) => ({ default: m.ScanSheet })))

interface CheckLine { orderLineId: string; productId: string; name: string; category: string | null; unit: string; ordered: number; received: number; shortClosed: number; pending: number; barcode: string | null }
interface CheckOrder { id: string; number: number; supplierId: string; supplierName: string; supplierPhone: string | null; expectedOn: string | null; note: string | null; lines: CheckLine[] }
interface Count { came: string; damaged: string }
interface Extra { productId: string; name: string; came: string }

const REASONS = ['not_sent', 'out_of_stock', 'damaged', 'wrong_item', 'rate_dispute', 'other'] as const
const num = (s: string) => (s.trim() === '' ? 0 : Number(s))
const round = (n: number) => Math.round(n * 1000) / 1000

/**
 * Check goods in: pick the order, tick each line as it comes off the truck (or type a different count), note damaged
 * units, add anything that came without being ordered. On save, every short line gets a decision: wait for the rest,
 * or close it short with a reason; the short list can go to the supplier on WhatsApp.
 */
export function GoodsInScreen({ shopId, shopName, isOwner }: { shopId: string; shopName: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const orders = useQuery({ queryKey: [shopId, 'goods-in'], queryFn: () => api<CheckOrder[]>('/v1/goods-in/orders') })
  const { products } = useProducts(shopId)
  const [orderId, setOrderId] = useState<string | null>(null)
  const [counts, setCounts] = useState<Record<string, Count>>({})
  const [extras, setExtras] = useState<Extra[]>([])
  const [extraText, setExtraText] = useState('')
  const [scan, setScan] = useState(false)
  const [flash, setFlash] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [godown, setGodown] = useState('')
  const [message, setMessage] = useState('')
  // After saving: the short lines, each to wait for or close short.
  const [shorts, setShorts] = useState<{ order: CheckOrder; lines: { line: CheckLine; short: number; reason: string; closed: boolean }[] } | null>(null)

  const order = orders.data?.find((o) => o.id === orderId) ?? null
  const countOf = (l: CheckLine) => counts[l.productId] ?? { came: '', damaged: '' }
  const setCount = (productId: string, patch: Partial<Count>) => setCounts((c) => ({ ...c, [productId]: { ...(c[productId] ?? { came: '', damaged: '' }), ...patch } }))
  const tone = (l: CheckLine) => {
    const c = countOf(l)
    if (c.came === '') return 'border-line'
    return num(c.came) >= l.pending ? 'border-emerald-500 bg-emerald-50/60' : num(c.came) > 0 ? 'border-amber-500 bg-amber-50/60' : 'border-red-400 bg-red-50/60'
  }

  function start(o: CheckOrder) {
    setOrderId(o.id)
    setCounts({})
    setExtras([])
    setMessage('')
    setShorts(null)
  }

  function onCode(code: string) {
    const c = normBarcode(code)
    const line = order?.lines.find((l) => normBarcode(l.barcode) === c)
    if (!line) return false
    // Each scan is one more unit of that line.
    setCount(line.productId, { came: String(round(num(countOf(line).came) + 1)) })
    setFlash(line.productId)
    document.getElementById(`gi-${line.productId}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' })
    return true
  }

  function addExtra(name: string) {
    const p = products.find((x) => x.name === name)
    if (!p || !order) { setExtraText(name); return }
    if (order.lines.some((l) => l.productId === p.id) || extras.some((e) => e.productId === p.id)) { setExtraText(''); return }
    setExtras([...extras, { productId: p.id, name: p.name, came: '' }])
    setExtraText('')
  }

  async function save() {
    if (!order) return
    const lines = [
      ...order.lines.filter((l) => num(countOf(l).came) > 0).map((l) => ({ productId: l.productId, quantity: num(countOf(l).came), damaged: num(countOf(l).damaged) || null })),
      ...extras.filter((e) => num(e.came) > 0).map((e) => ({ productId: e.productId, quantity: num(e.came), damaged: null as number | null })),
    ]
    if (lines.some((l) => !(l.quantity > 0) || (l.damaged ?? 0) > l.quantity || (l.damaged ?? 0) < 0)) { setMessage(t('gi.badCount')); return }
    if (lines.length === 0) { setMessage(t('gi.nothing')); return }
    setBusy(true)
    setMessage('')
    try {
      await api('/v1/goods-receipts', 'POST', { id: crypto.randomUUID(), supplierId: order.supplierId, orderId: order.id, lines, godownId: godown || null })
      const short = order.lines.map((l) => ({ line: l, short: round(l.pending - num(countOf(l).came)), reason: 'out_of_stock', closed: false })).filter((x) => x.short > 0)
      setShorts(short.length > 0 ? { order, lines: short } : null)
      setMessage(t('gi.saved', { number: order.number }))
      setOrderId(null)
      void queryClient.invalidateQueries({ queryKey: [shopId, 'goods-in'] })
      void queryClient.invalidateQueries({ queryKey: [shopId, 'suppliers'] })
      void queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    } finally {
      setBusy(false)
    }
  }

  async function closeShort(i: number) {
    if (!shorts) return
    const s = shorts.lines[i]
    try {
      await api(`/v1/suppliers/orders/${shorts.order.id}/close-short`, 'POST', { productId: s.line.productId, reason: s.reason })
      setShorts({ ...shorts, lines: shorts.lines.map((x, j) => (j === i ? { ...x, closed: true } : x)) })
      void queryClient.invalidateQueries({ queryKey: [shopId, 'goods-in'] })
      void queryClient.invalidateQueries({ queryKey: [shopId, 'suppliers'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  const shortText = (s: NonNullable<typeof shorts>) => t('gi.shortMessage', {
    shop: shopName, number: s.order.number,
    lines: s.lines.map((x) => `• ${x.line.name}: ${x.short} ${t(`units.${x.line.unit}`, { count: x.short })}${x.closed ? ` (${t(`gi.reason.${x.reason}`)})` : ''}`).join('\n'),
  })

  // The order list.
  if (!order) {
    return (
      <div className="flex flex-col gap-3 p-4">
        <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm">
          <h1 className="text-xl font-bold">🚚 {t('gi.title')}</h1>
          <p className="text-sm text-muted">{t('gi.hint')}</p>
        </section>
        {message && <p className="rounded-xl bg-teal-50 p-3 text-sm font-semibold text-teal-900">{message}</p>}
        {shorts && (
          <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm">
            <h2 className="font-bold">{t('gi.shortTitle', { number: shorts.order.number })}</h2>
            <p className="text-xs text-muted">{t(isOwner ? 'gi.shortHint' : 'gi.shortHintStaff')}</p>
            {shorts.lines.map((x, i) => (
              <div key={x.line.productId} className="flex flex-wrap items-center gap-2 rounded-xl border border-line p-2 text-sm">
                <span className="min-w-0 flex-1"><b>{x.line.name}</b> · {t('gi.shortBy', { qty: x.short })}</span>
                {x.closed ? <span className="text-xs font-semibold text-muted">{t('gi.closedShort', { reason: t(`gi.reason.${x.reason}`) })}</span> : isOwner && (
                  <>
                    <select aria-label={t('gi.why')} value={x.reason} onChange={(e) => setShorts({ ...shorts, lines: shorts.lines.map((y, j) => (j === i ? { ...y, reason: e.target.value } : y)) })} className="h-10 rounded-xl border border-line bg-white px-2 text-sm">
                      {REASONS.map((r) => <option key={r} value={r}>{t(`gi.reason.${r}`)}</option>)}
                    </select>
                    <button onClick={() => void closeShort(i)} className="h-10 rounded-xl border border-line px-3 text-sm font-semibold text-due hover:bg-red-50">{t('gi.closeShort')}</button>
                  </>
                )}
              </div>
            ))}
            <div className="flex flex-wrap gap-2">
              {shorts.order.supplierPhone && <a href={whatsAppLink(shorts.order.supplierPhone, shortText(shorts))} target="_blank" rel="noreferrer" className="flex h-11 items-center rounded-xl bg-[#25D366] px-4 text-sm font-bold text-white">{t('gi.sendShort')}</a>}
              <button onClick={() => setShorts(null)} className="h-11 rounded-xl border border-line px-4 text-sm font-semibold">{t('gi.wait')}</button>
            </div>
          </section>
        )}
        {orders.isError && <p className="text-sm text-due">{t('error')}</p>}
        {orders.data?.length === 0 && <p className="py-6 text-center text-muted">{t('gi.noOrders')}</p>}
        {orders.data?.map((o) => {
          const pending = o.lines.reduce((s, l) => s + l.pending, 0)
          return (
            <button key={o.id} onClick={() => start(o)} className="flex items-center justify-between gap-2 rounded-[18px] bg-white p-4 text-left shadow-sm hover:bg-[#f6faf9]">
              <span className="flex flex-col">
                <b>{o.supplierName} · {t('supplier2.orderNo', { number: o.number })}</b>
                <span className="text-xs text-muted">{t('po.types', { count: o.lines.filter((l) => l.pending > 0).length })}{o.expectedOn && ` · ${t('po.expected', { date: new Date(o.expectedOn).toLocaleDateString('en-IN', { day: 'numeric', month: 'short' }) })}`}</span>
              </span>
              <span className="shrink-0 rounded-full bg-teal-50 px-3 py-1 text-sm font-semibold text-teal-800">{t('supplier2.stillToCome', { qty: round(pending) })}</span>
            </button>
          )
        })}
      </div>
    )
  }

  // The tick-list, grouped by category.
  const groups = new Map<string, CheckLine[]>()
  for (const l of order.lines.filter((x) => x.pending > 0)) groups.set(l.category ?? t('gi.noCategory'), [...(groups.get(l.category ?? t('gi.noCategory')) ?? []), l])
  const done = order.lines.filter((l) => l.pending > 0 && num(countOf(l).came) >= l.pending).length
  const total = order.lines.filter((l) => l.pending > 0).length
  return (
    <div className="flex flex-col gap-3 p-4">
      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm">
        <button onClick={() => setOrderId(null)} className="self-start text-sm font-semibold text-teal-700">← {t('gi.allOrders')}</button>
        <h1 className="text-lg font-bold">{order.supplierName} · {t('supplier2.orderNo', { number: order.number })}</h1>
        {order.note && <p className="text-xs text-muted">{order.note}</p>}
        <div className="flex items-center gap-2">
          <span className="h-2 flex-1 overflow-hidden rounded-full bg-[#edf2f1]"><span className="block h-full rounded-full bg-emerald-500" style={{ width: `${total ? (done / total) * 100 : 0}%` }} /></span>
          <span className="text-sm font-semibold">{t('gi.done', { done, total })}</span>
        </div>
        <div className="flex flex-wrap gap-2">
          <button onClick={() => order.lines.forEach((l) => l.pending > 0 && countOf(l).came === '' && setCount(l.productId, { came: String(l.pending) }))} className="h-10 rounded-xl border border-line px-3 text-sm font-semibold">✓ {t('gi.allCame')}</button>
          <button onClick={() => setScan(true)} className="h-10 rounded-xl border border-line px-3 text-sm font-semibold">▦ {t('gi.scan')}</button>
        </div>
      </section>
      {[...groups.entries()].map(([category, lines]) => (
        <section key={category} className="flex flex-col gap-2">
          <h2 className="px-1 text-sm font-bold text-muted">{category} · {t('gi.groupDone', { done: lines.filter((l) => num(countOf(l).came) >= l.pending).length, total: lines.length })}</h2>
          {lines.map((l) => {
            const c = countOf(l)
            const full = c.came !== '' && num(c.came) >= l.pending
            return (
              <div key={l.productId} id={`gi-${l.productId}`} className={`flex flex-col gap-2 rounded-2xl border-2 bg-white p-3 shadow-sm transition ${tone(l)} ${flash === l.productId ? 'ring-2 ring-teal-600' : ''}`}>
                <div className="flex items-center gap-3">
                  <button aria-label={t('gi.tick', { name: l.name })} aria-pressed={full} onClick={() => setCount(l.productId, { came: full ? '' : String(l.pending) })}
                    className={`grid size-12 shrink-0 place-items-center rounded-xl text-2xl font-bold ${full ? 'bg-emerald-600 text-white' : 'border-2 border-line text-transparent'}`}>✓</button>
                  <span className="flex min-w-0 flex-1 flex-col">
                    <b className="truncate">{l.name}</b>
                    <span className="text-xs text-muted">{t('gi.toCome', { qty: l.pending, unit: t(`units.${l.unit}`, { count: l.pending }) })}{l.received > 0 && ` · ${t('supplier2.cameOf', { came: l.received, ordered: l.ordered })}`}</span>
                  </span>
                  <input aria-label={t('gi.came')} value={c.came} onChange={(e) => setCount(l.productId, { came: e.target.value })} inputMode="decimal" placeholder={t('gi.came')} className="h-12 w-20 rounded-xl border border-line bg-white px-2 text-right" />
                </div>
                {c.came !== '' && (
                  <label className="flex items-center justify-end gap-2 text-xs text-muted">{t('gi.damaged')}
                    <input aria-label={t('gi.damaged')} value={c.damaged} onChange={(e) => setCount(l.productId, { damaged: e.target.value })} inputMode="decimal" placeholder="0" className="h-10 w-20 rounded-xl border border-line bg-white px-2 text-right" />
                  </label>
                )}
                {c.came !== '' && num(c.came) > l.pending && <p className="text-xs font-semibold text-amber-800">⚠ {t('po.moreThanOrdered', { name: l.name, qty: round(num(c.came) - l.pending) })}</p>}
              </div>
            )
          })}
        </section>
      ))}
      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4 shadow-sm">
        <h2 className="text-sm font-bold">{t('gi.extraTitle')}</h2>
        <p className="text-xs text-muted">{t('gi.extraHint')}</p>
        {extras.map((e, i) => (
          <div key={e.productId} className="flex items-center gap-2 text-sm">
            <span className="min-w-0 flex-1 truncate font-semibold">{e.name}</span>
            <input aria-label={t('gi.came')} value={e.came} onChange={(ev) => setExtras(extras.map((x, j) => (j === i ? { ...x, came: ev.target.value } : x)))} inputMode="decimal" placeholder={t('gi.came')} className="h-11 w-20 rounded-xl border border-line px-2 text-right" />
            <button aria-label={t('remove')} onClick={() => setExtras(extras.filter((_, j) => j !== i))} className="h-11 px-2 text-due">✕</button>
          </div>
        ))}
        <ItemPicker products={products} text={extraText} placeholder={t('supplier2.itemSearch')} onText={addExtra} onPick={(p) => addExtra(p.name)} />
      </section>
      {message && <p className="text-sm text-due">{message}</p>}
      <div className="sticky bottom-2 z-10 rounded-2xl bg-white/95 p-2 shadow-lg ring-1 ring-black/5 backdrop-blur">
        <GodownPicker shopId={shopId} value={godown} onChange={setGodown} label="in" />
        <button disabled={busy} onClick={() => void save()} className="h-12 w-full rounded-xl bg-teal-700 font-bold text-white shadow-sm hover:bg-teal-800 disabled:opacity-60">{t('gi.save')}</button>
      </div>
      {scan && <Suspense fallback={null}><ScanSheet continuous onCode={onCode} onClose={() => setScan(false)} /></Suspense>}
    </div>
  )
}

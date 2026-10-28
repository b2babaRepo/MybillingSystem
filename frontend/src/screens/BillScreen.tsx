import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ItemCard } from '../components/ItemCard'
import { Button, Field, Sheet } from '../components/ui'
import { saveBill, useCustomers, usePopularOrder, useProducts, type CartLine } from '../lib/data'
import { formatDateTime, formatRupees, whatsAppLink } from '../lib/format'
import { round2 } from '../lib/local'
import type { PaymentMode, Unit } from '../lib/types'
import { navigate } from '../router'

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
  const [loose, setLoose] = useState(false)
  const [saved, setSaved] = useState<{ id: string; total: number; lines: CartLine[]; paid: number; customer: { name: string; phone: string | null } | null } | null>(null)

  const total = useMemo(() => round2(cart.reduce((sum, l) => sum + round2(l.quantity * l.unitPrice), 0)), [cart])
  const customer = customers.find((c) => c.id === customerId) ?? null
  const paidAmount = paid === null || !customer ? total : Math.min(Number(paid) || 0, total)

  function add(line: CartLine) {
    setSaved(null)
    setCart((current) => {
      const index = line.productId ? current.findIndex((l) => l.productId === line.productId) : -1
      if (index < 0) return [...current, line]
      return current.map((l, i) => (i === index ? { ...l, quantity: round2(l.quantity + 1) } : l))
    })
  }

  function setQuantity(index: number, quantity: number) {
    setCart((current) => (quantity <= 0 ? current.filter((_, i) => i !== index) : current.map((l, i) => (i === index ? { ...l, quantity } : l))))
  }

  /** Prices change, and starter-catalogue items have none yet: the counter price is editable per line. */
  function setPrice(index: number, unitPrice: number) {
    setCart((current) => current.map((l, i) => (i === index ? { ...l, unitPrice } : l)))
  }

  async function save() {
    const result = await saveBill(shopId, cart, customer, paidAmount, mode)
    setSaved({ ...result, lines: cart, paid: paidAmount, customer: customer && { name: customer.name, phone: customer.phone } })
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
            title={p.name}
            subtitle={p.sellingPrice > 0 ? `${formatRupees(p.sellingPrice)}/${t(`units.${p.unit}`)}` : t('stock.noPrice')}
            badge={cart.filter((l) => l.productId === p.id).reduce((n, l) => n + l.quantity, 0) || undefined}
            onClick={() => add({ productId: p.id, name: p.name, quantity: 1, unit: p.unit, unitPrice: p.sellingPrice })}
          />
        ))}
        <button onClick={() => setLoose(true)} className="min-h-20 rounded-xl border-2 border-dashed border-line p-2 font-semibold text-muted">
          + {t('addLooseItem')}
        </button>
      </div>

      <div className="rounded-[18px] bg-white p-3">
        {cart.length === 0 && (
          <div className="flex flex-col items-center gap-2 py-4 text-center text-muted">
            <p>{saved ? `${t('saved')} ${formatRupees(saved.total)}` : t('emptyCart')}</p>
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
              <div className="font-semibold">{l.name}</div>
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
            </div>
            <button className="h-11 w-11 rounded-lg bg-[#edf2f1] text-xl" onClick={() => setQuantity(i, round2(l.quantity - 1))}>−</button>
            <input
              value={l.quantity}
              onChange={(e) => setQuantity(i, Number(e.target.value) || 0)}
              inputMode="decimal"
              className="h-11 w-16 rounded-lg border border-line text-center"
            />
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
          <div className="flex gap-2 text-sm">
            {(['cash', 'upi'] as const).map((m) => (
              <button key={m} onClick={() => setMode(m)} className={`h-11 rounded-xl px-4 font-semibold ${mode === m ? 'bg-ink text-white' : 'border border-line bg-white text-ink'}`}>
                {t(m)}
              </button>
            ))}
          </div>
          <Button className="w-full" onClick={save} disabled={total <= 0}>
            {t('saveBill')} · {formatRupees(total)}
          </Button>
        </div>
      )}

      {loose && <LooseItemSheet onClose={() => setLoose(false)} onAdd={add} />}
    </div>
  )
}

function LooseItemSheet({ onClose, onAdd }: { onClose: () => void; onAdd: (line: CartLine) => void }) {
  const { t } = useTranslation()
  const [name, setName] = useState('')
  const [price, setPrice] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [unit, setUnit] = useState<Unit>('kg')
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

const UNITS: Unit[] = ['piece', 'kg', 'g', 'litre', 'ml', 'packet']

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
function billReceipt(
  t: (key: string, options?: Record<string, unknown>) => string,
  shopName: string,
  bill: { total: number; lines: CartLine[]; paid: number; customer: { name: string } | null },
) {
  const items = bill.lines.map((l) => `• ${l.name} ${l.quantity} ${t(`units.${l.unit}`)} × ${formatRupees(l.unitPrice)} = ${formatRupees(Math.round(l.quantity * l.unitPrice * 100) / 100)}`)
  const due = Math.round((bill.total - bill.paid) * 100) / 100
  return [
    t('billReceipt.header', { shop: shopName, date: formatDateTime(new Date().toISOString()) }),
    ...items,
    t('billReceipt.total', { total: formatRupees(bill.total) }),
    bill.paid > 0 ? t('billReceipt.paid', { paid: formatRupees(bill.paid) }) : '',
    bill.customer && due > 0 ? t('billReceipt.onKhata', { due: formatRupees(due) }) : '',
    t('billReceipt.check'),
  ].filter(Boolean).join('\n')
}

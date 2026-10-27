import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field, Sheet } from '../components/ui'
import { saveBill, useCustomers, useProducts, type CartLine } from '../lib/data'
import { formatRupees } from '../lib/format'
import { round2 } from '../lib/local'
import type { PaymentMode, Unit } from '../lib/types'

/** Item billing: tap tiles, adjust quantities, choose who pays and how much now. The rest goes on khata. */
export function BillScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const { products } = useProducts(shopId)
  const { customers } = useCustomers(shopId)
  const [cart, setCart] = useState<CartLine[]>([])
  const [customerId, setCustomerId] = useState('')
  const [paid, setPaid] = useState<string | null>(null)
  const [mode, setMode] = useState<PaymentMode>('cash')
  const [loose, setLoose] = useState(false)
  const [savedTotal, setSavedTotal] = useState<number | null>(null)

  const total = useMemo(() => round2(cart.reduce((sum, l) => sum + round2(l.quantity * l.unitPrice), 0)), [cart])
  const customer = customers.find((c) => c.id === customerId) ?? null
  const paidAmount = paid === null || !customer ? total : Math.min(Number(paid) || 0, total)

  function add(line: CartLine) {
    setSavedTotal(null)
    setCart((current) => {
      const index = line.productId ? current.findIndex((l) => l.productId === line.productId) : -1
      if (index < 0) return [...current, line]
      return current.map((l, i) => (i === index ? { ...l, quantity: round2(l.quantity + 1) } : l))
    })
  }

  function setQuantity(index: number, quantity: number) {
    setCart((current) => (quantity <= 0 ? current.filter((_, i) => i !== index) : current.map((l, i) => (i === index ? { ...l, quantity } : l))))
  }

  async function save() {
    const { total: saved } = await saveBill(shopId, cart, customer, paidAmount, mode)
    setCart([])
    setPaid(null)
    setCustomerId('')
    setSavedTotal(saved)
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="grid grid-cols-3 gap-2">
        {products.map((p) => (
          <button
            key={p.id}
            onClick={() => add({ productId: p.id, name: p.name, quantity: 1, unit: p.unit, unitPrice: p.sellingPrice })}
            className="min-h-20 rounded-xl bg-white p-2 text-left shadow-sm active:bg-teal-50"
          >
            <div className="line-clamp-2 font-semibold">{p.name}</div>
            <div className="text-sm text-slate-500">
              {formatRupees(p.sellingPrice)}/{t(`units.${p.unit}`)}
            </div>
          </button>
        ))}
        <button onClick={() => setLoose(true)} className="min-h-20 rounded-xl border-2 border-dashed border-slate-300 p-2 font-semibold text-slate-600">
          + {t('addLooseItem')}
        </button>
      </div>

      <div className="rounded-xl bg-white p-3 shadow-sm">
        {cart.length === 0 && <p className="py-4 text-center text-slate-500">{savedTotal !== null ? `${t('saved')} ${formatRupees(savedTotal)}` : t('emptyCart')}</p>}
        {cart.map((l, i) => (
          <div key={`${l.productId ?? l.name}-${i}`} className="flex items-center gap-2 border-b border-slate-100 py-2 last:border-0">
            <div className="flex-1">
              <div className="font-semibold">{l.name}</div>
              <div className="text-xs text-slate-500">{formatRupees(l.unitPrice)}/{t(`units.${l.unit}`)}</div>
            </div>
            <button className="h-10 w-10 rounded-lg bg-slate-100 text-xl" onClick={() => setQuantity(i, round2(l.quantity - 1))}>−</button>
            <input
              value={l.quantity}
              onChange={(e) => setQuantity(i, Number(e.target.value) || 0)}
              inputMode="decimal"
              className="h-10 w-16 rounded-lg border border-slate-300 text-center"
            />
            <button className="h-10 w-10 rounded-lg bg-slate-100 text-xl" onClick={() => setQuantity(i, round2(l.quantity + 1))}>+</button>
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
        <div className="space-y-3 rounded-xl bg-white p-3 shadow-sm">
          <label className="block">
            <span className="mb-1 block text-sm font-medium text-slate-600">{t('customer')}</span>
            <select value={customerId} onChange={(e) => setCustomerId(e.target.value)} className="h-12 w-full rounded-xl border border-slate-300 bg-white px-3 text-lg">
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
            <p className="font-semibold text-rose-600">
              {t('onKhata')}: {formatRupees(round2(total - paidAmount))}
            </p>
          )}
          <div className="flex gap-2 text-sm">
            {(['cash', 'upi'] as const).map((m) => (
              <button key={m} onClick={() => setMode(m)} className={`rounded-full px-3 py-1 font-medium ${mode === m ? 'bg-teal-700 text-white' : 'bg-slate-100 text-slate-600'}`}>
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
      <span className="mb-1 block text-sm font-medium text-slate-600">{t('unit')}</span>
      <select value={value} onChange={(e) => onChange(e.target.value as Unit)} className="h-12 w-full rounded-xl border border-slate-300 bg-white px-2 text-lg">
        {UNITS.map((u) => (
          <option key={u} value={u}>{t(`units.${u}`)}</option>
        ))}
      </select>
    </label>
  )
}

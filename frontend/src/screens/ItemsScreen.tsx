import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field, Sheet } from '../components/ui'
import { addProduct, useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'
import type { Unit } from '../lib/types'
import { UnitSelect } from './BillScreen'

export function ItemsScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const { products, isLoading } = useProducts(shopId)
  const [adding, setAdding] = useState(false)

  return (
    <div className="p-4">
      {!isLoading && products.length === 0 && <p className="py-8 text-center text-slate-500">{t('noItems')}</p>}
      <ul className="divide-y divide-slate-200 rounded-xl bg-white shadow-sm">
        {products.map((p) => (
          <li key={p.id} className="flex justify-between p-4">
            <span className="font-semibold">{p.name}</span>
            <span className="text-slate-600">
              {formatRupees(p.sellingPrice)}/{t(`units.${p.unit}`)}
            </span>
          </li>
        ))}
      </ul>
      <Button className="fixed right-4 bottom-24 shadow-lg" onClick={() => setAdding(true)}>
        + {t('addItem')}
      </Button>
      {adding && <AddProductSheet shopId={shopId} onClose={() => setAdding(false)} />}
    </div>
  )
}

function AddProductSheet({ shopId, onClose }: { shopId: string; onClose: () => void }) {
  const { t } = useTranslation()
  const [name, setName] = useState('')
  const [unit, setUnit] = useState<Unit>('piece')
  const [price, setPrice] = useState('')

  async function submit(e: FormEvent) {
    e.preventDefault()
    await addProduct(shopId, name.trim(), unit, Number(price))
    setName('')
    setPrice('')
  }

  return (
    <Sheet title={t('addItem')} onClose={onClose}>
      {/* Stays open after saving, so a shop can add many items in a row. */}
      <form onSubmit={submit} className="space-y-3">
        <Field label={t('itemName')} value={name} onChange={(e) => setName(e.target.value)} required autoFocus />
        <div className="grid grid-cols-2 gap-2">
          <UnitSelect value={unit} onChange={setUnit} />
          <Field label={t('price')} value={price} onChange={(e) => setPrice(e.target.value)} inputMode="decimal" required />
        </div>
        <Button type="submit" className="w-full" disabled={!name.trim() || !(Number(price) > 0)}>
          {t('save')}
        </Button>
      </form>
    </Sheet>
  )
}

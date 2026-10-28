import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { PendingDot } from '../../components/ui'
import { round2 } from '../../lib/local'
import { formatRupees } from '../../lib/format'
import { addItem, removeItem, saveItem, searchCatalogue, useCatalogue, useLists, type CatalogueItem, type ListItem, type ListKind } from '../../lib/planner'
import type { Unit } from '../../lib/types'
import { navigate } from '../../router'
import { UnitSelect } from '../BillScreen'

/**
 * Mockup 2: one list. Pick items from the common catalogue (search or browse by category) or type any item;
 * tick, change quantity, remove. Every change is saved on the phone at once.
 */
export function ListScreen({ userId, kind }: { userId: string; kind: ListKind }) {
  const { t } = useTranslation()
  const { lists } = useLists(userId)
  const list = lists.find((l) => l.kind === kind)!
  const [name, setName] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [unit, setUnit] = useState<Unit>('kg')
  const catalogue = useCatalogue(userId).data ?? []
  const [category, setCategory] = useState<string | null>(null)
  const categories = [...new Set(catalogue.map((i) => i.category))]
  const onList = new Set(list.items.map((i) => i.catalogueItemId).filter(Boolean))
  const suggestions = searchCatalogue(catalogue, name)

  /** One tap adds a catalogue item with a sensible first quantity (adjust with − and +). */
  async function pick(item: CatalogueItem) {
    const typed = Number(quantity)
    const qty = name.trim() && typed > 0 ? typed : item.unit === 'g' || item.unit === 'ml' ? 100 : 1
    await addItem(userId, kind, item.name, qty, item.unit, item.id)
    setName('')
    setQuantity('1')
  }

  async function add(e: FormEvent) {
    e.preventDefault()
    const qty = Number(quantity)
    if (!name.trim() || !(qty > 0)) return
    await addItem(userId, kind, name.trim(), qty, unit)
    setName('')
    setQuantity('1')
  }

  const update = (item: ListItem, change: Partial<ListItem>) => void saveItem(userId, kind, { ...item, ...change })
  const step = (item: ListItem, direction: 1 | -1) => {
    const size = item.unit === 'g' || item.unit === 'ml' ? 100 : item.unit === 'kg' || item.unit === 'litre' ? 0.5 : 1
    const next = round2(item.quantity + direction * size)
    if (next > 0) update(item, { quantity: next })
  }

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <div role="tablist" className="grid grid-cols-2 rounded-[14px] bg-white p-1">
        {(['monthly', 'weekly'] as const).map((k) => (
          <button
            key={k}
            role="tab"
            aria-selected={k === kind}
            onClick={() => navigate(`/list/${k}`)}
            className={`h-11 rounded-[10px] font-semibold ${k === kind ? 'bg-teal-700 text-white' : 'text-muted'}`}
          >
            {t(`plan.${k}`)}
          </button>
        ))}
      </div>

      <form onSubmit={add} className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
        <input
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder={t('plan.addPlaceholder')}
          aria-label={t('plan.addPlaceholder')}
          className="h-12 rounded-[14px] border border-line px-3 text-[17px] outline-none focus:border-teal-600"
        />
        {suggestions.length > 0 && (
          <ul className="flex flex-col divide-y divide-[#edf2f1] rounded-[14px] border border-line">
            {suggestions.map((item) => (
              <li key={item.id}>
                <button type="button" onClick={() => void pick(item)} className="flex min-h-12 w-full items-center gap-2 px-3 text-left">
                  <span className="flex-1 font-semibold">{item.name} <span className="font-normal text-muted">· {item.nameEn}</span></span>
                  <span className="text-sm text-muted">~{formatRupees(item.typicalPrice)}/{t(`units.${item.unit}`)}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
        <div className="grid grid-cols-[1fr_1fr_auto] items-end gap-2">
          <label className="block">
            <span className="mb-1 block text-sm font-semibold text-muted">{t('quantity')}</span>
            <input value={quantity} onChange={(e) => setQuantity(e.target.value)} inputMode="decimal" className="h-12 w-full rounded-[14px] border border-line px-3 text-lg" />
          </label>
          <UnitSelect value={unit} onChange={setUnit} />
          <button type="submit" disabled={!name.trim()} className="h-12 rounded-2xl bg-teal-700 px-5 text-lg font-semibold text-white disabled:opacity-40">
            {t('plan.add')}
          </button>
        </div>
      </form>

      {catalogue.length > 0 && !name.trim() && (
        <section className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
          <h2 className="text-base font-bold">{t('plan.pickItems')}</h2>
          <div className="-mx-1 flex gap-2 overflow-x-auto px-1 pb-1">
            {categories.map((c) => (
              <button
                key={c}
                onClick={() => setCategory(category === c ? null : c)}
                aria-pressed={category === c}
                className={`h-10 shrink-0 rounded-full px-3 text-sm font-semibold ${category === c ? 'bg-teal-700 text-white' : 'bg-[#edf2f1] text-ink'}`}
              >
                {c}
              </button>
            ))}
          </div>
          {category && (
            <div className="grid grid-cols-2 gap-2">
              {catalogue.filter((i) => i.category === category).map((item) => {
                const added = onList.has(item.id)
                return (
                  <button
                    key={item.id}
                    disabled={added}
                    onClick={() => void pick(item)}
                    className={`flex min-h-14 flex-col items-start justify-center rounded-[14px] px-3 py-2 text-left ${added ? 'bg-teal-50 text-teal-800' : 'border border-line'}`}
                  >
                    <span className="font-semibold">{added ? '✓ ' : '+ '}{item.name}</span>
                    <span className="text-xs text-muted">{item.nameEn} · ~{formatRupees(item.typicalPrice)}/{t(`units.${item.unit}`)}</span>
                  </button>
                )
              })}
            </div>
          )}
        </section>
      )}

      {list.items.length === 0 ? (
        <p className="py-6 text-center text-muted">{t('plan.emptyList')}</p>
      ) : (
        <ul className="divide-y divide-[#edf2f1] rounded-[18px] bg-white px-3">
          {list.items.map((item) => (
            <li key={item.id} className="flex items-center gap-2 py-2.5">
              <button
                onClick={() => update(item, { checked: !item.checked })}
                aria-label={item.name}
                aria-pressed={item.checked}
                className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-lg text-lg font-extrabold ${
                  item.checked ? 'bg-teal-700 text-white' : 'border-2 border-line bg-white'
                }`}
              >
                {item.checked ? '✓' : ''}
              </button>
              <span className="flex min-w-0 flex-1 items-center gap-1.5 text-base font-semibold">
                <span className="truncate">{item.name}</span>
                {item.pending && <PendingDot />}
              </span>
              <button aria-label={t('a11y.less')} onClick={() => step(item, -1)} className="h-11 w-11 rounded-[10px] bg-[#edf2f1] text-lg">−</button>
              <span className="min-w-14 text-center text-[15px] font-bold">
                {item.quantity} {t(`units.${item.unit}`)}
              </span>
              <button aria-label={t('a11y.more')} onClick={() => step(item, 1)} className="h-11 w-11 rounded-[10px] bg-[#edf2f1] text-lg">+</button>
              <button aria-label={t('remove')} onClick={() => void removeItem(userId, kind, item)} className="h-11 w-11 text-xl text-muted">×</button>
            </li>
          ))}
        </ul>
      )}

      {list.items.length > 0 && (
        <div className="fixed inset-x-0 bottom-[68px] z-10 mx-auto max-w-xl border-t border-line bg-white px-4 py-3">
          <button onClick={() => navigate(`/send/${kind}`)} className="h-14 w-full rounded-2xl bg-teal-700 text-lg font-bold text-white">
            {t('plan.sendToShop', { count: list.items.length })}
          </button>
        </div>
      )}
    </div>
  )
}

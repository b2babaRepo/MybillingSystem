import { useId } from 'react'
import { useTranslation } from 'react-i18next'
import { sameCategory, useCategories, type CategoryRow } from '../lib/categories'

/**
 * The one category box: type to pick one of the shop's categories, or type a new name and it is made when the item
 * saves. `onPick` fires when the text matches a category, so a form can fill that category's defaults.
 */
export function CategoryInput({ shopId, value, onChange, onPick }: { shopId: string; value: string; onChange: (value: string) => void; onPick?: (category: CategoryRow) => void }) {
  const { t } = useTranslation()
  const listId = useId()
  const rows = (useCategories(shopId).data ?? []).filter((c) => c.isActive)
  const match = rows.find((c) => sameCategory(c.name, value))
  return (
    <label className="block">
      <span className="mb-1 block text-sm font-semibold text-muted">{t('stock.category')}</span>
      <input
        value={value}
        list={listId}
        maxLength={60}
        onChange={(e) => {
          onChange(e.target.value)
          const hit = rows.find((c) => sameCategory(c.name, e.target.value))
          if (hit) onPick?.(hit)
        }}
        className="h-12 w-full rounded-2xl border-[1.5px] border-line bg-white px-3 text-lg outline-none transition focus:border-teal-600 focus:ring-4 focus:ring-teal-600/10"
      />
      <datalist id={listId}>{rows.map((c) => <option key={c.id} value={c.name} />)}</datalist>
      {value.trim() !== '' && !match && <span className="mt-1 block text-xs text-muted">{t('cat.willCreate', { name: value.trim() })}</span>}
    </label>
  )
}

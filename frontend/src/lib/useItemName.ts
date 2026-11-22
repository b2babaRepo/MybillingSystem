import { useCallback, useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { useProducts } from './data'
import { itemName } from './itemName'

/**
 * For screens whose rows carry only an item's id and saved name (reports, stock statement): the row's name in the
 * language being shown, from the shop's items, found by id or else by name. A row whose item is gone, or was renamed
 * by the shop, keeps the name it has. Display only: exports and messages to the CA keep the saved name.
 */
export function useItemName(shopId: string | null | undefined) {
  const { i18n } = useTranslation()
  const { products } = useProducts(shopId ?? '')
  const lookup = useMemo(() => {
    const byId = new Map(products.map((p) => [p.id, p]))
    const byName = new Map(products.map((p) => [p.name.trim().toLowerCase(), p]))
    return { byId, byName }
  }, [products])
  return useCallback((row: { productId?: string | null; name: string }) => {
    const p = (row.productId ? lookup.byId.get(row.productId) : undefined) ?? lookup.byName.get(row.name.trim().toLowerCase())
    return p ? itemName({ name: row.name, nameEn: p.nameEn, nameHi: p.nameHi }, i18n.language) : row.name
  }, [lookup, i18n.language])
}

import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { confirmAsk } from './ask'
import { navigate } from '../router'
import { addToCart } from './customer'
import { sendList, type Order } from './planner'

/**
 * "Order again" for any past order. A shop-page order goes back into that shop's cart to check out again (nothing is
 * sent yet, so no question); a list order is sent again as it was asked (not as the shop changed it), same shop,
 * delivery and payment, after one question.
 */
export function useReorder(userId: string, userName: string) {
  const { t } = useTranslation()
  const [busy, setBusy] = useState(false)
  async function reorder(order: Order) {
    const lines = order.lines.filter((l) => !l.removed)
    if (lines.length === 0 || busy) return
    if (order.source === 'store' && order.shopSlug) {
      const cart: Record<string, number> = {}
      for (const l of lines) { const id = l.requestedProductId ?? l.productId; if (id) cart[id] = (cart[id] ?? 0) + l.quantity }
      setBusy(true)
      try { await addToCart(order.shopId, cart) } finally { setBusy(false) }
      navigate(`/s/${order.shopSlug}/cart`)
      return
    }
    const ok = await confirmAsk(t('again.confirm', {
      list: order.source === 'store' ? t('orders2.online') : t(order.listKind === 'monthly' ? 'plan.monthlyList' : 'plan.weeklyList'), count: lines.length, shop: order.shopName,
      how: order.fulfilment === 'delivery' ? t('plan.delivery') : t('plan.pickup'),
    }))
    if (!ok) return
    setBusy(true)
    try {
      const items = lines.map((l) => ({ id: crypto.randomUUID(), name: l.requestedName ?? l.name, quantity: l.quantity, unit: l.unit, checked: false, note: l.note }))
      const choices = Object.fromEntries(items.map((item, i) => [item.id, { productId: lines[i].requestedProductId ?? lines[i].productId ?? null, preference: lines[i].preference ?? 'shop_chooses' }]))
      await sendList(userId, userName, items, {
        shop: { id: order.shopId, name: order.shopName, area: null, address: null }, kind: order.listKind, fulfilment: order.fulfilment,
        slot: '', payment: order.payment, note: '', address: order.address ?? null,
      }, choices)
    } finally {
      setBusy(false)
    }
    navigate('/orders')
  }
  /** Shop-page orders refill the cart; list orders are sent again. */
  const label = (order: Order) => (order.source === 'store' && order.shopSlug ? t('cust.fillCart') : t('again.title'))
  return { reorder, busy, label }
}

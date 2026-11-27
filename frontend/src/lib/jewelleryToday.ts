import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from './api'
import { useCustomers } from './data'
import { formatRupees, indiaToday } from './format'
import type { Approval, Girvi, Member, Order } from '../screens/JewelleryBookPanels'
import type { DueItem } from '../screens/JewelleryTodayPanel'

const formatDate = (iso: string) => new Date(`${iso}T00:00:00`).toLocaleDateString([], { day: 'numeric', month: 'short' })

const plusDays = (iso: string, days: number) => new Date(new Date(`${iso}T00:00:00Z`).getTime() + days * 86_400_000).toISOString().slice(0, 10)

/** Everything on the jewellery counter that needs the shop today, read from the lists the shop already keeps. */
export function useDueToday(shopId: string, shopName: string) {
  const { t } = useTranslation()
  const girvi = useQuery({ queryKey: [shopId, 'jewellery', 'girvi', 'open'], queryFn: () => api<Girvi[]>('/v1/jewellery/girvi?status=open') }).data ?? []
  const members = useQuery({ queryKey: [shopId, 'jewellery', 'members'], queryFn: () => api<Member[]>('/v1/jewellery/schemes/members') }).data ?? []
  const orders = useQuery({ queryKey: [shopId, 'jewellery', 'orders', 'open'], queryFn: () => api<Order[]>('/v1/jewellery/orders?status=open') }).data ?? []
  const approvals = useQuery({ queryKey: [shopId, 'jewellery', 'approvals'], queryFn: () => api<Approval[]>('/v1/jewellery/approvals?status=all') }).data ?? []
  const customers = useCustomers(shopId).data ?? []
  const today = indiaToday()
  const soon = plusDays(today, 7)
  const items: DueItem[] = []

  for (const g of girvi) {
    if (g.closedOn || g.dueOn > soon) continue
    const vars = { name: g.customer, number: g.number, shop: shopName, date: formatDate(g.dueOn), amount: formatRupees(g.toClose) }
    items.push({ key: `g${g.id}`, kind: 'girvi', title: t('jwl5.girviDue', vars), who: g.customer, phone: g.phone, message: t('jwl5.girviMsg', vars), overdue: g.overdue })
  }
  for (const m of members) {
    if (m.complete || m.closedOn || m.nextDue > today) continue
    const vars = { name: m.customer, plan: m.plan, n: m.paid + 1, months: m.months, shop: shopName, date: formatDate(m.nextDue), amount: formatRupees(m.instalment) }
    items.push({ key: `s${m.id}`, kind: 'scheme', title: t('jwl5.schemeDue', vars), who: m.customer, phone: m.phone, message: t('jwl5.schemeMsg', vars), overdue: m.missed > 0 })
  }
  for (const o of orders) {
    if (o.status !== 'ready' && !o.late) continue
    const vars = { name: o.customer, number: o.number, what: o.description, shop: shopName }
    items.push({ key: `o${o.id}`, kind: 'order', title: t(o.status === 'ready' ? 'jwl5.orderReady' : 'jwl5.orderLate', vars), who: o.customer, phone: o.phone, message: t('jwl5.orderMsg', vars), overdue: o.late })
  }
  for (const a of approvals) {
    if (a.closedOn || !a.late) continue
    const phone = customers.find((c) => c.id === a.customerId)?.phone ?? null
    const vars = { name: a.party, number: a.number, shop: shopName, date: formatDate(a.dueBack) }
    items.push({ key: `a${a.id}`, kind: 'approval', title: t('jwl5.approvalLate', vars), who: a.party, phone, message: t('jwl5.approvalMsg', vars), overdue: true })
  }
  return items.sort((x, y) => Number(y.overdue) - Number(x.overdue))
}

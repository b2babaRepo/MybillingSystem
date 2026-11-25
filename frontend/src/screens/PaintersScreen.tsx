import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { uuidv7 } from '../lib/ids'
import { ask } from '../components/Ask'
import { formatPhone, formatRupees, whatsAppLink } from '../lib/format'

interface Payout { id: string; amount: number; paidOn: string; mode: string; note: string | null }
interface Commission { painterId: string; name: string; phone: string | null; commissionPercent: number; bills: number; sales: number; commission: number; earnedAllTime: number; paidAllTime: number; due: number; payouts: Payout[] | null }

/** India-time month as yyyy-mm. */
const thisMonth = () => new Date(Date.now() + 5.5 * 3600_000).toISOString().slice(0, 7)

function shiftMonth(month: string, by: number) {
  const date = new Date(`${month}-01T00:00:00Z`)
  date.setUTCMonth(date.getUTCMonth() + by)
  return date.toISOString().slice(0, 7)
}

/**
 * Painters (paint shops): each with a commission %, tagged on the bills of customers they bring. The month's list
 * shows each painter's bills, sales and the commission due, with a WhatsApp message to tell them.
 */
export function PaintersScreen({ shopId, shopName }: { shopId: string; shopName: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [month, setMonth] = useState(thisMonth())
  const list = useQuery({ queryKey: [shopId, 'painters', 'commission', month], queryFn: () => api<Commission[]>(`/v1/painters/commission?month=${month}`) })
  const [name, setName] = useState('')
  const [phone, setPhone] = useState('')
  const [percent, setPercent] = useState('5')
  const [message, setMessage] = useState<string | null>(null)
  const label = new Date(`${month}-01T00:00:00Z`).toLocaleDateString(shownLocale(), { month: 'long', year: 'numeric', timeZone: 'UTC' })

  async function pay(p: Commission) {
    const raw = (await ask(t('hwp1.painters.payAsk', { name: p.name, due: formatRupees(p.due) }), String(p.due)))?.trim()
    const amount = Number(raw)
    if (!amount) return
    setMessage(null)
    try {
      await api(`/v1/painters/${p.painterId}/payouts`, 'POST', { id: uuidv7(), amount, mode: 'cash' })
      await queryClient.invalidateQueries({ queryKey: [shopId, 'painters'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    }
  }

  async function add() {
    setMessage(null)
    try {
      await api('/v1/painters', 'POST', { id: uuidv7(), name: name.trim(), phone: phone.trim() || null, commissionPercent: Number(percent) || 0 })
      setName('')
      setPhone('')
      await queryClient.invalidateQueries({ queryKey: [shopId, 'painters'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    }
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex items-center justify-between rounded-card bg-white p-2">
        <button onClick={() => setMonth(shiftMonth(month, -1))} aria-label={t('book.previous')} className="h-11 w-11 rounded-xl text-xl">‹</button>
        <span className="text-lg font-bold">{label}</span>
        <button onClick={() => setMonth(shiftMonth(month, 1))} disabled={month >= thisMonth()} aria-label={t('book.next')} className="h-11 w-11 rounded-xl text-xl disabled:opacity-30">›</button>
      </div>

      <ul className="divide-y divide-[#edf2f1] rounded-card bg-white">
        {list.data?.length === 0 && <li className="p-4 text-muted">{t('painters.empty')}</li>}
        {list.data?.map((p) => (
          <li key={p.painterId} className="flex flex-col gap-1 p-4">
            <div className="flex justify-between">
              <span className="text-[17px] font-bold">{p.name} <span className="text-sm font-normal text-muted">{p.commissionPercent}%</span></span>
              <span className="text-lg font-extrabold text-jama">{formatRupees(p.commission)}</span>
            </div>
            <span className="text-sm text-muted">{t('painters.line', { count: p.bills, sales: formatRupees(p.sales) })}</span>
            <span className="text-sm">{t('hwp1.painters.ledger', { earned: formatRupees(p.earnedAllTime), paid: formatRupees(p.paidAllTime) })}{' '}
              <b className={p.due > 0 ? 'text-udhaar' : 'text-jama'}>{t('hwp1.painters.due', { due: formatRupees(p.due) })}</b></span>
            {p.payouts && p.payouts.length > 0 && (
              <span className="text-xs text-muted">{p.payouts.map((x) => `${x.paidOn.slice(8, 10)}/${x.paidOn.slice(5, 7)} ${formatRupees(x.amount)}`).join(' · ')}</span>
            )}
            {p.due > 0 && <button onClick={() => void pay(p)} className="min-h-11 self-start rounded-xl border border-line px-4 text-sm font-semibold">{t('hwp1.painters.pay')}</button>}
            {p.phone && p.commission > 0 && (
              <a href={whatsAppLink(p.phone, t('painters.message', { name: p.name, month: label, sales: formatRupees(p.sales), commission: formatRupees(p.commission), shop: shopName }))}
                target="_blank" rel="noreferrer" className="flex min-h-11 items-center self-start text-sm font-semibold text-brand underline">
                {t('painters.tell', { phone: formatPhone(p.phone) })}
              </a>
            )}
          </li>
        ))}
      </ul>

      <section className="flex flex-col gap-3 rounded-card bg-white p-4">
        <h2 className="text-lg font-bold">{t('painters.addTitle')}</h2>
        <Field label={t('staff.name')} value={name} onChange={(e) => setName(e.target.value)} />
        <div className="grid grid-cols-2 gap-2">
          <Field label={t('staff.phone')} value={phone} onChange={(e) => setPhone(e.target.value)} inputMode="tel" />
          <Field label={t('painters.percent')} value={percent} onChange={(e) => setPercent(e.target.value.replace(/[^\d.]/g, ''))} inputMode="decimal" />
        </div>
        {message && <p className="text-sm text-due">{message}</p>}
        <Button disabled={name.trim().length < 2} onClick={() => void add()}>{t('painters.add')}</Button>
      </section>
    </div>
  )
}

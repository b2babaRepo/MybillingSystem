import { useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { shownLocale } from '../i18n'
import { Card, Input } from '../components/ui'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'

interface FoundBill { id: string; billNo: string; occurredAt: string; total: number; customerName: string | null; customerPhone: string | null; lines: number; items: string; cancelled: boolean }

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

/**
 * A customer comes back days later to return or exchange: find their bill by number, name, mobile or item, and open
 * it. The bill's own page has Cancel, Return items and Exchange.
 */
export function FindBillScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const [q, setQ] = useState('')
  const [typed, setTyped] = useState('')
  const [from, setFrom] = useState(() => iso(new Date(Date.now() - 90 * 86_400_000)))
  const [to, setTo] = useState(() => iso(new Date()))
  // Search a moment after typing stops.
  useEffect(() => { const h = setTimeout(() => setQ(typed.trim()), 300); return () => clearTimeout(h) }, [typed])
  const params = new URLSearchParams({ ...(q ? { q } : {}), ...(from ? { from } : {}), ...(to ? { to } : {}) })
  const found = useQuery({ queryKey: [shopId, 'bills', 'find', q, from, to], queryFn: () => api<FoundBill[]>(`/v1/bills/find?${params}`) })
  const bills = found.data ?? []
  const when = (s: string) => new Date(s).toLocaleString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric', hour: 'numeric', minute: '2-digit' })

  return (
    <div className="flex flex-col gap-3 p-4">
      <Card>
        <h1 className="text-lg font-extrabold">↩ {t('findBill.title')}</h1>
        <p className="mt-1 text-sm text-muted">{t('findBill.hint')}</p>
        <button onClick={() => navigate('/exchange-slips')} className="mt-1 min-h-10 text-sm font-semibold text-brand underline">🎟 {t('slip.register')} ›</button>
        <Input autoFocus value={typed} onChange={(e) => setTyped(e.target.value)} placeholder={t('findBill.search')} aria-label={t('findBill.search')} className="mt-3 w-full text-base" />
        <div className="mt-2 grid grid-cols-2 gap-2">
          <label className="block text-sm"><span className="mb-1 block font-semibold text-muted">{t('findBill.from')}</span>
            <Input type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)} className="w-full" /></label>
          <label className="block text-sm"><span className="mb-1 block font-semibold text-muted">{t('findBill.to')}</span>
            <Input type="date" value={to} min={from} onChange={(e) => setTo(e.target.value)} className="w-full" /></label>
        </div>
      </Card>
      {found.isError && <p className="text-sm text-due">{found.error.message}</p>}
      {found.isSuccess && (
        <p className="px-1 text-sm font-semibold text-muted">{bills.length === 0 ? t('findBill.none') : t('findBill.count', { count: bills.length })}</p>
      )}
      {bills.length > 0 && (
        <div className="divide-y divide-line rounded-card bg-white shadow-sm">
          {bills.map((b) => (
            <button key={b.id} onClick={() => navigate(`/print/bill/${b.id}`)} className="flex w-full items-center gap-3 px-4 py-3 text-left">
              <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                <span className="flex flex-wrap items-center gap-2">
                  <b>{b.billNo}</b>
                  {b.cancelled && <span className="rounded-full bg-due-soft px-2 text-xs font-bold text-due">{t('findBill.cancelled')}</span>}
                </span>
                <span className="truncate text-sm">{b.customerName ?? t('findBill.walkIn')}{b.customerPhone ? ` · ${b.customerPhone}` : ''}</span>
                <span className="truncate text-xs text-muted">{when(b.occurredAt)} · {b.items}</span>
              </span>
              <span className="flex flex-col items-end">
                <b className={b.cancelled ? 'text-muted line-through' : ''}>{formatRupees(b.total)}</b>
                <span className="text-xs font-semibold text-brand">{t('findBill.open')} ›</span>
              </span>
            </button>
          ))}
        </div>
      )}
      {bills.length === 50 && <p className="px-1 text-xs text-muted">{t('findBill.more')}</p>}
    </div>
  )
}

import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { shopTint } from '../../lib/shopTint'
import { MonthSelect, PrintShare } from '../../components/StatementTools'
import { monthLabel } from '../../lib/months'
import { shownLocale } from '../../i18n'
import { api } from '../../lib/api'
import { formatRupees } from '../../lib/format'
import { navigate } from '../../router'

interface PassbookItem { shopId: string; shopName: string; shopSlug: string | null; at: string; kind: 'bill' | 'payment'; amount: number; paid: number; onKhata: number; billId: string | null; billNo: string | null; billToken: string | null; mode: string | null; upiRef: string | null }
interface Passbook { month: string | null; items: PassbookItem[]; shops: { shopId: string; shopName: string; shopSlug: string | null; balance: number | null }[]; billed: number; paid: number }

type Show = 'all' | 'bill' | 'payment'

/**
 * The customer's money with every shop in one place: bills (counter bills on a confirmed khata, and app order
 * bills) and khata payments, newest first, for a month or the last 12 months. "Bills" alone is My bills.
 */
export function PassbookScreen() {
  const { t } = useTranslation()
  const [month, setMonth] = useState('')
  const [show, setShow] = useState<Show>('all')
  const book = useQuery({ queryKey: ['me', 'passbook', month], queryFn: () => api<Passbook>(`/v1/me/passbook${month ? `?month=${month}` : ''}`) }).data
  const items = (book?.items ?? []).filter((i) => show === 'all' || i.kind === show)
  const owed = (book?.shops ?? []).reduce((s, x) => s + Math.max(0, x.balance ?? 0), 0)
  const day = (iso: string) => new Date(iso).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric' })
  const period = month ? monthLabel(month) : t('cust7.last12')
  const chip = (value: Show, label: string) => (
    <button key={value} onClick={() => setShow(value)} aria-pressed={show === value} className={`h-10 shrink-0 rounded-full px-4 text-sm font-bold ${show === value ? 'bg-brand text-white' : 'border border-line bg-white'}`}>{label}</button>
  )
  const shareText = book ? `${t('cust7.passbook')} · ${period}\n${t('cust7.billed')}: ${formatRupees(book.billed)}\n${t('cust7.paidTotal')}: ${formatRupees(book.paid)}\n${t('myKhata.youOwe')}: ${formatRupees(owed)}` : ''

  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex items-center gap-2">
        <MonthSelect value={month} onChange={setMonth} allLabel={t('cust7.last12')} />
        <PrintShare text={shareText} />
      </div>
      {book && (
        <div className="grid grid-cols-3 gap-2">
          {[[t('cust7.billed'), book.billed], [t('cust7.paidTotal'), book.paid], [t('myKhata.youOwe'), owed]].map(([label, value]) => (
            <span key={label as string} className="flex flex-col rounded-2xl bg-white p-3 shadow-[0_1px_4px_rgba(16,32,29,0.06)]">
              <span className="text-xs font-semibold text-muted">{label}</span>
              <b className="text-lg tabular-nums">{formatRupees(value as number)}</b>
            </span>
          ))}
        </div>
      )}
      {book && book.shops.some((s) => s.balance !== null) && (
        <div className="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1">
          {book.shops.filter((s) => s.balance !== null).map((s) => (
            <span key={s.shopId} className="flex shrink-0 items-center gap-2 rounded-full bg-white py-1 pl-1 pr-3 text-sm shadow-[0_1px_4px_rgba(16,32,29,0.06)]">
              <span className="flex h-7 w-7 items-center justify-center rounded-full text-xs font-extrabold" style={shopTint(s.shopId)}>{s.shopName.charAt(0)}</span>
              <span className="font-semibold">{s.shopName}</span>
              <b className={(s.balance ?? 0) > 0 ? 'text-udhaar' : 'text-muted'}>{(s.balance ?? 0) > 0 ? formatRupees(s.balance!) : t('settled')}</b>
            </span>
          ))}
        </div>
      )}
      <div className="flex gap-2 print:hidden">{chip('all', t('store.all'))}{chip('bill', t('cust7.bills'))}{chip('payment', t('cust7.payments'))}</div>
      {book && items.length === 0 && <p className="py-8 text-center text-muted">{t('cust7.empty')}</p>}
      <ul className="flex flex-col divide-y divide-[#edf2f1] rounded-card bg-white">
        {items.map((i, n) => {
          const open = i.billId && i.billToken && i.shopSlug ? () => navigate(`/b/${i.shopSlug}/${i.billId}?t=${i.billToken}`) : undefined
          const body = (
            <>
              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl font-extrabold" style={shopTint(i.shopId)}>{i.shopName.charAt(0)}</span>
              <span className="flex min-w-0 flex-1 flex-col">
                <b className="truncate">{i.kind === 'bill' ? `${t('cust7.bill')} ${i.billNo ?? ''}` : `${t('cust7.payment')}${i.mode ? ` · ${i.mode.toUpperCase()}` : ''}`}</b>
                <span className="truncate text-xs text-muted">{i.shopName} · {day(i.at)}{i.upiRef ? ` · UTR ${i.upiRef}` : ''}</span>
                {i.kind === 'bill' && <span className={`text-xs font-semibold ${i.onKhata > 0 ? 'text-udhaar' : 'text-brand'}`}>{i.onKhata > 0 ? t('cust7.onKhata', { amount: formatRupees(i.onKhata) }) : t('cust7.paidFull')}</span>}
              </span>
              <b className={`shrink-0 tabular-nums ${i.kind === 'payment' ? 'text-brand' : ''}`}>{i.kind === 'payment' ? '+' : ''}{formatRupees(i.amount)}</b>
              {open && <span aria-hidden className="text-muted print:hidden">›</span>}
            </>
          )
          return (
            <li key={`${i.kind}-${i.billId ?? n}-${i.at}`}>
              {open
                ? <button onClick={open} className="flex min-h-16 w-full items-center gap-3 px-3.5 py-2.5 text-left">{body}</button>
                : <div className="flex min-h-16 items-center gap-3 px-3.5 py-2.5">{body}</div>}
            </li>
          )
        })}
      </ul>
      <p className="text-xs text-muted">{t('cust7.note')}</p>
    </div>
  )
}

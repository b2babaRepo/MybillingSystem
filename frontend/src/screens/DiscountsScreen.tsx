import { DataTable } from '../components/DataTable'
import { shownLocale } from '../i18n'
import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'

interface Cashier { userId: string; name: string; bills: number; sales: number; byHand: number; byHandLines: number; byOffer: number; byHandPercent: number }
interface Line { at: string; billNo: string | null; item: string; quantity: number; listPrice: number; soldAt: number; discount: number; by: string | null; offer: string | null }
interface Report { month: string; byHand: number; byOffer: number; cashiers: Cashier[]; lines: Line[] }

const thisMonth = () => { const d = new Date(); return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}` }

/**
 * Discounts: every line sold below its list price this month, by cashier. Price cuts by hand are shown apart from
 * planned offers, so one cashier giving away much more than the rest stands out.
 */
export function DiscountsScreen() {
  const { t } = useTranslation()
  const [month, setMonth] = useState(thisMonth())
  const [onlyHand, setOnlyHand] = useState(true)
  const r = useQuery({ queryKey: ['discounts', month], queryFn: () => api<Report>(`/v1/reports/discounts?month=${month}`) }).data
  const lines = (r?.lines ?? []).filter((l) => !onlyHand || !l.offer)
  return (
    <div className="flex flex-col gap-3 p-4">
      <header>
        <h1 className="text-xl font-bold">{t('disc.title')}</h1>
        <p className="text-sm text-muted">{t('disc.hint')}</p>
      </header>
      <input type="month" value={month} max={thisMonth()} onChange={(e) => setMonth(e.target.value)} aria-label={t('disc.month')} className="h-11 rounded-xl border border-line bg-white px-3" />
      {r && (
        <>
          <section className="grid grid-cols-2 gap-2">
            <div className="rounded-card bg-white p-3 shadow-sm"><p className="text-xs text-muted">{t('disc.byHand')}</p><p className="text-lg font-bold text-due">{formatRupees(r.byHand)}</p></div>
            <div className="rounded-card bg-white p-3 shadow-sm"><p className="text-xs text-muted">{t('disc.byOffer')}</p><p className="text-lg font-bold">{formatRupees(r.byOffer)}</p></div>
          </section>
          <section className="flex flex-col gap-2 text-sm">
            <h2 className="px-1 font-bold">{t('disc.cashiers')}</h2>
            <DataTable label={t('disc.cashiers')} rows={r.cashiers} rowKey={(c) => c.userId}
              columns={[
                { key: 'name', header: t('disc.cashier'), sortValue: (c) => c.name, render: (c) => c.name },
                { key: 'bills', header: t('disc.bills'), align: 'right', sortValue: (c) => c.bills, render: (c) => c.bills },
                { key: 'sales', header: t('disc.sales'), align: 'right', sortValue: (c) => c.sales, render: (c) => formatRupees(c.sales) },
                { key: 'hand', header: t('disc.byHand'), align: 'right', sortValue: (c) => c.byHand,
                  render: (c) => <span className={c.byHand > 0 ? 'font-semibold text-due' : ''}>{formatRupees(c.byHand)}{c.byHandLines > 0 && <span className="block text-xs font-normal text-muted">{t('disc.lines', { count: c.byHandLines })}</span>}</span> },
                { key: 'pct', header: '%', align: 'right', sortValue: (c) => c.byHandPercent, render: (c) => `${c.byHandPercent}%` },
                { key: 'offer', header: t('disc.byOffer'), align: 'right', sortValue: (c) => c.byOffer, render: (c) => formatRupees(c.byOffer) },
              ]} />
          </section>
          <section className="flex flex-col gap-1 rounded-card bg-white p-4 shadow-sm">
            <div className="flex items-center justify-between gap-2">
              <h2 className="font-bold">{t('disc.linesTitle')}</h2>
              <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={onlyHand} onChange={(e) => setOnlyHand(e.target.checked)} className="h-4 w-4" />{t('disc.onlyHand')}</label>
            </div>
            {lines.length === 0 && <p className="text-sm text-muted">{t('disc.none')}</p>}
            {lines.map((l, i) => (
              <div key={i} className="flex justify-between gap-2 border-t border-line py-1.5 text-sm">
                <span>
                  <span className="font-semibold">{l.item} × {l.quantity}</span>
                  <span className="block text-xs text-muted">{new Date(l.at).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })} · {l.billNo} · {l.by}{l.offer ? ` · ${t('disc.offer', { name: l.offer })}` : ''}</span>
                </span>
                <span className="text-right tabular-nums"><span className="font-semibold text-due">−{formatRupees(l.discount)}</span>
                  <span className="block text-xs text-muted">{formatRupees(l.listPrice)} → {formatRupees(l.soldAt)}</span></span>
              </div>
            ))}
          </section>
        </>
      )}
    </div>
  )
}

import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { monthToDate, saveCsv } from '../lib/csv'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'
import { DataTable } from '../components/DataTable'

type By = 'category' | 'supplier' | 'customer'
interface Group { key: string; name: string; sales: number; quantity: number; bills: number; profit: number | null; share: number }
interface SalesBy { total: number; groups: Group[] }

/** Sales for chosen dates, grouped by category, by the supplier the goods came from, or by customer, with share and profit. */
export function SalesByScreen() {
  const { t } = useTranslation()
  const [by, setBy] = useState<By>('category')
  const [range, setRange] = useState(monthToDate)
  const data = useQuery({ queryKey: ['sales-by', by, range.from, range.to], queryFn: () => api<SalesBy>(`/v1/reports/sales-by?by=${by}&from=${range.from}&to=${range.to}`) }).data
  const label = (g: Group) => g.name || (by === 'customer' ? t('sl.walkIn') : t('sl.unknown'))

  return (
    <div className="flex flex-col gap-3 p-4 lg:mx-auto lg:w-full lg:max-w-5xl">
      <section className="flex flex-col gap-3 rounded-card bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h1 className="text-xl font-bold">🏷️ {t('sl.salesBy')}</h1>
          <button disabled={!data} onClick={() => data && saveCsv(`sales-by-${by}-${range.from}-to-${range.to}.csv`, [[t(`sl.byWhat.${by}`), t('sl.sales'), t('sl.share'), t('sl.bills'), t('sl.profit')], ...data.groups.map((g) => [label(g), g.sales, g.share, g.bills, g.profit])])}
            className="h-10 rounded-xl border border-line px-3 text-sm font-semibold transition hover:bg-[#f6faf9] disabled:opacity-40">⬇ {t('sl.csv')}</button>
        </div>
        <div className="grid grid-cols-3 gap-1 rounded-2xl bg-[#edf2f1] p-1 text-sm">
          {(['category', 'supplier', 'customer'] as const).map((k) => (
            <button key={k} onClick={() => setBy(k)} aria-pressed={by === k} className={`h-10 rounded-xl font-semibold transition ${by === k ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted hover:text-ink'}`}>{t(`sl.byWhat.${k}`)}</button>
          ))}
        </div>
        {by === 'supplier' && <p className="text-xs text-muted">{t('rs.salesShare')}</p>}
        <div className="flex flex-wrap gap-2 text-sm">
          <label className="flex items-center gap-1.5">{t('sl.from')}<input type="date" value={range.from} max={range.to} onChange={(e) => e.target.value && setRange({ ...range, from: e.target.value })} className="h-10 rounded-xl border border-line bg-white px-2 outline-none focus:border-brand-line" /></label>
          <label className="flex items-center gap-1.5">{t('sl.to')}<input type="date" value={range.to} min={range.from} onChange={(e) => e.target.value && setRange({ ...range, to: e.target.value })} className="h-10 rounded-xl border border-line bg-white px-2 outline-none focus:border-brand-line" /></label>
        </div>
        {data && <span className="text-sm text-muted">{t('sl.sales')}: <b className="text-ink">{formatRupees(data.total)}</b></span>}
      </section>
      {data && data.groups.length > 0 && (
        <div className="hidden lg:block">
          <DataTable label={t('sl.salesBy')} rows={data.groups} rowKey={(g) => g.key || 'none'} defaultSort={{ key: 'sales', dir: -1 }}
            onRowClick={by === 'customer' ? (g) => { if (g.key) navigate(`/c/${g.key}`) } : undefined}
            columns={[
              { key: 'name', header: t(`sl.byWhat.${by}`), sortValue: (g) => label(g), filter: { kind: 'text', value: (g) => label(g) }, render: (g) => <span className="font-semibold">{label(g)}</span> },
              { key: 'sales', header: t('sl.sales'), align: 'right', width: 'w-32', sortValue: (g) => g.sales, render: (g) => <b>{formatRupees(g.sales)}</b> },
              { key: 'share', header: t('sl.share'), width: 'w-48', sortValue: (g) => g.share,
                render: (g) => <span className="flex items-center gap-2"><span className="h-2 flex-1 overflow-hidden rounded-full bg-[#edf2f1]"><span className="block h-full rounded-full bg-teal-600" style={{ width: `${Math.max(2, g.share)}%` }} /></span><span className="w-12 text-right text-xs">{g.share}%</span></span> },
              { key: 'qty', header: t('shTable.qty'), align: 'right', width: 'w-24', sortValue: (g) => g.quantity, render: (g) => g.quantity },
              { key: 'bills', header: t('sl.bills'), align: 'right', width: 'w-20', sortValue: (g) => g.bills, render: (g) => g.bills },
              { key: 'profit', header: t('sl.profit'), align: 'right', width: 'w-32', sortValue: (g) => g.profit,
                render: (g) => (g.profit == null ? '—' : <span className={g.profit >= 0 ? 'font-semibold text-jama' : 'font-semibold text-due'}>{formatRupees(g.profit)}</span>) },
            ]}
            footer={[t('yearRep.total'), formatRupees(data.total), '', '', '', formatRupees(data.groups.reduce((n, g) => n + (g.profit ?? 0), 0))]} />
        </div>
      )}
      <ul className="flex flex-col gap-2 lg:hidden">
        {data?.groups.map((g) => (
          <li key={g.key || 'none'}>
            <button disabled={by !== 'customer' || !g.key} onClick={() => navigate(`/c/${g.key}`)} className="flex w-full flex-col gap-1.5 rounded-2xl bg-white p-3 text-left shadow-sm enabled:hover:bg-[#f6faf9]">
              <span className="flex items-baseline justify-between gap-2">
                <b className="truncate">{label(g)}</b>
                <b className="shrink-0">{formatRupees(g.sales)}</b>
              </span>
              <span className="h-2 overflow-hidden rounded-full bg-[#edf2f1]"><span className="block h-full rounded-full bg-teal-600" style={{ width: `${Math.max(2, g.share)}%` }} /></span>
              <span className="flex justify-between text-xs text-muted">
                <span>{g.share}% · {t('sl.billsCount', { count: g.bills })}</span>
                {g.profit !== null && <span className={g.profit >= 0 ? 'font-semibold text-jama' : 'font-semibold text-due'}>{t('sl.profit')} {formatRupees(g.profit)}</span>}
              </span>
            </button>
          </li>
        ))}
        {data && data.groups.length === 0 && <p className="py-6 text-center text-muted">{t('sl.none')}</p>}
      </ul>
    </div>
  )
}

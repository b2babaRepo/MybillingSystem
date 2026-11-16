import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ReportShell, type ReportView } from '../components/ReportShell'
import { shownLocale } from '../i18n'
import { api } from '../lib/api'
import { formatRupees, indiaToday } from '../lib/format'
import { navigate } from '../router'

interface BalanceSheet {
  on: string
  cash: number; bank: number; stock: number; customersOwe: number; supplierAdvances: number; totalAssets: number
  suppliersOwed: number; customerAdvances: number; totalLiabilities: number
  capital: number; customersOwing: number; suppliersOwedCount: number; unvaluedItems: number
}

/**
 * The balance sheet for the CA and the bank: what the shop has against what it owes at the end of a day, with the
 * owner's capital as what is left. Every line opens the screen it comes from, so each figure can be checked.
 */
export function BalanceSheetScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const [on, setOn] = useState(indiaToday())
  const [view, setView] = useState<ReportView>('chart')
  const sheet = useQuery({ queryKey: [shopId, 'balance-sheet', on], queryFn: () => api<BalanceSheet>(`/v1/reports/balance-sheet?on=${on}`) }).data
  const day = new Date(`${on}T00:00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'long', year: 'numeric' })

  const row = (label: string, amount: number, path: string, note?: string) => (
    <button key={label} onClick={() => navigate(path)} className="flex min-h-12 w-full items-center justify-between gap-3 border-t border-line py-2.5 text-left first:border-t-0">
      <span className="flex flex-col"><span className="font-semibold">{label}</span>{note && <span className="text-xs text-muted">{note}</span>}</span>
      <span className="shrink-0 font-semibold tabular-nums">{formatRupees(amount)} ›</span>
    </button>
  )

  return (
    <ReportShell page="balance" title={t('bs.title')} isOwner view={view} onView={setView}
      ticks={sheet ? [{ label: t('bs.assets'), value: formatRupees(sheet.totalAssets) }, { label: t('bs.liabilities'), value: formatRupees(sheet.totalLiabilities) }, { label: t('bs.capital'), value: formatRupees(sheet.capital) }] : []}>
      <div className="mx-auto flex w-full max-w-3xl flex-col gap-3 p-4">
        <label className="flex flex-col gap-1 text-sm font-semibold text-muted">{t('bs.on')}
          <input type="date" value={on} max={indiaToday()} onChange={(e) => e.target.value && setOn(e.target.value)} className="h-12 rounded-xl border border-line bg-white px-3 text-base text-ink" />
        </label>
        {!sheet ? <p className="text-muted">…</p> : (
          <>
            <section className="rounded-card bg-white p-4 shadow-sm">
              <h2 className="flex justify-between text-lg font-bold"><span>{t('bs.assets')}</span><span className="tabular-nums text-jama">{formatRupees(sheet.totalAssets)}</span></h2>
              <p className="mb-1 text-xs text-muted">{t('bs.assetsHint')}</p>
              {row(t('bs.cash'), sheet.cash, '/cashledger')}
              {row(t('bs.bank'), sheet.bank, '/bankbook')}
              {row(t('bs.stock'), sheet.stock, '/stock-statement', sheet.unvaluedItems > 0 ? t('bs.unvalued', { count: sheet.unvaluedItems }) : t('bs.atCost'))}
              {row(t('bs.customersOwe'), sheet.customersOwe, '/dues', t('bs.people', { count: sheet.customersOwing }))}
              {sheet.supplierAdvances > 0 && row(t('bs.supplierAdvances'), sheet.supplierAdvances, '/suppliers')}
            </section>
            <section className="rounded-card bg-white p-4 shadow-sm">
              <h2 className="flex justify-between text-lg font-bold"><span>{t('bs.liabilities')}</span><span className="tabular-nums text-due">{formatRupees(sheet.totalLiabilities)}</span></h2>
              <p className="mb-1 text-xs text-muted">{t('bs.liabilitiesHint')}</p>
              {row(t('bs.suppliersOwed'), sheet.suppliersOwed, '/suppliers', t('bs.suppliers', { count: sheet.suppliersOwedCount }))}
              {sheet.customerAdvances > 0 && row(t('bs.customerAdvances'), sheet.customerAdvances, '/khata')}
            </section>
            <section className="rounded-card bg-brand-soft p-4">
              <p className="flex justify-between text-xl font-bold"><span>{t('bs.capital')}</span><span className="tabular-nums">{formatRupees(sheet.capital)}</span></p>
              <p className="mt-1 text-sm text-muted">{t('bs.capitalHint')}</p>
              <p className="mt-3 text-xs text-muted">{t('bs.asOf', { day })} · {t('bs.note')}</p>
            </section>
          </>
        )}
      </div>
    </ReportShell>
  )
}

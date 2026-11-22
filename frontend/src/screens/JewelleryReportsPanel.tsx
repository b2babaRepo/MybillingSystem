import { useQuery } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { btn } from '../components/ui'
import { api } from '../lib/api'
import { saveCsv } from '../lib/csv'
import { formatRupees } from '../lib/format'
import { grams, karat, type Metal } from '../lib/jewellery'

// Phase 4 of the jewellery counter: stock and sales by karat with fine weight, and cash per customer for PMLA (₹10 lakh)
// and the SFT return (more than ₹2 lakh), by financial year.

interface StockLine { metal: Metal; purity: number; pieces: number; onApproval: number; gross: number; net: number; fine: number; ratePerGram: number | null; value: number | null }
interface MetalTotals { metal: Metal; pieces: number; gross: number; net: number; fine: number; value: number; loose: number; withParties: number; owedToParties: number }
interface StockReport { lines: StockLine[]; totals: MetalTotals[]; ratesMissing: boolean }
interface SalesReport { fy: number; from: string; to: string; lines: { metal: Metal; purity: number; pieces: number; gross: number; net: number; fine: number; taxable: number; gst: number }[]; oldMetalBought: number; oldMetalFine: number }
interface CashCustomer { customerId: string | null; name: string; phone: string | null; pan: string | null; bills: number; billsCash: number; khataCash: number; girviCash: number; total: number; months: number[]; pmlaMonth: boolean; pmlaYear: boolean; sft: boolean }
interface CashReport { fy: number; from: string; to: string; pmlaLimit: number; sftLimit: number; customers: CashCustomer[]; walkInCash: number }

const MONTHS = ['Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec', 'Jan', 'Feb', 'Mar']
const th = 'px-2 py-2.5'
const purityLabel = (metal: Metal, purity: number, silver: string) => (metal === 'gold' ? `${karat(metal, purity)} · ${purity}` : `${silver} ${purity}`)

function Card({ tone, label, value, sub }: { tone: string; label: string; value: ReactNode; sub?: ReactNode }) {
  return <div className={`flex min-h-[76px] flex-col justify-between rounded-[18px] p-3 ${tone}`}><span className="text-[13px] font-bold">{label}</span><b className="text-xl">{value}</b>{sub && <span className="text-xs opacity-80">{sub}</span>}</div>
}

export function JewelleryReportsPanel({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const now = new Date()
  const thisFy = now.getMonth() >= 3 ? now.getFullYear() : now.getFullYear() - 1
  const [fy, setFy] = useState(thisFy)
  const [view, setView] = useState<'stock' | 'sales' | 'cash'>('stock')
  const stock = useQuery({ queryKey: [shopId, 'jewellery', 'report-stock'], queryFn: () => api<StockReport>('/v1/jewellery/reports/stock'), enabled: view === 'stock' }).data
  const sales = useQuery({ queryKey: [shopId, 'jewellery', 'report-sales', fy], queryFn: () => api<SalesReport>(`/v1/jewellery/reports/sales?fy=${fy}`), enabled: view === 'sales' }).data
  const cash = useQuery({ queryKey: [shopId, 'jewellery', 'report-cash', fy], queryFn: () => api<CashReport>(`/v1/jewellery/reports/cash?fy=${fy}`), enabled: view === 'cash' && isOwner }).data
  const fyLabel = (y: number) => `FY ${y}-${String(y + 1).slice(2)}`
  const gold = stock?.totals.find((x) => x.metal === 'gold')
  const silver = stock?.totals.find((x) => x.metal === 'silver')

  function exportCash(onlyFlagged: boolean) {
    if (!cash) return
    const rows = cash.customers.filter((c) => !onlyFlagged || c.sft || c.pmlaYear || c.pmlaMonth)
    saveCsv(`${onlyFlagged ? 'sft-pmla' : 'cash-by-customer'}-${fyLabel(cash.fy).replace(' ', '-')}.csv`, [
      ['Name', 'Mobile', 'PAN', 'Bills', 'Cash on bills', 'Cash on khata', 'Cash on girvi', 'Total cash', ...MONTHS, 'Over 2 lakh (SFT)', '10 lakh in a year (PMLA)', '10 lakh in a month (PMLA)'],
      ...rows.map((c) => [c.name, c.phone, c.pan, c.bills, c.billsCash, c.khataCash, c.girviCash, c.total, ...c.months, c.sft ? 'Yes' : '', c.pmlaYear ? 'Yes' : '', c.pmlaMonth ? 'Yes' : '']),
    ])
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center gap-2">
        <div className="flex gap-1 rounded-xl bg-[#edf2f1] p-1">
          {(isOwner ? (['stock', 'sales', 'cash'] as const) : (['stock', 'sales'] as const)).map((v) => <button key={v} onClick={() => setView(v)} aria-pressed={view === v} className={`min-h-9 rounded-lg px-3 text-sm font-semibold ${view === v ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted'}`}>{t(`jwl4.view.${v}`)}</button>)}
        </div>
        {view !== 'stock' && (
          <select value={fy} onChange={(e) => setFy(Number(e.target.value))} aria-label={t('jwl4.year')} className="ml-auto h-11 rounded-xl border border-line bg-white px-3">
            {[0, 1, 2, 3].map((n) => <option key={n} value={thisFy - n}>{fyLabel(thisFy - n)}</option>)}
          </select>
        )}
      </div>

      {view === 'stock' && stock && (
        <>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            <Card tone="bg-[#fdf1e3] text-[#a1460a]" label={t('jwl4.goldFine')} value={grams(gold?.fine ?? 0)} sub={t('jwl4.inPieces', { count: gold?.pieces ?? 0 })} />
            <Card tone="bg-[#edf2f1] text-[#33423d]" label={t('jwl4.silverFine')} value={grams(silver?.fine ?? 0)} sub={t('jwl4.inPieces', { count: silver?.pieces ?? 0 })} />
            <Card tone="bg-[#e3f2ee] text-[#0b5e4f]" label={t('jwl4.stockValue')} value={formatRupees((gold?.value ?? 0) + (silver?.value ?? 0))} sub={t('jwl4.atToday')} />
            <Card tone="bg-[#f1eafd] text-[#6d28d9]" label={t('jwl4.allGold')} value={grams((gold?.fine ?? 0) + (gold?.loose ?? 0) + (gold?.withParties ?? 0) - (gold?.owedToParties ?? 0))} sub={t('jwl4.allGoldSub')} />
          </div>
          {stock.ratesMissing && <p className="rounded-xl bg-warn-soft p-3 text-xs font-semibold text-warn-ink">{t('jwl4.ratesMissing')}</p>}
          <div className="overflow-x-auto rounded-card bg-white shadow-sm">
            <table className="w-full min-w-[700px] border-collapse text-sm">
              <thead className="bg-[#f6faf9] text-left text-[11px] tracking-wide text-muted uppercase">
                <tr><th className="px-3 py-2.5">{t('jwl.purity')}</th><th className={`${th} text-right`}>{t('jwl.pieces')}</th><th className={`${th} text-right`}>{t('jwl4.gross')}</th><th className={`${th} text-right`}>{t('jwl4.net')}</th>
                  <th className={`${th} text-right`}>{t('jwl.fine')}</th><th className={`${th} text-right`}>{t('jwl4.rate')}</th><th className="px-3 py-2.5 text-right">{t('jwl4.value')}</th></tr>
              </thead>
              <tbody>
                {stock.lines.length === 0 && <tr><td colSpan={7} className="p-6 text-center text-muted">{t('jwl.noPieces')}</td></tr>}
                {stock.lines.map((l) => (
                  <tr key={`${l.metal}${l.purity}`} className="border-t border-line">
                    <td className="px-3 py-2.5 font-bold">{purityLabel(l.metal, l.purity, t('jwl.silver'))}</td>
                    <td className={`${th} text-right`}>{l.pieces}{l.onApproval > 0 && <span className="block text-xs text-[#6d28d9]">{t('jwl4.outOnApproval', { count: l.onApproval })}</span>}</td>
                    <td className={`${th} text-right tabular-nums`}>{l.gross.toFixed(3)}</td><td className={`${th} text-right tabular-nums`}>{l.net.toFixed(3)}</td>
                    <td className={`${th} text-right font-bold tabular-nums`}>{l.fine.toFixed(3)}</td>
                    <td className={`${th} text-right`}>{l.ratePerGram ? `${formatRupees(l.ratePerGram)}/g` : '—'}</td>
                    <td className="px-3 py-2.5 text-right font-semibold">{l.value != null ? formatRupees(l.value) : '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {[gold, silver].filter((x) => x && (x.loose !== 0 || x.withParties > 0 || x.owedToParties > 0)).map((x) => (
            <p key={x!.metal} className="rounded-xl bg-white p-3 text-sm shadow-sm">{t('jwl4.metalLine', { metal: t(`jwl.${x!.metal}`), loose: grams(x!.loose), out: grams(x!.withParties), owed: grams(x!.owedToParties) })}</p>
          ))}
        </>
      )}

      {view === 'sales' && sales && (
        <>
          <div className="overflow-x-auto rounded-card bg-white shadow-sm">
            <table className="w-full min-w-[700px] border-collapse text-sm">
              <thead className="bg-[#f6faf9] text-left text-[11px] tracking-wide text-muted uppercase">
                <tr><th className="px-3 py-2.5">{t('jwl.purity')}</th><th className={`${th} text-right`}>{t('jwl4.piecesSold')}</th><th className={`${th} text-right`}>{t('jwl4.net')}</th><th className={`${th} text-right`}>{t('jwl.fine')}</th>
                  <th className={`${th} text-right`}>{t('jwl.taxable')}</th><th className="px-3 py-2.5 text-right">GST</th></tr>
              </thead>
              <tbody>
                {sales.lines.length === 0 && <tr><td colSpan={6} className="p-6 text-center text-muted">{t('jwl4.noSales')}</td></tr>}
                {sales.lines.map((l) => (
                  <tr key={`${l.metal}${l.purity}`} className="border-t border-line">
                    <td className="px-3 py-2.5 font-bold">{purityLabel(l.metal, l.purity, t('jwl.silver'))}</td><td className={`${th} text-right`}>{l.pieces}</td>
                    <td className={`${th} text-right tabular-nums`}>{l.net.toFixed(3)}</td><td className={`${th} text-right font-bold tabular-nums`}>{l.fine.toFixed(3)}</td>
                    <td className={`${th} text-right`}>{formatRupees(l.taxable)}</td><td className="px-3 py-2.5 text-right">{formatRupees(l.gst)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {sales.oldMetalBought > 0 && <p className="rounded-xl bg-white p-3 text-sm shadow-sm">{t('jwl4.oldBought', { amount: formatRupees(sales.oldMetalBought), fine: grams(sales.oldMetalFine) })}</p>}
        </>
      )}

      {view === 'cash' && cash && (
        <>
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            <Card tone="bg-[#e3f2ee] text-[#0b5e4f]" label={t('jwl4.customersPaidCash')} value={cash.customers.length} />
            <Card tone="bg-[#e8effd] text-[#1d4ed8]" label={t('jwl4.cashTotal')} value={formatRupees(cash.customers.reduce((n, c) => n + c.total, 0) + cash.walkInCash)} />
            <Card tone={cash.customers.some((c) => c.sft) ? 'bg-[#a1460a] text-white' : 'bg-[#fdf1e3] text-[#a1460a]'} label={t('jwl4.sftCount')} value={cash.customers.filter((c) => c.sft).length} />
            <Card tone={cash.customers.some((c) => c.pmlaYear || c.pmlaMonth) ? 'bg-[#be123c] text-white' : 'bg-[#fde8ec] text-[#be123c]'} label={t('jwl4.pmlaCount')} value={cash.customers.filter((c) => c.pmlaYear || c.pmlaMonth).length} />
          </div>
          <p className="rounded-xl bg-warn-soft p-3 text-xs font-semibold text-warn-ink">{t('jwl4.cashLaw')}</p>
          <div className="flex flex-wrap justify-end gap-2">
            <button onClick={() => exportCash(false)} disabled={cash.customers.length === 0} className={`min-h-11 rounded-xl px-4 ${btn.outline}`}>{t('jwl4.downloadAll')}</button>
            <button onClick={() => exportCash(true)} disabled={!cash.customers.some((c) => c.sft || c.pmlaYear || c.pmlaMonth)} className={`min-h-11 rounded-xl px-4 ${btn.solid}`}>{t('jwl4.downloadFlagged')}</button>
          </div>
          <div className="overflow-x-auto rounded-card bg-white shadow-sm">
            <table className="w-full min-w-[860px] border-collapse text-sm">
              <thead className="bg-[#f6faf9] text-left text-[11px] tracking-wide text-muted uppercase">
                <tr><th className="px-3 py-2.5">{t('jwl2.customer')}</th><th className={th}>PAN</th><th className={`${th} text-right`}>{t('jwl4.onBills')}</th><th className={`${th} text-right`}>{t('jwl4.onKhata')}</th>
                  <th className={`${th} text-right`}>{t('jwl4.onGirvi')}</th><th className={`${th} text-right`}>{t('jwl4.total')}</th><th className={th}>{t('jwl4.highestMonth')}</th><th className="px-3 py-2.5" /></tr>
              </thead>
              <tbody>
                {cash.customers.length === 0 && <tr><td colSpan={8} className="p-6 text-center text-muted">{t('jwl4.noCash')}</td></tr>}
                {cash.customers.map((c) => {
                  const top = c.months.indexOf(Math.max(...c.months))
                  return (
                    <tr key={c.customerId ?? c.name} className="border-t border-line">
                      <td className="px-3 py-2.5"><b>{c.name}</b>{c.phone && <span className="block text-xs text-muted">{c.phone}</span>}</td>
                      <td className={`${th} font-mono text-xs`}>{c.pan ?? (c.sft ? <span className="font-sans font-bold text-due">{t('jwl4.needPan')}</span> : '—')}</td>
                      <td className={`${th} text-right`}>{formatRupees(c.billsCash)}<span className="block text-xs text-muted">{t('jwl4.bills', { count: c.bills })}</span></td>
                      <td className={`${th} text-right`}>{c.khataCash ? formatRupees(c.khataCash) : '—'}</td>
                      <td className={`${th} text-right`}>{c.girviCash ? formatRupees(c.girviCash) : '—'}</td>
                      <td className={`${th} text-right font-bold`}>{formatRupees(c.total)}</td>
                      <td className={th}>{MONTHS[top]} · {formatRupees(c.months[top])}</td>
                      <td className="px-3 py-2.5"><span className="flex flex-wrap justify-end gap-1">
                        {c.sft && <span className="rounded-full bg-[#fdf1e3] px-2.5 py-0.5 text-xs font-bold text-[#a1460a]">SFT</span>}
                        {(c.pmlaYear || c.pmlaMonth) && <span className="rounded-full bg-due-soft px-2.5 py-0.5 text-xs font-bold text-due">PMLA</span>}</span></td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
          {cash.walkInCash > 0 && <p className="text-xs text-muted">{t('jwl4.walkIn', { amount: formatRupees(cash.walkInCash) })}</p>}
        </>
      )}
    </div>
  )
}

import { shownLocale } from '../i18n'
import { type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'

export type ReportView = 'chart' | 'ledger'
export type ReportPage = 'reports' | 'pnl' | 'gst' | 'balance'

/** One figure in the ticker strip: its label, value and, when known, the change on the period before. */
export interface Tick { label: string; value: string; change?: number | null; note?: string }

/** ▲ 12.4% in teal or ▼ 6.1% in orange (colour-blind safe, and the arrow carries it too). */
export function Delta({ value, invert = false, className = '' }: { value: number | null | undefined; invert?: boolean; className?: string }) {
  if (value === null || value === undefined || !Number.isFinite(value)) return null
  const good = invert ? value <= 0 : value >= 0
  return <span className={`font-semibold ${good ? 'text-brand' : 'text-orange-700'} ${className}`}>{value >= 0 ? '▲' : '▼'} {Math.abs(value)}%</span>
}

/**
 * The header of the Reports, P&L and GST pages: a dark band with the page tabs, the Chart / Ledger switch, and a
 * ticker strip of the period's figures with their moves.
 */
export function ReportShell({ page, title, isOwner, view, onView, ticks, children }: {
  page: ReportPage; title: string; isOwner: boolean; view: ReportView; onView: (v: ReportView) => void; ticks: Tick[]; children: ReactNode
}) {
  const { t } = useTranslation()
  const tabs: { key: ReportPage; path: string; label: string }[] = [
    { key: 'reports', path: '/reports', label: t('rv.reports') },
    ...(isOwner ? [{ key: 'pnl' as const, path: '/pnl', label: t('rv.pnl') }, { key: 'gst' as const, path: '/gst', label: t('rv.gst') }, { key: 'balance' as const, path: '/balance-sheet', label: t('rv.balance') }] : []),
  ]
  return (
    <div className="flex flex-col">
      <header className="bg-[#0f3d38] px-4 pt-4 text-white lg:px-6">
        <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-3">
          <h1 className="text-[22px] font-bold">{title}</h1>
          {tabs.length > 1 && (
            <nav aria-label={t('rv.pages')} className="flex flex-wrap gap-1.5">
              {tabs.map((tab) => (
                <button key={tab.key} onClick={() => navigate(tab.path)} aria-current={tab.key === page ? 'page' : undefined}
                  className={`min-h-11 rounded-full px-4 text-sm font-semibold ${tab.key === page ? 'bg-[#ffffff] text-[#0f3d38]' : 'border border-[#3f6f69] text-white'}`}>{tab.label}</button>
              ))}
            </nav>
          )}
          <div role="group" aria-label={t('rv.view')} className="flex gap-1 rounded-xl bg-[#164e48] p-1">
            {(['chart', 'ledger'] as const).map((v) => (
              <button key={v} onClick={() => onView(v)} aria-pressed={view === v}
                className={`min-h-10 rounded-lg px-3 text-[13px] ${view === v ? 'bg-[#5eead4] font-bold text-[#0f3d38]' : 'text-white'}`}>{t(`rv.${v}`)}</button>
            ))}
          </div>
        </div>
        {ticks.length > 0 && (
          <div aria-label={t('rv.ticker')} className="mx-auto mt-3 flex max-w-6xl gap-6 overflow-x-auto whitespace-nowrap border-t border-[#285650] py-2.5 text-[13px]">
            {ticks.map((k) => (
              <span key={k.label}><b className="uppercase">{k.label}</b> {k.value}{' '}
                {k.change !== undefined && k.change !== null
                  ? <span className={k.change >= 0 ? 'text-[#5eead4]' : 'text-[#fdba74]'}>{k.change >= 0 ? '▲' : '▼'} {Math.abs(k.change)}%</span>
                  : k.note && <span className="text-[#fdba74]">{k.note}</span>}
              </span>
            ))}
          </div>
        )}
      </header>
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-3 p-4">{children}</div>
    </div>
  )
}

/** A small trend line for a tile. */
export function Sparkline({ values, tone = '#0f766e' }: { values: number[]; tone?: string }) {
  if (values.length < 2) return null
  const max = Math.max(...values), min = Math.min(...values), span = max - min || 1
  const points = values.map((v, i) => `${(i / (values.length - 1)) * 200},${34 - ((v - min) / span) * 30}`).join(' ')
  return <svg viewBox="0 0 200 36" className="h-9 w-full" aria-hidden="true"><polyline points={points} fill="none" stroke={tone} strokeWidth="2.5" /></svg>
}

/** Daily bars coloured by whether the day beat the one before, with the earlier period's average as a dashed line. */
export function UpDownBars({ days, average, label }: { days: { date: string; sales: number }[]; average?: number; label: string }) {
  const { t } = useTranslation()
  const peak = Math.max(1, average ?? 0, ...days.map((d) => d.sales))
  const w = 700 / Math.max(days.length, 1)
  const y = (v: number) => 200 - (v / peak) * 185
  const best = days.reduce((a, d) => (d.sales > (a?.sales ?? -1) ? d : a), days[0])
  return (
    <figure className="m-0 flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm print:shadow-none">
      <figcaption className="flex flex-wrap items-baseline justify-between gap-2">
        <b>{label}</b>
        <span className="text-xs text-muted"><span className="text-brand">■</span> {t('rv.up')} · <span className="text-orange-700">■</span> {t('rv.down')}{average ? ` · ${t('rv.avgLine')}` : ''}</span>
      </figcaption>
      <svg viewBox="0 0 700 220" className="h-auto w-full" role="img" aria-label={best ? t('rv.bestDay', { date: best.date, amount: formatRupees(best.sales) }) : label}>
        <line x1="0" y1="200" x2="700" y2="200" stroke="#d5e0de" />
        {!!average && <line x1="0" y1={y(average)} x2="700" y2={y(average)} stroke="#4b5d59" strokeDasharray="6 6" />}
        {days.map((d, i) => {
          const up = i === 0 || d.sales >= days[i - 1].sales
          return <rect key={d.date} x={i * w + w * 0.15} y={y(d.sales)} width={w * 0.7} height={Math.max(2, 200 - y(d.sales))} rx="3" fill={d.sales === 0 ? '#e3ebe9' : up ? '#0f766e' : '#c2410c'}><title>{`${d.date}: ${formatRupees(d.sales)}`}</title></rect>
        })}
        <text x="2" y="216" fontSize="11" fill="#4b5d59">1</text>
        <text x="690" y="216" fontSize="11" fill="#4b5d59" textAnchor="end">{days.length}</text>
      </svg>
      {best && best.sales > 0 && <p className="text-[13px] text-muted">{t('rv.bestDay', { date: new Date(best.date).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' }), amount: formatRupees(best.sales) })}</p>}
    </figure>
  )
}

/** Sales down to profit, step by step: a positive first bar, then each deduction hanging from the one before. */
export function Waterfall({ steps, label }: { steps: { label: string; amount: number; total?: boolean }[]; label: string }) {
  const top = Math.max(1, ...steps.map((s) => Math.abs(s.amount)))
  const h = (v: number) => (Math.abs(v) / top) * 200
  const bars: (typeof steps[number] & { from: number; to: number })[] = []
  let level = 0
  for (const s of steps) {
    if (s.total) { bars.push({ ...s, from: 0, to: s.amount }); level = s.amount; continue }
    bars.push({ ...s, from: level + s.amount, to: level })
    level += s.amount
  }
  const w = 1000 / steps.length
  return (
    <svg viewBox="0 0 1000 270" className="h-auto w-full" role="img" aria-label={label}>
      <line x1="0" y1="230" x2="1000" y2="230" stroke="#d5e0de" />
      {bars.map((b, i) => {
        const yTop = 230 - h(Math.max(b.from, b.to)), height = Math.max(3, Math.abs(h(b.to) - h(b.from)))
        return (
          <g key={b.label}>
            <rect x={i * w + w * 0.12} y={yTop} width={w * 0.76} height={height} rx="6" fill={b.total ? (b.amount >= 0 ? '#0f766e' : '#c2410c') : '#c2410c'} />
            <text x={i * w + w / 2} y={yTop - 6} fontSize="13" textAnchor="middle" fontWeight={b.total ? 700 : 400} fill={b.total ? '#10201d' : '#9a3412'}>{b.total ? formatRupees(b.amount) : `− ${formatRupees(-b.amount)}`}</text>
            <text x={i * w + w / 2} y="252" fontSize="12" textAnchor="middle" fill="#4b5d59">{b.label}</text>
          </g>
        )
      })}
    </svg>
  )
}

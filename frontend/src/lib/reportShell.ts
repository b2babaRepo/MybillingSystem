import { useState } from 'react'
import type { ReportView } from '../components/ReportShell'

const KEY = 'gk-report-view'

/** Chart or ledger, remembered on this phone: the owner keeps charts, the CA's login keeps tables. */
export function useReportView(): [ReportView, (v: ReportView) => void] {
  const [view, setView] = useState<ReportView>(() => {
    try { return localStorage.getItem(KEY) === 'ledger' ? 'ledger' : 'chart' } catch { return 'chart' }
  })
  return [view, (v) => { setView(v); try { localStorage.setItem(KEY, v) } catch { /* private window */ } }]
}

/** Percent change from the period before; null when there is nothing to compare with. */
export const change = (now: number, before: number | undefined | null) =>
  before ? Math.round(((now - before) / Math.abs(before)) * 1000) / 10 : null

/** Days left until a date (negative once past). */
export const daysUntil = (iso: string) => Math.ceil((Date.parse(`${iso}T23:59:59+05:30`) - Date.now()) / 86_400_000)

import type { StatementLine } from './types'

export type Line = StatementLine & { pending?: boolean }

/**
 * What is still unpaid on each udhaar line today: payments clear the oldest udhaar first (the same rule as the dues by
 * age and the Sale bills list). Removed lines and their removals count for nothing.
 */
export function dueByLine(entries: Line[]): Map<string, number> {
  const open: { id: string; left: number }[] = []
  let credit = 0
  for (const e of entries) {
    if (e.isReversed || e.kind === 'reversal') continue
    if (e.amount > 0) {
      let left = e.amount
      const used = Math.min(credit, left)
      credit -= used
      left -= used
      open.push({ id: e.id, left })
    } else {
      let pay = -e.amount
      for (const o of open) {
        if (pay <= 0) break
        const take = Math.min(o.left, pay)
        o.left -= take
        pay -= take
      }
      credit += pay
    }
  }
  return new Map(open.map((o) => [o.id, Math.round(o.left * 100) / 100]))
}

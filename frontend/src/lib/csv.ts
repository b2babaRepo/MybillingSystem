/** Saves rows as a CSV file the phone or computer downloads (Excel opens it; the BOM keeps ₹ and Hindi right). */
export function saveCsv(fileName: string, rows: (string | number | null | undefined)[][]) {
  const cell = (v: string | number | null | undefined) => {
    const text = v === null || v === undefined ? '' : String(v)
    return /[",\n]/.test(text) ? `"${text.replace(/"/g, '""')}"` : text
  }
  const blob = new Blob(['﻿' + rows.map((r) => r.map(cell).join(',')).join('\r\n')], { type: 'text/csv;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  a.click()
  URL.revokeObjectURL(url)
}

/** "2026-11-01": the first of this month in India; and today. */
export function monthToDate(): { from: string; to: string } {
  const now = new Date(Date.now() + 19_800_000).toISOString().slice(0, 10)
  return { from: `${now.slice(0, 8)}01`, to: now }
}

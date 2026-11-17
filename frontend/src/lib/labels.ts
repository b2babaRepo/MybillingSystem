/**
 * Label stock shops buy: A4 sticker sheets (the common 65, 40 and 24 per sheet) and thermal label rolls (50x25 and
 * 38x25 mm, one label across). Sizes in mm; each counter remembers its own choice, like its bill paper.
 */
export interface LabelSheet {
  id: string
  /** Label size. */
  w: number
  h: number
  /** A4 sheets: labels across and down, the sheet's top/left margins and the gaps between labels. Rolls: 1 x 1. */
  cols: number
  rows: number
  top: number
  left: number
  gapX: number
  gapY: number
  /** A roll: each label is its own page of exactly its size. */
  roll: boolean
}

const a4 = (id: string, w: number, h: number, cols: number, rows: number, top: number, left: number, gapX: number, gapY = 0): LabelSheet =>
  ({ id, w, h, cols, rows, top, left, gapX, gapY, roll: false })
const roll = (id: string, w: number, h: number): LabelSheet => ({ id, w, h, cols: 1, rows: 1, top: 0, left: 0, gapX: 0, gapY: 0, roll: true })

export const LABEL_SHEETS: LabelSheet[] = [
  a4('a4-65', 38.1, 21.2, 5, 13, 10.7, 4.7, 2.5),
  a4('a4-40', 52.5, 29.7, 4, 10, 0, 0, 0),
  a4('a4-24', 63.5, 33.9, 3, 8, 12.9, 7.2, 2.5),
  roll('roll-50x25', 50, 25),
  roll('roll-38x25', 38, 25),
]

const KEY = 'gk-labels'

export function getSheet(): LabelSheet {
  try {
    const saved = localStorage.getItem(KEY)
    return LABEL_SHEETS.find((s) => s.id === saved) ?? LABEL_SHEETS[0]
  } catch {
    return LABEL_SHEETS[0]
  }
}

export function setSheet(id: string) {
  try { localStorage.setItem(KEY, id) } catch { /* private window */ }
}

/** The labels page by page: a sheet's worth each, skipping `skip` spots already used on the first sheet. */
export function pages<T>(labels: T[], sheet: LabelSheet, skip = 0): (T | null)[][] {
  const per = sheet.cols * sheet.rows
  const spots: (T | null)[] = [...Array<null>(sheet.roll ? 0 : Math.min(Math.max(skip, 0), per - 1)).fill(null), ...labels]
  const out: (T | null)[][] = []
  for (let i = 0; i < spots.length; i += per) out.push(spots.slice(i, i + per))
  return out
}

/** The print rule: page size, no browser margins (the sheet's own margins are drawn), so labels land on the stickers. */
export function labelPageRule(sheet: LabelSheet): string {
  return sheet.roll ? `@page { size: ${sheet.w}mm ${sheet.h}mm; margin: 0; }` : '@page { size: A4; margin: 0; }'
}

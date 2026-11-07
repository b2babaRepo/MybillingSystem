export type Paper = 'a4' | '80' | '58'

const KEY = 'gk-paper'

/** The paper on this device's printer: each counter has its own, so it is kept here, not on the shop. */
export function getPaper(): Paper {
  try {
    const saved = localStorage.getItem(KEY)
    return saved === '80' || saved === '58' ? saved : 'a4'
  } catch {
    return 'a4'
  }
}

export function setPaper(paper: Paper) {
  try { localStorage.setItem(KEY, paper) } catch { /* private window: it lasts until reload */ }
}

/**
 * The print rule for a roll: as wide as the paper and as long as the bill, with no margins of its own. Both must be real
 * lengths: browsers drop "58mm auto" and print a full page. A4 keeps the browser's default.
 */
export function pageRule(paper: Paper, lengthMm: number): string | null {
  return paper === 'a4' ? null : `@page { size: ${paper}mm ${Math.max(Math.ceil(lengthMm), 40)}mm; margin: 0; }`
}

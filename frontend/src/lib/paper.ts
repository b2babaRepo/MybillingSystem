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

/** The print rule for a roll: as wide as the paper, as long as the bill, no margins of its own. A4 keeps the browser's default. */
export function pageRule(paper: Paper): string | null {
  return paper === 'a4' ? null : `@page { size: ${paper}mm auto; margin: 0; }`
}

/** Keeps what is typed in a money or quantity box: digits and one decimal point; a comma (some phone keyboards) is a point. */
export function decimalText(raw: string) {
  const cleaned = raw.replace(/,/g, '.').replace(/[^\d.]/g, '')
  const dot = cleaned.indexOf('.')
  return dot < 0 ? cleaned : cleaned.slice(0, dot + 1) + cleaned.slice(dot + 1).replace(/\./g, '')
}

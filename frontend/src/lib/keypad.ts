/** Applies one key press to the amount being typed: digits, one decimal point, at most two paise digits. */
export function pressKey(value: string, key: string, maxDecimals = 2): string {
  if (key === '⌫') return value.slice(0, -1)
  if (key === '.') return value.includes('.') || maxDecimals === 0 ? value : (value || '0') + '.'
  const [whole, fraction] = value.split('.')
  if (fraction !== undefined && fraction.length >= maxDecimals) return value
  if (fraction === undefined && whole.length >= 8) return value
  if (value === '0') return key
  return value + key
}

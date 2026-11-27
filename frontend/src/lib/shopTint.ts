/** A shop's letter tile colours, picked from its id so the same shop always looks the same. */
const TINTS = [['#e3f2ee', '#0b5e4f'], ['#f1eafd', '#6d28d9'], ['#e8effd', '#1d4ed8'], ['#fdf0e1', '#9a3412'], ['#fde8ec', '#be123c'], ['#e0f2fe', '#0369a1']]

export function shopTint(id: string) {
  let h = 0
  for (const c of id) h = (h * 31 + c.charCodeAt(0)) >>> 0
  const [bg, ink] = TINTS[h % TINTS.length]
  return { background: bg, color: ink }
}

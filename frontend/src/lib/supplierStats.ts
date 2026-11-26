export const pct = (n: number | null | undefined) => (n == null ? '—' : `${Math.round(n * 100)}%`)

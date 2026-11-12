import type { TFunction } from 'i18next'

/** Days the free stock lasts at the 30-day sale rate, or null when nothing sold. */
export function coverDays(free: number, dailySales: number | null | undefined): number | null {
  if (!dailySales || dailySales <= 0) return null
  return Math.max(0, free) / dailySales
}

/**
 * Cover in the unit a shopkeeper thinks in: days under two weeks, weeks up to twelve, months beyond; "no sale" when
 * nothing sold (an endless number helps no one).
 */
export function coverText(t: TFunction, days: number | null): string {
  if (days === null) return t('cover.noSale')
  if (days < 14) return t('cover.days', { count: Math.floor(days) })
  if (days <= 84) return t('cover.weeks', { count: Math.round(days / 7) })
  return t('cover.months', { count: Math.round(days / 30) })
}

export const coverTone = (days: number | null) => (days === null ? 'text-muted' : days < 7 ? 'text-due' : days > 90 ? 'text-amber-700' : 'text-emerald-700')

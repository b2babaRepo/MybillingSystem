import i18n from 'i18next'

interface Named { name: string; nameEn?: string | null; nameHi?: string | null }

const same = (a: string | null | undefined, b: string) => !!a && a.trim().toLowerCase() === b.trim().toLowerCase()

/**
 * An item's name in the language being shown: its Hindi name in Hindi, its English name otherwise (there are no
 * Gujarati names in the lists yet, and English reads more easily there than Devanagari). Only for items still called
 * what the master list or common catalogue calls them: a name the shop typed or changed is theirs, shown as it is.
 */
export function itemName(item: Named, language: string = i18n.language ?? 'en'): string {
  if (!same(item.nameEn, item.name) && !same(item.nameHi, item.name)) return item.name
  if (language === 'hi') return item.nameHi?.trim() || item.name
  return item.nameEn?.trim() || item.name
}

/** Whether a search matches the item by any of its names, so "atta" finds आटा and the other way round. */
export const nameMatches = (item: Named, query: string) =>
  [item.name, item.nameEn, item.nameHi].some((n) => !!n && n.toLowerCase().includes(query))

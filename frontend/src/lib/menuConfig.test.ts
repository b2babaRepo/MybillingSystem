import { describe, expect, it } from 'vitest'
import { categoryOf, KIRANA } from './categoryTree'
import { applyMenu, type MenuNode, wordsRule } from './menuConfig'
import type { Product } from './types'

const node = (key: string, kind: 'dept' | 'cat', extra: Partial<MenuNode> = {}): MenuNode =>
  ({ key, kind, parentKey: null, nameHi: null, nameEn: null, nameGu: null, sortOrder: null, isHidden: false, words: null, icon: null, pictureAt: null, ...extra })
const item = (name: string, category: string | null) => ({ name, category }) as Pick<Product, 'name' | 'category'>

describe('applyMenu', () => {
  it('leaves the built-in tree alone with no changes', () => {
    expect(applyMenu(KIRANA, []).map((d) => d.key)).toEqual(KIRANA.map((d) => d.key))
  })

  it('renames, reorders, hides and pictures', () => {
    const tree = applyMenu(KIRANA, [
      node('tea', 'dept', { nameEn: 'Chai', sortOrder: -1, pictureAt: '2026-11-28T05:00:00Z' }),
      node('pooja', 'dept', { isHidden: true }),
      node('baby', 'cat', { isHidden: true }),
    ])
    expect(tree[0]).toMatchObject({ key: 'tea', en: 'Chai', hi: 'चाय, पेय' })
    expect(tree[0].picture).toMatch(/^\/api\/v1\/menu-pictures\/kirana\/tea\?v=\d+$/)
    expect(tree.some((d) => d.key === 'pooja')).toBe(false)
    expect(tree.flatMap((d) => d.categories).some((c) => c.key === 'baby')).toBe(false)
  })

  it('moves a category and adds one whose words win over the built-in groups', () => {
    const tree = applyMenu(KIRANA, [
      node('coffee', 'cat', { parentKey: 'dairy' }),
      node('rusk', 'cat', { parentKey: 'snacks', nameEn: 'Rusk & toast', words: 'rusk, toast' }),
    ])
    expect(tree.find((d) => d.key === 'dairy')!.categories.map((c) => c.key)).toContain('coffee')
    expect(tree.find((d) => d.key === 'tea')!.categories.map((c) => c.key)).not.toContain('coffee')
    expect(tree.find((d) => d.key === 'snacks')!.categories.map((c) => c.key)).toContain('rusk')
    // "Parle Rusk" sits in the dairy and bread group on the common list; the admin's Rusk tile takes it.
    expect(categoryOf(item('Parle Rusk (Elaichi)', 'डेयरी, ब्रेड'), tree).key).toBe('rusk')
    expect(categoryOf(item('Parle Rusk (Elaichi)', 'डेयरी, ब्रेड')).key).toBe('dairy')
  })

  it('turns the admin’s words into a safe rule', () => {
    expect(wordsRule('rusk, toast (bread)')!.test('Britannia Toast (bread)')).toBe(true)
    expect(wordsRule(' , ')).toBeNull()
  })
})

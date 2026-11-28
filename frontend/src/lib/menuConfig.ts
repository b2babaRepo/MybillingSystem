import { useQuery } from '@tanstack/react-query'
import { useMemo } from 'react'
import { api } from './api'
import { KIRANA, type Category, type Department } from './categoryTree'

/** One change the platform admin made to a trade's category menu (see backend Catalogue/MenuNodes.cs). */
export interface MenuNode {
  key: string
  kind: 'dept' | 'cat'
  parentKey: string | null
  nameHi: string | null
  nameEn: string | null
  nameGu: string | null
  sortOrder: number | null
  isHidden: boolean
  words: string | null
  icon: string | null
  pictureAt: string | null
}

export const MENU_TRADE = 'kirana'

const escape = (w: string) => w.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')

/** The admin's comma-separated words as a match rule ("rusk, toast" → /rusk|toast/i); none: null. */
export function wordsRule(words: string | null): RegExp | null {
  const list = (words ?? '').split(',').map((w) => w.trim()).filter(Boolean)
  return list.length ? new RegExp(list.map(escape).join('|'), 'i') : null
}

export const menuPicture = (trade: string, node: MenuNode) =>
  node.pictureAt ? `/api/v1/menu-pictures/${trade}/${node.key}?v=${Date.parse(node.pictureAt)}` : undefined

const NEW_TINT = { tint: '#eef4f3', ink: '#2f5f58' }

/**
 * The built-in tree with the platform admin's changes on top: names, order (by sortOrder, else the built-in place),
 * hiding, a category moved to another department, new departments and categories (a new category takes items by
 * its words), and pictures. Anything the admin hasn't touched stays as built in.
 */
export function applyMenu(base: Department[], nodes: MenuNode[], trade = MENU_TRADE): Department[] {
  const byKey = new Map(nodes.map((n) => [n.key, n]))
  const named = <T extends { hi: string; en: string; gu: string }>(item: T, n?: MenuNode): T =>
    n ? { ...item, hi: n.nameHi ?? item.hi, en: n.nameEn ?? item.en, gu: n.nameGu ?? item.gu } : item

  // Every category with where it now lives.
  const placed: { cat: Category; dept: string; order: number }[] = []
  base.forEach((d) => d.categories.forEach((c, i) => {
    const n = byKey.get(c.key)
    if (n?.isHidden) return
    const extra = wordsRule(n?.words ?? null)
    placed.push({
      cat: { ...named(c, n), icon: n?.icon ?? c.icon, picture: n ? menuPicture(trade, n) : undefined, kw: extra && c.kw ? new RegExp(`${c.kw.source}|${extra.source}`, 'i') : c.kw ?? extra ?? undefined },
      dept: n?.parentKey ?? d.key, order: n?.sortOrder ?? i,
    })
  }))
  const builtIn = new Set(base.flatMap((d) => [d.key, ...d.categories.map((c) => c.key)]))
  nodes.filter((n) => n.kind === 'cat' && !builtIn.has(n.key) && !n.isHidden).forEach((n, i) => {
    placed.push({
      cat: { key: n.key, hi: n.nameHi ?? n.nameEn ?? n.key, en: n.nameEn ?? n.nameHi ?? n.key, gu: n.nameGu ?? n.nameEn ?? n.key, icon: n.icon ?? 'box', ...NEW_TINT, custom: true, kw: wordsRule(n.words) ?? undefined, picture: menuPicture(trade, n) },
      dept: n.parentKey ?? base[0]?.key ?? 'other', order: n.sortOrder ?? 100 + i,
    })
  })

  const depts: { dept: Department; order: number }[] = base
    .map((d, i) => ({ d, n: byKey.get(d.key), i }))
    .filter(({ n }) => !n?.isHidden)
    .map(({ d, n, i }) => ({ dept: { ...named(d, n), icon: n?.icon ?? d.icon, picture: n ? menuPicture(trade, n) : undefined, categories: [] as Category[] }, order: n?.sortOrder ?? i }))
  nodes.filter((n) => n.kind === 'dept' && !builtIn.has(n.key) && !n.isHidden).forEach((n, i) => depts.push({
    dept: { key: n.key, hi: n.nameHi ?? n.nameEn ?? n.key, en: n.nameEn ?? n.nameHi ?? n.key, gu: n.nameGu ?? n.nameEn ?? n.key, icon: n.icon ?? 'box', picture: menuPicture(trade, n), categories: [] },
    order: n.sortOrder ?? 100 + i,
  }))
  const deptOf = new Map(depts.map((x) => [x.dept.key, x.dept]))
  for (const p of placed.sort((a, b) => a.order - b.order)) deptOf.get(p.dept)?.categories.push(p.cat)
  return depts.sort((a, b) => a.order - b.order).map((x) => x.dept)
}

/** The platform's raw changes to a trade's menu (public; every shop and store reads it). */
export const useMenuNodes = (trade = MENU_TRADE) =>
  useQuery({ queryKey: ['menu', trade], queryFn: () => api<MenuNode[]>(`/v1/menu/${trade}`), staleTime: 10 * 60_000, retry: false })

/** The kirana tree as the platform admin has set it; the built-in one until it loads, or if it can't. */
export function useMenuTree(trade = MENU_TRADE): Department[] {
  const nodes = useMenuNodes(trade).data
  return useMemo(() => (nodes?.length ? applyMenu(KIRANA, nodes, trade) : KIRANA), [nodes, trade])
}

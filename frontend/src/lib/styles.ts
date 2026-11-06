import type { Product } from './types'

/** One row on a list: an ordinary item, or a style (one design) with its sizes and colours. */
export type StyleEntry = { kind: 'item'; product: Product } | { kind: 'style'; name: string; items: Product[] }

const ALPHA = ['XXS', 'XS', 'S', 'M', 'L', 'XL', 'XXL', '2XL', '3XL', 'XXXL', '4XL', '5XL']

/**
 * Sizes in the order a shop thinks of them: letter sizes S → XXL, numbers small → large (28, 30, 32; 32B, 34B),
 * kids' ages by the first number (0-3M before 3-6M, months before years), then anything else alphabetically.
 */
export function compareSizes(a: string | null | undefined, b: string | null | undefined) {
  const rank = (s: string | null | undefined): [number, number, string] => {
    const v = (s ?? '').trim().toUpperCase()
    const alpha = ALPHA.indexOf(v)
    if (alpha >= 0) return [0, alpha, v]
    const age = v.match(/^(\d+)-(\d+)([MY])$/)
    if (age) return [1, (age[3] === 'Y' ? 1000 : 0) + Number(age[1]), v]
    const num = v.match(/^(\d+(?:\.\d+)?)/)
    if (num) return [2, Number(num[1]), v]
    return [3, 0, v]
  }
  const [ga, na, sa] = rank(a)
  const [gb, nb, sb] = rank(b)
  return ga - gb || na - nb || sa.localeCompare(sb)
}

/** Sorts a style's items by size, then colour. */
export function sortVariants(items: Product[]) {
  return [...items].sort((x, y) => compareSizes(x.size, y.size) || (x.colour ?? '').localeCompare(y.colour ?? ''))
}

/** Groups items that share a style name (in the order the first of each appears); others stay single. */
export function groupStyles(products: Product[]): StyleEntry[] {
  const out: StyleEntry[] = []
  const styles = new Map<string, Product[]>()
  for (const p of products) {
    const key = p.styleName?.trim()
    if (!key) {
      out.push({ kind: 'item', product: p })
      continue
    }
    const group = styles.get(key)
    if (group) group.push(p)
    else {
      const items = [p]
      styles.set(key, items)
      out.push({ kind: 'style', name: key, items })
    }
  }
  return out.map((e) => (e.kind === 'style' ? { ...e, items: sortVariants(e.items) } : e))
}

/** "32", "32 · Blue", or "Blue": how one item of a style is labelled. */
export function variantLabel(p: Pick<Product, 'size' | 'colour'>) {
  return [p.size, p.colour].filter(Boolean).join(' · ')
}

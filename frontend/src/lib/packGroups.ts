/**
 * Pack sizes of one item kept together when counting: "मैगी केचप 1 kg", "… 200 g", "… 500 g" become one heading,
 * "मैगी केचप", with a row per size. Picking an item with sizes from the common list names each "{name} {size}".
 */
const SIZE = /\s+((\d+(\.\d+)?\s*[x×]\s*)?\d+(\.\d+)?\s*(kg|g|gm|mg|l|ltr|litre|liter|ml|pcs?|pieces?|n|किलो|ग्राम|लीटर|मिली)|rs\.?\s*\d+|₹\s*\d+|\(\s*[^)]*\))$/i

/** The item's name without its pack size; the whole name when it has none. */
export function packStem(name: string): string {
  const stem = name.trim().replace(SIZE, '').trim()
  return stem.length >= 2 ? stem : name.trim()
}

/** The size part of a name ("1 kg"), given its stem. */
export const sizeOf = (name: string, stem: string) => name.trim().slice(stem.length).trim() || name.trim()

/** Items in order, with the sizes of one item next to each other under their stem (first appearance keeps its place). */
export function packGroups<T extends { name: string }>(items: T[]): { stem: string; items: T[] }[] {
  const groups = new Map<string, T[]>()
  for (const item of items) {
    const stem = packStem(item.name)
    groups.set(stem, [...(groups.get(stem) ?? []), item])
  }
  return [...groups.entries()].map(([stem, list]) => ({ stem, items: list }))
}

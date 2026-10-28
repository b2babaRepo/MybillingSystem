// A picture and colours for every grocery item and aisle, so tiles and cards are easy to spot at a glance and
// pleasant to look at. Emoji draw in colour on every phone, cost nothing to download and work offline; a
// shop's own photo replaces the emoji when there is one (lib/images.ts).

export interface ItemArt {
  emoji: string
  /** Gradient colours behind the picture. */
  from: string
  to: string
  /** A soft tint for photo tiles. */
  tint: string
}

/**
 * The aisles of the common category tree (server: Catalogue/CommonCatalogue.cs, Aisles), with their art, in
 * screen order. Names come from the server; this adds the picture and colours.
 */
export const AISLE_ART: Record<string, ItemArt> = {
  'atta-rice': { emoji: '🌾', from: '#fde68a', to: '#f59e0b', tint: '#fff7e0' },
  dal: { emoji: '🫘', from: '#fed7aa', to: '#ea580c', tint: '#fff1e6' },
  'oil-ghee': { emoji: '🫗', from: '#fef08a', to: '#eab308', tint: '#fefbe0' },
  masala: { emoji: '🌶️', from: '#fecaca', to: '#ef4444', tint: '#fff0f0' },
  'sugar-salt': { emoji: '🍬', from: '#fbcfe8', to: '#ec4899', tint: '#fff0f7' },
  'fruits-veg': { emoji: '🥕', from: '#d9f99d', to: '#65a30d', tint: '#f3fbe6' },
  dairy: { emoji: '🥛', from: '#bfdbfe', to: '#3b82f6', tint: '#eef5ff' },
  'tea-coffee': { emoji: '🍵', from: '#bbf7d0', to: '#16a34a', tint: '#ecfbf1' },
  breakfast: { emoji: '🍞', from: '#fde68a', to: '#d97706', tint: '#fff8e6' },
  snacks: { emoji: '🍪', from: '#fed7aa', to: '#f97316', tint: '#fff3e8' },
  instant: { emoji: '🍜', from: '#fecdd3', to: '#e11d48', tint: '#fff0f2' },
  drinks: { emoji: '🥤', from: '#a5f3fc', to: '#0891b2', tint: '#ebfbfe' },
  personal: { emoji: '🧴', from: '#e9d5ff', to: '#9333ea', tint: '#f7f0ff' },
  cleaning: { emoji: '🧼', from: '#bae6fd', to: '#0ea5e9', tint: '#edf8ff' },
  pooja: { emoji: '🪔', from: '#fed7aa', to: '#c2410c', tint: '#fff2e8' },
  household: { emoji: '💡', from: '#ddd6fe', to: '#7c3aed', tint: '#f4f1ff' },
}

const OTHER: ItemArt = { emoji: '🛒', from: '#99f6e4', to: '#0d9488', tint: '#ecfdf8' }

/**
 * One emoji per common-catalogue item, by its id (CommonCatalogue.cs: ids are row positions, rows are only
 * ever added at the end, so new ones go at the end here too).
 */
const BY_CATALOGUE_ID = [
  '', // ids start at 1
  '🌾', '🍚', '🍚', '🌾', '🥣', '🟡', // atta, rice, basmati, maida, suji, besan
  '🫘', '🫘', '🫘', '🫘', '🫘', '🫘', // toor, moong, chana, urad, masoor, rajma
  '🫛', '🍛', // kabuli chana, poha
  '🍬', '🟤', '🧂', // sugar, jaggery, salt
  '🫗', '🫗', '🥜', '🧈', '🧈', // mustard, refined, groundnut oil, ghee, vanaspati
  '🟡', '🌶️', '🌿', '🌱', '⚫', '🍛', '🧄', '⚫', '💚', '🌱', // haldi, mirch, dhaniya, jeera, rai, garam masala, hing, kali mirch, elaichi, methi
  '🍵', '☕', '🥛', '🥣', '🧀', '🧈', // tea, coffee, milk, curd, paneer, butter
  '🍞', '🍪', '🥨', '🍜', '🥚', // bread, biscuits, namkeen, maggi, eggs
  '🧼', '🧼', '🫧', '🧽', '🧴', '🪥', '🧴', // bath soap, washing soap, detergent, dishwash, phenyl, toothpaste, shampoo
  '🔥', '🪔', '🕯️', // matchbox, agarbatti, candles
  '🧅', '🥔', '🍅', '🍌', '🍋', // onion, potato, tomato, banana, lemon
  '🥤', '🧃', '💧', // cold drink, juice, water bottle
  '🥣', '🍓', '🍲', // cornflakes, jam, soup
  '🫙', '🪥', '🧴', // hair oil, toothbrush, face wash
  '🤍', '🪔', '🎋', // camphor, diya wicks, dhoop
  '💡', '🧹', // bulb, broom
]

/** The aisle of a common-list item, by id (same ranges as the server's CommonCatalogue.AisleOf). */
export function aisleOf(id: number | null | undefined): string | null {
  if (!id) return null
  if (id <= 6 || id === 14) return 'atta-rice'
  if (id <= 13) return 'dal'
  if (id <= 17) return 'sugar-salt'
  if (id <= 22) return 'oil-ghee'
  if (id <= 32) return 'masala'
  if (id <= 34) return 'tea-coffee'
  if (id <= 38 || id === 43) return 'dairy'
  if (id === 39) return 'breakfast'
  if (id <= 41) return 'snacks'
  if (id === 42) return 'instant'
  if (id === 44 || id === 49 || id === 50) return 'personal'
  if (id <= 48) return 'cleaning'
  if (id === 52) return 'pooja'
  if (id <= 53) return 'household'
  if (id <= 58) return 'fruits-veg'
  if (id <= 61) return 'drinks'
  if (id <= 63) return 'breakfast'
  if (id === 64) return 'instant'
  if (id <= 67) return 'personal'
  if (id <= 70) return 'pooja'
  return 'household'
}

/** Words in a shop's own item names that still say what it is ("Parle-G" is a biscuit). */
const BY_WORD: [RegExp, string][] = [
  [/biscuit|parle|cookie|बिस्कुट/i, '🍪'],
  [/milk|दूध/i, '🥛'],
  [/oil|तेल/i, '🫗'],
  [/soap|साबुन/i, '🧼'],
  [/egg|अंडे/i, '🥚'],
  [/rice|चावल/i, '🍚'],
  [/dal|दाल/i, '🫘'],
  [/atta|आटा/i, '🌾'],
  [/tea|चाय/i, '🍵'],
  [/sugar|चीनी/i, '🍬'],
  [/fruit|banana|apple|केला|सेब/i, '🍎'],
  [/onion|potato|tomato|प्याज|आलू|टमाटर|सब्ज़ी/i, '🥔'],
]

export function itemArt(item: { catalogueItemId?: number | null; aisle?: string | null; name?: string }): ItemArt {
  const colours = AISLE_ART[item.aisle ?? aisleOf(item.catalogueItemId) ?? ''] ?? OTHER
  const fromList = item.catalogueItemId ? BY_CATALOGUE_ID[item.catalogueItemId] : undefined
  const fromWord = item.name ? BY_WORD.find(([pattern]) => pattern.test(item.name!))?.[1] : undefined
  return { ...colours, emoji: fromList || fromWord || colours.emoji }
}

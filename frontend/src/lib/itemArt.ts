// A picture and colours for every grocery item, so tiles and cards are easy to spot at a glance and
// pleasant to look at. Emoji draw in colour on every phone, cost nothing to download and work offline.

export interface ItemArt {
  emoji: string
  /** Gradient colours behind the picture. */
  from: string
  to: string
}

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
]

/** Colours by the common list's categories, and a picture for items that aren't on it. */
const BY_CATEGORY: Record<string, ItemArt> = {
  'आटा, चावल, दाल': { emoji: '🌾', from: '#fde68a', to: '#f59e0b' },
  'चीनी, नमक, गुड़': { emoji: '🍬', from: '#fbcfe8', to: '#ec4899' },
  'तेल, घी': { emoji: '🫗', from: '#fef08a', to: '#eab308' },
  मसाले: { emoji: '🌶️', from: '#fecaca', to: '#ef4444' },
  'चाय, कॉफ़ी, दूध': { emoji: '🍵', from: '#bbf7d0', to: '#16a34a' },
  'बिस्कुट, नमकीन': { emoji: '🍪', from: '#fed7aa', to: '#f97316' },
  'साफ़-सफ़ाई': { emoji: '🧼', from: '#bae6fd', to: '#0ea5e9' },
  'घर का सामान': { emoji: '🪔', from: '#ddd6fe', to: '#8b5cf6' },
}

const OTHER: ItemArt = { emoji: '🛒', from: '#99f6e4', to: '#0d9488' }

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

/** The common list's category for an id (its rows run category by category). */
const CATEGORY_BY_ID: [number, string][] = [
  [14, 'आटा, चावल, दाल'], [17, 'चीनी, नमक, गुड़'], [22, 'तेल, घी'], [32, 'मसाले'],
  [38, 'चाय, कॉफ़ी, दूध'], [43, 'बिस्कुट, नमकीन'], [50, 'साफ़-सफ़ाई'], [53, 'घर का सामान'],
]

export function itemArt(item: { catalogueItemId?: number | null; category?: string | null; name?: string }): ItemArt {
  const listCategory = item.catalogueItemId ? CATEGORY_BY_ID.find(([last]) => item.catalogueItemId! <= last)?.[1] : undefined
  const colours = (listCategory && BY_CATEGORY[listCategory]) || (item.category && BY_CATEGORY[item.category]) || OTHER
  const fromList = item.catalogueItemId ? BY_CATALOGUE_ID[item.catalogueItemId] : undefined
  const fromWord = item.name ? BY_WORD.find(([pattern]) => pattern.test(item.name!))?.[1] : undefined
  return { ...colours, emoji: fromList || fromWord || colours.emoji }
}

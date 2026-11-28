import type { Product } from './types'

/**
 * The kirana category tree (reports/Category menu for item ordering.md): departments on the rail, category tiles
 * under them, and kind chips inside the large categories. Each node has a stable key and Hindi, English and
 * Gujarati names. An item finds its node from the master-list category it came with and words in its name, so a
 * merged group ("आटा, चावल, दाल") still splits into atta, rice and dal. Items no rule places go under "Other".
 */

export interface Names { hi: string; en: string; gu: string }
export interface Kind extends Names { key: string; kw: RegExp }
export interface Category extends Names {
  key: string
  icon: string
  tint: string
  ink: string
  /** Master-list categories this tile takes items from. */
  cats?: string[]
  /** Words in the name: with cats, only those items of the group; alone, any item whose category didn't place it. */
  kw?: RegExp
  kinds?: Kind[]
  /** The platform admin's picture for it (lib/menuConfig). */
  picture?: string
  /** Made by the platform admin: its words win over the built-in groups. */
  custom?: boolean
}
export interface Department extends Names { key: string; icon: string; categories: Category[]; picture?: string }

export const ICONS = {
  star: 'M12 3l2.6 5.3 5.9.9-4.3 4.1 1 5.8L12 16.4 6.8 19.1l1-5.8L3.5 9.2l5.9-.9z',
  sack: 'M8 4h8l-1 3c3 2 5 5 5 9a4 4 0 0 1-4 4H8a4 4 0 0 1-4-4c0-4 2-7 5-9z',
  jar: 'M8 3h8v3H8zM7 6h10v13a2 2 0 0 1-2 2H9a2 2 0 0 1-2-2z',
  cup: 'M4 8h13v5a5 5 0 0 1-5 5H9a5 5 0 0 1-5-5zM17 9h1.5a2.5 2.5 0 0 1 0 5H17M8 2v3M12 2v3',
  bowl: 'M3 11h18a9 9 0 0 1-18 0zM8 7c0-2 2-2 2-4M13 7c0-2 2-2 2-4',
  drop: 'M12 3c3 4 6 7 6 11a6 6 0 0 1-12 0c0-4 3-7 6-11z',
  spray: 'M9 8h6v13H9zM10 4h4v4h-4zM14 5h4',
  soap: 'M6 9h12a2 2 0 0 1 2 2v6a3 3 0 0 1-3 3H7a3 3 0 0 1-3-3v-6a2 2 0 0 1 2-2zM9 5a2 2 0 1 0 4 0',
  leaf: 'M5 19c0-8 6-14 14-14 0 8-6 14-14 14zM5 19l7-7',
  nut: 'M12 3c4 0 6 3 6 7 0 6-3 11-6 11s-6-5-6-11c0-4 2-7 6-7zM9 9c1 1 5 1 6 0',
  chilli: 'M6 20c8-1 13-7 13-14M17 4c1 0 2 1 2 2M6 20c-2 0-2-3 0-4 4-2 7-5 9-9',
  noodle: 'M3 11h18a9 9 0 0 1-18 0zM7 11c0-3 2-4 2-7M11 11c0-3 2-4 2-7M15 11c0-3 2-4 2-7',
  burger: 'M4 10a8 5 0 0 1 16 0zM4 13h16M5 16h14a3 3 0 0 1-3 3H8a3 3 0 0 1-3-3z',
  biscuit: 'M4 5h16v14H4zM8 9h.01M12 9h.01M16 9h.01M8 14h.01M12 14h.01M16 14h.01',
  milk: 'M9 2h6v3l2 3v12a2 2 0 0 1-2 2H9a2 2 0 0 1-2-2V8l2-3zM7 12h10',
  bottle: 'M10 2h4v4l2 3v11a2 2 0 0 1-2 2h-4a2 2 0 0 1-2-2V9l2-3z',
  /** A cold drink: a glass with a straw and ice. */
  soda: 'M6 8h12l-1.6 12.2a2 2 0 0 1-2 1.8H9.6a2 2 0 0 1-2-1.8zM13 8l2.5-6H19M9.5 12.5h2v2h-2zM12.5 15h2v2h-2z',
  tooth: 'M7 3c2 0 3 1 5 1s3-1 5-1c3 0 4 3 3 6l-2 11c-1 2-3 1-3-1l-1-4h-4l-1 4c0 2-2 3-3 1L4 9C3 6 4 3 7 3z',
  home: 'M3 11l9-7 9 7M5 10v10h14V10',
  lamp: 'M12 3c2 3 3 5 3 7a3 3 0 0 1-6 0c0-2 1-4 3-7zM6 21h12M8 17h8',
  baby: 'M12 4a4 4 0 1 1 0 8 4 4 0 0 1 0-8zM6 21c0-4 3-6 6-6s6 2 6 6',
  candy: 'M8 8h8v8H8zM8 8L4 4M16 16l4 4M4 4l-1 4M4 4l4-1M20 20l1-4M20 20l-4 1',
  box: 'M3 7l9-4 9 4v10l-9 4-9-4zM3 7l9 4 9-4M12 11v10',
} as const

const k = (key: string, hi: string, en: string, gu: string, kw: RegExp): Kind => ({ key, hi, en, gu, kw })

export const KIRANA: Department[] = [
  { key: 'staples', hi: 'आटा, दाल', en: 'Atta & dal', gu: 'લોટ, દાળ', icon: 'sack', categories: [
    { key: 'dal', hi: 'दाल', en: 'Dal', gu: 'દાળ', icon: 'sack', tint: '#f4f0d9', ink: '#6b5a12', cats: ['आटा, चावल, दाल'],
      kw: /\bdal\b|daal|दाल|rajma|राजमा|chana|चना|chhole|छोले|moong|मूंग|urad|उड़द|masoor|मसूर|toor|tuar|arhar|तूर|अरहर|lobia|kulthi|matar|मटर|futana|फुटाना|लोबिया|कुल्थी/i,
      kinds: [k('toor', 'तूर', 'Toor', 'તુવેર', /toor|tuar|arhar|तूर|अरहर/i), k('moong', 'मूंग', 'Moong', 'મગ', /moong|मूंग/i),
        k('chana', 'चना', 'Chana', 'ચણા', /chana|चना|chhole|छोले|kabuli|काबुली|futana|फुटाना/i), k('urad', 'उड़द', 'Urad', 'અડદ', /urad|उड़द/i),
        k('masoor', 'मसूर', 'Masoor', 'મસૂર', /masoor|मसूर/i), k('rajma', 'राजमा', 'Rajma', 'રાજમા', /rajma|राजमा|lobia/i)] },
    { key: 'rice', hi: 'चावल, पोहा', en: 'Rice & poha', gu: 'ચોખા, પૌંઆ', icon: 'bowl', tint: '#eef2e3', ink: '#4f5f1f', cats: ['आटा, चावल, दाल'], kw: /rice|चावल|basmati|बासमती|poha|पोहा|murmura|मुरमुरा|kolam|कोलम|sona masoori|सोना मसूरी|daawat|दावत|rozana|रोज़ाना|mogra|मोगरा|ponni/i },
    { key: 'atta', hi: 'आटा, बेसन', en: 'Atta & flours', gu: 'લોટ, બેસન', icon: 'sack', tint: '#f6efe2', ink: '#7a5418', cats: ['आटा, चावल, दाल'] },
    { key: 'oil', hi: 'तेल, घी', en: 'Oil & ghee', gu: 'તેલ, ઘી', icon: 'drop', tint: '#fff5d9', ink: '#8a6400', cats: ['तेल, घी'],
      kinds: [k('mustard', 'सरसों', 'Mustard', 'સરસવ', /mustard|सरसों|sarson/i), k('refined', 'रिफ़ाइंड', 'Refined', 'રિફાઇન્ડ', /refined|soya|sunflower|rice bran|groundnut|palm/i),
        k('ghee', 'घी', 'Ghee', 'ઘી', /ghee|घी|vanaspati|dalda/i)] },
    { key: 'sugar', hi: 'चीनी, नमक, गुड़', en: 'Sugar, salt & jaggery', gu: 'ખાંડ, મીઠું, ગોળ', icon: 'jar', tint: '#f1f1f1', ink: '#4a4a4a', cats: ['चीनी, नमक, गुड़'] },
  ] },
  { key: 'masala', hi: 'मसाले, मेवे', en: 'Masala & dry fruits', gu: 'મસાલા, સૂકો મેવો', icon: 'jar', categories: [
    { key: 'masala', hi: 'मसाले', en: 'Masala', gu: 'મસાલા', icon: 'chilli', tint: '#fde4e1', ink: '#a1261b', cats: ['मसाले'],
      kinds: [k('mix', 'मिक्स मसाले', 'Blends', 'મિક્સ મસાલા', /garam|chhole|chole|biryani|sabzi|kitchen king|pav bhaji|chaat|sambar|meat|chicken|mix/i),
        k('powder', 'पिसे मसाले', 'Powders', 'પીસેલા મસાલા', /powder|पाउडर|pisi|ground/i), k('whole', 'खड़े मसाले', 'Whole', 'આખા મસાલા', /sabut|whole|jeera|saunf|ajwain|methi|rai|elaichi|laung|dalchini|tej patta|kali mirch/i)] },
    { key: 'dryfruit', hi: 'सूखे मेवे', en: 'Dry fruits', gu: 'સૂકો મેવો', icon: 'nut', tint: '#f3ecdf', ink: '#6b4f1f', cats: ['सूखे मेवे', 'पिस्ता'], kw: /kaju|badam|almond|cashew|kishmish|raisin|akhrot|walnut|pista|makhana|khajoor|dates|anjeer/i,
      kinds: [k('kaju', 'काजू', 'Cashew', 'કાજુ', /kaju|cashew|काजू/i), k('badam', 'बादाम', 'Almond', 'બદામ', /badam|almond|बादाम/i),
        k('kishmish', 'किशमिश', 'Raisins', 'કિસમિસ', /kishmish|raisin|munakka|किशमिश/i), k('makhana', 'मखाना', 'Makhana', 'મખાણા', /makhana|मखाना/i)] },
    { key: 'pickle', hi: 'अचार, सॉस, पापड़', en: 'Pickles, sauces & papad', gu: 'અથાણું, સોસ, પાપડ', icon: 'jar', tint: '#e9f3e1', ink: '#3f6420', cats: ['अचार, सॉस, पापड़'] },
  ] },
  { key: 'tea', hi: 'चाय, पेय', en: 'Tea & drinks', gu: 'ચા, પીણાં', icon: 'cup', categories: [
    { key: 'coffee', hi: 'कॉफ़ी', en: 'Coffee', gu: 'કોફી', icon: 'cup', tint: '#f3e9e2', ink: '#6b3f1f', cats: ['चाय, कॉफ़ी, दूध'], kw: /coffee|कॉफ़ी|nescafe|bru\b/i },
    { key: 'health', hi: 'हेल्थ ड्रिंक', en: 'Health drinks', gu: 'હેલ્થ ડ્રિંક', icon: 'milk', tint: '#e8eefc', ink: '#2b4a9a', cats: ['चाय, कॉफ़ी, दूध'], kw: /horlicks|bournvita|boost|complan|protinex|milk powder|everyday|amulya/i },
    { key: 'tea', hi: 'चाय पत्ती', en: 'Tea', gu: 'ચા', icon: 'cup', tint: '#fdf0e1', ink: '#9a5300', cats: ['चाय, कॉफ़ी, दूध'],
      kinds: [k('green', 'ग्रीन टी', 'Green tea', 'ગ્રીન ટી', /green|herbal|tulsi/i), k('bags', 'टी बैग', 'Tea bags', 'ટી બેગ', /bag/i)] },
    { key: 'drinks', hi: 'कोल्ड ड्रिंक, जूस', en: 'Cold drinks & juices', gu: 'ઠંડા પીણાં, જ્યુસ', icon: 'soda', tint: '#e3f1f6', ink: '#1f5a6b', cats: ['पेय'] },
  ] },
  { key: 'snacks', hi: 'नाश्ता, बिस्कुट', en: 'Snacks & biscuits', gu: 'નાસ્તો, બિસ્કિટ', icon: 'biscuit', categories: [
    { key: 'noodles', hi: 'नूडल्स, पास्ता', en: 'Noodles & pasta', gu: 'નૂડલ્સ, પાસ્તા', icon: 'noodle', tint: '#fff3d6', ink: '#8a5a00', cats: ['इंस्टेंट, नाश्ता'], kw: /noodle|नूडल|maggi|मैगी|yippee|pasta|macaroni|vermicelli|seviyan|सेवइयां|hakka/i },
    { key: 'ready', hi: 'रेडी मिक्स, सूप', en: 'Ready mixes & soup', gu: 'રેડી મિક્સ, સૂપ', icon: 'bowl', tint: '#fde8e4', ink: '#a23a22', cats: ['इंस्टेंट, नाश्ता'] },
    { key: 'biscuits', hi: 'बिस्कुट, नमकीन', en: 'Biscuits & namkeen', gu: 'બિસ્કિટ, નમકીન', icon: 'biscuit', tint: '#f6ecdc', ink: '#7a4f18', cats: ['बिस्कुट, नमकीन'],
      kinds: [k('namkeen', 'नमकीन', 'Namkeen', 'નમકીન', /namkeen|नमकीन|bhujia|भुजिया|chips|kurkure|lays|mixture|moong dal|aloo/i), k('biscuit', 'बिस्कुट', 'Biscuits', 'બિસ્કિટ', /biscuit|बिस्कुट|cookie|parle|britannia|sunfeast|marie|good day|bourbon/i)] },
    { key: 'choco', hi: 'चॉकलेट, टॉफ़ी', en: 'Chocolates & toffee', gu: 'ચોકલેટ, ટોફી', icon: 'candy', tint: '#f1e4f3', ink: '#6b2a74', cats: ['चॉकलेट, टॉफ़ी'] },
  ] },
  { key: 'dairy', hi: 'दूध, ब्रेड', en: 'Dairy & bread', gu: 'દૂધ, બ્રેડ', icon: 'milk', categories: [
    { key: 'dairy', hi: 'डेयरी, ब्रेड', en: 'Dairy, bread & rusk', gu: 'ડેરી, બ્રેડ', icon: 'milk', tint: '#e8f1fb', ink: '#2a5486', cats: ['डेयरी, ब्रेड'] },
  ] },
  { key: 'home', hi: 'साफ़-सफ़ाई', en: 'Home & care', gu: 'સાફ-સફાઈ', icon: 'spray', categories: [
    { key: 'personal', hi: 'पर्सनल केयर', en: 'Personal care', gu: 'પર્સનલ કેર', icon: 'soap', tint: '#fbe7ef', ink: '#8a2650', cats: ['पर्सनल केयर'],
      kinds: [k('soap', 'साबुन', 'Soap', 'સાબુ', /soap|साबुन|lux|lifebuoy|dettol|santoor|cinthol|dove|pears|godrej no/i), k('shampoo', 'शैम्पू', 'Shampoo', 'શેમ્પૂ', /shampoo|शैम्पू|clinic|sunsilk|head & shoulders|dove hair/i),
        k('hairoil', 'बालों का तेल', 'Hair oil', 'વાળનું તેલ', /hair oil|coconut oil|parachute|amla|navratna|almond oil|jasmine oil/i), k('cream', 'क्रीम', 'Creams', 'ક્રીમ', /cream|lotion|vaseline|boroline|fair|ponds/i)] },
    { key: 'oral', hi: 'दांतों की देखभाल', en: 'Oral care', gu: 'દાંતની સંભાળ', icon: 'tooth', tint: '#e3f3f6', ink: '#1f6270', cats: ['दांतों की देखभाल'] },
    { key: 'cleaning', hi: 'साफ़-सफ़ाई', en: 'Cleaning', gu: 'સાફ-સફાઈ', icon: 'spray', tint: '#e6f4ea', ink: '#2f6b3a', cats: ['साफ़-सफ़ाई'],
      kinds: [k('detergent', 'डिटर्जेंट', 'Detergent', 'ડિટર્જન્ટ', /surf|tide|ariel|wheel|rin|ghadi|nirma|detergent|washing|powder|bar/i), k('dish', 'बर्तन', 'Dishwash', 'વાસણ', /vim|pril|exo|dish|bartan|scrub/i),
        k('floor', 'फ़र्श, टॉयलेट', 'Floor & toilet', 'ફ્લોર, ટોઇલેટ', /lizol|harpic|phenyl|floor|toilet|domex|colin/i)] },
    { key: 'household', hi: 'घर का सामान', en: 'Household', gu: 'ઘરનો સામાન', icon: 'home', tint: '#eeeef6', ink: '#3f3f74', cats: ['घर का सामान'] },
  ] },
  { key: 'pooja', hi: 'पूजा, बच्चे', en: 'Pooja & baby', gu: 'પૂજા, બાળકો', icon: 'lamp', categories: [
    { key: 'pooja', hi: 'पूजा का सामान', en: 'Pooja items', gu: 'પૂજા સામગ્રી', icon: 'lamp', tint: '#fff0dc', ink: '#9a4f00', cats: ['पूजा का सामान'] },
    { key: 'baby', hi: 'बेबी केयर', en: 'Baby care', gu: 'બેબી કેર', icon: 'baby', tint: '#fbeef2', ink: '#8a3a56', cats: ['बेबी केयर'] },
  ] },
]

const OTHER: Category = { key: 'other', hi: 'बाकी सामान', en: 'Other', gu: 'બાકી સામાન', icon: 'box', tint: '#eef1f0', ink: '#4a5654' }

const text = (p: Pick<Product, 'name' | 'nameEn' | 'nameHi'>) => [p.name, p.nameEn, p.nameHi].filter(Boolean).join(' ')

/** The tile an item belongs on: its master category first (split by name words), then name words alone, else Other. */
export function categoryOf(p: Pick<Product, 'name' | 'nameEn' | 'nameHi' | 'category'>, tree: Department[] = KIRANA): Category {
  const all = tree.flatMap((d) => d.categories)
  const words = text(p)
  // A category the platform admin added takes the items its words name, before the built-in groups do.
  const custom = all.find((c) => c.custom && c.kw?.test(words))
  if (custom) return custom
  const cat = p.category?.trim()
  if (cat) {
    const found = all.find((c) => c.cats?.includes(cat) && (!c.kw || c.kw.test(words)))
    if (found) return found
  }
  return all.find((c) => c.kw?.test(words)) ?? OTHER
}

/** The kind chip an item falls under inside its category, or null. */
export const kindOf = (p: Pick<Product, 'name' | 'nameEn' | 'nameHi'>, c: Category) => c.kinds?.find((kd) => kd.kw.test(text(p)))?.key ?? null

export const nameIn = (n: Names, language: string) => (language === 'hi' ? n.hi : language === 'gu' ? n.gu : n.en)
/** The smaller second line: English under Hindi or Gujarati, Hindi under English. */
export const subName = (n: Names, language: string) => (language === 'en' ? n.hi : n.en)

export interface MenuCategory { category: Category; items: Product[] }
export interface MenuDepartment { department: Department; categories: MenuCategory[] }

/**
 * The menu for this shop: only departments and tiles that hold its items, each tile's items most-sold first. A shop
 * whose items mostly fall outside the kirana tree (another trade) gets one tile per category it uses instead.
 */
export function buildMenu(products: Product[], rank: Map<string, number>, tree: Department[] = KIRANA): MenuDepartment[] {
  const sorted = [...products].sort((a, b) => (rank.get(a.id) ?? 1e9) - (rank.get(b.id) ?? 1e9) || a.name.localeCompare(b.name))
  const placed = sorted.map((p) => ({ p, c: categoryOf(p, tree) }))
  const kirana = placed.filter((x) => x.c !== OTHER).length >= placed.length / 2
  if (!kirana) {
    const byCat = new Map<string, Product[]>()
    for (const p of sorted) {
      const name = p.category?.trim() || OTHER.en
      byCat.set(name, [...(byCat.get(name) ?? []), p])
    }
    const categories = [...byCat.entries()].sort(([a], [b]) => a.localeCompare(b)).map(([name, items], i): MenuCategory => ({
      category: { ...OTHER, key: `cat:${name}`, hi: name, en: name, gu: name, tint: TINTS[i % TINTS.length][0], ink: TINTS[i % TINTS.length][1] }, items,
    }))
    return [{ department: { key: 'all', hi: 'सब सामान', en: 'All items', gu: 'બધો સામાન', icon: 'box', categories: [] }, categories }]
  }

  const menu: MenuDepartment[] = []
  for (const d of tree) {
    const categories = d.categories.map((c) => ({ category: c, items: placed.filter((x) => x.c === c).map((x) => x.p) })).filter((c) => c.items.length > 0)
    if (categories.length > 0) menu.push({ department: d, categories })
  }
  const rest = placed.filter((x) => x.c === OTHER).map((x) => x.p)
  if (rest.length > 0) menu.push({ department: { key: 'other', hi: OTHER.hi, en: OTHER.en, gu: OTHER.gu, icon: 'box', categories: [] }, categories: [{ category: OTHER, items: rest }] })
  return menu
}

const TINTS = [['#fdf0e1', '#9a5300'], ['#e6f4ea', '#2f6b3a'], ['#e8eefc', '#2b4a9a'], ['#fde4e1', '#a1261b'], ['#f3ecdf', '#6b4f1f'], ['#f1e4f3', '#6b2a74']]

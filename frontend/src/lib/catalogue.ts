import type { Unit } from './types'

export interface ImportRow {
  name: string
  unit: Unit
  sellingPrice: number | null
  costPrice: number | null
  category: string | null
  openingStock: number | null
  reorderLevel: number | null
}

/**
 * Everyday kirana items to start from: names, units and categories only. Prices differ by town and brand,
 * so the shop fills them in; until then an item shows "No price" and can still be billed at any price.
 */
export const STARTER_CATALOGUE: ImportRow[] = (
  [
    ['आटा', 'kg', 'आटा, चावल, दाल'], ['चावल', 'kg', 'आटा, चावल, दाल'], ['बासमती चावल', 'kg', 'आटा, चावल, दाल'],
    ['मैदा', 'kg', 'आटा, चावल, दाल'], ['सूजी', 'kg', 'आटा, चावल, दाल'], ['बेसन', 'kg', 'आटा, चावल, दाल'],
    ['तूर दाल', 'kg', 'आटा, चावल, दाल'], ['मूंग दाल', 'kg', 'आटा, चावल, दाल'], ['चना दाल', 'kg', 'आटा, चावल, दाल'],
    ['उड़द दाल', 'kg', 'आटा, चावल, दाल'], ['मसूर दाल', 'kg', 'आटा, चावल, दाल'], ['राजमा', 'kg', 'आटा, चावल, दाल'],
    ['काबुली चना', 'kg', 'आटा, चावल, दाल'], ['पोहा', 'kg', 'आटा, चावल, दाल'],
    ['चीनी', 'kg', 'चीनी, नमक, गुड़'], ['गुड़', 'kg', 'चीनी, नमक, गुड़'], ['नमक', 'packet', 'चीनी, नमक, गुड़'],
    ['सरसों तेल', 'litre', 'तेल, घी'], ['रिफ़ाइंड तेल', 'litre', 'तेल, घी'], ['मूंगफली तेल', 'litre', 'तेल, घी'],
    ['देसी घी', 'litre', 'तेल, घी'], ['वनस्पति', 'kg', 'तेल, घी'],
    ['हल्दी पाउडर', 'packet', 'मसाले'], ['लाल मिर्च पाउडर', 'packet', 'मसाले'], ['धनिया पाउडर', 'packet', 'मसाले'],
    ['जीरा', 'g', 'मसाले'], ['राई', 'g', 'मसाले'], ['गरम मसाला', 'packet', 'मसाले'], ['हींग', 'packet', 'मसाले'],
    ['काली मिर्च', 'g', 'मसाले'], ['इलायची', 'g', 'मसाले'], ['मेथी दाना', 'g', 'मसाले'],
    ['चायपत्ती', 'packet', 'चाय, कॉफ़ी, दूध'], ['कॉफ़ी', 'packet', 'चाय, कॉफ़ी, दूध'], ['दूध', 'litre', 'चाय, कॉफ़ी, दूध'],
    ['दही', 'kg', 'चाय, कॉफ़ी, दूध'], ['पनीर', 'g', 'चाय, कॉफ़ी, दूध'], ['मक्खन', 'packet', 'चाय, कॉफ़ी, दूध'],
    ['ब्रेड', 'packet', 'बिस्कुट, नमकीन'], ['बिस्कुट', 'packet', 'बिस्कुट, नमकीन'], ['नमकीन', 'packet', 'बिस्कुट, नमकीन'],
    ['मैगी', 'packet', 'बिस्कुट, नमकीन'], ['अंडे', 'piece', 'बिस्कुट, नमकीन'],
    ['नहाने का साबुन', 'piece', 'साफ़-सफ़ाई'], ['कपड़े धोने का साबुन', 'piece', 'साफ़-सफ़ाई'], ['सर्फ़ / डिटर्जेंट', 'kg', 'साफ़-सफ़ाई'],
    ['बर्तन धोने का साबुन', 'piece', 'साफ़-सफ़ाई'], ['फ़िनाइल', 'litre', 'साफ़-सफ़ाई'], ['टूथपेस्ट', 'piece', 'साफ़-सफ़ाई'],
    ['शैम्पू', 'packet', 'साफ़-सफ़ाई'], ['माचिस', 'packet', 'घर का सामान'], ['अगरबत्ती', 'packet', 'घर का सामान'],
    ['मोमबत्ती', 'packet', 'घर का सामान'],
  ] as [string, Unit, string][]
).map(([name, unit, category]) => ({ name, unit, category, sellingPrice: null, costPrice: null, openingStock: null, reorderLevel: null }))

/** Spellings shopkeepers use for units, in English and Hindi. */
const UNIT_WORDS: Record<string, Unit> = {
  piece: 'piece', pc: 'piece', pcs: 'piece', nos: 'piece', no: 'piece', पीस: 'piece', नग: 'piece',
  kg: 'kg', kgs: 'kg', kilo: 'kg', किलो: 'kg',
  g: 'g', gm: 'g', gms: 'g', gram: 'g', grams: 'g', ग्राम: 'g',
  l: 'litre', ltr: 'litre', litre: 'litre', liter: 'litre', लीटर: 'litre',
  ml: 'ml', मिली: 'ml',
  packet: 'packet', pkt: 'packet', pack: 'packet', पैकेट: 'packet',
}

/** Splits one CSV line, honouring "quoted, values". Tabs (pasted from Excel) count as separators too. */
function splitLine(line: string): string[] {
  const cells: string[] = []
  let cell = ''
  let quoted = false
  for (let i = 0; i < line.length; i++) {
    const c = line[i]
    if (c === '"' && quoted && line[i + 1] === '"') {
      cell += '"'
      i++
    } else if (c === '"') {
      quoted = !quoted
    } else if ((c === ',' || c === '\t') && !quoted) {
      cells.push(cell.trim())
      cell = ''
    } else {
      cell += c
    }
  }
  cells.push(cell.trim())
  return cells
}

const toNumber = (text: string | undefined) => {
  if (!text) return null
  const value = Number(text.replace(/[₹,\s]/g, ''))
  return Number.isFinite(value) ? value : null
}

/**
 * Reads pasted or uploaded rows: name, unit, price, cost, category, stock, reorder level (only name and unit
 * are needed). A first row whose unit cell isn't a unit is taken as a header and skipped.
 */
export function parseCsv(text: string): { rows: ImportRow[]; errors: { row: number; unit: string }[] } {
  const lines = text.split(/\r?\n/).filter((l) => l.trim().length > 0)
  const rows: ImportRow[] = []
  const errors: { row: number; unit: string }[] = []
  lines.forEach((line, index) => {
    const [name, unitText = '', price, cost, category, stock, reorder] = splitLine(line)
    const unit = UNIT_WORDS[unitText.toLowerCase()]
    if (!unit) {
      if (index > 0 || rows.length > 0) errors.push({ row: index + 1, unit: unitText })
      return
    }
    if (!name) return
    rows.push({
      name,
      unit,
      sellingPrice: toNumber(price),
      costPrice: toNumber(cost),
      category: category || null,
      openingStock: toNumber(stock),
      reorderLevel: toNumber(reorder),
    })
  })
  return { rows, errors }
}

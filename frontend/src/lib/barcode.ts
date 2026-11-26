// EAN-13 bars for printed tags: pack barcodes (13 digits, or 12-digit UPC with a 0 in front) and the shop's own codes
// (2…, made by the server with their check digit). Each digit is 7 modules; guards 101 / 01010 / 101 frame them.

const L = ['0001101', '0011001', '0010011', '0111101', '0100011', '0110001', '0101111', '0111011', '0110111', '0001011']
const G = ['0100111', '0110011', '0011011', '0100001', '0011101', '0111001', '0000101', '0010001', '0001001', '0010111']
const R = ['1110010', '1100110', '1101100', '1000010', '1011100', '1001110', '1010000', '1000100', '1001000', '1110100']
const PARITY = ['LLLLLL', 'LLGLGG', 'LLGGLG', 'LLGGGL', 'LGLLGG', 'LGGLLG', 'LGGGLL', 'LGLGLG', 'LGLGGL', 'LGGLGL']

/** The 13 digits to draw, or null when the code isn't an EAN-13 / UPC-A (then the tag prints the code as text). */
export function ean13Digits(code: string | null | undefined): string | null {
  const digits = (code ?? '').replace(/\D/g, '')
  if (digits.length === 13) return digits
  if (digits.length === 12) return `0${digits}`
  return null
}

/** The bar pattern as 95 modules of '1' (bar) and '0' (space). */
export function ean13Modules(digits13: string): string {
  const d = digits13.split('').map(Number)
  const parity = PARITY[d[0]]
  let bits = '101'
  for (let i = 1; i <= 6; i++) bits += (parity[i - 1] === 'L' ? L : G)[d[i]]
  bits += '01010'
  for (let i = 7; i <= 12; i++) bits += R[d[i]]
  return bits + '101'
}

// Code 128 for codes that aren't EAN-13 ("SHIRT40", a supplier's own codes): each symbol is 3 bars and 3 spaces, 11
// modules in all, written as their widths; the stop symbol has a 4th bar (13 modules).
const C128 = [
  '212222', '222122', '222221', '121223', '121322', '131222', '122213', '122312', '132212', '221213',
  '221312', '231212', '112232', '122132', '122231', '113222', '123122', '123221', '223211', '221132',
  '221231', '213212', '223112', '312131', '311222', '321122', '321221', '312212', '322112', '322211',
  '212123', '212321', '232121', '111323', '131123', '131321', '112313', '132113', '132311', '211313',
  '231113', '231311', '112133', '112331', '132131', '113123', '113321', '133121', '313121', '211331',
  '231131', '213113', '213311', '213131', '311123', '311321', '331121', '312113', '312311', '332111',
  '314111', '221411', '431111', '111224', '111422', '121124', '121421', '141122', '141221', '112214',
  '112412', '122114', '122411', '142112', '142211', '241211', '221114', '413111', '241112', '134111',
  '111242', '121142', '121241', '114212', '124112', '124211', '411212', '421112', '421211', '212141',
  '214121', '412121', '111143', '111341', '131141', '114113', '114311', '411113', '411311', '113141',
  '114131', '311141', '411131', '211412', '211214', '211232', '2331112',
]
export const CODE128_PATTERNS = C128

/**
 * Code 128 (set B: printable ASCII) as modules of '1' and '0': start, the characters, the check symbol, stop.
 * Null when the text has a character set B can't carry.
 */
export function code128Modules(text: string): string | null {
  const values = [...text].map((c) => c.charCodeAt(0) - 32)
  if (values.length === 0 || values.some((v) => v < 0 || v > 94)) return null
  const check = (104 + values.reduce((sum, v, i) => sum + v * (i + 1), 0)) % 103
  return [104, ...values, check, 106].map((v) => C128[v].split('').map((w, i) => (i % 2 === 0 ? '1' : '0').repeat(Number(w))).join('')).join('')
}

/**
 * One spelling per barcode, as the server keeps it: letters and digits only, and a 12-digit UPC-A with its leading 0,
 * the 13-digit form cameras often report. So a pack saved one way still matches when scanned the other.
 */
export function normBarcode(code: string | null | undefined) {
  const clean = (code ?? '').replace(/[^0-9A-Za-z]/g, '')
  return /^\d{12}$/.test(clean) ? `0${clean}` : clean
}

/** A camera can be used here (a phone or laptop with a camera API). On a page that isn't https the scanner says so. */
export const cameraPossible = () => typeof navigator !== 'undefined' && !!navigator.mediaDevices?.getUserMedia

/**
 * A weighing scale's label barcode, read with the shop's label layout (13 characters, like 21PPPPPWWWWWC): fixed digits
 * and x (any digit), P the item's code on the scale, W the weight in grams or R the price in paise, C the EAN check digit.
 * Null when the code isn't a label of that layout or its check digit is wrong (a misread).
 */
export function readScaleLabel(code: string, layout: string | null | undefined): { itemCode: string; kg?: number; price?: number } | null {
  if (!layout || layout.length !== 13 || !/^\d{13}$/.test(code)) return null
  let itemCode = ''
  let value = ''
  for (let i = 0; i < 13; i++) {
    const want = layout[i]
    const got = code[i]
    if (want === 'P') itemCode += got
    else if (want === 'W' || want === 'R') value += got
    else if (/\d/.test(want) && want !== got) return null
  }
  const sum = [...code.slice(0, 12)].reduce((s, d, i) => s + Number(d) * (i % 2 ? 3 : 1), 0)
  if ((10 - (sum % 10)) % 10 !== Number(code[12]) || !itemCode || !value) return null
  return layout.includes('W') ? { itemCode, kg: Number(value) / 1000 } : { itemCode, price: Number(value) / 100 }
}

/** The same item code on the scale and in the app, with or without leading zeros (00123 is 123). */
export const sameItemCode = (a: string | null | undefined, b: string) => !!a && /^\d{1,6}$/.test(a) && Number(a) === Number(b)

/** The item a scanned code is: its main barcode or one of its extra ones. Codes shorter than 4 never match. */
export function itemForBarcode<T extends { barcode?: string | null; extraBarcodes?: string[] | null }>(products: T[], code: string): T | undefined {
  const clean = normBarcode(code)
  if (clean.length < 4) return undefined
  return products.find((p) => (!!p.barcode && normBarcode(p.barcode) === clean) || !!p.extraBarcodes?.some((c) => normBarcode(c) === clean))
}

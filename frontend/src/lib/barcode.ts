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

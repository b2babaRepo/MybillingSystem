/**
 * An amount in words the way Indian bills write it, in lakhs and crores: 125050.5 →
 * "Rupees One Lakh Twenty Five Thousand Fifty and Fifty Paise Only". English, as on GST invoices whatever the bill's language.
 */
const ONES = ['', 'One', 'Two', 'Three', 'Four', 'Five', 'Six', 'Seven', 'Eight', 'Nine', 'Ten', 'Eleven', 'Twelve', 'Thirteen', 'Fourteen',
  'Fifteen', 'Sixteen', 'Seventeen', 'Eighteen', 'Nineteen']
const TENS = ['', '', 'Twenty', 'Thirty', 'Forty', 'Fifty', 'Sixty', 'Seventy', 'Eighty', 'Ninety']

/** 0-99 in words ('' for 0). */
function two(n: number): string {
  return n < 20 ? ONES[n] : `${TENS[Math.floor(n / 10)]}${n % 10 ? ` ${ONES[n % 10]}` : ''}`
}

/** 0-999 in words. */
function three(n: number): string {
  const hundred = Math.floor(n / 100)
  return [hundred ? `${ONES[hundred]} Hundred` : '', two(n % 100)].filter(Boolean).join(' ')
}

/** A whole number in the Indian system (crore, lakh, thousand, hundred). */
export function wholeInWords(n: number): string {
  if (n === 0) return 'Zero'
  const crore = Math.floor(n / 10_000_000)
  const lakh = Math.floor((n % 10_000_000) / 100_000)
  const thousand = Math.floor((n % 100_000) / 1000)
  const rest = n % 1000
  return [crore ? `${crore >= 100 ? wholeInWords(crore) : two(crore)} Crore` : '', lakh ? `${two(lakh)} Lakh` : '', thousand ? `${two(thousand)} Thousand` : '', three(rest)]
    .filter(Boolean).join(' ')
}

export function rupeesInWords(amount: number): string {
  const paiseTotal = Math.round(Math.abs(amount) * 100)
  const rupees = Math.floor(paiseTotal / 100)
  const paise = paiseTotal % 100
  return `${amount < 0 ? 'Minus ' : ''}Rupees ${wholeInWords(rupees)}${paise ? ` and ${two(paise)} Paise` : ''} Only`
}

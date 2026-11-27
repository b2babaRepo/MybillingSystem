/**
 * An Indian mobile number as the server accepts it (Common/PhoneNumber.cs): 10 digits starting 6–9, optionally with
 * a leading 0 or 91, or any + number of 8–15 digits. Spaces, dashes and brackets are allowed. Returns null when the
 * server would refuse it, so a form can say so before saving instead of the save failing later in sync.
 */
export function validPhone(input: string): string | null {
  const text = input.trim()
  if (!text || /[^\d\s\-()+]/.test(text)) return null
  let digits = text.replace(/\D/g, '')
  if (text.startsWith('+')) return digits.length >= 8 && digits.length <= 15 && digits[0] !== '0' ? `+${digits}` : null
  if (digits.length === 11 && digits[0] === '0') digits = digits.slice(1)
  if (digits.length === 10 && /^[6-9]/.test(digits)) return `+91${digits}`
  if (digits.length === 12 && digits.startsWith('91') && /^[6-9]/.test(digits.slice(2))) return `+${digits}`
  return null
}

/** Empty is fine (the number is optional); anything typed must be a valid number. */
export const phoneOk = (input: string) => !input.trim() || validPhone(input) !== null

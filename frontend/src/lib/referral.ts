/**
 * A shop's invite link carries its referral code: apnakaarobar.com/#/?ref=sharma-store (or ?ref= before the #).
 * The code is kept on this phone until the new shop signs up, so it survives moving between pages.
 */
const KEY = 'gk-referral'

const clean = (code: string | null | undefined) => (code ?? '').trim().toLowerCase().replace(/[^a-z0-9-]/g, '').slice(0, 60)

/** The code from the address, if any (remembered), else the one remembered before. */
export function referralFromLink(): string {
  if (typeof window === 'undefined') return ''
  const fromSearch = new URLSearchParams(window.location.search).get('ref')
  const hashQuery = window.location.hash.includes('?') ? window.location.hash.slice(window.location.hash.indexOf('?') + 1) : ''
  const fromHash = new URLSearchParams(hashQuery).get('ref')
  const code = clean(fromSearch ?? fromHash)
  try {
    if (code) localStorage.setItem(KEY, code)
    return code || clean(localStorage.getItem(KEY))
  } catch {
    return code
  }
}

export function forgetReferral() {
  try { localStorage.removeItem(KEY) } catch { /* storage off */ }
}

/** The link a shop shares: opens sign-up with its code filled in. */
export const inviteLink = (code: string) => `${window.location.origin}/#/?ref=${encodeURIComponent(code)}`

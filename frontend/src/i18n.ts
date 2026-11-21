import { getSession, setSession } from './lib/session'
import { useEffect } from 'react'
import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'

export type Language = 'en' | 'hi' | 'gu'

/** The languages offered, in the order the language button steps through them, each named in itself. */
export const LANGUAGES: { code: Language; name: string }[] = [
  { code: 'en', name: 'English' },
  { code: 'hi', name: 'हिंदी' },
  { code: 'gu', name: 'ગુજરાતી' },
]

const SHORT: Record<Language, string> = { en: 'EN', hi: 'हि', gu: 'ગુ' }

/** The language after this one, for a one-tap language button. */
export function nextLanguage(current: string): { code: Language; short: string } {
  const at = Math.max(0, LANGUAGES.findIndex((l) => l.code === current))
  const code = LANGUAGES[(at + 1) % LANGUAGES.length].code
  return { code, short: SHORT[code] }
}

/** Locale for dates and numbers in the shown language. */
export const localeOf = (language: string) => (language === 'hi' ? 'hi-IN' : language === 'gu' ? 'gu-IN' : 'en-IN')

/** The locale for dates in the language being shown: Hindi, Gujarati or English month names. */
export const shownLocale = () => localeOf(i18n.language ?? 'en')

// Each language is its own chunks; only the one being shown is fetched. The first screens wait for the small one.
const loaders: Record<Language, () => Promise<Record<string, unknown>>> = {
  en: () => import('./i18n/en').then((module) => module.en),
  hi: () => import('./i18n/hi').then((module) => module.hi),
  gu: () => import('./i18n/gu').then((module) => module.gu),
}

// The rest follows without holding up the first screen. It comes from the service worker after the first visit.
const moreLoaders: Record<Language, () => Promise<Record<string, unknown>>> = {
  en: () => import('./i18n/enMore').then((module) => module.enMore),
  hi: () => import('./i18n/hiMore').then((module) => module.hiMore),
  gu: () => import('./i18n/guMore').then((module) => module.guMore),
}

const loaded = new Set<Language>()

async function loadLanguage(language: Language) {
  if (loaded.has(language)) return
  // Gujarati falls back to English for the few screens with their own text (admin, team, agent): load it too.
  if (language === 'gu') await loadLanguage('en')
  // Merged: the admin screen may have added its own text to this language already.
  i18n.addResourceBundle(language, 'translation', await loaders[language](), true, false)
  loaded.add(language)
  // Screens showing while it arrives redraw with the words (bindI18nStore below).
  void moreLoaders[language]().then((text) => i18n.addResourceBundle(language, 'translation', text, true, false))
}

/** Adds a screen's own text on top of the main files, once. A language it doesn't have falls back to English. */
/** A language's full text, loaded without switching the app to it (a poster in Hindi while the app is in English). */
export async function ensureLanguage(language: Language) {
  await loadLanguage(language)
  i18n.addResourceBundle(language, 'translation', await moreLoaders[language](), true, false)
}

export function addText(text: Record<'en' | 'hi', Record<string, unknown>> & { gu?: Record<string, unknown> }) {
  for (const language of ['en', 'hi', 'gu'] as const) if (text[language]) i18n.addResourceBundle(language, 'translation', text[language]!, true, false)
}

function initialLanguage(): Language {
  try {
    // English by default; Hindi when the person has chosen it.
    const saved = localStorage.getItem('grocery-khata.lang')
    return saved === 'hi' || saved === 'gu' ? saved : 'en'
  } catch {
    return 'en'
  }
}

/** Resolves once the shown language's text is loaded; the app renders after it. */
export const i18nReady = (async () => {
  const language = initialLanguage()
  await i18n.use(initReactI18next).init({ resources: {}, lng: language, fallbackLng: { gu: ['en'], default: [] }, interpolation: { escapeValue: false }, react: { bindI18nStore: 'added' } })
  await loadLanguage(language)
  document.documentElement.lang = language
})()

export async function setLanguage(language: Language, { toAccount = true } = {}) {
  await loadLanguage(language)
  await i18n.changeLanguage(language)
  document.documentElement.lang = language
  try {
    localStorage.setItem('grocery-khata.lang', language)
  } catch {
    // Not saved; the choice lasts this session.
  }
  if (toAccount) saveToAccount(language)
}

/** Signed in: the account keeps the language too, so it follows the person to another phone. Best effort, never blocks. */
function saveToAccount(language: Language) {
  const session = getSession()
  if (!session || session.user.preferredLanguage === language) return
  setSession({ ...session, user: { ...session.user, preferredLanguage: language } })
  void fetch('/api/v1/profile/language', {
    method: 'PUT', headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${session.accessToken}` }, body: JSON.stringify({ language }),
  }).catch(() => {})
}

/** At sign-in (or a new person on this phone): show the language their account was set to. */
export function useAccountLanguage(preferred: string | undefined, userId: string | undefined) {
  useEffect(() => {
    if ((preferred === 'en' || preferred === 'hi' || preferred === 'gu') && preferred !== i18n.language) void setLanguage(preferred, { toAccount: false })
    // Only when the person changes: a later change on this phone is theirs and already saved to the account.
  }, [userId])
}

export default i18n

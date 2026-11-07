import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'

type Language = 'en' | 'hi'

// Each language is its own chunks; only the one being shown is fetched. The first screens wait for the small one.
const loaders: Record<Language, () => Promise<Record<string, unknown>>> = {
  en: () => import('./i18n/en').then((module) => module.en),
  hi: () => import('./i18n/hi').then((module) => module.hi),
}

// The rest follows without holding up the first screen. It comes from the service worker after the first visit.
const moreLoaders: Record<Language, () => Promise<Record<string, unknown>>> = {
  en: () => import('./i18n/enMore').then((module) => module.enMore),
  hi: () => import('./i18n/hiMore').then((module) => module.hiMore),
}

const loaded = new Set<Language>()

async function loadLanguage(language: Language) {
  if (loaded.has(language)) return
  // Merged: the admin screen may have added its own text to this language already.
  i18n.addResourceBundle(language, 'translation', await loaders[language](), true, false)
  loaded.add(language)
  // Screens showing while it arrives redraw with the words (bindI18nStore below).
  void moreLoaders[language]().then((text) => i18n.addResourceBundle(language, 'translation', text, true, false))
}

/** Adds a screen's own text (both languages) on top of the main files, once. */
export function addText(text: Record<Language, Record<string, unknown>>) {
  for (const language of ['en', 'hi'] as const) i18n.addResourceBundle(language, 'translation', text[language], true, false)
}

function initialLanguage(): Language {
  try {
    // English by default; Hindi when the person has chosen it.
    return localStorage.getItem('grocery-khata.lang') === 'hi' ? 'hi' : 'en'
  } catch {
    return 'en'
  }
}

/** Resolves once the shown language's text is loaded; the app renders after it. */
export const i18nReady = (async () => {
  const language = initialLanguage()
  await i18n.use(initReactI18next).init({ resources: {}, lng: language, fallbackLng: false, interpolation: { escapeValue: false }, react: { bindI18nStore: 'added' } })
  await loadLanguage(language)
  document.documentElement.lang = language
})()

export async function setLanguage(language: Language) {
  await loadLanguage(language)
  await i18n.changeLanguage(language)
  document.documentElement.lang = language
  try {
    localStorage.setItem('grocery-khata.lang', language)
  } catch {
    // Not saved; the choice lasts this session.
  }
}

export default i18n

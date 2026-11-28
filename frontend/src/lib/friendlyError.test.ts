import i18n from 'i18next'
import { beforeAll, describe, expect, it } from 'vitest'
import { enMore } from '../i18n/enMore'
import { hiMore } from '../i18n/hiMore'
import { friendlyError } from './api'

describe('friendlyError', () => {
  beforeAll(async () => {
    await i18n.init({ lng: 'hi', fallbackLng: 'en', resources: { en: { translation: enMore }, hi: { translation: hiMore } } })
  })

  it('a known problem code gets its own sentence in the app language', async () => {
    await i18n.changeLanguage('hi')
    expect(friendlyError(409, { title: 'email-taken', detail: 'Another account already uses this email.' })).toBe('यह ईमेल किसी दूसरे खाते पर पहले से है।')
  })

  it('in Hindi, English server text comes after a Hindi lead saying what kind of problem it is', async () => {
    await i18n.changeLanguage('hi')
    expect(friendlyError(400, { title: 'One or more validation errors occurred.', errors: { Email: ['Enter a valid email.'] } })).toBe('भरी गई जानकारी ठीक नहीं — Enter a valid email.')
    expect(friendlyError(500, null)).toBe('सर्वर में दिक्कत: थोड़ी देर बाद फिर कोशिश करें')
    // Already in Hindi: shown as it is.
    expect(friendlyError(400, { detail: 'नाम डालें' })).toBe('नाम डालें')
  })

  it('in English, the server text as it is', async () => {
    await i18n.changeLanguage('en')
    expect(friendlyError(403, { detail: 'Only the owner can do this.' })).toBe('Only the owner can do this.')
    expect(friendlyError(404, null)).toBe('Not found: it may have been removed')
  })
})

import type { Aisle } from '../components/AisleTiles'

// The common aisles' Gujarati names (the server has Hindi and English): Gujarati phones see their own script.
const GU: Record<string, string> = {
  'atta-rice': 'લોટ અને ચોખા', dal: 'દાળ અને કઠોળ', 'oil-ghee': 'તેલ અને ઘી', masala: 'મસાલા', 'sugar-salt': 'ખાંડ, મીઠું અને ગોળ',
  'fruits-veg': 'ફળ અને શાકભાજી', dairy: 'દૂધ, દહીં અને ઈંડાં', 'tea-coffee': 'ચા અને કોફી', breakfast: 'બ્રેડ અને નાસ્તો',
  snacks: 'બિસ્કિટ અને નમકીન', instant: 'નૂડલ્સ અને ઇન્સ્ટન્ટ', drinks: 'ઠંડાં પીણાં અને જ્યૂસ', personal: 'નહાવા, વાળ અને દાંત',
  cleaning: 'સફાઈ', pooja: 'પૂજાનો સામાન', household: 'ઘરવખરી',
}
const GU_SECTION: Record<string, string> = {
  'Daily staples': 'રોજનું રાશન', 'Dairy & breakfast': 'દૂધ અને નાસ્તો', 'Snacks & drinks': 'નાસ્તો અને પીણાં', 'Home, cleaning & pooja': 'ઘર, સફાઈ અને પૂજા',
}

/** An aisle's name for the app's language: Hindi, Gujarati, else English. */
export const aisleName = (a: Aisle, language: string) => (language === 'hi' ? a.name : language === 'gu' ? GU[a.code] ?? a.nameEn : a.nameEn)

/** A section heading ("Daily staples") for the app's language. */
export const sectionName = (a: Aisle, language: string) => (language === 'hi' ? a.section : language === 'gu' ? GU_SECTION[a.sectionEn] ?? a.sectionEn : a.sectionEn)

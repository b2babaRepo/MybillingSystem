// Customer search that forgives: everyday Hindi, Gujarati and Hinglish words typed in English or their own script
// (doodh, cheeni, chawal, jhumka) find the English item names shops use, and a letter or two wrong still matches.

/** Words people type → words shops write. Kept short and maintained by hand; the AI order reader covers the rest. */
export const SYNONYMS: Record<string, string[]> = {
  doodh: ['milk'], dudh: ['milk'], paal: ['milk'], दूध: ['milk'], દૂધ: ['milk'],
  cheeni: ['sugar'], chini: ['sugar'], shakkar: ['sugar'], चीनी: ['sugar'], ખાંડ: ['sugar'], khand: ['sugar'],
  chawal: ['rice'], chaval: ['rice'], चावल: ['rice'], ચોખા: ['rice'], chokha: ['rice'],
  aata: ['atta', 'flour', 'wheat'], atta: ['flour', 'wheat'], आटा: ['atta', 'flour'], લોટ: ['atta', 'flour'], lot: ['atta', 'flour'],
  dal: ['dal', 'pulses', 'lentil'], daal: ['dal', 'pulses', 'lentil'], दाल: ['dal'], દાળ: ['dal'],
  tel: ['oil'], तेल: ['oil'], તેલ: ['oil'],
  ghee: ['ghee'], घी: ['ghee'], ઘી: ['ghee'],
  namak: ['salt'], नमक: ['salt'], મીઠું: ['salt'], mithu: ['salt'],
  chai: ['tea'], chaay: ['tea'], चाय: ['tea'], ચા: ['tea'],
  haldi: ['turmeric', 'haldi'], हल्दी: ['turmeric', 'haldi'], હળદર: ['turmeric'],
  mirch: ['chilli', 'chili', 'mirch'], मिर्च: ['chilli'], મરચું: ['chilli'],
  dhaniya: ['coriander'], kothimbir: ['coriander'], धनिया: ['coriander'], ધાણા: ['coriander'],
  jeera: ['cumin', 'jeera'], जीरा: ['cumin'], જીરું: ['cumin'],
  anda: ['egg'], ande: ['egg'], अंडा: ['egg'], ઈંડા: ['egg'],
  dahi: ['curd', 'yogurt'], दही: ['curd'], દહીં: ['curd'],
  paneer: ['paneer', 'cottage cheese'], makhan: ['butter'], मक्खन: ['butter'], માખણ: ['butter'],
  sabun: ['soap'], साबुन: ['soap'], સાબુ: ['soap'],
  biscut: ['biscuit'], biskut: ['biscuit'], aloo: ['potato'], pyaz: ['onion'], pyaaz: ['onion'], kanda: ['onion'], tamatar: ['tomato'],
  jhumka: ['earring', 'jhumka'], jhumki: ['earring', 'jhumka'], kangan: ['bangle'], chudi: ['bangle'], payal: ['anklet'], haar: ['necklace'], angoothi: ['ring'],
  kurti: ['kurti', 'kurta'], saree: ['saree', 'sari'], sari: ['saree'], dupatta: ['dupatta', 'stole'], chappal: ['slipper', 'sandal'],
}

const clean = (s: string) => s.toLowerCase().normalize('NFC').replace(/[^\p{L}\p{M}\p{N}\s]/gu, ' ').replace(/\s+/g, ' ').trim()

/** Edit distance, stopping early once it is beyond the limit. */
export function distance(a: string, b: string, limit = 2): number {
  if (Math.abs(a.length - b.length) > limit) return limit + 1
  let prev = Array.from({ length: b.length + 1 }, (_, i) => i)
  for (let i = 1; i <= a.length; i++) {
    const row = [i]
    let best = i
    for (let j = 1; j <= b.length; j++) {
      row[j] = Math.min(prev[j] + 1, row[j - 1] + 1, prev[j - 1] + (a[i - 1] === b[j - 1] ? 0 : 1))
      best = Math.min(best, row[j])
    }
    if (best > limit) return limit + 1
    prev = row
  }
  return prev[b.length]
}

/** The words a query stands for: itself, and its synonyms. */
export function expand(query: string): string[][] {
  return clean(query).split(' ').filter(Boolean).map((w) => [w, ...(SYNONYMS[w] ?? [])])
}

/**
 * How well a text matches the query (0: not at all). Every query word must be found, as itself or a synonym:
 * at the start of a word scores best, inside a word next, then one or two letters wrong (words of 4+ letters).
 */
export function score(text: string, query: string): number {
  const words = clean(text).split(' ')
  const wanted = expand(query)
  if (wanted.length === 0) return 0
  let total = 0
  for (const options of wanted) {
    let best = 0
    for (const o of options) {
      for (const w of words) {
        if (w.startsWith(o)) best = Math.max(best, 3)
        else if (o.length >= 3 && w.includes(o)) best = Math.max(best, 2)
        else if (o.length >= 4 && distance(w.slice(0, o.length + 1), o, o.length >= 7 ? 2 : 1) <= (o.length >= 7 ? 2 : 1)) best = Math.max(best, 1)
      }
    }
    if (best === 0) return 0
    total += best
  }
  return total
}

/** Items ranked by match, best first; those that don't match are left out. */
export function rank<T>(items: T[], query: string, text: (item: T) => string): T[] {
  return items.map((item) => ({ item, s: score(text(item), query) })).filter((x) => x.s > 0).sort((a, b) => b.s - a.s).map((x) => x.item)
}

const RECENT = 'gk-recent-searches'

export function recentSearches(): string[] {
  try {
    return JSON.parse(localStorage.getItem(RECENT) ?? '[]') as string[]
  } catch {
    return []
  }
}

export function rememberSearch(q: string) {
  const term = q.trim()
  if (term.length < 2) return
  try {
    localStorage.setItem(RECENT, JSON.stringify([term, ...recentSearches().filter((x) => x.toLowerCase() !== term.toLowerCase())].slice(0, 8)))
  } catch { /* private mode */ }
}

export function clearSearches() {
  try { localStorage.removeItem(RECENT) } catch { /* private mode */ }
}

/** Speech to text in the page's language, where the browser can (Chrome on Android, Edge). Null when it can't. */
export function listen(lang: string, onText: (text: string) => void, onEnd: () => void): (() => void) | null {
  const W = window as unknown as { SpeechRecognition?: new () => SpeechLike; webkitSpeechRecognition?: new () => SpeechLike }
  const Ctor = W.SpeechRecognition ?? W.webkitSpeechRecognition
  if (!Ctor) return null
  const r = new Ctor()
  r.lang = lang === 'hi' ? 'hi-IN' : lang === 'gu' ? 'gu-IN' : 'en-IN'
  r.interimResults = false
  r.onresult = (e) => onText(e.results[0]?.[0]?.transcript ?? '')
  r.onend = onEnd
  r.onerror = onEnd
  r.start()
  return () => r.stop()
}

interface SpeechLike {
  lang: string
  interimResults: boolean
  onresult: (e: { results: { [i: number]: { [j: number]: { transcript: string } } } }) => void
  onend: () => void
  onerror: () => void
  start: () => void
  stop: () => void
}

export const canListen = () => typeof window !== 'undefined' && ('SpeechRecognition' in window || 'webkitSpeechRecognition' in window)

// ---- Data saver: small photos only, on this phone ----

const SAVER = 'gk-data-saver'
export function dataSaver(): boolean {
  try { return localStorage.getItem(SAVER) === '1' } catch { return false }
}
export function setDataSaver(on: boolean) {
  try { if (on) localStorage.setItem(SAVER, '1'); else localStorage.removeItem(SAVER) } catch { /* private mode */ }
}

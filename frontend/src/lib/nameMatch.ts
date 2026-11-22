// Matching what a shopkeeper types against item names, in either script: "aata" finds "आटा", "चीनी" finds "Cheeni".

const CONSONANTS: Record<string, string> = {
  क: 'k', ख: 'kh', ग: 'g', घ: 'gh', ङ: 'n', च: 'ch', छ: 'chh', ज: 'j', झ: 'jh', ञ: 'n',
  ट: 't', ठ: 'th', ड: 'd', ढ: 'dh', ण: 'n', त: 't', थ: 'th', द: 'd', ध: 'dh', न: 'n',
  प: 'p', फ: 'f', ब: 'b', भ: 'bh', म: 'm', य: 'y', र: 'r', ल: 'l', व: 'v', श: 'sh', ष: 'sh', स: 's', ह: 'h',
  क़: 'k', ख़: 'kh', ग़: 'g', ज़: 'j', ड़: 'd', ढ़: 'dh', फ़: 'f', ळ: 'l',
}
const VOWELS: Record<string, string> = { अ: 'a', आ: 'aa', इ: 'i', ई: 'ee', उ: 'u', ऊ: 'oo', ऋ: 'ri', ए: 'e', ऐ: 'ai', ओ: 'o', औ: 'au', ऑ: 'o' }
const MATRAS: Record<string, string> = { 'ा': 'aa', 'ि': 'i', 'ी': 'ee', 'ु': 'u', 'ू': 'oo', 'ृ': 'ri', 'े': 'e', 'ै': 'ai', 'ो': 'o', 'ौ': 'au', 'ॉ': 'o' }

/** Devanagari spelled out in Latin letters the way people type Hindi on a phone ("आटा" → "aataa"). */
export function toLatin(text: string): string {
  const chars = [...text.normalize('NFC')]
  let out = ''
  for (let i = 0; i < chars.length; i++) {
    const c = chars[i] + (chars[i + 1] === '़' ? chars[++i] : '') // nukta forms
    const next = chars[i + 1]
    if (CONSONANTS[c]) {
      out += CONSONANTS[c]
      // The inherent "a": not before a vowel sign or halant, and not at the end of a word.
      const endOfWord = next === undefined || /\s/.test(next)
      if (!(next && (MATRAS[next] || next === '्')) && !endOfWord) out += 'a'
    } else if (MATRAS[c]) out += MATRAS[c]
    else if (VOWELS[c]) out += VOWELS[c]
    else if (c === 'ं' || c === 'ँ') out += 'n'
    else if (c === '्' || c === 'ः') { /* halant, visarga */ }
    else out += c
  }
  return out
}

/** Spelling-insensitive form: "aata", "atta" and "आटा" all become "ata". */
export function loose(text: string): string {
  return toLatin(text).toLowerCase()
    .replace(/aa/g, 'a').replace(/ee/g, 'i').replace(/oo/g, 'u').replace(/w/g, 'v').replace(/z/g, 'j')
    .replace(/ph/g, 'f').replace(/sh/g, 's').replace(/([bcdgjkpt])h/g, '$1').replace(/y\b/g, 'i')
    .replace(/([a-z])\1+/g, '$1').replace(/[^a-z0-9]+/g, '')
    // Hindi drops many short a's (बासमती is "basmati", not "basamati"): ignore them after the first letter.
    .replace(/(?!^)a/g, '')
}

/** Each typed word must start one of the name's words, in either script. */
export function nameMatches(name: string, query: string): boolean {
  const words = name.split(/[\s()/,.-]+/).map(loose).filter(Boolean)
  const typed = query.split(/\s+/).map(loose).filter(Boolean)
  return typed.length > 0 && typed.every((q) => words.some((w) => w.startsWith(q)))
}

/** "2*", "2x", "1.5 ×" before the item or code: how many to add. */
export function splitQuantity(text: string): { quantity: number | null; rest: string } {
  const m = text.match(/^\s*(\d+(?:\.\d+)?)\s*[*xX×]\s*(.*)$/)
  if (!m) return { quantity: null, rest: text }
  const quantity = Number(m[1])
  return quantity > 0 ? { quantity, rest: m[2] } : { quantity: null, rest: text }
}

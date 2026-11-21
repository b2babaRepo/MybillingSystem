import { describe, expect, it } from 'vitest'
import { en } from './en'
import { enMore } from './enMore'
import { hn } from './hn'

const keys = (tree: object, prefix = ''): string[] =>
  Object.entries(tree).flatMap(([k, v]) => (v && typeof v === 'object' ? keys(v, `${prefix}${k}.`) : [`${prefix}${k}`]))
const at = (tree: object, path: string) => path.split('.').reduce<unknown>((o, k) => (o as Record<string, unknown> | undefined)?.[k], tree)
const slots = (text: unknown) => [...String(text).matchAll(/{{\s*(\w+)\s*}}/g)].map((m) => m[1]).sort()

describe('Hinglish text', () => {
  const english = { ...enMore, ...en }
  it('only has keys the English text has, with the same {{slots}}', () => {
    for (const path of keys(hn)) {
      const source = at(en, path) ?? at(enMore, path) ?? at(english, path)
      expect(source, path).toBeTypeOf('string')
      expect(slots(at(hn, path)), path).toEqual(slots(source))
    }
  })
})

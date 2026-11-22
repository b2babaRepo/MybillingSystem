import { describe, expect, it } from 'vitest'
import { agentText } from './agent'
import { en } from './en'
import { enMore } from './enMore'
import { gu } from './gu'
import { guMore } from './guMore'
import { kharchaText } from './kharcha'
import { teamText } from './team'

/** Every key path in a text tree, "plan2.banner.trial_one". */
const keys = (tree: object, prefix = ''): string[] =>
  Object.entries(tree).flatMap(([k, v]) => (v && typeof v === 'object' ? keys(v, `${prefix}${k}.`) : [`${prefix}${k}`])).sort()

/** The {{names}} a text fills in, so a translation can't drop or misspell one. */
const slots = (tree: object): Record<string, string[]> =>
  Object.fromEntries(keys(tree).map((path) => [path, [...String(path.split('.').reduce<unknown>((o, k) => (o as Record<string, unknown>)[k], tree)).matchAll(/{{\s*(\w+)\s*}}/g)].map((m) => m[1]).sort()]))

describe('Gujarati text', () => {
  const pairs: [string, object, object][] = [
    ['main', en, gu], ['more', enMore, guMore], ['kharcha', kharchaText.en, kharchaText.gu], ['team', teamText.en, teamText.gu], ['agent', agentText.en, agentText.gu],
  ]
  it.each(pairs)('%s has every English key and fills in the same names', (_name, english, gujarati) => {
    expect(keys(gujarati)).toEqual(keys(english))
    expect(slots(gujarati)).toEqual(slots(english))
  })
})

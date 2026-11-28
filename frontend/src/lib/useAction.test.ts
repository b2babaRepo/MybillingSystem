import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { resetToasts, subscribeToasts, type Toast } from './notify'
import { runAction } from './useAction'

describe('runAction', () => {
  let seen: Toast[] = []
  let stop = () => {}
  beforeEach(() => { vi.stubGlobal('window', globalThis); resetToasts(); stop = subscribeToasts((t) => { seen = t }) })
  afterEach(() => { stop(); resetToasts(); vi.unstubAllGlobals() })

  it('says done when the save works and returns its result', async () => {
    const value = await runAction(async () => 7, { done: 'Supplier added' })
    expect(value).toBe(7)
    expect(seen.map((t) => [t.kind, t.text])).toEqual([['success', 'Supplier added']])
  })

  it('says why when it fails, never silently', async () => {
    const value = await runAction(async () => { throw new Error('No internet') })
    expect(value).toBeUndefined()
    expect(seen.map((t) => [t.kind, t.text])).toEqual([['error', 'No internet']])
  })
})

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { dismissToast, notify, resetToasts, subscribeToasts, type Toast } from './notify'

describe('notify', () => {
  let seen: Toast[] = []
  let stop = () => {}
  beforeEach(() => {
    vi.useFakeTimers()
    vi.stubGlobal('window', globalThis)
    resetToasts()
    stop = subscribeToasts((t) => { seen = t })
  })
  afterEach(() => { stop(); resetToasts(); vi.useRealTimers(); vi.unstubAllGlobals() })

  it('a success goes by itself after a few seconds; an error stays until closed', () => {
    notify.success('Customer added')
    const errorId = notify.error(new Error('Not saved: check the internet'))
    expect(seen.map((t) => [t.kind, t.text])).toEqual([['success', 'Customer added'], ['error', 'Not saved: check the internet']])
    vi.advanceTimersByTime(4100)
    expect(seen.map((t) => t.kind)).toEqual(['error'])
    dismissToast(errorId)
    expect(seen).toEqual([])
  })

  it('the same message again is counted, not stacked, and at most three show', () => {
    notify.success('Saved'); notify.success('Saved')
    expect(seen).toHaveLength(1)
    expect(seen[0].repeat).toBe(2)
    notify.info('a'); notify.info('b'); notify.info('c')
    expect(seen.map((t) => t.text)).toEqual(['a', 'b', 'c'])
  })

  it('an undo button stays long enough to press and runs the undo', () => {
    const undo = vi.fn()
    notify.success('Item removed', { undo })
    vi.advanceTimersByTime(5000)
    expect(seen).toHaveLength(1)
    seen[0].action!.run()
    expect(undo).toHaveBeenCalledOnce()
    vi.advanceTimersByTime(3100)
    expect(seen).toEqual([])
  })
})

import { beforeEach, describe, expect, it } from 'vitest'
import { appLockMatches, clearAppLock, hasAppLock, passcodeProblem, setAppLock } from './appLock'

const store = new Map<string, string>()
beforeEach(() => {
  store.clear()
  globalThis.localStorage = { getItem: (k: string) => store.get(k) ?? null, setItem: (k: string, v: string) => void store.set(k, v), removeItem: (k: string) => void store.delete(k) } as Storage
})

describe('app lock', () => {
  it('keeps a salted hash, never the digits, and checks against it', async () => {
    expect(hasAppLock()).toBe(false)
    await setAppLock('4826')
    expect(hasAppLock()).toBe(true)
    expect([...store.values()][0]).not.toContain('4826')
    expect(await appLockMatches('4826')).toBe(true)
    expect(await appLockMatches('4827')).toBe(false)
    clearAppLock()
    expect(hasAppLock()).toBe(false)
  })
  it('refuses short and easy passcodes', () => {
    expect(passcodeProblem('12')).toBe('length')
    expect(passcodeProblem('12a4')).toBe('length')
    expect(passcodeProblem('1234')).toBe('easy')
    expect(passcodeProblem('0000')).toBe('easy')
    expect(passcodeProblem('9876')).toBe('easy')
    expect(passcodeProblem('4826')).toBeNull()
  })
})

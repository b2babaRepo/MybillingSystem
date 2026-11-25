import { beforeEach, describe, expect, it } from 'vitest'
import { appLockMatches, clearAppLock, hasAppLock, passcodeProblem, rightPasscode, setAppLock, wrongPasscode, wrongSoFar } from './appLock'

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
  it('keeps a slow PBKDF2 hash, and upgrades a passcode saved as a single SHA-256 when it is next entered right', async () => {
    await setAppLock('4826')
    expect(JSON.parse([...store.values()][0]).rounds).toBeGreaterThanOrEqual(100_000)

    const salt = 'ab12'
    const old = Array.from(new Uint8Array(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(`${salt}:5937`))), (b) => b.toString(16).padStart(2, '0')).join('')
    store.set('gk-app-lock', JSON.stringify({ salt, hash: old }))
    expect(await appLockMatches('5938')).toBe(false)
    expect(JSON.parse(store.get('gk-app-lock')!).rounds).toBeUndefined()
    expect(await appLockMatches('5937')).toBe(true)
    const upgraded = JSON.parse(store.get('gk-app-lock')!)
    expect(upgraded.rounds).toBeGreaterThanOrEqual(100_000)
    expect(upgraded.hash).not.toBe(old)
    expect(await appLockMatches('5937')).toBe(true)
  })
  it('counts wrong passcodes on the phone until one is right', async () => {
    await setAppLock('4826')
    expect(wrongSoFar()).toBe(0)
    expect(wrongPasscode()).toBe(1)
    expect(wrongPasscode()).toBe(2)
    expect(wrongSoFar()).toBe(2) // what a reload reads back
    rightPasscode()
    expect(wrongSoFar()).toBe(0)
    expect(await appLockMatches('4826')).toBe(true)
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

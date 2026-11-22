import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const KEY = 'grocery-khata.session'
const session = (refreshToken: string, minutesLeft: number) => ({
  accessToken: `access-${refreshToken}`,
  accessTokenExpiresAt: new Date(Date.now() + minutesLeft * 60_000).toISOString(),
  refreshToken,
  refreshTokenExpiresAt: new Date(Date.now() + 30 * 86_400_000).toISOString(),
  user: { id: 'u1', fullName: 'Neha', preferredLanguage: 'en' },
  memberships: [],
  shopId: 's1',
})

// Two tabs share one saved session; each also keeps its own copy in memory.
function storage() {
  const items = new Map<string, string>()
  return { getItem: (k: string) => items.get(k) ?? null, setItem: (k: string, v: string) => void items.set(k, v), removeItem: (k: string) => void items.delete(k) }
}

describe('refreshing the sign-in from a tab with an old copy', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.stubGlobal('localStorage', storage())
  })
  afterEach(() => vi.unstubAllGlobals())

  it("takes the other tab's new session instead of sending the swapped token", async () => {
    const fetch = vi.fn()
    vi.stubGlobal('fetch', fetch)
    const { setSession, getSession } = await import('./session')
    const { refreshSession } = await import('./api')
    setSession(session('old', 0) as never) // this tab's copy, about to expire
    localStorage.setItem(KEY, JSON.stringify(session('new', 15))) // another tab refreshed meanwhile

    expect(await refreshSession()).toBe(true)
    expect(fetch).not.toHaveBeenCalled()
    expect(getSession()?.refreshToken).toBe('new')
  })

  it('asks the server when no other tab has refreshed', async () => {
    const fetch = vi.fn(async () => new Response(JSON.stringify(session('next', 15)), { status: 200, headers: { 'Content-Type': 'application/json' } }))
    vi.stubGlobal('fetch', fetch)
    const { setSession, getSession } = await import('./session')
    const { refreshSession } = await import('./api')
    setSession(session('mine', 0) as never)

    expect(await refreshSession()).toBe(true)
    expect(fetch).toHaveBeenCalledOnce()
    expect(getSession()?.refreshToken).toBe('next')
  })
})

import { getSession, savedSession, setSession } from './session'
import type { AuthResponse, ProblemDetails } from './types'

/** The request never reached the server (offline, or the server is down). Safe to retry later. */
export class NetworkError extends Error {
  constructor() {
    super('No connection')
  }
}

/** The server answered with an error. */
export class ApiError extends Error {
  status: number
  problem: ProblemDetails | null

  constructor(status: number, problem: ProblemDetails | null) {
    super(problemMessage(problem) ?? `Request failed (${status})`)
    this.status = status
    this.problem = problem
  }
}

/** The first field error, else the detail or title: the text to show the shopkeeper. */
export function problemMessage(problem: ProblemDetails | null): string | undefined {
  const firstError = problem?.errors ? Object.values(problem.errors)[0]?.[0] : undefined
  return firstError ?? problem?.detail ?? problem?.title
}

/**
 * A proxy (Nginx in production, Vite in development) answers 502/503/504 when the API behind it is down or
 * restarting. To the shop that is the same as no internet: use the saved copy and keep entries in the outbox.
 * The API's own 503s ("Codes by SMS aren't available right now") come as JSON and are shown as they are.
 */
const GATEWAY_DOWN = new Set([502, 503, 504])

/** True for a proxy's "the API is down" page; false for an answer the API itself gave. */
export function isGatewayDown(response: Pick<Response, 'status' | 'headers'>): boolean {
  return GATEWAY_DOWN.has(response.status) && !(response.headers.get('content-type') ?? '').includes('json')
}

async function send(path: string, method: string, body: unknown, token?: string, extra?: Record<string, string>): Promise<Response> {
  const response = await fetchOrThrow(path, method, body, token, extra)
  if (isGatewayDown(response)) throw new NetworkError()
  return response
}

async function fetchOrThrow(path: string, method: string, body: unknown, token?: string, extra?: Record<string, string>): Promise<Response> {
  try {
    return await fetch(`/api${path}`, {
      method,
      // Form data (a voice recording) goes as-is: the browser sets its multipart content type.
      headers: {
        ...(body === undefined || body instanceof FormData ? {} : { 'Content-Type': 'application/json' }),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        // The phone's clock: the server puts entry times right when it is wrong.
        'X-Client-Time': new Date().toISOString(),
        ...(billPasscode ? { 'X-Bill-Passcode': billPasscode } : {}),
        ...extra,
      },
      body: body === undefined ? undefined : body instanceof FormData ? body : JSON.stringify(body),
    })
  } catch {
    throw new NetworkError()
  }
}

let refreshing: Promise<boolean> | null = null

/** One tab at a time across the whole app (where the browser can), else just within this tab. */
function oneAtATime<T>(work: () => Promise<T>): Promise<T> {
  const locks = typeof navigator !== 'undefined' ? (navigator as Navigator & { locks?: LockManager }).locks : undefined
  return locks ? (locks.request('gk-session-refresh', work) as Promise<T>) : work()
}

/**
 * Swaps the refresh token for new tokens. One at a time: parallel calls in this tab share the same refresh, and tabs
 * take turns. A tab whose token another tab has already swapped takes that tab's new session instead of sending the
 * old token, which the server would take for a stolen one and sign the person out everywhere.
 */
export function refreshSession(): Promise<boolean> {
  const mine = getSession()?.refreshToken
  refreshing ??= oneAtATime(async () => {
    try {
      const saved = savedSession()
      if (saved && saved.refreshToken !== mine && new Date(saved.accessTokenExpiresAt).getTime() > Date.now() + 60_000) {
        setSession(saved)
        return true
      }
      const session = saved ?? getSession()
      if (!session) return false
      const response = await send('/v1/auth/refresh', 'POST', { refreshToken: session.refreshToken })
      if (response.ok) {
        setSession((await response.json()) as AuthResponse)
        return true
      }
      if (response.status === 401) setSession(null)
      return false
    } finally {
      refreshing = null
    }
  })
  return refreshing
}

function expiresSoon(session: AuthResponse) {
  return new Date(session.accessTokenExpiresAt).getTime() - Date.now() < 60_000
}

/**
 * Calls the API as the signed-in user: refreshes the access token when it is about to expire, and once
 * more on a 401. Returns the raw response; throws {@link NetworkError} when there is no connection.
 */
/** `headers`: more to send (an outbox entry's bill passcode, typed when it was made offline). */
export async function request(path: string, method = 'GET', body?: unknown, headers?: Record<string, string>): Promise<Response> {
  let session = getSession()
  if (session && expiresSoon(session)) {
    await refreshSession()
    session = getSession()
  }

  let response = await send(path, method, body, session?.accessToken, headers)
  if (response.status === 401 && session && (await refreshSession())) {
    response = await send(path, method, body, getSession()?.accessToken, headers)
  }
  return response
}

export async function readProblem(response: Response): Promise<ProblemDetails | null> {
  try {
    return (await response.json()) as ProblemDetails
  } catch {
    return null
  }
}

/** {@link request}, then the JSON body; throws {@link ApiError} for an error status. */
export async function api<T>(path: string, method = 'GET', body?: unknown): Promise<T> {
  const response = await request(path, method, body)
  if (!response.ok) throw new ApiError(response.status, await readProblem(response))
  // 204 No Content: nothing to read (an action done, or nothing there yet).
  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

/** The bill passcode typed for the action being sent: only while {@link withBillPasscode} retries it. */
let billPasscode: string | null = null

/** The server wants the shop's bill passcode for this action (cancel a bill, a return…). */
export const needsBillPasscode = (error: unknown): error is ApiError => error instanceof ApiError && error.status === 403 && error.problem?.type === 'bill-passcode'

/**
 * Runs an action that undoes money. When the shop has a bill passcode, the server refuses it at first: then the
 * passcode is asked for and the action sent again with it. Closing the prompt ends it with the server's refusal.
 */
export async function withBillPasscode<T>(run: () => Promise<T>, askPasscode: (wrongBefore: boolean) => Promise<string | null>): Promise<T> {
  try {
    return await run()
  } catch (refused) {
    if (!needsBillPasscode(refused)) throw refused
    let last: ApiError = refused
    // Two tries at typing it; the server locks it after five wrong ones.
    for (let attempt = 0; attempt < 2; attempt++) {
      const code = (await askPasscode(attempt > 0))?.trim()
      if (!code) throw last
      billPasscode = code
      try {
        return await run()
      } catch (again) {
        if (!needsBillPasscode(again)) throw again
        last = again
      } finally {
        billPasscode = null
      }
    }
    throw last
  }
}

/** Sign-in calls go without a token and save the session they return. */
export async function authenticate(path: string, body: unknown): Promise<AuthResponse> {
  const response = await send(path, 'POST', body, path.endsWith('select-shop') ? getSession()?.accessToken : undefined)
  if (!response.ok) throw new ApiError(response.status, await readProblem(response))
  const session = (await response.json()) as AuthResponse
  setSession(session)
  return session
}

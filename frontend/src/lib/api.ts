import { getSession, setSession } from './session'
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
 */
const GATEWAY_DOWN = new Set([502, 503, 504])

async function send(path: string, method: string, body: unknown, token?: string): Promise<Response> {
  const response = await fetchOrThrow(path, method, body, token)
  if (GATEWAY_DOWN.has(response.status)) throw new NetworkError()
  return response
}

async function fetchOrThrow(path: string, method: string, body: unknown, token?: string): Promise<Response> {
  try {
    return await fetch(`/api${path}`, {
      method,
      // Form data (a voice recording) goes as-is: the browser sets its multipart content type.
      headers: {
        ...(body === undefined || body instanceof FormData ? {} : { 'Content-Type': 'application/json' }),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        // The phone's clock: the server puts entry times right when it is wrong.
        'X-Client-Time': new Date().toISOString(),
      },
      body: body === undefined ? undefined : body instanceof FormData ? body : JSON.stringify(body),
    })
  } catch {
    throw new NetworkError()
  }
}

let refreshing: Promise<boolean> | null = null

/** Swaps the refresh token for new tokens. One at a time: parallel calls share the same refresh. */
export function refreshSession(): Promise<boolean> {
  refreshing ??= (async () => {
    try {
      const session = getSession()
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
  })()
  return refreshing
}

function expiresSoon(session: AuthResponse) {
  return new Date(session.accessTokenExpiresAt).getTime() - Date.now() < 60_000
}

/**
 * Calls the API as the signed-in user: refreshes the access token when it is about to expire, and once
 * more on a 401. Returns the raw response; throws {@link NetworkError} when there is no connection.
 */
export async function request(path: string, method = 'GET', body?: unknown): Promise<Response> {
  let session = getSession()
  if (session && expiresSoon(session)) {
    await refreshSession()
    session = getSession()
  }

  let response = await send(path, method, body, session?.accessToken)
  if (response.status === 401 && session && (await refreshSession())) {
    response = await send(path, method, body, getSession()?.accessToken)
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

/** Sign-in calls go without a token and save the session they return. */
export async function authenticate(path: string, body: unknown): Promise<AuthResponse> {
  const response = await send(path, 'POST', body, path.endsWith('select-shop') ? getSession()?.accessToken : undefined)
  if (!response.ok) throw new ApiError(response.status, await readProblem(response))
  const session = (await response.json()) as AuthResponse
  setSession(session)
  return session
}

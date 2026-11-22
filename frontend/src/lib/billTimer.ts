/** Longer than this, the bill was left open rather than being made: not a timing worth keeping. */
const MAX_SECONDS = 1800

/** Seconds from the first item to saving. Null when it wasn't timed (a bill picked up from hold) or was left open. */
export function billSeconds(startedAt: number | null, now: number): number | null {
  if (!startedAt) return null
  const seconds = Math.round((now - startedAt) / 1000)
  return seconds >= 0 && seconds <= MAX_SECONDS ? seconds : null
}

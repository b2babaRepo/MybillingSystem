import { useEffect, useState } from 'react'

/**
 * The time now (ms), for screens that show "today", "expired" or "x days ago". Read once when the screen opens and
 * again every minute, so the screen stays right if it is left open (past midnight, say), and drawing stays pure.
 */
export function useNow(everyMs = 60_000): number {
  const [now, setNow] = useState(() => Date.now())
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), everyMs)
    return () => clearInterval(timer)
  }, [everyMs])
  return now
}

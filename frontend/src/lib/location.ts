/** The phone's location now, once. Rejects with a short reason (blocked, unavailable, timed out). */
export function currentPoint(): Promise<{ lat: number; lng: number }> {
  return new Promise((resolve, reject) => {
    if (!('geolocation' in navigator)) { reject(new Error('unavailable')); return }
    navigator.geolocation.getCurrentPosition(
      (p) => resolve({ lat: Math.round(p.coords.latitude * 1e6) / 1e6, lng: Math.round(p.coords.longitude * 1e6) / 1e6 }),
      (e) => reject(new Error(e.code === e.PERMISSION_DENIED ? 'blocked' : e.code === e.TIMEOUT ? 'timeout' : 'unavailable')),
      { timeout: 15_000, enableHighAccuracy: true },
    )
  })
}

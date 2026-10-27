import { useSyncExternalStore } from 'react'

// A tiny hash router: four screens don't need a routing library in the bundle.

const subscribe = (listener: () => void) => {
  window.addEventListener('hashchange', listener)
  return () => window.removeEventListener('hashchange', listener)
}

const currentPath = () => window.location.hash.slice(1) || '/'

export function usePath() {
  return useSyncExternalStore(subscribe, currentPath)
}

export function navigate(path: string) {
  window.location.hash = path
}

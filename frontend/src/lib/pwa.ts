import { useSyncExternalStore } from 'react'
import { registerSW } from 'virtual:pwa-register'

// App updates and installing to the home screen.
//
// Updates wait for the shopkeeper: a silent reload in the middle of a bill would throw the cart away, so a new
// version only announces itself and is applied when they tap "Update". Unsynced entries are safe either way:
// they live in IndexedDB, not in the page.

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>
}

let state = { updateReady: false, canInstall: false }
let installEvent: BeforeInstallPromptEvent | null = null
const listeners = new Set<() => void>()

function set(change: Partial<typeof state>) {
  state = { ...state, ...change }
  listeners.forEach((listener) => listener())
}

const applyUpdate = registerSW({
  immediate: true,
  onNeedRefresh: () => set({ updateReady: true }),
})

window.addEventListener('beforeinstallprompt', (event) => {
  // Chrome on Android: keep the prompt for our own button instead of the browser's mini bar.
  event.preventDefault()
  installEvent = event as BeforeInstallPromptEvent
  set({ canInstall: true })
})
window.addEventListener('appinstalled', () => {
  installEvent = null
  set({ canInstall: false })
})

export function usePwa() {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
    () => state,
  )
}

export function update() {
  void applyUpdate(true)
}

export async function install() {
  if (!installEvent) return
  await installEvent.prompt()
  await installEvent.userChoice
  installEvent = null
  set({ canInstall: false })
}

/** iPhone Safari has no install prompt: people add it from the Share menu, so we tell them how. */
export const isIosSafari = () =>
  /iphone|ipad|ipod/i.test(navigator.userAgent) && !('standalone' in navigator && (navigator as { standalone?: boolean }).standalone)

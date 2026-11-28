import i18n from 'i18next'

/**
 * The app's one way to say "done" or "it didn't work" after an add, change or removal: a short message at the
 * bottom of the screen (above the tabs) that every screen shares. One <Toaster /> at the root shows them.
 *
 *   notify.success(t('customer.added'))                       – green ✓, goes away by itself after 4 s
 *   notify.success(t('item.removed'), { undo: () => restore() }) – with an Undo button, stays 8 s
 *   notify.error(err)                                          – red ✕, stays until closed (an error, or the text)
 *   notify.info(t('saved.onPhone'))                            – grey, for "saved on this phone, will send later"
 */
export type ToastKind = 'success' | 'error' | 'info'

export interface Toast {
  id: number
  kind: ToastKind
  text: string
  action?: { label: string; run: () => void }
  /** Milliseconds before it goes by itself; null: stays until closed. */
  duration: number | null
  /** The same message shown again while still on screen: counted instead of stacking copies. */
  repeat: number
}

type Listener = (toasts: Toast[]) => void

const MAX = 3
let toasts: Toast[] = []
let nextId = 1
const listeners = new Set<Listener>()
const timers = new Map<number, number>()

function emit() {
  for (const listener of listeners) listener(toasts)
}

export function subscribeToasts(listener: Listener): () => void {
  listeners.add(listener)
  listener(toasts)
  return () => { listeners.delete(listener) }
}

export function dismissToast(id: number) {
  window.clearTimeout(timers.get(id))
  timers.delete(id)
  toasts = toasts.filter((t) => t.id !== id)
  emit()
}

function schedule(toast: Toast) {
  window.clearTimeout(timers.get(toast.id))
  if (toast.duration != null) timers.set(toast.id, window.setTimeout(() => dismissToast(toast.id), toast.duration))
}

function push(kind: ToastKind, text: string, options: { undo?: () => void; action?: { label: string; run: () => void }; duration?: number | null } = {}): number {
  const clean = text.trim() || i18n.t(kind === 'error' ? 'error' : 'saved')
  const same = toasts.find((t) => t.kind === kind && t.text === clean)
  if (same) {
    same.repeat += 1
    toasts = [...toasts]
    schedule(same)
    emit()
    return same.id
  }

  const action = options.action ?? (options.undo ? { label: i18n.t('toast.undo'), run: options.undo } : undefined)
  const toast: Toast = {
    id: nextId++,
    kind,
    text: clean,
    action,
    repeat: 1,
    // Errors wait to be read; with a button there's time to press it; otherwise a few seconds.
    duration: options.duration !== undefined ? options.duration : kind === 'error' ? null : action ? 8000 : 4000,
  }
  toasts = [...toasts, toast].slice(-MAX)
  schedule(toast)
  emit()
  return toast.id
}

/** The words for a failure: the error's own message (server or app), else the app's general "something went wrong". */
export function errorText(error: unknown): string {
  if (error instanceof Error && error.message) return error.message
  if (typeof error === 'string' && error) return error
  return i18n.t('error')
}

export const notify = {
  success: (text: string, options?: { undo?: () => void; action?: { label: string; run: () => void }; duration?: number | null }) => push('success', text, options),
  info: (text: string, options?: { action?: { label: string; run: () => void }; duration?: number | null }) => push('info', text, options),
  error: (error: unknown, options?: { action?: { label: string; run: () => void } }) => push('error', errorText(error), options),
}

/** Tests: start from nothing. */
export function resetToasts() {
  for (const id of timers.keys()) window.clearTimeout(timers.get(id))
  timers.clear()
  toasts = []
  emit()
}

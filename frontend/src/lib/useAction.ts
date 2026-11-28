import { useCallback, useRef, useState } from 'react'
import i18n from 'i18next'
import { notify } from './notify'

export interface ActionOptions {
  /** Said when it works ("ग्राहक जुड़ गया"). Omitted: a plain "हो गया"; false: nothing (the screen shows it itself). */
  done?: string | false
  /** Undo for a removal that can be put back: shown on the success message. */
  undo?: () => void
}

/**
 * The one way a screen saves, changes or removes something so the shopkeeper always knows how it went: runs the
 * action, then a green "done" or a red "didn't work: why" (lib/notify), and keeps `busy` true meanwhile so the button
 * can't be pressed twice. Returns the action's result, or undefined when it failed.
 *
 *   const { run, busy } = useAction()
 *   <Button disabled={busy} onClick={() => void run(() => api('/v1/suppliers', 'POST', form), { done: t('suppliers.added') })} />
 */
export function useAction() {
  const [busy, setBusy] = useState(false)
  const running = useRef(false)
  const run = useCallback(async <T>(action: () => Promise<T>, options: ActionOptions = {}): Promise<T | undefined> => {
    if (running.current) return undefined
    running.current = true
    setBusy(true)
    try {
      return await runAction(action, options)
    } finally {
      running.current = false
      setBusy(false)
    }
  }, [])
  return { run, busy }
}

/** The action, then its done or failed message (no React: the hook adds busy). */
export async function runAction<T>(action: () => Promise<T>, options: ActionOptions = {}): Promise<T | undefined> {
  try {
    const result = await action()
    if (options.done !== false) notify.success(options.done ?? i18n.t('toast.done'), { undo: options.undo })
    return result
  } catch (error) {
    notify.error(error)
    return undefined
  }
}

/** For a write sent in the background (no button waiting on it): says so if it fails, instead of failing silently. */
export function reportFailure(error: unknown) {
  notify.error(error)
}

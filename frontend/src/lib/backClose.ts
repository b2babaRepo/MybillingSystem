import { useEffect, useRef } from 'react'

/**
 * The phone's Back button closes an open sheet or menu instead of leaving the page. While `open`, an extra history
 * entry (same address) sits on top; Back pops it and `onClose` runs. Closing any other way (a tap, ×) removes that
 * entry again, so Back afterwards goes where it always did. Nested sheets close one at a time, top first.
 */
export function useBackToClose(open: boolean, onClose: () => void) {
  const close = useRef(onClose)
  useEffect(() => { close.current = onClose })
  useEffect(() => {
    if (!open) return
    const id = Math.random().toString(36).slice(2)
    let pushed = false
    let popped = false
    // A tick later: a mount undone at once (React's dev double mount) never touches history.
    const timer = window.setTimeout(() => {
      window.history.pushState({ ...(window.history.state ?? {}), gkSheet: id }, '')
      pushed = true
    }, 0)
    const onPop = () => {
      // Back removed our entry (a sheet opened on top of ours keeps its own, so ours stays open).
      if (pushed && !popped && window.history.state?.gkSheet !== id) {
        popped = true
        close.current()
      }
    }
    window.addEventListener('popstate', onPop)
    return () => {
      window.clearTimeout(timer)
      window.removeEventListener('popstate', onPop)
      // Closed by a tap: take our entry off, unless something (a new page) went on top of it already.
      if (pushed && !popped && window.history.state?.gkSheet === id) window.history.back()
    }
  }, [open])
}

/** Runs `action` once the sheet's history entry is gone, so a page it opens isn't undone by that Back. */
export function afterSheetClosed(action: () => void) {
  if (window.history.state?.gkSheet) {
    let ran = false
    const done = () => { window.removeEventListener('popstate', done); if (!ran) { ran = true; action() } }
    window.addEventListener('popstate', done)
    // Fallback if no Back happens (the sheet's entry was already gone).
    window.setTimeout(() => { if (window.history.state?.gkSheet === undefined) done() }, 400)
  } else {
    action()
  }
}

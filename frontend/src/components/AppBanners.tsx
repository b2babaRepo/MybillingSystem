import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { install, isIosSafari, update, usePwa } from '../lib/pwa'

const DISMISSED = 'grocery-khata.install-dismissed'

function dismissedBefore() {
  try {
    return localStorage.getItem(DISMISSED) === '1'
  } catch {
    return false
  }
}

/** "A new version is ready" (applied only when tapped) and "Install the app", above the bottom bar. */
export function AppBanners() {
  const { t } = useTranslation()
  const { updateReady, canInstall } = usePwa()
  const [dismissed, setDismissed] = useState(dismissedBefore)

  function dismiss() {
    setDismissed(true)
    try {
      localStorage.setItem(DISMISSED, '1')
    } catch {
      // Not remembered; it comes back next visit.
    }
  }

  if (updateReady) {
    return (
      <div role="status" className="fixed inset-x-4 bottom-[84px] z-20 mx-auto lg:bottom-6 flex max-w-xl items-center justify-between gap-3 rounded-2xl bg-ink p-4 text-white shadow-lg">
        <span>{t('pwa.updateReady')}</span>
        <button onClick={update} className="min-h-11 shrink-0 rounded-xl bg-teal-500 px-4 font-bold text-ink">{t('pwa.update')}</button>
      </div>
    )
  }

  const showIosHint = !canInstall && isIosSafari()
  if (dismissed || (!canInstall && !showIosHint)) return null

  return (
    <div className="fixed inset-x-4 bottom-[84px] z-20 mx-auto lg:bottom-6 flex max-w-xl items-center justify-between gap-3 rounded-2xl border border-line bg-white p-4 shadow-lg">
      <span className="text-sm">{showIosHint ? t('pwa.iosHint') : t('pwa.installHint')}</span>
      <span className="flex shrink-0 gap-1">
        {canInstall && <button onClick={() => void install()} className="min-h-11 rounded-xl bg-brand px-4 font-bold text-white">{t('pwa.install')}</button>}
        <button onClick={dismiss} aria-label={t('cancel')} className="min-h-11 min-w-11 rounded-xl text-xl text-muted">×</button>
      </span>
    </div>
  )
}

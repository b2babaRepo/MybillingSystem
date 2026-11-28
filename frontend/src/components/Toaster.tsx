import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { dismissToast, subscribeToasts, type Toast } from '../lib/notify'

const look = {
  success: { box: 'bg-emerald-700 text-white', icon: '✓' },
  error: { box: 'bg-red-700 text-white', icon: '✕' },
  info: { box: 'bg-slate-800 text-white', icon: 'ℹ' },
} as const

/**
 * Shows notify()'s messages: bottom centre, just above the bottom tabs on a phone (bottom right on a computer), never
 * covering the bill's Save button for long. Colour, an icon and the words together, so no one relies on colour alone.
 * Screen readers hear successes politely and errors at once.
 */
export function Toaster() {
  const { t } = useTranslation()
  const [toasts, setToasts] = useState<Toast[]>([])
  useEffect(() => subscribeToasts(setToasts), [])

  return (
    <div className="pointer-events-none fixed inset-x-0 bottom-[calc(5.5rem+env(safe-area-inset-bottom))] z-50 flex flex-col items-center gap-2 px-3 lg:bottom-6 lg:items-end lg:px-6 print:hidden">
      {/* Always in the page, so screen readers announce what is added to them. */}
      <div role="status" aria-live="polite" className="sr-only">{toasts.filter((x) => x.kind !== 'error').map((x) => x.text).join('. ')}</div>
      <div role="alert" aria-live="assertive" className="sr-only">{toasts.filter((x) => x.kind === 'error').map((x) => x.text).join('. ')}</div>
      {toasts.map((toast) => (
        <div key={toast.id} aria-hidden="true"
          className={`pointer-events-auto flex w-full max-w-md animate-[toast-in_160ms_ease-out] items-start gap-3 rounded-2xl px-4 py-3 shadow-[0_10px_30px_rgba(0,0,0,0.25)] ${look[toast.kind].box}`}>
          <span className="mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-white/20 text-sm font-extrabold">{look[toast.kind].icon}</span>
          <p className="min-w-0 flex-1 text-[15px] font-semibold leading-snug">
            {toast.text}{toast.repeat > 1 && <span className="ml-1 opacity-75">×{toast.repeat}</span>}
          </p>
          {toast.action && (
            <button type="button" onClick={() => { toast.action!.run(); dismissToast(toast.id) }}
              className="min-h-9 shrink-0 rounded-lg bg-white/20 px-3 text-sm font-bold underline-offset-2 hover:bg-white/30">
              {toast.action.label}
            </button>
          )}
          <button type="button" onClick={() => dismissToast(toast.id)} aria-label={t('a11y.close')}
            className="-mr-1 flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-xl leading-none opacity-80 hover:bg-white/15">×</button>
        </div>
      ))}
    </div>
  )
}

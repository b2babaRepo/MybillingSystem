import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { setLanguage } from '../i18n'

export interface AccountAction {
  label: string
  onClick: () => void
}

/**
 * The round initial at the top right of every home screen. Tapping it opens a bottom sheet with who is signed in,
 * a few account actions, the language switch and a clear red Sign out at the bottom.
 */
export function AccountMenu({ name, role, actions, unsynced, onSignOut }: {
  name: string
  role: string
  actions: AccountAction[]
  unsynced: number
  onSignOut: () => void
}) {
  const { t, i18n } = useTranslation()
  const [open, setOpen] = useState(false)
  const initial = name.trim().charAt(0).toUpperCase() || '?'
  const run = (action: () => void) => () => {
    setOpen(false)
    action()
  }

  return (
    <>
      <button onClick={() => setOpen(true)} aria-label={t('account.open')} aria-haspopup="dialog"
        className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full border-2 border-white bg-amber-400 text-[17px] font-extrabold text-amber-950">
        {initial}
      </button>
      {open && (
        <div role="dialog" aria-modal="true" aria-label={t('account.title')} className="fixed inset-0 z-30 flex items-end justify-center bg-black/55 text-ink" onClick={() => setOpen(false)}>
          <div className="flex w-full max-w-xl flex-col gap-2.5 rounded-t-[26px] bg-white px-4 pb-[calc(1.75rem+env(safe-area-inset-bottom))] pt-3" onClick={(e) => e.stopPropagation()}>
            <span aria-hidden className="h-1.5 w-11 self-center rounded-full bg-[#cfd8d6]" />
            <div className="flex items-center gap-3 py-2">
              <span aria-hidden className="flex h-13 w-13 items-center justify-center rounded-full bg-amber-400 text-[22px] font-extrabold text-amber-950">{initial}</span>
              <span className="flex min-w-0 flex-col">
                <span className="truncate text-lg font-bold">{name}</span>
                <span className="truncate text-sm text-muted">{role}</span>
              </span>
            </div>
            {actions.map((a) => (
              <button key={a.label} onClick={run(a.onClick)} className="min-h-14 rounded-2xl border border-line px-4 text-left font-semibold">{a.label}</button>
            ))}
            <button onClick={() => setLanguage(i18n.language === 'hi' ? 'en' : 'hi')} className="min-h-14 rounded-2xl border border-line px-4 text-left font-semibold">
              {t('account.language')}: {i18n.language === 'hi' ? 'हिंदी → English' : 'English → हिंदी'}
            </button>
            <button onClick={run(onSignOut)} className="mt-1.5 min-h-14 rounded-2xl bg-red-50 text-[17px] font-bold text-red-700">{t('signOut')}</button>
            {unsynced > 0 && <p className="text-center text-sm text-muted">{t('account.unsynced', { count: unsynced })}</p>}
          </div>
        </div>
      )}
    </>
  )
}

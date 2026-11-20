import { useEffect } from 'react'
import type { ButtonHTMLAttributes, HTMLAttributes, InputHTMLAttributes, ReactNode, SelectHTMLAttributes } from 'react'
import { useTranslation } from 'react-i18next'
import { formatRupees } from '../lib/format'

/**
 * How a button looks, apart from its size and place: `solid` for the main action (save, add), `outline` for the one
 * beside it (hold, minus, choose), `link` for small actions in text, `quiet` for minor ones (clear, cancel), `danger` for a link that undoes something. Screens
 * keep their own size and layout classes and add one of these.
 */
export const btn = {
  solid: 'bg-brand font-bold text-white shadow-md shadow-teal-900/20 transition hover:bg-brand-strong active:scale-[0.98] active:bg-brand-strong disabled:opacity-45 disabled:shadow-none',
  outline: 'border-[1.5px] border-brand bg-white font-bold text-brand-strong transition hover:bg-brand-soft active:scale-[0.98] disabled:opacity-45',
  /** Adds a row or starts something new: + Add item, + New order. Dashed so it never looks like Save. */
  add: 'inline-flex items-center justify-center gap-1.5 rounded-xl border-[1.5px] border-dashed border-brand-line bg-brand-soft/60 px-4 font-bold text-brand transition hover:bg-brand-soft active:scale-[0.98]',
  /** A button that cancels or removes; filled red only inside the confirm box. */
  dangerOutline: 'rounded-xl border-[1.5px] border-red-200 bg-white px-3 font-bold text-due transition hover:bg-due-soft active:scale-[0.98] disabled:opacity-45',
  link: 'font-semibold text-brand underline disabled:opacity-50',
  quiet: 'text-muted underline',
  /** A link that undoes or warns: reopen, remove, cancel an order. */
  danger: 'font-semibold text-due underline disabled:opacity-50',
}

/** A text box or drop-down at counter size; `controlSm` for rows with several boxes side by side. */
export const control = 'h-12 min-w-0 rounded-xl border border-line bg-white px-3'
export const controlSm = 'h-11 min-w-0 rounded-xl border border-line bg-white px-3'

/** A white card on the green-grey ground; the tones mark brand info, a warning or something overdue. */
export function Card({ tone = 'plain', className = '', ...props }: HTMLAttributes<HTMLElement> & { tone?: 'plain' | 'brand' | 'warn' | 'due' }) {
  const tones = { plain: 'bg-white shadow-sm', brand: 'bg-brand-soft', warn: 'bg-warn-soft', due: 'bg-due-soft' }
  return <section {...props} className={`rounded-card p-4 ${tones[tone]} ${className}`} />
}

export function Input({ small = false, className = '', ...props }: InputHTMLAttributes<HTMLInputElement> & { small?: boolean }) {
  return <input {...props} className={`${small ? controlSm : control} outline-none focus:border-brand-line ${className}`} />
}

export function Select({ small = false, className = '', ...props }: SelectHTMLAttributes<HTMLSelectElement> & { small?: boolean }) {
  return <select {...props} className={`${small ? controlSm : control} outline-none focus:border-brand-line ${className}`} />
}

/** `busy` greys the button and blocks a second tap while its action runs (saving a bill twice is the worry). */
export function Button({ className = '', tone = 'primary', busy = false, disabled, children, ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { tone?: 'primary' | 'credit' | 'payment' | 'plain'; busy?: boolean }) {
  const tones = {
    primary: 'bg-brand text-white shadow-md shadow-teal-900/25 hover:bg-brand-strong active:bg-brand-strong',
    credit: 'bg-udhaar text-white shadow-md shadow-orange-900/25 active:bg-orange-800',
    payment: 'bg-jama text-white shadow-md shadow-emerald-900/25 active:bg-emerald-800',
    plain: 'bg-white text-ink border border-line hover:bg-[#f6faf9] active:bg-[#edf2f1]',
  }
  return (
    <button
      {...props}
      disabled={disabled || busy}
      aria-busy={busy || undefined}
      className={`min-h-12 rounded-xl px-4 text-lg font-bold transition active:scale-[0.98] disabled:opacity-45 disabled:shadow-none disabled:active:scale-100 ${tones[tone]} ${className}`}
    >
      {busy ? <span aria-hidden className="mr-2 inline-block h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent align-[-2px]" /> : null}
      {children}
    </button>
  )
}

export function Field({ label, ...props }: InputHTMLAttributes<HTMLInputElement> & { label: string }) {
  return (
    <label className="block">
      <span className="mb-1 block text-sm font-semibold text-muted">{label}</span>
      <input
        {...props}
        className="h-12 w-full rounded-2xl border-[1.5px] border-line bg-white px-3 text-lg outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10"
      />
    </label>
  )
}

/** Balance with colour and words: red "owes", green "advance". */
export function Balance({ amount, large = false }: { amount: number; large?: boolean }) {
  const { t } = useTranslation()
  const tone = amount > 0 ? 'text-due' : amount < 0 ? 'text-jama' : 'text-muted'
  const label = amount > 0 ? t('owes') : amount < 0 ? t('advance') : t('settled')
  return (
    <div className={`text-right ${tone}`}>
      <div className={large ? 'text-4xl font-bold' : 'text-lg font-bold'}>{formatRupees(Math.abs(amount))}</div>
      <div className="text-xs font-semibold">{label}</div>
    </div>
  )
}

/** A panel that slides up from the bottom: as wide as the app, never taller than the screen, with a close button. */
export function Sheet({ title, onClose, children }: { title: string; onClose: () => void; children: ReactNode }) {
  const { t } = useTranslation()
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])
  return (
    <div className="fixed inset-0 z-20 flex items-end justify-center bg-black/40 lg:items-center lg:p-6" onClick={onClose}>
      <div role="dialog" aria-modal="true" aria-label={title} className="max-h-[calc(100dvh-1.5rem)] w-full max-w-xl overflow-y-auto overscroll-contain rounded-t-3xl bg-ground p-4 pb-[calc(2rem+env(safe-area-inset-bottom))] lg:max-w-lg lg:rounded-3xl lg:p-5" onClick={(e) => e.stopPropagation()}>
        <div aria-hidden className="mx-auto -mt-1 mb-3 h-1.5 w-10 rounded-full bg-line lg:hidden" />
        <div className="mb-3 flex items-center justify-between gap-2">
          <h2 className="text-xl font-bold">{title}</h2>
          <button type="button" onClick={onClose} aria-label={t('a11y.close')} className="-mr-2 flex h-11 w-11 shrink-0 items-center justify-center rounded-full text-2xl text-muted hover:bg-white">×</button>
        </div>
        {children}
      </div>
    </div>
  )
}

/**
 * A form that slides in from the right on a computer (the page stays visible behind it) and up from the bottom on a
 * phone. Escape or the dimmed area closes it.
 */
export function Drawer({ title, onClose, children }: { title: string; onClose: () => void; children: ReactNode }) {
  const { t } = useTranslation()
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [onClose])
  return (
    <div className="fixed inset-0 z-30 flex items-end justify-center bg-black/30 lg:items-stretch lg:justify-end print:hidden" onClick={onClose}>
      <div role="dialog" aria-modal="true" aria-label={title} onClick={(e) => e.stopPropagation()}
        className="flex max-h-[92dvh] w-full max-w-xl flex-col overflow-hidden rounded-t-3xl bg-ground shadow-2xl lg:h-full lg:max-h-none lg:max-w-xl lg:rounded-none">
        <div className="flex items-center justify-between gap-2 border-b border-line bg-white px-4 py-2">
          <h2 className="text-lg font-bold">{title}</h2>
          <button type="button" onClick={onClose} aria-label={t('a11y.close')} className="-mr-2 flex h-11 w-11 shrink-0 items-center justify-center rounded-full text-2xl text-muted hover:bg-[#f1f5f4]">×</button>
        </div>
        <div className="flex flex-col gap-3 overflow-y-auto overscroll-contain p-4 pb-[calc(1.5rem+env(safe-area-inset-bottom))]">{children}</div>
      </div>
    </div>
  )
}

export function PendingDot() {
  const { t } = useTranslation()
  return <span title={t('pending')} className="inline-block h-2.5 w-2.5 rounded-full bg-amber-500" />
}

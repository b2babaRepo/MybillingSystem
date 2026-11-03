import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { formatRupees } from '../lib/format'

export function Button({ className = '', tone = 'primary', ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { tone?: 'primary' | 'credit' | 'payment' | 'plain' }) {
  const tones = {
    primary: 'bg-teal-700 text-white active:bg-teal-800',
    credit: 'bg-udhaar text-white active:bg-orange-800',
    payment: 'bg-jama text-white active:bg-emerald-800',
    plain: 'bg-white text-ink border border-line active:bg-[#edf2f1]',
  }
  return (
    <button
      {...props}
      className={`min-h-12 rounded-2xl px-4 text-lg font-semibold disabled:opacity-40 ${tones[tone]} ${className}`}
    />
  )
}

export function Field({ label, ...props }: InputHTMLAttributes<HTMLInputElement> & { label: string }) {
  return (
    <label className="block">
      <span className="mb-1 block text-sm font-semibold text-muted">{label}</span>
      <input
        {...props}
        className="h-12 w-full rounded-[14px] border-[1.5px] border-line bg-white px-3 text-lg outline-none focus:border-teal-600"
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
  return (
    <div className="fixed inset-0 z-20 flex items-end justify-center bg-black/40" onClick={onClose}>
      <div role="dialog" aria-modal="true" aria-label={title} className="max-h-[calc(100dvh-1.5rem)] w-full max-w-xl overflow-y-auto overscroll-contain rounded-t-3xl bg-ground p-4 pb-[calc(2rem+env(safe-area-inset-bottom))]" onClick={(e) => e.stopPropagation()}>
        <div className="mb-3 flex items-center justify-between gap-2">
          <h2 className="text-xl font-bold">{title}</h2>
          <button type="button" onClick={onClose} aria-label={t('a11y.close')} className="-mr-2 flex h-11 w-11 shrink-0 items-center justify-center rounded-full text-2xl text-muted">×</button>
        </div>
        {children}
      </div>
    </div>
  )
}

export function PendingDot() {
  const { t } = useTranslation()
  return <span title={t('pending')} className="inline-block h-2.5 w-2.5 rounded-full bg-amber-500" />
}

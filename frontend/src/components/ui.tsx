import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { formatRupees } from '../lib/format'

export function Button({ className = '', tone = 'primary', ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { tone?: 'primary' | 'credit' | 'payment' | 'plain' }) {
  const tones = {
    primary: 'bg-teal-700 text-white active:bg-teal-800',
    credit: 'bg-rose-600 text-white active:bg-rose-700',
    payment: 'bg-emerald-600 text-white active:bg-emerald-700',
    plain: 'bg-white text-slate-800 border border-slate-300 active:bg-slate-100',
  }
  return (
    <button
      {...props}
      className={`min-h-12 rounded-xl px-4 text-lg font-semibold disabled:opacity-40 ${tones[tone]} ${className}`}
    />
  )
}

export function Field({ label, ...props }: InputHTMLAttributes<HTMLInputElement> & { label: string }) {
  return (
    <label className="block">
      <span className="mb-1 block text-sm font-medium text-slate-600">{label}</span>
      <input
        {...props}
        className="h-12 w-full rounded-xl border border-slate-300 bg-white px-3 text-lg outline-none focus:border-teal-600"
      />
    </label>
  )
}

/** Balance with colour and words: red "owes", green "advance". */
export function Balance({ amount, large = false }: { amount: number; large?: boolean }) {
  const { t } = useTranslation()
  const tone = amount > 0 ? 'text-rose-600' : amount < 0 ? 'text-emerald-600' : 'text-slate-500'
  const label = amount > 0 ? t('owes') : amount < 0 ? t('advance') : t('settled')
  return (
    <div className={`text-right ${tone}`}>
      <div className={large ? 'text-4xl font-bold' : 'text-lg font-bold'}>{formatRupees(Math.abs(amount))}</div>
      <div className="text-xs font-medium uppercase tracking-wide">{label}</div>
    </div>
  )
}

export function Sheet({ title, onClose, children }: { title: string; onClose: () => void; children: ReactNode }) {
  return (
    <div className="fixed inset-0 z-20 flex items-end bg-black/40" onClick={onClose}>
      <div className="w-full rounded-t-2xl bg-slate-50 p-4 pb-8" onClick={(e) => e.stopPropagation()}>
        <h2 className="mb-3 text-xl font-bold">{title}</h2>
        {children}
      </div>
    </div>
  )
}

export function PendingDot() {
  const { t } = useTranslation()
  return <span title={t('pending')} className="inline-block h-2.5 w-2.5 rounded-full bg-amber-500" />
}

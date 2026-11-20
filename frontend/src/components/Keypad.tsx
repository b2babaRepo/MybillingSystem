import { useEffect, useRef } from 'react'
import { pressKey } from '../lib/keypad'

const KEYS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '.', '0', '⌫']

/**
 * A big number pad: faster than the phone keyboard and never covers the buttons. With `keyboard`, a computer's keys
 * work too (digits, point, Backspace), except while a text box has the cursor.
 */
export function Keypad({ value, onChange, maxDecimals = 2, keyboard = false }: { value: string; onChange: (next: string) => void; maxDecimals?: number; keyboard?: boolean }) {
  const latest = useRef({ value, onChange })
  useEffect(() => { latest.current = { value, onChange } })
  useEffect(() => {
    if (!keyboard) return
    const onKey = (e: KeyboardEvent) => {
      if (e.ctrlKey || e.metaKey || e.altKey) return
      const target = e.target as HTMLElement | null
      if (target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.tagName === 'SELECT' || target.isContentEditable)) return
      const key = /^[0-9.]$/.test(e.key) ? e.key : e.key === 'Backspace' ? '⌫' : null
      if (!key) return
      e.preventDefault()
      latest.current.onChange(pressKey(latest.current.value, key, maxDecimals))
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [keyboard, maxDecimals])
  return (
    <div className="grid grid-cols-3 gap-2">
      {KEYS.map((key) => (
        <button
          key={key}
          type="button"
          onClick={() => onChange(pressKey(value, key, maxDecimals))}
          className="h-[58px] rounded-[14px] bg-white text-[26px] font-bold text-ink shadow-[0_1px_2px_rgba(16,32,29,0.08)] active:bg-[#edf2f1]"
        >
          {key}
        </button>
      ))}
    </div>
  )
}

import { pressKey } from '../lib/keypad'

const KEYS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '.', '0', '⌫']

/** A big number pad: faster than the phone keyboard and never covers the buttons. */
export function Keypad({ value, onChange, maxDecimals = 2 }: { value: string; onChange: (next: string) => void; maxDecimals?: number }) {
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

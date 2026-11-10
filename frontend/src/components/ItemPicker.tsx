import { useState } from 'react'
import type { Product } from '../lib/types'

/**
 * A box to find an item by typing: the matches show in a short list right under it, inside the page. (The browser's
 * own suggestion list grows to the whole item list and spills over the screen.) Tapping a match picks it.
 */
export function ItemPicker({ products, text, onText, onPick, placeholder, className = '' }: {
  products: Pick<Product, 'id' | 'name'>[]
  text: string
  onText: (text: string) => void
  onPick: (product: Pick<Product, 'id' | 'name'>) => void
  placeholder: string
  className?: string
}) {
  const [open, setOpen] = useState(false)
  const query = text.trim().toLowerCase()
  const matches = (query ? products.filter((p) => p.name.toLowerCase().includes(query)) : products).slice(0, 8)
  const exact = products.some((p) => p.name.toLowerCase() === query)
  return (
    <div className={`relative min-w-0 ${className}`}>
      <input value={text} placeholder={placeholder} aria-label={placeholder} autoComplete="off"
        onChange={(e) => { onText(e.target.value); setOpen(true) }} onFocus={() => setOpen(true)} onBlur={() => window.setTimeout(() => setOpen(false), 150)}
        className="h-12 w-full min-w-0 rounded-xl border border-line bg-white px-3" />
      {open && !exact && matches.length > 0 && (
        <ul role="listbox" className="absolute inset-x-0 top-full z-30 mt-1 max-h-64 overflow-auto rounded-xl border border-line bg-white shadow-lg">
          {matches.map((p) => (
            <li key={p.id}>
              <button type="button" role="option" aria-selected={false} onMouseDown={(e) => e.preventDefault()} onClick={() => { onPick(p); setOpen(false) }}
                className="flex min-h-11 w-full items-center px-3 text-left text-sm hover:bg-teal-50">
                {p.name}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

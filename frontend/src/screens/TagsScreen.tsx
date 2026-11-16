import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ean13Digits, ean13Modules } from '../lib/barcode'
import { useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'
import { variantLabel } from '../lib/styles'

/** One EAN-13 barcode as SVG bars with its digits under it. */
function Ean13({ code }: { code: string }) {
  const digits = ean13Digits(code)
  if (!digits) return <p className="text-center font-mono text-sm">{code}</p>
  const bits = ean13Modules(digits)
  return (
    <svg viewBox="0 0 113 40" className="w-full" role="img" aria-label={digits}>
      {bits.split('').map((b, i) => (b === '1' ? <rect key={i} x={9 + i} y={0} width={1} height={i < 3 || (i >= 45 && i < 50) || i >= 92 ? 34 : 30} fill="#000" /> : null))}
      <text x={3} y={39} fontSize={7} fontFamily="monospace">{digits[0]}</text>
      <text x={14} y={39} fontSize={7} fontFamily="monospace" letterSpacing={1.6}>{digits.slice(1, 7)}</text>
      <text x={60} y={39} fontSize={7} fontFamily="monospace" letterSpacing={1.6}>{digits.slice(7)}</text>
    </svg>
  )
}

/**
 * Price tags to print and stick on: the shop name, the item and its size/colour, MRP and price, and its barcode, so
 * the counter scans it into the bill. One tag per piece in stock to start with; change any count, then print.
 */
export function TagsScreen({ shopId, shopName, ids }: { shopId: string; shopName: string; ids: string[] }) {
  const { t } = useTranslation()
  const { products } = useProducts(shopId)
  const items = ids.map((id) => products.find((p) => p.id === id)).filter((p): p is NonNullable<typeof p> => !!p)
  const [counts, setCounts] = useState<Record<string, string>>({})
  const countOf = (id: string, stock: number) => Math.max(0, Math.floor(Number(counts[id] ?? Math.max(1, stock)) || 0))
  const tags = items.flatMap((p) => Array.from({ length: countOf(p.id, p.stock) }, () => p))

  return (
    <div className="flex flex-col gap-3 p-4">
      <section className="flex flex-col gap-2 rounded-card bg-white p-3 print:hidden">
        <h2 className="font-bold">{t('tags.title')}</h2>
        <p className="text-sm text-muted">{t('tags.hint')}</p>
        {items.map((p) => (
          <label key={p.id} className="flex items-center justify-between gap-2">
            <span className="min-w-0 flex-1 truncate">{p.name}{p.barcode ? '' : ` · ${t('tags.noBarcode')}`}</span>
            <input value={counts[p.id] ?? String(Math.max(1, p.stock))} onChange={(e) => setCounts({ ...counts, [p.id]: e.target.value })} inputMode="numeric"
              aria-label={`${t('tags.count')}: ${p.name}`} className="h-11 w-20 rounded-lg border border-line text-center" />
          </label>
        ))}
        <button onClick={() => window.print()} disabled={tags.length === 0} className="h-12 rounded-xl bg-brand font-bold text-white disabled:opacity-40">{t('tags.print', { count: tags.length })}</button>
      </section>
      <div className="grid grid-cols-3 gap-2 print:gap-1">
        {tags.map((p, i) => (
          <div key={`${p.id}-${i}`} className="flex break-inside-avoid flex-col gap-0.5 rounded border border-dashed border-gray-400 bg-white p-2 text-black">
            <span className="truncate text-[10px] font-semibold">{shopName}</span>
            <span className="truncate text-[12px] font-bold leading-tight">{p.styleName || p.name}</span>
            {variantLabel(p) && <span className="text-[12px] font-extrabold">{variantLabel(p)}</span>}
            <span className="text-[12px]">
              {p.mrp && p.mrp > p.sellingPrice ? <><s className="text-gray-500">MRP {formatRupees(p.mrp)}</s> </> : p.mrp ? <>MRP </> : null}
              <b className="text-[14px]">{formatRupees(p.sellingPrice)}</b>
            </span>
            {p.barcode && <Ean13 code={p.barcode} />}
          </div>
        ))}
      </div>
    </div>
  )
}

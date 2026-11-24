import { useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import QRCode from 'qrcode'
import { createPortal } from 'react-dom'
import { useTranslation } from 'react-i18next'
import { btn } from '../components/ui'
import { api } from '../lib/api'
import { code128Modules } from '../lib/barcode'
import { karat, tagCode, type JewelleryPiece } from '../lib/jewellery'

// Dumbbell (barbell) jewellery tags on a thermal roll: the printable flap of a common 55 × 13 mm tag. The size can be
// changed for other rolls. Weights and purity, not the price: the price moves with the day's rate.
const SIZES = [{ id: '55x13', w: 55, h: 13 }, { id: '60x15', w: 60, h: 15 }, { id: '50x12', w: 50, h: 12 }]

function Bars({ code }: { code: string }) {
  const bits = code128Modules(code)
  if (!bits) return <span className="font-mono">{code}</span>
  const quiet = 6
  const width = bits.length + quiet * 2
  return (
    <svg viewBox={`0 0 ${width} 30`} preserveAspectRatio="none" className="h-full w-full" role="img" aria-label={code}>
      {bits.split('').map((b, i) => (b === '1' ? <rect key={i} x={quiet + i} y={0} width={1.02} height={30} fill="#000" /> : null))}
    </svg>
  )
}

/** A hallmarked piece's HUID as a small QR on the tag: the customer scans it to read the code, then checks it in BIS CARE. */
function HuidQr({ huid, size }: { huid: string; size: number }) {
  const [src, setSrc] = useState<string | null>(null)
  useEffect(() => { let live = true; void QRCode.toDataURL(huid, { margin: 0, width: 160 }).then((d) => { if (live) setSrc(d) }); return () => { live = false } }, [huid])
  return src ? <img src={src} alt={`HUID ${huid}`} style={{ width: `${size}mm`, height: `${size}mm` }} className="shrink-0" /> : null
}

/** One tag: the left half with the item and weights, the right half with the tag number as a barcode the bill scans,
 * and the HUID as a QR beside it on hallmarked pieces. */
function Tag({ p, shopName, w, h }: { p: JewelleryPiece; shopName: string; w: number; h: number }) {
  const text = `${Math.min(2.2, h * 0.17)}mm`
  return (
    <div className="label-page flex overflow-hidden bg-white text-black" style={{ width: `${w}mm`, height: `${h}mm`, padding: '0.8mm 1.2mm', fontSize: text, lineHeight: 1.15 }}>
      <div className="flex w-1/2 min-w-0 flex-col justify-center">
        <b className="truncate">{p.name}</b>
        <span className="truncate">{p.metal === 'gold' ? karat(p.metal, p.purity) : `Ag ${p.purity}`} · G {p.gross.toFixed(3)}</span>
        <span className="truncate">{p.stone ? `St ${p.stone.toFixed(3)} · ` : ''}N {p.net.toFixed(3)}</span>
        <span className="truncate">{p.huid ? `HUID ${p.huid}` : shopName}</span>
      </div>
      <div className="flex w-1/2 min-w-0 items-center gap-1 pl-1">
        <div className="flex h-full min-w-0 flex-1 flex-col items-center justify-center">
          <div className="min-h-0 w-full flex-1"><Bars code={tagCode(p.tagNo)} /></div>
          <b>J-{p.tagNo}</b>
        </div>
        {p.huid && <HuidQr huid={p.huid} size={h - 1.6} />}
      </div>
    </div>
  )
}

export function JewelleryTagsScreen({ shopId, shopName, ids }: { shopId: string; shopName: string; ids: string[] }) {
  const { t } = useTranslation()
  const all = useQuery({ queryKey: [shopId, 'jewellery', 'pieces', 'all-for-tags'], queryFn: () => api<JewelleryPiece[]>('/v1/jewellery/pieces?status=all') }).data ?? []
  const pieces = ids.map((id) => all.find((p) => p.id === id)).filter((p): p is JewelleryPiece => !!p)
  const [size, setSize] = useState(() => { try { return localStorage.getItem('gk-jwl-tag') ?? '55x13' } catch { return '55x13' } })
  const s = SIZES.find((x) => x.id === size) ?? SIZES[0]
  const pageRule = `@page { size: ${s.w}mm ${s.h}mm; margin: 0 }`

  return (
    <div className="flex flex-col gap-3 p-4">
      <style>{`${pageRule} @media print { body > *:not(.gk-labels) { display: none !important; } .label-page { break-after: page; } .label-page:last-child { break-after: auto; } }`}</style>
      <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm print:hidden">
        <h1 className="text-lg font-extrabold">{t('jwl.tagsTitle')}</h1>
        <p className="text-sm text-muted">{t('jwl.tagsHint')}</p>
        <label className="flex flex-col gap-1 text-sm font-semibold">{t('jwl.tagSize')}
          <select value={size} onChange={(e) => { setSize(e.target.value); try { localStorage.setItem('gk-jwl-tag', e.target.value) } catch { /* private window */ } }} className="h-11 rounded-xl border border-line bg-white px-2">
            {SIZES.map((x) => <option key={x.id} value={x.id}>{x.w} × {x.h} mm</option>)}
          </select></label>
        <button onClick={() => window.print()} disabled={pieces.length === 0} className={`h-12 rounded-xl ${btn.solid}`}>{t('jwl.printTagsNow', { count: pieces.length })}</button>
      </section>
      <div className="flex flex-wrap gap-3 print:hidden [&_.label-page]:shadow [&_.label-page]:ring-1 [&_.label-page]:ring-gray-300">
        {pieces.map((p) => <Tag key={p.id} p={p} shopName={shopName} w={s.w} h={s.h} />)}
      </div>
      {createPortal(<div className="gk-labels hidden print:block">{pieces.map((p) => <Tag key={p.id} p={p} shopName={shopName} w={s.w} h={s.h} />)}</div>, document.body)}
    </div>
  )
}

import { btn } from '../components/ui'
import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { createPortal } from 'react-dom'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { code128Modules, ean13Digits, ean13Modules } from '../lib/barcode'
import { useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'
import { getSheet, LABEL_SHEETS, labelPageRule, pages, setSheet, type LabelSheet } from '../lib/labels'
import { variantLabel } from '../lib/styles'
import type { Product } from '../lib/types'

/**
 * A barcode as SVG bars stretched to the space it is given: EAN-13 for pack and shop codes (with its digits under it),
 * Code 128 for any other code, so whatever the item carries scans at the counter.
 */
function Bars({ code, showText }: { code: string; showText: boolean }) {
  const digits = ean13Digits(code)
  const bits = digits ? ean13Modules(digits) : code128Modules(code)
  if (!bits) return <p className="text-center font-mono text-[2.4mm]">{code}</p>
  // A quiet zone of 10 modules each side: scanners need the blank space to find the code.
  const quiet = 10
  const width = bits.length + quiet * 2
  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <svg viewBox={`0 0 ${width} 30`} preserveAspectRatio="none" className="min-h-0 w-full flex-1" role="img" aria-label={code}>
        {bits.split('').map((b, i) => (b === '1' ? <rect key={i} x={quiet + i} y={0} width={1.02} height={30} fill="#000" /> : null))}
      </svg>
      {showText && <p className="text-center font-mono text-[2.2mm] leading-none tracking-[0.3mm]">{digits ?? code}</p>}
    </div>
  )
}

export interface LabelFields { shop: boolean; name: boolean; mrp: boolean; price: boolean; code: boolean }

/** One label at its real size. */
function Label({ p, shopName, sheet, fields }: { p: Product | null; shopName: string; sheet: LabelSheet; fields: LabelFields }) {
  const box = { width: `${sheet.w}mm`, height: `${sheet.h}mm` }
  if (!p) return <div style={box} />
  // Text sized from the label's height, so the same layout fits 21 mm stickers and 34 mm ones.
  const small = `${Math.min(2.6, sheet.h * 0.1)}mm`
  const big = `${Math.min(3.4, sheet.h * 0.135)}mm`
  const variant = variantLabel(p)
  // 0 means "price not set yet": a label never says ₹0.
  fields = { ...fields, price: fields.price && p.sellingPrice > 0 }
  return (
    <div style={{ ...box, padding: '1.2mm 1.6mm' }} className="flex flex-col overflow-hidden bg-white leading-tight text-black">
      {fields.shop && <span style={{ fontSize: small }} className="truncate font-semibold">{shopName}</span>}
      {fields.name && <span style={{ fontSize: small }} className="truncate font-bold">{p.styleName || p.name}{variant ? ` · ${variant}` : ''}</span>}
      {(fields.mrp && p.mrp) || fields.price ? (
        <span style={{ fontSize: small }} className="truncate">
          {fields.mrp && p.mrp ? (fields.price && p.mrp > p.sellingPrice ? <s>MRP {formatRupees(p.mrp)}</s> : <>MRP {formatRupees(p.mrp)}</>) : null}
          {fields.price && (!fields.mrp || !p.mrp || p.mrp > p.sellingPrice) && <b style={{ fontSize: big }}> {formatRupees(p.sellingPrice)}</b>}
        </span>
      ) : null}
      {p.barcode ? <Bars code={p.barcode} showText={fields.code} /> : <span className="flex-1" />}
    </div>
  )
}

/** The sheets or roll labels as they print: A4 with the sheet's own margins, or one roll label per page. */
export function Sheets({ list, sheet, shopName, fields }: { list: (Product | null)[][]; sheet: LabelSheet; shopName: string; fields: LabelFields }) {
  return (
    <>
      {list.map((page, n) => (
        <div key={n} className="label-page bg-white" style={sheet.roll ? { width: `${sheet.w}mm`, height: `${sheet.h}mm` } : {
          width: '210mm', height: '297mm', paddingTop: `${sheet.top}mm`, paddingLeft: `${sheet.left}mm`, display: 'grid', alignContent: 'start',
          gridTemplateColumns: `repeat(${sheet.cols}, ${sheet.w}mm)`, gridAutoRows: `${sheet.h}mm`, columnGap: `${sheet.gapX}mm`, rowGap: `${sheet.gapY}mm`,
        }}>
          {page.map((p, i) => <Label key={i} p={p} shopName={shopName} sheet={sheet} fields={fields} />)}
        </div>
      ))}
    </>
  )
}

/**
 * Barcode labels to print and stick on: on A4 sticker sheets or a thermal label roll, with the shop's name, the item,
 * MRP and price, and its barcode, so the counter scans it into the bill. Items without a code get a shop code first.
 * One label per piece in stock to start with; change any count, add more items, then print.
 */
export function TagsScreen({ shopId, shopName, ids }: { shopId: string; shopName: string; ids: string[] }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { products } = useProducts(shopId)
  const [picked, setPicked] = useState<string[]>(ids)
  const [search, setSearch] = useState('')
  const [counts, setCounts] = useState<Record<string, string>>({})
  const [sheet, chooseSheet] = useState<LabelSheet>(getSheet)
  const [skip, setSkip] = useState('0')
  const [fields, setFields] = useState<LabelFields>({ shop: true, name: true, mrp: true, price: true, code: true })
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')

  const items = picked.map((id) => products.find((p) => p.id === id)).filter((p): p is Product => !!p)
  const countOf = (id: string, stock: number) => Math.max(0, Math.min(500, Math.floor(Number(counts[id] ?? Math.max(1, stock)) || 0)))
  const labels = items.flatMap((p) => Array.from({ length: countOf(p.id, p.stock) }, () => p))
  const list = pages(labels, sheet, Number(skip) || 0)
  const noCode = items.filter((p) => !p.barcode)
  const matches = search.trim().length < 2 ? [] : products.filter((p) => !picked.includes(p.id) && p.name.toLowerCase().includes(search.trim().toLowerCase())).slice(0, 8)

  async function makeCodes() {
    setBusy(true)
    setMessage('')
    let made = 0
    try {
      for (const p of noCode) { await api(`/v1/products/${p.id}/barcode/new`, 'POST', {}); made++ }
      setMessage(t('labels.made', { count: made }))
    } catch (err) {
      setMessage(`${t('labels.made', { count: made })} ${err instanceof Error ? err.message : t('error')}`)
    } finally {
      await queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
      setBusy(false)
    }
  }

  const toggle = (key: keyof LabelFields) => (
    <label key={key} className="flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={fields[key]} onChange={(e) => setFields({ ...fields, [key]: e.target.checked })} className="h-5 w-5" />{t(`labels.field.${key}`)}</label>
  )

  return (
    <div className="flex flex-col gap-3 p-4">
      <style>{`${labelPageRule(sheet)} @media print { body > *:not(.gk-labels) { display: none !important; } .label-page { break-after: page; } .label-page:last-child { break-after: auto; } }`}</style>
      <section className="flex flex-col gap-2 rounded-card bg-white p-3 print:hidden">
        <h2 className="font-bold">{t('labels.title')}</h2>
        <p className="text-sm text-muted">{t('labels.hint')}</p>
        <label className="flex flex-col gap-1 text-sm font-semibold">{t('labels.sheet')}
          <select value={sheet.id} onChange={(e) => { const next = LABEL_SHEETS.find((s) => s.id === e.target.value)!; setSheet(next.id); chooseSheet(next) }} className="h-11 rounded-xl border border-line bg-white px-2">
            {LABEL_SHEETS.map((s) => <option key={s.id} value={s.id}>{t(`labels.sheets.${s.id}`)}</option>)}
          </select>
        </label>
        {!sheet.roll && (
          <label className="flex items-center justify-between gap-2 text-sm">{t('labels.skip')}
            <input value={skip} onChange={(e) => setSkip(e.target.value)} inputMode="numeric" className="h-11 w-20 rounded-lg border border-line text-center" /></label>
        )}
        <div className="flex flex-wrap gap-x-4">{(['shop', 'name', 'mrp', 'price', 'code'] as const).map(toggle)}</div>
      </section>

      <section className="flex flex-col gap-2 rounded-card bg-white p-3 print:hidden">
        {items.map((p) => (
          <div key={p.id} className="flex items-center justify-between gap-2">
            <span className="min-w-0 flex-1 truncate">{p.name}{variantLabel(p) ? ` · ${variantLabel(p)}` : ''}{p.barcode ? '' : <span className="text-due"> · {t('tags.noBarcode')}</span>}</span>
            <input value={counts[p.id] ?? String(Math.max(1, p.stock))} onChange={(e) => setCounts({ ...counts, [p.id]: e.target.value })} inputMode="numeric"
              aria-label={`${t('tags.count')}: ${p.name}`} className="h-11 w-20 rounded-lg border border-line text-center" />
            <button onClick={() => setPicked(picked.filter((id) => id !== p.id))} aria-label={`${t('labels.remove')}: ${p.name}`} className="h-11 w-11 rounded-lg border border-line">✕</button>
          </div>
        ))}
        <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('labels.add')} aria-label={t('labels.add')} className="h-11 rounded-xl border border-line px-3" />
        {matches.map((p) => (
          <button key={p.id} onClick={() => { setPicked([...picked, p.id]); setSearch('') }} className="min-h-11 rounded-lg px-2 text-left hover:bg-[#f6faf9]">+ {p.name}{variantLabel(p) ? ` · ${variantLabel(p)}` : ''}</button>
        ))}
        {noCode.length > 0 && (
          <button disabled={busy} onClick={() => void makeCodes()} className={`min-h-11 px-3 rounded-xl ${btn.outline}`}>{t('labels.makeCodes', { count: noCode.length })}</button>
        )}
        {message && <p role="status" className="text-sm font-semibold">{message}</p>}
        <button onClick={() => window.print()} disabled={labels.length === 0} className={`h-12 rounded-xl ${btn.solid}`}>
          {t('labels.print', { count: labels.length, pages: list.length })}</button>
        <p className="text-xs text-muted">{t(sheet.roll ? 'labels.rollTip' : 'labels.sheetTip')}</p>
      </section>

      {/* Preview at real size; the print copy goes straight onto the page, outside the app's layout. */}
      <div className="overflow-x-auto print:hidden">
        <div className="flex w-max flex-col gap-3 [&_.label-page]:shadow [&_.label-page]:ring-1 [&_.label-page]:ring-gray-300">
          <Sheets list={list.slice(0, 3)} sheet={sheet} shopName={shopName} fields={fields} />
        </div>
        {list.length > 3 && <p className="mt-2 text-sm text-muted">{t('labels.morePages', { count: list.length - 3 })}</p>}
      </div>
      {createPortal(<div className="gk-labels hidden print:block"><Sheets list={list} sheet={sheet} shopName={shopName} fields={fields} /></div>, document.body)}
    </div>
  )
}

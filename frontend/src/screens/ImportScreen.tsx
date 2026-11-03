import { navigate } from '../router'
import { STARTER_TYPES, useShopSettings, type ShopType } from '../lib/reports'
import { useQueryClient } from '@tanstack/react-query'
import { useState, type ChangeEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { api } from '../lib/api'
import { parseCsv, STARTER_CATALOGUE, type ImportRow } from '../lib/catalogue'

/**
 * Loading a shop's items fast (the plan's target: about 300 in under an hour). Paste from Excel, pick a CSV,
 * or start from the common kirana list. Names the shop already has are skipped, so importing twice is safe.
 */
const PLACEHOLDER: Partial<Record<ShopType, string>> = {
  building_materials: 'Cement OPC 53 (50 kg), bag, 385, 360, Cement, 100, 20\nसरिया 12 mm, किलो, 62',
  paint: 'Wall Emulsion 20 L, piece, 4200, 3900, Paint, 5, 2\nप्राइमर 4 L, piece, 650',
  hardware: 'Door hinge 4 inch, pair, 45, 35, Hinges, 50, 10\nपेंच 1 इंच, piece, 2',
  woodwork: 'Plywood 18 mm 8x4, piece, 2400, 2000, Plywood, 20, 5\nबीडिंग पट्टी, running_ft, 12',
  electrical: 'LED bulb 9 W, piece, 120, 90, Lights, 50, 10\nतार 1.5 sq mm, metre, 18',
  other: 'Item name, piece, 100, 80, Category, 10, 2',
}

export function ImportScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [text, setText] = useState('')
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const parsed = parseCsv(text)
  const type = useShopSettings(shopId).data?.type ?? 'kirana'
  const hasTradeList = type !== 'kirana' && STARTER_TYPES.includes(type)

  async function upload(rows: ImportRow[]) {
    setBusy(true)
    setMessage('')
    try {
      const result = await api<{ created: number; skipped: number }>('/v1/products/import', 'POST', { rows })
      setMessage(t('stock.imported', result))
      setText('')
      void queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('error'))
    } finally {
      setBusy(false)
    }
  }

  // A trade's own starter list, with GST and HSN, comes from the server.
  async function addTradeList() {
    setBusy(true)
    setMessage('')
    try {
      const result = await api<{ created: number; skipped: number }>('/v1/products/starter', 'POST', {})
      setMessage(t('stock.imported', result))
      void queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('error'))
    } finally {
      setBusy(false)
    }
  }

  async function readFile(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (file) setText(await file.text())
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      {message && <p className="rounded-xl bg-emerald-50 p-3 font-semibold text-emerald-800">{message}</p>}
      <Button onClick={() => navigate('/items/pick')}>{t('pick.title')}</Button>

      {type === 'kirana' && (
      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
        <h2 className="text-base font-bold">{t('stock.starter')}</h2>
        <p className="text-sm text-muted">{t('stock.starterHint', { count: STARTER_CATALOGUE.length })}</p>
        <Button disabled={busy} onClick={() => upload(STARTER_CATALOGUE)}>{t('stock.starter')}</Button>
      </section>
      )}
      {hasTradeList && (
        <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
          <h2 className="text-base font-bold">{t(`stock.tradeStarter.${type}`)}</h2>
          <p className="text-sm text-muted">{t('stock.tradeStarterHint')}</p>
          <Button disabled={busy} onClick={() => void addTradeList()}>{t(`stock.tradeStarter.${type}`)}</Button>
        </section>
      )}

      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
        <h2 className="text-base font-bold">{t('stock.import')}</h2>
        <p className="text-sm text-muted">{t('stock.importHint')}</p>
        <label className="flex min-h-12 cursor-pointer items-center justify-center rounded-2xl border border-dashed border-line font-semibold text-teal-700">
          {t('stock.chooseFile')}
          <input type="file" accept=".csv,text/csv,text/plain" onChange={readFile} className="sr-only" />
        </label>
        <textarea
          value={text}
          onChange={(e) => setText(e.target.value)}
          rows={8}
          aria-label={t('stock.import')}
          placeholder={PLACEHOLDER[type] ?? 'Aashirvaad Atta 5kg, pkt, 245, 230, Atta & rice, 12, 4\nचीनी, किलो, 48'}
          className="rounded-[14px] border border-line p-3 font-mono text-sm"
        />
        {parsed.errors.map((e) => (
          <p key={e.row} className="text-sm text-due">{t('stock.badUnit', { row: e.row, unit: e.unit })}</p>
        ))}
        {parsed.rows.length > 0 && <p className="text-sm font-semibold">{t('stock.rowsReady', { count: parsed.rows.length })}</p>}
        <Button disabled={busy || parsed.rows.length === 0 || parsed.errors.length > 0} onClick={() => upload(parsed.rows)}>
          {t('stock.importNow', { count: parsed.rows.length })}
        </Button>
      </section>
    </div>
  )
}

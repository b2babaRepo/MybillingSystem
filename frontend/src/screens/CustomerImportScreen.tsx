import { useQueryClient } from '@tanstack/react-query'
import { useState, type ChangeEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { parseParties } from '../lib/partyImport'

/**
 * Moving to the app with customers already on credit: paste from Excel or pick a CSV, see exactly what will go in, then
 * import. A customer already here is left alone, so importing twice changes nothing.
 */
export function CustomerImportScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [text, setText] = useState('')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const [problems, setProblems] = useState<string[]>([])
  const { rows, errors } = parseParties(text)
  const owed = rows.reduce((sum, r) => sum + Math.max(r.balance, 0), 0)
  const advances = rows.reduce((sum, r) => sum + Math.max(-r.balance, 0), 0)

  async function pick(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (file) setText(await file.text())
  }

  async function upload() {
    setBusy(true)
    setMessage('')
    setProblems([])
    try {
      const result = await api<{ created: number; skipped: number; totalOwed: number; problems: string[] }>('/v1/customers/import', 'POST', { rows })
      setMessage(t('partyImport.done', { created: result.created, skipped: result.skipped, owed: formatRupees(result.totalOwed) }))
      setProblems(result.problems)
      setText('')
      await queryClient.invalidateQueries({ queryKey: [shopId, 'customers'] })
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('error'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      <h1 className="text-lg font-bold">{t('partyImport.title')}</h1>
      <p className="text-sm text-muted">{t('partyImport.hint')}</p>
      <textarea
        aria-label={t('partyImport.title')}
        value={text}
        onChange={(e) => setText(e.target.value)}
        rows={8}
        placeholder={'Name, Phone, Balance\nRamesh Kumar, 9876543210, 1200\nसुरेश, , 300 Cr'}
        className="w-full rounded-xl border border-line bg-white p-3 text-[15px]"
      />
      <label className="flex min-h-11 cursor-pointer items-center justify-center rounded-xl border border-line bg-white font-semibold">
        {t('partyImport.file')}
        <input type="file" accept=".csv,.txt,text/csv,text/plain" className="sr-only" onChange={(e) => void pick(e)} />
      </label>

      {rows.length > 0 && (
        <section className="flex flex-col gap-1 rounded-[18px] bg-white p-4">
          <p className="font-bold">{t('partyImport.summary', { count: rows.length, owed: formatRupees(owed) })}</p>
          {advances > 0 && <p className="text-sm">{t('partyImport.advances', { amount: formatRupees(advances) })}</p>}
          <p className="text-sm font-semibold text-udhaar">{t('partyImport.checkSign')}</p>
          <ul className="mt-1 divide-y divide-[#edf2f1]">
            {rows.slice(0, 8).map((r, i) => (
              <li key={i} className="flex justify-between gap-2 py-1.5 text-sm">
                <span className="min-w-0 truncate">{r.name}{r.phone ? ` · ${r.phone}` : ''}</span>
                <span className={`shrink-0 font-semibold ${r.balance < 0 ? 'text-teal-700' : ''}`}>{r.balance < 0 ? t('partyImport.advance', { amount: formatRupees(-r.balance) }) : formatRupees(r.balance)}</span>
              </li>
            ))}
          </ul>
          {rows.length > 8 && <p className="text-xs text-muted">{t('partyImport.more', { count: rows.length - 8 })}</p>}
        </section>
      )}
      {errors.length > 0 && <p className="text-sm font-semibold text-due">{t('partyImport.badLines', { lines: errors.slice(0, 10).join(', ') })}</p>}
      <Button disabled={busy || rows.length === 0} onClick={() => void upload()}>{t('partyImport.now', { count: rows.length })}</Button>
      {message && <p className="rounded-xl bg-emerald-50 p-3 font-semibold text-emerald-800">{message}</p>}
      {problems.length > 0 && <ul className="text-sm text-due">{problems.slice(0, 20).map((p) => <li key={p}>{p}</li>)}</ul>}
    </div>
  )
}

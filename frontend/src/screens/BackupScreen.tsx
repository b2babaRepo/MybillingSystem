import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type ChangeEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { shownLocale } from '../i18n'
import { Button } from '../components/ui'
import { api } from '../lib/api'
import { chunks, daysSince, parseBackup, type Backup } from '../lib/backup'
import { formatRupees } from '../lib/format'

export interface BackupStatus { lastBackupAt: string | null; empty: boolean }

export const useBackupStatus = (shopId: string) =>
  useQuery({ queryKey: [shopId, 'backup-status'], queryFn: () => api<BackupStatus>('/v1/shop/backup-status'), staleTime: 60_000, retry: false })

/**
 * Backup and restore. Everything is already kept on our server, so a backup is the shop's own copy: one file with all
 * of it, to keep on the phone, in Drive or with the CA. Restore brings a backup into a new, empty shop (a second
 * account, or after starting over): customers and suppliers with what they owe today, and items with their stock.
 * It never writes over a shop that already has records.
 */
export function BackupScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const status = useBackupStatus(shopId)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const [file, setFile] = useState<Backup | null>(null)
  const [fileError, setFileError] = useState('')
  const [problems, setProblems] = useState<string[]>([])
  const days = daysSince(status.data?.lastBackupAt ?? null)

  async function download() {
    setBusy(true)
    setMessage('')
    try {
      const data = await api<{ shop?: { slug?: string } }>('/v1/shop/export')
      const blob = new Blob([JSON.stringify(data, null, 1)], { type: 'application/json' })
      const name = `${data.shop?.slug ?? 'shop'}-backup-${new Date().toISOString().slice(0, 10)}.json`
      const backupFile = new File([blob], name, { type: 'application/json' })
      // On a phone: straight to Drive, WhatsApp (to yourself) or Files. Elsewhere: Downloads.
      if (navigator.canShare?.({ files: [backupFile] })) {
        try { await navigator.share({ files: [backupFile], title: name }) } catch { /* closed the share sheet: the backup is still taken */ }
      } else {
        const url = URL.createObjectURL(blob)
        const a = document.createElement('a')
        a.href = url
        a.download = name
        a.click()
        setTimeout(() => URL.revokeObjectURL(url), 10_000)
      }
      setMessage(t('backup.done'))
      await status.refetch()
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('error'))
    } finally {
      setBusy(false)
    }
  }

  async function pick(e: ChangeEvent<HTMLInputElement>) {
    const picked = e.target.files?.[0]
    e.target.value = ''
    setFile(null); setFileError(''); setMessage(''); setProblems([])
    if (!picked) return
    const parsed = parseBackup(await picked.text())
    if ('error' in parsed) setFileError(t(parsed.error))
    else setFile(parsed)
  }

  async function restore() {
    if (!file) return
    setBusy(true)
    setMessage('')
    const found: string[] = []
    const done = { suppliers: 0, customers: 0, items: 0 }
    try {
      for (const rows of chunks(file.suppliers)) {
        const r = await api<{ created: number; problems: string[] }>('/v1/suppliers/import', 'POST', { rows })
        done.suppliers += r.created; found.push(...r.problems)
      }
      for (const rows of chunks(file.customers)) {
        const r = await api<{ created: number; problems: string[] }>('/v1/customers/import', 'POST', { rows })
        done.customers += r.created; found.push(...r.problems)
      }
      for (const rows of chunks(file.products, 500)) {
        // One bad item stops only its own batch of 500: the rest still come in.
        try { done.items += (await api<{ created: number }>('/v1/products/import', 'POST', { rows })).created } catch (err) {
          found.push(`${t('backup.itemsFailed', { from: rows[0].name, to: rows[rows.length - 1].name })} ${err instanceof Error ? err.message : ''}`)
        }
      }
      setMessage(t('backup.restored', done))
      setFile(null)
    } catch (err) {
      setMessage(`${t('backup.restored', done)} ${err instanceof Error ? err.message : t('error')}`)
    } finally {
      setProblems(found)
      setBusy(false)
      await queryClient.invalidateQueries({ queryKey: [shopId] })
    }
  }

  const owed = file?.customers.reduce((s, c) => s + Math.max(c.balance, 0), 0) ?? 0
  const toSuppliers = file?.suppliers.reduce((s, c) => s + Math.max(c.balance, 0), 0) ?? 0

  return (
    <div className="flex flex-col gap-4 p-4">
      <section className="flex flex-col gap-2 rounded-card bg-white p-4">
        <h1 className="text-lg font-bold">{t('backup.title')}</h1>
        <p className={`rounded-xl p-3 text-sm font-semibold ${days == null || days > 7 ? 'bg-warn-soft text-warn-ink' : 'bg-emerald-50 text-emerald-800'}`}>
          {status.isPending ? '…' : days == null ? t('backup.never')
            : t('backup.last', { date: new Date(status.data!.lastBackupAt!).toLocaleString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric', hour: 'numeric', minute: '2-digit' }), count: days })}
        </p>
        <p className="text-sm text-muted">{t('backup.hint')}</p>
        <Button disabled={busy} onClick={() => void download()}>{t('backup.now')}</Button>
      </section>

      <section className="flex flex-col gap-2 rounded-card bg-white p-4">
        <h2 className="font-bold">{t('backup.restoreTitle')}</h2>
        <p className="text-sm text-muted">{t('backup.restoreHint')}</p>
        {status.data && !status.data.empty ? (
          <p className="rounded-xl bg-warn-soft p-3 text-sm font-semibold text-warn-ink">{t('backup.notEmpty')}</p>
        ) : (
          <label className="flex min-h-11 cursor-pointer items-center justify-center rounded-xl border border-line bg-white font-semibold">
            {t('backup.pick')}
            <input type="file" accept=".json,application/json" className="sr-only" onChange={(e) => void pick(e)} />
          </label>
        )}
        {fileError && <p className="text-sm font-semibold text-due">{fileError}</p>}
        {file && (
          <div className="flex flex-col gap-1 rounded-xl border border-line p-3 text-sm">
            <p className="font-bold">{t('backup.fileOf', { shop: file.shopName, date: file.exportedAt ? new Date(file.exportedAt).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric' }) : '?' })}</p>
            <p>{t('backup.willAdd', { customers: file.customers.length, owed: formatRupees(owed), suppliers: file.suppliers.length, toSuppliers: formatRupees(toSuppliers), items: file.products.length })}</p>
            <p className="text-muted">{t('backup.stays', file.history)}</p>
            <Button disabled={busy || file.customers.length + file.suppliers.length + file.products.length === 0} onClick={() => void restore()}>{t('backup.restoreNow')}</Button>
          </div>
        )}
      </section>

      {message && <p role="status" className="rounded-xl bg-emerald-50 p-3 font-semibold text-emerald-800">{message}</p>}
      {problems.length > 0 && <ul className="text-sm text-due">{problems.slice(0, 20).map((p) => <li key={p}>{p}</li>)}</ul>}
    </div>
  )
}

import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { api } from '../lib/api'
import { decimalText } from '../lib/decimalText'

/** Owner only: how much staff may take off a tagged piece's making or wastage on a bill. */
export function StaffCapSetting({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const qc = useQueryClient()
  const current = useQuery({ queryKey: [shopId, 'jewellery', 'settings'], queryFn: () => api<{ maxOffPercent: number | null; showOnline: boolean }>('/v1/jewellery/settings') }).data
  const [draft, setDraft] = useState<string | null>(null)
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const value = draft ?? (current?.maxOffPercent != null ? String(current.maxOffPercent) : '')

  async function save() {
    setBusy(true); setMessage('')
    try {
      await api('/v1/jewellery/settings', 'PUT', { maxOffPercent: value === '' ? null : Number(value) })
      await qc.invalidateQueries({ queryKey: [shopId, 'jewellery', 'settings'] })
      setDraft(null); setMessage(t('jwl5.capSaved'))
    } catch (e) { setMessage(e instanceof Error ? e.message : t('error')) } finally { setBusy(false) }
  }

  return (
    <div className="flex flex-col gap-2 rounded-xl border border-line p-3">
      <b>{t('jwl5.capTitle')}</b>
      <p className="text-sm text-muted">{t('jwl5.capHint')}</p>
      <div className="flex flex-wrap items-center gap-2">
        <label className="flex items-center gap-1"><input aria-label={t('jwl5.capTitle')} value={value} onChange={(e) => setDraft(decimalText(e.target.value))} inputMode="decimal" placeholder="—"
          className="h-11 w-24 rounded-xl border border-line bg-white px-3 text-right outline-none focus:border-brand-line" /> %</label>
        <Button className="px-6" busy={busy} onClick={() => void save()}>{t('jwl5.capSave')}</Button>
        {message && <span role="status" className="text-sm font-semibold text-brand-deep">{message}</span>}
      </div>
      <label className="mt-2 flex min-h-11 items-start gap-2 border-t border-line pt-3 text-sm">
        <input type="checkbox" checked={current?.showOnline ?? false} disabled={!current || busy} className="mt-0.5 h-5 w-5"
          onChange={(e) => { const show = e.target.checked; setBusy(true); void api('/v1/jewellery/settings/show-online', 'PUT', { show }).then(() => qc.invalidateQueries({ queryKey: [shopId, 'jewellery', 'settings'] })).finally(() => setBusy(false)) }} />
        <span><b>{t('jwl5.showOnline')}</b><span className="block text-muted">{t('jwl5.showOnlineHint')}</span></span>
      </label>
    </div>
  )
}

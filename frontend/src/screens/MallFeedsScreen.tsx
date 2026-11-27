import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { controlSm } from '../components/ui'
import { btn } from '../lib/buttonStyles'
import { confirmAsk } from '../lib/ask'
import { shownLocale } from '../i18n'
import { api } from '../lib/api'

interface Feed { id: string; name: string; keyPrefix: string; createdAt: string; revokedAt: string | null; lastUsedAt: string | null; pushUrl: string | null; lastPushedDay: string | null; lastPushError: string | null }

const when = (iso: string) => new Date(iso).toLocaleString(shownLocale(), { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })

/**
 * Sales feeds for a mall: a key that reads this shop's sales and nothing else (no costs, no customers), for the mall
 * or its sales-reporting company; and, if they give an address, each day's figures sent there after 2 am.
 */
export function MallFeedsScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const feeds = useQuery({ queryKey: [shopId, 'mall-feeds'], queryFn: () => api<Feed[]>('/v1/mall-feeds'), retry: false })
  const [form, setForm] = useState({ name: '', pushUrl: '' })
  const [newKey, setNewKey] = useState<string | null>(null)
  const [message, setMessage] = useState('')

  async function run(work: () => Promise<void>) {
    setMessage('')
    try {
      await work()
      await feeds.refetch()
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  const create = () => run(async () => {
    const made = await api<{ key: string }>('/v1/mall-feeds', 'POST', { name: form.name.trim(), pushUrl: form.pushUrl.trim() || null })
    setNewKey(made.key)
    setForm({ name: '', pushUrl: '' })
  })
  const revoke = async (feed: Feed) => {
    if (await confirmAsk(t('mall.revokeConfirm', { name: feed.name }))) void run(() => api(`/v1/mall-feeds/${feed.id}/revoke`, 'POST'))
  }
  const base = `${location.origin}/api/mall/v1`

  return (
    <div className="flex flex-col gap-3 p-4">
      <p className="text-sm text-muted">{t('mall.hint')}</p>
      {newKey && (
        <section className="flex flex-col gap-2 rounded-card border-2 border-brand bg-white p-4 text-sm">
          <b>{t('mall.keyOnce')}</b>
          <code className="break-all rounded-lg bg-[#f1f5f4] p-2 font-mono">{newKey}</code>
          <button type="button" onClick={() => void navigator.clipboard?.writeText(newKey)} className={`min-h-11 self-start rounded-xl px-4 ${btn.outline}`}>{t('mall.copy')}</button>
        </section>
      )}
      <section className="flex flex-col gap-2 rounded-card bg-white p-4">
        <input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} maxLength={80} placeholder={t('mall.name')} aria-label={t('mall.name')} className={controlSm} />
        <input value={form.pushUrl} onChange={(e) => setForm({ ...form, pushUrl: e.target.value })} maxLength={300} placeholder={t('mall.pushUrl')} aria-label={t('mall.pushUrl')} inputMode="url" className={controlSm} />
        <button type="button" disabled={form.name.trim().length < 2} onClick={() => void create()} className={`min-h-12 rounded-xl ${btn.solid}`}>{t('mall.create')}</button>
      </section>
      {message && <p className="text-sm text-due">{message}</p>}
      <ul className="flex flex-col gap-2">
        {feeds.data?.map((f) => (
          <li key={f.id} className="flex flex-col gap-1 rounded-card bg-white p-3 text-sm">
            <p className="flex flex-wrap items-center justify-between gap-2">
              <b>{f.name}</b>
              <span className="font-mono text-muted">{f.keyPrefix}…</span>
            </p>
            <p className="text-muted">
              {f.revokedAt ? t('mall.stopped', { when: when(f.revokedAt) }) : f.lastUsedAt ? t('mall.lastRead', { when: when(f.lastUsedAt) }) : t('mall.neverRead')}
              {f.pushUrl && !f.revokedAt ? ` · ${f.lastPushError ? t('mall.pushFailed', { error: f.lastPushError }) : f.lastPushedDay ? t('mall.pushed', { day: f.lastPushedDay }) : t('mall.pushWaiting')}` : ''}
            </p>
            {!f.revokedAt && <button type="button" onClick={() => void revoke(f)} className={`min-h-11 self-start text-sm ${btn.dangerOutline}`}>{t('mall.revoke')}</button>}
          </li>
        ))}
      </ul>
      <details className="rounded-card bg-white p-3 text-sm">
        <summary className="font-semibold">{t('mall.forMall')}</summary>
        <p className="mt-2 text-muted">{t('mall.howTo')}</p>
        <pre className="mt-2 overflow-x-auto rounded-lg bg-[#f1f5f4] p-2 text-xs">{`GET ${base}/daily?from=2026-11-01&to=2026-11-30
GET ${base}/bills?date=2026-11-26
GET ${base}/bills?date=2026-11-26&format=csv
Header: X-Mall-Key: gkm_…`}</pre>
      </details>
    </div>
  )
}

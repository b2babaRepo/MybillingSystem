import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { useOutbox } from '../lib/data'
import { formatDateTime } from '../lib/format'
import { discardFailed, flush, retryFailed } from '../lib/outbox'

/** Everything still on this phone only, and anything the server refused, so nothing is lost silently. */
export function SyncScreen({ shopId, persistent }: { shopId: string; persistent: boolean }) {
  const { t } = useTranslation()
  const { ops, state, pending, failed } = useOutbox(shopId)

  return (
    <div className="space-y-3 p-4">
      {!persistent && <p className="rounded-xl bg-amber-50 p-3 text-amber-800">{t('storageWarning')}</p>}
      <div className="rounded-xl bg-white p-4 shadow-sm">
        <p className="text-lg font-semibold">
          {state === 'syncing' ? t('syncing') : pending + failed === 0 ? t('allSynced') : t('waiting', { count: pending })}
        </p>
        {state === 'offline' && <p className="text-slate-500">{t('offline')}</p>}
        {state === 'signed-out' && <p className="text-rose-600">{t('signedOutSync')}</p>}
        <div className="mt-3 flex gap-2">
          <Button onClick={() => void flush(shopId)} disabled={pending === 0}>{t('syncNow')}</Button>
          {failed > 0 && <Button tone="plain" onClick={() => void retryFailed(shopId)}>{t('retry')}</Button>}
        </div>
      </div>
      <ul className="divide-y divide-slate-200 rounded-xl bg-white shadow-sm">
        {ops.map((op) => (
          <li key={op.seq} className="flex items-center gap-3 p-3">
            <div className="flex-1">
              <div className="font-semibold">{op.label}</div>
              <div className="text-xs text-slate-500">{formatDateTime(op.createdAt)}</div>
              {op.status === 'failed' && (
                <div className="text-sm text-rose-600">
                  {t('failed')}: {op.error}
                </div>
              )}
            </div>
            {op.status === 'failed' && (
              <button className="text-sm text-rose-600 underline" onClick={() => void discardFailed(op.seq!)}>
                {t('discard')}
              </button>
            )}
          </li>
        ))}
      </ul>
    </div>
  )
}

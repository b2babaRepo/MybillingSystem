import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { counterName, setCounterName, useOutbox } from '../lib/data'
import { useState } from 'react'
import { formatDateTime } from '../lib/format'
import { discardFailed, flush, retryFailed } from '../lib/outbox'

/** Everything still on this phone only, and anything the server refused, so nothing is lost silently. */
export function SyncScreen({ shopId, persistent }: { shopId: string; persistent: boolean }) {
  const { t } = useTranslation()
  const { ops, state, pending, failed } = useOutbox(shopId)

  return (
    <div className="space-y-3 p-4">
      {!persistent && <p className="rounded-xl bg-amber-50 p-3 text-amber-800">{t('storageWarning')}</p>}
      <CounterName />
      <div className="rounded-[18px] bg-white p-4">
        <p className="text-lg font-semibold">
          {state === 'syncing' ? t('syncing') : pending + failed === 0 ? t('allSynced') : t('waiting', { count: pending })}
        </p>
        {state === 'offline' && <p className="text-muted">{t('offline')}</p>}
        {state === 'signed-out' && <p className="text-due">{t('signedOutSync')}</p>}
        <div className="mt-3 flex gap-2">
          <Button onClick={() => void flush(shopId)} disabled={pending === 0}>{t('syncNow')}</Button>
          {failed > 0 && <Button tone="plain" onClick={() => void retryFailed(shopId)}>{t('retry')}</Button>}
        </div>
      </div>
      <ul className="divide-y divide-[#edf2f1] rounded-[18px] bg-white">
        {ops.map((op) => (
          <li key={op.seq} className="flex items-center gap-3 p-3">
            <div className="flex-1">
              <div className="font-semibold">{op.label}</div>
              <div className="text-xs text-muted">{formatDateTime(op.createdAt)}</div>
              {op.status === 'failed' && (
                <div className="text-sm text-due">
                  {t('failed')}: {op.error}
                </div>
              )}
            </div>
            {op.status === 'failed' && (
              <button className="text-sm text-due underline" onClick={() => void discardFailed(op.seq!)}>
                {t('discard')}
              </button>
            )}
          </li>
        ))}
      </ul>
    </div>
  )
}

/** Names this device as a counter ("Counter 2"): bills made here carry the name. Kept on this device only. */
function CounterName() {
  const { t } = useTranslation()
  const [name, setName] = useState(counterName)
  const [saved, setSaved] = useState(false)
  return (
    <label className="flex flex-col gap-1 rounded-[18px] bg-white p-4 text-sm">
      <b>{t('counter.label')}</b>
      <span className="text-muted">{t('counter.hint')}</span>
      <span className="flex gap-2">
        <input value={name} maxLength={30} onChange={(e) => { setName(e.target.value); setSaved(false) }} className="h-11 flex-1 rounded-xl border border-line px-3" />
        <button onClick={() => { setCounterName(name); setSaved(true) }} className="h-11 rounded-xl bg-teal-700 px-4 font-semibold text-white">{saved ? '✓' : t('counter.save')}</button>
      </span>
    </label>
  )
}

import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Sheet } from '../components/ui'
import { api, request } from '../lib/api'
import { formatRupees, whatsAppLink } from '../lib/format'
import { buildReminder, useShopSettings, type Due, type DuesReport } from '../lib/reports'
import { navigate } from '../router'

type Bucket = 'all' | 'd0' | 'd8' | 'd30'

/**
 * Who owes what, and for how long: oldest dues first, with one tap to remind on WhatsApp (the shop's own
 * wording and UPI ID), call, or read a short script before calling.
 */
export function DuesScreen({ shopId, shopName }: { shopId: string; shopName: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const dues = useQuery({ queryKey: [shopId, 'dues'], queryFn: () => api<DuesReport>('/v1/reports/dues') })
  const settings = useShopSettings(shopId)
  const [bucket, setBucket] = useState<Bucket>('all')
  const [script, setScript] = useState<Due | null>(null)

  if (dues.isError) return <p className="p-4 text-due">{dues.error.message}</p>
  const data = dues.data
  if (!data) return <p className="p-4 text-muted">…</p>

  const shown = data.customers.filter((d) =>
    bucket === 'all' || (bucket === 'd0' && d.days0To7 > 0) || (bucket === 'd8' && d.days8To30 > 0) || (bucket === 'd30' && d.over30 > 0))
  const reminderFor = (d: Due) =>
    buildReminder(settings.data?.reminderTemplate, { name: d.name, amount: d.balance, shop: shopName, upi: settings.data?.upiId ?? null })
  // Opening WhatsApp must not wait for the server (pop-up blockers); the "reminded" mark goes in the background.
  const markReminded = (d: Due) => {
    void request(`/v1/customers/${d.customerId}/reminded`, 'POST').then(() => queryClient.invalidateQueries({ queryKey: [shopId, 'dues'] })).catch(() => {})
  }

  const buckets: { key: Bucket; label: string; amount: number; tone: string }[] = [
    { key: 'd0', label: t('dues.bucket0'), amount: data.days0To7, tone: 'bg-blue-50 text-blue-800' },
    { key: 'd8', label: t('dues.bucket8'), amount: data.days8To30, tone: 'bg-orange-50 text-orange-800' },
    { key: 'd30', label: t('dues.bucket30'), amount: data.over30, tone: 'bg-red-50 text-red-800' },
  ]

  return (
    <div className="flex flex-col gap-3 p-4">
      <section className="flex flex-col gap-3 rounded-[20px] bg-white p-[18px]">
        <button onClick={() => setBucket('all')} className="flex min-h-11 items-center justify-between text-left" aria-pressed={bucket === 'all'}>
          <span className="text-[15px] font-semibold text-muted">{t('dues.total')}</span>
          <span className="text-[34px] leading-none font-extrabold text-due">{formatRupees(data.total)}</span>
        </button>
        <div className="grid grid-cols-3 gap-2">
          {buckets.map((b) => (
            <button key={b.key} onClick={() => setBucket(bucket === b.key ? 'all' : b.key)} aria-pressed={bucket === b.key}
              className={`flex min-h-14 flex-col rounded-xl p-2.5 text-left ${b.tone} ${bucket === b.key ? 'ring-2 ring-ink' : ''}`}>
              <span className="text-xs font-semibold">{b.label}</span>
              <span className="text-[17px] font-bold">{formatRupees(b.amount)}</span>
            </button>
          ))}
        </div>
      </section>

      {data.customers.length === 0 && <p className="py-8 text-center text-muted">{t('dues.empty')}</p>}
      <ul className="flex flex-col gap-2">
        {shown.map((d) => (
          <li key={d.customerId} className="flex flex-col gap-2 rounded-[18px] bg-white p-4">
            <button onClick={() => navigate(`/c/${d.customerId}`)} className="flex items-start justify-between gap-2 text-left">
              <span className="flex flex-col">
                <span className="text-[17px] font-bold">{d.name}</span>
                <span className="text-[13px] text-muted">
                  {t('dues.oldest', { days: d.oldestDays })}
                  {d.lastRemindedAt && ` · ${t('dues.remindedAgo', { when: ago(d.lastRemindedAt, t) })}`}
                </span>
              </span>
              <span className="flex flex-col items-end">
                <span className="text-lg font-bold text-due">{formatRupees(d.balance)}</span>
                {d.over30 > 0 && <span className="text-xs font-semibold text-red-800">{t('dues.bucket30')}: {formatRupees(d.over30)}</span>}
              </span>
            </button>
            <div className="grid grid-cols-3 gap-2">
              {d.phone ? (
                <>
                  <a href={whatsAppLink(d.phone, reminderFor(d))} target="_blank" rel="noreferrer" onClick={() => markReminded(d)}
                    className="flex min-h-11 items-center justify-center rounded-xl bg-[#15803d] font-semibold text-white">{t('dues.remind')}</a>
                  <a href={`tel:${d.phone}`} className="flex min-h-11 items-center justify-center rounded-xl border border-line font-semibold">{t('dues.call')}</a>
                </>
              ) : (
                <span className="col-span-2 flex min-h-11 items-center text-sm text-muted">{t('dues.noPhone')}</span>
              )}
              <button onClick={() => setScript(d)} className="min-h-11 rounded-xl border border-line font-semibold">{t('dues.script')}</button>
            </div>
          </li>
        ))}
      </ul>

      {script && (
        <Sheet title={t('dues.scriptTitle')} onClose={() => setScript(null)}>
          <p className="rounded-xl bg-white p-4 text-lg leading-relaxed">{t('dues.scriptText', { name: script.name, shop: shopName, amount: script.balance })}</p>
        </Sheet>
      )}
    </div>
  )
}

function ago(iso: string, t: (key: string, options?: Record<string, unknown>) => string) {
  const days = Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000)
  return days <= 0 ? t('dues.today') : t('dues.daysAgo', { count: days })
}

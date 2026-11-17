import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { DataTable } from '../components/DataTable'
import { Sheet } from '../components/ui'
import { api, request } from '../lib/api'
import { formatRupees, whatsAppLink } from '../lib/format'
import { buildReminder, useShopSettings, type Due, type DuesReport } from '../lib/reports'
import { navigate } from '../router'
import { shownLocale } from '../i18n'

type Bucket = 'all' | 'd0' | 'd8' | 'd30' | 'd60' | 'd90' | 'd90p' | 'overdue'

/** "Due 5 Dec" or "Overdue since 5 Dec": when the oldest unpaid udhaar falls due under the customer's credit days. */
function dueLine(d: Due, t: (key: string, options?: Record<string, unknown>) => string): { text: string; late: boolean } | null {
  if (!d.dueOn) return null
  const day = new Date(`${d.dueOn}T00:00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })
  return (d.overdue ?? 0) > 0 ? { text: t('dues.overdueSince', { amount: formatRupees(d.overdue!), date: day }), late: true } : { text: t('dues.dueOn', { date: day }), late: false }
}

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
    bucket === 'all' || (bucket === 'd0' && d.days0To7 > 0) || (bucket === 'd8' && d.days8To30 > 0) || (bucket === 'd30' && d.over30 > 0)
    || (bucket === 'd60' && (d.days31To60 ?? 0) > 0) || (bucket === 'd90' && (d.days61To90 ?? 0) > 0) || (bucket === 'd90p' && (d.over90 ?? 0) > 0)
    || (bucket === 'overdue' && (d.overdue ?? 0) > 0))
  // Over 30 days, split the way accounts read it.
  const older: { key: Bucket; label: string; amount: number }[] = [
    { key: 'd60', label: t('dues.bucket60'), amount: data.days31To60 ?? 0 },
    { key: 'd90', label: t('dues.bucket90'), amount: data.days61To90 ?? 0 },
    { key: 'd90p', label: t('dues.bucket90p'), amount: data.over90 ?? 0 },
  ]
  const reminderFor = (d: Due) =>
    buildReminder(settings.data?.reminderTemplate, { name: d.name, amount: d.balance, shop: shopName, upi: settings.data?.upiId ?? null, slug: settings.data?.slug })
  // Opening WhatsApp must not wait for the server (pop-up blockers); the "reminded" mark goes in the background.
  const markReminded = (d: Due) => {
    void request(`/v1/customers/${d.customerId}/reminded`, 'POST').then(() => queryClient.invalidateQueries({ queryKey: [shopId, 'dues'] })).catch(() => {})
  }

  const buckets: { key: Bucket; label: string; amount: number; tone: string }[] = [
    { key: 'd0', label: t('dues.bucket0'), amount: data.days0To7, tone: 'bg-blue-50 text-blue-800' },
    { key: 'd8', label: t('dues.bucket8'), amount: data.days8To30, tone: 'bg-orange-50 text-orange-800' },
    { key: 'd30', label: t('dues.bucket30'), amount: data.over30, tone: 'bg-due-soft text-red-800' },
  ]

  return (
    <div className="flex flex-col gap-3 p-4">
      <button onClick={() => navigate('/reminders')} className="flex h-12 items-center justify-center rounded-2xl border-2 border-brand font-bold text-brand-strong">{t('reminders.title')}</button>
      <section className="flex flex-col gap-3 rounded-[20px] bg-white p-[18px] lg:flex-row lg:items-center lg:gap-6">
        <button onClick={() => setBucket('all')} className="flex min-h-11 items-center justify-between gap-3 text-left lg:shrink-0" aria-pressed={bucket === 'all'}>
          <span className="text-[15px] font-semibold text-muted">{t('dues.total')}</span>
          <span className="text-[34px] leading-none font-extrabold text-due">{formatRupees(data.total)}</span>
        </button>
        <div className="grid grid-cols-3 gap-2 lg:flex-1">
          {buckets.map((b) => (
            <button key={b.key} onClick={() => setBucket(bucket === b.key ? 'all' : b.key)} aria-pressed={bucket === b.key}
              className={`flex min-h-14 flex-col rounded-xl p-2.5 text-left ${b.tone} ${bucket === b.key ? 'ring-2 ring-ink' : ''}`}>
              <span className="text-xs font-semibold">{b.label}</span>
              <span className="text-[17px] font-bold">{formatRupees(b.amount)}</span>
            </button>
          ))}
        </div>
      </section>
      {data.over30 > 0 && (
        <section className="flex flex-wrap items-center gap-2 rounded-card bg-white p-3" aria-label={t('dues.olderTitle')}>
          <span className="text-sm font-semibold text-muted">{t('dues.olderTitle')}</span>
          {older.map((b) => (
            <button key={b.key} onClick={() => setBucket(bucket === b.key ? 'all' : b.key)} aria-pressed={bucket === b.key} disabled={b.amount === 0}
              className={`min-h-11 rounded-xl bg-due-soft px-3 text-left text-red-800 disabled:opacity-40 ${bucket === b.key ? 'ring-2 ring-ink' : ''}`}>
              <span className="block text-xs font-semibold">{b.label}</span><span className="font-bold">{formatRupees(b.amount)}</span>
            </button>
          ))}
        </section>
      )}
      {(data.overdue ?? 0) > 0 && (
        <button onClick={() => setBucket(bucket === 'overdue' ? 'all' : 'overdue')} aria-pressed={bucket === 'overdue'}
          className={`flex min-h-12 items-center justify-between rounded-2xl bg-due px-4 font-bold text-white ${bucket === 'overdue' ? 'ring-2 ring-ink' : ''}`}>
          <span>⏰ {t('dues.overdueTotal', { count: data.customers.filter((d) => (d.overdue ?? 0) > 0).length })}</span><span>{formatRupees(data.overdue!)}</span>
        </button>
      )}

      {data.customers.length === 0 && <p className="py-8 text-center text-muted">{t('dues.empty')}</p>}
      {shown.length > 0 && (
        <DataTable
          label={t('dues.title')}
          rows={shown}
          rowKey={(d) => d.customerId}
          onRowClick={(d) => navigate(`/c/${d.customerId}`)}
          columns={[
            { key: 'name', header: t('dues.colName'), sortValue: (d) => d.name, render: (d) => (
              <span className="flex flex-col"><span className="font-bold">{d.name}</span>
                {dueLine(d, t) && <span className={`text-xs font-semibold ${dueLine(d, t)!.late ? 'text-due' : 'text-muted'}`}>{dueLine(d, t)!.text}</span>}
                {d.lastRemindedAt && <span className="text-xs text-muted">{t('dues.remindedAgo', { when: ago(d.lastRemindedAt, t) })}</span>}
                {d.moneyWaiting && <span className="text-xs font-semibold text-udhaar">{t('receipts.moneyWaiting')}</span>}</span>) },
            { key: 'days', header: t('dues.colDays'), align: 'right', sortValue: (d) => d.oldestDays, render: (d) => d.oldestDays },
            { key: 'd0', header: t('dues.bucket0'), align: 'right', sortValue: (d) => d.days0To7, render: (d) => (d.days0To7 ? formatRupees(d.days0To7) : '—') },
            { key: 'd8', header: t('dues.bucket8'), align: 'right', sortValue: (d) => d.days8To30, render: (d) => (d.days8To30 ? formatRupees(d.days8To30) : '—') },
            { key: 'd30', header: t('dues.bucket30'), align: 'right', sortValue: (d) => d.over30, render: (d) => (d.over30 ? <span className="font-semibold text-red-800">{formatRupees(d.over30)}</span> : '—') },
            { key: 'bal', header: t('dues.colTotal'), align: 'right', sortValue: (d) => d.balance, render: (d) => <span className="font-bold text-due">{formatRupees(d.balance)}</span> },
            { key: 'act', header: '', align: 'right', render: (d) => (
              <span className="inline-flex gap-1.5" onClick={(e) => e.stopPropagation()}>
                {d.phone && <a href={whatsAppLink(d.phone, reminderFor(d))} target="_blank" rel="noreferrer" onClick={() => markReminded(d)} className="rounded-lg bg-[#15803d] px-2.5 py-1.5 text-sm font-semibold text-white">{t('dues.remind')}</a>}
                <button onClick={() => setScript(d)} className="rounded-lg border border-line px-2.5 py-1.5 text-sm font-semibold">{t('dues.script')}</button>
              </span>) },
          ]}
          footer={[t('dues.colTotal'), '', formatRupees(shown.reduce((n, d) => n + d.days0To7, 0)), formatRupees(shown.reduce((n, d) => n + d.days8To30, 0)), formatRupees(shown.reduce((n, d) => n + d.over30, 0)), formatRupees(shown.reduce((n, d) => n + d.balance, 0)), '']}
          renderCard={(d) => (
          <div className="mb-2 flex flex-col gap-2 rounded-card bg-white p-4">
            <button onClick={() => navigate(`/c/${d.customerId}`)} className="flex items-start justify-between gap-2 text-left">
              <span className="flex flex-col">
                <span className="text-[17px] font-bold">{d.name}</span>
                <span className="text-[13px] text-muted">
                  {t('dues.oldest', { days: d.oldestDays })}
                  {d.lastRemindedAt && ` · ${t('dues.remindedAgo', { when: ago(d.lastRemindedAt, t) })}`}
                </span>
                {d.moneyWaiting && <span className="text-[13px] font-semibold text-udhaar">{t('receipts.moneyWaiting')}</span>}
                {dueLine(d, t) && <span className={`text-[13px] font-semibold ${dueLine(d, t)!.late ? 'text-due' : 'text-muted'}`}>{dueLine(d, t)!.text}</span>}
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
          </div>
          )}
        />
      )}

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

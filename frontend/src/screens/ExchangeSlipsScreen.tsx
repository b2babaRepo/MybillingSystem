import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { shownLocale } from '../i18n'
import { Card } from '../components/ui'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'

interface Slip { id: string; noteNo: string; amount: number; balance: number; issuedAt: string; validTill: string; status: 'open' | 'used' | 'expired' | 'cancelled'; billNo: string; customerName: string | null; customerPhone: string | null; usedOnBillNo: string | null }

/** Every exchange slip: open ones (credit still owed to customers) first, then used and expired. */
export function ExchangeSlipsScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const slips = useQuery({ queryKey: [shopId, 'exchange-slips'], queryFn: () => api<Slip[]>('/v1/exchange-slips') })
  const list = slips.data ?? []
  const openTotal = list.filter((s) => s.status === 'open').reduce((n, s) => n + s.balance, 0)
  const day = (iso: string) => new Date(iso.length === 10 ? `${iso}T00:00` : iso).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })
  const tag = { open: 'bg-brand-soft text-brand-deep', used: 'bg-line/60 text-muted', expired: 'bg-due-soft text-due', cancelled: 'bg-due-soft text-due' }
  return (
    <div className="flex flex-col gap-3 p-4">
      <Card>
        <h1 className="text-lg font-extrabold">🎟 {t('slip.register')}</h1>
        <p className="mt-1 text-sm text-muted">{t('slip.registerHint')}</p>
        <p className="mt-2 font-bold">{t('slip.openTotal', { amount: formatRupees(openTotal) })}</p>
      </Card>
      {slips.isError && <p className="text-sm text-due">{slips.error.message}</p>}
      {list.length > 0 && (
        <div className="divide-y divide-line rounded-card bg-white shadow-sm">
          {list.map((s) => (
            <button key={s.id} onClick={() => navigate(`/print/slip/${s.id}`)} className="flex w-full items-center gap-3 px-4 py-3 text-left">
              <span className="flex min-w-0 flex-1 flex-col gap-0.5">
                <span className="flex flex-wrap items-center gap-2"><b>{s.noteNo}</b>
                  <span className={`rounded-full px-2 text-xs font-bold ${tag[s.status]}`}>{t(s.status === 'open' ? 'slip.open' : s.status === 'used' ? 'slip.usedTag' : s.status === 'cancelled' ? 'slip.cancelledTag' : 'slip.expiredTag')}</span></span>
                <span className="truncate text-sm">{s.customerName ?? '—'}{s.customerPhone ? ` · ${s.customerPhone}` : ''}</span>
                <span className="text-xs text-muted">{day(s.issuedAt)} · {s.billNo} · {s.status === 'used' ? t('slip.usedOn', { bill: s.usedOnBillNo ?? '' }) : `${t('slip.validTill')} ${day(s.validTill)}`}</span>
              </span>
              <span className="flex flex-col items-end"><b className={s.status === 'open' ? '' : 'text-muted'}>{formatRupees(s.status === 'open' ? s.balance : s.amount)}</b>
                {s.status === 'open' && s.balance < s.amount && <span className="text-xs text-muted">{t('slip.ofAmount', { amount: formatRupees(s.amount) })}</span>}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

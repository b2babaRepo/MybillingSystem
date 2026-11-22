import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { shownLocale } from '../i18n'
import { Card } from '../components/ui'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'
import { DataTable } from '../components/DataTable'

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
    <div className="flex flex-col gap-3 p-4 lg:mx-auto lg:w-full lg:max-w-5xl">
      <Card>
        <h1 className="text-lg font-extrabold">🎟 {t('slip.register')}</h1>
        <p className="mt-1 text-sm text-muted">{t('slip.registerHint')}</p>
        <p className="mt-2 font-bold">{t('slip.openTotal', { amount: formatRupees(openTotal) })}</p>
      </Card>
      {slips.isError && <p className="text-sm text-due">{slips.error.message}</p>}
      {list.length > 0 && (
        <div className="hidden lg:block">
          <DataTable label={t('slip.register')} rows={list} rowKey={(x) => x.id} download="exchange-slips" onRowClick={(x) => navigate(`/print/slip/${x.id}`)}
            columns={[
              { key: 'no', header: t('slipTable.no'), width: 'w-40', sortValue: (x) => x.noteNo, filter: { kind: 'text', value: (x) => x.noteNo }, render: (x) => <b>{x.noteNo}</b>, csv: (x) => x.noteNo },
              { key: 'issued', header: t('slipTable.issued'), width: 'w-24', sortValue: (x) => x.issuedAt, render: (x) => day(x.issuedAt), csv: (x) => x.issuedAt.slice(0, 10) },
              { key: 'who', header: t('chequeTable.customer'), sortValue: (x) => x.customerName ?? '', filter: { kind: 'text', value: (x) => `${x.customerName ?? ''} ${x.customerPhone ?? ''}` },
                render: (x) => <span className="flex flex-col">{x.customerName ?? '—'}{x.customerPhone && <span className="text-xs text-muted">{x.customerPhone}</span>}</span>, csv: (x) => x.customerName ?? '' },
              { key: 'bill', header: t('slipTable.from'), width: 'w-36', sortValue: (x) => x.billNo, render: (x) => x.billNo, csv: (x) => x.billNo },
              { key: 'amount', header: t('slipTable.amount'), align: 'right', width: 'w-28', sortValue: (x) => x.amount, render: (x) => formatRupees(x.amount), csv: (x) => x.amount },
              { key: 'left', header: t('slipTable.left'), align: 'right', width: 'w-28', sortValue: (x) => (x.status === 'open' ? x.balance : 0), render: (x) => (x.status === 'open' ? <b>{formatRupees(x.balance)}</b> : '—'), csv: (x) => (x.status === 'open' ? x.balance : 0) },
              { key: 'till', header: t('slip.validTill'), width: 'w-28', sortValue: (x) => x.validTill, render: (x) => (x.status === 'used' ? t('slip.usedOn', { bill: x.usedOnBillNo ?? '' }) : day(x.validTill)), csv: (x) => x.validTill },
              { key: 'status', header: t('chequeTable.status'), width: 'w-28', sortValue: (x) => x.status,
                filter: { kind: 'select', value: (x) => x.status, options: (['open', 'used', 'expired', 'cancelled'] as const).map((st) => ({ value: st, label: t(st === 'open' ? 'slip.open' : st === 'used' ? 'slip.usedTag' : st === 'cancelled' ? 'slip.cancelledTag' : 'slip.expiredTag') })) },
                render: (x) => <span className={`rounded-full px-2 text-xs font-bold ${tag[x.status]}`}>{t(x.status === 'open' ? 'slip.open' : x.status === 'used' ? 'slip.usedTag' : x.status === 'cancelled' ? 'slip.cancelledTag' : 'slip.expiredTag')}</span>, csv: (x) => x.status },
            ]}
            footer={[t('yearRep.total'), '', '', '', formatRupees(list.reduce((n, x) => n + x.amount, 0)), formatRupees(openTotal), '', '']} />
        </div>
      )}
      {list.length > 0 && (
        <div className="divide-y divide-line rounded-card bg-white shadow-sm lg:hidden">
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

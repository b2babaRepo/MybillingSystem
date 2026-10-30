import { useQuery, useQueryClient } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { BillIcon, CustomersIcon, PaymentIcon, PlusIcon } from '../components/Icons'
import { api, request } from '../lib/api'
import { useProducts, useToday } from '../lib/data'
import { formatRupees } from '../lib/format'
import { useSubscription } from '../lib/billing'
import { useShopSettings } from '../lib/reports'
import { navigate } from '../router'

/**
 * Dashboard A, "Clean Teal": today's sales with the cash / UPI / udhaar split, four big actions,
 * and who owes the most. The teal band continues the app header; the summary card overlaps it.
 */
export function HomeScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const shopType = useShopSettings(shopId).data?.type ?? 'kirana'
  const { t } = useTranslation()
  const { today } = useToday(shopId)
  const { products } = useProducts(shopId)
  const lowCount = products.filter((p) => p.isLow).length

  return (
    <div className="pb-4">
      <div className="h-14 bg-teal-700" />
      <section className="mx-4 -mt-12 flex flex-col gap-3.5 rounded-[20px] bg-white p-[18px] shadow-[0_6px_20px_rgba(16,32,29,0.08)]">
        <div className="flex items-baseline justify-between">
          <span className="text-[15px] font-semibold text-muted">{t('todaySales')}</span>
          <span className="text-[13px] text-muted">{today ? t('billsCount', { count: today.bills }) : ' '}</span>
        </div>
        <div className="text-[40px] leading-none font-extrabold">{today ? formatRupees(today.sales) : '…'}</div>
        <div className="grid grid-cols-3 gap-2">
          <Split label={t('cash')} amount={today?.cash} className="bg-emerald-50 text-emerald-800" />
          <Split label={t('upi')} amount={today?.upi} className="bg-blue-50 text-blue-800" />
          <Split label={t('creditShort')} amount={today?.udhaar} className="bg-orange-50 text-orange-800" />
        </div>
      </section>

      <section className="mx-4 mt-4 grid grid-cols-2 gap-3">
        <Action label={t('newBill')} to="/bill" primary icon={<BillIcon size={28} />} />
        <Action label={t('addUdhaar')} to="/khata" icon={<PlusIcon size={28} className="text-udhaar" />} />
        <Action label={t('receivePayment')} to="/khata" icon={<PaymentIcon size={28} className="text-jama" />} />
        <Action label={t('customers')} to="/khata" icon={<CustomersIcon size={28} className="text-blue-700" />} />
      </section>

      {isOwner && <PlanBanner shopId={shopId} />}
      {isOwner && <BalanceAlert />}

      <div className="mx-4 mt-4 grid grid-cols-2 gap-3">
        <button onClick={() => navigate('/cashbook')} className="flex min-h-12 items-center justify-between rounded-[18px] bg-white px-4 text-left font-semibold">
          <span>{t('book.todayLink')}</span><span aria-hidden="true">→</span>
        </button>
        <button onClick={() => navigate('/expenses')} className="flex min-h-12 items-center justify-between rounded-[18px] bg-white px-4 text-left font-semibold">
          <span>{t('expense.link')}</span><span aria-hidden="true">→</span>
        </button>
      </div>

      <div className="mx-4 mt-3 grid grid-cols-2 gap-3">
        <button onClick={() => navigate('/dues')} className="flex min-h-12 items-center justify-between rounded-[18px] bg-white px-4 text-left font-semibold">
          <span>{t('dues.link')}</span><span aria-hidden="true">→</span>
        </button>
        {/* Owner screens: helpers (staff) don't see them; the server refuses them too. */}
        {isOwner && (
          <button onClick={() => navigate('/reports')} className="flex min-h-12 items-center justify-between rounded-[18px] bg-white px-4 text-left font-semibold">
            <span>{t('reports.link')}</span><span aria-hidden="true">→</span>
          </button>
        )}
      </div>

      <button onClick={() => navigate('/store')} className="mx-4 mt-3 flex min-h-12 w-[calc(100%-2rem)] items-center justify-between rounded-[18px] bg-white px-4 text-left font-semibold">
        <span>{t('myStore.link')}</span>
        <span aria-hidden="true">→</span>
      </button>

      {isOwner && shopType === 'paint' && (
        <button onClick={() => navigate('/painters')} className="mx-4 mt-3 flex min-h-12 w-[calc(100%-2rem)] items-center justify-between rounded-[18px] bg-white px-4 text-left font-semibold">
          <span>{t('painters.link')}</span><span aria-hidden="true">→</span>
        </button>
      )}

      {isOwner && shopType !== 'kirana' && (
        <button onClick={() => navigate('/rates')} className="mx-4 mt-3 flex min-h-12 w-[calc(100%-2rem)] items-center justify-between rounded-[18px] bg-white px-4 text-left font-semibold">
          <span>{t('rates.link')}</span><span aria-hidden="true">→</span>
        </button>
      )}

      {isOwner && (
        <button onClick={() => navigate('/settings')} className="mx-4 mt-3 flex min-h-12 w-[calc(100%-2rem)] items-center justify-between rounded-[18px] bg-white px-4 text-left font-semibold">
          <span>{t('settings.link')}</span>
          <span aria-hidden="true">→</span>
        </button>
      )}

      {lowCount > 0 && (
        <button onClick={() => navigate('/items')} className="mx-4 mt-4 flex min-h-12 w-[calc(100%-2rem)] items-center justify-between rounded-[18px] bg-orange-50 px-4 text-left font-semibold text-orange-800">
          <span>{t('stock.lowCount', { count: lowCount })}</span>
          <span aria-hidden="true">→</span>
        </button>
      )}

      <section className="mx-4 mt-4 flex flex-col rounded-[18px] bg-white px-4 py-3.5">
        <div className="flex items-center justify-between">
          <h2 className="text-base font-bold">{t('topDues')}</h2>
          <button className="min-h-11 px-1 text-sm font-semibold text-teal-700" onClick={() => navigate('/khata')}>
            {t('seeAll')}
          </button>
        </div>
        {today && today.topDues.length === 0 && <p className="py-2 text-muted">{t('noDues')}</p>}
        {today?.topDues.map((c) => (
          <button
            key={c.id}
            onClick={() => navigate(`/c/${c.id}`)}
            className="flex min-h-12 items-center justify-between border-b border-[#edf2f1] py-2.5 text-left last:border-0"
          >
            <span className="text-base font-semibold">{c.name}</span>
            <span className="text-base font-bold text-due">{formatRupees(c.balance)}</span>
          </button>
        ))}
        {today && today.customersOwing > 0 && (
          <div className="mt-2 flex justify-between rounded-xl bg-red-50 px-3 py-2 text-sm text-due">
            <span>{t('inMarket', { count: today.customersOwing })}</span>
            <span className="font-bold">{formatRupees(today.outstanding)}</span>
          </div>
        )}
      </section>
    </div>
  )
}

function Split({ label, amount, className }: { label: string; amount: number | undefined; className: string }) {
  return (
    <div className={`flex flex-col rounded-xl p-2.5 ${className}`}>
      <span className="text-xs">{label}</span>
      <span className="text-[17px] font-bold">{amount === undefined ? '…' : formatRupees(amount)}</span>
    </div>
  )
}

function Action({ label, to, icon, primary = false }: { label: string; to: string; icon: ReactNode; primary?: boolean }) {
  return (
    <button
      onClick={() => navigate(to)}
      className={`flex min-h-[92px] flex-col justify-between rounded-[18px] p-4 text-left ${
        primary ? 'bg-teal-700 text-white active:bg-teal-800' : 'border border-line bg-white text-ink active:bg-slate-50'
      }`}
    >
      {icon}
      <span className="text-lg font-bold">{label}</span>
    </button>
  )
}

interface BalanceCheck {
  checkedAt: string
  mismatches: number
  details: { customerId: string; name: string; storedBalance: number; sumOfEntries: number }[]
}

/**
 * The owner sees this only when last night's khata check found a balance that doesn't match its entries (a bug or
 * a hand edit in the database). Normally nothing shows. "Check again" re-runs it after a fix.
 */
function BalanceAlert() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const check = useQuery({
    queryKey: ['balance-check'],
    queryFn: async () => {
      const response = await request('/v1/ledger/checks/latest')
      return response.status === 200 ? ((await response.json()) as BalanceCheck) : null
    },
    staleTime: 600_000,
  })
  if (!check.data || check.data.mismatches === 0) return null
  return (
    <div className="mx-4 mt-4 flex flex-col gap-2 rounded-[18px] bg-red-50 p-4 text-due ring-1 ring-red-200">
      <span className="font-bold">⚠ {t('balanceCheck.title', { count: check.data.mismatches })}</span>
      {check.data.details.slice(0, 3).map((d) => (
        <button key={d.customerId} onClick={() => navigate(`/c/${d.customerId}`)} className="text-left text-sm underline">
          {t('balanceCheck.line', { name: d.name, stored: formatRupees(d.storedBalance), entries: formatRupees(d.sumOfEntries) })}
        </button>
      ))}
      <span className="text-xs text-ink">{t('balanceCheck.hint')}</span>
      <button className="min-h-11 self-start rounded-xl border border-red-200 bg-white px-3 text-sm font-semibold"
        onClick={() => void api('/v1/ledger/checks', 'POST', {}).then(() => queryClient.invalidateQueries({ queryKey: ['balance-check'] }))}>
        {t('balanceCheck.again')}
      </button>
    </div>
  )
}

/**
 * The plan in a line, only when it matters: the trial's last week, payment due (grace), on hold, or on Free.
 * Taps through to My plan.
 */
function PlanBanner({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const s = useSubscription(shopId).data
  if (!s) return null
  const show = (s.status === 'trial' && (s.daysLeft ?? 99) <= 7) || s.status === 'grace' || s.status === 'on_hold' || s.status === 'free'
  if (!show) return null
  const tone = s.status === 'trial' ? 'bg-teal-50 text-teal-900' : 'bg-amber-50 text-amber-900'
  return (
    <button onClick={() => navigate('/plan')} className={`mx-4 mt-3 flex min-h-12 w-[calc(100%-2rem)] items-center justify-between rounded-[18px] px-4 text-left font-semibold ${tone}`}>
      <span>{t(`plan2.banner.${s.status}`, { count: s.daysLeft ?? 0, reason: s.holdReason ?? '' })}</span>
      <span aria-hidden="true">→</span>
    </button>
  )
}

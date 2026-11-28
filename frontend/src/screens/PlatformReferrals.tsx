import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { shownLocale } from '../i18n'

interface ReferralRow { shopId: string; shopName: string; joinedAt: string; referrerShopId: string; referrerName: string; referrerCode: string; paid: boolean; firstPaidAt: string | null; rewarded: boolean }
interface ReferrerTotal { shopId: string; shopName: string; code: string; joined: number; paid: number; rewarded: number }

const day = (iso: string) => new Date(iso).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric' })

/** Shops brought by other shops' referral codes: who brought whom, when they joined, whether they paid, whether the free month was given. */
export function PlatformReferralsPanel() {
  const { t } = useTranslation()
  const data = useQuery({ queryKey: ['platform', 'referrals'], queryFn: () => api<{ referrals: ReferralRow[]; referrers: ReferrerTotal[] }>('/v1/platform/referrals') }).data
  if (!data) return <p className="text-muted">…</p>
  const paid = data.referrals.filter((r) => r.paid).length
  return (
    <section className="flex flex-col gap-4">
      <header>
        <h2 className="text-xl font-bold">{t('referralsAdmin.title')}</h2>
        <p className="text-sm text-muted">{t('referralsAdmin.hint')}</p>
      </header>
      <div className="grid grid-cols-3 gap-2">
        {[[t('referralsAdmin.joined'), data.referrals.length], [t('referralsAdmin.paid'), paid], [t('referralsAdmin.rewarded'), data.referrals.filter((r) => r.rewarded).length]].map(([label, value]) => (
          <div key={label as string} className="rounded-card bg-white p-3 shadow-sm"><p className="text-xs text-muted">{label}</p><p className="text-2xl font-bold">{value}</p></div>
        ))}
      </div>
      {data.referrals.length === 0 && <p className="rounded-card bg-white p-4 text-muted">{t('referralsAdmin.none')}</p>}
      {data.referrers.length > 0 && (
        <div className="overflow-x-auto rounded-card bg-white shadow-sm">
          <table className="w-full text-sm">
            <caption className="px-3 pt-3 text-left font-bold">{t('referralsAdmin.byShop')}</caption>
            <thead><tr className="text-left text-xs text-muted"><th className="px-3 py-2">{t('referralsAdmin.shop')}</th><th className="px-3 py-2">{t('referralsAdmin.code')}</th><th className="px-3 py-2 text-right">{t('referralsAdmin.joined')}</th><th className="px-3 py-2 text-right">{t('referralsAdmin.paid')}</th><th className="px-3 py-2 text-right">{t('referralsAdmin.rewarded')}</th></tr></thead>
            <tbody>
              {data.referrers.map((r) => (
                <tr key={r.shopId} className="border-t border-line"><td className="px-3 py-2 font-semibold">{r.shopName}</td><td className="px-3 py-2 font-mono text-xs">{r.code}</td><td className="px-3 py-2 text-right">{r.joined}</td><td className="px-3 py-2 text-right">{r.paid}</td><td className="px-3 py-2 text-right">{r.rewarded}</td></tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {data.referrals.length > 0 && (
        <div className="overflow-x-auto rounded-card bg-white shadow-sm">
          <table className="w-full text-sm">
            <caption className="px-3 pt-3 text-left font-bold">{t('referralsAdmin.list')}</caption>
            <thead><tr className="text-left text-xs text-muted"><th className="px-3 py-2">{t('referralsAdmin.newShop')}</th><th className="px-3 py-2">{t('referralsAdmin.broughtBy')}</th><th className="px-3 py-2">{t('referralsAdmin.joinedOn')}</th><th className="px-3 py-2">{t('referralsAdmin.status')}</th></tr></thead>
            <tbody>
              {data.referrals.map((r) => (
                <tr key={r.shopId} className="border-t border-line">
                  <td className="px-3 py-2 font-semibold">{r.shopName}</td>
                  <td className="px-3 py-2">{r.referrerName}</td>
                  <td className="px-3 py-2 whitespace-nowrap">{day(r.joinedAt)}</td>
                  <td className="px-3 py-2">
                    <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${r.rewarded ? 'bg-emerald-50 text-emerald-800' : r.paid ? 'bg-brand-soft text-brand-deep' : 'bg-warn-soft text-warn-ink'}`}>
                      {r.rewarded ? t('referralsAdmin.stRewarded') : r.paid ? t('referralsAdmin.stPaid', { date: day(r.firstPaidAt!) }) : t('referralsAdmin.stTrial')}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}

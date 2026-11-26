import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { navigate } from '../router'
import { btn } from '../lib/buttonStyles'

interface PublicPlan {
  code: string; name: string; priceMonthly: number | null; priceYear1: number | null; priceYear3: number | null; priceYear5: number | null
  features: string[]; maxCustomers: number | null; maxStaff: number; maxOwners: number; whatsAppPerMonth: number
}

/**
 * The public price page (#/pricing): every plan on sale with its yearly price and what it includes, open to anyone.
 * Prices come from the plans the platform admin sets, so the page never goes out of date.
 */
export function PricingPage() {
  const { t } = useTranslation()
  const plans = useQuery({ queryKey: ['plans', 'public'], queryFn: () => api<PublicPlan[]>('/v1/plans/public') }).data ?? []
  return (
    <main className="mx-auto flex min-h-screen max-w-5xl flex-col gap-5 bg-page px-4 py-8">
      <header className="text-center">
        <h1 className="text-3xl font-extrabold text-brand-deep">{t('jwl7.pricingTitle')}</h1>
        <p className="mx-auto mt-2 max-w-2xl text-muted">{t('jwl7.pricingHint')}</p>
      </header>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {plans.map((p) => (
          <section key={p.code} className={`flex flex-col gap-2 rounded-card bg-white p-5 shadow-sm ${p.code === 'jewellery' ? 'ring-2 ring-[#b8860b]' : ''}`}>
            <h2 className="text-xl font-extrabold">{p.code === 'jewellery' ? `💍 ${p.name}` : p.name}</h2>
            {p.priceYear1 == null && p.priceMonthly == null
              ? <p className="text-2xl font-extrabold">{t('jwl7.free')}</p>
              : <p><span className="text-3xl font-extrabold">{formatRupees(p.priceYear1 ?? (p.priceMonthly ?? 0) * 12)}</span> <span className="text-muted">/ {t('jwl7.perYear')}</span></p>}
            {(p.priceYear3 || p.priceYear5 || p.priceMonthly) && (
              <p className="text-xs text-muted">
                {[p.priceMonthly ? `${formatRupees(p.priceMonthly)} / ${t('jwl7.perMonth')}` : null,
                  p.priceYear3 ? `${formatRupees(p.priceYear3)} / ${t('jwl7.years3')}` : null,
                  p.priceYear5 ? `${formatRupees(p.priceYear5)} / ${t('jwl7.years5')}` : null].filter(Boolean).join(' · ')}
              </p>
            )}
            <ul className="mt-1 flex flex-col gap-1 text-sm">
              <li>✓ {p.maxCustomers ? t('jwl7.customers', { count: p.maxCustomers }) : t('jwl7.noCap')}</li>
              <li>✓ {p.maxStaff > 0 ? t('jwl7.staff', { count: p.maxStaff }) : t('jwl7.noStaff')}</li>
              {p.whatsAppPerMonth > 0 && <li>✓ {t('jwl7.whatsapp', { count: p.whatsAppPerMonth })}</li>}
            </ul>
          </section>
        ))}
      </div>
      <p className="text-center text-sm text-muted">{t('jwl7.gstExtra')}</p>
      <button onClick={() => navigate('/')} className={`mx-auto h-12 rounded-xl px-8 ${btn.solid}`}>{t('jwl7.start')}</button>
    </main>
  )
}

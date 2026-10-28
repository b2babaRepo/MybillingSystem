import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { KhataHistory } from '../../components/KhataHistory'
import { Balance } from '../../components/ui'
import { setLanguage } from '../../i18n'
import { api } from '../../lib/api'
import { formatPhone, formatRupees } from '../../lib/format'
import type { Statement } from '../../lib/types'
import { navigate } from '../../router'

// Transparency step 2: the customer's own, read-only copy of their khata. The same ledger the shop sees,
// removed entries and their reasons included. Reached by the private link the shop shares (#/k/...), with no
// account, or inside the household app for every shop where the person's phone is a khata customer.

interface KhataView {
  shopName: string
  shopArea: string | null
  shopPhone: string | null
  statement: Statement
}

interface MyKhata {
  shopId: string
  shopName: string
  shopArea: string | null
  customerId: string
  customerName: string
  balance: number
}

function KhataBody({ view }: { view: KhataView }) {
  const { t } = useTranslation()
  const { customer } = view.statement
  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex items-start justify-between rounded-[18px] bg-white p-4">
        <div className="min-w-0">
          <div className="text-sm text-muted">{[view.shopName, view.shopArea].filter(Boolean).join(' · ')}</div>
          <div className="text-2xl font-bold">{customer.name}</div>
        </div>
        <Balance amount={customer.balance} large />
      </div>
      <p className="rounded-[14px] bg-teal-50 p-3 text-sm text-teal-900">{t('myKhata.readOnly', { shop: view.shopName })}</p>
      {view.shopPhone && (
        <a className="flex h-12 items-center justify-center rounded-2xl border border-line bg-white font-semibold" href={`tel:${view.shopPhone}`}>
          📞 {t('myKhata.callShop', { phone: formatPhone(view.shopPhone) })}
        </a>
      )}
      <KhataHistory entries={view.statement.entries} />
    </div>
  )
}

/** The page behind the shop's link: #/k/{shop}/{customer}?t={token}. Open to anyone holding the link. */
export function KhataLinkPage({ path }: { path: string }) {
  const { t, i18n } = useTranslation()
  const match = path.match(/^\/k\/([^/]+)\/([0-9a-f-]+)\?t=([\w-]+)/)
  const view = useQuery({
    queryKey: ['khata-link-view', path],
    queryFn: () => api<KhataView>(`/v1/khata/${match![1]}/${match![2]}?t=${match![3]}`),
    enabled: !!match,
    retry: false,
  })

  return (
    <div className="mx-auto min-h-dvh max-w-xl">
      <header className="sticky top-0 z-10 flex items-center gap-2 bg-teal-700 px-4 py-3 text-white">
        <h1 className="flex-1 truncate text-xl font-bold">{t('myKhata.title')}</h1>
        <button onClick={() => setLanguage(i18n.language === 'hi' ? 'en' : 'hi')} className="h-11 rounded-full bg-white/20 px-3 text-sm font-semibold">
          {i18n.language === 'hi' ? 'EN' : 'हि'}
        </button>
      </header>
      {view.data ? (
        <KhataBody view={view.data} />
      ) : view.isError || !match ? (
        <p className="p-6 text-center text-muted">{t('myKhata.linkInvalid')}</p>
      ) : (
        <p className="p-6 text-center text-muted">…</p>
      )}
    </div>
  )
}

/** Household app: every shop where my phone has a khata. */
export function MyKhataScreen() {
  const { t } = useTranslation()
  const mine = useQuery({ queryKey: ['my-khata'], queryFn: () => api<MyKhata[]>('/v1/me/khata') })
  return (
    <div className="flex flex-col gap-3 p-4">
      {mine.data?.length === 0 && <p className="py-8 text-center text-muted">{t('myKhata.none')}</p>}
      <ul className="divide-y divide-[#edf2f1] rounded-[18px] bg-white">
        {mine.data?.map((k) => (
          <li key={k.customerId}>
            <button className="flex min-h-16 w-full items-center gap-3 px-3.5 py-3 text-left" onClick={() => navigate(`/khata/${k.shopId}/${k.customerId}`)}>
              <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-[#e0efec] text-lg font-extrabold text-teal-700">{k.shopName.charAt(0)}</span>
              <div className="min-w-0 flex-1">
                <div className="truncate font-bold">{k.shopName}</div>
                {k.shopArea && <div className="truncate text-sm text-muted">{k.shopArea}</div>}
              </div>
              <div className={`text-right font-bold ${k.balance > 0 ? 'text-udhaar' : 'text-muted'}`}>
                {formatRupees(Math.abs(k.balance))}
                <div className="text-xs font-normal">{k.balance > 0 ? t('myKhata.youOwe') : t('settled')}</div>
              </div>
              <span aria-hidden className="text-xl text-muted">›</span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  )
}

/** Household app: my khata at one shop. */
export function MyKhataDetail({ shopId, customerId }: { shopId: string; customerId: string }) {
  const { t } = useTranslation()
  const view = useQuery({ queryKey: ['my-khata', shopId, customerId], queryFn: () => api<KhataView>(`/v1/me/khata/${shopId}/${customerId}`) })
  if (view.isError) return <p className="p-6 text-center text-muted">{t('myKhata.linkInvalid')}</p>
  return view.data ? <KhataBody view={view.data} /> : <p className="p-6 text-center text-muted">…</p>
}

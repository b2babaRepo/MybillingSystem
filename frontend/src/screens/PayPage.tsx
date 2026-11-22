import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { formatRupees } from '../lib/format'
import { upiUri } from '../lib/reports'

/** #/pay/{shop}/{amount}: one big button that opens the customer's UPI app to pay the shop. Public; shows no khata. */
export function PayPage({ path }: { path: string }) {
  const { t } = useTranslation()
  const [, , slug = '', raw = ''] = path.split('/')
  const amount = Math.max(0, Math.min(10_000_000, Number(raw) || 0))
  const shop = useQuery({ queryKey: ['pay', slug], queryFn: async () => {
    // Anonymous, like the store: a signed-in visitor's token must not make the shop's page act as their own shop.
    const response = await fetch(`/api/v1/store/${encodeURIComponent(decodeURIComponent(slug))}/pay`)
    if (!response.ok) throw new Error(String(response.status))
    return (await response.json()) as { name: string; upiId: string }
  }, retry: false })

  return (
    <main className="mx-auto flex min-h-dvh max-w-md flex-col items-center justify-center gap-4 bg-ground p-6 text-center">
      {shop.isLoading && <p className="text-muted">…</p>}
      {shop.isError && <p className="text-lg font-semibold">{t('payPage.missing')}</p>}
      {shop.data && (
        <>
          <p className="text-muted">{t('payPage.to')}</p>
          <h1 className="text-2xl font-bold">{shop.data.name}</h1>
          {amount > 0 && <p className="text-4xl font-bold text-brand-strong">{formatRupees(amount)}</p>}
          <a href={upiUri(shop.data.upiId, shop.data.name, amount, t('payPage.note', { shop: shop.data.name }))}
            className="flex min-h-14 w-full items-center justify-center rounded-2xl bg-[#15803d] text-lg font-bold text-white">
            {t('payPage.pay')}
          </a>
          <p className="text-sm text-muted">{t('payPage.or')}</p>
          <button onClick={() => void navigator.clipboard?.writeText(shop.data.upiId)} className="min-h-11 rounded-xl border border-line bg-white px-4 font-mono">
            {shop.data.upiId} · {t('payPage.copy')}
          </button>
          <p className="text-xs text-muted">{t('payPage.hint')}</p>
        </>
      )}
    </main>
  )
}

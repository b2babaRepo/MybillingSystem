import QRCode from 'qrcode'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { whatsAppLink } from '../lib/format'
import { alertsEnabled, enableAlerts } from '../lib/orderAlerts'

/** The shop's online-store link: copy it, send it on WhatsApp, or print the QR code for the counter. */
export function MyStoreScreen({ shopName, slug }: { shopName: string; slug: string }) {
  const { t } = useTranslation()
  const url = `${window.location.origin}/#/s/${slug}`
  const [qr, setQr] = useState('')
  const [copied, setCopied] = useState(false)
  const [alerts, setAlerts] = useState(alertsEnabled)

  useEffect(() => {
    void QRCode.toDataURL(url, { width: 512, margin: 2, color: { dark: '#10201d', light: '#ffffff' } }).then(setQr)
  }, [url])

  async function copy() {
    try {
      await navigator.clipboard.writeText(url)
      setCopied(true)
    } catch {
      // Clipboard blocked: the link is on screen to copy by hand.
    }
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      <section className="flex flex-col items-center gap-3 rounded-[20px] bg-white p-5 text-center print:shadow-none">
        <h2 className="text-xl font-bold">{shopName}</h2>
        <p className="hidden text-lg font-semibold print:block">{t('myStore.scanToOrder')}</p>
        {qr && <img src={qr} alt={`QR: ${url}`} className="h-56 w-56 print:h-80 print:w-80" />}
        <p className="font-mono text-sm break-all text-muted">{url}</p>
      </section>
      <p className="text-sm text-muted print:hidden">{t('myStore.hint')}</p>
      <div className="grid grid-cols-2 gap-2 print:hidden">
        <Button tone="plain" onClick={copy}>{copied ? t('myStore.copied') : t('myStore.copy')}</Button>
        <a href={whatsAppLink(null, t('myStore.shareText', { shop: shopName, url }))} target="_blank" rel="noreferrer"
          className="flex min-h-12 items-center justify-center rounded-2xl bg-[#15803d] px-4 text-lg font-semibold text-white">
          {t('myStore.share')}
        </a>
      </div>
      <Button tone="plain" className="print:hidden" onClick={() => window.print()}>{t('myStore.printQr')}</Button>

      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-4 print:hidden">
        <h2 className="text-base font-bold">{t('myStore.alerts')}</h2>
        <p className="text-sm text-muted">{t('myStore.alertsHint')}</p>
        {alerts ? (
          <p className="font-semibold text-jama">{t('myStore.alertsOn')}</p>
        ) : (
          <Button onClick={() => void enableAlerts().then(setAlerts)}>{t('myStore.alerts')}</Button>
        )}
      </section>
    </div>
  )
}

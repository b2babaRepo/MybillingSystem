import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import QRCode from 'qrcode'
import { watchDisplay, type DisplayState } from '../lib/customerDisplay'
import { formatRupees } from '../lib/format'

/**
 * The screen the customer sees at the counter: open it in a second window on the till's computer and move it to the
 * monitor facing the customer. It shows each item as it is scanned, the offers taken, the total, a UPI QR for the
 * amount, and the change after payment.
 */
export function CustomerDisplay({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const [state, setState] = useState<DisplayState | null>(null)
  const [qrImage, setQr] = useState<{ link: string; url: string } | null>(null)
  useEffect(() => watchDisplay(shopId, setState), [shopId])
  const due = state?.done ? 0 : state?.total ?? 0
  // A UPI link for the amount due; its QR is drawn once per link (a QR for another amount is never shown).
  const link = state?.upiId && due > 0 ? `upi://pay?pa=${encodeURIComponent(state.upiId)}&pn=${encodeURIComponent(state.shop)}&am=${due.toFixed(2)}&cu=INR` : null
  useEffect(() => {
    if (!link) return
    let live = true
    void QRCode.toDataURL(link, { margin: 1, width: 360 }).then((url) => { if (live) setQr({ link, url }) })
    return () => { live = false }
  }, [link])
  const qr = link && qrImage?.link === link ? qrImage.url : null

  return (
    <div className="flex min-h-dvh flex-col bg-ink p-6 text-white">
      <h1 className="text-3xl font-bold">{state?.shop ?? t('display.welcome')}</h1>
      {!state || (state.lines.length === 0 && !state.done) ? (
        <p className="m-auto text-4xl font-semibold opacity-80">{t('display.welcome')}</p>
      ) : state.done ? (
        <div className="m-auto flex flex-col items-center gap-4 text-center">
          <p className="text-5xl font-extrabold">{t('display.thanks')}</p>
          <p className="text-3xl">{t('display.paid', { amount: formatRupees(state.done.paid) })}</p>
          {state.done.change > 0 && <p className="text-4xl font-bold text-amber-300">{t('display.change', { amount: formatRupees(state.done.change) })}</p>}
        </div>
      ) : (
        <div className="mt-6 grid flex-1 gap-6 lg:grid-cols-[1fr_auto]">
          <ul className="flex flex-col gap-2 overflow-y-auto text-2xl">
            {state.lines.map((l, i) => (
              <li key={i} className="flex justify-between gap-4 border-b border-white/15 pb-2">
                <span>{l.name} <span className="opacity-70">× {l.quantity}</span></span><span className="font-semibold">{formatRupees(l.amount)}</span>
              </li>
            ))}
          </ul>
          <div className="flex flex-col gap-3 lg:w-96">
            {state.offers.map((o) => <p key={o.name} className="flex justify-between text-xl text-amber-300"><span>🏷 {o.name}</span><span>−{formatRupees(o.saved)}</span></p>)}
            {state.saved > 0 && <p className="text-xl text-emerald-300">{t('display.saved', { amount: formatRupees(state.saved) })}</p>}
            <p className="flex justify-between rounded-xl bg-white/10 p-4 text-4xl font-extrabold"><span>{t('display.total')}</span><span>{formatRupees(state.total)}</span></p>
            {qr && <img src={qr} alt={t('display.scanToPay')} className="w-full rounded-xl bg-white p-2" />}
            {qr && <p className="text-center text-lg opacity-80">{t('display.scanToPay')}</p>}
          </div>
        </div>
      )}
    </div>
  )
}

import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { karat, type Metal } from '../lib/jewellery'
import { btn } from '../lib/buttonStyles'
import { useNow } from '../lib/useNow'

interface Rate { metal: Metal; purity: number; sellPerGram: number }

const W = 1080
const H = 1920
const inr = (n: number) => `₹${Math.round(n).toLocaleString('en-IN')}`

/**
 * Today's gold and silver rates as a picture sized for WhatsApp Status (1080 × 1920): the shop's name, the date, gold per
 * 10 g, silver per kg, and the shop's page link. One tap shares it (on a phone the share sheet offers WhatsApp → My
 * status); where a browser can't share pictures it is saved instead.
 */
export function RateCardImage({ rates, shopName, link }: { rates: Rate[]; shopName: string; link: string | null }) {
  const nowMs = useNow()
  const { t, i18n } = useTranslation()
  const canvas = useRef<HTMLCanvasElement>(null)
  const [note, setNote] = useState('')
  const date = new Date(nowMs).toLocaleDateString(i18n.language === 'en' ? 'en-IN' : `${i18n.language}-IN`, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })
  const rows = useMemo((): [string, string][] => [
    ...[999, 916, 750].flatMap((p): [string, string][] => { const r = rates.find((x) => x.metal === 'gold' && x.purity === p); return r ? [[`${karat('gold', p)} · ${p}`, `${inr(r.sellPerGram * 10)} ${t('jwl6.rateCardPer10')}`]] : [] }),
    ...[999, 925].flatMap((p): [string, string][] => { const r = rates.find((x) => x.metal === 'silver' && x.purity === p); return r ? [[`${t('jwl.silver')} · ${p}`, `${inr(r.sellPerGram * 1000)} ${t('jwl6.rateCardPerKg')}`]] : [] }),
  ], [rates, t])

  useEffect(() => {
    const c = canvas.current
    if (!c || rows.length === 0) return
    c.width = W
    c.height = H
    const ctx = c.getContext('2d')!
    const bg = ctx.createLinearGradient(0, 0, 0, H)
    bg.addColorStop(0, '#3b1d0a')
    bg.addColorStop(1, '#7a4a12')
    ctx.fillStyle = bg
    ctx.fillRect(0, 0, W, H)
    ctx.textAlign = 'center'
    ctx.fillStyle = '#f5d27a'
    ctx.font = 'bold 76px system-ui, sans-serif'
    ctx.fillText(shopName, W / 2, 230, W - 120)
    ctx.fillStyle = '#ffffff'
    ctx.font = 'bold 92px system-ui, sans-serif'
    ctx.fillText(t('jwl6.rateCardTitle'), W / 2, 400)
    ctx.font = '44px system-ui, sans-serif'
    ctx.fillStyle = '#f3e3c3'
    ctx.fillText(date, W / 2, 480, W - 120)
    // One rounded panel per rate.
    // Up to five rows fit between the date and the footer.
    let y = 580
    for (const [label, price] of rows) {
      ctx.fillStyle = 'rgba(255,255,255,0.1)'
      ctx.beginPath()
      ctx.roundRect(80, y, W - 160, 176, 36)
      ctx.fill()
      ctx.textAlign = 'left'
      ctx.fillStyle = '#f5d27a'
      ctx.font = 'bold 54px system-ui, sans-serif'
      ctx.fillText(label, 130, y + 78, 520)
      ctx.textAlign = 'right'
      ctx.fillStyle = '#ffffff'
      ctx.font = 'bold 64px system-ui, sans-serif'
      ctx.fillText(price.split(' ')[0], W - 130, y + 82)
      ctx.font = '36px system-ui, sans-serif'
      ctx.fillStyle = '#f3e3c3'
      ctx.fillText(price.split(' ').slice(1).join(' '), W - 130, y + 136)
      y += 206
    }
    ctx.textAlign = 'center'
    ctx.fillStyle = '#f3e3c3'
    ctx.font = '38px system-ui, sans-serif'
    ctx.fillText(t('jwl6.rateCardGst'), W / 2, H - 230, W - 120)
    if (link) {
      ctx.fillStyle = '#f5d27a'
      ctx.font = 'bold 40px system-ui, sans-serif'
      ctx.fillText(link.replace(/^https?:\/\//, ''), W / 2, H - 150, W - 120)
    }
  }, [rows, shopName, date, link, t])

  async function share() {
    setNote('')
    const c = canvas.current
    if (!c) return
    const blob = await new Promise<Blob | null>((resolve) => c.toBlob(resolve, 'image/png'))
    if (!blob) return
    const file = new File([blob], `rates-${new Date().toISOString().slice(0, 10)}.png`, { type: 'image/png' })
    try {
      if (navigator.canShare?.({ files: [file] })) {
        await navigator.share({ files: [file], title: t('jwl6.rateCardTitle') })
        return
      }
    } catch (e) {
      if (e instanceof DOMException && e.name === 'AbortError') return
    }
    // No picture sharing here (most computers): save it, to post from the phone's gallery.
    const a = document.createElement('a')
    a.href = URL.createObjectURL(blob)
    a.download = file.name
    a.click()
    URL.revokeObjectURL(a.href)
  }

  if (rows.length === 0) return <p className="text-sm text-muted">{t('jwl6.rateCardNoRate')}</p>
  return (
    <div className="flex flex-col gap-2">
      <b className="text-sm">{t('jwl6.rateCard')}</b>
      <canvas ref={canvas} className="w-40 rounded-xl shadow" aria-label={t('jwl6.rateCardTitle')} />
      <button type="button" onClick={() => void share()} className={`h-12 rounded-xl px-4 sm:self-start ${btn.solid}`}>{typeof navigator !== 'undefined' && 'canShare' in navigator ? t('jwl6.rateCardShare') : t('jwl6.rateCardSave')}</button>
      {note && <p className="text-sm text-muted">{note}</p>}
    </div>
  )
}

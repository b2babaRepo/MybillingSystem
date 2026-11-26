import { useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import i18n, { ensureLanguage } from '../i18n'
import { formatRupees } from '../lib/format'
import { isRunning, useOffers } from '../lib/offers'
import { drawPoster, OFFER_LOOK, POSTER_TEMPLATES, posterFile, type PosterGroup } from '../lib/posters'
import { useShopSettings } from '../lib/reports'

const CONTACT_KEY = 'gk-poster-contact'

/**
 * WhatsApp posters: a greeting, a festival wish, a shop notice or a running offer, drawn with the shop's name, how to
 * reach it and a UPI QR, ready for WhatsApp status and groups. Text can be changed and written in Hindi, Gujarati or
 * English. Drawn on the phone: nothing is uploaded.
 */
export function PostersScreen({ shopId, shopName }: { shopId: string; shopName: string }) {
  const { t } = useTranslation()
  const settings = useShopSettings(shopId).data
  const offers = (useOffers(shopId).data ?? []).filter((o) => isRunning(o, Date.now()))
  const [group, setGroup] = useState<PosterGroup>('greeting')
  const [lang, setLang] = useState(() => (['hi', 'gu', 'en'].includes(i18n.language) ? i18n.language : 'hi'))
  const [picked, setPicked] = useState<string>('morning')
  const [title, setTitle] = useState('')
  const [sub, setSub] = useState('')
  const [contact, setContact] = useState(() => { try { return localStorage.getItem(CONTACT_KEY) ?? '' } catch { return '' } })
  const [withQr, setWithQr] = useState(true)
  const [state, setState] = useState<'idle' | 'busy' | 'saved' | 'failed'>('idle')
  const canvas = useRef<HTMLCanvasElement>(null)
  // The poster's language may not be the app's: load its words first.
  const [ready, setReady] = useState<string | null>(null)
  useEffect(() => { let live = true; void ensureLanguage(lang as 'en' | 'hi' | 'gu').then(() => { if (live) setReady(lang) }); return () => { live = false } }, [lang])
  const pt = useMemo(() => i18n.getFixedT(ready ?? 'en'), [ready])

  // The shop's area or address until the owner writes their own line (phone, timings).
  const contactLine = contact || [settings?.address, settings?.area].filter(Boolean).join(', ')
  const offer = offers.find((o) => `offer:${o.id}` === picked)
  const template = POSTER_TEMPLATES.find((p) => p.id === picked)
  const look = offer ? OFFER_LOOK : template ?? POSTER_TEMPLATES[0]

  // A new poster or language fills in its own words; the owner can then change them.
  const [filledFor, setFilledFor] = useState<{ picked: string; pt: typeof pt } | null>(null)
  if (filledFor?.picked !== picked || filledFor.pt !== pt) {
    setFilledFor({ picked, pt })
    if (offer) {
      setTitle(offer.kind === 'percent' ? pt('posters.offerPercent', { value: offer.value }) : pt('posters.offerAmount', { amount: formatRupees(offer.value) }))
      setSub(`${offer.name}\n${pt('posters.offerTill', { date: new Date(offer.endsAt).toLocaleDateString(lang === 'en' ? 'en-IN' : `${lang}-IN`, { day: 'numeric', month: 'short' }) })}`)
    } else if (template) {
      setTitle(pt(`posters.t.${template.id}.title`))
      setSub(pt(`posters.t.${template.id}.sub`))
    }
  }

  useEffect(() => {
    if (!canvas.current) return
    void drawPoster(canvas.current, { ...look, title, sub, shop: settings?.name ?? shopName, contact: contactLine, upi: withQr ? settings?.upiId ?? null : null })
  }, [look, title, sub, settings, shopName, contactLine, withQr])

  function saveContact(value: string) {
    setContact(value)
    try { localStorage.setItem(CONTACT_KEY, value) } catch { /* no storage */ }
  }

  async function share() {
    if (!canvas.current) return
    setState('busy')
    try {
      const file = await posterFile(canvas.current, `${shopName}-${picked}`.replace(/[^\p{L}\p{N}-]+/gu, '-'))
      if (navigator.canShare?.({ files: [file] })) {
        await navigator.share({ files: [file], text: title })
        setState('idle')
      } else {
        download(file)
        setState('saved')
      }
    } catch (e) {
      setState(e instanceof Error && e.name === 'AbortError' ? 'idle' : 'failed')
    }
  }
  function download(file?: File) {
    const go = (f: File) => {
      const a = document.createElement('a')
      a.href = URL.createObjectURL(f)
      a.download = f.name
      a.click()
      setTimeout(() => URL.revokeObjectURL(a.href), 10_000)
    }
    if (file) go(file)
    else if (canvas.current) void posterFile(canvas.current, `${shopName}-${picked}`.replace(/[^\p{L}\p{N}-]+/gu, '-')).then(go)
  }

  const chip = (on: boolean) => `min-h-10 shrink-0 rounded-full px-3 text-sm font-semibold ${on ? 'bg-ink text-white' : 'bg-white text-ink ring-1 ring-line'}`
  const list = group === 'offer' ? [] : POSTER_TEMPLATES.filter((p) => p.group === group)
  return (
    <div className="flex flex-col gap-3 p-4">
      <div>
        <h1 className="text-lg font-extrabold">{t('posters.title')}</h1>
        <p className="text-sm text-muted">{t('posters.hint')}</p>
      </div>
      <div className="flex gap-2 overflow-x-auto pb-1" role="group" aria-label={t('posters.kind')}>
        {(['greeting', 'festival', 'shop', 'offer'] as const).map((g) => (
          <button key={g} onClick={() => { setGroup(g); setPicked(g === 'offer' ? (offers[0] ? `offer:${offers[0].id}` : '') : POSTER_TEMPLATES.find((p) => p.group === g)!.id) }} aria-pressed={group === g} className={chip(group === g)}>{t(`posters.groups.${g}`)}</button>
        ))}
      </div>

      <div className="grid gap-4 lg:grid-cols-[1fr_420px]">
        <div className="flex flex-col gap-3">
          <div className="grid grid-cols-3 gap-2 sm:grid-cols-4">
            {list.map((p) => (
              <button key={p.id} onClick={() => setPicked(p.id)} aria-pressed={picked === p.id}
                className={`flex aspect-square flex-col items-center justify-center gap-1 rounded-2xl p-2 text-center text-xs font-semibold ${picked === p.id ? 'ring-4 ring-brand' : 'ring-1 ring-line'}`}
                style={{ background: `linear-gradient(135deg, ${p.from}, ${p.to})`, color: p.ink }}>
                <span aria-hidden className="text-3xl">{p.art}</span>{t(`posters.names.${p.id}`)}
              </button>
            ))}
            {group === 'offer' && offers.map((o) => (
              <button key={o.id} onClick={() => setPicked(`offer:${o.id}`)} aria-pressed={picked === `offer:${o.id}`}
                className={`flex aspect-square flex-col items-center justify-center gap-1 rounded-2xl p-2 text-center text-xs font-semibold text-white ${picked === `offer:${o.id}` ? 'ring-4 ring-brand' : ''}`}
                style={{ background: `linear-gradient(135deg, ${OFFER_LOOK.from}, ${OFFER_LOOK.to})` }}>
                <span aria-hidden className="text-3xl">🏷️</span>{o.name}
              </button>
            ))}
          </div>
          {group === 'offer' && offers.length === 0 && <p className="rounded-card bg-white p-4 text-sm text-muted">{t('posters.noOffers')}</p>}

          <section className="flex flex-col gap-2 rounded-card bg-white p-3 shadow-sm">
            <label className="text-sm"><span className="mb-1 block font-semibold text-muted">{t('posters.language')}</span>
              <select value={lang} onChange={(e) => setLang(e.target.value)} className="h-11 w-full rounded-xl border border-line bg-white px-2">
                <option value="hi">हिन्दी</option><option value="gu">ગુજરાતી</option><option value="en">English</option>
              </select></label>
            <label className="text-sm"><span className="mb-1 block font-semibold text-muted">{t('posters.headline')}</span>
              <input value={title} onChange={(e) => setTitle(e.target.value)} maxLength={80} className="h-11 w-full rounded-xl border border-line px-3 text-base" /></label>
            <label className="text-sm"><span className="mb-1 block font-semibold text-muted">{t('posters.line')}</span>
              <textarea value={sub} onChange={(e) => setSub(e.target.value)} rows={2} maxLength={160} className="w-full rounded-xl border border-line px-3 py-2 text-base" /></label>
            <label className="text-sm"><span className="mb-1 block font-semibold text-muted">{t('posters.contact')}</span>
              <input value={contact} onChange={(e) => saveContact(e.target.value)} placeholder={contactLine || t('posters.contactPlaceholder')} maxLength={90} className="h-11 w-full rounded-xl border border-line px-3 text-base" /></label>
            {settings?.upiId
              ? <label className="flex min-h-11 items-center gap-2 text-sm font-semibold"><input type="checkbox" checked={withQr} onChange={(e) => setWithQr(e.target.checked)} className="h-5 w-5" />{t('posters.qr', { upi: settings.upiId })}</label>
              : <p className="text-xs text-muted">{t('posters.noUpi')}</p>}
          </section>
        </div>

        <div className="flex flex-col gap-2 lg:sticky lg:top-20 lg:self-start">
          <canvas ref={canvas} aria-label={t('posters.preview')} className="aspect-square w-full rounded-2xl shadow-md" />
          <button disabled={state === 'busy' || (group === 'offer' && !offer)} onClick={() => void share()} className="flex min-h-14 items-center justify-center gap-2 rounded-2xl bg-[#15803d] px-4 text-lg font-bold text-white shadow-sm disabled:opacity-50">
            <span aria-hidden>💬</span>{t('posters.share')}</button>
          <button disabled={group === 'offer' && !offer} onClick={() => download()} className="min-h-12 rounded-2xl border border-line bg-white px-4 font-semibold disabled:opacity-50">⬇ {t('posters.download')}</button>
          {state === 'saved' && <p role="status" className="rounded-xl bg-brand-soft p-2 text-sm text-brand-deep">{t('posters.saved')}</p>}
          {state === 'failed' && <p role="status" className="rounded-xl bg-due-soft p-2 text-sm text-due">{t('posters.failed')}</p>}
        </div>
      </div>
    </div>
  )
}

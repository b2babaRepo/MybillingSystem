import { lazy, Suspense, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useShopSearch, type ShopEntry } from '../lib/planner'
import { cameraPossible } from '../lib/barcode'

const ScanSheet = lazy(() => import('./ScanSheet').then((m) => ({ default: m.ScanSheet })))

/**
 * Connect to one shop the customer already knows: its ID (GKS-000042), the owner's mobile, or the QR code the shop shows
 * (its store link). The server matches these exactly, so a part of a number lists nobody.
 */
export function ConnectShop({ onPick, current }: { onPick: (shop: ShopEntry) => void; current?: string | null }) {
  const { t } = useTranslation()
  const [text, setText] = useState('')
  const [asked, setAsked] = useState('')
  const [scanning, setScanning] = useState(false)
  const [message, setMessage] = useState('')
  const found = useShopSearch(asked)
  const results = asked ? (found.data ?? []) : []

  function find(value: string) {
    setMessage('')
    setAsked(value.trim())
  }

  function onCode(code: string) {
    // The shop's QR is its store link (…/#/s/its-name); anything else isn't a shop's code.
    if (!/\/s\/[a-z0-9-]{3,}/i.test(code)) {
      setMessage(t('connect.notShopQr'))
      return false
    }
    setScanning(false)
    setText('')
    find(code)
    return true
  }

  return (
    <div className="flex flex-col gap-2 rounded-2xl bg-white p-3">
      <span className="font-bold">{t('connect.title')}</span>
      <span className="text-[13px] text-muted">{t('connect.hint')}</span>
      <form onSubmit={(e) => { e.preventDefault(); find(text) }} className="flex gap-2">
        <input value={text} onChange={(e) => setText(e.target.value)} placeholder={t('connect.placeholder')} aria-label={t('connect.placeholder')}
          className="h-11 min-w-0 flex-1 rounded-xl border border-line px-3" />
        <button disabled={!text.trim()} className="h-11 rounded-xl bg-brand px-4 font-bold text-white disabled:opacity-50">{t('connect.find')}</button>
      </form>
      {cameraPossible() && (
        <button onClick={() => { setMessage(''); setScanning(true) }} className="flex min-h-11 items-center justify-center gap-2 rounded-xl border border-brand-line/50 font-bold text-brand">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden><path d="M4 8V5a1 1 0 0 1 1-1h3M16 4h3a1 1 0 0 1 1 1v3M20 16v3a1 1 0 0 1-1 1h-3M8 20H5a1 1 0 0 1-1-1v-3M8 8h3v3H8zM13 13h3v3h-3z" /></svg>
          {t('connect.scan')}
        </button>
      )}
      {message && <p className="text-sm text-due">{message}</p>}
      {asked && found.isSuccess && results.length === 0 && <p className="text-sm text-muted">{t('connect.none')}</p>}
      {results.map((s) => (
        <button key={s.id} onClick={() => { setAsked(''); setText(''); onPick(s) }} aria-pressed={current === s.id}
          className="flex min-h-12 items-center gap-3 rounded-xl border border-line px-3 text-left">
          <span aria-hidden className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-brand font-extrabold text-white">{s.name.charAt(0)}</span>
          <span className="flex min-w-0 flex-1 flex-col">
            <span className="truncate font-bold">{s.name}</span>
            <span className="truncate text-[13px] text-muted">{[s.number ? `GKS-${String(s.number).padStart(6, '0')}` : null, s.area].filter(Boolean).join(' · ')}</span>
          </span>
          <span className="shrink-0 text-sm font-bold text-brand">{t('connect.connect')} ›</span>
        </button>
      ))}
      {scanning && <Suspense fallback={null}><ScanSheet onCode={onCode} onClose={() => setScanning(false)} /></Suspense>}
    </div>
  )
}

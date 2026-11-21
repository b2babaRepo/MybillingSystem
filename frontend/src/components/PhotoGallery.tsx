import { useEffect, useRef, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

/**
 * An item's photos full screen, like a shopping app's product view: swipe (or the arrows, or the keyboard) through
 * them, dots and small pictures below show which one is open.
 */
export function PhotoGallery({ title, photos, start = 0, onClose, footer }: { title: string; photos: { full: string; thumb: string }[]; start?: number; onClose: () => void; footer?: ReactNode }) {
  const { t } = useTranslation()
  const [at, setAt] = useState(start)
  const strip = useRef<HTMLDivElement>(null)

  const go = (index: number) => {
    const next = Math.max(0, Math.min(photos.length - 1, index))
    setAt(next)
    strip.current?.children[next]?.scrollIntoView({ behavior: 'smooth', inline: 'center', block: 'nearest' })
  }

  useEffect(() => {
    strip.current?.children[start]?.scrollIntoView({ inline: 'center', block: 'nearest' })
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
      else if (e.key === 'ArrowRight') go(at + 1)
      else if (e.key === 'ArrowLeft') go(at - 1)
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  })

  // Swiping moves the strip: the photo nearest the middle is the open one.
  const onScroll = () => {
    const el = strip.current
    if (!el) return
    const index = Math.round(el.scrollLeft / el.clientWidth)
    if (index !== at) setAt(index)
  }

  return (
    <div role="dialog" aria-modal="true" aria-label={title} className="fixed inset-0 z-50 flex flex-col bg-black/95 text-white">
      <div className="flex items-center gap-2 px-4 py-3">
        <span className="min-w-0 flex-1 truncate font-bold">{title}</span>
        <span className="text-sm opacity-75">{at + 1} / {photos.length}</span>
        <button onClick={onClose} aria-label={t('a11y.close')} className="h-11 w-11 rounded-full text-2xl hover:bg-white/10">✕</button>
      </div>
      <div className="relative flex-1">
        <div ref={strip} onScroll={onScroll} className="flex h-full snap-x snap-mandatory overflow-x-auto [scrollbar-width:none]">
          {photos.map((p, i) => (
            <div key={p.full} className="flex h-full w-full shrink-0 snap-center items-center justify-center p-4">
              <img src={p.full} alt={`${title} ${i + 1}`} className="max-h-full max-w-full rounded-xl bg-white object-contain" />
            </div>
          ))}
        </div>
        {at > 0 && <button onClick={() => go(at - 1)} aria-label={t('gallery.previous')} className="absolute left-2 top-1/2 h-12 w-12 -translate-y-1/2 rounded-full bg-white/20 text-2xl hover:bg-white/30">‹</button>}
        {at < photos.length - 1 && <button onClick={() => go(at + 1)} aria-label={t('gallery.next')} className="absolute right-2 top-1/2 h-12 w-12 -translate-y-1/2 rounded-full bg-white/20 text-2xl hover:bg-white/30">›</button>}
      </div>
      {footer && <div className="mx-auto w-full max-w-md px-4 text-white/80 [&_*]:text-white/80">{footer}</div>}
      {photos.length > 1 && (
        <div className="flex justify-center gap-2 overflow-x-auto px-4 pb-5 pt-2">
          {photos.map((p, i) => (
            <button key={p.thumb} onClick={() => go(i)} aria-label={t('gallery.photo', { n: i + 1 })} aria-current={i === at}
              className={`h-14 w-14 shrink-0 overflow-hidden rounded-lg border-2 bg-white ${i === at ? 'border-amber-400' : 'border-transparent opacity-60'}`}>
              <img src={p.thumb} alt="" className="h-full w-full object-contain" />
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

import { useEffect, useRef, useState, type ReactNode } from 'react'
import { itemArt } from '../lib/itemArt'

/**
 * A grocery card. With a shop's photo: the product on a soft tint, like the shopping apps' cards. Without one:
 * the item's picture floating gently on its aisle's colours, a soft shine passing over it. Then the name and
 * price. Used wherever people pick items: billing tiles, the shop's common list, the household planner and the
 * online store. Motion stops for people who turned off animations.
 */
export function ItemCard({ item, title, subtitle, badge, onClick, selected, compact, square, imageUrl, photos, onPhotoClick, children }: {
  item: { catalogueItemId?: number | null; aisle?: string | null; category?: string | null; name?: string }
  title: string
  subtitle?: ReactNode
  /** Corner label ("✓ Added", a count). */
  badge?: ReactNode
  onClick?: () => void
  selected?: boolean
  compact?: boolean
  /** Shopping-app card for a grid: a square photo box and a smaller two-line name. */
  square?: boolean
  /** The shop's photo (lib/images.ts); the drawn picture shows until it loads, and if it fails. */
  imageUrl?: string
  /** Several photos (square cards): they slide by themselves and can be swiped by hand, dots below. */
  photos?: string[]
  /** Tapping the photo (only the photo, not the buttons below it), e.g. to open a gallery. */
  onPhotoClick?: () => void
  children?: ReactNode
}) {
  const art = itemArt(item)
  const [photoLoaded, setPhotoLoaded] = useState(false)
  const [photoFailed, setPhotoFailed] = useState(false)
  const photo = imageUrl && !photoFailed
  const Wrapper = onClick ? 'button' : 'div'
  if (square && photos && photos.length > 1) {
    return (
      <Wrapper onClick={onClick} className={`item-card group relative flex h-full w-full flex-col overflow-hidden rounded-card bg-white text-left shadow-[0_2px_10px_rgba(16,32,29,0.08)] transition hover:shadow-[0_8px_22px_rgba(16,32,29,0.12)] ${selected ? 'ring-[3px] ring-brand-line shadow-[0_6px_18px_rgba(13,148,136,0.25)]' : ''}`}>
        <PhotoStrip photos={photos} onPhotoClick={onPhotoClick} badge={badge} />
        <div className="flex flex-1 flex-col p-2">
          <div className="line-clamp-2 min-h-[2.6em] text-[13px] leading-[1.3] font-semibold">{title}</div>
          {subtitle && <div className="mt-0.5 text-sm text-muted">{subtitle}</div>}
          {children}
        </div>
      </Wrapper>
    )
  }
  return (
    <Wrapper
      onClick={onClick}
      className={`item-card group relative flex h-full w-full flex-col overflow-hidden rounded-card bg-white text-left shadow-[0_2px_10px_rgba(16,32,29,0.08)] transition hover:-translate-y-0.5 hover:shadow-[0_8px_22px_rgba(16,32,29,0.12)] active:scale-[0.97] ${
        selected ? 'ring-[3px] ring-brand-line shadow-[0_6px_18px_rgba(13,148,136,0.25)]' : ''
      }`}
    >
      <div
        className={`relative flex items-center justify-center overflow-hidden ${square ? 'aspect-square' : compact ? 'h-16' : 'h-24'} ${onPhotoClick && photo ? 'cursor-zoom-in' : ''}`}
        onClick={onPhotoClick && photo ? (e) => { e.stopPropagation(); onPhotoClick() } : undefined}
        style={{ background: photo && photoLoaded ? (square ? '#ffffff' : art.tint) : square ? art.tint : `linear-gradient(135deg, ${art.from}, ${art.to})` }}
      >
        {!(photo && photoLoaded) && square && <span aria-hidden className="text-[40px] opacity-80">{art.emoji}</span>}
        {!(photo && photoLoaded) && !square && (
          <>
            <span aria-hidden className="item-card-bubble left-2 top-2 h-6 w-6" />
            <span aria-hidden className="item-card-bubble bottom-1 right-4 h-9 w-9 [animation-delay:1.2s]" />
            <span aria-hidden className={`item-card-emoji drop-shadow-[0_4px_6px_rgba(0,0,0,0.25)] ${compact ? 'text-[34px]' : 'text-[48px]'}`}>{art.emoji}</span>
            <span aria-hidden className="item-card-shine" />
          </>
        )}
        {photo && (
          <img
            src={imageUrl}
            alt=""
            loading="lazy"
            decoding="async"
            width={square ? 160 : compact ? 64 : 96}
            height={square ? 160 : compact ? 64 : 96}
            onLoad={() => setPhotoLoaded(true)}
            onError={() => setPhotoFailed(true)}
            className={`absolute inset-0 m-auto h-[88%] w-auto rounded-xl object-contain mix-blend-multiply ${photoLoaded ? '' : 'opacity-0'}`}
          />
        )}
        {badge && <span className="absolute right-1.5 top-1.5 rounded-full bg-white/90 px-2 py-0.5 text-[11px] font-bold text-ink shadow">{badge}</span>}
      </div>
      <div className="flex flex-1 flex-col p-2">
        <div className={square ? 'line-clamp-2 min-h-[2.6em] text-[13px] leading-[1.3] font-semibold' : 'line-clamp-2 text-[15px] leading-tight font-bold'}>{title}</div>
        {subtitle && <div className="mt-0.5 text-sm text-muted">{subtitle}</div>}
        {children}
      </div>
    </Wrapper>
  )
}

/**
 * An item's photos inside its card: one at a time, sliding on every few seconds and swipeable by hand (the slide stops
 * while a finger or the mouse is on it, and for people who turned off animations); dots show which one is showing.
 */
function PhotoStrip({ photos, onPhotoClick, badge }: { photos: string[]; onPhotoClick?: () => void; badge?: ReactNode }) {
  const strip = useRef<HTMLDivElement>(null)
  const [at, setAt] = useState(0)
  const [paused, setPaused] = useState(false)
  const go = (index: number) => {
    const el = strip.current
    if (el) el.scrollTo({ left: index * el.clientWidth, behavior: 'smooth' })
  }
  useEffect(() => {
    if (paused || window.matchMedia?.('(prefers-reduced-motion: reduce)').matches) return
    const timer = window.setInterval(() => go((at + 1) % photos.length), 3500)
    return () => window.clearInterval(timer)
  }, [at, paused, photos.length])
  return (
    <div className="relative aspect-square bg-white" onPointerEnter={() => setPaused(true)} onPointerLeave={() => setPaused(false)} onTouchStart={() => setPaused(true)}>
      <div ref={strip} onScroll={(e) => setAt(Math.round(e.currentTarget.scrollLeft / e.currentTarget.clientWidth))}
        className="flex h-full snap-x snap-mandatory overflow-x-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
        {photos.map((src) => (
          <div key={src} className={`flex h-full w-full shrink-0 snap-center items-center justify-center ${onPhotoClick ? 'cursor-zoom-in' : ''}`}
            onClick={onPhotoClick ? (e) => { e.stopPropagation(); onPhotoClick() } : undefined}>
            <img src={src} alt="" loading="lazy" decoding="async" className="h-[88%] w-auto rounded-xl object-contain mix-blend-multiply" />
          </div>
        ))}
      </div>
      <div className="pointer-events-none absolute inset-x-0 bottom-1.5 flex justify-center gap-1">
        {photos.map((src, i) => <span key={src} className={`h-1.5 rounded-full transition-all ${i === at ? 'w-4 bg-brand' : 'w-1.5 bg-black/25'}`} />)}
      </div>
      {badge && <span className="absolute right-1.5 top-1.5 rounded-full bg-white/90 px-2 py-0.5 text-[11px] font-bold text-ink shadow">{badge}</span>}
    </div>
  )
}

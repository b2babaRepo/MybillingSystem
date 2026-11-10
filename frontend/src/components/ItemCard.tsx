import { useState, type ReactNode } from 'react'
import { itemArt } from '../lib/itemArt'

/**
 * A grocery card. With a shop's photo: the product on a soft tint, like the shopping apps' cards. Without one:
 * the item's picture floating gently on its aisle's colours, a soft shine passing over it. Then the name and
 * price. Used wherever people pick items: billing tiles, the shop's common list, the household planner and the
 * online store. Motion stops for people who turned off animations.
 */
export function ItemCard({ item, title, subtitle, badge, onClick, selected, compact, square, imageUrl, children }: {
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
  children?: ReactNode
}) {
  const art = itemArt(item)
  const [photoLoaded, setPhotoLoaded] = useState(false)
  const [photoFailed, setPhotoFailed] = useState(false)
  const photo = imageUrl && !photoFailed
  const Wrapper = onClick ? 'button' : 'div'
  return (
    <Wrapper
      onClick={onClick}
      className={`item-card group relative flex w-full flex-col overflow-hidden rounded-[18px] bg-white text-left shadow-[0_2px_10px_rgba(16,32,29,0.08)] transition hover:-translate-y-0.5 hover:shadow-[0_8px_22px_rgba(16,32,29,0.12)] active:scale-[0.97] ${
        selected ? 'ring-[3px] ring-teal-600 shadow-[0_6px_18px_rgba(13,148,136,0.25)]' : ''
      }`}
    >
      <div
        className={`relative flex items-center justify-center overflow-hidden ${square ? 'aspect-square' : compact ? 'h-16' : 'h-24'}`}
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

import type { ReactNode } from 'react'
import { itemArt } from '../lib/itemArt'

/**
 * A colourful grocery card: the item's picture floating gently on its category's colours, a soft shine passing
 * over it, then the name and price. Used wherever people pick items: billing tiles, the shop's common list,
 * the household planner and the online store. Motion stops for people who turned off animations.
 */
export function ItemCard({ item, title, subtitle, badge, onClick, selected, compact, children }: {
  item: { catalogueItemId?: number | null; category?: string | null; name?: string }
  title: string
  subtitle?: ReactNode
  /** Corner label ("✓ Added", "Low", a price). */
  badge?: ReactNode
  onClick?: () => void
  selected?: boolean
  compact?: boolean
  children?: ReactNode
}) {
  const art = itemArt(item)
  const Wrapper = onClick ? 'button' : 'div'
  return (
    <Wrapper
      onClick={onClick}
      className={`item-card group relative flex flex-col overflow-hidden rounded-[18px] bg-white text-left shadow-[0_2px_10px_rgba(16,32,29,0.08)] transition active:scale-[0.97] ${
        selected ? 'ring-[3px] ring-teal-600' : ''
      }`}
    >
      <div
        className={`relative flex items-center justify-center overflow-hidden ${compact ? 'h-16' : 'h-24'}`}
        style={{ background: `linear-gradient(135deg, ${art.from}, ${art.to})` }}
      >
        <span aria-hidden className="item-card-bubble left-2 top-2 h-6 w-6" />
        <span aria-hidden className="item-card-bubble bottom-1 right-4 h-9 w-9 [animation-delay:1.2s]" />
        <span aria-hidden className={`item-card-emoji drop-shadow-[0_4px_6px_rgba(0,0,0,0.25)] ${compact ? 'text-[34px]' : 'text-[48px]'}`}>{art.emoji}</span>
        <span aria-hidden className="item-card-shine" />
        {badge && <span className="absolute right-1.5 top-1.5 rounded-full bg-white/90 px-2 py-0.5 text-[11px] font-bold text-ink shadow">{badge}</span>}
      </div>
      <div className="flex flex-1 flex-col p-2">
        <div className="line-clamp-2 text-[15px] leading-tight font-bold">{title}</div>
        {subtitle && <div className="mt-0.5 text-sm text-muted">{subtitle}</div>}
        {children}
      </div>
    </Wrapper>
  )
}

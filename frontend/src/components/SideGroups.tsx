import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'

// Pages a limited helper can't use (the server refuses them anyway).
const LIMITED: Record<string, string[]> = {
  reports: ['/reports', '/pnl', '/gst', '/cashbook', '/cashledger', '/bankbook'],
  expenses: ['/expenses'],
  purchases: ['/suppliers', '/reorder'],
}
const hidden = (blocked: string[], path: string) => path !== '/reports/day' && blocked.some((b) => (LIMITED[b] ?? []).some((p) => path === p || path.startsWith(p + '/')))
import { navigate } from '../router'

export interface SideGroup { key: string; title: string; add?: { path: string; label: string }; links: { path: string; label: string }[] }

const ICONS: Record<string, string> = {
    '/cashledger': 'M4 4h16v16H4zM8 9h8M8 13h8M8 17h5',
    '/dues': 'M12 8v4m0 4h.01M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18Z',
    '/receipts': 'M4 7h16v10H4zM12 12h.01M7 12h.01M17 12h.01',
    '/cashbook': 'M5 4h14v16H5zM9 4v16',
    '/expenses': 'M4 7h16v12H4zM16 13h.01',
    '/reports': 'M4 20V10M10 20V4M16 20v-7M22 20H2',
    '/pnl': 'M3 17l6-6 4 4 8-8M15 7h6v6',
    '/loans': 'M3 10l9-6 9 6M5 10v8M9 10v8M15 10v8M19 10v8M3 20h18',
    '/verify-data': 'M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6zM8.5 12l2.5 2.5L16 9.5',
    '/trial-balance': 'M4 5h16M4 12h16M4 19h16M12 5v14',
    '/gst-annual': 'M6 3h12v18H6zM9 8h6M9 12h6M9 16h3',
    '/gst': 'M6 3h12v18l-3-2-3 2-3-2-3 2zM9 9h6M9 13h6',
    '/stocktake': 'M9 5h6M9 3h6v4H9zM5 5h2v16h10V5h2M9 12l2 2 4-4',
    '/estimates': 'M14 3H6v18h12V7zM14 3v4h4M9 13h6M9 17h4',
    '/purchase-uploads': 'M12 16V4M7 9l5-5 5 5M4 16v4h16v-4',
    '/suppliers': 'M3 9l1.5-5h15L21 9M4 9v11h16V9M9 20v-6h6v6',
    '/jewellery': 'M6 3h12l3 6-9 12L3 9zM3 9h18M9 3l3 6 3-6M12 9v12',
    '/find-bill': 'M6 2h12v20l-3-2-3 2-3-2-3 2zM9 7h6M9 11h6M9 15h4',
    '/exchange-slips': 'M7 4L3 8l4 4M3 8h14M17 20l4-4-4-4M21 16H7',
    '/sale-orders': 'M9 3h6v3H9zM6 5h3M15 5h3v16H6V5M9 12h6M9 16h4',
    '/holds': 'M12 3a9 9 0 100 18 9 9 0 000-18zM12 7v5l3 2',
    '/wa-inbox': 'M4 4h16v12H8l-4 4zM8 9h8M8 12h5',
    '/orders-placed': 'M9 3h6v3H9zM6 5h3M15 5h3v16H6V5M9 13l2 2 4-4',
    '/goods-to-come': 'M2 6h13v11H2zM15 10h4l3 3v4h-7M6 20h.01M18 20h.01',
    '/goods-in': 'M3 7l9-4 9 4v10l-9 4-9-4zM8 12l3 3 5-5',
    '/godowns': 'M3 21V9l9-6 9 6v12M7 21v-8h10v8M7 17h10',
    '/bardana': 'M7 3h10l-1 4 3 3v11H5V10l3-3zM8 7h8',
    '/festivals': 'M12 3l2.8 5.7 6.2.9-4.5 4.4 1 6.2L12 17.3 6.5 20.2l1-6.2L3 9.6l6.2-.9z',
    '/lots': 'M5 3h14v18H5zM8 7h8M8 12h.01M12 12h.01M16 12h.01M8 16h.01M12 16h.01M16 16h.01',
    '/received-by-supplier': 'M3 13l3-9h12l3 9v7H3zM3 13h5l1 3h6l1-3h5',
    '/stock-statement': 'M5 3h10l4 4v14H5zM8 11h8M8 15h8M8 7h4',
    '/stock-health': 'M3 12h4l3-8 4 16 3-8h4',
    '/categories': 'M3 6h6l2 2h10v12H3z',
    '/sales-by': 'M12 3v9h9A9 9 0 1112 3zM15 3a6 6 0 016 6h-6z',
    '/balance-sheet': 'M12 3v18M5 7h14M5 7l-3 7h6zM19 7l-3 7h6zM8 21h8',
    '/discounts': 'M19 5L5 19M7 7h.01M17 17h.01',
    '/medical/stock': 'M10 3h4v7h7v4h-7v7h-4v-7H3v-4h7z',
    '/medical/h1': 'M5 3h14v18H5zM9 3v18M12 8h4M12 12h4',
    '/medical/refills': 'M4 12a8 8 0 0114-5l2 2M20 4v5h-5M20 12a8 8 0 01-14 5l-2-2M4 20v-5h5',
    '/medical/returns': 'M9 14L4 9l5-5M4 9h11a5 5 0 010 10h-3',
    '/medical/month': 'M3 6h18v15H3zM3 10h18M8 3v4M16 3v4',
    '/reorder': 'M4 4h2l2 11h11l2-8H7M10 20h.01M17 20h.01',
    '/posters': 'M4 4h16v16H4zM8 15l3-3 2 2 3-4 2 3M8.5 8.5h.01',
    '/offers': 'M3 12V3h9l9 9-9 9zM7.5 7.5h.01',
    '/settings': 'M12 9a3 3 0 100 6 3 3 0 000-6zM12 2v3M12 19v3M4.2 4.2l2.1 2.1M17.7 17.7l2.1 2.1M2 12h3M19 12h3M4.2 19.8l2.1-2.1M17.7 6.3l2.1-2.1',
    '/reports/day': 'M4 5h16v15H4zM4 9h16M8 3v4M16 3v4M8 13h8M8 16h5',
    '/reminders': 'M6 16V11a6 6 0 0 1 12 0v5l2 2H4zM10 21h4',
    '/disputes': 'M4 5h16v11H9l-5 4zM12 8v3M12 13h.01',
    '/bankbook': 'M3 10l9-6 9 6M6 10v8M12 10v8M18 10v8M3 20h18',
    '/cheques': 'M3 7h18v10H3zM6 13h6M15 13h3',
    '/store': 'M4 9l1-5h14l1 5M4 9h16v11H4zM9 20v-6h6v6',
    '/sync': 'M4 12a8 8 0 0114-5l2 2M20 4v5h-5M20 12a8 8 0 01-14 5l-2-2M4 20v-5h5',
    '/backup': 'M7 18a5 5 0 0 1-.5-10 6 6 0 0 1 11.5 1.5A4 4 0 0 1 17 18zM12 10v6M9.5 13.5 12 16l2.5-2.5',
  }

/**
 * The desktop side menu's groups (sell, customers, buy, money, stock, reports, more). Each folds on its heading and has a
 * + for its most common job; folded groups are remembered on this computer. Loaded after the first screen.
 */
export function SideGroups({ groups, open, current }: { groups: SideGroup[]; open: boolean; current?: string }) {
  const limits = useQuery({ queryKey: ['my-limits'], queryFn: () => api<string[]>('/v1/shop/my-limits'), staleTime: 60_000 })
  const blocked = limits.data ?? []
  const sideGroups = blocked.length === 0 ? groups : groups.map((g) => ({ ...g, add: g.add && hidden(blocked, g.add.path) ? undefined : g.add, links: g.links.filter((l) => !hidden(blocked, l.path)) }))
  // Folded groups, remembered on this computer.
  const [folded, setFoldedState] = useState<string[]>(() => { try { return JSON.parse(localStorage.getItem('gk-menu-folded') ?? '[]') as string[] } catch { return [] } })
  const toggleGroup = (key: string) => {
    const next = folded.includes(key) ? folded.filter((k) => k !== key) : [...folded, key]
    setFoldedState(next)
    try { localStorage.setItem('gk-menu-folded', JSON.stringify(next)) } catch { /* private window */ }
  }

  return (
    <>
        {sideGroups.map((group) => group.links.length > 0 && (
          <div key={group.key} className="hidden lg:flex lg:flex-col lg:gap-0.5">
            {open
              ? (
                <div className="mt-3 flex items-center gap-1 pr-1">
                  <button type="button" onClick={() => toggleGroup(group.key)} aria-expanded={!folded.includes(group.key)}
                    className="flex min-h-9 flex-1 items-center justify-between rounded-lg px-4 text-xs font-bold tracking-wide text-muted uppercase hover:bg-[#f1f5f4]">
                    {group.title}
                    <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true" className={folded.includes(group.key) ? '' : 'rotate-180'}><path d="M6 9l6 6 6-6" /></svg>
                  </button>
                  {group.add && (
                    <button type="button" onClick={() => navigate(group.add!.path)} title={group.add.label} aria-label={group.add.label}
                      className="flex h-9 w-9 items-center justify-center rounded-full text-brand hover:bg-brand-soft">
                      <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true"><circle cx="12" cy="12" r="9" /><path d="M12 8v8M8 12h8" /></svg>
                    </button>
                  )}
                </div>
              )
              : <span className="mx-2 mt-3 mb-1 border-t border-line" aria-hidden="true" />}
            {(!open || !folded.includes(group.key)) && group.links.map((link) => (
              <button
                key={link.path}
                onClick={() => navigate(link.path)}
                title={link.label}
                aria-current={current === link.path ? 'page' : undefined}
                className={`flex min-h-10 items-center gap-3 rounded-xl text-left text-[15px] ${open ? 'px-4' : 'justify-center px-0'} ${current === link.path ? 'bg-brand-soft font-bold text-brand' : 'font-semibold text-muted hover:bg-[#f1f5f4]'}`}
              >
                <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" className="shrink-0">
                  <path d={ICONS[link.path] ?? 'M12 12h.01'} />
                </svg>
                <span className={open ? '' : 'sr-only'}>{link.label}</span>
              </button>
            ))}
          </div>
        ))}
    </>
  )
}

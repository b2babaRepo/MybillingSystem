import type { ReactNode } from 'react'

/**
 * Desktop (1024px+): the list on the left and the chosen item's detail beside it, both in view.
 * Phone: one at a time — the detail when something is chosen, otherwise the list.
 */
export function ListDetail({ list, detail, placeholder, hasDetail }: {
  list: ReactNode
  detail: ReactNode
  /** Shown on the right on desktop when nothing is chosen yet. */
  placeholder?: ReactNode
  hasDetail: boolean
}) {
  return (
    <div className="lg:grid lg:grid-cols-[minmax(320px,380px)_minmax(0,1fr)] lg:items-start">
      <div className={`${hasDetail ? 'hidden lg:block' : ''} lg:sticky lg:top-20 lg:max-h-[calc(100dvh-6rem)] lg:overflow-y-auto`}>{list}</div>
      <div className={hasDetail ? '' : 'hidden lg:block'}>
        {hasDetail ? detail : placeholder && <div className="m-4 flex min-h-64 items-center justify-center rounded-[18px] bg-white p-6 text-center text-muted">{placeholder}</div>}
      </div>
    </div>
  )
}

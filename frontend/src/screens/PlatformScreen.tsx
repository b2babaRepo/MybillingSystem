import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BackIcon } from '../components/Icons'
import { Button } from '../components/ui'
import { api, request } from '../lib/api'
import { formatDateTime, formatPhone } from '../lib/format'
import { navigate } from '../router'

interface Overview { shops: number; active: number; blocked: number; awaitingApproval: number; newThisWeek: number; households: number }
interface ShopRow {
  id: string
  name: string
  area: string | null
  slug: string
  createdAt: string
  approvedAt: string | null
  isActive: boolean
  blockedReason: string | null
  ownerName: string | null
  ownerPhone: string | null
}
interface Person { userId: string; membershipId: string; name: string; phone: string; role: 'owner' | 'staff'; isActive: boolean; lockedOut: boolean }
interface Detail { shop: ShopRow; address: string | null; adminNote: string | null; people: Person[]; customers: number; billsLast30Days: number; lastBillAt: string | null; lastBalanceCheck: { mismatches: number } | null }

type Status = 'new' | 'active' | 'blocked' | 'all'

/**
 * The platform admin's view of every shop: counts at the top, then new / active / blocked shops with search. Open a
 * shop to see its people (call or WhatsApp them, unlock a lockout), its activity, a private support note, and to
 * approve, block (with a reason) or unblock it.
 */
export function PlatformScreen() {
  const { t } = useTranslation()
  const [status, setStatus] = useState<Status>('new')
  const [q, setQ] = useState('')
  const [openId, setOpenId] = useState<string | null>(null)
  const overview = useQuery({ queryKey: ['platform', 'overview'], queryFn: () => api<Overview>('/v1/platform/overview') })
  const shops = useQuery({
    queryKey: ['platform', 'shops', status, q],
    queryFn: () => api<ShopRow[]>(`/v1/platform/shops?status=${status}&q=${encodeURIComponent(q.trim())}`),
  })
  const tiles: [string, number | undefined][] = [
    [t('platform.awaiting'), overview.data?.awaitingApproval],
    [t('platform.active'), overview.data?.active],
    [t('platform.blocked'), overview.data?.blocked],
    [t('platform.newWeek'), overview.data?.newThisWeek],
    [t('platform.households'), overview.data?.households],
  ]

  return (
    <div className="mx-auto min-h-dvh max-w-3xl pb-10">
      <header className="sticky top-0 z-10 flex items-center gap-2 bg-slate-800 px-4 py-3 text-white">
        <button onClick={() => navigate('/')} className="-ml-2 flex h-11 w-11 items-center justify-center" aria-label={t('a11y.back')}><BackIcon /></button>
        <h1 className="text-xl font-bold">{t('platform.title')}</h1>
      </header>

      <div className="grid grid-cols-3 gap-2 p-4 sm:grid-cols-5">
        {tiles.map(([label, value]) => (
          <div key={label} className="rounded-2xl bg-white p-3">
            <div className="text-2xl font-extrabold tabular-nums">{value ?? '–'}</div>
            <div className="text-xs text-muted">{label}</div>
          </div>
        ))}
      </div>

      <div className="flex flex-col gap-3 px-4">
        <div role="tablist" className="grid grid-cols-4 gap-1 rounded-2xl bg-white p-1 text-sm font-semibold">
          {(['new', 'active', 'blocked', 'all'] as const).map((s) => (
            <button key={s} role="tab" aria-selected={status === s} onClick={() => setStatus(s)}
              className={`min-h-11 rounded-xl ${status === s ? 'bg-teal-700 text-white' : ''}`}>{t(`platform.tab.${s}`)}</button>
          ))}
        </div>
        <label className="sr-only" htmlFor="platform-search">{t('platform.search')}</label>
        <input id="platform-search" value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('platform.search')}
          className="min-h-12 rounded-2xl border border-line bg-white px-4" />

        {shops.data?.length === 0 && <p className="p-6 text-center text-muted">{t('platform.none')}</p>}
        <ul className="flex flex-col gap-2">
          {shops.data?.map((s) => (
            <li key={s.id} className="rounded-2xl bg-white">
              <button onClick={() => setOpenId(openId === s.id ? null : s.id)} aria-expanded={openId === s.id} className="flex w-full items-start gap-3 p-4 text-left">
                <span className="flex min-w-0 flex-1 flex-col">
                  <span className="truncate text-[17px] font-bold">{s.name}</span>
                  <span className="truncate text-sm text-muted">
                    {[s.area, s.ownerName, formatPhone(s.ownerPhone)].filter(Boolean).join(' · ')}
                  </span>
                  <span className="text-xs text-muted">{t('platform.opened', { date: formatDateTime(s.createdAt) })}</span>
                </span>
                <StatusChip shop={s} />
              </button>
              {openId === s.id && <ShopDetail id={s.id} />}
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}

function StatusChip({ shop }: { shop: ShopRow }) {
  const { t } = useTranslation()
  const [label, style] = !shop.isActive
    ? [t('platform.chip.blocked'), 'bg-red-50 text-red-700']
    : !shop.approvedAt
      ? [t('platform.chip.new'), 'bg-amber-100 text-amber-900']
      : [t('platform.chip.active'), 'bg-teal-50 text-teal-800']
  return <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs font-bold ${style}`}>{label}</span>
}

function ShopDetail({ id }: { id: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const detail = useQuery({ queryKey: ['platform', 'shop', id], queryFn: () => api<Detail>(`/v1/platform/shops/${id}`) })
  const [note, setNote] = useState<string | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const d = detail.data
  if (!d) return <p className="px-4 pb-4 text-muted">{t('platform.loading')}</p>

  async function run(path: string, body: object = {}, method: 'POST' | 'PUT' = 'POST', done?: string) {
    setBusy(true)
    setMessage(null)
    try {
      const response = await request(path, method, body)
      if (!response.ok) throw new Error(((await response.json()) as { errors?: Record<string, string[]>; title?: string }).errors?.Reason?.[0] ?? t('error'))
      await queryClient.invalidateQueries({ queryKey: ['platform'] })
      if (done) setMessage(done)
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  function block() {
    const reason = prompt(t('platform.blockAsk', { name: d!.shop.name }))?.trim()
    if (reason) void run(`/v1/platform/shops/${id}/block`, { reason })
  }

  const stats: [string, string | number][] = [
    [t('platform.customers'), d.customers],
    [t('platform.bills30'), d.billsLast30Days],
    [t('platform.lastBill'), d.lastBillAt ? formatDateTime(d.lastBillAt) : '–'],
    [t('platform.khataCheck'), d.lastBalanceCheck ? (d.lastBalanceCheck.mismatches === 0 ? t('platform.checkOk') : t('platform.checkBad', { count: d.lastBalanceCheck.mismatches })) : '–'],
  ]

  return (
    <div className="flex flex-col gap-3 border-t border-line p-4">
      {d.shop.blockedReason && <p className="rounded-xl bg-red-50 p-3 text-sm text-red-800">{t('platform.blockedBecause', { reason: d.shop.blockedReason })}</p>}
      {message && <p className="rounded-xl bg-teal-50 p-3 text-sm font-semibold text-teal-900">{message}</p>}
      <dl className="grid grid-cols-2 gap-2 text-sm">
        {stats.map(([label, value]) => (
          <div key={label} className="rounded-xl bg-ground p-2"><dt className="text-xs text-muted">{label}</dt><dd className="font-bold">{value}</dd></div>
        ))}
      </dl>
      {d.address && <p className="text-sm text-muted">{d.address}</p>}

      <h3 className="font-bold">{t('platform.people')}</h3>
      <ul className="flex flex-col gap-2">
        {d.people.map((p) => (
          <li key={p.membershipId} className="flex flex-wrap items-center gap-2 text-sm">
            <span className="min-w-0 flex-1"><b>{p.name}</b> · {p.role === 'owner' ? t('staff.owner') : t('staff.helper')}{!p.isActive && ` · ${t('staff.off')}`}<br /><span className="text-muted">{formatPhone(p.phone)}</span></span>
            <a href={`tel:${p.phone}`} className="flex min-h-11 items-center rounded-xl border border-line px-3 font-semibold no-underline">{t('platform.call')}</a>
            <a href={`https://wa.me/${p.phone.replace(/\D/g, '')}`} target="_blank" rel="noreferrer" className="flex min-h-11 items-center rounded-xl border border-line px-3 font-semibold no-underline">WhatsApp</a>
            {p.lockedOut && <button disabled={busy} onClick={() => void run(`/v1/platform/users/${p.userId}/unlock`, {}, 'POST', t('platform.unlocked', { name: p.name }))} className="min-h-11 rounded-xl bg-amber-100 px-3 font-semibold text-amber-900">{t('platform.unlock')}</button>}
          </li>
        ))}
      </ul>

      <label className="flex flex-col gap-1 text-sm font-bold">
        {t('platform.note')}
        <textarea value={note ?? d.adminNote ?? ''} onChange={(e) => setNote(e.target.value)} rows={3} className="rounded-xl border border-line p-3 font-normal" placeholder={t('platform.notePlaceholder')} />
      </label>
      {note !== null && note !== (d.adminNote ?? '') && (
        <Button tone="plain" disabled={busy} onClick={() => void run(`/v1/platform/shops/${id}/note`, { note }, 'PUT', t('platform.noteSaved')).then(() => setNote(null))}>{t('platform.saveNote')}</Button>
      )}

      <div className="grid grid-cols-2 gap-2">
        {d.shop.isActive && !d.shop.approvedAt && <Button disabled={busy} onClick={() => void run(`/v1/platform/shops/${id}/approve`)}>{t('platform.approve')}</Button>}
        {d.shop.isActive
          ? <button disabled={busy} onClick={block} className="min-h-12 rounded-2xl border border-line font-bold text-red-700">{t('platform.block')}</button>
          : <Button disabled={busy} onClick={() => void run(`/v1/platform/shops/${id}/unblock`)}>{t('platform.unblock')}</Button>}
      </div>
    </div>
  )
}

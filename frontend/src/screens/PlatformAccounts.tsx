import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { shownLocale } from '../i18n'
import { Button, Field, Sheet } from '../components/ui'
import type { Can } from '../lib/platformAccess'
import { ask, confirmAsk } from '../lib/ask'
import { notify } from '../lib/notify'

interface AccountRole { kind: string; detail: string | null; shopId: string | null; active: boolean }
interface AccountRow { id: string; fullName: string; phone: string; email: string | null; emailVerified: boolean; phoneVerified: boolean; isActive: boolean; locked: boolean; createdAt: string; lastSignInAt: string | null; roles: AccountRole[] }
interface AccountDetail { account: AccountRow; preferredLanguage: string; hasPassword: boolean; activeSessions: number; address: string | null; city: string | null; pincode: string | null }

const ROLE_FILTERS = ['', 'owner', 'staff', 'accountant', 'agent', 'admin', 'customer'] as const
const day = (iso: string) => new Date(iso).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric' })

function RoleChip({ role }: { role: AccountRole }) {
  const { t } = useTranslation()
  const tone = role.kind === 'owner' ? 'bg-brand-soft text-brand-deep' : role.kind === 'admin' ? 'bg-violet-50 text-violet-800' : role.kind === 'agent' ? 'bg-amber-50 text-amber-800' : 'bg-slate-100 text-slate-700'
  return (
    <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${tone} ${role.active ? '' : 'line-through opacity-60'}`}>
      {t(`accountsAdmin.role_${role.kind}`)}{role.detail ? `: ${role.detail}` : ''}
    </span>
  )
}

/** Every login account — shopkeepers, staff, agents, customers, admins — searchable, with edit, suspend, reset and remove. */
export function PlatformAccountsPanel({ can }: { can: Can }) {
  const { t } = useTranslation()
  const [q, setQ] = useState('')
  const [search, setSearch] = useState('')
  const [kind, setKind] = useState<string>('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [open, setOpen] = useState<string | null>(null)
  const query = new URLSearchParams({ q: search, role: kind, status, page: String(page) })
  const data = useQuery({ queryKey: ['platform', 'users', search, kind, status, page], queryFn: () => api<{ items: AccountRow[]; total: number; page: number; pageSize: number }>(`/v1/platform/users?${query}`) }).data
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1
  return (
    <section className="flex flex-col gap-3">
      <header>
        <h2 className="text-xl font-bold">{t('accountsAdmin.title')}</h2>
        <p className="text-sm text-muted">{t('accountsAdmin.hint')}</p>
      </header>
      <form className="flex gap-2" onSubmit={(e) => { e.preventDefault(); setSearch(q.trim()); setPage(1) }}>
        <input value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('accountsAdmin.search')} className="h-11 min-w-0 flex-1 rounded-xl border border-line bg-white px-3" />
        <Button type="submit" className="h-11">{t('accountsAdmin.find')}</Button>
      </form>
      <div className="flex flex-wrap gap-2">
        {ROLE_FILTERS.map((k) => (
          <button key={k} onClick={() => { setKind(k); setPage(1) }} className={`min-h-9 rounded-full px-3 text-sm font-semibold ${kind === k ? 'bg-brand text-white' : 'border border-line bg-white'}`}>
            {k ? t(`accountsAdmin.role_${k}`) : t('accountsAdmin.all')}
          </button>
        ))}
        <select value={status} onChange={(e) => { setStatus(e.target.value); setPage(1) }} className="min-h-9 rounded-full border border-line bg-white px-3 text-sm">
          <option value="">{t('accountsAdmin.anyStatus')}</option>
          <option value="active">{t('accountsAdmin.active')}</option>
          <option value="suspended">{t('accountsAdmin.suspended')}</option>
        </select>
      </div>
      {!data ? <p className="text-muted">…</p> : (
        <>
          <p className="text-sm text-muted">{t('accountsAdmin.count', { count: data.total })}</p>
          <ul className="flex flex-col gap-2">
            {data.items.map((a) => (
              <li key={a.id}>
                <button onClick={() => setOpen(a.id)} className="flex w-full flex-col gap-1 rounded-card bg-white p-3 text-left shadow-sm">
                  <span className="flex items-center justify-between gap-2">
                    <span className="font-bold">{a.fullName}</span>
                    {!a.isActive ? <span className="rounded-full bg-due-soft px-2 py-0.5 text-xs font-semibold text-due">{t('accountsAdmin.suspended')}</span>
                      : a.locked ? <span className="rounded-full bg-warn-soft px-2 py-0.5 text-xs font-semibold text-warn-ink">{t('accountsAdmin.locked')}</span> : null}
                  </span>
                  <span className="text-sm text-muted">{a.phone}{a.email ? ` · ${a.email}${a.emailVerified ? ' ✓' : ''}` : ''}</span>
                  <span className="flex flex-wrap gap-1">{a.roles.map((r, i) => <RoleChip key={i} role={r} />)}</span>
                  <span className="text-xs text-muted">{t('accountsAdmin.joined', { date: day(a.createdAt) })}{a.lastSignInAt ? ` · ${t('accountsAdmin.lastSeen', { date: day(a.lastSignInAt) })}` : ''}</span>
                </button>
              </li>
            ))}
          </ul>
          {pages > 1 && (
            <div className="flex items-center justify-center gap-3">
              <button disabled={page <= 1} onClick={() => setPage(page - 1)} className="min-h-10 rounded-xl border border-line px-3 disabled:opacity-40">‹</button>
              <span className="text-sm">{page} / {pages}</span>
              <button disabled={page >= pages} onClick={() => setPage(page + 1)} className="min-h-10 rounded-xl border border-line px-3 disabled:opacity-40">›</button>
            </div>
          )}
        </>
      )}
      {open && <AccountSheet id={open} can={can} onClose={() => setOpen(null)} />}
    </section>
  )
}

function AccountSheet({ id, can, onClose }: { id: string; can: Can; onClose: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const detail = useQuery({ queryKey: ['platform', 'user', id], queryFn: () => api<AccountDetail>(`/v1/platform/users/${id}`) }).data
  const [edit, setEdit] = useState<{ fullName: string; email: string; preferredLanguage: string } | null>(null)
  const [note, setNote] = useState('')
  const canChange = can('support')
  const canRemove = can('accounts.remove')

  async function act(path: string, method: 'POST' | 'PUT' | 'DELETE', body: unknown, done: string, confirmText?: string) {
    if (confirmText && !(await confirmAsk(confirmText))) return
    setNote('')
    try {
      await api(`/v1/platform/users/${id}${path}`, method, body)
      await queryClient.invalidateQueries({ queryKey: ['platform', 'user', id] })
      await queryClient.invalidateQueries({ queryKey: ['platform', 'users'] })
      setNote('')
      notify.success(done)
      setEdit(null)
    } catch (e) {
      notify.error(e)
    }
  }

  const a = detail?.account
  return (
    <Sheet title={a?.fullName ?? '…'} onClose={onClose}>
      {!detail || !a ? <p className="text-muted">…</p> : (
        <div className="flex flex-col gap-3">
          <dl className="grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-sm">
            <dt className="text-muted">{t('accountsAdmin.phone')}</dt><dd>{a.phone} {a.phoneVerified ? '✓' : ''}</dd>
            <dt className="text-muted">{t('accountsAdmin.email')}</dt><dd>{a.email ?? '—'} {a.email ? (a.emailVerified ? `✓ ${t('accountsAdmin.verified')}` : t('accountsAdmin.notVerified')) : ''}</dd>
            <dt className="text-muted">{t('accountsAdmin.status')}</dt><dd>{a.isActive ? t('accountsAdmin.active') : t('accountsAdmin.suspended')}{a.locked ? ` · ${t('accountsAdmin.locked')}` : ''}</dd>
            <dt className="text-muted">{t('accountsAdmin.password')}</dt><dd>{detail.hasPassword ? t('accountsAdmin.passwordSet') : t('accountsAdmin.passwordNone')}</dd>
            <dt className="text-muted">{t('accountsAdmin.sessions')}</dt><dd>{detail.activeSessions}</dd>
            <dt className="text-muted">{t('accountsAdmin.joinedOn')}</dt><dd>{day(a.createdAt)}{a.lastSignInAt ? ` · ${t('accountsAdmin.lastSeen', { date: day(a.lastSignInAt) })}` : ''}</dd>
            {(detail.address || detail.city) && <><dt className="text-muted">{t('accountsAdmin.address')}</dt><dd>{[detail.address, detail.city, detail.pincode].filter(Boolean).join(', ')}</dd></>}
          </dl>
          <div>
            <p className="mb-1 text-sm font-bold">{t('accountsAdmin.roles')}</p>
            <div className="flex flex-wrap gap-1">{a.roles.map((r, i) => <RoleChip key={i} role={r} />)}</div>
          </div>
          <p className="rounded-lg bg-brand-soft p-2 text-xs text-brand-deep">{t('accountsAdmin.passwordNote')}</p>

          {edit ? (
            <div className="flex flex-col gap-2 rounded-xl border border-line p-3">
              <Field label={t('accountsAdmin.name')} value={edit.fullName} onChange={(e) => setEdit({ ...edit, fullName: e.target.value })} />
              <Field label={t('accountsAdmin.email')} value={edit.email} onChange={(e) => setEdit({ ...edit, email: e.target.value })} inputMode="email" />
              <label className="flex flex-col gap-1 text-sm text-muted">{t('accountsAdmin.language')}
                <select value={edit.preferredLanguage} onChange={(e) => setEdit({ ...edit, preferredLanguage: e.target.value })} className="h-11 rounded-xl border border-line bg-white px-3 text-ink">
                  <option value="hi">हिंदी</option><option value="en">English</option><option value="gu">ગુજરાતી</option><option value="hn">Hinglish</option>
                </select>
              </label>
              <div className="flex gap-2">
                <Button onClick={() => void act('', 'PUT', edit, t('saved'))}>{t('save')}</Button>
                <Button tone="plain" onClick={() => setEdit(null)}>{t('cancel')}</Button>
              </div>
            </div>
          ) : canChange && (
            <div className="grid grid-cols-2 gap-2">
              <Button tone="plain" onClick={() => setEdit({ fullName: a.fullName, email: a.email ?? '', preferredLanguage: detail.preferredLanguage })}>{t('accountsAdmin.edit')}</Button>
              <Button tone="plain" onClick={() => void act('/send-reset', 'POST', {}, t('accountsAdmin.resetSent'))}>{t('accountsAdmin.sendReset')}</Button>
              <Button tone="plain" onClick={() => void act('/sign-out', 'POST', {}, t('accountsAdmin.signedOut'))}>{t('accountsAdmin.signOut')}</Button>
              {a.isActive
                ? <Button tone="plain" onClick={async () => { const reason = await ask(t('accountsAdmin.suspendWhy')); if (reason !== null) void act('/suspend', 'POST', { reason }, t('accountsAdmin.suspendedDone')) }}>{t('accountsAdmin.suspend')}</Button>
                : <Button tone="plain" onClick={() => void act('/activate', 'POST', {}, t('accountsAdmin.activatedDone'))}>{t('accountsAdmin.activate')}</Button>}
              {canRemove && <button onClick={() => void act('', 'DELETE', undefined, t('accountsAdmin.removedDone'), t('accountsAdmin.removeConfirm', { name: a.fullName }))}
                className="col-span-2 min-h-11 rounded-xl border border-due px-3 font-bold text-due">{t('accountsAdmin.remove')}</button>}
            </div>
          )}
          {note && <p className="text-sm text-muted">{note}</p>}
        </div>
      )}
    </Sheet>
  )
}

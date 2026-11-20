import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BackIcon } from '../components/Icons'
import { Button, btn } from '../components/ui'
import { addText, shownLocale } from '../i18n'
import { teamText } from '../i18n/team'
import { api, ApiError, request } from '../lib/api'
import { shrinkPhoto } from '../lib/images'
import { formatRupees, whatsAppLink } from '../lib/format'
import { navigate } from '../router'
import type { ExpenseCategory } from './ExpensesScreen'
import { shiftMonth, thisMonth } from './KharchaMonth'
import { teamViewLink } from './TeamViewPage'
import { ask, confirmAsk } from '../components/Ask'

addText(teamText)

type Role = 'owner' | 'admin' | 'member' | 'viewer'
interface Member { id: string; name: string; role: Role; hasLogin: boolean; isMe: boolean; monthlyLimit: number | null; spentThisMonth: number }
interface Team { id: string; name: string; myRole: Role; myMemberId: string; members: Member[]; inviteCode: string | null; inviteExpiresAt: string | null; maxMembers: number; viewLinkOn: boolean; viewToken: string | null; approvalAbove: number | null; pending: number }
interface TeamSummary { id: string; name: string; myRole: Role; members: number; thisMonth: number }
interface Line { id: string; occurredAt: string; amount: number; category: ExpenseCategory; note: string | null; mode: 'cash' | 'upi'; memberId: string; memberName: string; writtenBy: string | null; struckAt: string | null; struckReason: string | null; canStrike: boolean; pending: boolean; canApprove: boolean; hasPhoto: boolean; approvedBy: string | null; fromAdvance: boolean }
interface AdvanceBalance { memberId: string; name: string; given: number; spent: number; returned: number; holds: number }
interface AdvanceEntry { id: string; at: string; memberId: string; memberName: string; kind: 'given' | 'returned'; amount: number; note: string | null; by: string | null; struck: boolean }
interface Advances { balances: AdvanceBalance[]; entries: AdvanceEntry[] }
interface NameTotal { memberId: string; name: string; total: number; count: number }
interface Day { date: string; total: number; byName: NameTotal[]; lines: Line[]; pendingTotal: number }
interface Month { month: string; complete: boolean; total: number; cash: number; upi: number; count: number; byName: NameTotal[]; byCategory: { category: ExpenseCategory; total: number; count: number }[]; byDay: { date: string; total: number }[]; lines: Line[] }

const indiaToday = () => new Date(Date.now() + 5.5 * 3600_000).toISOString().slice(0, 10)
const shiftDay = (day: string, by: number) => new Date(Date.parse(`${day}T00:00:00Z`) + by * 86_400_000).toISOString().slice(0, 10)
const monthLabel = (month: string) => new Date(`${month}-01T00:00:00`).toLocaleDateString(shownLocale(), { month: 'long', year: 'numeric' })
export const teamJoinLink = (code: string) => `${location.origin}/#/team/join/${code}`

function useRun() {
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  async function run(action: () => Promise<unknown>, done?: string) {
    setBusy(true)
    setMessage(null)
    try {
      await action()
      if (done) setMessage(done)
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }
  return { busy, message, run }
}

function Header({ title, back }: { title: string; back: string }) {
  const { t } = useTranslation()
  return (
    <header className="sticky top-0 z-10 flex items-center gap-2 bg-brand-strong px-4 py-3 text-white">
      <button onClick={() => navigate(back)} className="-ml-2 flex h-11 w-11 items-center justify-center" aria-label={t('a11y.back')}><BackIcon /></button>
      <h1 className="truncate text-xl font-bold">{title}</h1>
    </header>
  )
}

/** The consent people give before their lines are shared with a team (kept with its date and wording version). */
function Consent({ checked, onChange }: { checked: boolean; onChange: (v: boolean) => void }) {
  const { t } = useTranslation()
  return (
    <label className="flex items-start gap-3 rounded-xl bg-ground p-3 text-sm">
      <input type="checkbox" checked={checked} onChange={(e) => onChange(e.target.checked)} className="mt-0.5 h-5 w-5 shrink-0" />
      <span>{t('team.consent')}</span>
    </label>
  )
}

/** Team kharcha (/team…): your teams, making or joining one, and a team's book. Any account, any plan. */
export function TeamScreen({ path, userName }: { path: string; userName: string }) {
  const join = path.match(/^\/team\/join\/([A-Za-z0-9]+)$/)
  const one = path.match(/^\/team\/([0-9a-f-]{36})$/)
  if (join) return <JoinTeam code={join[1].toUpperCase()} userName={userName} />
  if (one) return <TeamBook id={one[1]} />
  return <MyTeams userName={userName} />
}

function MyTeams({ userName }: { userName: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const teams = useQuery({ queryKey: ['teams'], queryFn: () => api<TeamSummary[]>('/v1/teams') }).data ?? []
  const [name, setName] = useState('')
  const [yourName, setYourName] = useState(userName.split(' ')[0])
  const [consent, setConsent] = useState(false)
  const [code, setCode] = useState('')
  const { busy, message, run } = useRun()
  const box = 'h-12 rounded-xl border border-line bg-white px-3 text-lg'

  return (
    <div className="mx-auto min-h-dvh max-w-md pb-10">
      <Header title={t('team.title')} back="/" />
      <div className="flex flex-col gap-3 p-4">
        <p className="text-sm text-muted">{t('team.intro')}</p>
        {teams.map((x) => (
          <button key={x.id} onClick={() => navigate(`/team/${x.id}`)} className="flex items-center justify-between rounded-card bg-white p-4 text-left shadow-sm">
            <span className="flex flex-col"><b className="text-lg">{x.name}</b><span className="text-sm text-muted">{t('team.people', { count: x.members })} · {t(`team.role.${x.myRole}`)}</span></span>
            <span className="text-right"><span className="block text-xs text-muted">{t('team.thisMonth')}</span><b>{formatRupees(x.thisMonth)}</b></span>
          </button>
        ))}

        <section className="flex flex-col gap-3 rounded-[20px] bg-white p-4">
          <h2 className="font-bold">{t('team.join')}</h2>
          <input value={code} onChange={(e) => setCode(e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 8))} placeholder="AB3K9Q2X" aria-label={t('team.code')} className={`${box} font-mono tracking-widest`} />
          <Button disabled={code.length !== 8} onClick={() => navigate(`/team/join/${code}`)}>{t('team.next')}</Button>
        </section>

        <section className="flex flex-col gap-3 rounded-[20px] bg-white p-4">
          <h2 className="font-bold">{t('team.create')}</h2>
          <label className="flex flex-col gap-1"><span className="text-sm text-muted">{t('team.name')}</span>
            <input value={name} onChange={(e) => setName(e.target.value)} placeholder={t('team.namePlaceholder')} className={box} /></label>
          <label className="flex flex-col gap-1"><span className="text-sm text-muted">{t('team.yourName')}</span>
            <input value={yourName} onChange={(e) => setYourName(e.target.value)} className={box} /></label>
          <Consent checked={consent} onChange={setConsent} />
          <Button disabled={busy || name.trim().length < 2 || !consent} onClick={() => void run(async () => {
            const team = await api<Team>('/v1/teams', 'POST', { name: name.trim(), yourName: yourName.trim(), consent })
            await queryClient.invalidateQueries({ queryKey: ['teams'] })
            navigate(`/team/${team.id}`)
          })}>{t('team.createButton')}</Button>
        </section>
        {message && <p className="rounded-xl bg-due-soft p-3 text-sm font-semibold text-red-800">{message}</p>}
      </div>
    </div>
  )
}

/** Opened from an invite link or a typed code: who the team is, then agree and join. */
function JoinTeam({ code, userName }: { code: string; userName: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const preview = useQuery({ queryKey: ['team-preview', code], queryFn: () => api<{ name: string; members: number; createdBy: string; alreadyIn: boolean }>(`/v1/teams/preview?code=${code}`), retry: false })
  const [yourName, setYourName] = useState(userName.split(' ')[0])
  const [consent, setConsent] = useState(false)
  const { busy, message, run } = useRun()
  const p = preview.data
  return (
    <div className="mx-auto min-h-dvh max-w-md pb-10">
      <Header title={t('team.join')} back="/team" />
      <div className="flex flex-col gap-3 p-4">
        {preview.isError && <p className="rounded-xl bg-due-soft p-3 font-semibold text-red-800">{preview.error.message}</p>}
        {p && (
          <section className="flex flex-col gap-3 rounded-[20px] bg-white p-4">
            <span className="text-2xl font-extrabold">{p.name}</span>
            <span className="text-sm text-muted">{t('team.madeBy', { name: p.createdBy, count: p.members })}</span>
            <label className="flex flex-col gap-1"><span className="text-sm text-muted">{t('team.yourName')}</span>
              <input value={yourName} onChange={(e) => setYourName(e.target.value)} className="h-12 rounded-xl border border-line px-3 text-lg bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /></label>
            <Consent checked={consent} onChange={setConsent} />
            <Button disabled={busy || !consent} onClick={() => void run(async () => {
              const team = await api<Team>('/v1/teams/join', 'POST', { code, yourName: yourName.trim(), consent })
              await queryClient.invalidateQueries({ queryKey: ['teams'] })
              navigate(`/team/${team.id}`)
            })}>{t('team.joinButton', { name: p.name })}</Button>
          </section>
        )}
        {message && <p className="rounded-xl bg-due-soft p-3 text-sm font-semibold text-red-800">{message}</p>}
      </div>
    </div>
  )
}

/** One team's book: write a line, today by name, the month by name and heading, and the people. */
function TeamBook({ id }: { id: string }) {
  const { t } = useTranslation()
  const team = useQuery({ queryKey: ['team', id], queryFn: () => api<Team>(`/v1/teams/${id}`) })
  const [tab, setTab] = useState<'day' | 'month' | 'advance' | 'people'>('day')
  const x = team.data
  if (team.isError) {
    // 404: not in this team (or it doesn't exist). Anything else (no signal, server restarting) can be tried again.
    const notIn = team.error instanceof ApiError && team.error.status === 404
    return (
      <div className="mx-auto max-w-md">
        <Header title={t('team.title')} back="/team" />
        <p className="p-6 text-center text-muted">{notIn ? t('team.notIn') : team.error.message}</p>
        {!notIn && <div className="px-4"><Button onClick={() => void team.refetch()}>{t('team.retry')}</Button></div>}
      </div>
    )
  }
  if (!x) return <p className="p-6 text-center">…</p>
  return (
    <div className="mx-auto min-h-dvh max-w-md pb-10">
      <Header title={x.name} back="/team" />
      <div className="flex flex-col gap-3 p-4">
        <div role="tablist" className="grid grid-cols-4 gap-1 rounded-2xl bg-white p-1 text-sm font-semibold">
          {(['day', 'month', 'advance', 'people'] as const).map((v) => (
            <button key={v} role="tab" aria-selected={tab === v} onClick={() => setTab(v)} className={`min-h-11 rounded-xl ${tab === v ? 'bg-brand text-white' : ''}`}>{t(`team.tab.${v}`)}</button>
          ))}
        </div>
        {x.pending > 0 && <p className="rounded-xl bg-amber-100 p-3 text-sm font-semibold text-warn-ink">⏳ {t('team.pendingBanner', { count: x.pending })}</p>}
        {x.viewLinkOn && <p className="rounded-xl bg-warn-soft p-3 text-sm text-warn-ink">🔗 {t('team.viewOnNotice')}</p>}
        {tab === 'day' && <TeamDay team={x} />}
        {tab === 'month' && <TeamMonth team={x} />}
        {tab === 'advance' && <TeamAdvances team={x} />}
        {tab === 'people' && <TeamPeople team={x} />}
      </div>
    </div>
  )
}

function NameBars({ rows }: { rows: NameTotal[] }) {
  const { t } = useTranslation()
  const top = Math.max(1, ...rows.map((r) => r.total))
  return (
    <div className="flex flex-col gap-2">
      {rows.map((r) => (
        <div key={r.memberId} className="flex flex-col gap-1">
          <span className="flex justify-between text-sm"><span className="font-semibold">{r.name === 'Former member' ? t('team.former') : r.name} <span className="font-normal text-muted">×{r.count}</span></span><b>{formatRupees(r.total)}</b></span>
          <span className="h-2 rounded-full bg-ground"><span className="block h-2 rounded-full bg-teal-600" style={{ width: `${(r.total / top) * 100}%` }} /></span>
        </div>
      ))}
    </div>
  )
}

function TeamDay({ team }: { team: Team }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [day, setDay] = useState(indiaToday())
  const page = useQuery({ queryKey: ['team', team.id, 'day', day], queryFn: () => api<Day>(`/v1/teams/${team.id}/day?date=${day}`) }).data
  const manages = team.myRole === 'owner' || team.myRole === 'admin'
  const [form, setForm] = useState({ amount: '', note: '', mode: 'cash' as 'cash' | 'upi', memberId: team.myMemberId, photo: '', fromAdvance: null as boolean | null })
  const advances = useQuery({ queryKey: ['team', team.id, 'advances'], queryFn: () => api<Advances>(`/v1/teams/${team.id}/advances`) }).data
  const holds = advances?.balances.find((b) => b.memberId === form.memberId)?.holds ?? 0
  const fromAdvance = form.fromAdvance ?? holds > 0
  const { busy, message, run } = useRun()
  const isToday = day === indiaToday()

  const add = () => run(async () => {
    await api(`/v1/teams/${team.id}/expenses`, 'POST', {
      id: crypto.randomUUID(), amount: Number(form.amount), note: form.note.trim() || null, mode: form.mode,
      memberId: form.memberId, occurredAt: isToday ? null : `${day}T12:00:00+05:30`, photoDataUrl: form.photo || null, fromAdvance,
    })
    setForm({ ...form, amount: '', note: '', photo: '', fromAdvance: null })
    await queryClient.invalidateQueries({ queryKey: ['team', team.id] })
    await queryClient.invalidateQueries({ queryKey: ['teams'] })
  })

  return (
    <>
      <div className="flex items-center justify-between rounded-card bg-white p-2 shadow-sm">
        <button onClick={() => setDay(shiftDay(day, -1))} aria-label={t('book.previous')} className="h-11 w-11 rounded-xl text-xl">‹</button>
        <span className="text-lg font-bold">{isToday ? t('team.today') : new Date(`${day}T00:00:00`).toLocaleDateString(shownLocale(), { weekday: 'short', day: 'numeric', month: 'short' })}</span>
        <button onClick={() => setDay(shiftDay(day, 1))} disabled={isToday} aria-label={t('book.next')} className="h-11 w-11 rounded-xl text-xl disabled:opacity-30">›</button>
      </div>

      {team.myRole !== 'viewer' && (
        <section className="flex flex-col gap-2 rounded-[20px] bg-white p-4">
          <div className="grid grid-cols-[1fr_2fr] gap-2">
            <input value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value.replace(/[^\d.]/g, '') })} inputMode="decimal" placeholder="₹" aria-label={t('team.amount')} className="h-12 min-w-0 rounded-xl border border-line px-3 text-lg font-bold" />
            <input value={form.note} onChange={(e) => setForm({ ...form, note: e.target.value })} placeholder={t('team.notePlaceholder')} aria-label={t('team.note')} className="h-12 min-w-0 rounded-xl border border-line px-3 text-lg bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
          </div>
          <div className="grid grid-cols-2 gap-2">
            {(['cash', 'upi'] as const).map((m) => (
              <button key={m} onClick={() => setForm({ ...form, mode: m })} aria-pressed={form.mode === m}
                className={`min-h-11 rounded-xl font-semibold ${form.mode === m ? 'bg-brand text-white' : 'border border-line'}`}>{m === 'cash' ? t('team.cash') : 'UPI'}</button>
            ))}
          </div>
          <label className={`flex min-h-11 cursor-pointer items-center justify-center gap-2 rounded-xl border text-sm font-semibold ${form.photo ? 'border-brand bg-brand-soft text-brand-deep' : 'border-dashed border-line'}`}>
            📷 {form.photo ? t('team.photoAdded') : t('team.addPhoto')}
            <input type="file" accept="image/*" capture="environment" className="sr-only"
              onChange={(e) => { const file = e.target.files?.[0]; if (file) void shrinkPhoto(file, 1024, 140_000, false).then((url) => setForm((f) => ({ ...f, photo: url }))) }} />
          </label>
          {(holds > 0 || form.fromAdvance) && (
            <label className="flex min-h-11 items-center gap-2 rounded-xl bg-ground px-3 text-sm">
              <input type="checkbox" checked={fromAdvance} onChange={(e) => setForm({ ...form, fromAdvance: e.target.checked })} className="h-5 w-5" />
              {t('team.fromAdvance', { amount: formatRupees(holds) })}
            </label>
          )}
          {team.approvalAbove && !manages && <p className="text-xs text-muted">{t('team.approvalNote', { amount: formatRupees(team.approvalAbove) })}</p>}
          {manages && (
            <label className="flex items-center gap-2 text-sm"><span className="text-muted">{t('team.spentBy')}</span>
              <select value={form.memberId} onChange={(e) => setForm({ ...form, memberId: e.target.value })} className="h-11 flex-1 rounded-xl border border-line bg-white px-2 outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10">
                {team.members.filter((m) => m.role !== 'viewer').map((m) => <option key={m.id} value={m.id}>{m.isMe ? t('team.me', { name: m.name }) : m.name}</option>)}
              </select>
            </label>
          )}
          <Button disabled={busy || !(Number(form.amount) > 0)} onClick={() => void add()}>{t('team.add')}</Button>
          {message && <p className="text-sm font-semibold text-red-800">{message}</p>}
        </section>
      )}

      {page && (
        <section className="flex flex-col gap-3 rounded-[20px] bg-white p-4">
          <div className="flex items-baseline justify-between"><h2 className="font-bold">{t('team.byName')}</h2><span className="text-2xl font-extrabold">{formatRupees(page.total)}</span></div>
          {page.pendingTotal > 0 && <p className="text-sm font-semibold text-warn">⏳ {t('team.pendingTotal', { amount: formatRupees(page.pendingTotal) })}</p>}
          {page.byName.length === 0 ? <p className="text-sm text-muted">{t('team.nothing')}</p> : <NameBars rows={page.byName} />}
          {page.byName.length > 0 && (
            <a href={whatsAppLink(null, [
              t('team.waToday', { team: team.name, date: new Date(`${day}T00:00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' }) }),
              t('team.waTotal', { amount: formatRupees(page.total) }),
              ...page.byName.map((n) => `• ${n.name === 'Former member' ? t('team.former') : n.name}: ${formatRupees(n.total)}`),
              team.viewToken ? `${t('team.waSee')} ${teamViewLink(team.id, team.viewToken)}` : '',
            ].filter(Boolean).join('\n'))} target="_blank" rel="noreferrer"
              className="flex min-h-11 items-center justify-center rounded-xl bg-[#15803d] text-sm font-bold text-white">{t('team.shareToday')}</a>
          )}
          <ul className="divide-y divide-[#edf2f1] text-sm">
            {page.lines.map((l) => (
              <li key={l.id} className={`flex items-center justify-between gap-2 py-2 ${l.struckAt ? 'text-muted line-through' : ''} ${l.pending ? 'rounded-lg bg-warn-soft px-2' : ''}`}>
                <span className="min-w-0">{l.fromAdvance && <span title={t('team.fromAdvanceShort')}>💼 </span>}<b>{l.memberName === 'Former member' ? t('team.former') : l.memberName}</b> · {l.note ?? t(`expense.cat.${l.category}`)} · {l.mode === 'upi' ? 'UPI' : t('team.cash')}
                  {l.writtenBy && <span className="block text-xs text-muted">{t('team.writtenBy', { name: l.writtenBy })}{l.approvedBy ? ` · ${t('team.okBy', { name: l.approvedBy })}` : ''}{l.struckReason ? ` · ${l.struckReason}` : ''}</span>}
                  {l.pending && <span className="block text-xs font-semibold text-warn">⏳ {t('team.waiting')}</span>}</span>
                <span className="flex items-center gap-2"><b>{formatRupees(l.amount)}</b>
                  {l.hasPhoto && <button onClick={() => void openPhoto(team.id, l.id)} aria-label={t('team.seePhoto')} className="h-9 w-9 rounded-lg border border-line">📷</button>}
                  {l.canApprove && <button disabled={busy} onClick={() => void run(async () => { await api(`/v1/teams/${team.id}/expenses/${l.id}/approve`, 'POST', {}); await queryClient.invalidateQueries({ queryKey: ['team', team.id] }) })}
                    className={`h-9 px-2 text-xs rounded-xl ${btn.solid}`}>{t('team.ok')}</button>}
                  {l.canStrike && <button onClick={async () => { const reason = (await ask(t('team.strikeAsk'))); if (reason !== null) void run(async () => { await api(`/v1/teams/${team.id}/expenses/${l.id}/strike`, 'POST', { reason }); await queryClient.invalidateQueries({ queryKey: ['team', team.id] }) }) }}
                    aria-label={t('team.strike')} className={`h-9 w-9 ${btn.dangerOutline}`}>✕</button>}</span>
              </li>
            ))}
          </ul>
        </section>
      )}
    </>
  )
}

function TeamMonth({ team }: { team: Team }) {
  const { t } = useTranslation()
  const [month, setMonth] = useState(thisMonth())
  const m = useQuery({ queryKey: ['team', team.id, 'month', month], queryFn: () => api<Month>(`/v1/teams/${team.id}/month?month=${month}`) }).data
  const share = m && [
    t('team.waTitle', { team: team.name, month: monthLabel(m.month) }),
    t('team.waTotal', { amount: formatRupees(m.total) }),
    ...m.byName.map((n) => `• ${n.name}: ${formatRupees(n.total)}`),
    t('team.waModes', { cash: formatRupees(m.cash), upi: formatRupees(m.upi) }),
    team.viewToken ? `${t('team.waSee')} ${teamViewLink(team.id, team.viewToken)}` : '',
  ].filter(Boolean).join('\n')
  return (
    <>
      <div className="flex items-center justify-between rounded-card bg-white p-2 shadow-sm">
        <button onClick={() => setMonth(shiftMonth(month, -1))} aria-label={t('book.previous')} className="h-11 w-11 rounded-xl text-xl">‹</button>
        <span className="text-lg font-bold">{monthLabel(month)}</span>
        <button onClick={() => setMonth(shiftMonth(month, 1))} disabled={month >= thisMonth()} aria-label={t('book.next')} className="h-11 w-11 rounded-xl text-xl disabled:opacity-30">›</button>
      </div>
      {m && <>
        <section className="flex flex-col gap-1 rounded-[20px] bg-white p-4">
          <span className="text-sm font-semibold text-muted">{m.complete ? t('team.monthClosed') : t('team.monthSoFar')}</span>
          <span className="text-3xl font-extrabold">{formatRupees(m.total)}</span>
          <span className="text-sm">{t('team.modes', { cash: formatRupees(m.cash), upi: formatRupees(m.upi), count: m.count })}</span>
        </section>
        {m.count > 0 && <>
          <section className="flex flex-col gap-3 rounded-[20px] bg-white p-4"><h2 className="font-bold">{t('team.byName')}</h2><NameBars rows={m.byName} /></section>
          <section className="flex flex-col gap-1 rounded-[20px] bg-white p-4">
            <h2 className="font-bold">{t('team.byHeading')}</h2>
            {m.byCategory.map((c) => <span key={c.category} className="flex justify-between text-sm"><span>{t(`expense.cat.${c.category}`)} ×{c.count}</span><b>{formatRupees(c.total)}</b></span>)}
          </section>
          <a href={whatsAppLink(null, share!)} target="_blank" rel="noreferrer" className="flex min-h-12 items-center justify-center rounded-xl bg-[#15803d] font-bold text-white">{t('team.share')}</a>
        </>}
        {m.count === 0 && <p className="rounded-[20px] bg-white p-6 text-center text-muted">{t('team.nothingMonth')}</p>}
      </>}
    </>
  )
}

function TeamPeople({ team }: { team: Team }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const manages = team.myRole === 'owner' || team.myRole === 'admin'
  const [named, setNamed] = useState('')
  const { busy, message, run } = useRun()
  const refresh = (next: Team) => queryClient.setQueryData(['team', team.id], next)
  const full = team.members.length >= team.maxMembers
  const invite = team.inviteCode && team.inviteExpiresAt && Date.parse(team.inviteExpiresAt) > Date.now() ? team.inviteCode : null
  const inviteText = invite ? t('team.inviteText', { team: team.name, code: invite, link: teamJoinLink(invite) }) : ''

  return (
    <>
      <ul className="flex flex-col divide-y divide-[#edf2f1] rounded-[20px] bg-white px-4">
        {team.members.map((m) => (
          <li key={m.id} className="flex flex-wrap items-center gap-2 py-3">
            <span className="min-w-0 flex-1"><b>{m.name}</b>{m.isMe && ` (${t('team.you')})`}<br />
              <span className="text-xs text-muted">{t(`team.role.${m.role}`)}{m.hasLogin ? '' : ` · ${t('team.noApp')}`}</span></span>
            {team.myRole === 'owner' && !m.isMe && m.role !== 'owner' && m.hasLogin && (
              <select aria-label={t('team.roleLabel')} value={m.role} disabled={busy}
                onChange={(e) => void run(async () => refresh(await api<Team>(`/v1/teams/${team.id}/members/${m.id}/role`, 'PUT', { role: e.target.value })))}
                className="h-11 rounded-xl border border-line bg-white px-2 text-sm outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10">
                {(['admin', 'member', 'viewer'] as const).map((r) => <option key={r} value={r}>{t(`team.role.${r}`)}</option>)}
              </select>
            )}
            {manages && !m.isMe && m.role !== 'owner' && (
              <button disabled={busy} onClick={async () => (await confirmAsk(t('team.removeAsk', { name: m.name }))) && void run(async () => refresh(await api<Team>(`/v1/teams/${team.id}/members/${m.id}/remove`, 'POST', {})))}
                className="min-h-11 rounded-xl border border-line px-3 text-sm font-semibold text-due transition hover:bg-[#f6faf9]">{t('team.remove')}</button>
            )}
          </li>
        ))}
      </ul>
      <p className="text-center text-xs text-muted">{t('team.limit', { count: team.members.length, max: team.maxMembers })}</p>

      {team.myRole === 'owner' && (
        <section className="flex flex-col gap-3 rounded-[20px] bg-white p-4">
          <h2 className="font-bold">{t('team.rulesTitle')}</h2>
          <label className="flex items-center gap-2 text-sm"><span className="flex-1">{t('team.approvalAbove')}</span>
            <input defaultValue={team.approvalAbove ?? ''} inputMode="numeric" placeholder={t('team.none')} aria-label={t('team.approvalAbove')}
              onBlur={(e) => { const v = e.target.value.replace(/\D/g, ''); if (String(team.approvalAbove ?? '') !== v) void run(async () => refresh(await api<Team>(`/v1/teams/${team.id}/rules`, 'PUT', { approvalAbove: v ? Number(v) : null }))) }}
              className="h-11 w-28 rounded-xl border border-line px-2 text-right bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /></label>
          {team.members.filter((m) => m.role !== 'owner' && m.role !== 'viewer').map((m) => (
            <label key={m.id} className="flex flex-col gap-1 text-sm">
              <span className="flex items-center gap-2"><span className="flex-1">{t('team.limitFor', { name: m.name })}</span>
                <input defaultValue={m.monthlyLimit ?? ''} inputMode="numeric" placeholder={t('team.none')} aria-label={t('team.limitFor', { name: m.name })}
                  onBlur={(e) => { const v = e.target.value.replace(/\D/g, ''); if (String(m.monthlyLimit ?? '') !== v) void run(async () => refresh(await api<Team>(`/v1/teams/${team.id}/members/${m.id}/limit`, 'PUT', { monthlyLimit: v ? Number(v) : null }))) }}
                  className="h-11 w-28 rounded-xl border border-line px-2 text-right bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" /></span>
              {m.monthlyLimit && (
                <span className="flex items-center gap-2 text-xs text-muted">
                  <span className="h-2 flex-1 rounded-full bg-ground"><span className={`block h-2 rounded-full ${m.spentThisMonth > m.monthlyLimit ? 'bg-red-500' : 'bg-teal-600'}`} style={{ width: `${Math.min(100, (m.spentThisMonth / m.monthlyLimit) * 100)}%` }} /></span>
                  {formatRupees(m.spentThisMonth)} / {formatRupees(m.monthlyLimit)}
                </span>
              )}
            </label>
          ))}
          <p className="text-xs text-muted">{t('team.rulesHint')}</p>
        </section>
      )}

      {manages && !full && (
        <section className="flex flex-col gap-3 rounded-[20px] bg-white p-4">
          <h2 className="font-bold">{t('team.inviteTitle')}</h2>
          {invite ? <>
            <span className="text-center font-mono text-3xl font-extrabold tracking-widest">{invite}</span>
            <span className="text-center text-xs text-muted">{t('team.inviteValid', { time: new Date(team.inviteExpiresAt!).toLocaleString(shownLocale(), { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' }) })}</span>
            <a href={whatsAppLink(null, inviteText)} target="_blank" rel="noreferrer" className="flex min-h-12 items-center justify-center rounded-xl bg-[#15803d] font-bold text-white">{t('team.inviteWa')}</a>
          </> : (
            <Button disabled={busy} onClick={() => void run(async () => refresh(await api<Team>(`/v1/teams/${team.id}/invite`, 'POST', {})))}>{t('team.makeCode')}</Button>
          )}
          <h2 className="mt-2 font-bold">{t('team.namedTitle')}</h2>
          <p className="text-xs text-muted">{t('team.namedHint')}</p>
          <div className="flex gap-2">
            <input value={named} onChange={(e) => setNamed(e.target.value)} placeholder={t('team.namedPlaceholder')} aria-label={t('team.namedTitle')} className="h-12 min-w-0 flex-1 rounded-xl border border-line px-3 bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
            <button disabled={busy || named.trim().length < 2} onClick={() => void run(async () => { refresh(await api<Team>(`/v1/teams/${team.id}/members`, 'POST', { name: named.trim() })); setNamed('') })}
              className="h-12 rounded-xl bg-brand px-4 font-bold text-white disabled:opacity-40">{t('team.addNamed')}</button>
          </div>
        </section>
      )}
      {manages && full && <p className="rounded-xl bg-warn-soft p-3 text-sm text-warn-ink">{t('team.full', { max: team.maxMembers })}</p>}

      {team.myRole === 'owner' && (
        <section className="flex flex-col gap-2 rounded-[20px] bg-white p-4">
          <h2 className="font-bold">{t('team.viewTitle')}</h2>
          <p className="text-xs text-muted">{t('team.viewHint')}</p>
          {team.viewToken ? <>
            <a href={whatsAppLink(null, t('team.viewShare', { team: team.name, link: teamViewLink(team.id, team.viewToken) }))} target="_blank" rel="noreferrer"
              className="flex min-h-12 items-center justify-center rounded-xl bg-[#15803d] font-bold text-white">{t('team.viewSend')}</a>
            <button disabled={busy} onClick={() => void run(async () => refresh(await api<Team>(`/v1/teams/${team.id}/view-link?on=false`, 'POST', {})))}
              className="min-h-11 rounded-xl border border-line font-semibold text-due transition hover:bg-[#f6faf9]">{t('team.viewOff')}</button>
          </> : (
            <Button disabled={busy} onClick={() => void run(async () => refresh(await api<Team>(`/v1/teams/${team.id}/view-link?on=true`, 'POST', {})))}>{t('team.viewOn')}</Button>
          )}
        </section>
      )}

      {team.myRole !== 'owner' && (
        <section className="flex flex-col gap-2 rounded-[20px] bg-white p-4">
          <h2 className="font-bold">{t('team.leaveTitle')}</h2>
          <p className="text-xs text-muted">{t('team.leaveHint')}</p>
          <div className="grid grid-cols-2 gap-2">
            {[false, true].map((del) => (
              <button key={String(del)} disabled={busy} className={`min-h-11 text-sm transition hover:bg-[#f6faf9] ${btn.dangerOutline}`}
                onClick={async () => (await confirmAsk(t(del ? 'team.leaveDeleteAsk' : 'team.leaveKeepAsk'))) && void run(async () => {
                  await api(`/v1/teams/${team.id}/leave`, 'POST', { deleteMyLines: del })
                  await queryClient.invalidateQueries({ queryKey: ['teams'] })
                  navigate('/team')
                })}>{t(del ? 'team.leaveDelete' : 'team.leaveKeep')}</button>
            ))}
          </div>
        </section>
      )}
      {message && <p className="rounded-xl bg-due-soft p-3 text-sm font-semibold text-red-800">{message}</p>}
    </>
  )
}

/** Opens a line's bill photo (it needs the signed-in token, so it is fetched, then shown). */
async function openPhoto(teamId: string, lineId: string) {
  const response = await request(`/v1/teams/${teamId}/expenses/${lineId}/photo`)
  if (response.ok) window.open(URL.createObjectURL(await response.blob()), '_blank')
}

/**
 * Advances: money the owner hands someone to spend for the team. Each person's account shows given, spent from it,
 * returned, and what they still hold. The owner or an admin records money given or handed back; members see their own.
 */
function TeamAdvances({ team }: { team: Team }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const manages = team.myRole === 'owner' || team.myRole === 'admin'
  const data = useQuery({ queryKey: ['team', team.id, 'advances'], queryFn: () => api<Advances>(`/v1/teams/${team.id}/advances`) }).data
  const people = team.members.filter((m) => m.role !== 'viewer')
  const [form, setForm] = useState({ memberId: people.find((m) => !m.isMe)?.id ?? team.myMemberId, kind: 'given' as 'given' | 'returned', amount: '', note: '' })
  const { busy, message, run } = useRun()
  const save = (next: Advances) => queryClient.setQueryData(['team', team.id, 'advances'], next)
  const name = (n: string) => (n === 'Former member' ? t('team.former') : n)

  return (
    <>
      <p className="text-sm text-muted">{t('team.advanceIntro')}</p>
      {data?.balances.length === 0 && <p className="rounded-[20px] bg-white p-6 text-center text-muted">{t('team.noAdvances')}</p>}
      {data && data.balances.length > 0 && (
        <ul className="flex flex-col gap-2">
          {data.balances.map((b) => (
            <li key={b.memberId} className="flex flex-col gap-1 rounded-[20px] bg-white p-4">
              <span className="flex items-baseline justify-between"><b className="text-lg">{name(b.name)}</b>
                <span className={`text-xl font-extrabold ${b.holds < 0 ? 'text-due' : ''}`}>{formatRupees(b.holds)}</span></span>
              <span className="text-xs text-muted">{b.holds < 0 ? t('team.overspent') : t('team.holds')}</span>
              <span className="text-sm">{t('team.advanceLine', { given: formatRupees(b.given), spent: formatRupees(b.spent), returned: formatRupees(b.returned) })}</span>
            </li>
          ))}
        </ul>
      )}

      {manages && (
        <section className="flex flex-col gap-2 rounded-[20px] bg-white p-4">
          <h2 className="font-bold">{t('team.advanceTitle')}</h2>
          <div className="grid grid-cols-2 gap-2">
            {(['given', 'returned'] as const).map((k) => (
              <button key={k} onClick={() => setForm({ ...form, kind: k })} aria-pressed={form.kind === k}
                className={`min-h-11 rounded-xl font-semibold ${form.kind === k ? 'bg-brand text-white' : 'border border-line'}`}>{t(`team.advanceKind.${k}`)}</button>
            ))}
          </div>
          <select value={form.memberId} onChange={(e) => setForm({ ...form, memberId: e.target.value })} aria-label={t('team.spentBy')} className="h-12 rounded-xl border border-line bg-white px-2 outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10">
            {people.map((m) => <option key={m.id} value={m.id}>{m.name}</option>)}
          </select>
          <div className="grid grid-cols-[1fr_2fr] gap-2">
            <input value={form.amount} onChange={(e) => setForm({ ...form, amount: e.target.value.replace(/[^\d.]/g, '') })} inputMode="decimal" placeholder="₹" aria-label={t('team.amount')} className="h-12 min-w-0 rounded-xl border border-line px-3 text-lg font-bold" />
            <input value={form.note} onChange={(e) => setForm({ ...form, note: e.target.value })} placeholder={t('team.advanceNote')} aria-label={t('team.note')} className="h-12 min-w-0 rounded-xl border border-line px-3 bg-white outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
          </div>
          <Button disabled={busy || !(Number(form.amount) > 0)} onClick={() => void run(async () => {
            save(await api<Advances>(`/v1/teams/${team.id}/advances`, 'POST', { ...form, amount: Number(form.amount), note: form.note.trim() || null }))
            setForm({ ...form, amount: '', note: '' })
          })}>{t(`team.advanceSave.${form.kind}`)}</Button>
          {message && <p className="text-sm font-semibold text-red-800">{message}</p>}
        </section>
      )}

      {data && data.entries.length > 0 && (
        <ul className="divide-y divide-[#edf2f1] rounded-[20px] bg-white px-4 text-sm">
          {data.entries.map((e) => (
            <li key={e.id} className={`flex items-center justify-between gap-2 py-2 ${e.struck ? 'text-muted line-through' : ''}`}>
              <span>{e.kind === 'given' ? '➡️' : '⬅️'} <b>{name(e.memberName)}</b> · {t(`team.advanceKind.${e.kind}`)}{e.note ? ` · ${e.note}` : ''}
                <span className="block text-xs text-muted">{new Date(e.at).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })}{e.by ? ` · ${e.by}` : ''}</span></span>
              <span className="flex items-center gap-2"><b>{formatRupees(e.amount)}</b>
                {manages && !e.struck && <button disabled={busy} aria-label={t('team.strike')} className={`h-9 w-9 ${btn.dangerOutline}`}
                  onClick={async () => (await confirmAsk(t('team.advanceStrikeAsk'))) && void run(async () => save(await api<Advances>(`/v1/teams/${team.id}/advances/${e.id}/strike`, 'POST', {})))}>✕</button>}</span>
            </li>
          ))}
        </ul>
      )}
    </>
  )
}

import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { addText, shownLocale } from '../i18n'
import { teamText } from '../i18n/team'
import { formatRupees } from '../lib/format'

addText(teamText)

interface NameTotal { memberId: string; name: string; total: number; count: number }
interface PublicView { name: string; month: string; monthTotal: number; monthByName: NameTotal[]; monthByCategory: { category: string; total: number; count: number }[]; todayTotal: number; todayByName: NameTotal[] }

/**
 * A team's view link (/tv/{team}?t=…), for people who haven't installed the app: this month's and today's totals by
 * name and heading. No notes; nothing to change. The owner can turn the link off at any time.
 */
export function TeamViewPage({ path }: { path: string }) {
  const { t } = useTranslation()
  const match = path.match(/^\/tv\/([0-9a-f-]{36})\?t=([0-9a-f]+)$/)
  const view = useQuery({
    queryKey: ['team-view', path],
    queryFn: async () => {
      const response = await fetch(`/api/v1/team-view/${match![1]}?t=${match![2]}`)
      if (!response.ok) throw new Error(String(response.status))
      return (await response.json()) as PublicView
    },
    enabled: !!match,
    retry: false,
  })
  if (!match || view.isError) return <p className="p-6 text-center">{t('team.viewGone')}</p>
  const v = view.data
  if (!v) return <p className="p-6 text-center">…</p>
  const name = (n: string) => (n === 'Former member' ? t('team.former') : n)
  const bars = (rows: NameTotal[]) => {
    const top = Math.max(1, ...rows.map((r) => r.total))
    return rows.map((r) => (
      <div key={r.memberId} className="flex flex-col gap-1">
        <span className="flex justify-between text-sm"><span className="font-semibold">{name(r.name)}</span><b>{formatRupees(r.total)}</b></span>
        <span className="h-2 rounded-full bg-ground"><span className="block h-2 rounded-full bg-teal-600" style={{ width: `${(r.total / top) * 100}%` }} /></span>
      </div>
    ))
  }
  return (
    <div className="mx-auto flex min-h-dvh max-w-md flex-col gap-3 p-4">
      <header className="rounded-[20px] bg-brand-strong p-4 text-white">
        <p className="text-sm opacity-80">{t('team.title')}</p>
        <h1 className="text-2xl font-extrabold">{v.name}</h1>
      </header>
      <section className="flex flex-col gap-3 rounded-[20px] bg-white p-4">
        <div className="flex items-baseline justify-between"><h2 className="font-bold">{t('team.today')}</h2><span className="text-xl font-extrabold">{formatRupees(v.todayTotal)}</span></div>
        {v.todayByName.length === 0 ? <p className="text-sm text-muted">{t('team.nothing')}</p> : bars(v.todayByName)}
      </section>
      <section className="flex flex-col gap-3 rounded-[20px] bg-white p-4">
        <div className="flex items-baseline justify-between">
          <h2 className="font-bold">{new Date(`${v.month}-01T00:00:00`).toLocaleDateString(shownLocale(), { month: 'long', year: 'numeric' })}</h2>
          <span className="text-2xl font-extrabold">{formatRupees(v.monthTotal)}</span>
        </div>
        {bars(v.monthByName)}
        <h3 className="mt-2 text-sm font-bold">{t('team.byHeading')}</h3>
        {v.monthByCategory.map((c) => <span key={c.category} className="flex justify-between text-sm"><span>{t(`expense.cat.${c.category}`)}</span><b>{formatRupees(c.total)}</b></span>)}
      </section>
      <a href="/" className="rounded-xl bg-brand p-3 text-center font-bold text-white">{t('team.getApp')}</a>
    </div>
  )
}

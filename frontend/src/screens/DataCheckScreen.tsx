import { btn } from '../components/ui'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { navigate } from '../router'

interface Problem { name: string; detail: string; id: string | null }
interface Check { code: 'khata' | 'stock' | 'billNumbers' | 'billPaid' | 'noCost'; checked: number; problems: number; warning: boolean; samples: Problem[] }
interface DataCheck { checkedAt: string; checks: Check[] }

/**
 * Verify my data: one button rechecks the khata, stock, bill numbers and bill payments, and lists anything that doesn't
 * add up, with where to look. It only reads: nothing is changed.
 */
export function DataCheckScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const q = useQuery({ queryKey: [shopId, 'data-check'], queryFn: () => api<DataCheck>('/v1/data-check'), staleTime: 0 })
  const data = q.data
  const errors = data?.checks.filter((c) => !c.warning).reduce((n, c) => n + c.problems, 0) ?? 0
  // Where each kind of problem can be looked at.
  const open = (c: Check, p: Problem) => c.code === 'khata' && p.id ? () => navigate(`/c/${p.id}`) : c.code === 'billPaid' && p.id ? () => navigate(`/print/bill/${p.id}`) : null
  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div><h1 className="text-lg font-extrabold">{t('dataCheck.title')}</h1><p className="text-sm text-muted">{t('dataCheck.hint')}</p></div>
        <button onClick={() => void q.refetch()} disabled={q.isFetching} className={`min-h-11 px-4 rounded-xl ${btn.solid}`}>
          {q.isFetching ? t('dataCheck.checking') : `↻ ${t('dataCheck.again')}`}</button>
      </div>
      {q.isError && <p className="text-sm text-due">{q.error.message}</p>}
      {data && (
        <p className={`rounded-card p-4 text-base font-bold ${errors === 0 ? 'bg-green-50 text-green-900' : 'bg-due-soft text-due'}`}>
          {errors === 0 ? `✓ ${t('dataCheck.allGood')}` : t('dataCheck.found', { count: errors })}
          <span className="block text-xs font-normal opacity-80">{t('dataCheck.at', { time: new Date(data.checkedAt).toLocaleString() })}</span>
        </p>
      )}
      {data?.checks.map((c) => (
        <section key={c.code} className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
          <div className="flex items-start gap-3">
            <span aria-hidden className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-full text-lg font-bold ${c.problems === 0 ? 'bg-green-50 text-green-700' : c.warning ? 'bg-warn-soft text-warn-ink' : 'bg-due-soft text-due'}`}>
              {c.problems === 0 ? '✓' : c.warning ? '!' : '✗'}</span>
            <div className="min-w-0 flex-1">
              <h2 className="font-bold">{t(`dataCheck.checks.${c.code}.title`)}</h2>
              <p className="text-sm text-muted">{c.problems === 0 ? t(`dataCheck.checks.${c.code}.ok`, { count: c.checked }) : t(`dataCheck.checks.${c.code}.bad`, { count: c.problems })}</p>
            </div>
          </div>
          {c.samples.length > 0 && (
            <ul className="divide-y divide-line rounded-xl bg-[#f6faf9] text-sm">
              {c.samples.map((p, i) => {
                const go = open(c, p)
                return (
                  <li key={`${p.name}-${i}`} className="flex items-center justify-between gap-2 px-3 py-2">
                    <span className="min-w-0 truncate font-semibold">{p.name}</span>
                    <span className="shrink-0 text-muted">{p.detail === 'twice' || p.detail === 'missing' ? t(`dataCheck.${p.detail}`) : p.detail && t(`dataCheck.checks.${c.code}.detail`, { stored: p.detail.split(' / ')[0], expected: p.detail.split(' / ')[1] })}</span>
                    {go && <button onClick={go} className="min-h-9 shrink-0 font-semibold text-brand underline">{t('dataCheck.open')}</button>}
                  </li>
                )
              })}
              {c.problems > c.samples.length && <li className="px-3 py-2 text-xs text-muted">{t('dataCheck.more', { count: c.problems - c.samples.length })}</li>}
            </ul>
          )}
          {c.problems > 0 && <p className="text-xs text-muted">{t(`dataCheck.checks.${c.code}.fix`)}</p>}
        </section>
      ))}
      <p className="text-xs text-muted">{t('dataCheck.readOnly')}</p>
    </div>
  )
}

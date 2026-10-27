import { useTranslation } from 'react-i18next'
import { useLists, type ShoppingList } from '../../lib/planner'
import { navigate } from '../../router'

/** Mockup 1, "My lists": the monthly list as the hero card overlapping the teal band, the weekly one below. */
export function PlanHomeScreen({ userId }: { userId: string }) {
  const { t } = useTranslation()
  const { lists } = useLists(userId)
  const monthly = lists.find((l) => l.kind === 'monthly')!
  const weekly = lists.find((l) => l.kind === 'weekly')!

  return (
    <div>
      <div className="h-14 bg-teal-700" />
      <ListCard
        list={monthly}
        title={t('plan.monthlyList')}
        hint={t('plan.monthlyHint')}
        hero
      />
      <ListCard list={weekly} title={t('plan.weeklyList')} hint={t('plan.weeklyHint')} />
    </div>
  )
}

function ListCard({ list, title, hint, hero = false }: { list: ShoppingList; title: string; hint: string; hero?: boolean }) {
  const { t } = useTranslation()
  const done = list.items.filter((i) => i.checked).length
  const count = list.items.length
  const percent = count === 0 ? 0 : Math.round((done / count) * 100)

  return (
    <button
      onClick={() => navigate(`/list/${list.kind}`)}
      className={`mx-4 flex w-[calc(100%-2rem)] flex-col gap-2.5 bg-white p-[18px] text-left ${
        hero ? '-mt-12 rounded-[20px] shadow-[0_6px_20px_rgba(16,32,29,0.08)]' : 'mt-3 rounded-[18px] border border-line'
      }`}
    >
      <span className="text-[13px] font-semibold text-muted">{title}</span>
      <span className={`${hero ? 'text-[26px]' : 'text-xl'} leading-tight font-extrabold`}>
        {count === 0 ? hint : t('plan.itemsCount', { count })}
      </span>
      {count > 0 && (
        <>
          <span className="h-2 overflow-hidden rounded bg-[#edf2f1]">
            <span className="block h-full bg-teal-700" style={{ width: `${percent}%` }} />
          </span>
          <span className="text-sm text-muted">{t('plan.tickedOf', { done, count })}</span>
        </>
      )}
    </button>
  )
}

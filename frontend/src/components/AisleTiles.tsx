import { aisleName, sectionName } from '../lib/aisleNames'
import { useTranslation } from 'react-i18next'
import { AISLE_ART } from '../lib/itemArt'
import { useAisles } from '../lib/aisles'

export interface Aisle {
  code: string
  name: string
  nameEn: string
  section: string
  sectionEn: string
}

/**
 * Browse by aisle: bold section headers ("रोज़ का राशन"), each with a grid of soft-tinted rounded tiles holding
 * the aisle's picture and a short name below, like the shopping apps' category grids. Only aisles in
 * `available` are shown (an aisle the shop has nothing in stays hidden); none given shows all.
 */
export function AisleTiles({ available, counts, onPick }: { available?: Set<string>; counts?: Map<string, number>; onPick: (aisle: Aisle) => void }) {
  const { i18n } = useTranslation()
  const aisles = (useAisles().data ?? []).filter((a) => !available || available.has(a.code))
  const sections = [...new Set(aisles.map((a) => a.section))]
  return (
    <div className="flex flex-col gap-4">
      {sections.map((section) => {
        const inSection = aisles.filter((a) => a.section === section)
        return (
          <section key={section}>
            <h3 className="mb-2 text-[17px] font-extrabold">{sectionName(inSection[0], i18n.language)}</h3>
            <div className="grid grid-cols-4 gap-x-2 gap-y-3 sm:grid-cols-6 lg:grid-cols-8">
              {inSection.map((aisle) => {
                const art = AISLE_ART[aisle.code]
                const count = counts?.get(aisle.code)
                return (
                  <button key={aisle.code} onClick={() => onPick(aisle)} className="group flex flex-col items-center gap-1.5 text-center">
                    <span
                      className="item-card relative flex aspect-square w-full items-center justify-center overflow-hidden rounded-2xl shadow-[0_1px_4px_rgba(16,32,29,0.08)] transition group-active:scale-95"
                      style={{ background: `linear-gradient(160deg, ${art?.tint ?? '#ecfdf8'}, ${art?.from ?? '#99f6e4'})` }}
                    >
                      <span aria-hidden className="item-card-emoji text-[40px] drop-shadow-[0_3px_4px_rgba(0,0,0,0.2)]">{art?.emoji ?? '🛒'}</span>
                      <span aria-hidden className="item-card-shine" />
                      {count ? <span className="absolute right-1 top-1 rounded-full bg-white/90 px-1.5 text-[10px] font-bold">{count}</span> : null}
                    </span>
                    <span className="line-clamp-2 text-[12px] leading-tight font-semibold">{aisleName(aisle, i18n.language)}</span>
                  </button>
                )
              })}
            </div>
          </section>
        )
      })}
    </div>
  )
}

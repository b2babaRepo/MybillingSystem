import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'

/**
 * Where goods come in or go out: the shop shelf ('') or one of the shop's godowns. Shows nothing for a shop without
 * godowns, so the screens stay as they were for most shops.
 */
export function GodownPicker({ shopId, value, onChange, label }: { shopId: string; value: string; onChange: (id: string) => void; label: 'in' | 'out' }) {
  const { t } = useTranslation()
  const godowns = (useQuery({ queryKey: [shopId, 'godowns'], queryFn: () => api<{ id: string; name: string; isActive: boolean }[]>('/v1/godowns'), staleTime: 300_000 }).data ?? [])
    .filter((g) => g.isActive)
  if (godowns.length === 0) return null
  return (
    <label className="flex items-center justify-between gap-2 text-sm">
      <span className="text-muted">{t(label === 'in' ? 'godown.unloadAt' : 'godown.handFrom')}</span>
      <select value={value} onChange={(e) => onChange(e.target.value)} className="h-11 min-w-0 rounded-xl border border-line bg-white px-2 font-semibold">
        <option value="">{t('godown.shop')}</option>
        {godowns.map((g) => <option key={g.id} value={g.id}>{g.name}</option>)}
      </select>
    </label>
  )
}

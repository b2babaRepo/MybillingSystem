import { shownLocale } from '../i18n'
import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Button, controlSm } from '../components/ui'
import { api } from '../lib/api'
import { navigate } from '../router'

interface FestivalItem { productId: string; name: string; category: string | null; unit: string; stock: number; onOrder: number; lastYear: number | null; usual: number; suggest: number }
interface Festival { key: string; name: string; date: string; daysLeft: number; changed: boolean; basis: 'last_year' | 'usual_items'; items: FestivalItem[] }
interface Supplier { id: string; name: string; isActive?: boolean }

const input = controlSm
const day = (iso: string) => new Date(`${iso}T00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })

/** Home: the next festival coming, with how many items to stock up on. */
export function FestivalCard({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const next = useQuery({ queryKey: ['festivals', shopId, 'on'], queryFn: () => api<Festival[]>('/v1/festivals/upcoming'), staleTime: 3_600_000 }).data?.[0]
  if (!next) return null
  const toOrder = next.items.filter((i) => i.suggest > 0).length
  return (
    <button onClick={() => navigate('/festivals')} className="flex items-center gap-3 rounded-card bg-warn-soft p-4 text-left shadow-sm">
      <span className="text-2xl" aria-hidden>🪔</span>
      <span className="flex-1">
        <span className="block font-bold">{t('fest.coming', { name: next.name, count: next.daysLeft })}</span>
        <span className="block text-sm text-muted">{toOrder > 0 ? t('fest.toOrder', { count: toOrder }) : t('fest.ready')}</span>
      </span>
      <span aria-hidden>›</span>
    </button>
  )
}

/**
 * Festivals: what to stock before each festival in the next month. From the shop's own sales in the same run-up last
 * year when there are any, or else the items that festival usually needs. The owner can move a date or turn one off.
 */
export function FestivalsScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const festivals = useQuery({ queryKey: ['festivals', shopId, 'all'], queryFn: () => api<Festival[]>('/v1/festivals/upcoming?all=true') }).data ?? []
  const off = useQuery({ queryKey: ['festivals', shopId, 'on'], queryFn: () => api<Festival[]>('/v1/festivals/upcoming') }).data
  const suppliers = (useQuery({ queryKey: [shopId, 'suppliers'], queryFn: () => api<Supplier[]>('/v1/suppliers'), enabled: isOwner }).data ?? []).filter((s) => s.isActive !== false)
  const [qty, setQty] = useState<Record<string, string>>({})
  const [supplier, setSupplier] = useState<Record<string, string>>({})
  const [editing, setEditing] = useState<string | null>(null)
  const [date, setDate] = useState('')
  const [message, setMessage] = useState('')
  const isOff = (f: Festival) => !!off && !off.some((x) => x.key === f.key && x.date === f.date)

  async function run(action: () => Promise<unknown>, done?: string) {
    setMessage('')
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: ['festivals', shopId] })
      if (done) setMessage(done)
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  const amount = (f: Festival, i: FestivalItem) => Number(qty[`${f.key}:${i.productId}`] ?? i.suggest) || 0
  const setting = (f: Festival, body: object) => run(() => api('/v1/festivals/settings', 'POST', { key: f.key, year: Number(f.date.slice(0, 4)), ...body }))
  const draft = (f: Festival) => {
    const lines = f.items.map((i) => ({ productId: i.productId, quantity: amount(f, i) })).filter((l) => l.quantity > 0)
    void run(async () => {
      await api('/v1/suppliers/orders', 'POST', { id: crypto.randomUUID(), supplierId: supplier[f.key], draft: true, note: f.name, lines })
      void queryClient.invalidateQueries({ queryKey: [shopId, 'suppliers'] })
    }, t('fest.drafted'))
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      <header>
        <h1 className="text-xl font-bold">{t('fest.title')}</h1>
        <p className="text-sm text-muted">{t('fest.hint')}</p>
      </header>
      {festivals.length === 0 && <p className="rounded-xl bg-white p-4 text-sm text-muted shadow-sm">{t('fest.none')}</p>}
      {message && <p className="text-sm text-brand-strong">{message}</p>}
      {festivals.map((f) => {
        const muted = isOff(f)
        const ordering = f.items.some((i) => amount(f, i) > 0)
        return (
          <section key={`${f.key}${f.date}`} className={`flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm ${muted ? 'opacity-60' : ''}`}>
            <div className="flex items-start justify-between gap-2">
              <div>
                <h2 className="text-lg font-bold">🪔 {f.name}</h2>
                <p className="text-sm text-muted">{day(f.date)} · {t('fest.inDays', { count: f.daysLeft })}{muted ? ` · ${t('fest.off')}` : ''}</p>
              </div>
              {isOwner && (
                <div className="flex flex-col items-end gap-1 text-xs font-semibold text-brand">
                  <button onClick={() => { setEditing(editing === f.key ? null : f.key); setDate(f.date) }}>{t('fest.changeDate')}</button>
                  <button onClick={() => void setting(f, { off: !muted })}>{muted ? t('fest.turnOn') : t('fest.turnOff')}</button>
                </div>
              )}
            </div>
            {editing === f.key && (
              <div className="flex gap-2">
                <input type="date" value={date} onChange={(e) => setDate(e.target.value)} className={input} />
                <Button onClick={() => { void setting(f, { date }); setEditing(null) }}>{t('fest.saveDate')}</Button>
              </div>
            )}
            <p className="rounded-xl bg-[#f1f5f4] p-2 text-xs text-muted">{f.basis === 'last_year' ? t('fest.fromLastYear') : t('fest.fromUsual')}</p>
            {f.items.length === 0 && <p className="text-sm text-muted">{t('fest.noItems')}</p>}
            {f.items.length > 0 && (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead><tr className="text-left text-xs text-muted">
                    <th className="py-1 pr-2">{t('godown.item')}</th>
                    <th className="px-1 text-right">{f.basis === 'last_year' ? t('fest.lastYear') : t('fest.usual')}</th>
                    <th className="px-1 text-right">{t('fest.inStock')}</th>
                    <th className="pl-1 text-right">{t('fest.order')}</th>
                  </tr></thead>
                  <tbody>
                    {f.items.map((i) => (
                      <tr key={i.productId} className="border-t border-line">
                        <td className="py-1 pr-2">{i.name}</td>
                        <td className="px-1 text-right tabular-nums">{f.basis === 'last_year' ? i.lastYear : i.usual}</td>
                        <td className="px-1 text-right tabular-nums">{i.stock}{i.onOrder > 0 ? <span className="block text-xs text-muted">+{i.onOrder} {t('fest.coming2')}</span> : null}</td>
                        <td className="pl-1 text-right">
                          {isOwner
                            ? <input aria-label={`${t('fest.order')} ${i.name}`} value={qty[`${f.key}:${i.productId}`] ?? String(i.suggest)} onChange={(e) => setQty({ ...qty, [`${f.key}:${i.productId}`]: e.target.value })} inputMode="decimal" className="h-9 w-16 rounded-lg border border-line px-2 text-right" />
                            : <span className="font-semibold tabular-nums">{i.suggest}</span>}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
            {isOwner && ordering && (
              <div className="grid grid-cols-[1fr_auto] gap-2">
                <select aria-label={t('bardana.supplier')} value={supplier[f.key] ?? ''} onChange={(e) => setSupplier({ ...supplier, [f.key]: e.target.value })} className={input}>
                  <option value="">{t('fest.pickSupplier')}</option>
                  {suppliers.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
                </select>
                <Button disabled={!supplier[f.key]} onClick={() => draft(f)}>{t('fest.draft')}</Button>
              </div>
            )}
          </section>
        )
      })}
    </div>
  )
}

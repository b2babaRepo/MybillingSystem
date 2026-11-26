import { controlSm, btn } from '../components/ui'
import { useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { confirmAsk } from '../lib/ask'
import { api } from '../lib/api'
import { useCategories, type CategoryRow } from '../lib/categories'
import { GST_RATES } from '../lib/gst'
import type { Unit } from '../lib/types'

const UNITS: Unit[] = ['piece', 'kg', 'g', 'litre', 'ml', 'packet', 'dozen', 'box', 'bag', 'metre']
const input = controlSm

interface Draft { name: string; parentId: string; isActive: boolean; gst: string; hsn: string; unit: string; cover: string; slow: string }
const draftOf = (c: CategoryRow): Draft => ({
  name: c.name, parentId: c.parentId ?? '', isActive: c.isActive, gst: c.defaultGstRate == null ? '' : String(c.defaultGstRate), hsn: c.defaultHsn ?? '',
  unit: c.defaultUnit ?? '', cover: c.targetCoverDays == null ? '' : String(c.targetCoverDays), slow: c.slowAfterDays == null ? '' : String(c.slowAfterDays),
})

/**
 * Manage categories: one list the whole app uses. Rename (every item follows), merge two names for one group, hide,
 * put a group under another, set the order of the chips, and set defaults that new items in it take.
 */
export function CategoriesScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const categories = useCategories(shopId)
  const rows = categories.data ?? []
  const [open, setOpen] = useState<string | null>(null)
  const [draft, setDraft] = useState<Draft | null>(null)
  const [mergeInto, setMergeInto] = useState('')
  const [newName, setNewName] = useState('')
  const [message, setMessage] = useState('')

  async function run(action: () => Promise<unknown>) {
    setMessage('')
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: [shopId, 'categories'] })
      void queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
      return true
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
      return false
    }
  }

  const body = (d: Draft) => ({
    name: d.name.trim(), parentId: d.parentId || null, isActive: d.isActive, defaultGstRate: d.gst === '' ? null : Number(d.gst), defaultHsn: d.hsn.trim() || null,
    defaultUnit: d.unit || null, targetCoverDays: d.cover === '' ? null : Number(d.cover), slowAfterDays: d.slow === '' ? null : Number(d.slow),
  })

  async function move(i: number, by: -1 | 1) {
    const ids = rows.map((r) => r.id)
    const j = i + by
    if (j < 0 || j >= ids.length) return
    ;[ids[i], ids[j]] = [ids[j], ids[i]]
    queryClient.setQueryData<CategoryRow[]>([shopId, 'categories'], ids.map((id) => rows.find((r) => r.id === id)!))
    await run(() => api('/v1/categories/order', 'POST', { ids }))
  }

  async function merge(c: CategoryRow) {
    const into = rows.find((r) => r.id === mergeInto)
    if (!into || !(await confirmAsk(t('cat.mergeConfirm', { from: c.name, into: into.name, count: c.items })))) return
    if (await run(() => api(`/v1/categories/${c.id}/merge`, 'POST', { intoId: into.id }))) setOpen(null)
  }

  const tops = rows.filter((r) => r.parentId == null)
  return (
    <div className="flex flex-col gap-3 p-4">
      <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
        <h1 className="text-xl font-bold">🗂️ {t('cat.title')}</h1>
        <p className="text-sm text-muted">{t('cat.hint')}</p>
        <form onSubmit={(e) => { e.preventDefault(); if (newName.trim()) void run(() => api('/v1/categories', 'POST', { name: newName.trim() })).then((ok) => ok && setNewName('')) }} className="flex gap-2">
          <input value={newName} onChange={(e) => setNewName(e.target.value)} maxLength={60} placeholder={t('cat.newPlaceholder')} aria-label={t('cat.new')} className={`${input} flex-1`} />
          <button className={`h-11 px-4 rounded-xl ${btn.solid}`}>{t('cat.add')}</button>
        </form>
      </section>
      {message && <p className="text-sm text-due">{message}</p>}
      {categories.data && rows.length === 0 && <p className="py-6 text-center text-muted">{t('cat.none')}</p>}
      <ul className="flex flex-col gap-2">
        {rows.map((c, i) => (
          <li key={c.id} className={`rounded-2xl bg-white shadow-sm ${c.isActive ? '' : 'opacity-60'}`}>
            <div className="flex items-center gap-2 p-3">
              <span className="flex flex-col">
                <button aria-label={t('cat.up', { name: c.name })} disabled={i === 0} onClick={() => void move(i, -1)} className="h-6 w-8 rounded text-muted hover:bg-[#edf2f1] disabled:opacity-30">▲</button>
                <button aria-label={t('cat.down', { name: c.name })} disabled={i === rows.length - 1} onClick={() => void move(i, 1)} className="h-6 w-8 rounded text-muted hover:bg-[#edf2f1] disabled:opacity-30">▼</button>
              </span>
              <button onClick={() => { setOpen(open === c.id ? null : c.id); setDraft(draftOf(c)); setMergeInto('') }} className="flex min-w-0 flex-1 flex-col text-left">
                <b className="truncate">{c.parentId && <span className="text-muted">{rows.find((r) => r.id === c.parentId)?.name} › </span>}{c.name}</b>
                <span className="text-xs text-muted">
                  {t('cat.items', { count: c.items })}{!c.isActive && ` · ${t('cat.hidden')}`}
                  {c.defaultGstRate != null && ` · GST ${c.defaultGstRate}%`}{c.slowAfterDays != null && ` · ${t('cat.slowShort', { days: c.slowAfterDays })}`}
                </span>
              </button>
              <span aria-hidden className="text-muted">{open === c.id ? '▾' : '▸'}</span>
            </div>
            {open === c.id && draft && (
              <div className="flex flex-col gap-2 border-t border-line p-3 text-sm">
                <label className="flex flex-col gap-1 text-xs text-muted">{t('cat.name')}<input value={draft.name} maxLength={60} onChange={(e) => setDraft({ ...draft, name: e.target.value })} className={input} /></label>
                <label className="flex flex-col gap-1 text-xs text-muted">{t('cat.under')}
                  <select value={draft.parentId} onChange={(e) => setDraft({ ...draft, parentId: e.target.value })} className={input}>
                    <option value="">{t('cat.topLevel')}</option>
                    {tops.filter((r) => r.id !== c.id).map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
                  </select>
                </label>
                <p className="mt-1 text-xs font-semibold text-muted">{t('cat.defaults')}</p>
                <div className="grid grid-cols-2 gap-2">
                  <label className="flex flex-col gap-1 text-xs text-muted">GST
                    <select value={draft.gst} onChange={(e) => setDraft({ ...draft, gst: e.target.value })} className={input}>
                      <option value="">—</option>
                      {GST_RATES.map((r) => <option key={r} value={r}>{r}%</option>)}
                    </select>
                  </label>
                  <label className="flex flex-col gap-1 text-xs text-muted">HSN<input value={draft.hsn} inputMode="numeric" maxLength={8} onChange={(e) => setDraft({ ...draft, hsn: e.target.value })} className={input} /></label>
                  <label className="flex flex-col gap-1 text-xs text-muted">{t('cat.unit')}
                    <select value={draft.unit} onChange={(e) => setDraft({ ...draft, unit: e.target.value })} className={input}>
                      <option value="">—</option>
                      {UNITS.map((u) => <option key={u} value={u}>{t(`units.${u}`)}</option>)}
                    </select>
                  </label>
                  <label className="flex flex-col gap-1 text-xs text-muted">{t('cat.cover')}<input value={draft.cover} inputMode="numeric" onChange={(e) => setDraft({ ...draft, cover: e.target.value })} placeholder="30" className={input} /></label>
                  <label className="col-span-2 flex flex-col gap-1 text-xs text-muted">{t('cat.slowAfter')}<input value={draft.slow} inputMode="numeric" onChange={(e) => setDraft({ ...draft, slow: e.target.value })} placeholder="30" className={input} /></label>
                </div>
                <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={!draft.isActive} onChange={(e) => setDraft({ ...draft, isActive: !e.target.checked })} className="h-5 w-5" />{t('cat.hide')}</label>
                <button onClick={() => void run(() => api(`/v1/categories/${c.id}`, 'PUT', body(draft))).then((ok) => ok && setOpen(null))} className={`h-11 rounded-xl ${btn.solid}`}>{t('cat.save')}</button>
                <div className="mt-2 flex flex-wrap items-center gap-2 border-t border-line pt-3">
                  <span className="text-xs text-muted">{t('cat.mergeLabel')}</span>
                  <select aria-label={t('cat.mergeLabel')} value={mergeInto} onChange={(e) => setMergeInto(e.target.value)} className={`${input} flex-1`}>
                    <option value="">{t('cat.pick')}</option>
                    {rows.filter((r) => r.id !== c.id).map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
                  </select>
                  <button disabled={!mergeInto} onClick={() => void merge(c)} className={`h-11 px-3 hover:bg-due-soft ${btn.dangerOutline}`}>{t('cat.merge')}</button>
                </div>
              </div>
            )}
          </li>
        ))}
      </ul>
    </div>
  )
}

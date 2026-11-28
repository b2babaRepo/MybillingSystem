import { useQueryClient } from '@tanstack/react-query'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { api, request } from '../lib/api'
import { ICONS, KIRANA, type Category, type Department } from '../lib/categoryTree'
import { shrinkPhoto } from '../lib/images'
import { applyMenu, MENU_TRADE, menuPicture, useMenuNodes, type MenuNode } from '../lib/menuConfig'

interface Row { key: string; kind: 'dept' | 'cat'; hi: string; en: string; gu: string; icon: string; tint: string; ink: string; parent: string | null; hidden: boolean; builtIn: boolean; node?: MenuNode; picture?: string }

const Glyph = ({ name, stroke = 'currentColor' }: { name: string; stroke?: string }) => (
  <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke={stroke} strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={ICONS[name as keyof typeof ICONS] ?? ICONS.box} /></svg>
)

const slug = (text: string) => text.toLowerCase().normalize('NFKD').replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '').slice(0, 30)

/**
 * The platform admin's category menu for kirana shops: every department on the rail and every category tile under it,
 * with its picture, names in three languages, order, hiding, moving a category, and new departments and categories.
 * Changes reach every shop's bill screen, orders, picker and online store; "Reset" puts a node back as built in.
 */
export function PlatformMenuPanel() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const data = useMenuNodes().data
  const nodes = useMemo(() => data ?? [], [data])
  const byKey = useMemo(() => new Map(nodes.map((n) => [n.key, n])), [nodes])
  // Placement as if nothing were hidden, so hidden nodes still show here (greyed) and can be shown again.
  const tree = useMemo(() => applyMenu(KIRANA, nodes.map((n) => ({ ...n, isHidden: false }))), [nodes])
  const builtIn = useMemo(() => new Set(KIRANA.flatMap((d) => [d.key, ...d.categories.map((c) => c.key)])), [])
  const row = (x: Department | Category, kind: 'dept' | 'cat', parent: string | null): Row => {
    const n = byKey.get(x.key)
    return { key: x.key, kind, hi: x.hi, en: x.en, gu: x.gu, icon: x.icon, tint: 'tint' in x ? x.tint : '#eef4f3', ink: 'ink' in x ? x.ink : '#2f5f58', parent, hidden: !!n?.isHidden, builtIn: builtIn.has(x.key), node: n, picture: n ? menuPicture(MENU_TRADE, n) : undefined }
  }
  const [selected, setSelected] = useState<string>(KIRANA[0].key)
  const [creating, setCreating] = useState<'dept' | 'cat' | null>(null)
  const all = tree.flatMap((d) => [row(d, 'dept', null), ...d.categories.map((c) => row(c, 'cat', d.key))])
  const current = all.find((r) => r.key === selected)
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['menu', MENU_TRADE] })

  return (
    <section className="flex flex-col gap-3">
      <header className="flex flex-wrap items-end justify-between gap-2">
        <div>
          <h2 className="text-xl font-bold">{t('menuAdmin.title')}</h2>
          <p className="text-sm text-muted">{t('menuAdmin.hint')}</p>
        </div>
        <div className="flex gap-2">
          <Button tone="plain" onClick={() => { setCreating('dept'); setSelected('') }}>{t('menuAdmin.newDept')}</Button>
          <Button tone="plain" onClick={() => { setCreating('cat'); setSelected('') }}>{t('menuAdmin.newCat')}</Button>
        </div>
      </header>
      <div className="grid gap-3 lg:grid-cols-[minmax(0,1fr)_380px]">
        <div className="flex flex-col gap-2">
          {tree.map((d) => {
            const dr = row(d, 'dept', null)
            return (
              <div key={d.key} className="rounded-card border border-line bg-white p-3">
                <button onClick={() => { setSelected(d.key); setCreating(null) }} className={`flex w-full items-center gap-3 rounded-xl p-1 text-left ${selected === d.key ? 'bg-brand-soft' : ''} ${dr.hidden ? 'opacity-50' : ''}`}>
                  <Thumb r={dr} size="h-12 w-12" />
                  <span className="flex-1"><b>{d.hi}</b> <span className="text-sm text-muted">{d.en} · {d.gu}</span></span>
                  {dr.hidden && <span className="rounded-full bg-ground px-2 text-xs">{t('menuAdmin.hidden')}</span>}
                  {dr.node && <span className="rounded-full bg-warn-soft px-2 text-xs text-warn-ink">{t('menuAdmin.changed')}</span>}
                </button>
                <div className="mt-2 grid grid-cols-[repeat(auto-fill,minmax(110px,1fr))] gap-2">
                  {d.categories.map((c) => {
                    const cr = row(c, 'cat', d.key)
                    return (
                      <button key={c.key} onClick={() => { setSelected(c.key); setCreating(null) }}
                        className={`flex flex-col items-center gap-1 rounded-xl border p-2 text-center text-sm ${selected === c.key ? 'border-brand bg-brand-soft' : 'border-line'} ${cr.hidden ? 'opacity-50' : ''}`}>
                        <Thumb r={cr} size="h-16 w-16" />
                        <span className="font-semibold leading-tight">{c.hi}</span>
                        <span className="text-xs text-muted">{c.en}</span>
                        {cr.hidden && <span className="text-[11px] text-muted">{t('menuAdmin.hidden')}</span>}
                      </button>
                    )
                  })}
                </div>
              </div>
            )
          })}
        </div>
        <div className="lg:sticky lg:top-20 lg:self-start">
          {creating
            ? <Editor key={`new-${creating}`} kind={creating} departments={tree} onSaved={(key) => { setCreating(null); setSelected(key); void refresh() }} onCancel={() => setCreating(null)} />
            : current && <Editor key={current.key} row={current} kind={current.kind} departments={tree} onSaved={() => void refresh()} />}
        </div>
      </div>
    </section>
  )
}

function Thumb({ r, size }: { r: Row; size: string }) {
  return (
    <span className={`flex ${size} shrink-0 items-center justify-center overflow-hidden rounded-xl`} style={{ background: r.tint }}>
      {r.picture ? <img src={r.picture} alt="" className="h-full w-full object-cover" /> : <Glyph name={r.icon} stroke={r.ink} />}
    </span>
  )
}

/** One department or category: picture, names, order, hiding, its department, and its words (a new category). */
function Editor({ row, kind, departments, onSaved, onCancel }: { row?: Row; kind: 'dept' | 'cat'; departments: Department[]; onSaved: (key: string) => void; onCancel?: () => void }) {
  const { t } = useTranslation()
  const n = row?.node
  const [form, setForm] = useState({
    hi: n?.nameHi ?? row?.hi ?? '', en: n?.nameEn ?? row?.en ?? '', gu: n?.nameGu ?? row?.gu ?? '',
    order: n?.sortOrder == null ? '' : String(n.sortOrder), hidden: n?.isHidden ?? false,
    parent: n?.parentKey ?? row?.parent ?? departments[0]?.key ?? '', words: n?.words ?? '', icon: n?.icon ?? row?.icon ?? 'box',
  })
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)
  const [preview, setPreview] = useState<string | null>(null)
  const key = row?.key ?? slug(form.en)
  const set = (next: Partial<typeof form>) => setForm({ ...form, ...next })
  const field = 'h-11 w-full rounded-xl border border-line bg-white px-3'

  async function run(action: () => Promise<unknown>, done: string) {
    setBusy(true)
    setMessage(null)
    try {
      await action()
      setMessage({ ok: true, text: done })
      onSaved(key)
    } catch (e) {
      setMessage({ ok: false, text: e instanceof Error ? e.message : t('error') })
    } finally {
      setBusy(false)
    }
  }
  const save = () => {
    if (!key) { setMessage({ ok: false, text: t('menuAdmin.needName') }); return }
    void run(() => api(`/v1/platform/menu/${MENU_TRADE}/${key}`, 'PUT', {
      kind, parentKey: kind === 'cat' && (form.parent !== row?.parent || !row?.builtIn) ? form.parent : null,
      nameHi: form.hi, nameEn: form.en, nameGu: form.gu, sortOrder: form.order === '' ? null : Number(form.order),
      isHidden: form.hidden, words: kind === 'cat' ? form.words : null, icon: form.icon,
    }), t('menuAdmin.saved'))
  }
  async function upload(file: File | undefined) {
    if (!file || !key) return
    const dataUrl = await shrinkPhoto(file, 800, 900_000, false)
    setPreview(dataUrl)
    await run(() => api(`/v1/platform/menu/${MENU_TRADE}/${key}/picture`, 'PUT', { dataUrl, kind }), t('menuAdmin.pictureSaved'))
  }
  const picture = preview ?? row?.picture

  return (
    <div className="flex flex-col gap-3 rounded-card border border-line bg-white p-4">
      <h3 className="text-lg font-bold">{row ? `${row.hi} · ${row.en}` : t(kind === 'dept' ? 'menuAdmin.newDept' : 'menuAdmin.newCat')}</h3>
      <div className="flex items-center gap-3">
        <span className="flex h-24 w-24 shrink-0 items-center justify-center overflow-hidden rounded-2xl border border-line" style={{ background: row?.tint ?? '#eef4f3' }}>
          {picture ? <img src={picture} alt="" className="h-full w-full object-cover" /> : <Glyph name={form.icon} stroke={row?.ink} />}
        </span>
        <div className="flex flex-col gap-2">
          <label className={`flex min-h-11 cursor-pointer items-center rounded-xl border border-brand px-3 font-semibold text-brand-strong ${!key ? 'pointer-events-none opacity-50' : ''}`}>
            {t('menuAdmin.upload')}
            <input type="file" accept="image/*" hidden onChange={(e) => { void upload(e.target.files?.[0]); e.target.value = '' }} />
          </label>
          {row?.picture && !preview && <button onClick={() => void run(() => request(`/v1/platform/menu/${MENU_TRADE}/${key}/picture`, 'DELETE'), t('menuAdmin.pictureRemoved'))} className="text-left text-sm text-muted underline">{t('menuAdmin.removePicture')}</button>}
          {!key && <span className="text-xs text-muted">{t('menuAdmin.nameFirst')}</span>}
        </div>
      </div>
      <label className="text-sm font-semibold">{t('menuAdmin.nameHi')}<input value={form.hi} onChange={(e) => set({ hi: e.target.value })} maxLength={60} className={field} /></label>
      <label className="text-sm font-semibold">{t('menuAdmin.nameEn')}<input value={form.en} onChange={(e) => set({ en: e.target.value })} maxLength={60} className={field} /></label>
      <label className="text-sm font-semibold">{t('menuAdmin.nameGu')}<input value={form.gu} onChange={(e) => set({ gu: e.target.value })} maxLength={60} className={field} /></label>
      {kind === 'cat' && (
        <label className="text-sm font-semibold">{t('menuAdmin.department')}
          <select value={form.parent} onChange={(e) => set({ parent: e.target.value })} className={field}>
            {departments.map((d) => <option key={d.key} value={d.key}>{d.hi} · {d.en}</option>)}
          </select>
        </label>
      )}
      {kind === 'cat' && (
        <label className="text-sm font-semibold">{t('menuAdmin.words')}
          <input value={form.words} onChange={(e) => set({ words: e.target.value })} maxLength={300} placeholder="rusk, toast, टोस्ट" className={field} />
          <span className="block text-xs font-normal text-muted">{t('menuAdmin.wordsHint')}</span>
        </label>
      )}
      <div className="grid grid-cols-2 gap-2">
        <label className="text-sm font-semibold">{t('menuAdmin.order')}<input value={form.order} onChange={(e) => set({ order: e.target.value.replace(/[^\d-]/g, '') })} inputMode="numeric" placeholder={t('menuAdmin.orderAuto')} className={field} /></label>
        <label className="text-sm font-semibold">{t('menuAdmin.icon')}
          <select value={form.icon} onChange={(e) => set({ icon: e.target.value })} className={field}>
            {Object.keys(ICONS).map((i) => <option key={i} value={i}>{i}</option>)}
          </select>
        </label>
      </div>
      <label className="flex min-h-11 items-center gap-2 font-semibold"><input type="checkbox" checked={form.hidden} onChange={(e) => set({ hidden: e.target.checked })} className="h-5 w-5" />{t('menuAdmin.hide')}</label>
      {message && <p role="status" className={`text-sm ${message.ok ? 'text-jama' : 'text-due'}`}>{message.text}</p>}
      <div className="flex flex-wrap gap-2">
        <Button disabled={busy} onClick={save}>{t('menuAdmin.save')}</Button>
        {row?.node && <Button tone="plain" disabled={busy} onClick={() => void run(() => request(`/v1/platform/menu/${MENU_TRADE}/${key}`, 'DELETE'), t('menuAdmin.reset'))}>{t(row.builtIn ? 'menuAdmin.resetBuiltIn' : 'menuAdmin.delete')}</Button>}
        {onCancel && <Button tone="plain" onClick={onCancel}>{t('cancel')}</Button>}
      </div>
    </div>
  )
}

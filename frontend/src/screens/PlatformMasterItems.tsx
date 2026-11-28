import { SHOP_TYPES, type ShopType } from '../lib/reports'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { btn } from '../lib/buttonStyles'
import { api, request } from '../lib/api'
import { shrinkPhoto } from '../lib/images'
import { shownError } from '../lib/notify'

interface MasterRow { id: number; trade: string; category: string; name: string; nameHi: string | null; unit: string; gstRate: number | null; hsnCode: string | null; isActive: boolean; packSizes: string[]; imageAt?: string | null; imageCredit?: string | null }

interface SuggestionRow { id: number; shopName: string; trade: string; category: string; name: string; unit: string; note: string | null }

/** Items shops asked for: approve (it joins that trade's master list) or turn down. */
function SuggestionsQueue({ onDone }: { onDone: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const rows = useQuery({ queryKey: ['platform', 'suggestions'], queryFn: () => api<SuggestionRow[]>('/v1/platform/master-items/suggestions') }).data ?? []
  const [message, setMessage] = useState<string | null>(null)
  const decide = async (r: SuggestionRow, approve: boolean) => {
    setMessage(null)
    try {
      await (approve
        ? api(`/v1/platform/master-items/suggestions/${r.id}/approve`, 'POST', { trade: r.trade, name: r.name, category: r.category, unit: r.unit })
        : api(`/v1/platform/master-items/suggestions/${r.id}/reject`, 'POST', {}))
      void queryClient.invalidateQueries({ queryKey: ['platform', 'suggestions'] })
      onDone()
    } catch (e) {
      setMessage(shownError(e))
    }
  }
  if (rows.length === 0) return null
  return (
    <div className="rounded-xl bg-warn-soft p-3">
      <h3 className="font-bold">{t('masterItems.suggestions', { count: rows.length })}</h3>
      {message && <p className="text-sm text-due">{message}</p>}
      <ul className="mt-2 flex flex-col gap-1">
        {rows.map((r) => (
          <li key={r.id} className="flex items-center gap-2 rounded-xl bg-white px-2">
            <span className="min-w-0 flex-1">{r.name}<span className="block text-xs text-muted">{t(`shopType.${r.trade}`)} · {r.category} · {t(`units.${r.unit}`)} · {r.shopName}{r.note ? ` · ${r.note}` : ''}</span></span>
            <button onClick={() => void decide(r, true)} className="min-h-11 px-2 font-semibold text-brand">{t('masterItems.approve')}</button>
            <button onClick={() => void decide(r, false)} className="min-h-11 px-2 font-semibold">{t('masterItems.reject')}</button>
          </li>
        ))}
      </ul>
    </div>
  )
}

/** The master item lists every shop picks from, one per trade: add, edit, or turn an item off (never deleted). */
export function MasterItemsPanel() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [trade, setTrade] = useState<ShopType>('kirana')
  const [q, setQ] = useState('')
  const empty = { name: '', nameHi: '', category: '', unit: 'piece', gst: '', hsn: '', packs: '' }
  const [draft, setDraft] = useState(empty)
  const [importing, setImporting] = useState(false)
  const [unitFilter, setUnitFilter] = useState('')
  const [gstFilter, setGstFilter] = useState('')
  const [noPhotoOnly, setNoPhotoOnly] = useState(false)
  const [adding, setAdding] = useState(false)
  const [editing, setEditing] = useState<number | null>(null)
  const [message, setMessage] = useState<string | null>(null)
  // Credit line for photos uploaded next (empty for the platform's own photos); browsers like some in-app ones block pop-up prompts.
  const [photoCredit, setPhotoCredit] = useState('')
  const rows = useQuery({ queryKey: ['platform', 'master', trade, q], queryFn: () => api<MasterRow[]>(`/v1/platform/master-items?trade=${trade}&q=${encodeURIComponent(q.trim())}`) }).data ?? []
  const body = (d: typeof empty, isActive?: boolean) => ({ trade, name: d.name.trim(), nameHi: d.nameHi.trim() || null, category: d.category.trim(), unit: d.unit, gstRate: d.gst === '' ? null : Number(d.gst), hsnCode: d.hsn.trim() || null, isActive, packSizes: d.packs.split(',').map((s) => s.trim()).filter(Boolean) })

  async function run(action: () => Promise<unknown>) {
    setMessage(null)
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: ['platform', 'master'] })
    } catch (e) {
      setMessage(shownError(e))
    }
  }

  const save = () => run(async () => {
    if (editing) await api(`/v1/platform/master-items/${editing}`, 'PUT', body(draft))
    else await api('/v1/platform/master-items', 'POST', body(draft))
    setDraft({ ...empty, category: draft.category, unit: draft.unit })
    setEditing(null)
  })
  const view = rows.filter((r) => (!unitFilter || r.unit === unitFilter)
    && (!gstFilter || (gstFilter === 'none' ? r.gstRate == null : r.gstRate === Number(gstFilter)))
    && (!noPhotoOnly || !r.imageAt))
  const [csv, setCsv] = useState('')
  const importCsv = () => run(async () => {
    const r = await api<{ added: number; updated: number; problems: string[] }>('/v1/platform/master-items/import', 'POST', { trade, text: csv })
    setCsv('')
    setMessage([t('masterItems.imported', r), ...r.problems].join('\n'))
  })
  const edit = (r: MasterRow) => { setEditing(r.id); setDraft({ name: r.name, nameHi: r.nameHi ?? '', category: r.category, unit: r.unit, gst: r.gstRate == null ? '' : String(r.gstRate), hsn: r.hsnCode ?? '', packs: r.packSizes.join(', ') }) }

  const input = 'h-11 min-w-0 rounded-xl border border-line bg-white px-3'
  return (
    <section className="flex flex-col gap-3 rounded-2xl bg-white p-4">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="mr-auto text-lg font-bold">{t('masterItems.title')} · {t('masterItems.count', { count: rows.length })}</h2>
        <button onClick={() => setImporting(!importing)} aria-expanded={importing} className={`flex min-h-11 items-center gap-2 rounded-xl px-4 ${btn.outline}`}><svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M12 15V3M7 8l5-5 5 5M4 15v5h16v-5" /></svg>{t('masterItems.import')}</button>
        <button onClick={() => { setAdding(!adding || editing !== null); setEditing(null); setDraft(empty) }} className={`min-h-11 rounded-xl px-4 ${adding && editing === null ? btn.outline : btn.solid}`}>{adding && editing === null ? t('cancel') : `+ ${t('masterItems.add')}`}</button>
      </div>
      {/* What needs work in this trade's list, at a glance. */}
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        <div className="flex min-h-[76px] flex-col justify-between rounded-[18px] bg-[#e3f2ee] p-3 text-[#0b5e4f]"><span className="text-[13px] font-bold">{t('plat5.items')}</span><b className="text-2xl">{rows.length}</b></div>
        <div className="flex min-h-[76px] flex-col justify-between rounded-[18px] bg-[#fdf1e3] p-3 text-[#a1460a]"><span className="text-[13px] font-bold">{t('plat5.noPhoto')}</span><b className="text-2xl">{rows.filter((r) => !r.imageAt).length}</b></div>
        <div className="flex min-h-[76px] flex-col justify-between rounded-[18px] bg-[#e8effd] p-3 text-[#1d4ed8]"><span className="text-[13px] font-bold">{t('plat5.noGst')}</span><b className="text-2xl">{rows.filter((r) => r.gstRate == null || !r.hsnCode).length}</b></div>
        <div className="flex min-h-[76px] flex-col justify-between rounded-[18px] bg-[#edf2f1] p-3 text-[#33423d]"><span className="text-[13px] font-bold">{t('plat5.off')}</span><b className="text-2xl">{rows.filter((r) => !r.isActive).length}</b></div>
      </div>
      <div className="-mx-1 flex gap-2 overflow-x-auto px-1 pb-1 [scrollbar-width:none]">
        {SHOP_TYPES.filter((x) => x !== 'other').map((x) => (
          <button key={x} onClick={() => { setTrade(x); setEditing(null) }} aria-pressed={trade === x}
            className={`min-h-10 shrink-0 rounded-full px-4 text-sm font-semibold whitespace-nowrap transition ${trade === x ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'border border-line bg-white'}`}>{t(`shopType.${x}`)}</button>
        ))}
      </div>
      <span className="relative block">
        <span aria-hidden className="pointer-events-none absolute top-1/2 left-3.5 -translate-y-1/2 text-muted"><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d="M11 4a7 7 0 100 14 7 7 0 000-14zM20 20l-3.5-3.5" /></svg></span>
        <input aria-label={t('masterItems.search')} placeholder={t('masterItems.search')} value={q} onChange={(e) => setQ(e.target.value)} className={`${input} w-full bg-[#f6faf9] pl-10`} />
      </span>
      <div className="flex flex-wrap items-end gap-3">
        <label className="flex flex-col gap-1 text-[11px] font-bold tracking-wide text-muted uppercase">{t('masterItems.unit')}
          <select value={unitFilter} onChange={(e) => setUnitFilter(e.target.value)} className="h-10 rounded-xl border border-line bg-white px-2 text-sm font-medium normal-case text-ink">
            <option value="">{t('plat6.all')}</option>{[...new Set(rows.map((r) => r.unit))].map((u) => <option key={u} value={u}>{t(`units.${u}`)}</option>)}
          </select></label>
        <label className="flex flex-col gap-1 text-[11px] font-bold tracking-wide text-muted uppercase">GST
          <select value={gstFilter} onChange={(e) => setGstFilter(e.target.value)} className="h-10 rounded-xl border border-line bg-white px-2 text-sm font-medium normal-case text-ink">
            <option value="">{t('plat6.all')}</option><option value="none">{t('plat5.gstNotSet')}</option>{['0', '5', '18', '40'].map((g) => <option key={g} value={g}>{g}%</option>)}
          </select></label>
        <label className="flex min-h-10 items-center gap-2 text-sm font-semibold"><input type="checkbox" checked={noPhotoOnly} onChange={(e) => setNoPhotoOnly(e.target.checked)} className="h-5 w-5" />{t('plat6.noPhotoOnly')}</label>
        {(unitFilter || gstFilter || noPhotoOnly) && <span className="text-sm text-muted">{t('plat6.showing', { shown: view.length, total: rows.length })}</span>}
      </div>
      {(adding || editing !== null) && <div className="grid grid-cols-2 gap-2 rounded-xl bg-ground p-2 ring-2 ring-brand/30">
        <input aria-label={t('masterItems.name')} placeholder={t('masterItems.name')} value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} className={input} />
        <input aria-label={t('masterItems.nameHi')} placeholder={t('masterItems.nameHi')} value={draft.nameHi} onChange={(e) => setDraft({ ...draft, nameHi: e.target.value })} className={input} />
        <input aria-label={t('masterItems.category')} placeholder={t('masterItems.category')} value={draft.category} onChange={(e) => setDraft({ ...draft, category: e.target.value })} className={input} />
        <select aria-label={t('masterItems.unit')} value={draft.unit} onChange={(e) => setDraft({ ...draft, unit: e.target.value })} className={input}>
          {['piece', 'kg', 'g', 'litre', 'ml', 'packet', 'pair', 'dozen', 'box', 'jar', 'bag', 'tonne', 'brass', 'cft', 'metre', 'sq_ft', 'running_ft', 'quintal', 'carton', 'bottle', 'set', 'strip'].map((u) => <option key={u} value={u}>{t(`units.${u}`)}</option>)}
        </select>
        <select aria-label={t('masterItems.gst')} value={draft.gst} onChange={(e) => setDraft({ ...draft, gst: e.target.value })} className={input}>
          <option value="">{t('masterItems.gst')}: –</option>{['0', '5', '18', '40'].map((r) => <option key={r} value={r}>{r}%</option>)}
        </select>
        <input aria-label={t('masterItems.hsn')} placeholder={t('masterItems.hsn')} value={draft.hsn} onChange={(e) => setDraft({ ...draft, hsn: e.target.value })} inputMode="numeric" maxLength={8} className={input} />
        <input aria-label={t('masterItems.packs')} placeholder={t('masterItems.packs')} value={draft.packs} onChange={(e) => setDraft({ ...draft, packs: e.target.value })} className={`${input} col-span-2`} />
        <Button className="col-span-2" disabled={draft.name.trim().length < 2 || draft.category.trim().length < 2} onClick={() => void save()}>{editing ? t('save') : t('masterItems.add')}</Button>
      </div>}
      <SuggestionsQueue onDone={() => void queryClient.invalidateQueries({ queryKey: ['platform', 'master'] })} />
      {importing && <div className="flex flex-col gap-2 rounded-xl bg-ground p-3 ring-2 ring-brand/30">
        <b>{t('masterItems.import')}</b>
        <p className="text-xs text-muted">{t('masterItems.importHint')}</p>
        <textarea value={csv} onChange={(e) => setCsv(e.target.value)} rows={6} placeholder={'Toor dal, तूर दाल, Dal, kg, 5, 0713, 500 g|1 kg'} className="mt-2 w-full rounded-xl border border-line p-2 font-mono text-sm" />
        <Button disabled={!csv.trim()} onClick={() => void importCsv()}>{t('masterItems.importButton')}</Button>
      </div>}
      {message && <p className="whitespace-pre-line text-sm text-due">{message}</p>}
      <label className="flex flex-col gap-1 text-xs text-muted">{t('masterItems.creditLabel')}
        <input value={photoCredit} onChange={(e) => setPhotoCredit(e.target.value)} maxLength={120} placeholder="Unsplash: name" className="h-10 rounded-xl border border-line bg-white px-3 text-sm text-ink" />
      </label>
      <div className="hidden max-h-[40rem] overflow-auto rounded-xl border border-line lg:block">
        <table className="w-full min-w-[860px] border-collapse text-sm">
          <thead className="sticky top-0 bg-[#f6faf9] text-left text-[11px] tracking-wide text-muted uppercase">
            <tr><th className="w-16 px-3 py-2.5">{t('plat5.photo')}</th><th className="px-2 py-2.5">{t('plat5.item')}</th><th className="px-2 py-2.5">{t('masterItems.category')}</th><th className="px-2 py-2.5">{t('masterItems.unit')}</th><th className="px-2 py-2.5">{t('plat5.gstHsn')}</th><th className="px-2 py-2.5">{t('plat5.sizes')}</th><th className="px-3 py-2.5 text-right" /></tr>
          </thead>
          <tbody>
            {view.map((r) => (
              <tr key={r.id} className={`border-t border-line ${r.isActive ? '' : 'opacity-50'} ${editing === r.id ? 'bg-[#eef7f4]' : 'hover:bg-[#f6faf9]'}`}>
                <td className="px-3 py-1.5"><MasterPhoto row={r} run={run} credit={photoCredit} /></td>
                <td className="px-2 py-1.5"><span className="flex flex-col"><b>{r.name}</b>{r.nameHi && <span className="text-xs text-muted">{r.nameHi}</span>}</span></td>
                <td className="px-2 py-1.5"><span className="rounded-full bg-[#edf2f1] px-2.5 py-0.5 text-xs font-semibold">{r.category}</span></td>
                <td className="px-2 py-1.5">{t(`units.${r.unit}`)}</td>
                <td className="px-2 py-1.5">{r.gstRate == null
                  ? <span className="rounded-full bg-warn-soft px-2.5 py-0.5 text-xs font-bold text-warn-ink">{t('plat5.gstNotSet')}{r.hsnCode ? ` · ${r.hsnCode}` : ''}</span>
                  : <span>{r.gstRate}%{r.hsnCode ? ` · ${r.hsnCode}` : ''}</span>}</td>
                <td className="px-2 py-1.5 text-muted">{r.packSizes.join(' / ') || '—'}</td>
                <td className="px-3 py-1.5 text-right whitespace-nowrap">
                  <button onClick={() => edit(r)} className="mr-1.5 min-h-9 rounded-xl border border-line px-3 text-sm font-semibold">{t('masterItems.edit')}</button>
                  <button onClick={() => void run(() => api(`/v1/platform/master-items/${r.id}`, 'PUT', { ...body({ name: r.name, nameHi: r.nameHi ?? '', category: r.category, unit: r.unit, gst: r.gstRate == null ? '' : String(r.gstRate), hsn: r.hsnCode ?? '', packs: r.packSizes.join(', ') }), isActive: !r.isActive }))}
                    className={`min-h-9 text-sm ${r.isActive ? btn.dangerOutline : `rounded-xl px-3 ${btn.solid}`}`}>{r.isActive ? t('masterItems.off') : t('masterItems.on')}</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <ul className="flex max-h-[32rem] flex-col overflow-y-auto lg:hidden">
        {view.map((r) => (
          <li key={r.id} className={`flex items-center gap-2 border-t border-line py-1 text-sm ${r.isActive ? '' : 'opacity-50'}`}>
            <MasterPhoto row={r} run={run} credit={photoCredit} />
            <span className="min-w-0 flex-1 truncate">{r.name}{r.nameHi ? ` · ${r.nameHi}` : ''}<span className="block text-xs text-muted">{r.category} · {t(`units.${r.unit}`)}{r.gstRate != null ? ` · ${r.gstRate}%` : ''}{r.hsnCode ? ` · ${r.hsnCode}` : ''}{r.packSizes.length ? ` · ${r.packSizes.join(' / ')}` : ''}</span></span>
            <button onClick={() => edit(r)} className="min-h-11 px-2 font-semibold text-brand">{t('masterItems.edit')}</button>
            <button onClick={() => void run(() => api(`/v1/platform/master-items/${r.id}`, 'PUT', { ...body({ name: r.name, nameHi: r.nameHi ?? '', category: r.category, unit: r.unit, gst: r.gstRate == null ? '' : String(r.gstRate), hsn: r.hsnCode ?? '', packs: r.packSizes.join(', ') }), isActive: !r.isActive }))}
              className="min-h-11 px-2 font-semibold">{r.isActive ? t('masterItems.off') : t('masterItems.on')}</button>
          </li>
        ))}
      </ul>
    </section>
  )
}

/**
 * A master item's photo: every shop item picked from it shows this until the shop adds its own. Only photos the
 * platform owns or is licensed for go here (own shoot, free stock, credited CC); the credit shows under it in shops.
 */
function MasterPhoto({ row, run, credit }: { row: MasterRow; run: (action: () => Promise<unknown>) => Promise<void>; credit: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
  // The photo just sent, shown at once: the browser can hold back the first load of the server's new copy for a long time.
  const [preview, setPreview] = useState<string | null>(null)
  const url = preview ?? (row.imageAt ? `/api/v1/master-images/${row.id}?v=${Date.parse(row.imageAt)}&size=thumb` : null)
  // Kept under the server's 1 MB request limit once sent (photos grow by a third as text): a 1200 px WebP of a pack is a few hundred KB.
  async function upload(file: File) {
    setBusy(true)
    setError(null)
    setSaved(false)
    try {
      // Step times go to the console, so a slow upload shows which step waited (reading the file, or the server).
      const started = performance.now()
      const dataUrl = await shrinkPhoto(file, 1200, 600_000, false)
      const shrunk = performance.now()
      await api(`/v1/platform/master-items/${row.id}/image`, 'PUT', { dataUrl, credit: credit.trim() || null })
      console.info(`[master photo] read+shrink ${Math.round(shrunk - started)} ms, upload ${Math.round(performance.now() - shrunk)} ms, ${file.size} bytes in, ${Math.round(dataUrl.length * 0.75)} bytes sent`)
      setPreview(dataUrl)
      setSaved(true)
      setTimeout(() => setSaved(false), 4000)
      // Refresh the list in the background so the new photo shows; a failed refresh is not a failed upload.
      void queryClient.invalidateQueries({ queryKey: ['platform', 'master'] })
    } catch (e) {
      setError(t('masterItems.photoFailed', { reason: shownError(e) }))
    } finally {
      setBusy(false)
    }
  }
  return (
    <span className="flex w-14 shrink-0 flex-col items-center">
      <label className={`flex h-12 w-12 cursor-pointer items-center justify-center overflow-hidden rounded-lg border border-line bg-white text-lg ${busy ? 'opacity-50' : ''}`} title={row.imageCredit ?? t('masterItems.photo')}>
        {busy ? '…' : url ? <img src={url} alt="" className="h-full w-full object-contain" /> : '📷'}
        <input type="file" accept="image/*" className="sr-only" aria-label={t('masterItems.photo')} disabled={busy}
          onChange={(e) => { const file = e.target.files?.[0]; if (file) void upload(file); e.target.value = '' }} />
      </label>
      {url && !busy && <button onClick={() => { setPreview(null); void run(() => request(`/v1/platform/master-items/${row.id}/image`, 'DELETE')) }} className="text-[10px] text-muted underline">{t('masterItems.removePhoto')}</button>}
      {saved && <span role="status" className="mt-0.5 text-center text-[11px] font-semibold leading-tight text-brand">✓ {t('masterItems.photoSaved')}</span>}
      {error && <span role="alert" className="mt-0.5 text-center text-[11px] font-semibold leading-tight text-due">{error}</span>}
    </span>
  )
}

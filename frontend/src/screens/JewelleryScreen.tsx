import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { CustomerFinder } from '../components/CustomerFinder'
import { Button, Drawer, btn } from '../components/ui'
import { api } from '../lib/api'
import { addCustomer, useCustomers } from '../lib/data'
import { formatRupees } from '../lib/format'
import { GOLD_PURITIES, SILVER_PURITIES, grams, karat, type JewelleryBill, type JewelleryPiece, type JewelleryQuote, type MakingType, type Metal, type MetalRates } from '../lib/jewellery'
import { navigate } from '../router'
import { ApprovalsPanel, GirviPanel, OrdersPanel, SchemesPanel, type Member } from './JewelleryBookPanels'

type Tab = 'bill' | 'stock' | 'rates' | 'girvi' | 'schemes' | 'orders' | 'approvals'
const icon = (d: string, size = 18) => <svg aria-hidden="true" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d={d} /></svg>
const box = 'h-11 min-w-0 rounded-xl border border-line bg-white px-3 outline-none focus:border-brand-line'
const num = (v: string) => v.replace(/[^\d.]/g, '')

/**
 * The jewellery counter: today's gold and silver rates, tagged pieces in stock, and the bill worked out by weight and
 * purity (with old gold taken in, PAN from ₹2 lakh and the ₹2 lakh-a-day cash limit). The bill is an ordinary GST
 * bill underneath (3% on the whole value, HSN 7113), so khata, cash book and reports carry on as usual.
 */
export function JewelleryScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const rates = useQuery({ queryKey: [shopId, 'jewellery', 'rates'], queryFn: () => api<MetalRates>('/v1/jewellery/rates') })
  const [tab, setTab] = useState<Tab | null>(null)
  const shown: Tab = tab ?? (rates.data && rates.data.current.length === 0 ? 'rates' : 'bill')
  const tabs: { key: Tab; label: string; d: string; on: string; off: string }[] = [
    { key: 'bill', label: t('jwl.newBill'), d: 'M6 2h12v20l-3-2-3 2-3-2-3 2zM9 7h6M9 11h6M9 15h4', on: 'bg-[#0b5e4f] text-white shadow-lg shadow-[#0b5e4f]/30', off: 'bg-[#e3f2ee] text-[#0b5e4f] hover:bg-[#d2ebe4]' },
    { key: 'stock', label: t('jwl.stock'), d: 'M3 7l9-4 9 4v10l-9 4-9-4zM3 7l9 4 9-4M12 11v10', on: 'bg-[#6d28d9] text-white shadow-lg shadow-[#6d28d9]/30', off: 'bg-[#f1eafd] text-[#6d28d9] hover:bg-[#e7dcfb]' },
    { key: 'rates', label: t('jwl.rates'), d: 'M3 17l6-6 4 4 8-8M15 7h6v6', on: 'bg-[#a1460a] text-white shadow-lg shadow-[#a1460a]/30', off: 'bg-[#fdf1e3] text-[#a1460a] hover:bg-[#fbe6cc]' },
    { key: 'orders', label: t('jwl2.tabOrders'), d: 'M9 3h6l1 3H8zM5 6h14v15H5zM9 11h6M9 15h4', on: 'bg-[#1d4ed8] text-white shadow-lg shadow-[#1d4ed8]/30', off: 'bg-[#e8effd] text-[#1d4ed8] hover:bg-[#dbe6fc]' },
    { key: 'schemes', label: t('jwl2.tabSchemes'), d: 'M4 7h16v12H4zM4 11h16M8 3v4M16 3v4', on: 'bg-[#0b5e4f] text-white shadow-lg shadow-[#0b5e4f]/30', off: 'bg-[#e3f2ee] text-[#0b5e4f] hover:bg-[#d2ebe4]' },
    { key: 'girvi', label: t('jwl2.tabGirvi'), d: 'M5 11h14v10H5zM8 11V7a4 4 0 0 1 8 0v4', on: 'bg-[#be123c] text-white shadow-lg shadow-[#be123c]/30', off: 'bg-[#fde8ec] text-[#be123c] hover:bg-[#fbd5dd]' },
    { key: 'approvals', label: t('jwl2.tabApprovals'), d: 'M14 3H6v18h12V7zM14 3v4h4M9 13l2 2 4-4', on: 'bg-[#6d28d9] text-white shadow-lg shadow-[#6d28d9]/30', off: 'bg-[#f1eafd] text-[#6d28d9] hover:bg-[#e7dcfb]' },
  ]
  const gold22 = rates.data?.current.find((r) => r.metal === 'gold' && r.purity === 916)
  const silver = rates.data?.current.find((r) => r.metal === 'silver' && r.purity === 999)

  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="mr-auto text-xl font-extrabold">{t('jwl.title')}</h1>
        {gold22 && <span className="rounded-full bg-[#fdf1e3] px-3 py-1 text-sm font-bold text-[#a1460a]">22K {formatRupees(gold22.sellPerGram * 10)}/10 g</span>}
        {silver && <span className="rounded-full bg-[#edf2f1] px-3 py-1 text-sm font-bold">{t('jwl.silver')} {formatRupees(silver.sellPerGram * 1000)}/kg</span>}
      </div>
      <div role="tablist" className="grid grid-cols-2 gap-2 sm:grid-cols-4 xl:grid-cols-7">
        {tabs.map((x) => (
          <button key={x.key} role="tab" aria-selected={shown === x.key} onClick={() => setTab(x.key)}
            className={`flex min-h-[52px] items-center justify-center gap-2.5 rounded-2xl px-3 text-sm font-bold transition active:scale-[0.98] ${shown === x.key ? x.on : x.off}`}>
            <span aria-hidden className={`flex h-[30px] w-[30px] shrink-0 items-center justify-center rounded-full ${shown === x.key ? 'bg-white/20' : 'bg-white'}`}>{icon(x.d)}</span>{x.label}
          </button>
        ))}
      </div>
      {shown === 'rates' && <RatesPanel shopId={shopId} isOwner={isOwner} rates={rates.data} />}
      {shown === 'stock' && <StockPanel shopId={shopId} isOwner={isOwner} />}
      {shown === 'bill' && <BillPanel shopId={shopId} />}
      {shown === 'girvi' && <GirviPanel shopId={shopId} isOwner={isOwner} />}
      {shown === 'schemes' && <SchemesPanel shopId={shopId} isOwner={isOwner} />}
      {shown === 'orders' && <OrdersPanel shopId={shopId} />}
      {shown === 'approvals' && <ApprovalsPanel shopId={shopId} />}
    </div>
  )
}

/* ---------------- rates ---------------- */

/** Gold per 10 g, silver per kg, as the trade quotes them; stored per gram. Purities left empty come from 24K. */
function RatesPanel({ shopId, isOwner, rates }: { shopId: string; isOwner: boolean; rates: MetalRates | undefined }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const rows: { metal: Metal; purity: number }[] = [...GOLD_PURITIES.map((p) => ({ metal: 'gold' as Metal, purity: p })), ...SILVER_PURITIES.map((p) => ({ metal: 'silver' as Metal, purity: p }))]
  const per = (m: Metal) => (m === 'gold' ? 10 : 1000)
  const [draft, setDraft] = useState<Record<string, { sell: string; buy: string }>>({})
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const key = (m: Metal, p: number) => `${m}-${p}`
  const current = (m: Metal, p: number) => rates?.current.find((r) => r.metal === m && r.purity === p)

  async function save() {
    const list = rows.flatMap(({ metal, purity }) => {
      const d = draft[key(metal, purity)]
      if (!d || !Number(d.sell)) return []
      return [{ metal, purity, sellPerGram: Number(d.sell) / per(metal), buyPerGram: Number(d.buy) ? Number(d.buy) / per(metal) : null }]
    })
    if (list.length === 0) return
    setBusy(true)
    setMessage('')
    try {
      await api('/v1/jewellery/rates', 'POST', { rates: list })
      await queryClient.invalidateQueries({ queryKey: [shopId, 'jewellery', 'rates'] })
      setDraft({})
      setMessage(t('jwl.ratesSaved'))
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="flex flex-col gap-3 rounded-card bg-white p-4 shadow-sm">
      <p className="text-sm text-muted">{t('jwl.ratesHint')}</p>
      <div className="overflow-x-auto rounded-xl border border-line">
        <table className="w-full min-w-[560px] border-collapse text-sm">
          <thead className="bg-[#f6faf9] text-left text-[11px] tracking-wide text-muted uppercase">
            <tr><th className="px-3 py-2.5">{t('jwl.purity')}</th><th className="px-2 py-2.5">{t('jwl.sellRate')}</th><th className="px-2 py-2.5">{t('jwl.buyRate')}</th><th className="px-3 py-2.5 text-right">{t('jwl.now')}</th></tr>
          </thead>
          <tbody>
            {rows.map(({ metal, purity }) => {
              const c = current(metal, purity)
              const d = draft[key(metal, purity)] ?? { sell: '', buy: '' }
              return (
                <tr key={key(metal, purity)} className="border-t border-line">
                  <td className="px-3 py-2"><b>{metal === 'gold' ? `${karat(metal, purity)} · ${purity}` : `${t('jwl.silver')} ${purity}`}</b><span className="block text-xs text-muted">{metal === 'gold' ? t('jwl.per10g') : t('jwl.perKg')}</span></td>
                  <td className="px-2 py-2"><input disabled={!isOwner} value={d.sell} onChange={(e) => setDraft({ ...draft, [key(metal, purity)]: { ...d, sell: num(e.target.value) } })} inputMode="decimal"
                    placeholder={c ? String(Math.round(c.sellPerGram * per(metal))) : purity === 999 ? t('jwl.enterRate') : t('jwl.from24k')} aria-label={`${t('jwl.sellRate')} ${karat(metal, purity)}`} className={`${box} w-36`} /></td>
                  <td className="px-2 py-2"><input disabled={!isOwner} value={d.buy} onChange={(e) => setDraft({ ...draft, [key(metal, purity)]: { ...d, buy: num(e.target.value) } })} inputMode="decimal"
                    placeholder={c?.buyPerGram ? String(Math.round(c.buyPerGram * per(metal))) : '—'} aria-label={`${t('jwl.buyRate')} ${karat(metal, purity)}`} className={`${box} w-36`} /></td>
                  <td className="px-3 py-2 text-right">{c ? <><b>{formatRupees(c.sellPerGram)}</b><span className="text-xs text-muted">/g</span>{c.derived && <span className="block text-[11px] text-muted">{t('jwl.fromPure')}</span>}</> : <span className="text-muted">—</span>}</td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
      {isOwner
        ? <Button className="self-start px-8" busy={busy} onClick={() => void save()}>{t('jwl.saveRates')}</Button>
        : <p className="text-sm text-muted">{t('jwl.ownerSetsRates')}</p>}
      {message && <p role="status" className="text-sm font-semibold text-brand-deep">{message}</p>}
      {(rates?.today.length ?? 0) > 0 && (
        <div className="flex flex-col gap-1">
          <b className="text-sm">{t('jwl.todayChanges')}</b>
          {rates!.today.map((r, i) => (
            <span key={i} className="text-sm text-muted">{new Date(r.setAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })} · {r.metal === 'gold' ? karat(r.metal, r.purity) : `${t('jwl.silver')} ${r.purity}`} · {formatRupees(r.sellPerGram)}/g</span>
          ))}
        </div>
      )}
    </section>
  )
}

/* ---------------- stock ---------------- */

interface PieceDraft { name: string; category: string; metal: Metal; purity: number; gross: string; stone: string; wastage: string; makingType: MakingType; making: string; stoneValue: string; hallmark: string; huid: string; fixed: string }
const blankPiece: PieceDraft = { name: '', category: '', metal: 'gold', purity: 916, gross: '', stone: '', wastage: '', makingType: 'per_gram', making: '', stoneValue: '', hallmark: '', huid: '', fixed: '' }

function StockPanel({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<'in' | 'sold'>('in')
  const [q, setQ] = useState('')
  const pieces = useQuery({ queryKey: [shopId, 'jewellery', 'pieces', status, q], queryFn: () => api<JewelleryPiece[]>(`/v1/jewellery/pieces?status=${status === 'in' ? 'in' : 'sold'}&q=${encodeURIComponent(q.trim())}`) })
  const [editing, setEditing] = useState<{ id: string | null; draft: PieceDraft } | null>(null)
  const [picked, setPicked] = useState<string[]>([])
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const list = pieces.data ?? []
  const totals = useMemo(() => ({ count: list.length, gold: list.filter((p) => p.metal === 'gold').reduce((n, p) => n + p.net, 0), silver: list.filter((p) => p.metal === 'silver').reduce((n, p) => n + p.net, 0) }), [list])

  async function save() {
    if (!editing) return
    const d = editing.draft
    setBusy(true)
    setError('')
    try {
      const body = { name: d.name, category: d.category || null, metal: d.metal, purity: d.purity, gross: Number(d.gross), stone: Number(d.stone) || 0, wastagePercent: Number(d.wastage) || 0,
        makingType: d.makingType, makingValue: Number(d.making) || 0, stoneValue: Number(d.stoneValue) || 0, hallmarkCharge: Number(d.hallmark) || 0, huid: d.huid || null, fixedPrice: Number(d.fixed) || null }
      if (editing.id) await api(`/v1/jewellery/pieces/${editing.id}`, 'PUT', body)
      else await api('/v1/jewellery/pieces', 'POST', body)
      await queryClient.invalidateQueries({ queryKey: [shopId, 'jewellery', 'pieces'] })
      // A new piece: keep the form for the next one, same metal, purity and making.
      setEditing(editing.id ? null : { id: null, draft: { ...d, name: '', gross: '', stone: '', huid: '', stoneValue: '' } })
    } catch (e) {
      setError(e instanceof Error ? e.message : t('error'))
    } finally {
      setBusy(false)
    }
  }
  const toDraft = (p: JewelleryPiece): PieceDraft => ({ name: p.name, category: p.category ?? '', metal: p.metal, purity: p.purity, gross: String(p.gross), stone: p.stone ? String(p.stone) : '', wastage: p.wastagePercent ? String(p.wastagePercent) : '',
    makingType: p.makingType, making: p.makingValue ? String(p.makingValue) : '', stoneValue: p.stoneValue ? String(p.stoneValue) : '', hallmark: p.hallmarkCharge ? String(p.hallmarkCharge) : '', huid: p.huid ?? '', fixed: p.fixedPrice ? String(p.fixedPrice) : '' })
  const set = (k: keyof PieceDraft, v: string | number) => editing && setEditing({ ...editing, draft: { ...editing.draft, [k]: v } })
  const d = editing?.draft
  const net = d ? Math.max(0, (Number(d.gross) || 0) - (Number(d.stone) || 0)) : 0

  return (
    <div className="flex flex-col gap-3">
      <div className="grid grid-cols-3 gap-2">
        <div className="flex min-h-[76px] flex-col justify-between rounded-[18px] bg-[#f1eafd] p-3 text-[#6d28d9]"><span className="text-[13px] font-bold">{t('jwl.pieces')}</span><b className="text-2xl">{totals.count}</b></div>
        <div className="flex min-h-[76px] flex-col justify-between rounded-[18px] bg-[#fdf1e3] p-3 text-[#a1460a]"><span className="text-[13px] font-bold">{t('jwl.goldNet')}</span><b className="text-xl">{grams(totals.gold)}</b></div>
        <div className="flex min-h-[76px] flex-col justify-between rounded-[18px] bg-[#edf2f1] p-3"><span className="text-[13px] font-bold text-muted">{t('jwl.silverNet')}</span><b className="text-xl">{grams(totals.silver)}</b></div>
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <div className="flex gap-1 rounded-xl bg-[#edf2f1] p-1">
          {(['in', 'sold'] as const).map((s) => <button key={s} onClick={() => setStatus(s)} aria-pressed={status === s} className={`min-h-9 rounded-lg px-3 text-sm font-semibold ${status === s ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted'}`}>{t(`jwl.status.${s}`)}</button>)}
        </div>
        <input value={q} onChange={(e) => setQ(e.target.value)} placeholder={t('jwl.searchPieces')} aria-label={t('jwl.searchPieces')} className={`${box} flex-1 basis-48 bg-[#f6faf9]`} />
        {picked.length > 0 && <button onClick={() => navigate(`/jewellery/tags/${picked.join(',')}`)} className={`flex min-h-11 items-center gap-2 rounded-xl px-4 ${btn.outline}`}>{icon('M6 9V3h12v6M3 9h18v8H3zM6 14h12v7H6z', 16)}{t('jwl.printTags', { count: picked.length })}</button>}
        <button onClick={() => setEditing({ id: null, draft: blankPiece })} className={`min-h-11 rounded-xl px-4 ${btn.solid}`}>+ {t('jwl.addPiece')}</button>
      </div>
      {list.length === 0 && <p className="rounded-card bg-white p-6 text-center text-muted shadow-sm">{t('jwl.noPieces')}</p>}
      {list.length > 0 && (
        <div className="overflow-x-auto rounded-card bg-white shadow-sm">
          <table className="w-full min-w-[820px] border-collapse text-sm">
            <thead className="bg-[#f6faf9] text-left text-[11px] tracking-wide text-muted uppercase">
              <tr>
                <th className="w-10 px-3 py-2.5"><input type="checkbox" aria-label={t('jwl.tickAll')} className="h-5 w-5" checked={list.every((p) => picked.includes(p.id))} onChange={(e) => setPicked(e.target.checked ? list.map((p) => p.id) : [])} /></th>
                <th className="px-2 py-2.5">{t('jwl.tag')}</th><th className="px-2 py-2.5">{t('jwl.item')}</th><th className="px-2 py-2.5">{t('jwl.purity')}</th>
                <th className="px-2 py-2.5 text-right">{t('jwl.gross')}</th><th className="px-2 py-2.5 text-right">{t('jwl.stoneWt')}</th><th className="px-2 py-2.5 text-right">{t('jwl.net')}</th>
                <th className="px-2 py-2.5">{t('jwl.making')}</th><th className="px-2 py-2.5">HUID</th><th className="px-3 py-2.5" />
              </tr>
            </thead>
            <tbody>
              {list.map((p) => (
                <tr key={p.id} className={`border-t border-line ${picked.includes(p.id) ? 'bg-[#eef7f4]' : 'hover:bg-[#f6faf9]'}`}>
                  <td className="px-3 py-2"><input type="checkbox" aria-label={`${t('jwl.tag')} J-${p.tagNo}`} className="h-5 w-5" checked={picked.includes(p.id)} onChange={(e) => setPicked(e.target.checked ? [...picked, p.id] : picked.filter((x) => x !== p.id))} /></td>
                  <td className="px-2 py-2 font-mono font-bold">J-{p.tagNo}</td>
                  <td className="px-2 py-2"><span className="flex flex-col"><b>{p.name}</b>{p.category && <span className="text-xs text-muted">{p.category}</span>}{p.onApproval && <span className="mt-0.5 self-start rounded-full bg-[#f1eafd] px-2 text-xs font-bold text-[#6d28d9]">{t('jwl2.onApproval')}</span>}</span></td>
                  <td className="px-2 py-2"><span className={`rounded-full px-2.5 py-0.5 text-xs font-bold ${p.metal === 'gold' ? 'bg-[#fdf1e3] text-[#a1460a]' : 'bg-[#edf2f1] text-[#33423d]'}`}>{p.metal === 'gold' ? karat(p.metal, p.purity) : `${t('jwl.silver')} ${p.purity}`}</span></td>
                  <td className="px-2 py-2 text-right tabular-nums">{p.gross.toFixed(3)}</td>
                  <td className="px-2 py-2 text-right tabular-nums text-muted">{p.stone ? p.stone.toFixed(3) : '—'}</td>
                  <td className="px-2 py-2 text-right font-bold tabular-nums">{p.net.toFixed(3)}</td>
                  <td className="px-2 py-2 text-muted">{p.fixedPrice ? formatRupees(p.fixedPrice) : makingLabel(t, p.makingType, p.makingValue)}{p.wastagePercent ? ` · ${t('jwl.wastageShort', { value: p.wastagePercent })}` : ''}</td>
                  <td className="px-2 py-2">{p.huid ? <span className="font-mono text-xs">{p.huid}</span> : p.metal === 'gold' && !p.fixedPrice ? <span className="rounded-full bg-warn-soft px-2 text-xs font-bold text-warn-ink">{t('jwl.noHuid')}</span> : '—'}</td>
                  <td className="px-3 py-2 text-right whitespace-nowrap">{!p.sold && <button onClick={() => setEditing({ id: p.id, draft: toDraft(p) })} className="min-h-9 rounded-xl border border-line px-3 text-sm font-semibold">{t('jwl.edit')}</button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {editing && d && (
        <Drawer title={editing.id ? t('jwl.editPiece') : t('jwl.addPiece')} onClose={() => setEditing(null)}>
          <label className="flex flex-col gap-1 text-sm font-semibold">{t('jwl.name')}<input autoFocus value={d.name} onChange={(e) => set('name', e.target.value)} maxLength={80} placeholder={t('jwl.namePlaceholder')} className={box} /></label>
          <label className="flex flex-col gap-1 text-sm font-semibold">{t('jwl.category')}<input value={d.category} onChange={(e) => set('category', e.target.value)} maxLength={60} placeholder={t('jwl.categoryPlaceholder')} className={box} /></label>
          <div className="grid grid-cols-2 gap-2">
            <label className="flex flex-col gap-1 text-sm font-semibold">{t('jwl.metal')}
              <select value={d.metal} onChange={(e) => { const m = e.target.value as Metal; setEditing({ ...editing, draft: { ...d, metal: m, purity: m === 'gold' ? 916 : 925 } }) }} className={box}>
                <option value="gold">{t('jwl.gold')}</option><option value="silver">{t('jwl.silver')}</option>
              </select></label>
            <label className="flex flex-col gap-1 text-sm font-semibold">{t('jwl.purity')}
              <select value={d.purity} onChange={(e) => set('purity', Number(e.target.value))} className={box}>
                {(d.metal === 'gold' ? GOLD_PURITIES : SILVER_PURITIES).map((p) => <option key={p} value={p}>{d.metal === 'gold' ? `${karat('gold', p)} (${p})` : p}</option>)}
              </select></label>
            <label className="flex flex-col gap-1 text-sm font-semibold">{t('jwl.grossG')}<input value={d.gross} onChange={(e) => set('gross', num(e.target.value))} inputMode="decimal" placeholder="0.000" className={box} /></label>
            <label className="flex flex-col gap-1 text-sm font-semibold">{t('jwl.stoneG')}<input value={d.stone} onChange={(e) => set('stone', num(e.target.value))} inputMode="decimal" placeholder="0.000" className={box} /></label>
          </div>
          <p className="rounded-xl bg-[#f6faf9] px-3 py-2 text-sm">{t('jwl.netIs')} <b>{grams(net)}</b></p>
          <div className="grid grid-cols-2 gap-2">
            <label className="flex flex-col gap-1 text-sm font-semibold">{t('jwl.wastagePct')}<input value={d.wastage} onChange={(e) => set('wastage', num(e.target.value))} inputMode="decimal" placeholder="0" className={box} /></label>
            <label className="flex flex-col gap-1 text-sm font-semibold">{t('jwl.makingType')}
              <select value={d.makingType} onChange={(e) => set('makingType', e.target.value)} className={box}>
                {(['per_gram', 'percent', 'per_piece'] as const).map((m) => <option key={m} value={m}>{t(`jwl.makingKind.${m}`)}</option>)}
              </select></label>
            <label className="flex flex-col gap-1 text-sm font-semibold">{t('jwl.makingValue')}<input value={d.making} onChange={(e) => set('making', num(e.target.value))} inputMode="decimal" placeholder="0" className={box} /></label>
            <label className="flex flex-col gap-1 text-sm font-semibold">{t('jwl.stoneValue')}<input value={d.stoneValue} onChange={(e) => set('stoneValue', num(e.target.value))} inputMode="decimal" placeholder="0" className={box} /></label>
            <label className="flex flex-col gap-1 text-sm font-semibold">{t('jwl.hallmark')}<input value={d.hallmark} onChange={(e) => set('hallmark', num(e.target.value))} inputMode="decimal" placeholder="0" className={box} /></label>
            <label className="flex flex-col gap-1 text-sm font-semibold">HUID<input value={d.huid} onChange={(e) => set('huid', e.target.value.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 6))} placeholder="AB12CD" className={`${box} font-mono`} /></label>
          </div>
          <label className="flex flex-col gap-1 text-sm font-semibold">{t('jwl.fixedPrice')}<input value={d.fixed} onChange={(e) => set('fixed', num(e.target.value))} inputMode="decimal" placeholder={t('jwl.fixedPlaceholder')} className={box} />
            <span className="text-xs font-normal text-muted">{t('jwl.fixedHint')}</span></label>
          {error && <p className="text-sm font-semibold text-due">{error}</p>}
          <Button busy={busy} disabled={d.name.trim().length < 1 || !(Number(d.gross) > 0)} onClick={() => void save()}>{editing.id ? t('save') : t('jwl.savePiece')}</Button>
          {editing.id && isOwner && (
            <button className={`min-h-11 ${btn.dangerOutline}`} onClick={async () => { await api(`/v1/jewellery/pieces/${editing.id}`, 'DELETE'); await queryClient.invalidateQueries({ queryKey: [shopId, 'jewellery', 'pieces'] }); setEditing(null) }}>{t('jwl.removePiece')}</button>
          )}
        </Drawer>
      )}
    </div>
  )
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function makingLabel(t: (k: string, o?: any) => string, type: MakingType, value: number) {
  if (!value) return '—'
  return type === 'per_gram' ? t('jwl.makingPerG', { value: formatRupees(value) }) : type === 'percent' ? `${value}%` : t('jwl.makingPerPiece', { value: formatRupees(value) })
}

/* ---------------- bill ---------------- */

interface Line { pieceId: string | null; label: string; rate: string; making: string; wastage: string }
interface OldDraft { description: string; metal: Metal; gross: string; reading: string; deduction: string }

function BillPanel({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { customers } = useCustomers(shopId)
  const [customerId, setCustomerId] = useState<string | null>(null)
  const [lines, setLines] = useState<Line[]>([])
  const [olds, setOlds] = useState<OldDraft[]>([])
  const [tagText, setTagText] = useState('')
  const [mode, setMode] = useState<'cash' | 'upi' | 'card'>('cash')
  const [paid, setPaid] = useState('')
  const [pan, setPan] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [schemeMemberId, setSchemeMemberId] = useState<string | null>(null)
  const customer = customers.find((c) => c.id === customerId) ?? null
  // A finished savings scheme for this customer: its bonus comes off the making charges.
  const members = useQuery({ queryKey: [shopId, 'jewellery', 'members'], queryFn: () => api<Member[]>('/v1/jewellery/schemes/members'), enabled: customerId != null }).data ?? []
  const ready = members.filter((m) => m.customerId === customerId && m.complete && !m.redeemedBillId)
  useEffect(() => { setSchemeMemberId(null) }, [customerId])

  const body = {
    customerId,
    schemeMemberId,
    lines: lines.map((l) => ({ pieceId: l.pieceId, ratePerGram: Number(l.rate) || null, makingValue: l.making === '' ? null : Number(l.making), wastagePercent: l.wastage === '' ? null : Number(l.wastage) })),
    oldMetal: olds.filter((o) => Number(o.gross) > 0 && Number(o.reading) > 0).map((o) => ({ description: o.description || null, metal: o.metal, gross: Number(o.gross), reading: Number(o.reading), deductionPercent: Number(o.deduction) || 0 })),
  }
  const quote = useQuery({
    queryKey: [shopId, 'jewellery', 'quote', JSON.stringify(body)], enabled: lines.length > 0, retry: false,
    queryFn: () => api<JewelleryQuote>('/v1/jewellery/quote', 'POST', body),
  })
  const qd = quote.data
  const toPay = qd ? Math.max(0, qd.toPay) : 0
  // Money received defaults to what is left to pay; in cash, never more than the day's limit allows.
  useEffect(() => { setPaid(qd ? String(Math.max(0, qd.toPay - (qd.khataAdvance ?? 0))) : '') }, [qd?.toPay, qd?.khataAdvance]) // eslint-disable-line react-hooks/exhaustive-deps
  const cashOver = mode === 'cash' && qd != null && Number(paid) > qd.cashLimit

  async function addTag() {
    const text = tagText.trim()
    if (!text) return
    setError('')
    try {
      const found = await api<JewelleryPiece[]>(`/v1/jewellery/pieces?q=${encodeURIComponent(text)}`)
      const p = found.find((x) => !lines.some((l) => l.pieceId === x.id))
      if (!p) { setError(t('jwl.tagNotFound', { tag: text })); return }
      setLines([...lines, { pieceId: p.id, label: `J-${p.tagNo} · ${p.name}`, rate: '', making: '', wastage: '' }])
      setTagText('')
    } catch (e) {
      setError(e instanceof Error ? e.message : t('error'))
    }
  }

  async function save() {
    setBusy(true)
    setError('')
    try {
      const billId = crypto.randomUUID()
      const saved = await api<JewelleryBill>('/v1/jewellery/bills', 'POST', { ...body, billId, moneyPaid: Number(paid) || 0, mode, pan: pan.trim() || null })
      await queryClient.invalidateQueries({ queryKey: [shopId] })
      navigate(`/print/bill/${saved.billId}`)
    } catch (e) {
      setError(e instanceof Error ? e.message : t('error'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex flex-col gap-3 lg:grid lg:grid-cols-[minmax(0,1fr)_420px] lg:items-start">
      <div className="flex flex-col gap-3">
        <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
          <b>{t('jwl.addByTag')}</b>
          <form onSubmit={(e) => { e.preventDefault(); void addTag() }} className="flex gap-2">
            <input autoFocus value={tagText} onChange={(e) => setTagText(e.target.value)} placeholder={t('jwl.tagPlaceholder')} aria-label={t('jwl.addByTag')} className={`${box} flex-1 bg-[#f6faf9] font-mono`} />
            <button className={`min-h-11 rounded-xl px-4 ${btn.solid}`}>{t('jwl.add')}</button>
          </form>
          {error && <p className="text-sm font-semibold text-due">{error}</p>}
        </section>

        {qd && qd.lines.map((l, i) => (
          <section key={i} className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
            <div className="flex items-start gap-2">
              <span className="flex min-w-0 flex-1 flex-col"><b>{l.name}{l.tagNo ? <span className="ml-1.5 font-mono text-xs text-muted">J-{l.tagNo}</span> : null}</b>
                <span className="text-xs text-muted">{l.metal === 'gold' ? karat(l.metal, l.purity) : `${t('jwl.silver')} ${l.purity}`} · {t('jwl.gross')} {grams(l.gross)}{l.stone ? ` · ${t('jwl.stoneWt')} ${grams(l.stone)}` : ''} · {t('jwl.net')} <b>{grams(l.net)}</b>{l.huid ? ` · HUID ${l.huid}` : ''}</span></span>
              <button onClick={() => setLines(lines.filter((_, x) => x !== i))} aria-label={t('jwl.removeLine')} className="h-9 w-9 rounded-full text-xl text-muted hover:bg-[#f1f5f4]">×</button>
            </div>
            {!l.fixedPrice && (
              <div className="grid grid-cols-3 gap-2 text-xs">
                <label className="flex flex-col gap-1 font-semibold text-muted">{t('jwl.ratePerG')}<input value={lines[i]?.rate ?? ''} onChange={(e) => setLines(lines.map((x, n) => (n === i ? { ...x, rate: num(e.target.value) } : x)))} inputMode="decimal" placeholder={String(l.ratePerGram)} className={`${box} h-10 text-sm`} /></label>
                <label className="flex flex-col gap-1 font-semibold text-muted">{t('jwl.wastagePct')}<input value={lines[i]?.wastage ?? ''} onChange={(e) => setLines(lines.map((x, n) => (n === i ? { ...x, wastage: num(e.target.value) } : x)))} inputMode="decimal" placeholder={String(l.wastagePercent)} className={`${box} h-10 text-sm`} /></label>
                <label className="flex flex-col gap-1 font-semibold text-muted">{t(`jwl.makingKind.${l.makingType}`)}<input value={lines[i]?.making ?? ''} onChange={(e) => setLines(lines.map((x, n) => (n === i ? { ...x, making: num(e.target.value) } : x)))} inputMode="decimal" placeholder={String(l.makingValue)} className={`${box} h-10 text-sm`} /></label>
              </div>
            )}
            <div className="grid grid-cols-2 gap-x-4 gap-y-0.5 rounded-xl bg-[#f6faf9] p-3 text-sm sm:grid-cols-3">
              {!l.fixedPrice && <><span className="text-muted">{t('jwl.metalValue')}</span><span className="text-right sm:col-span-2">{formatRupees(l.metalValue)}</span></>}
              {l.wastageValue > 0 && <><span className="text-muted">{t('jwl.wastageValue', { value: l.wastagePercent })}</span><span className="text-right sm:col-span-2">{formatRupees(l.wastageValue)}</span></>}
              {l.makingAmount > 0 && <><span className="text-muted">{t('jwl.making')}</span><span className="text-right sm:col-span-2">{formatRupees(l.makingAmount)}</span></>}
              {l.stoneValue > 0 && <><span className="text-muted">{t('jwl.stoneValue')}</span><span className="text-right sm:col-span-2">{formatRupees(l.stoneValue)}</span></>}
              {l.hallmarkCharge > 0 && <><span className="text-muted">{t('jwl.hallmark')}</span><span className="text-right sm:col-span-2">{formatRupees(l.hallmarkCharge)}</span></>}
              <b>{t('jwl.taxable')}</b><b className="text-right sm:col-span-2">{formatRupees(l.taxable)}</b>
            </div>
          </section>
        ))}
        {lines.length > 0 && !qd && quote.isError && <p className="rounded-card bg-due-soft p-3 text-sm font-semibold text-due">{quote.error.message}</p>}

        <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
          <div className="flex items-center justify-between gap-2"><b>{t('jwl.oldMetal')}</b>
            <button onClick={() => setOlds([...olds, { description: '', metal: 'gold', gross: '', reading: '', deduction: '2' }])} className={`min-h-10 rounded-xl px-3 text-sm ${btn.add}`}>+ {t('jwl.addOld')}</button></div>
          {olds.length === 0 && <p className="text-sm text-muted">{t('jwl.oldHint')}</p>}
          {olds.map((o, i) => {
            const worked = qd?.oldMetal[i]
            const upd = (k: keyof OldDraft, v: string) => setOlds(olds.map((x, n) => (n === i ? { ...x, [k]: v } : x)))
            return (
              <div key={i} className="flex flex-col gap-2 rounded-xl bg-[#fdf1e3]/60 p-3">
                <div className="grid grid-cols-2 gap-2 sm:grid-cols-5">
                  <input value={o.description} onChange={(e) => upd('description', e.target.value)} placeholder={t('jwl.oldWhat')} aria-label={t('jwl.oldWhat')} className={`${box} col-span-2`} />
                  <select value={o.metal} onChange={(e) => upd('metal', e.target.value)} aria-label={t('jwl.metal')} className={box}><option value="gold">{t('jwl.gold')}</option><option value="silver">{t('jwl.silver')}</option></select>
                  <input value={o.gross} onChange={(e) => upd('gross', num(e.target.value))} inputMode="decimal" placeholder={t('jwl.grossG')} aria-label={t('jwl.grossG')} className={box} />
                  <input value={o.reading} onChange={(e) => upd('reading', num(e.target.value))} inputMode="decimal" placeholder={t('jwl.reading')} aria-label={t('jwl.reading')} className={box} />
                </div>
                <div className="flex flex-wrap items-center gap-2 text-sm">
                  <label className="flex items-center gap-2">{t('jwl.deduction')}<input value={o.deduction} onChange={(e) => upd('deduction', num(e.target.value))} inputMode="decimal" className={`${box} h-9 w-16`} />%</label>
                  {worked && <span className="text-muted">{t('jwl.fine')} <b className="text-ink">{grams(worked.fine)}</b> × {formatRupees(worked.ratePerGram)} − {formatRupees(worked.deduction)} = <b className="text-ink">{formatRupees(worked.value)}</b></span>}
                  <button onClick={() => setOlds(olds.filter((_, n) => n !== i))} className={`ml-auto min-h-9 text-sm ${btn.dangerOutline}`}>{t('jwl.remove')}</button>
                </div>
              </div>
            )
          })}
        </section>
      </div>

      <section className="flex flex-col gap-3 rounded-card bg-white p-4 shadow-sm lg:sticky lg:top-20">
        <CustomerFinder customers={customers} selected={customer} onKhata={0} onPick={setCustomerId} onCreate={(name, phone) => addCustomer(shopId, name, phone)} />
        {ready.length > 0 && (
          <label className="flex flex-col gap-1 rounded-xl bg-[#f1eafd] p-3 text-sm font-semibold text-[#6d28d9]">{t('jwl2.useScheme')}
            <select value={schemeMemberId ?? ''} onChange={(e) => setSchemeMemberId(e.target.value || null)} className={`${box} text-ink`}>
              <option value="">{t('jwl2.noScheme')}</option>
              {ready.map((m) => <option key={m.id} value={m.id}>{t('jwl2.schemeOption', { plan: m.plan, number: m.number, paid: formatRupees(m.paidTotal), bonus: formatRupees(m.bonus) })}</option>)}
            </select></label>
        )}
        {!qd && <p className="py-6 text-center text-muted">{t('jwl.emptyBill')}</p>}
        {qd && (
          <>
            <div className="flex flex-col gap-1 text-sm">
              {qd.schemeDiscount > 0 && <p className="flex justify-between font-semibold text-[#6d28d9]"><span>{t('jwl2.schemeBonus')}</span><span>−{formatRupees(qd.schemeDiscount)}</span></p>}
              <p className="flex justify-between"><span className="text-muted">{t('jwl.taxable')}</span><span>{formatRupees(qd.taxable)}</span></p>
              <p className="flex justify-between"><span className="text-muted">{t('jwl.gst3')}</span><span>{formatRupees(qd.gst)}</span></p>
              {qd.roundOff !== 0 && <p className="flex justify-between"><span className="text-muted">{t('jwl.roundOff')}</span><span>{qd.roundOff > 0 ? '+' : ''}{qd.roundOff.toFixed(2)}</span></p>}
              <p className="flex justify-between border-t border-line pt-1 text-lg font-extrabold"><span>{t('jwl.total')}</span><span>{formatRupees(qd.total)}</span></p>
              {qd.oldMetalValue > 0 && <p className="flex justify-between font-semibold text-[#a1460a]"><span>{t('jwl.lessOld')}</span><span>−{formatRupees(qd.oldMetalValue)}</span></p>}
              <p className={`flex justify-between rounded-xl px-3 py-2 text-lg font-extrabold ${qd.toPay < 0 ? 'bg-due-soft text-due' : 'bg-brand-soft text-brand-deep'}`}><span>{qd.toPay < 0 ? t('jwl.shopPays') : t('jwl.toPay')}</span><span>{formatRupees(Math.abs(qd.toPay))}</span></p>
              {qd.khataAdvance > 0 && qd.toPay > 0 && <p className="text-xs text-muted">{t('jwl2.advanceUsed', { amount: formatRupees(Math.min(qd.khataAdvance, qd.toPay)) })}</p>}
            </div>
            {qd.problems.map((p) => <p key={p} className="rounded-xl bg-warn-soft p-2 text-xs font-semibold text-warn-ink">{p}</p>)}
            {qd.toPay > 0 && <>
              <div role="radiogroup" aria-label={t('jwl.paidBy')} className="grid grid-cols-3 gap-1 rounded-2xl bg-[#edf2f1] p-1 text-sm">
                {(['cash', 'upi', 'card'] as const).map((m) => <button key={m} role="radio" aria-checked={mode === m} onClick={() => setMode(m)} className={`min-h-11 rounded-xl font-semibold transition ${mode === m ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted'}`}>{t(m)}</button>)}
              </div>
              <label className="flex items-center justify-between gap-2 text-sm font-semibold">{t('jwl.received')}
                <input value={paid} onChange={(e) => setPaid(num(e.target.value))} inputMode="decimal" className={`${box} w-40 text-right text-lg font-bold`} /></label>
              {cashOver && <p className="rounded-xl bg-due-soft p-2 text-sm font-semibold text-due">{t('jwl.cashOver', { amount: formatRupees(qd.cashLimit) })}</p>}
              {customer == null && Number(paid) < toPay && <p className="text-xs font-semibold text-due">{t('jwl.needCustomer')}</p>}
              {customer != null && Number(paid) < toPay - qd.khataAdvance && <p className="text-xs text-muted">{t('jwl.onKhata', { amount: formatRupees(toPay - qd.khataAdvance - (Number(paid) || 0)) })}</p>}
            </>}
            {qd.panNeeded && (
              <label className="flex flex-col gap-1 text-sm font-semibold">{t('jwl.pan')}
                <input value={pan} onChange={(e) => setPan(e.target.value.toUpperCase().slice(0, 10))} placeholder="ABCDE1234F" className={`${box} font-mono`} />
                <span className="text-xs font-normal text-muted">{t('jwl.panHint')}</span></label>
            )}
            <Button busy={busy} disabled={cashOver || (qd.panNeeded && pan.trim().length < 7) || (customer == null && Number(paid) < toPay)} onClick={() => void save()} className="h-14 text-lg">
              {t('jwl.saveBill', { amount: formatRupees(qd.total) })}
            </Button>
          </>
        )}
      </section>
    </div>
  )
}

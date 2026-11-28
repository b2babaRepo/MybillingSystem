import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Drawer } from '../components/ui'
import { btn } from '../lib/buttonStyles'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { grams, type JewelleryPiece, type Metal } from '../lib/jewellery'
import { canReadScale, readScaleGrams } from '../lib/scale'
import { useScales, blankStone } from '../lib/jewelleryWorkshop'
import { useNow } from '../lib/useNow'
import { shownError } from '../lib/notify'

// Phase 3 of the jewellery counter: karigar and trader metal accounts in fine grams, the shop's loose metal, tray
// counts, the counter scales, and the stones set in a piece.

const box = 'h-11 min-w-0 rounded-xl border border-line bg-white px-3 outline-none focus:border-brand-line'
const num = (v: string) => v.replace(/[^\d.]/g, '')
const when = (iso: string) => new Date(iso).toLocaleString([], { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })
const date = (d: string) => new Date(`${d}T00:00:00`).toLocaleDateString([], { day: 'numeric', month: 'short', year: 'numeric' })

function Card({ tone, label, value, sub }: { tone: string; label: string; value: ReactNode; sub?: ReactNode }) {
  return <div className={`flex min-h-[76px] flex-col justify-between rounded-[18px] p-3 ${tone}`}><span className="text-[13px] font-bold">{label}</span><b className="text-xl">{value}</b>{sub && <span className="text-xs opacity-80">{sub}</span>}</div>
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return <label className="flex flex-col gap-1 text-sm font-semibold">{label}{children}</label>
}

function useRun() {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  async function run(action: () => Promise<unknown>) {
    setBusy(true)
    setError('')
    try { await action(); return true } catch (e) { setError(shownError(e)); return false } finally { setBusy(false) }
  }
  return { busy, error, run }
}

/* ---------------- scales ---------------- */

export interface Scale { id: string; name: string; readsTo: number; stampedUntil: string | null; baud: number; isActive: boolean; daysLeft: number }

/** "Weigh" beside a weight box: reads the counter scale (Chrome on a computer) and rounds to what it is stamped for. */
export function WeighButton({ shopId, onWeight }: { shopId: string; onWeight: (grams: string) => void }) {
  const { t } = useTranslation()
  const scale = useScales(shopId).find((s) => s.isActive)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  if (!canReadScale()) return null
  async function weigh(choose = false) {
    setBusy(true)
    setError('')
    try { onWeight(String(await readScaleGrams(scale?.readsTo ?? 0.001, scale?.baud ?? 9600, choose))) } catch (e) {
      if (!(e instanceof Error && e.name === 'NotFoundError')) setError(shownError(e))
    } finally { setBusy(false) }
  }
  return (
    <span className="flex flex-wrap items-center gap-2">
      <button type="button" disabled={busy} onClick={() => void weigh()} className="flex min-h-9 items-center gap-1.5 rounded-xl bg-[#e8effd] px-3 text-sm font-bold text-[#1d4ed8] hover:bg-[#dbe6fc] disabled:opacity-50">
        <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M12 3v18M5 21h14M5 7h14M5 7l-3 7a3 3 0 006 0zM19 7l-3 7a3 3 0 006 0z" /></svg>
        {busy ? t('scale.reading') : t('jwl3.weigh')}
      </button>
      <button type="button" disabled={busy} onClick={() => void weigh(true)} className={`min-h-9 text-xs ${btn.link}`}>{t('scale.change')}</button>
      {error && <span className="text-xs font-semibold text-due">{error}</span>}
    </span>
  )
}

/* ---------------- stones on a piece ---------------- */

export interface StoneDraft { kind: string; labGrown: boolean; count: string; carat: string; shape: string; colour: string; clarity: string; certLab: string; certNo: string; ratePerCarat: string }
export function StonesEditor({ stones, onChange }: { stones: StoneDraft[]; onChange: (s: StoneDraft[]) => void }) {
  const { t } = useTranslation()
  const upd = (i: number, k: keyof StoneDraft, v: string | boolean) => onChange(stones.map((s, n) => (n === i ? { ...s, [k]: v } : s)))
  const carats = stones.reduce((n, s) => n + (Number(s.carat) || 0), 0)
  const value = stones.reduce((n, s) => n + (Number(s.carat) || 0) * (Number(s.ratePerCarat) || 0), 0)
  return (
    <section className="flex flex-col gap-2 rounded-xl border border-line p-3">
      <div className="flex items-center gap-2"><b className="text-sm">{t('jwl3.stones')}</b><span className="text-xs text-muted">{t('jwl3.stonesHint')}</span>
        <button type="button" onClick={() => onChange([...stones, blankStone])} className={`ml-auto min-h-9 rounded-xl px-3 text-sm ${btn.outline}`}>+ {t('jwl3.addStone')}</button></div>
      {stones.map((s, i) => (
        <div key={i} className="flex flex-col gap-2 rounded-xl bg-[#f6faf9] p-2">
          <div className="grid grid-cols-3 gap-2">
            <input value={s.kind} onChange={(e) => upd(i, 'kind', e.target.value)} maxLength={40} aria-label={t('jwl3.stoneKind')} placeholder={t('jwl3.stoneKind')} className={box} />
            <input value={s.count} onChange={(e) => upd(i, 'count', num(e.target.value))} inputMode="numeric" aria-label={t('jwl3.count')} placeholder={t('jwl3.count')} className={box} />
            <input value={s.carat} onChange={(e) => upd(i, 'carat', num(e.target.value))} inputMode="decimal" aria-label={t('jwl3.carats')} placeholder={t('jwl3.carats')} className={box} />
            <input value={s.shape} onChange={(e) => upd(i, 'shape', e.target.value)} maxLength={30} aria-label={t('jwl3.shape')} placeholder={t('jwl3.shape')} className={box} />
            <input value={s.colour} onChange={(e) => upd(i, 'colour', e.target.value)} maxLength={20} aria-label={t('jwl3.colour')} placeholder={t('jwl3.colour')} className={box} />
            <input value={s.clarity} onChange={(e) => upd(i, 'clarity', e.target.value)} maxLength={20} aria-label={t('jwl3.clarity')} placeholder={t('jwl3.clarity')} className={box} />
            <input value={s.certLab} onChange={(e) => upd(i, 'certLab', e.target.value)} maxLength={20} aria-label={t('jwl3.certLab')} placeholder={t('jwl3.certLab')} className={box} />
            <input value={s.certNo} onChange={(e) => upd(i, 'certNo', e.target.value)} maxLength={40} aria-label={t('jwl3.certNo')} placeholder={t('jwl3.certNo')} className={box} />
            <input value={s.ratePerCarat} onChange={(e) => upd(i, 'ratePerCarat', num(e.target.value))} inputMode="decimal" aria-label={t('jwl3.ratePerCarat')} placeholder={t('jwl3.ratePerCarat')} className={box} />
          </div>
          <div className="flex flex-wrap items-center gap-3 text-sm">
            <label className="flex min-h-9 items-center gap-2"><input type="checkbox" checked={s.labGrown} onChange={(e) => upd(i, 'labGrown', e.target.checked)} className="h-5 w-5" />{t('jwl3.labGrown')}</label>
            <span className="text-muted">{formatRupees((Number(s.carat) || 0) * (Number(s.ratePerCarat) || 0))}</span>
            <button type="button" onClick={() => onChange(stones.filter((_, n) => n !== i))} className={`ml-auto min-h-9 text-sm ${btn.dangerOutline}`}>{t('jwl.remove')}</button>
          </div>
        </div>
      ))}
      {stones.length > 0 && <p className="text-xs text-muted">{t('jwl3.stonesTotal', { carats: carats.toFixed(3), grams: (carats * 0.2).toFixed(3), value: formatRupees(value) })}</p>}
    </section>
  )
}

/* ---------------- karigars and traders ---------------- */

interface Party {
  id: string; name: string; phone: string | null; kind: 'karigar' | 'trader'; allowancePercent: number; allowanceOnFine: boolean; labourPerGram: number; gstRegistered: boolean; isActive: boolean
  gold: number; silver: number; labourDue: number; reverseChargeGst: number; yearPassed: boolean; oldestDue: string | null
}
interface Move {
  id: string; number: number; kind: string; metal: Metal; gross: number; reading: number; fine: number; finished: boolean; allowance: number; labour: number; ratePerGram: number | null
  cashEffect: number; mode: string | null; balanceEffect: number; looseEffect: number; balanceAfter: number; note: string | null; dueBack: string | null; at: string
}
interface Loose { metal: Metal; opening: number; oldBought: number; fromParties: number; toParties: number; fine: number }
type MoveKind = 'issue' | 'receive' | 'rate_cut' | 'labour_paid' | 'opening'
interface MoveDraft { party: Party | null; kind: MoveKind; metal: Metal; gross: string; reading: string; finished: boolean; fine: string; rate: string; theyPay: boolean; amount: string; mode: string; note: string }

/** "has 1.116 g of yours" / "you owe 2.000 g" for a fine balance. */
function Balance({ fine, metal }: { fine: number; metal: Metal }) {
  const { t } = useTranslation()
  if (Math.abs(fine) < 0.0005) return <span className="text-muted">{t(`jwl.${metal}`)} {t('jwl3.settled')}</span>
  return fine > 0
    ? <span className="font-semibold text-[#a1460a]">{t('jwl3.holdsYours', { metal: t(`jwl.${metal}`), grams: grams(fine) })}</span>
    : <span className="font-semibold text-[#1d4ed8]">{t('jwl3.youOwe', { metal: t(`jwl.${metal}`), grams: grams(-fine) })}</span>
}

export function WorkshopPanel({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const parties = useQuery({ queryKey: [shopId, 'jewellery', 'parties'], queryFn: () => api<Party[]>('/v1/jewellery/parties') }).data ?? []
  const loose = useQuery({ queryKey: [shopId, 'jewellery', 'loose'], queryFn: () => api<Loose[]>('/v1/jewellery/loose-metal') }).data ?? []
  const [editing, setEditing] = useState<{ id: string | null; name: string; phone: string; kind: 'karigar' | 'trader'; allowance: string; onFine: boolean; labour: string; gst: boolean } | null>(null)
  const [move, setMove] = useState<MoveDraft | null>(null)
  const [history, setHistory] = useState<Party | null>(null)
  const moves = useQuery({ queryKey: [shopId, 'jewellery', 'moves', history?.id], enabled: history != null, queryFn: () => api<Move[]>(`/v1/jewellery/parties/${history!.id}/moves`) }).data ?? []
  const { busy, error, run } = useRun()
  const refresh = () => queryClient.invalidateQueries({ queryKey: [shopId] })
  const gold = loose.find((l) => l.metal === 'gold')
  const silver = loose.find((l) => l.metal === 'silver')
  const startMove = (party: Party | null, kind: MoveKind, metal: Metal = 'gold') => setMove({ party, kind, metal, gross: '', reading: metal === 'gold' ? '99.5' : '99.9', finished: true,
    fine: party ? String(Math.abs(metal === 'gold' ? party.gold : party.silver) || '') : '', rate: '', theyPay: party ? (metal === 'gold' ? party.gold : party.silver) > 0 : true, amount: party?.labourDue ? String(party.labourDue) : '', mode: 'cash', note: '' })
  const m = move
  const fineNow = m ? (Number(m.gross) || 0) * (Number(m.reading) || 0) / 100 : 0
  const allowance = m?.party && m.kind === 'receive' && m.party.kind === 'karigar' && m.finished ? ((m.party.allowanceOnFine ? fineNow : Number(m.gross) || 0) * m.party.allowancePercent) / 100 : 0

  return (
    <div className="flex flex-col gap-3">
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        <Card tone="bg-[#fdf1e3] text-[#a1460a]" label={t('jwl3.looseGold')} value={grams(gold?.fine ?? 0)} sub={t('jwl3.fineNote')} />
        <Card tone="bg-[#edf2f1] text-[#33423d]" label={t('jwl3.looseSilver')} value={grams(silver?.fine ?? 0)} sub={t('jwl3.fineNote')} />
        <Card tone="bg-[#f1eafd] text-[#6d28d9]" label={t('jwl3.withKarigars')} value={grams(parties.filter((p) => p.kind === 'karigar' && p.gold > 0).reduce((n, p) => n + p.gold, 0))} sub={t('jwl.gold')} />
        <Card tone={parties.some((p) => p.yearPassed) ? 'bg-[#b45309] text-white' : 'bg-[#e8effd] text-[#1d4ed8]'} label={t('jwl3.labourDue')} value={formatRupees(parties.reduce((n, p) => n + Math.max(0, p.labourDue), 0))}
          sub={parties.some((p) => p.yearPassed) ? t('jwl3.yearAlert') : undefined} />
      </div>
      {gold && (gold.oldBought > 0 || gold.opening !== 0 || gold.fromParties > 0 || gold.toParties > 0) && (
        <p className="rounded-xl bg-white p-3 text-xs text-muted shadow-sm">{t('jwl3.looseLine', { opening: grams(gold.opening), old: grams(gold.oldBought), from: grams(gold.fromParties), to: grams(gold.toParties) })}</p>
      )}
      {isOwner && (
        <div className="flex flex-wrap justify-end gap-2">
          <button onClick={() => startMove(null, 'opening')} className={`min-h-11 rounded-xl px-4 ${btn.outline}`}>+ {t('jwl3.openingMetal')}</button>
          <button onClick={() => setEditing({ id: null, name: '', phone: '', kind: 'karigar', allowance: '', onFine: false, labour: '', gst: false })} className={`min-h-11 rounded-xl px-4 ${btn.solid}`}>+ {t('jwl3.addParty')}</button>
        </div>
      )}
      {parties.length === 0 && <p className="rounded-card bg-white p-6 text-center text-muted shadow-sm">{t('jwl3.noParties')}</p>}
      <div className="grid gap-3 lg:grid-cols-2">
        {parties.map((p) => (
          <section key={p.id} className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
            <div className="flex flex-wrap items-center gap-2">
              <span className={`rounded-full px-2.5 py-0.5 text-xs font-bold ${p.kind === 'karigar' ? 'bg-[#f1eafd] text-[#6d28d9]' : 'bg-[#e8effd] text-[#1d4ed8]'}`}>{t(`jwl3.kind.${p.kind}`)}</span>
              <b className="text-lg">{p.name}</b>{p.phone && <span className="text-sm text-muted">{p.phone}</span>}
              {isOwner && <button onClick={() => setEditing({ id: p.id, name: p.name, phone: p.phone ?? '', kind: p.kind, allowance: p.allowancePercent ? String(p.allowancePercent) : '', onFine: p.allowanceOnFine, labour: p.labourPerGram ? String(p.labourPerGram) : '', gst: p.gstRegistered })}
                className={`ml-auto min-h-9 text-sm ${btn.link}`}>{t('jwl.edit')}</button>}
            </div>
            <div className="flex flex-col gap-0.5 text-sm"><Balance fine={p.gold} metal="gold" />{Math.abs(p.silver) >= 0.0005 && <Balance fine={p.silver} metal="silver" />}</div>
            {p.kind === 'karigar' && (
              <p className="text-xs text-muted">{t('jwl3.terms', { allowance: p.allowancePercent, on: p.allowanceOnFine ? t('jwl3.onFine') : t('jwl3.onGross'), labour: formatRupees(p.labourPerGram) })}
                {p.labourDue > 0 && <> · <b className="text-ink">{t('jwl3.labourOwed', { amount: formatRupees(p.labourDue) })}</b></>}
                {p.reverseChargeGst > 0 && <> · {t('jwl3.rcm', { amount: formatRupees(p.reverseChargeGst) })}</>}</p>
            )}
            {p.yearPassed && p.oldestDue && <p className="rounded-xl bg-due-soft p-2 text-xs font-semibold text-due">{t('jwl3.yearPassed', { date: date(p.oldestDue) })}</p>}
            <div className="flex flex-wrap gap-2 border-t border-line pt-2">
              <button onClick={() => startMove(p, 'issue')} className={`min-h-9 rounded-xl px-3 text-sm ${btn.solid}`}>{t('jwl3.issue')}</button>
              <button onClick={() => startMove(p, 'receive')} className={`min-h-9 rounded-xl px-3 text-sm ${btn.outline}`}>{t('jwl3.receive')}</button>
              {isOwner && <button onClick={() => startMove(p, 'rate_cut')} className="min-h-9 rounded-xl border border-line px-3 text-sm font-semibold">{t('jwl3.rateCut')}</button>}
              {isOwner && p.kind === 'karigar' && p.labourDue > 0 && <button onClick={() => startMove(p, 'labour_paid')} className="min-h-9 rounded-xl border border-line px-3 text-sm font-semibold">{t('jwl3.payLabour')}</button>}
              <button onClick={() => setHistory(p)} className={`ml-auto min-h-9 text-sm ${btn.link}`}>{t('jwl3.history')}</button>
            </div>
          </section>
        ))}
      </div>

      {editing && (
        <Drawer title={editing.id ? editing.name : t('jwl3.addParty')} onClose={() => setEditing(null)}>
          <div className="grid grid-cols-2 gap-1 rounded-2xl bg-[#edf2f1] p-1 text-sm">
            {(['karigar', 'trader'] as const).map((k) => <button key={k} onClick={() => setEditing({ ...editing, kind: k })} aria-pressed={editing.kind === k} className={`min-h-11 rounded-xl font-semibold ${editing.kind === k ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted'}`}>{t(`jwl3.kind.${k}`)}</button>)}
          </div>
          <Field label={t('jwl3.name')}><input autoFocus value={editing.name} onChange={(e) => setEditing({ ...editing, name: e.target.value })} maxLength={80} className={box} /></Field>
          <Field label={t('jwl3.phone')}><input value={editing.phone} onChange={(e) => setEditing({ ...editing, phone: e.target.value.replace(/[^\d+ ]/g, '') })} inputMode="tel" maxLength={20} className={box} /></Field>
          {editing.kind === 'karigar' && <>
            <div className="grid grid-cols-2 gap-2">
              <Field label={t('jwl3.allowance')}><input value={editing.allowance} onChange={(e) => setEditing({ ...editing, allowance: num(e.target.value) })} inputMode="decimal" placeholder="0" className={box} /></Field>
              <Field label={t('jwl3.labourPerGram')}><input value={editing.labour} onChange={(e) => setEditing({ ...editing, labour: num(e.target.value) })} inputMode="decimal" placeholder="0" className={box} /></Field>
            </div>
            <div className="grid grid-cols-2 gap-1 rounded-2xl bg-[#edf2f1] p-1 text-sm">
              {([false, true] as const).map((f) => <button key={String(f)} onClick={() => setEditing({ ...editing, onFine: f })} aria-pressed={editing.onFine === f} className={`min-h-11 rounded-xl font-semibold ${editing.onFine === f ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted'}`}>{f ? t('jwl3.onFine') : t('jwl3.onGross')}</button>)}
            </div>
            <p className="text-xs text-muted">{t('jwl3.allowanceHint')}</p>
            <label className="flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={editing.gst} onChange={(e) => setEditing({ ...editing, gst: e.target.checked })} className="h-5 w-5" />{t('jwl3.gstRegistered')}</label>
          </>}
          {error && <p className="text-sm font-semibold text-due">{error}</p>}
          <Button busy={busy} disabled={!editing.name.trim()} onClick={() => void run(() => api(editing.id ? `/v1/jewellery/parties/${editing.id}` : '/v1/jewellery/parties', editing.id ? 'PUT' : 'POST',
            { name: editing.name, phone: editing.phone || null, kind: editing.kind, allowancePercent: Number(editing.allowance) || 0, allowanceOnFine: editing.onFine, labourPerGram: Number(editing.labour) || 0, gstRegistered: editing.gst }))
            .then((ok) => { if (ok) { setEditing(null); void refresh() } })}>{t('save')}</Button>
        </Drawer>
      )}

      {m && (
        <Drawer title={m.party ? `${t(`jwl3.moveTitle.${m.kind}`)} · ${m.party.name}` : t('jwl3.openingMetal')} onClose={() => setMove(null)}>
          {m.kind !== 'labour_paid' && (
            <div className="grid grid-cols-2 gap-1 rounded-2xl bg-[#edf2f1] p-1 text-sm">
              {(['gold', 'silver'] as const).map((x) => <button key={x} onClick={() => setMove({ ...m, metal: x, reading: m.kind === 'rate_cut' ? m.reading : x === 'gold' ? '99.5' : '99.9', fine: m.party && m.kind === 'rate_cut' ? String(Math.abs(x === 'gold' ? m.party.gold : m.party.silver) || '') : m.fine })}
                aria-pressed={m.metal === x} className={`min-h-11 rounded-xl font-semibold ${m.metal === x ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted'}`}>{t(`jwl.${x}`)}</button>)}
            </div>
          )}
          {(m.kind === 'issue' || m.kind === 'receive' || m.kind === 'opening') && <>
            <div className="grid grid-cols-2 gap-2">
              <Field label={t('jwl.grossG')}><input autoFocus value={m.gross} onChange={(e) => setMove({ ...m, gross: num(e.target.value) })} inputMode="decimal" placeholder="0.000" className={box} /></Field>
              <Field label={t('jwl3.touch')}><input value={m.reading} onChange={(e) => setMove({ ...m, reading: num(e.target.value) })} inputMode="decimal" className={box} /></Field>
            </div>
            <WeighButton shopId={shopId} onWeight={(g) => setMove({ ...m, gross: g })} />
            {m.kind === 'receive' && m.party?.kind === 'karigar' && (
              <div className="grid grid-cols-2 gap-1 rounded-2xl bg-[#edf2f1] p-1 text-sm">
                {([true, false] as const).map((f) => <button key={String(f)} onClick={() => setMove({ ...m, finished: f })} aria-pressed={m.finished === f} className={`min-h-11 rounded-xl font-semibold ${m.finished === f ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted'}`}>{f ? t('jwl3.finished') : t('jwl3.scrap')}</button>)}
              </div>
            )}
            <p className="rounded-xl bg-[#f6faf9] p-3 text-sm">{t('jwl.fine')} <b>{grams(fineNow)}</b>
              {allowance > 0 && <> · {t('jwl3.allowed')} <b>{grams(allowance)}</b></>}
              {m.kind === 'receive' && m.party?.kind === 'karigar' && m.finished && m.party.labourPerGram > 0 && <> · {t('jwl3.labourOn', { amount: formatRupees((Number(m.gross) || 0) * m.party.labourPerGram) })}</>}</p>
            {m.kind === 'receive' && m.party?.kind === 'karigar' && m.finished && <p className="text-xs text-muted">{t('jwl3.tagAfter')}</p>}
          </>}
          {m.kind === 'rate_cut' && m.party && <>
            <div className="grid grid-cols-2 gap-1 rounded-2xl bg-[#edf2f1] p-1 text-sm">
              {([true, false] as const).map((f) => <button key={String(f)} onClick={() => setMove({ ...m, theyPay: f })} aria-pressed={m.theyPay === f} className={`min-h-11 rounded-xl font-semibold ${m.theyPay === f ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted'}`}>{f ? t('jwl3.theyPay', { name: m.party!.name }) : t('jwl3.wePay')}</button>)}
            </div>
            <div className="grid grid-cols-2 gap-2">
              <Field label={t('jwl3.fineG')}><input value={m.fine} onChange={(e) => setMove({ ...m, fine: num(e.target.value) })} inputMode="decimal" className={box} /></Field>
              <Field label={t('jwl3.ratePerFineG')}><input value={m.rate} onChange={(e) => setMove({ ...m, rate: num(e.target.value) })} inputMode="decimal" className={box} /></Field>
            </div>
            <p className="rounded-xl bg-[#f6faf9] p-3 text-sm font-semibold">{m.theyPay ? t('jwl3.moneyIn') : t('jwl3.moneyOut')} {formatRupees(Math.round((Number(m.fine) || 0) * (Number(m.rate) || 0)))}</p>
          </>}
          {m.kind === 'labour_paid' && <Field label={t('jwl3.labourAmount')}><input value={m.amount} onChange={(e) => setMove({ ...m, amount: num(e.target.value) })} inputMode="decimal" className={`${box} text-lg font-bold`} /></Field>}
          {(m.kind === 'rate_cut' || m.kind === 'labour_paid') && (
            <div className="grid grid-cols-3 gap-1 rounded-2xl bg-[#edf2f1] p-1 text-sm">
              {(['cash', 'upi', 'bank'] as const).map((x) => <button key={x} onClick={() => setMove({ ...m, mode: x })} aria-pressed={m.mode === x} className={`min-h-11 rounded-xl font-semibold ${m.mode === x ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted'}`}>{t(x)}</button>)}
            </div>
          )}
          <Field label={t('jwl3.note')}><input value={m.note} onChange={(e) => setMove({ ...m, note: e.target.value })} maxLength={200} placeholder={t('jwl3.notePlaceholder')} className={box} /></Field>
          {error && <p className="text-sm font-semibold text-due">{error}</p>}
          <Button busy={busy} disabled={m.kind === 'rate_cut' ? !(Number(m.fine) > 0 && Number(m.rate) > 0) : m.kind === 'labour_paid' ? !(Number(m.amount) > 0) : !(Number(m.gross) > 0 && Number(m.reading) > 0)}
            onClick={() => void run(() => api('/v1/jewellery/metal-moves', 'POST', { partyId: m.party?.id ?? null, kind: m.kind, metal: m.metal, gross: Number(m.gross) || null, reading: Number(m.reading) || null, finished: m.finished,
              fine: Number(m.fine) || null, ratePerGram: Number(m.rate) || null, theyPay: m.theyPay, amount: Number(m.amount) || null, mode: m.mode, note: m.note || null }))
              .then((ok) => { if (ok) { setMove(null); void refresh() } })}>{t('save')}</Button>
        </Drawer>
      )}

      {history && (
        <Drawer title={t('jwl3.historyOf', { name: history.name })} onClose={() => setHistory(null)}>
          {moves.length === 0 && <p className="text-sm text-muted">{t('jwl3.noMoves')}</p>}
          {moves.map((x) => (
            <div key={x.id} className="flex flex-col gap-0.5 border-b border-line pb-2 text-sm">
              <span className="flex justify-between gap-2"><b>{t(`jwl3.moveTitle.${x.kind}`)}{x.kind === 'receive' && !x.finished ? ` · ${t('jwl3.scrap')}` : ''}</b><span className="text-xs text-muted">{when(x.at)}</span></span>
              {x.gross > 0 && <span className="text-muted">{t(`jwl.${x.metal}`)} {grams(x.gross)} @ {x.reading}% = {t('jwl.fine')} {grams(x.fine)}{x.allowance > 0 ? ` · ${t('jwl3.allowed')} ${grams(x.allowance)}` : ''}{x.labour > 0 ? ` · ${t('jwl3.labourOn', { amount: formatRupees(x.labour) })}` : ''}</span>}
              {x.kind === 'rate_cut' && <span className="text-muted">{grams(x.fine)} × {formatRupees(x.ratePerGram ?? 0)} = {formatRupees(Math.abs(x.cashEffect))} ({x.cashEffect > 0 ? t('jwl3.moneyIn') : t('jwl3.moneyOut')})</span>}
              {x.kind === 'labour_paid' && <span className="text-muted">{formatRupees(-x.cashEffect)} · {x.mode ? t(x.mode) : ''}</span>}
              {x.note && <span className="text-xs text-muted">{x.note}</span>}
              {x.kind !== 'labour_paid' && <span className="text-xs">{t('jwl3.after')} <Balance fine={x.balanceAfter} metal={x.metal} /></span>}
            </div>
          ))}
        </Drawer>
      )}
    </div>
  )
}

/* ---------------- trays and scales ---------------- */

interface TrayView { tray: string; pieces: number; gross: number; lastCountAt: string | null; lastCountKind: string | null; lastCountClean: boolean }
interface TrayCountView { id: string; tray: string; kind: string; expectedPieces: number; expectedGross: number; countedPieces: number; countedGross: number | null; grossDifference: number | null; missing: string[]; extra: string[]; clean: boolean; at: string }

export function TraysPanel({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const nowMs = useNow()
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const trays = useQuery({ queryKey: [shopId, 'jewellery', 'trays'], queryFn: () => api<TrayView[]>('/v1/jewellery/trays') }).data ?? []
  const counts = useQuery({ queryKey: [shopId, 'jewellery', 'tray-counts'], queryFn: () => api<TrayCountView[]>('/v1/jewellery/tray-counts') }).data ?? []
  const pieces = useQuery({ queryKey: [shopId, 'jewellery', 'pieces', 'in', ''], queryFn: () => api<JewelleryPiece[]>('/v1/jewellery/pieces?status=in&q=') }).data ?? []
  const scales = useScales(shopId)
  const [counting, setCounting] = useState<{ tray: string; kind: 'opening' | 'closing'; tags: string[]; text: string; gross: string } | null>(null)
  const [result, setResult] = useState<TrayCountView | null>(null)
  const [scale, setScale] = useState<{ id: string | null; name: string; readsTo: string; until: string; baud: string; active: boolean } | null>(null)
  const { busy, error, run } = useRun()
  const hour = new Date(nowMs).getHours()
  const inTray = counting ? pieces.filter((p) => !p.onApproval && (p.tray ?? '') === counting.tray) : []
  const toggle = (tag: string) => counting && setCounting({ ...counting, tags: counting.tags.includes(tag) ? counting.tags.filter((x) => x !== tag) : [...counting.tags, tag] })

  return (
    <div className="flex flex-col gap-3">
      <p className="rounded-xl bg-white p-3 text-sm text-muted shadow-sm">{t('jwl3.trayHow')}</p>
      {trays.length === 0 && <p className="rounded-card bg-white p-6 text-center text-muted shadow-sm">{t('jwl.noPieces')}</p>}
      <div className="grid grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-3">
        {trays.map((x) => (
          <section key={x.tray} className="flex flex-col gap-1 rounded-[18px] bg-white p-4 shadow-sm">
            <b className="text-lg">{x.tray || t('jwl3.noTray')}</b>
            <span className="text-sm">{t('jwl3.trayLine', { count: x.pieces, gross: grams(x.gross) })}</span>
            <span className={`text-xs font-semibold ${x.lastCountAt == null ? 'text-muted' : x.lastCountClean ? 'text-[#0b5e4f]' : 'text-due'}`}>
              {x.lastCountAt == null ? t('jwl3.neverCounted') : t(x.lastCountClean ? 'jwl3.lastClean' : 'jwl3.lastDiff', { when: when(x.lastCountAt), kind: t(`jwl3.countKind.${x.lastCountKind}`) })}</span>
            <button onClick={() => { setResult(null); setCounting({ tray: x.tray, kind: hour < 14 ? 'opening' : 'closing', tags: [], text: '', gross: '' }) }} className={`mt-1 min-h-10 self-start rounded-xl px-4 text-sm ${btn.solid}`}>{t('jwl3.countTray')}</button>
          </section>
        ))}
      </div>
      {counts.length > 0 && (
        <section className="flex flex-col gap-1 rounded-card bg-white p-4 shadow-sm">
          <b>{t('jwl3.recentCounts')}</b>
          {counts.slice(0, 12).map((c) => (
            <p key={c.id} className="flex flex-wrap gap-2 border-t border-line pt-1 text-sm">
              <span className="w-32 text-muted">{when(c.at)}</span><b>{c.tray || t('jwl3.noTray')}</b><span className="text-muted">{t(`jwl3.countKind.${c.kind}`)}</span>
              <span className={`ml-auto font-semibold ${c.clean ? 'text-[#0b5e4f]' : 'text-due'}`}>{c.clean ? t('jwl3.allThere', { count: c.countedPieces }) : t('jwl3.diffShort', { missing: c.missing.length, extra: c.extra.length })}</span>
            </p>
          ))}
        </section>
      )}

      <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
        <div className="flex items-center gap-2"><b>{t('jwl3.scales')}</b>
          {isOwner && <button onClick={() => setScale({ id: null, name: '', readsTo: '0.001', until: '', baud: '9600', active: true })} className={`ml-auto min-h-9 rounded-xl px-3 text-sm ${btn.outline}`}>+ {t('jwl3.addScale')}</button>}</div>
        {scales.length === 0 && <p className="text-sm text-muted">{t('jwl3.noScales')}</p>}
        {scales.map((s) => (
          <p key={s.id} className="flex flex-wrap items-center gap-2 border-t border-line pt-2 text-sm">
            <b>{s.name}</b><span className="text-muted">{t('jwl3.readsTo', { value: s.readsTo })}</span>
            {s.stampedUntil ? <span className={`rounded-full px-2.5 py-0.5 text-xs font-bold ${s.daysLeft < 0 ? 'bg-due-soft text-due' : s.daysLeft <= 30 ? 'bg-warn-soft text-warn-ink' : 'bg-[#e3f2ee] text-[#0b5e4f]'}`}>
              {s.daysLeft < 0 ? t('jwl3.stampExpired', { date: date(s.stampedUntil) }) : t('jwl3.stampedTill', { date: date(s.stampedUntil) })}</span> : <span className="rounded-full bg-warn-soft px-2.5 py-0.5 text-xs font-bold text-warn-ink">{t('jwl3.noStampDate')}</span>}
            {!s.isActive && <span className="text-xs text-muted">{t('jwl3.notInUse')}</span>}
            {isOwner && <button onClick={() => setScale({ id: s.id, name: s.name, readsTo: String(s.readsTo), until: s.stampedUntil ?? '', baud: String(s.baud), active: s.isActive })} className={`ml-auto min-h-9 text-sm ${btn.link}`}>{t('jwl.edit')}</button>}
          </p>
        ))}
        <p className="text-xs text-muted">{canReadScale() ? t('jwl3.scaleHint') : t('jwl3.scaleNoBrowser')}</p>
      </section>

      {counting && (
        <Drawer title={t('jwl3.countingTray', { tray: counting.tray || t('jwl3.noTray') })} onClose={() => setCounting(null)}>
          {result ? (
            <>
              <p className={`rounded-xl p-3 text-sm font-semibold ${result.clean ? 'bg-[#e3f2ee] text-[#0b5e4f]' : 'bg-due-soft text-due'}`}>{result.clean ? t('jwl3.allThere', { count: result.countedPieces }) : t('jwl3.notClean')}</p>
              <p className="text-sm">{t('jwl3.expected', { count: result.expectedPieces, gross: grams(result.expectedGross) })}</p>
              {result.countedGross != null && <p className="text-sm">{t('jwl3.weighed', { gross: grams(result.countedGross), diff: `${(result.grossDifference ?? 0) > 0 ? '+' : ''}${(result.grossDifference ?? 0).toFixed(3)} g` })}</p>}
              {result.missing.length > 0 && <p className="text-sm"><b className="text-due">{t('jwl3.missing')}</b> {result.missing.join(', ')}</p>}
              {result.extra.length > 0 && <p className="text-sm"><b className="text-[#a1460a]">{t('jwl3.extra')}</b> {result.extra.join(', ')}</p>}
              <Button onClick={() => setCounting(null)}>{t('jwl3.done')}</Button>
            </>
          ) : (
            <>
              <div className="grid grid-cols-2 gap-1 rounded-2xl bg-[#edf2f1] p-1 text-sm">
                {(['opening', 'closing'] as const).map((k) => <button key={k} onClick={() => setCounting({ ...counting, kind: k })} aria-pressed={counting.kind === k} className={`min-h-11 rounded-xl font-semibold ${counting.kind === k ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted'}`}>{t(`jwl3.countKind.${k}`)}</button>)}
              </div>
              <Field label={t('jwl3.scanTags')}>
                <input autoFocus value={counting.text} onChange={(e) => setCounting({ ...counting, text: e.target.value })} placeholder="J-12"
                  onKeyDown={(e) => { if (e.key === 'Enter' && counting.text.trim()) { e.preventDefault(); const n = counting.text.trim().replace(/^GKJ:/i, '').replace(/^J-?/i, ''); setCounting({ ...counting, text: '', tags: counting.tags.includes(`J-${n}`) ? counting.tags : [...counting.tags, `J-${n}`] }) } }} className={box} />
              </Field>
              <p className="text-xs text-muted">{t('jwl3.orTick', { found: counting.tags.length, total: inTray.length })}</p>
              <div className="flex max-h-64 flex-col gap-1 overflow-y-auto rounded-xl border border-line p-2">
                {inTray.map((p) => <label key={p.id} className="flex min-h-10 items-center gap-2 text-sm"><input type="checkbox" className="h-5 w-5" checked={counting.tags.includes(`J-${p.tagNo}`)} onChange={() => toggle(`J-${p.tagNo}`)} />
                  <span className="font-mono font-bold">J-{p.tagNo}</span>{p.name} · {grams(p.gross)}</label>)}
                {counting.tags.filter((tag) => !inTray.some((p) => `J-${p.tagNo}` === tag)).map((tag) => <span key={tag} className="text-sm font-semibold text-[#a1460a]">{tag} · {t('jwl3.notThisTray')}</span>)}
              </div>
              <Field label={t('jwl3.weighAll')}><input value={counting.gross} onChange={(e) => setCounting({ ...counting, gross: num(e.target.value) })} inputMode="decimal" placeholder={t('jwl3.optional')} className={box} /></Field>
              <WeighButton shopId={shopId} onWeight={(g) => setCounting({ ...counting, gross: g })} />
              {error && <p className="text-sm font-semibold text-due">{error}</p>}
              <Button busy={busy} onClick={() => void run(async () => {
                const r = await api<TrayCountView>('/v1/jewellery/tray-counts', 'POST', { tray: counting.tray, kind: counting.kind, tags: counting.tags, countedGross: counting.gross ? Number(counting.gross) : null })
                setResult(r)
                await queryClient.invalidateQueries({ queryKey: [shopId, 'jewellery'] })
              })}>{t('jwl3.saveCount', { count: counting.tags.length })}</Button>
            </>
          )}
        </Drawer>
      )}

      {scale && (
        <Drawer title={scale.id ? scale.name : t('jwl3.addScale')} onClose={() => setScale(null)}>
          <Field label={t('jwl3.scaleName')}><input autoFocus value={scale.name} onChange={(e) => setScale({ ...scale, name: e.target.value })} maxLength={60} placeholder={t('jwl3.scalePlaceholder')} className={box} /></Field>
          <Field label={t('jwl3.verifiedTo')}>
            <select value={scale.readsTo} onChange={(e) => setScale({ ...scale, readsTo: e.target.value })} className={box}>
              {['0.001', '0.01', '0.1'].map((v) => <option key={v} value={v}>{v} g</option>)}
            </select></Field>
          <p className="text-xs text-muted">{t('jwl3.verifiedHint')}</p>
          <div className="grid grid-cols-2 gap-2">
            <Field label={t('jwl3.stampedUntil')}><input type="date" value={scale.until} onChange={(e) => setScale({ ...scale, until: e.target.value })} className={box} /></Field>
            <Field label={t('jwl3.baud')}>
              <select value={scale.baud} onChange={(e) => setScale({ ...scale, baud: e.target.value })} className={box}>
                {[1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200].map((b) => <option key={b} value={b}>{b}</option>)}
              </select></Field>
          </div>
          <label className="flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={scale.active} onChange={(e) => setScale({ ...scale, active: e.target.checked })} className="h-5 w-5" />{t('jwl3.inUse')}</label>
          {error && <p className="text-sm font-semibold text-due">{error}</p>}
          <Button busy={busy} disabled={!scale.name.trim()} onClick={() => void run(() => api(scale.id ? `/v1/jewellery/scales/${scale.id}` : '/v1/jewellery/scales', scale.id ? 'PUT' : 'POST',
            { name: scale.name, readsTo: Number(scale.readsTo), stampedUntil: scale.until || null, baud: Number(scale.baud), isActive: scale.active }))
            .then((ok) => { if (ok) { setScale(null); void queryClient.invalidateQueries({ queryKey: [shopId, 'jewellery', 'scales'] }) } })}>{t('save')}</Button>
        </Drawer>
      )}
    </div>
  )
}

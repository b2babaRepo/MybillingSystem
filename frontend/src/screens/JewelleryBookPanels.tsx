import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { CustomerFinder } from '../components/CustomerFinder'
import { Button, Drawer, btn } from '../components/ui'
import { api } from '../lib/api'
import { addCustomer, useCustomers } from '../lib/data'
import { formatRupees } from '../lib/format'
import { grams, karat, type JewelleryPiece, type Metal } from '../lib/jewellery'
import { navigate } from '../router'

// Phase 2 of the jewellery counter: girvi, savings schemes, orders and repairs, approval memos.

const box = 'h-11 min-w-0 rounded-xl border border-line bg-white px-3 outline-none focus:border-brand-line'
const num = (v: string) => v.replace(/[^\d.]/g, '')
const today = () => new Date(Date.now() + 5.5 * 3600_000).toISOString().slice(0, 10)
const plusDays = (n: number) => new Date(Date.now() + 5.5 * 3600_000 + n * 86_400_000).toISOString().slice(0, 10)
const date = (d: string) => new Date(`${d}T00:00:00`).toLocaleDateString([], { day: 'numeric', month: 'short', year: 'numeric' })

function Card({ tone, label, value }: { tone: string; label: string; value: ReactNode }) {
  return <div className={`flex min-h-[76px] flex-col justify-between rounded-[18px] p-3 ${tone}`}><span className="text-[13px] font-bold">{label}</span><b className="text-xl">{value}</b></div>
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return <label className="flex flex-col gap-1 text-sm font-semibold">{label}{children}</label>
}

function useRun() {
  const { t } = useTranslation()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  async function run(action: () => Promise<unknown>) {
    setBusy(true)
    setError('')
    try { await action(); return true } catch (e) { setError(e instanceof Error ? e.message : t('error')); return false } finally { setBusy(false) }
  }
  return { busy, error, run, setError }
}

/** The customer picker used in every form here, with "add a new customer" built in. */
function WhoField({ shopId, value, onChange }: { shopId: string; value: string | null; onChange: (id: string) => void }) {
  const { customers } = useCustomers(shopId)
  return <CustomerFinder customers={customers} selected={customers.find((c) => c.id === value) ?? null} onKhata={0} onPick={onChange} onCreate={(name, phone) => addCustomer(shopId, name, phone)} inputId="jwl-customer" />
}

/* ---------------- girvi ---------------- */

interface Girvi {
  id: string; number: number; customerId: string; customer: string; phone: string | null; items: string; metal: Metal; gross: number; reading: number; fine: number; pledgeValue: number
  principal: number; ratePerMonth: number; dayWise: boolean; pledgedOn: string; dueOn: string; kyc: string | null; note: string | null; closedOn: string | null
  principalLeft: number; interestDue: number; toClose: number; overdue: boolean; payments: { paidOn: string; amount: number; interestPart: number; principalPart: number; mode: string }[]
}

export function GirviPanel({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<'open' | 'closed'>('open')
  const list = useQuery({ queryKey: [shopId, 'jewellery', 'girvi', status], queryFn: () => api<Girvi[]>(`/v1/jewellery/girvi?status=${status}`) }).data ?? []
  const [adding, setAdding] = useState(false)
  const [paying, setPaying] = useState<Girvi | null>(null)
  const [form, setForm] = useState({ customerId: null as string | null, items: '', metal: 'gold' as Metal, gross: '', reading: '91.6', principal: '', rate: '1.5', dayWise: false, months: '12', kyc: '', mode: 'cash' })
  const [pay, setPay] = useState({ amount: '', mode: 'cash' })
  const { busy, error, run } = useRun()
  const refresh = () => queryClient.invalidateQueries({ queryKey: [shopId, 'jewellery', 'girvi'] })
  const open = list.filter((g) => !g.closedOn)

  return (
    <div className="flex flex-col gap-3">
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        <Card tone="bg-[#e3f2ee] text-[#0b5e4f]" label={t('jwl2.loansOpen')} value={open.length} />
        <Card tone="bg-[#e8effd] text-[#1d4ed8]" label={t('jwl2.lentOut')} value={formatRupees(open.reduce((n, g) => n + g.principalLeft, 0))} />
        <Card tone="bg-[#fdf1e3] text-[#a1460a]" label={t('jwl2.interestDue')} value={formatRupees(open.reduce((n, g) => n + g.interestDue, 0))} />
        <Card tone={open.some((g) => g.overdue) ? 'bg-[#b45309] text-white' : 'bg-[#edf2f1] text-[#33423d]'} label={t('jwl2.overdue')} value={open.filter((g) => g.overdue).length} />
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <div className="flex gap-1 rounded-xl bg-[#edf2f1] p-1">
          {(['open', 'closed'] as const).map((s) => <button key={s} onClick={() => setStatus(s)} aria-pressed={status === s} className={`min-h-9 rounded-lg px-3 text-sm font-semibold ${status === s ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted'}`}>{t(`jwl2.girviStatus.${s}`)}</button>)}
        </div>
        <span className="mr-auto" />
        {isOwner && <button onClick={() => setAdding(true)} className={`min-h-11 rounded-xl px-4 ${btn.solid}`}>+ {t('jwl2.newGirvi')}</button>}
      </div>
      <p className="rounded-xl bg-warn-soft p-3 text-xs font-semibold text-warn-ink">{t('jwl2.girviLaw')}</p>
      {list.length === 0 && <p className="rounded-card bg-white p-6 text-center text-muted shadow-sm">{t('jwl2.noGirvi')}</p>}
      {list.length > 0 && (
        <div className="overflow-x-auto rounded-card bg-white shadow-sm">
          <table className="w-full min-w-[860px] border-collapse text-sm">
            <thead className="bg-[#f6faf9] text-left text-[11px] tracking-wide text-muted uppercase">
              <tr><th className="px-3 py-2.5">#</th><th className="px-2 py-2.5">{t('jwl2.customer')}</th><th className="px-2 py-2.5">{t('jwl2.pledged')}</th><th className="px-2 py-2.5 text-right">{t('jwl2.lent')}</th>
                <th className="px-2 py-2.5">{t('jwl2.interest')}</th><th className="px-2 py-2.5 text-right">{t('jwl2.toClose')}</th><th className="px-2 py-2.5">{t('jwl2.due')}</th><th className="px-3 py-2.5" /></tr>
            </thead>
            <tbody>
              {list.map((g) => (
                <tr key={g.id} className="border-t border-line align-top">
                  <td className="px-3 py-2.5 font-mono font-bold">{g.number}</td>
                  <td className="px-2 py-2.5"><b>{g.customer}</b>{g.phone && <span className="block text-xs text-muted">{g.phone}</span>}</td>
                  <td className="px-2 py-2.5">{g.items}<span className="block text-xs text-muted">{grams(g.gross)} · {g.reading}% · {t('jwl.fine')} {grams(g.fine)}{g.pledgeValue ? ` · ${formatRupees(g.pledgeValue)}` : ''}</span></td>
                  <td className="px-2 py-2.5 text-right">{formatRupees(g.principal)}<span className="block text-xs text-muted">{date(g.pledgedOn)}</span></td>
                  <td className="px-2 py-2.5">{g.ratePerMonth}% {t('jwl2.perMonth')}<span className="block text-xs text-muted">{g.dayWise ? t('jwl2.dayWise') : t('jwl2.monthWise')}</span></td>
                  <td className="px-2 py-2.5 text-right">{g.closedOn ? <span className="text-muted">{t('jwl2.closedOn', { date: date(g.closedOn) })}</span> : <><b>{formatRupees(g.toClose)}</b><span className="block text-xs text-muted">{t('jwl2.interestShort')} {formatRupees(g.interestDue)}</span></>}</td>
                  <td className="px-2 py-2.5">{g.overdue ? <span className="rounded-full bg-due-soft px-2.5 py-0.5 text-xs font-bold text-due">{date(g.dueOn)}</span> : date(g.dueOn)}</td>
                  <td className="px-3 py-2.5 text-right">{!g.closedOn && <button onClick={() => { setPaying(g); setPay({ amount: String(g.toClose), mode: 'cash' }) }} className={`min-h-9 rounded-xl px-3 text-sm ${btn.solid}`}>{t('jwl2.receive')}</button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {adding && (
        <Drawer title={t('jwl2.newGirvi')} onClose={() => setAdding(false)}>
          <WhoField shopId={shopId} value={form.customerId} onChange={(id) => setForm({ ...form, customerId: id })} />
          <Field label={t('jwl2.pledgedItems')}><input value={form.items} onChange={(e) => setForm({ ...form, items: e.target.value })} maxLength={200} placeholder={t('jwl2.itemsPlaceholder')} className={box} /></Field>
          <div className="grid grid-cols-3 gap-2">
            <Field label={t('jwl.metal')}><select value={form.metal} onChange={(e) => setForm({ ...form, metal: e.target.value as Metal })} className={box}><option value="gold">{t('jwl.gold')}</option><option value="silver">{t('jwl.silver')}</option></select></Field>
            <Field label={t('jwl.grossG')}><input value={form.gross} onChange={(e) => setForm({ ...form, gross: num(e.target.value) })} inputMode="decimal" className={box} /></Field>
            <Field label={t('jwl.reading')}><input value={form.reading} onChange={(e) => setForm({ ...form, reading: num(e.target.value) })} inputMode="decimal" className={box} /></Field>
          </div>
          <div className="grid grid-cols-3 gap-2">
            <Field label={t('jwl2.amountLent')}><input value={form.principal} onChange={(e) => setForm({ ...form, principal: num(e.target.value) })} inputMode="decimal" className={box} /></Field>
            <Field label={t('jwl2.ratePerMonth')}><input value={form.rate} onChange={(e) => setForm({ ...form, rate: num(e.target.value) })} inputMode="decimal" className={box} /></Field>
            <Field label={t('jwl2.months')}><input value={form.months} onChange={(e) => setForm({ ...form, months: num(e.target.value) })} inputMode="numeric" className={box} /></Field>
          </div>
          <label className="flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={form.dayWise} onChange={(e) => setForm({ ...form, dayWise: e.target.checked })} className="h-5 w-5" />{t('jwl2.dayWiseLabel')}</label>
          <Field label={t('jwl2.kyc')}><input value={form.kyc} onChange={(e) => setForm({ ...form, kyc: e.target.value })} maxLength={60} placeholder={t('jwl2.kycPlaceholder')} className={box} /></Field>
          <Field label={t('jwl2.givenBy')}><select value={form.mode} onChange={(e) => setForm({ ...form, mode: e.target.value })} className={box}><option value="cash">{t('cash')}</option><option value="upi">{t('upi')}</option><option value="bank">{t('bank')}</option></select></Field>
          {error && <p className="text-sm font-semibold text-due">{error}</p>}
          <Button busy={busy} disabled={!form.customerId || !form.items.trim() || !(Number(form.gross) > 0) || !(Number(form.principal) > 0)} onClick={() => void run(async () => {
            await api('/v1/jewellery/girvi', 'POST', { customerId: form.customerId, items: form.items, metal: form.metal, gross: Number(form.gross), reading: Number(form.reading), principal: Number(form.principal),
              ratePerMonth: Number(form.rate) || 0, dayWise: form.dayWise, months: Number(form.months) || 12, kyc: form.kyc || null, mode: form.mode })
          }).then((ok) => { if (ok) { setAdding(false); void refresh() } })}>{t('jwl2.giveLoan')}</Button>
        </Drawer>
      )}

      {paying && (
        <Drawer title={t('jwl2.receiveOn', { number: paying.number })} onClose={() => setPaying(null)}>
          <p className="rounded-xl bg-[#f6faf9] p-3 text-sm">{t('jwl2.owedNow', { principal: formatRupees(paying.principalLeft), interest: formatRupees(paying.interestDue), total: formatRupees(paying.toClose) })}</p>
          <Field label={t('jwl.received')}><input value={pay.amount} onChange={(e) => setPay({ ...pay, amount: num(e.target.value) })} inputMode="decimal" className={`${box} text-lg font-bold`} /></Field>
          <div className="grid grid-cols-3 gap-1 rounded-2xl bg-[#edf2f1] p-1 text-sm">
            {(['cash', 'upi', 'bank'] as const).map((m) => <button key={m} onClick={() => setPay({ ...pay, mode: m })} aria-pressed={pay.mode === m} className={`min-h-11 rounded-xl font-semibold ${pay.mode === m ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted'}`}>{t(m)}</button>)}
          </div>
          <p className="text-xs text-muted">{Number(pay.amount) >= paying.toClose ? t('jwl2.closesNote') : t('jwl2.partNote')}</p>
          {error && <p className="text-sm font-semibold text-due">{error}</p>}
          <Button busy={busy} disabled={!(Number(pay.amount) > 0)} onClick={() => void run(() => api(`/v1/jewellery/girvi/${paying.id}/payments`, 'POST', { amount: Number(pay.amount), mode: pay.mode }))
            .then((ok) => { if (ok) { setPaying(null); void refresh() } })}>{t('jwl2.savePayment')}</Button>
        </Drawer>
      )}
    </div>
  )
}

/* ---------------- schemes ---------------- */

interface Plan { id: string; name: string; months: number; instalment: number; bonus: number; isActive: boolean; members: number }
export interface Member {
  id: string; number: number; planId: string; plan: string; customerId: string; customer: string; phone: string | null; startedOn: string; months: number; instalment: number; bonus: number
  paid: number; paidTotal: number; complete: boolean; redeemedBillId: string | null; closedOn: string | null; nextDue: string
}

export function SchemesPanel({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const plans = useQuery({ queryKey: [shopId, 'jewellery', 'plans'], queryFn: () => api<Plan[]>('/v1/jewellery/schemes') }).data ?? []
  const members = useQuery({ queryKey: [shopId, 'jewellery', 'members'], queryFn: () => api<Member[]>('/v1/jewellery/schemes/members') }).data ?? []
  const [newPlan, setNewPlan] = useState<{ name: string; months: string; instalment: string; bonus: string } | null>(null)
  const [joining, setJoining] = useState<{ planId: string; customerId: string | null } | null>(null)
  const { busy, error, run } = useRun()
  const refresh = () => queryClient.invalidateQueries({ queryKey: [shopId, 'jewellery'] })
  const late = members.filter((m) => !m.complete && m.nextDue < today())

  return (
    <div className="flex flex-col gap-3">
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        <Card tone="bg-[#e3f2ee] text-[#0b5e4f]" label={t('jwl2.members')} value={members.length} />
        <Card tone="bg-[#e8effd] text-[#1d4ed8]" label={t('jwl2.collected')} value={formatRupees(members.reduce((n, m) => n + m.paidTotal, 0))} />
        <Card tone="bg-[#f1eafd] text-[#6d28d9]" label={t('jwl2.readyToBuy')} value={members.filter((m) => m.complete).length} />
        <Card tone={late.length ? 'bg-[#b45309] text-white' : 'bg-[#edf2f1] text-[#33423d]'} label={t('jwl2.lateInstalments')} value={late.length} />
      </div>
      <div className="flex flex-wrap gap-2">
        {plans.map((p) => (
          <div key={p.id} className="flex min-w-56 flex-1 flex-col gap-2 rounded-[18px] bg-[#fdf1e3] p-4">
            <b className="text-lg">{p.name}</b>
            <span className="text-sm">{t('jwl2.planLine', { months: p.months, amount: formatRupees(p.instalment), bonus: formatRupees(p.bonus) })}</span>
            <span className="text-xs text-muted">{t('jwl2.membersCount', { count: p.members })}</span>
            {p.isActive && <button onClick={() => setJoining({ planId: p.id, customerId: null })} className={`min-h-10 self-start rounded-xl px-3 text-sm ${btn.solid}`}>+ {t('jwl2.enrol')}</button>}
          </div>
        ))}
        {isOwner && <button onClick={() => setNewPlan({ name: '', months: '11', instalment: '', bonus: '' })} className={`min-h-[120px] min-w-56 flex-1 rounded-[18px] ${btn.add}`}>+ {t('jwl2.newPlan')}</button>}
      </div>
      {members.length > 0 && (
        <div className="overflow-x-auto rounded-card bg-white shadow-sm">
          <table className="w-full min-w-[760px] border-collapse text-sm">
            <thead className="bg-[#f6faf9] text-left text-[11px] tracking-wide text-muted uppercase">
              <tr><th className="px-3 py-2.5">#</th><th className="px-2 py-2.5">{t('jwl2.customer')}</th><th className="px-2 py-2.5">{t('jwl2.scheme')}</th><th className="px-2 py-2.5">{t('jwl2.paid')}</th><th className="px-2 py-2.5">{t('jwl2.nextDue')}</th><th className="px-3 py-2.5" /></tr>
            </thead>
            <tbody>
              {members.map((m) => (
                <tr key={m.id} className="border-t border-line">
                  <td className="px-3 py-2.5 font-mono font-bold">{m.number}</td>
                  <td className="px-2 py-2.5"><b>{m.customer}</b>{m.phone && <span className="block text-xs text-muted">{m.phone}</span>}</td>
                  <td className="px-2 py-2.5">{m.plan}</td>
                  <td className="px-2 py-2.5">
                    <span className="flex items-center gap-2"><span className="h-2 w-24 overflow-hidden rounded-full bg-[#edf2f1]"><span className="block h-full bg-brand" style={{ width: `${(m.paid / m.months) * 100}%` }} /></span>{m.paid}/{m.months}</span>
                    <span className="text-xs text-muted">{formatRupees(m.paidTotal)}</span>
                  </td>
                  <td className="px-2 py-2.5">{m.complete ? <span className="rounded-full bg-[#f1eafd] px-2.5 py-0.5 text-xs font-bold text-[#6d28d9]">{t('jwl2.complete', { bonus: formatRupees(m.bonus) })}</span> : m.nextDue < today() ? <span className="rounded-full bg-due-soft px-2.5 py-0.5 text-xs font-bold text-due">{date(m.nextDue)}</span> : date(m.nextDue)}</td>
                  <td className="px-3 py-2.5 text-right">{!m.complete && <button disabled={busy} onClick={() => void run(() => api(`/v1/jewellery/schemes/members/${m.id}/instalments`, 'POST', { entryId: crypto.randomUUID(), mode: 'cash' })).then(() => refresh())}
                    className={`min-h-9 rounded-xl px-3 text-sm ${btn.solid}`}>{t('jwl2.takeInstalment', { amount: formatRupees(m.instalment) })}</button>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {error && <p className="text-sm font-semibold text-due">{error}</p>}
      <p className="text-xs text-muted">{t('jwl2.schemeHow')}</p>

      {newPlan && (
        <Drawer title={t('jwl2.newPlan')} onClose={() => setNewPlan(null)}>
          <Field label={t('jwl2.planName')}><input value={newPlan.name} onChange={(e) => setNewPlan({ ...newPlan, name: e.target.value })} maxLength={60} placeholder={t('jwl2.planPlaceholder')} className={box} /></Field>
          <div className="grid grid-cols-3 gap-2">
            <Field label={t('jwl2.months')}><input value={newPlan.months} onChange={(e) => setNewPlan({ ...newPlan, months: num(e.target.value) })} inputMode="numeric" className={box} /></Field>
            <Field label={t('jwl2.instalment')}><input value={newPlan.instalment} onChange={(e) => setNewPlan({ ...newPlan, instalment: num(e.target.value) })} inputMode="decimal" className={box} /></Field>
            <Field label={t('jwl2.bonus')}><input value={newPlan.bonus} onChange={(e) => setNewPlan({ ...newPlan, bonus: num(e.target.value) })} inputMode="decimal" className={box} /></Field>
          </div>
          <p className="text-xs text-muted">{t('jwl2.bonusHint')}</p>
          {error && <p className="text-sm font-semibold text-due">{error}</p>}
          <Button busy={busy} disabled={!newPlan.name.trim() || !(Number(newPlan.instalment) > 0)} onClick={() => void run(() => api('/v1/jewellery/schemes', 'POST', { name: newPlan.name, months: Number(newPlan.months), instalment: Number(newPlan.instalment), bonus: Number(newPlan.bonus) || 0 }))
            .then((ok) => { if (ok) { setNewPlan(null); void refresh() } })}>{t('save')}</Button>
        </Drawer>
      )}
      {joining && (
        <Drawer title={t('jwl2.enrol')} onClose={() => setJoining(null)}>
          <WhoField shopId={shopId} value={joining.customerId} onChange={(id) => setJoining({ ...joining, customerId: id })} />
          {error && <p className="text-sm font-semibold text-due">{error}</p>}
          <Button busy={busy} disabled={!joining.customerId} onClick={() => void run(() => api('/v1/jewellery/schemes/members', 'POST', { planId: joining.planId, customerId: joining.customerId }))
            .then((ok) => { if (ok) { setJoining(null); void refresh() } })}>{t('jwl2.enrol')}</Button>
        </Drawer>
      )}
    </div>
  )
}

/* ---------------- orders and repairs ---------------- */

interface Order {
  id: string; number: number; kind: 'order' | 'repair'; customerId: string; customer: string; phone: string | null; description: string; metal: Metal | null; purity: number | null
  weight: number | null; weightOut: number | null; fixedRate: number | null; charge: number | null; dueOn: string; status: string; advance: number; billId: string | null; note: string | null; late: boolean
}

export function OrdersPanel({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [status, setStatus] = useState<'open' | 'done'>('open')
  const list = useQuery({ queryKey: [shopId, 'jewellery', 'orders', status], queryFn: () => api<Order[]>(`/v1/jewellery/orders?status=${status}`) }).data ?? []
  const [form, setForm] = useState<{ kind: 'order' | 'repair'; customerId: string | null; description: string; metal: Metal; purity: number; weight: string; fixedRate: string; dueOn: string; advance: string } | null>(null)
  const [act, setAct] = useState<{ order: Order; what: 'advance' | 'deliver' } | null>(null)
  const [amount, setAmount] = useState('')
  const [weightOut, setWeightOut] = useState('')
  const { busy, error, run } = useRun()
  const refresh = () => queryClient.invalidateQueries({ queryKey: [shopId] })

  return (
    <div className="flex flex-col gap-3">
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
        <Card tone="bg-[#e3f2ee] text-[#0b5e4f]" label={t('jwl2.openOrders')} value={list.filter((o) => o.kind === 'order' && status === 'open').length} />
        <Card tone="bg-[#e8effd] text-[#1d4ed8]" label={t('jwl2.openRepairs')} value={list.filter((o) => o.kind === 'repair' && status === 'open').length} />
        <Card tone="bg-[#f1eafd] text-[#6d28d9]" label={t('jwl2.advances')} value={formatRupees(list.reduce((n, o) => n + o.advance, 0))} />
        <Card tone={list.some((o) => o.late) ? 'bg-[#b45309] text-white' : 'bg-[#edf2f1] text-[#33423d]'} label={t('jwl2.lateOrders')} value={list.filter((o) => o.late).length} />
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <div className="flex gap-1 rounded-xl bg-[#edf2f1] p-1">
          {(['open', 'done'] as const).map((s) => <button key={s} onClick={() => setStatus(s)} aria-pressed={status === s} className={`min-h-9 rounded-lg px-3 text-sm font-semibold ${status === s ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted'}`}>{t(`jwl2.orderStatus.${s}`)}</button>)}
        </div>
        <span className="mr-auto" />
        <button onClick={() => setForm({ kind: 'repair', customerId: null, description: '', metal: 'gold', purity: 916, weight: '', fixedRate: '', dueOn: plusDays(3), advance: '' })} className={`min-h-11 rounded-xl px-4 ${btn.outline}`}>+ {t('jwl2.newRepair')}</button>
        <button onClick={() => setForm({ kind: 'order', customerId: null, description: '', metal: 'gold', purity: 916, weight: '', fixedRate: '', dueOn: plusDays(15), advance: '' })} className={`min-h-11 rounded-xl px-4 ${btn.solid}`}>+ {t('jwl2.newOrder')}</button>
      </div>
      {list.length === 0 && <p className="rounded-card bg-white p-6 text-center text-muted shadow-sm">{t('jwl2.noOrders')}</p>}
      {list.map((o) => (
        <section key={o.id} className="flex flex-wrap items-start gap-3 rounded-card bg-white p-4 shadow-sm">
          <span className={`rounded-full px-2.5 py-0.5 text-xs font-bold ${o.kind === 'order' ? 'bg-[#e3f2ee] text-[#0b5e4f]' : 'bg-[#e8effd] text-[#1d4ed8]'}`}>{t(`jwl2.kind.${o.kind}`)} #{o.number}</span>
          <span className="flex min-w-0 flex-1 basis-64 flex-col gap-0.5">
            <b>{o.description}</b>
            <span className="text-sm text-muted">{o.customer}{o.phone ? ` · ${o.phone}` : ''}</span>
            <span className="text-xs text-muted">{[o.metal && o.purity ? (o.metal === 'gold' ? karat(o.metal, o.purity) : `${t('jwl.silver')} ${o.purity}`) : null, o.weight ? `${o.kind === 'repair' ? t('jwl2.weightIn') : t('jwl2.approx')} ${grams(o.weight)}` : null,
              o.weightOut ? `${t('jwl2.weightOut')} ${grams(o.weightOut)}` : null, o.fixedRate ? t('jwl2.rateFixed', { rate: formatRupees(o.fixedRate) }) : o.kind === 'order' ? t('jwl2.rateOnDelivery') : null].filter(Boolean).join(' · ')}</span>
          </span>
          <span className="flex flex-col items-end gap-1 text-sm">
            <span className={o.late ? 'rounded-full bg-due-soft px-2.5 py-0.5 text-xs font-bold text-due' : 'text-muted'}>{t('jwl2.dueOn', { date: date(o.dueOn) })}</span>
            {o.advance > 0 && <span>{t('jwl2.advancePaid', { amount: formatRupees(o.advance) })}</span>}
            <span className="rounded-full bg-[#edf2f1] px-2.5 py-0.5 text-xs font-semibold">{t(`jwl2.state.${o.status}`)}</span>
          </span>
          {(o.status === 'booked' || o.status === 'ready') && (
            <div className="flex w-full flex-wrap gap-2 border-t border-line pt-3">
              <button onClick={() => { setAct({ order: o, what: 'advance' }); setAmount('') }} className={`min-h-9 rounded-xl px-3 text-sm ${btn.outline}`}>{t('jwl2.takeAdvance')}</button>
              {o.status === 'booked' && <button disabled={busy} onClick={() => void run(() => api(`/v1/jewellery/orders/${o.id}/status`, 'POST', { status: 'ready' })).then(() => refresh())} className="min-h-9 rounded-xl border border-line px-3 text-sm font-semibold">{t('jwl2.markReady')}</button>}
              {o.kind === 'repair'
                ? <button onClick={() => { setAct({ order: o, what: 'deliver' }); setAmount(o.charge ? String(o.charge) : ''); setWeightOut(o.weight ? String(o.weight) : '') }} className={`min-h-9 rounded-xl px-3 text-sm ${btn.solid}`}>{t('jwl2.deliverRepair')}</button>
                : <button onClick={() => navigate('/jewellery')} className={`min-h-9 rounded-xl px-3 text-sm ${btn.solid}`} title={t('jwl2.billOrderHint')}>{t('jwl2.billOrder')}</button>}
              {o.kind === 'order' && <button disabled={busy} onClick={() => void run(() => api(`/v1/jewellery/orders/${o.id}/status`, 'POST', { status: 'delivered' })).then(() => refresh())} className="min-h-9 rounded-xl border border-line px-3 text-sm font-semibold">{t('jwl2.markDelivered')}</button>}
              <button disabled={busy} onClick={() => void run(() => api(`/v1/jewellery/orders/${o.id}/status`, 'POST', { status: 'cancelled' })).then(() => refresh())} className={`ml-auto min-h-9 text-sm ${btn.dangerOutline}`}>{t('cancel')}</button>
            </div>
          )}
        </section>
      ))}
      {error && <p className="text-sm font-semibold text-due">{error}</p>}

      {form && (
        <Drawer title={form.kind === 'order' ? t('jwl2.newOrder') : t('jwl2.newRepair')} onClose={() => setForm(null)}>
          <WhoField shopId={shopId} value={form.customerId} onChange={(id) => setForm({ ...form, customerId: id })} />
          <Field label={form.kind === 'order' ? t('jwl2.whatToMake') : t('jwl2.whatToRepair')}><input value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} maxLength={200} className={box} /></Field>
          <div className="grid grid-cols-3 gap-2">
            <Field label={t('jwl.metal')}><select value={form.metal} onChange={(e) => setForm({ ...form, metal: e.target.value as Metal, purity: e.target.value === 'gold' ? 916 : 925 })} className={box}><option value="gold">{t('jwl.gold')}</option><option value="silver">{t('jwl.silver')}</option></select></Field>
            <Field label={form.kind === 'order' ? t('jwl2.approxG') : t('jwl2.weightInG')}><input value={form.weight} onChange={(e) => setForm({ ...form, weight: num(e.target.value) })} inputMode="decimal" className={box} /></Field>
            <Field label={t('jwl2.deliverBy')}><input type="date" value={form.dueOn} min={today()} onChange={(e) => setForm({ ...form, dueOn: e.target.value })} className={box} /></Field>
          </div>
          {form.kind === 'order' && <Field label={t('jwl2.fixRate')}><input value={form.fixedRate} onChange={(e) => setForm({ ...form, fixedRate: num(e.target.value) })} inputMode="decimal" placeholder={t('jwl2.fixRatePlaceholder')} className={box} /></Field>}
          <Field label={t('jwl2.advanceNow')}><input value={form.advance} onChange={(e) => setForm({ ...form, advance: num(e.target.value) })} inputMode="decimal" placeholder="0" className={box} /></Field>
          {error && <p className="text-sm font-semibold text-due">{error}</p>}
          <Button busy={busy} disabled={!form.customerId || !form.description.trim()} onClick={() => void run(async () => {
            const o = await api<Order>('/v1/jewellery/orders', 'POST', { kind: form.kind, customerId: form.customerId, description: form.description, metal: form.metal, purity: form.purity,
              weight: Number(form.weight) || null, fixedRate: Number(form.fixedRate) || null, dueOn: form.dueOn })
            if (Number(form.advance) > 0) await api(`/v1/jewellery/orders/${o.id}/advance`, 'POST', { entryId: crypto.randomUUID(), amount: Number(form.advance), mode: 'cash' })
          }).then((ok) => { if (ok) { setForm(null); void refresh() } })}>{t('save')}</Button>
        </Drawer>
      )}
      {act && (
        <Drawer title={act.what === 'advance' ? t('jwl2.takeAdvance') : t('jwl2.deliverRepair')} onClose={() => setAct(null)}>
          {act.what === 'deliver' && <Field label={t('jwl2.weightOutG')}><input value={weightOut} onChange={(e) => setWeightOut(num(e.target.value))} inputMode="decimal" className={box} /></Field>}
          <Field label={act.what === 'advance' ? t('jwl.received') : t('jwl2.repairCharge')}><input value={amount} onChange={(e) => setAmount(num(e.target.value))} inputMode="decimal" className={`${box} text-lg font-bold`} /></Field>
          {act.what === 'deliver' && Number(amount) > 0 && <p className="text-sm">{t('jwl2.repairTotal', { total: formatRupees(Math.round(Number(amount) * 1.18)) })}</p>}
          {error && <p className="text-sm font-semibold text-due">{error}</p>}
          <Button busy={busy} disabled={!(Number(amount) > 0)} onClick={() => void run(async () => {
            if (act.what === 'advance') await api(`/v1/jewellery/orders/${act.order.id}/advance`, 'POST', { entryId: crypto.randomUUID(), amount: Number(amount), mode: 'cash' })
            else {
              const done = await api<Order>(`/v1/jewellery/orders/${act.order.id}/deliver-repair`, 'POST', { billId: crypto.randomUUID(), weightOut: Number(weightOut) || null, charge: Number(amount), moneyPaid: Math.max(0, Math.round(Number(amount) * 1.18) - act.order.advance), mode: 'cash' })
              if (done.billId) navigate(`/print/bill/${done.billId}`)
            }
          }).then((ok) => { if (ok) { setAct(null); void refresh() } })}>{t('save')}</Button>
        </Drawer>
      )}
    </div>
  )
}

/* ---------------- approval memos ---------------- */

interface Approval { id: string; number: number; customerId: string | null; party: string; outAt: string; dueBack: string; closedOn: string | null; late: boolean; items: { pieceId: string; tagNo: number; name: string; grossOut: number; grossBack: number | null; returned: boolean; sold: boolean; weightDiffers: boolean }[] }

export function ApprovalsPanel({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const list = useQuery({ queryKey: [shopId, 'jewellery', 'approvals'], queryFn: () => api<Approval[]>('/v1/jewellery/approvals?status=all') }).data ?? []
  const stock = useQuery({ queryKey: [shopId, 'jewellery', 'pieces', 'in', ''], queryFn: () => api<JewelleryPiece[]>('/v1/jewellery/pieces?status=in&q=') }).data ?? []
  const [sending, setSending] = useState<{ customerId: string | null; party: string; days: string; pieceIds: string[] } | null>(null)
  const [returning, setReturning] = useState<{ memo: Approval; pieceId: string; weight: string } | null>(null)
  const { busy, error, run } = useRun()
  const refresh = () => queryClient.invalidateQueries({ queryKey: [shopId, 'jewellery'] })
  const open = list.filter((m) => !m.closedOn)
  const free = stock.filter((p) => !p.onApproval)

  return (
    <div className="flex flex-col gap-3">
      <div className="grid grid-cols-3 gap-2">
        <Card tone="bg-[#e3f2ee] text-[#0b5e4f]" label={t('jwl2.memosOut')} value={open.length} />
        <Card tone="bg-[#e8effd] text-[#1d4ed8]" label={t('jwl2.piecesOut')} value={open.reduce((n, m) => n + m.items.filter((i) => !i.returned && !i.sold).length, 0)} />
        <Card tone={open.some((m) => m.late) ? 'bg-[#b45309] text-white' : 'bg-[#edf2f1] text-[#33423d]'} label={t('jwl2.lateBack')} value={open.filter((m) => m.late).length} />
      </div>
      <button onClick={() => setSending({ customerId: null, party: '', days: '3', pieceIds: [] })} className={`min-h-11 self-end rounded-xl px-4 ${btn.solid}`}>+ {t('jwl2.sendOnApproval')}</button>
      {list.length === 0 && <p className="rounded-card bg-white p-6 text-center text-muted shadow-sm">{t('jwl2.noMemos')}</p>}
      {list.map((m) => (
        <section key={m.id} className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
          <div className="flex flex-wrap items-center gap-2">
            <b>{t('jwl2.memo')} #{m.number} · {m.party}</b>
            <span className={`ml-auto rounded-full px-2.5 py-0.5 text-xs font-bold ${m.closedOn ? 'bg-[#edf2f1] text-muted' : m.late ? 'bg-due-soft text-due' : 'bg-[#e3f2ee] text-[#0b5e4f]'}`}>{m.closedOn ? t('jwl2.memoClosed') : t('jwl2.backBy', { date: date(m.dueBack) })}</span>
          </div>
          {m.items.map((i) => (
            <div key={i.pieceId} className="flex flex-wrap items-center gap-2 border-t border-line pt-2 text-sm">
              <span className="font-mono font-bold">J-{i.tagNo}</span><span className="flex-1">{i.name} · {grams(i.grossOut)}</span>
              {i.sold && <span className="rounded-full bg-[#f1eafd] px-2.5 py-0.5 text-xs font-bold text-[#6d28d9]">{t('jwl2.sold')}</span>}
              {i.returned && <span className={`rounded-full px-2.5 py-0.5 text-xs font-bold ${i.weightDiffers ? 'bg-due-soft text-due' : 'bg-[#e3f2ee] text-[#0b5e4f]'}`}>{i.weightDiffers ? t('jwl2.backDifferent', { weight: grams(i.grossBack ?? 0) }) : t('jwl2.back')}</span>}
              {!i.sold && !i.returned && <button onClick={() => setReturning({ memo: m, pieceId: i.pieceId, weight: '' })} className={`min-h-9 rounded-xl px-3 text-sm ${btn.outline}`}>{t('jwl2.cameBack')}</button>}
            </div>
          ))}
        </section>
      ))}
      {error && <p className="text-sm font-semibold text-due">{error}</p>}

      {sending && (
        <Drawer title={t('jwl2.sendOnApproval')} onClose={() => setSending(null)}>
          <WhoField shopId={shopId} value={sending.customerId} onChange={(id) => setSending({ ...sending, customerId: id })} />
          <Field label={t('jwl2.backInDays')}><input value={sending.days} onChange={(e) => setSending({ ...sending, days: num(e.target.value) })} inputMode="numeric" className={`${box} w-24`} /></Field>
          <b className="text-sm">{t('jwl2.choosePieces')}</b>
          <div className="flex max-h-72 flex-col gap-1 overflow-y-auto rounded-xl border border-line p-2">
            {free.length === 0 && <p className="p-2 text-sm text-muted">{t('jwl.noPieces')}</p>}
            {free.map((p) => (
              <label key={p.id} className="flex min-h-10 items-center gap-2 text-sm"><input type="checkbox" className="h-5 w-5" checked={sending.pieceIds.includes(p.id)}
                onChange={(e) => setSending({ ...sending, pieceIds: e.target.checked ? [...sending.pieceIds, p.id] : sending.pieceIds.filter((x) => x !== p.id) })} />
                <span className="font-mono font-bold">J-{p.tagNo}</span>{p.name} · {grams(p.gross)}</label>
            ))}
          </div>
          {error && <p className="text-sm font-semibold text-due">{error}</p>}
          <Button busy={busy} disabled={!sending.customerId || sending.pieceIds.length === 0} onClick={() => void run(() => api('/v1/jewellery/approvals', 'POST', { customerId: sending.customerId, pieceIds: sending.pieceIds, days: Number(sending.days) || 3 }))
            .then((ok) => { if (ok) { setSending(null); void refresh() } })}>{t('jwl2.send', { count: sending.pieceIds.length })}</Button>
        </Drawer>
      )}
      {returning && (
        <Drawer title={t('jwl2.cameBack')} onClose={() => setReturning(null)}>
          <Field label={t('jwl2.weighAgain')}><input autoFocus value={returning.weight} onChange={(e) => setReturning({ ...returning, weight: num(e.target.value) })} inputMode="decimal" placeholder="0.000" className={`${box} text-lg font-bold`} /></Field>
          {error && <p className="text-sm font-semibold text-due">{error}</p>}
          <Button busy={busy} disabled={!(Number(returning.weight) > 0)} onClick={() => void run(() => api(`/v1/jewellery/approvals/${returning.memo.id}/return`, 'POST', { pieceId: returning.pieceId, grossBack: Number(returning.weight) }))
            .then((ok) => { if (ok) { setReturning(null); void refresh() } })}>{t('save')}</Button>
        </Drawer>
      )}
    </div>
  )
}

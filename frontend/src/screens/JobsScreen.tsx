import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { useCustomers, useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'
import { ExportImportPanel } from './ExportImportPanel'
import { KarigarPanel, MachinesPanel } from './WorkshopPanels'

type Status = 'quote' | 'order' | 'making' | 'ready' | 'delivered' | 'cancelled'
interface Job {
  id: string; number: number; customerId: string | null; customerName: string | null; title: string; status: Status; price: number
  estimatedCost: number | null; dueOn: string | null; note: string | null; billId: string | null
  material: number; labour: number; other: number; cost: number; advance: number; profit: number
}
interface Entry { id: string; kind: 'material' | 'labour' | 'other' | 'advance'; description: string; quantity: number | null; amount: number; occurredAt: string }

const NEXT: Partial<Record<Status, Status>> = { quote: 'order', order: 'making', making: 'ready' }

/**
 * Work made to order (furniture, fittings): each job from quote to delivery, with what went into it and what it
 * earned. Built on the shop's khata (advances), stock (materials) and kharcha book (wages, other spending).
 */
export function JobsScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const [tab, setTab] = useState<'jobs' | 'machines' | 'karigar' | 'trade'>('jobs')
  const [done, setDone] = useState(false)
  const [openId, setOpenId] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)
  const jobs = useQuery({ queryKey: [shopId, 'jobs', done], queryFn: () => api<Job[]>(`/v1/jobs${done ? '?done=true' : ''}`) })

  if (openId) return <JobPage shopId={shopId} id={openId} onBack={() => setOpenId(null)} />
  const chip = (on: boolean) => `min-h-11 flex-1 rounded-full text-sm font-semibold ${on ? 'bg-teal-700 text-white' : 'border border-line bg-white'}`
  const tabs = isOwner && (
    <div className="flex gap-2 rounded-full bg-white p-1">
      {(['jobs', 'machines', 'karigar', 'trade'] as const).map((x) => <button key={x} onClick={() => setTab(x)} className={chip(tab === x)}>{t(`workshop.tab.${x}`)}</button>)}
    </div>
  )
  if (tab !== 'jobs') {
    return <div className="flex flex-col gap-3 p-4 pb-28">{tabs}{tab === 'machines' ? <MachinesPanel shopId={shopId} /> : tab === 'karigar' ? <KarigarPanel shopId={shopId} /> : <ExportImportPanel />}</div>
  }
  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      {tabs}
      <p className="text-sm text-muted">{t('jobs.hint')}</p>
      <div className="flex gap-2">
        <button onClick={() => setDone(false)} className={chip(!done)}>{t('jobs.open')}</button>
        <button onClick={() => setDone(true)} className={chip(done)}>{t('jobs.closed')}</button>
      </div>
      {!done && (adding ? <NewJob shopId={shopId} onDone={(id) => { setAdding(false); if (id) setOpenId(id) }} /> : <Button onClick={() => setAdding(true)}>{t('jobs.new')}</Button>)}
      {jobs.isLoading && <p className="text-muted">…</p>}
      {jobs.data?.length === 0 && <p className="rounded-[18px] bg-white p-4 text-center text-muted">{t('jobs.empty')}</p>}
      <ul className="flex flex-col gap-2">
        {jobs.data?.map((j) => (
          <li key={j.id}>
            <button onClick={() => setOpenId(j.id)} className="flex w-full items-center gap-3 rounded-[18px] bg-white p-3 text-left">
              <span className="min-w-0 flex-1">
                <span className="block font-semibold">#{j.number} {j.title}</span>
                <span className="block text-xs text-muted">{t(`jobs.status.${j.status}`)}{j.customerName ? ` · ${j.customerName}` : ''}{j.dueOn ? ` · ${t('jobs.due', { date: j.dueOn })}` : ''}</span>
              </span>
              <span className="text-right">
                <span className="block font-bold">{formatRupees(j.price)}</span>
                <span className={`block text-xs font-semibold ${j.profit < 0 ? 'text-due' : 'text-teal-700'}`}>{t('jobs.profitShort', { amount: formatRupees(j.profit) })}</span>
              </span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  )
}

function NewJob({ shopId, onDone }: { shopId: string; onDone: (id?: string) => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const { customers } = useCustomers(shopId)
  const [d, setD] = useState({ title: '', customerId: '', price: '', cost: '', dueOn: '', quote: false })
  const [message, setMessage] = useState('')

  async function save() {
    setMessage('')
    const id = crypto.randomUUID()
    try {
      await api('/v1/jobs', 'POST', {
        id, title: d.title.trim(), customerId: d.customerId || null, price: Number(d.price) || 0, estimatedCost: d.cost ? Number(d.cost) : null,
        dueOn: d.dueOn || null, status: d.quote ? 'quote' : 'order',
      })
      void queryClient.invalidateQueries({ queryKey: [shopId, 'jobs'] })
      onDone(id)
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  return (
    <div className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
      <Field label={t('jobs.name')} value={d.title} onChange={(e) => setD({ ...d, title: e.target.value })} placeholder={t('jobs.titleHint')} />
      <label className="block">
        <span className="mb-1 block text-sm font-semibold text-muted">{t('jobs.customer')}</span>
        <select value={d.customerId} onChange={(e) => setD({ ...d, customerId: e.target.value })} className="h-12 w-full rounded-xl border border-line bg-white px-3">
          <option value="">{t('jobs.noCustomer')}</option>
          {customers.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
      </label>
      <div className="grid grid-cols-2 gap-2">
        <Field label={t('jobs.price')} value={d.price} onChange={(e) => setD({ ...d, price: e.target.value })} inputMode="decimal" />
        <Field label={t('jobs.estimate')} value={d.cost} onChange={(e) => setD({ ...d, cost: e.target.value })} inputMode="decimal" />
      </div>
      <Field label={t('jobs.dueOn')} type="date" value={d.dueOn} onChange={(e) => setD({ ...d, dueOn: e.target.value })} />
      <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={d.quote} onChange={(e) => setD({ ...d, quote: e.target.checked })} className="h-5 w-5" />{t('jobs.onlyQuote')}</label>
      {message && <p className="text-sm text-due">{message}</p>}
      <div className="flex gap-2">
        <Button tone="plain" className="flex-1" onClick={() => onDone()}>{t('cancel')}</Button>
        <Button className="flex-1" disabled={d.title.trim().length < 2} onClick={() => void save()}>{t('save')}</Button>
      </div>
    </div>
  )
}

/** One job's page. <paramref>embedded</paramref>: shown inside a hall booking, which has its own back, stages, final bill and cancel. */
export function JobPage({ shopId, id, onBack, embedded = false }: { shopId: string; id: string; onBack?: () => void; embedded?: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const detail = useQuery({ queryKey: [shopId, 'job', id], queryFn: () => api<{ job: Job; entries: Entry[]; overhead: number }>(`/v1/jobs/${id}`) })
  const { products } = useProducts(shopId)
  const [form, setForm] = useState<'advance' | 'material' | 'labour' | 'other' | 'deliver' | null>(null)
  const [f, setF] = useState({ amount: '', text: '', productId: '', qty: '', mode: 'cash' })
  const [message, setMessage] = useState('')
  const job = detail.data?.job
  if (!job) return <p className="p-4 text-muted">…</p>

  const open = job.status !== 'delivered' && job.status !== 'cancelled'
  async function run(path: string, body: unknown) {
    setMessage('')
    try {
      await api(`/v1/jobs/${id}${path}`, path === '/status' ? 'PUT' : 'POST', body)
      setForm(null)
      setF({ amount: '', text: '', productId: '', qty: '', mode: 'cash' })
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  const submit = () => {
    const key = crypto.randomUUID()
    if (form === 'advance') return run('/advance', { id: key, amount: Number(f.amount), mode: f.mode })
    if (form === 'material') return run('/material', { id: key, productId: f.productId, quantity: Number(f.qty) })
    if (form === 'labour' || form === 'other') return run(`/${form}`, { id: key, description: f.text.trim(), amount: Number(f.amount), mode: f.mode })
    if (form === 'deliver') return run('/deliver', { billId: key, paidAmount: Number(f.amount) || 0, mode: f.mode })
  }
  const due = Math.max(0, job.price - job.advance)
  const line = (label: string, value: number, strong = false) => (
    <div className={`flex justify-between ${strong ? 'font-bold' : ''}`}><span>{label}</span><span>{formatRupees(value)}</span></div>
  )
  const modes = (
    <select value={f.mode} onChange={(e) => setF({ ...f, mode: e.target.value })} aria-label={t('jobs.mode')} className="h-12 rounded-xl border border-line bg-white px-3">
      <option value="cash">{t('cash')}</option><option value="upi">UPI</option>
    </select>
  )

  return (
    <div className={embedded ? "flex flex-col gap-3" : "flex flex-col gap-3 p-4 pb-28"}>
      {!embedded && <button onClick={onBack} className="min-h-11 self-start font-semibold text-teal-700">← {t('jobs.all')}</button>}
      <div className="rounded-[18px] bg-white p-4">
        <h2 className="text-lg font-bold">#{job.number} {job.title}</h2>
        <p className="text-sm text-muted">{t(`jobs.status.${job.status}`)}{job.customerName ? ` · ${job.customerName}` : ''}{job.dueOn ? ` · ${t('jobs.due', { date: job.dueOn })}` : ''}</p>
        <div className="mt-3 flex flex-col gap-1 text-sm">
          {line(t('jobs.price'), job.price, true)}
          {line(t('jobs.materialCost'), job.material)}
          {line(t('jobs.labourCost'), job.labour)}
          {line(t('jobs.otherCost'), job.other)}
          {line(t('jobs.totalCost'), job.cost, true)}
          {job.estimatedCost != null && line(t('jobs.estimate'), job.estimatedCost)}
          <div className={`flex justify-between text-base font-bold ${job.profit < 0 ? 'text-due' : 'text-teal-700'}`}><span>{t('jobs.profit')}</span><span>{formatRupees(job.profit)}</span></div>
          {(detail.data?.overhead ?? 0) > 0 && (
            <>
              {line(t('jobs.overhead'), detail.data!.overhead)}
              {line(t('jobs.profitAfter'), job.profit - detail.data!.overhead, true)}
            </>
          )}
          {line(t('jobs.advance'), job.advance)}
        </div>
        <p className="mt-2 text-xs text-muted">{t((detail.data?.overhead ?? 0) > 0 ? 'jobs.overheadHint' : 'jobs.costHint')}</p>
      </div>

      {open && (
        <div className="grid grid-cols-2 gap-2">
          {job.customerId && <Button tone="payment" onClick={() => setForm('advance')}>{t('jobs.takeAdvance')}</Button>}
          <Button tone="plain" onClick={() => setForm('material')}>{t('jobs.useMaterial')}</Button>
          <Button tone="plain" onClick={() => setForm('labour')}>{t('jobs.payKarigar')}</Button>
          <Button tone="plain" onClick={() => setForm('other')}>{t('jobs.otherSpend')}</Button>
          {!embedded && NEXT[job.status] && <Button tone="plain" onClick={() => void run('/status', { status: NEXT[job.status] })}>{t('jobs.moveTo', { status: t(`jobs.status.${NEXT[job.status]}`) })}</Button>}
          {!embedded && <Button onClick={() => { setForm('deliver'); setF({ ...f, amount: String(job.customerId ? 0 : job.price) }) }}>{t('jobs.deliver')}</Button>}
        </div>
      )}

      {form && (
        <div className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
          <h3 className="font-bold">{t(`jobs.form.${form}`)}</h3>
          {form === 'material' && (
            <>
              <select value={f.productId} onChange={(e) => setF({ ...f, productId: e.target.value })} aria-label={t('jobs.item')} className="h-12 rounded-xl border border-line bg-white px-3">
                <option value="">{t('jobs.item')}</option>
                {products.map((p) => <option key={p.id} value={p.id}>{p.name} ({t(`units.${p.unit}`)}{p.costPrice != null ? `, ${formatRupees(p.costPrice)}` : ''})</option>)}
              </select>
              <Field label={t('jobs.qty')} value={f.qty} onChange={(e) => setF({ ...f, qty: e.target.value })} inputMode="decimal" />
            </>
          )}
          {(form === 'labour' || form === 'other') && (
            <Field label={t(form === 'labour' ? 'jobs.karigar' : 'jobs.what')} value={f.text} onChange={(e) => setF({ ...f, text: e.target.value })} placeholder={form === 'labour' ? t('jobs.karigarHint') : ''} />
          )}
          {form === 'deliver' && <p className="text-sm text-muted">{t('jobs.deliverHint', { price: formatRupees(job.price), due: formatRupees(due) })}</p>}
          {form !== 'material' && (
            <div className="grid grid-cols-2 gap-2">
              <Field label={t(form === 'deliver' ? 'jobs.paidNow' : 'jobs.amount')} value={f.amount} onChange={(e) => setF({ ...f, amount: e.target.value })} inputMode="decimal" />
              {modes}
            </div>
          )}
          {message && <p className="text-sm text-due">{message}</p>}
          <div className="flex gap-2">
            <Button tone="plain" className="flex-1" onClick={() => setForm(null)}>{t('cancel')}</Button>
            <Button className="flex-1" onClick={() => void submit()}>{t('save')}</Button>
          </div>
        </div>
      )}
      {!form && message && <p className="text-sm text-due">{message}</p>}

      <ul className="flex flex-col gap-1 rounded-[18px] bg-white p-2">
        {detail.data?.entries.map((e) => (
          <li key={e.id} className="flex min-h-11 items-center justify-between px-2 text-sm">
            <span>{t(`jobs.kind.${e.kind}`)} · {e.description}{e.quantity != null ? ` × ${e.quantity}` : ''}</span>
            <span className={e.kind === 'advance' ? 'font-semibold text-teal-700' : ''}>{formatRupees(e.amount)}</span>
          </li>
        ))}
      </ul>
      {open && !embedded && <button onClick={() => { if (confirm(t('jobs.cancelConfirm'))) void run('/status', { status: 'cancelled' }) }} className="min-h-11 text-sm font-semibold text-due">{t('jobs.cancelJob')}</button>}
    </div>
  )
}

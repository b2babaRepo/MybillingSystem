import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api, request } from '../lib/api'
import { useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'
import type { Unit } from '../lib/types'
import { confirmAsk } from '../components/Ask'

interface Machine { id: string; name: string; boughtOn: string; cost: number; ratePercent: number; disposedOn: string | null; thisYear: number; bookValue: number }
interface Machines { financialYear: string; thisYear: number; perMonth: number; machines: Machine[] }

/** The workshop's machines and how much they wear this year: a cost in profit and in each job's running costs. */
export function MachinesPanel({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const list = useQuery({ queryKey: [shopId, 'machines'], queryFn: () => api<Machines>('/v1/workshop/machines') })
  const [d, setD] = useState({ name: '', boughtOn: '', cost: '' })
  const [message, setMessage] = useState('')

  async function run(path: string, body: unknown) {
    setMessage('')
    try {
      await api(path, 'POST', body)
      setD({ name: '', boughtOn: '', cost: '' })
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  return (
    <div className="flex flex-col gap-3">
      {list.data && (
        <div className="rounded-[18px] bg-white p-4">
          <p className="font-bold">{t('workshop.wearYear', { year: list.data.financialYear, amount: formatRupees(list.data.thisYear) })}</p>
          <p className="text-sm text-muted">{t('workshop.wearMonth', { amount: formatRupees(list.data.perMonth) })}</p>
          <p className="mt-1 text-xs text-muted">{t('workshop.wearHint')}</p>
        </div>
      )}
      <ul className="flex flex-col gap-1 rounded-[18px] bg-white p-2">
        {list.data?.machines.map((m) => (
          <li key={m.id} className="flex min-h-12 items-center gap-2 px-2">
            <span className="min-w-0 flex-1">
              <span className="block font-semibold">{m.name}</span>
              <span className="block text-xs text-muted">{t('workshop.machineLine', { cost: formatRupees(m.cost), date: m.boughtOn, value: formatRupees(m.bookValue), rate: m.ratePercent })}{m.disposedOn ? ` · ${t('workshop.disposed')}` : ''}</span>
            </span>
            <span className="text-sm font-semibold">{formatRupees(m.thisYear)}</span>
            {!m.disposedOn && <button onClick={async () => { if ((await confirmAsk(t('workshop.disposeConfirm')))) void run(`/v1/workshop/machines/${m.id}/dispose`, {}) }} className="min-h-11 px-2 text-sm font-semibold text-due">{t('workshop.dispose')}</button>}
          </li>
        ))}
        {list.data?.machines.length === 0 && <li className="p-3 text-center text-muted">{t('workshop.noMachines')}</li>}
      </ul>
      <div className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
        <h3 className="font-bold">{t('workshop.addMachine')}</h3>
        <Field label={t('workshop.machineName')} value={d.name} onChange={(e) => setD({ ...d, name: e.target.value })} placeholder={t('workshop.machineHint')} />
        <div className="grid grid-cols-2 gap-2">
          <Field label={t('workshop.boughtOn')} type="date" value={d.boughtOn} onChange={(e) => setD({ ...d, boughtOn: e.target.value })} />
          <Field label={t('workshop.cost')} value={d.cost} onChange={(e) => setD({ ...d, cost: e.target.value })} inputMode="decimal" />
        </div>
        {message && <p className="text-sm text-due">{message}</p>}
        <Button disabled={d.name.trim().length < 2 || !d.boughtOn || !d.cost}
          onClick={() => void run('/v1/workshop/machines', { id: crypto.randomUUID(), name: d.name.trim(), boughtOn: d.boughtOn, cost: Number(d.cost) })}>{t('save')}</Button>
      </div>
    </div>
  )
}

interface Challan {
  id: string; number: number; karigar: string; karigarGstin: string | null; productName: string; unit: Unit; quantity: number; unitCost: number
  note: string | null; sentAt: string; receivedQuantity: number | null; receivedAt: string | null; overAYear: boolean
}

/** Goods sent to outside karigars on numbered challans, and what came back: the register GST asks for (ITC-04). */
export function KarigarPanel({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const list = useQuery({ queryKey: [shopId, 'karigar'], queryFn: () => api<Challan[]>('/v1/workshop/karigar') })
  const { products } = useProducts(shopId)
  const [d, setD] = useState({ karigar: '', gstin: '', productId: '', qty: '', note: '' })
  const [backQty, setBackQty] = useState<Record<string, string>>({})
  const [message, setMessage] = useState('')

  async function run(path: string, body: unknown, reset = false) {
    setMessage('')
    try {
      await api(path, 'POST', body)
      if (reset) setD({ karigar: '', gstin: '', productId: '', qty: '', note: '' })
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  async function download() {
    const response = await request('/v1/workshop/itc04.csv')
    if (!response.ok) return setMessage(t('error'))
    const url = URL.createObjectURL(await response.blob())
    const a = document.createElement('a')
    a.href = url
    a.download = 'itc04.csv'
    a.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm text-muted">{t('workshop.karigarHint')}</p>
      <div className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
        <h3 className="font-bold">{t('workshop.send')}</h3>
        <div className="grid grid-cols-2 gap-2">
          <Field label={t('workshop.karigar')} value={d.karigar} onChange={(e) => setD({ ...d, karigar: e.target.value })} />
          <Field label={t('workshop.gstin')} value={d.gstin} onChange={(e) => setD({ ...d, gstin: e.target.value.toUpperCase() })} maxLength={15} />
        </div>
        <select value={d.productId} onChange={(e) => setD({ ...d, productId: e.target.value })} aria-label={t('jobs.item')} className="h-12 rounded-xl border border-line bg-white px-3">
          <option value="">{t('jobs.item')}</option>
          {products.map((p) => <option key={p.id} value={p.id}>{p.name} ({t(`units.${p.unit}`)})</option>)}
        </select>
        <div className="grid grid-cols-2 gap-2">
          <Field label={t('workshop.qtySent')} value={d.qty} onChange={(e) => setD({ ...d, qty: e.target.value })} inputMode="decimal" />
          <Field label={t('workshop.work')} value={d.note} onChange={(e) => setD({ ...d, note: e.target.value })} placeholder={t('workshop.workHint')} />
        </div>
        <Button disabled={d.karigar.trim().length < 2 || !d.productId || !d.qty}
          onClick={() => void run('/v1/workshop/karigar', { id: crypto.randomUUID(), karigar: d.karigar.trim(), karigarGstin: d.gstin.trim() || null, productId: d.productId, quantity: Number(d.qty), note: d.note.trim() || null }, true)}>
          {t('workshop.sendButton')}
        </Button>
      </div>
      {message && <p className="text-sm text-due">{message}</p>}
      <ul className="flex flex-col gap-2">
        {list.data?.map((c) => (
          <li key={c.id} className={`rounded-[18px] p-3 ${c.overAYear ? 'bg-amber-50' : 'bg-white'}`}>
            <p className="font-semibold">#{c.number} {c.karigar} · {c.productName} × {c.quantity} {t(`units.${c.unit}`)}</p>
            <p className="text-xs text-muted">
              {t('workshop.sentOn', { date: new Date(c.sentAt).toLocaleDateString('en-IN') })}{c.note ? ` · ${c.note}` : ''}
              {c.receivedAt ? ` · ${t('workshop.backOn', { date: new Date(c.receivedAt).toLocaleDateString('en-IN'), qty: c.receivedQuantity })}` : ''}
            </p>
            {c.overAYear && <p className="text-sm font-semibold text-due">{t('workshop.overAYear')}</p>}
            {!c.receivedAt && (
              <div className="mt-2 flex gap-2">
                <input value={backQty[c.id] ?? String(c.quantity)} onChange={(e) => setBackQty({ ...backQty, [c.id]: e.target.value })} inputMode="decimal"
                  aria-label={t('workshop.backQty')} className="h-11 w-24 rounded-xl border border-line px-3" />
                <Button tone="plain" className="flex-1" onClick={() => void run(`/v1/workshop/karigar/${c.id}/receive`, { quantity: Number(backQty[c.id] ?? c.quantity) })}>{t('workshop.receive')}</Button>
              </div>
            )}
          </li>
        ))}
        {list.data?.length === 0 && <li className="rounded-[18px] bg-white p-3 text-center text-muted">{t('workshop.noChallans')}</li>}
      </ul>
      <Button tone="plain" onClick={() => void download()}>{t('workshop.itc04')}</Button>
      <TdsPanel shopId={shopId} />
    </div>
  )
}

interface Tds { financialYear: string; singleLimit: number; yearLimit: number; karigars: { karigar: string; total: number; largest: number; payments: number; alert: 'single' | 'year' | 'near' | null }[] }

/** Wages paid to each karigar this year against the contractor TDS limits: a warning to take to the CA. */
export function TdsPanel({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const tds = useQuery({ queryKey: [shopId, 'tds'], queryFn: () => api<Tds>('/v1/workshop/tds') }).data
  if (!tds || tds.karigars.length === 0) return null
  return (
    <div className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
      <h3 className="font-bold">{t('workshop.tdsTitle', { year: tds.financialYear })}</h3>
      <p className="text-xs text-muted">{t('workshop.tdsHint', { single: formatRupees(tds.singleLimit), year: formatRupees(tds.yearLimit) })}</p>
      <ul className="flex flex-col gap-1">
        {tds.karigars.map((k) => (
          <li key={k.karigar} className={`rounded-xl px-2 py-2 ${k.alert === 'single' || k.alert === 'year' ? 'bg-amber-50' : ''}`}>
            <div className="flex justify-between font-semibold"><span>{k.karigar}</span><span>{formatRupees(k.total)}</span></div>
            <p className="text-xs text-muted">{t('workshop.tdsLine', { count: k.payments, largest: formatRupees(k.largest) })}</p>
            {k.alert && <p className={`text-sm font-semibold ${k.alert === 'near' ? 'text-muted' : 'text-due'}`}>{t(`workshop.tdsAlert.${k.alert}`)}</p>}
          </li>
        ))}
      </ul>
    </div>
  )
}

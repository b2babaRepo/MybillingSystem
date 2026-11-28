import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { DataTable } from '../components/DataTable'
import { shownError } from '../lib/notify'

interface Expiring { id: string; productName: string; batchNo: string; expiry: string; quantity: number; value: number; expired: boolean; daysLeft: number }
interface Return {
  id: string; number: number; supplierName: string; value: number; daysOpen: number; creditNoteNo: string | null; creditAmount: number | null; settledAt: string | null; loss: number
  lines: { productName: string; batchNo: string; quantity: number; value: number }[]
}

const month = (iso: string) => new Date(`${iso}T00:00`).toLocaleDateString(shownLocale(), { month: 'short', year: 'numeric' })
const select = 'h-12 w-full rounded-xl border border-line bg-white px-3'

/** Expiry returns: send near-expiry batches back to a distributor, then settle each return when its credit note comes. */
export function ReturnsScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const suppliers = useQuery({ queryKey: [shopId, 'suppliers'], queryFn: () => api<{ id: string; name: string }[]>('/v1/suppliers') }).data ?? []
  const expiring = useQuery({ queryKey: [shopId, 'batches', 'expiring', 120], queryFn: () => api<Expiring[]>('/v1/batches/expiring?days=120') }).data ?? []
  const returns = useQuery({ queryKey: [shopId, 'returns'], queryFn: () => api<Return[]>('/v1/suppliers/returns?all=true') }).data ?? []
  const open = returns.filter((r) => !r.settledAt)
  const [supplierId, setSupplierId] = useState('')
  const [picked, setPicked] = useState<Record<string, string>>({})
  const [credit, setCredit] = useState<Record<string, { no: string; amount: string }>>({})
  const [message, setMessage] = useState('')

  async function run(work: () => Promise<unknown>, done: string) {
    setMessage('')
    try {
      await work()
      setMessage(done)
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setMessage(shownError(e))
    }
  }
  const lines = Object.entries(picked).filter(([, q]) => Number(q) > 0).map(([batchId, q]) => ({ batchId, quantity: Number(q) }))

  return (
    <div className="flex flex-col gap-3 p-4 pb-28 lg:mx-auto lg:w-full lg:max-w-5xl">
      {message && <p className="rounded-xl bg-brand-soft p-3 text-sm font-semibold text-brand-deep">{message}</p>}
      <section className="flex flex-col gap-2 rounded-card bg-white p-3">
        <h3 className="font-bold">{t('returns.title')}</h3>
        <p className="text-xs text-muted">{t('returns.hint')}</p>
        <select value={supplierId} onChange={(e) => setSupplierId(e.target.value)} aria-label={t('returns.supplier')} className={select}>
          <option value="">{t('returns.chooseSupplier')}</option>{suppliers.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
        </select>
        {expiring.length === 0 && <p className="text-sm text-muted">{t('batches.noneExpiring')}</p>}
        {expiring.length > 0 && (
          <table className="hidden w-full border-collapse overflow-hidden rounded-xl text-sm lg:table">
            <thead className="bg-brand-soft text-left text-xs uppercase tracking-wide">
              <tr><th className="px-2 py-2">{t('batches.medicine')}</th><th className="w-32 px-2 py-2">{t('batches.batchNo')}</th><th className="w-28 px-2 py-2">{t('batches.expiry')}</th>
                <th className="w-20 px-2 py-2 text-right">{t('batches.quantity')}</th><th className="w-28 px-2 py-2 text-right">{t('stockTable.value')}</th><th className="w-28 px-2 py-2 text-right">{t('returns.qty')}</th></tr>
            </thead>
            <tbody className="divide-y divide-line">
              {expiring.map((e) => (
                <tr key={e.id} className={e.expired ? 'bg-due-soft/60' : ''}>
                  <td className="px-2 py-1.5 font-semibold">{e.productName}</td>
                  <td className="px-2 py-1.5 font-mono text-xs">{e.batchNo}</td>
                  <td className="px-2 py-1.5">{month(e.expiry)}{e.expired ? ` · ${t('batches.expired')}` : ''}</td>
                  <td className="px-2 py-1.5 text-right">{e.quantity}</td>
                  <td className="px-2 py-1.5 text-right">{formatRupees(e.value)}</td>
                  <td className="px-2 py-1 text-right">
                    <input aria-label={`${t('returns.qty')}: ${e.productName} ${e.batchNo}`} value={picked[e.id] ?? ''} inputMode="decimal"
                      onChange={(ev) => setPicked({ ...picked, [e.id]: ev.target.value })} className="h-9 w-24 rounded-lg border border-line px-2 text-right" />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
        {expiring.map((e) => (
          <div key={e.id} className={`lg:hidden grid grid-cols-[1fr_6rem] items-center gap-2 rounded-xl p-2 text-sm ${e.expired ? 'bg-due-soft' : 'bg-warn-soft'}`}>
            <span><b>{e.productName}</b><span className="block text-xs">{t('batches.batchLine', { batch: e.batchNo, month: month(e.expiry), qty: e.quantity })} · {formatRupees(e.value)}</span></span>
            <input aria-label={`${t('returns.qty')}: ${e.productName} ${e.batchNo}`} placeholder={t('returns.qty')} value={picked[e.id] ?? ''} inputMode="decimal"
              onChange={(ev) => setPicked({ ...picked, [e.id]: ev.target.value })} className="h-11 rounded-lg border border-line px-2" />
          </div>
        ))}
        <Button disabled={!supplierId || lines.length === 0} onClick={() => void run(async () => {
          await api('/v1/suppliers/returns', 'POST', { id: crypto.randomUUID(), supplierId, lines })
          setPicked({})
        }, t('returns.sent'))}>{t('returns.send')}</Button>
      </section>

      <section className="flex flex-col gap-2 rounded-card bg-white p-3">
        <h3 className="font-bold">{t('returns.open', { count: open.length })}</h3>
        {open.length === 0 && <p className="text-sm text-muted">{t('returns.none')}</p>}
        {open.map((r) => {
          const c = credit[r.id] ?? { no: '', amount: String(r.value) }
          return (
            <div key={r.id} className={`flex flex-col gap-2 rounded-xl p-2 ${r.daysOpen > 60 ? 'bg-due-soft' : 'bg-ground'}`}>
              <p className="flex justify-between text-sm font-semibold"><span>{t('returns.line', { number: r.number, supplier: r.supplierName, days: r.daysOpen })}</span><span>{formatRupees(r.value)}</span></p>
              {r.lines.map((l, i) => <p key={i} className="text-xs">{l.productName} · {l.batchNo} × {l.quantity}</p>)}
              <div className="grid grid-cols-2 gap-2">
                <Field label={t('returns.creditNo')} value={c.no} onChange={(e) => setCredit({ ...credit, [r.id]: { ...c, no: e.target.value } })} />
                <Field label={t('returns.creditAmount')} value={c.amount} onChange={(e) => setCredit({ ...credit, [r.id]: { ...c, amount: e.target.value } })} inputMode="decimal" />
              </div>
              <Button tone="plain" onClick={() => void run(() => api(`/v1/suppliers/returns/${r.id}/credit-note`, 'POST', { creditNoteNo: c.no.trim() || null, amount: Number(c.amount) || 0 }), t('returns.settled'))}>{t('returns.settle')}</Button>
            </div>
          )
        })}
        {returns.some((r) => r.settledAt) && <h4 className="mt-2 font-semibold">{t('returns.settledList')}</h4>}
        {returns.some((r) => r.settledAt) && (
          <div className="hidden lg:block">
            <DataTable label={t('returns.settledList')} rows={returns.filter((r) => r.settledAt)} rowKey={(r) => r.id} download="expiry-returns"
              columns={[
                { key: 'no', header: t('estTable.no'), width: 'w-20', sortValue: (r) => r.number, render: (r) => <b>#{r.number}</b>, csv: (r) => r.number },
                { key: 'supplier', header: t('poTable.supplier'), sortValue: (r) => r.supplierName, filter: { kind: 'text', value: (r) => r.supplierName }, render: (r) => r.supplierName, csv: (r) => r.supplierName },
                { key: 'cn', header: t('returns.creditNo'), width: 'w-36', filter: { kind: 'text', value: (r) => r.creditNoteNo ?? '' }, render: (r) => r.creditNoteNo ?? '—', csv: (r) => r.creditNoteNo ?? '' },
                { key: 'settled', header: t('medTable.settled'), width: 'w-28', sortValue: (r) => r.settledAt ?? '', render: (r) => (r.settledAt ? new Date(r.settledAt).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' }) : '—'), csv: (r) => r.settledAt?.slice(0, 10) ?? '' },
                { key: 'sent', header: t('medTable.sent'), align: 'right', width: 'w-28', sortValue: (r) => r.value, render: (r) => formatRupees(r.value), csv: (r) => r.value },
                { key: 'credit', header: t('returns.creditAmount'), align: 'right', width: 'w-28', sortValue: (r) => r.creditAmount ?? 0, render: (r) => <b>{formatRupees(r.creditAmount ?? 0)}</b>, csv: (r) => r.creditAmount ?? 0 },
                { key: 'loss', header: t('medTable.loss'), align: 'right', width: 'w-24', sortValue: (r) => r.loss, render: (r) => (r.loss > 0 ? <span className="text-due">{formatRupees(r.loss)}</span> : '—'), csv: (r) => r.loss },
              ]} />
          </div>
        )}
        {returns.filter((r) => r.settledAt).slice(0, 20).map((r) => (
          <p key={r.id} className="flex justify-between text-sm lg:hidden"><span>#{r.number} · {r.supplierName}{r.creditNoteNo ? ` · ${r.creditNoteNo}` : ''}</span>
            <span>{formatRupees(r.creditAmount ?? 0)}{r.loss > 0 ? <span className="ml-1 text-due">({t('returns.loss', { value: formatRupees(r.loss) })})</span> : null}</span></p>
        ))}
      </section>
    </div>
  )
}

import { shownLocale } from '../i18n'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { useNow } from '../lib/useNow'

interface Row {
  soldAt: string; billNo: string | null; patientName: string; patientAddress: string | null; doctorName: string; doctorAddress: string | null; doctorRegNo: string | null
  medicine: string; quantity: number; batch: string | null; cancelled: boolean
}

const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`

/** The Schedule H1 register: every H1 sale, with its doctor and patient, for a period. Printable for the drug inspector. */
export function H1RegisterScreen({ shopId, shopName }: { shopId: string; shopName: string }) {
  const nowMs = useNow()
  const { t } = useTranslation()
  const today = new Date(nowMs)
  const [from, setFrom] = useState(iso(new Date(today.getFullYear(), today.getMonth(), 1)))
  const [to, setTo] = useState(iso(today))
  const rows = useQuery({ queryKey: [shopId, 'h1-register', from, to], queryFn: () => api<Row[]>(`/v1/pharmacy/h1-register?from=${from}&to=${to}`) }).data ?? []
  return (
    <div className="flex flex-col gap-3 p-4 pb-28 lg:mx-auto lg:w-full lg:max-w-6xl print:max-w-none print:p-0">
      <h2 className="text-lg font-bold">{t('rx.register')}</h2>
      <p className="text-sm text-muted print:hidden">{t('rx.registerHint')}</p>
      <p className="hidden font-semibold print:block">{t('heads.h1Line', { shop: shopName, from: new Date(from).toLocaleDateString(shownLocale()), to: new Date(to).toLocaleDateString(shownLocale()) })}</p>
      <div className="grid grid-cols-2 items-end gap-2 sm:grid-cols-[1fr_1fr_auto] print:hidden">
        <Field label={t('rx.from')} type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        <Field label={t('rx.to')} type="date" value={to} onChange={(e) => setTo(e.target.value)} />
        <Button tone="plain" className="col-span-2 h-12 px-3 sm:col-span-1" onClick={() => window.print()}>{t('rx.printRegister')}</Button>
      </div>
      {rows.length === 0 && <p className="text-sm text-muted">{t('rx.noneRegister')}</p>}
      {/* The register as the drug inspector expects it: one numbered line per sale, on a computer and on paper. */}
      {rows.length > 0 && (
        <table className="hidden w-full border-collapse overflow-hidden rounded-card bg-white text-sm shadow-sm lg:table print:table print:text-[11px] print:shadow-none">
          <thead className="bg-brand-soft text-left text-xs uppercase tracking-wide print:bg-white">
            <tr className="print:border-b-2 print:border-black">
              <th className="w-10 px-2 py-2">{t('h1Table.sr')}</th><th className="w-24 px-2 py-2">{t('cashTable.date')}</th><th className="w-32 px-2 py-2">{t('h1Table.bill')}</th>
              <th className="px-2 py-2">{t('rx.patient')}</th><th className="px-2 py-2">{t('rx.doctor')}</th>
              <th className="px-2 py-2">{t('h1Table.medicine')}</th><th className="w-28 px-2 py-2">{t('batches.batchNo')}</th><th className="w-14 px-2 py-2 text-right">{t('h1Table.qty')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line print:divide-black/30">
            {rows.map((r, i) => (
              <tr key={i} className={`align-top ${r.cancelled ? 'bg-due-soft/60 text-muted line-through' : ''}`}>
                <td className="px-2 py-1.5">{i + 1}</td>
                <td className="whitespace-nowrap px-2 py-1.5">{new Date(r.soldAt).toLocaleDateString(shownLocale())}</td>
                <td className="px-2 py-1.5">{r.billNo ?? '—'}{r.cancelled && <span className="block text-xs font-semibold text-due no-underline">{t('rx.cancelled')}</span>}</td>
                <td className="px-2 py-1.5"><span className="font-semibold">{r.patientName}</span>{r.patientAddress && <span className="block text-xs text-muted">{r.patientAddress}</span>}</td>
                <td className="px-2 py-1.5"><span className="font-semibold">{r.doctorName}</span>{r.doctorRegNo && <span className="block text-xs">{t('h1Table.reg')} {r.doctorRegNo}</span>}{r.doctorAddress && <span className="block text-xs text-muted">{r.doctorAddress}</span>}</td>
                <td className="px-2 py-1.5 font-semibold">{r.medicine}</td>
                <td className="px-2 py-1.5 font-mono text-xs">{r.batch ?? '—'}</td>
                <td className="px-2 py-1.5 text-right">{r.quantity}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {rows.map((r, i) => (
        <div key={i} className={`rounded-xl p-2 text-sm lg:hidden print:hidden ${r.cancelled ? 'bg-due-soft line-through' : 'bg-white'}`}>
          <p className="flex justify-between gap-2"><b>{r.medicine} × {r.quantity}</b><span>{new Date(r.soldAt).toLocaleDateString(shownLocale())}{r.billNo ? ` · ${r.billNo}` : ''}</span></p>
          <p>{t('rx.patient')}: {r.patientName}{r.patientAddress ? `, ${r.patientAddress}` : ''}</p>
          <p>{t('rx.doctor')}: {r.doctorName}{r.doctorRegNo ? ` (${r.doctorRegNo})` : ''}{r.doctorAddress ? `, ${r.doctorAddress}` : ''}</p>
          {r.batch && <p className="text-xs text-muted">{r.batch}</p>}
          {r.cancelled && <p className="text-xs font-semibold text-due no-underline">{t('rx.cancelled')}</p>}
        </div>
      ))}
    </div>
  )
}

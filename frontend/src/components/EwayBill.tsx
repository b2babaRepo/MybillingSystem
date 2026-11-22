import { btn } from './ui'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ewayBillJson, pincodeIn } from '../lib/ewaybill'
import type { BillView } from '../lib/types'

const SHOP_PIN = 'gk-shop-pincode'

/**
 * On a GST bill of ₹50,000 or more: the few trip details the bill doesn't hold, then a file for the e-way bill
 * portal's bulk upload. The shop's pincode is remembered on this device.
 */
export function EwayBill({ shopName, bill, customerName }: { shopName: string; bill: BillView['bill']; customerName: string | null }) {
  const { t } = useTranslation()
  const [open, setOpen] = useState(false)
  const saved = (() => { try { return localStorage.getItem(SHOP_PIN) ?? '' } catch { return '' } })()
  const [fromPincode, setFrom] = useState(saved || pincodeIn(bill.shopAddress))
  const [toPincode, setTo] = useState(pincodeIn(bill.buyerAddress))
  const [distance, setDistance] = useState('')
  const [vehicleNo, setVehicle] = useState(bill.vehicleNumber ?? '')
  const [buyerName, setBuyer] = useState(customerName ?? '')
  const [problems, setProblems] = useState<string[]>([])

  if (!open) return <button onClick={() => setOpen(true)} className="mt-4 h-11 w-full rounded-xl border border-brand font-semibold text-brand-strong print:hidden">{t('eway.button')}</button>

  function download() {
    const result = ewayBillJson(shopName, bill, { fromPincode: fromPincode.trim(), toPincode: toPincode.trim(), distanceKm: Number(distance), vehicleNo, buyerName: buyerName.trim() || 'Customer' })
    if ('problems' in result) { setProblems(result.problems); return }
    setProblems([])
    try { localStorage.setItem(SHOP_PIN, fromPincode.trim()) } catch { /* no storage */ }
    const url = URL.createObjectURL(new Blob([JSON.stringify(result.json, null, 2)], { type: 'application/json' }))
    const a = document.createElement('a')
    a.href = url
    a.download = `ewaybill-${(bill.billNo ?? bill.id).replace(/\//g, '-')}.json`
    a.click()
    URL.revokeObjectURL(url)
  }

  const field = (label: string, value: string, set: (v: string) => void, mode: 'numeric' | 'text' = 'text') => (
    <label className="flex flex-col gap-1">{label}<input inputMode={mode} value={value} onChange={(e) => set(e.target.value)} className="h-11 rounded-xl border border-line px-3" /></label>
  )
  return (
    <section className="mt-4 flex flex-col gap-2 rounded-xl border border-line p-3 text-sm print:hidden">
      <b>{t('eway.title')}</b>
      <p className="text-muted">{t('eway.hint')}</p>
      <div className="grid gap-2 sm:grid-cols-2">
        {field(t('eway.from'), fromPincode, setFrom, 'numeric')}
        {field(t('eway.to'), toPincode, setTo, 'numeric')}
        {field(t('eway.distance'), distance, setDistance, 'numeric')}
        {field(t('eway.vehicle'), vehicleNo, setVehicle)}
        {!bill.buyerGstin && field(t('eway.buyer'), buyerName, setBuyer)}
      </div>
      {problems.length > 0 && <ul className="list-disc pl-5 text-due">{problems.map((p) => <li key={p}>{p}</li>)}</ul>}
      <div className="flex flex-wrap gap-2">
        <button onClick={download} className={`h-11 flex-1 px-4 rounded-xl ${btn.solid}`}>{t('eway.download')}</button>
        <a href="https://ewaybillgst.gov.in" target="_blank" rel="noreferrer" className="flex h-11 items-center rounded-xl border border-line px-4 font-semibold">{t('eway.portal')}</a>
      </div>
    </section>
  )
}

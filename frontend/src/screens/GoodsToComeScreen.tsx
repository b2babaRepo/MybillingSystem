import { btn } from '../lib/buttonStyles'
import { shownLocale } from '../i18n'
import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { api, request } from '../lib/api'
import { shrinkPhoto } from '../lib/images'
import { formatRupees, whatsAppLink } from '../lib/format'
import { OPEN_SUPPLIER_KEY } from './OrdersPlacedScreen'
import { navigate } from '../router'
import { DataTable } from '../components/DataTable'
import { shownError } from '../lib/notify'

type Channel = 'call' | 'whatsapp' | 'visit'
interface Shipment { id: string; dispatchedOn: string | null; expectedOn: string | null; transporter: string | null; lrNo: string | null; vehicleNo: string | null; ewayBillNo: string | null; ewayValidUntil: string | null; freight: number | null; freightPaidBy: 'shop' | 'supplier' | null; arrived: boolean; hasPhoto?: boolean }
interface FollowUp { at: string; channel: Channel; note: string | null; newPromisedDate: string | null; by: string | null }
interface Coming {
  id: string; number: number; supplierId: string; supplier: string; phone: string | null; stage: string; expectedOn: string | null; daysLate: number | null; promiseMoves: number
  pending: number; items: number; value: number; snoozedUntil: string | null; createdAt: string; note: string | null; shipments: Shipment[]; followUps: FollowUp[]
}

const STAGES = ['sent', 'confirmed', 'dispatched'] as const
// E-way bill: needed for goods above ₹50,000 moving by road (rule 138); some states set other limits within the state.
const EWAY_ABOVE = 50_000
const day = (iso: string) => new Date(iso.length === 10 ? `${iso}T00:00` : iso).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short' })
const todayIso = () => new Date(Date.now() - new Date().getTimezoneOffset() * 60_000).toISOString().slice(0, 10)
const plusDays = (n: number) => new Date(Date.now() - new Date().getTimezoneOffset() * 60_000 + n * 86_400_000).toISOString().slice(0, 10)
const blankLot = { transporter: '', lrNo: '', vehicleNo: '', ewayBillNo: '', ewayValidUntil: '', freight: '', freightPaidBy: '', dispatchedOn: '', expectedOn: '' }

/**
 * Goods to come: every order still waiting for goods, the latest first. Where it has got to (sent, confirmed,
 * dispatched), the truck and bilty for each lot, a log of every call and WhatsApp, and the promised date (and how
 * often it moved). Call and WhatsApp buttons write the follow-up as they open.
 */
export function GoodsToComeScreen({ shopId, shopName }: { shopId: string; shopName: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const coming = useQuery({ queryKey: [shopId, 'goods-to-come'], queryFn: () => api<Coming[]>('/v1/goods-to-come') })
  const [open, setOpen] = useState<string | null>(null)
  const [note, setNote] = useState({ text: '', date: '', channel: 'call' as Channel })
  const [lot, setLot] = useState<typeof blankLot | null>(null)
  const [message, setMessage] = useState('')
  const data = coming.data ?? []
  const late = data.filter((o) => (o.daysLate ?? 0) > 0).length

  async function run(action: () => Promise<unknown>) {
    setMessage('')
    try {
      await action()
      await queryClient.invalidateQueries({ queryKey: [shopId, 'goods-to-come'] })
      void queryClient.invalidateQueries({ queryKey: [shopId, 'suppliers'] })
      return true
    } catch (e) {
      setMessage(shownError(e))
      return false
    }
  }

  const addPhoto = (orderId: string, lotId: string, file: File) => run(async () => api(`/v1/suppliers/orders/${orderId}/shipments/${lotId}/photo`, 'POST', { photoDataUrl: await shrinkPhoto(file, 1400, 140_000, false) }))
  const openPhoto = async (orderId: string, lotId: string) => {
    const tab = window.open('', '_blank')
    const r = await request(`/v1/suppliers/orders/${orderId}/shipments/${lotId}/photo`)
    if (r.ok && tab) tab.location.href = URL.createObjectURL(await r.blob())
    else tab?.close()
  }
  const logContact = (o: Coming, channel: Channel) => void run(() => api(`/v1/suppliers/orders/${o.id}/follow-ups`, 'POST', { id: crypto.randomUUID(), channel, note: null, newPromisedDate: null }))
  const chase = (o: Coming) => t('gtc.chaseMessage', { shop: shopName, number: o.number, items: o.items, date: o.expectedOn ? day(o.expectedOn) : '—' })

  async function saveNote(o: Coming) {
    if (await run(() => api(`/v1/suppliers/orders/${o.id}/follow-ups`, 'POST', { id: crypto.randomUUID(), channel: note.channel, note: note.text.trim() || null, newPromisedDate: note.date || null }))) setNote({ text: '', date: '', channel: 'call' })
  }

  async function saveLot(o: Coming) {
    if (!lot) return
    const ok = await run(() => api(`/v1/suppliers/orders/${o.id}/shipments`, 'POST', {
      id: crypto.randomUUID(), dispatchedOn: lot.dispatchedOn || null, expectedOn: lot.expectedOn || null, transporter: lot.transporter || null, lrNo: lot.lrNo || null,
      vehicleNo: lot.vehicleNo || null, ewayBillNo: lot.ewayBillNo.replace(/\s/g, '') || null, ewayValidUntil: lot.ewayValidUntil || null,
      freight: lot.freight === '' ? null : Number(lot.freight), freightPaidBy: lot.freightPaidBy || null,
    }))
    if (ok) setLot(null)
  }

  function openSupplier(id: string) {
    try { sessionStorage.setItem(OPEN_SUPPLIER_KEY, id) } catch { /* no storage */ }
    navigate('/suppliers')
  }

  const input = 'h-11 min-w-0 rounded-xl border border-line bg-white px-3'
  return (
    <div className="flex flex-col gap-3 p-4 lg:mx-auto lg:w-full lg:max-w-5xl">
      <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
        <h1 className="text-xl font-bold">⏳ {t('gtc.title')}</h1>
        <p className="text-sm text-muted">{t('gtc.hint')}</p>
        {data.length > 0 && <p className="text-sm font-semibold">{t('gtc.summary', { count: data.length })}{late > 0 && <span className="text-due"> · {t('gtc.lateCount', { count: late })}</span>}</p>}
      </section>
      {message && <p className="text-sm text-due">{message}</p>}
      {coming.data && data.length === 0 && <p className="py-6 text-center text-muted">{t('gtc.none')}</p>}
      {data.length > 0 && (
        <div className="hidden lg:block">
          <DataTable label={t('gtc.title')} rows={data} rowKey={(o) => o.id} selectedKey={open} onRowClick={(o) => { setOpen(open === o.id ? null : o.id); setLot(null) }} download="goods-to-come"
            defaultSort={{ key: 'late', dir: -1 }}
            columns={[
              { key: 'supplier', header: t('poTable.supplier'), sortValue: (o) => o.supplier, filter: { kind: 'text', value: (o) => o.supplier }, render: (o) => <span className="font-semibold">{o.supplier}</span>, csv: (o) => o.supplier },
              { key: 'no', header: t('estTable.no'), width: 'w-20', sortValue: (o) => o.number, render: (o) => `#${o.number}`, csv: (o) => o.number },
              { key: 'expected', header: t('poTable.expected'), width: 'w-28', sortValue: (o) => o.expectedOn ?? '', render: (o) => (o.expectedOn ? day(o.expectedOn) : '—'), csv: (o) => o.expectedOn ?? '' },
              { key: 'late', header: t('gtcTable.late'), align: 'right', width: 'w-24', sortValue: (o) => o.daysLate ?? 0,
                render: (o) => ((o.daysLate ?? 0) > 0 ? <span className="font-semibold text-due">{t('supTable.days', { count: o.daysLate })}</span> : o.expectedOn === todayIso() ? <span className="text-warn">{t('gtc.dueToday')}</span> : '—'), csv: (o) => o.daysLate ?? '' },
              { key: 'moved', header: t('gtcTable.moved'), align: 'right', width: 'w-20', sortValue: (o) => o.promiseMoves, render: (o) => o.promiseMoves || '—', csv: (o) => o.promiseMoves },
              { key: 'items', header: t('estTable.items'), align: 'right', width: 'w-20', sortValue: (o) => o.items, render: (o) => o.items, csv: (o) => o.items },
              { key: 'pending', header: t('gtcTable.toCome'), align: 'right', width: 'w-24', sortValue: (o) => o.pending, render: (o) => <b>{o.pending}</b>, csv: (o) => o.pending },
              { key: 'value', header: t('poTable.value'), align: 'right', width: 'w-28', sortValue: (o) => o.value, render: (o) => (o.value > 0 ? formatRupees(o.value) : '—'), csv: (o) => o.value },
              { key: 'stage', header: t('gtcTable.stage'), width: 'w-32', sortValue: (o) => o.stage,
                filter: { kind: 'select', value: (o) => o.stage, options: [...new Set(data.map((o) => o.stage))].map((st) => ({ value: st, label: t(`gtc.stage.${st}`) })) },
                render: (o) => <span className="rounded-full bg-brand-soft px-2.5 py-0.5 text-xs font-semibold text-brand-strong">{t(`gtc.stage.${o.stage}`)}</span>, csv: (o) => t(`gtc.stage.${o.stage}`) },
            ]} />
          {!open && <p className="mt-2 px-1 text-xs text-muted">{t('gtcTable.pick')}</p>}
        </div>
      )}
      {data.map((o) => {
        const isOpen = open === o.id
        const isLate = (o.daysLate ?? 0) > 0
        const snoozed = !!o.snoozedUntil && o.snoozedUntil >= todayIso()
        return (
          <section key={o.id} className={`flex-col gap-2 rounded-card bg-white p-4 shadow-sm ${isLate ? 'ring-1 ring-red-200' : ''} ${isOpen ? 'flex' : 'flex lg:hidden'}`}>
            <button onClick={() => { setOpen(isOpen ? null : o.id); setLot(null) }} className="flex items-start justify-between gap-2 text-left">
              <span className="flex min-w-0 flex-col">
                <b>{o.supplier} · {t('supplier2.orderNo', { number: o.number })}</b>
                <span className={`text-xs ${isLate ? 'font-semibold text-due' : 'text-muted'}`}>
                  {o.expectedOn ? (isLate ? t('gtc.late', { count: o.daysLate! }) : o.expectedOn === todayIso() ? t('gtc.dueToday') : t('po.expected', { date: day(o.expectedOn) })) : t('gtc.noDate')}
                  {o.promiseMoves > 0 && ` · ${t('gtc.moved', { count: o.promiseMoves })}`}
                </span>
                <span className="text-xs text-muted">{t('po.types', { count: o.items })} · {t('supplier2.stillToCome', { qty: o.pending })}{o.value > 0 && ` · ${formatRupees(o.value)}`}{snoozed && ` · 🔕 ${t('gtc.snoozedTill', { date: day(o.snoozedUntil!) })}`}</span>
              </span>
              <span className="shrink-0 rounded-full bg-brand-soft px-2.5 py-0.5 text-xs font-semibold text-brand-strong">{t(`gtc.stage.${o.stage}`)}</span>
            </button>
            <div className="flex flex-wrap gap-2">
              {o.phone && <a href={`tel:${o.phone}`} onClick={() => logContact(o, 'call')} className="flex h-10 items-center rounded-xl border border-line px-3 text-sm font-semibold">📞 {t('gtc.call')}</a>}
              {o.phone && <a href={whatsAppLink(o.phone, chase(o))} target="_blank" rel="noreferrer" onClick={() => logContact(o, 'whatsapp')} className="flex h-10 items-center rounded-xl bg-[#25D366] px-3 text-sm font-bold text-white">WhatsApp</a>}
              {!o.phone && <button onClick={() => openSupplier(o.supplierId)} className="h-10 rounded-xl border border-line px-3 text-sm font-semibold text-muted">{t('gtc.addPhone')}</button>}
            </div>
            {o.shipments.length > 0 && (
              <ul className="flex flex-col gap-1 text-xs">
                {o.shipments.map((s) => (
                  <li key={s.id} className={s.arrived ? 'text-muted line-through' : ''}>
                    🚚 {[s.transporter, s.lrNo && `${t('bag.bilty')} ${s.lrNo}`, s.vehicleNo, s.ewayBillNo && `${t('gtc.eway')} ${s.ewayBillNo}`].filter(Boolean).join(' · ') || t('gtc.lot')}
                    {s.dispatchedOn && ` · ${t('gtc.left', { date: day(s.dispatchedOn) })}`}{s.expectedOn && ` · ${t('po.expected', { date: day(s.expectedOn) })}`}
                    {s.freight != null && ` · ${t('gtc.freight')} ${formatRupees(s.freight)}${s.freightPaidBy ? ` (${t(`gtc.paidBy.${s.freightPaidBy}`)})` : ''}`}
                    {s.ewayValidUntil && !s.arrived && s.ewayValidUntil < todayIso() && <b className="text-due"> · {t('gtc.ewayExpired')}</b>}
                    <span className="ml-1 inline-flex gap-2 no-underline">
                      {s.hasPhoto && <button onClick={() => void openPhoto(o.id, s.id)} className="font-semibold text-brand">📄 {t('gtc.seeBilty')}</button>}
                      <label className="cursor-pointer font-semibold text-brand">📷 {t(s.hasPhoto ? 'gtc.newBilty' : 'gtc.addBilty')}
                        <input type="file" accept="image/*" capture="environment" className="hidden" onChange={(e) => { const f = e.target.files?.[0]; e.target.value = ''; if (f) void addPhoto(o.id, s.id, f) }} />
                      </label>
                    </span>
                  </li>
                ))}
              </ul>
            )}
            {isOpen && (
              <div className="flex flex-col gap-3 border-t border-line pt-3 text-sm">
                <div className="flex flex-col gap-1">
                  <span className="text-xs font-semibold text-muted">{t('gtc.whereIs')}</span>
                  <div className="flex flex-wrap gap-1">
                    {/* Where it started: a placed order lights this step until it moves on, so its place is always shown. */}
                    <span aria-current={o.stage === 'placed' ? 'step' : undefined} className={`flex h-9 items-center rounded-full px-3 text-sm font-semibold ${o.stage === 'placed' ? 'bg-brand text-white' : 'bg-[#edf2f1] text-muted'}`}>{t('gtc.stage.placed')}</span>
                    {STAGES.map((s) => (
                      <button key={s} onClick={() => s === 'dispatched' ? setLot({ ...blankLot, dispatchedOn: todayIso(), expectedOn: o.expectedOn ?? '' }) : void run(() => api(`/v1/suppliers/orders/${o.id}/stage`, 'POST', { stage: s }))}
                        aria-pressed={o.stage === s} className={`h-9 rounded-full px-3 text-sm font-semibold ${o.stage === s ? 'bg-brand text-white' : 'bg-[#edf2f1] text-muted'}`}>{t(`gtc.stage.${s}`)}</button>
                    ))}
                    {o.stage === 'dispatched' && <button onClick={() => setLot({ ...blankLot, dispatchedOn: todayIso(), expectedOn: o.expectedOn ?? '' })} className="h-9 rounded-full border border-line px-3 text-sm font-semibold">+ {t('gtc.addLot')}</button>}
                  </div>
                </div>
                {lot && (
                  <div className="grid grid-cols-2 gap-2 rounded-xl bg-[#f6faf9] p-3">
                    <label className="flex flex-col gap-1 text-xs text-muted">{t('gtc.leftOn')}<input type="date" value={lot.dispatchedOn} onChange={(e) => setLot({ ...lot, dispatchedOn: e.target.value })} className={input} /></label>
                    <label className="flex flex-col gap-1 text-xs text-muted">{t('po.expectedOn')}<input type="date" value={lot.expectedOn} onChange={(e) => setLot({ ...lot, expectedOn: e.target.value })} className={input} /></label>
                    <input aria-label={t('bag.transporter')} placeholder={t('bag.transporter')} value={lot.transporter} onChange={(e) => setLot({ ...lot, transporter: e.target.value })} className={`${input} col-span-2`} />
                    <input aria-label={t('bag.bilty')} placeholder={t('bag.bilty')} value={lot.lrNo} onChange={(e) => setLot({ ...lot, lrNo: e.target.value })} className={input} />
                    <input aria-label={t('bag.vehicle')} placeholder={t('bag.vehicle')} value={lot.vehicleNo} onChange={(e) => setLot({ ...lot, vehicleNo: e.target.value })} className={input} />
                    <input aria-label={t('gtc.ewayNo')} placeholder={t('gtc.ewayNo')} value={lot.ewayBillNo} inputMode="numeric" onChange={(e) => setLot({ ...lot, ewayBillNo: e.target.value })} className={input} />
                    <label className="flex flex-col gap-1 text-xs text-muted">{t('gtc.ewayValid')}<input type="date" value={lot.ewayValidUntil} onChange={(e) => setLot({ ...lot, ewayValidUntil: e.target.value })} className={input} /></label>
                    <input aria-label={t('gtc.freight')} placeholder={t('gtc.freight')} value={lot.freight} inputMode="decimal" onChange={(e) => setLot({ ...lot, freight: e.target.value })} className={input} />
                    <select aria-label={t('gtc.freightBy')} value={lot.freightPaidBy} onChange={(e) => setLot({ ...lot, freightPaidBy: e.target.value })} className={input}>
                      <option value="">{t('gtc.freightBy')}</option><option value="shop">{t('gtc.paidBy.shop')}</option><option value="supplier">{t('gtc.paidBy.supplier')}</option>
                    </select>
                    {o.value > EWAY_ABOVE && !lot.ewayBillNo && <p className="col-span-2 text-xs text-warn">{t('gtc.ewayHint', { amount: formatRupees(EWAY_ABOVE) })}</p>}
                    <button onClick={() => void saveLot(o)} className={`h-11 rounded-xl ${btn.solid}`}>{t('gtc.saveLot')}</button>
                    <button onClick={() => setLot(null)} className="h-11 rounded-xl border border-line font-semibold">{t('cancel')}</button>
                  </div>
                )}
                <div className="flex flex-col gap-2">
                  <span className="text-xs font-semibold text-muted">{t('gtc.followUp')}</span>
                  <div className="grid grid-cols-2 gap-2">
                    <label className="flex flex-col gap-1 text-xs text-muted">{t('gtc.how')}
                      <select value={note.channel} onChange={(e) => setNote({ ...note, channel: e.target.value as Channel })} className={`${input} text-sm text-ink`}>
                        {(['call', 'whatsapp', 'visit'] as const).map((c) => <option key={c} value={c}>{t(`gtc.channel.${c}`)}</option>)}
                      </select>
                    </label>
                    <label className="flex flex-col gap-1 text-xs text-muted">{t('gtc.newDate')}<input type="date" value={note.date} min={todayIso()} onChange={(e) => setNote({ ...note, date: e.target.value })} className={`${input} text-sm text-ink`} /></label>
                    <p className="col-span-2 -mt-1 text-xs text-muted">{o.expectedOn ? t('gtc.promisedNow', { date: day(o.expectedOn) }) : t('gtc.noPromise')}</p>
                    <input aria-label={t('gtc.whatSaid')} placeholder={t('gtc.whatSaid')} value={note.text} maxLength={300} onChange={(e) => setNote({ ...note, text: e.target.value })} className={`${input} col-span-2`} />
                  </div>
                  <button disabled={!note.text.trim() && !note.date} onClick={() => void saveNote(o)} className={`h-11 rounded-xl ${btn.outline}`}>{t('gtc.saveFollowUp')}</button>
                </div>
                {o.followUps.length > 0 && (
                  <ul className="flex flex-col gap-1 text-xs">
                    {o.followUps.map((f, i) => (
                      <li key={i} className="text-muted">
                        {day(f.at)} · {t(`gtc.channel.${f.channel}`)}{f.by && ` · ${f.by}`}{f.note && <span className="text-ink"> · {f.note}</span>}{f.newPromisedDate && <b className="text-warn"> · {t('gtc.promisedFor', { date: day(f.newPromisedDate) })}</b>}
                      </li>
                    ))}
                  </ul>
                )}
                <div className="flex flex-wrap items-center gap-2 text-xs">
                  <span className="text-muted">{t('gtc.reminders')}</span>
                  {snoozed
                    ? <button onClick={() => void run(() => api(`/v1/suppliers/orders/${o.id}/snooze`, 'POST', { until: null }))} className="h-9 rounded-xl border border-line px-3 font-semibold">🔔 {t('gtc.remindAgain')}</button>
                    : [3, 7].map((n) => <button key={n} onClick={() => void run(() => api(`/v1/suppliers/orders/${o.id}/snooze`, 'POST', { until: plusDays(n) }))} className="h-9 rounded-xl border border-line px-3 font-semibold">🔕 {t('gtc.snoozeDays', { count: n })}</button>)}
                </div>
              </div>
            )}
          </section>
        )
      })}
    </div>
  )
}

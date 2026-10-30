import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../components/ui'
import { api } from '../lib/api'
import { BADGE_ICON, type AgentSummary, type FieldAgent, type Registration, type RegistrationPayment } from '../lib/agents'
import { priceFor, type PlanInfo, type PlanTerm } from '../lib/billing'
import { formatDateTime, formatRupees } from '../lib/format'
import { shrinkPhoto } from '../lib/images'
import { addText } from '../i18n'
import { agentText } from '../i18n/agent'

addText(agentText)

const SHOP_TYPES = ['kirana', 'building_materials', 'paint', 'hardware', 'other'] as const
const PHOTO_KINDS = ['front', 'inside', 'owner'] as const
const TERMS: PlanTerm[] = ['year1', 'year3', 'year5']

const STATUS_STYLE: Record<string, string> = {
  submitted: 'bg-amber-50 text-amber-900', approved: 'bg-emerald-50 text-emerald-900', rejected: 'bg-red-50 text-red-900',
}

/** The approved agent's work: sign a shop up on a visit, and the list of shops they signed up with where each stands. */
export function AgentShops({ agent }: { agent: FieldAgent }) {
  const { t } = useTranslation()
  const [tab, setTab] = useState<'home' | 'new' | 'mine' | 'awards'>('home')
  const mine = useQuery({ queryKey: ['agent', 'shops'], queryFn: () => api<Registration[]>('/v1/agent/shops') })
  const summary = useQuery({ queryKey: ['agent', 'summary'], queryFn: () => api<AgentSummary>('/v1/agent/summary') }).data
  const shops = mine.data ?? []

  return (
    <div className="flex flex-col gap-3">
      <div role="tablist" className="grid grid-cols-4 gap-1 rounded-2xl bg-white p-1 text-sm font-semibold">
        {(['home', 'new', 'mine', 'awards'] as const).map((x) => (
          <button key={x} role="tab" aria-selected={tab === x} onClick={() => setTab(x)} className={`min-h-11 rounded-xl ${tab === x ? 'bg-teal-700 text-white' : ''}`}>
            {t(`agentShops.tab.${x}`)}
          </button>
        ))}
      </div>
      {tab === 'home' && summary && <AgentHome s={summary} />}
      {tab === 'awards' && summary && <AgentAwards s={summary} />}
      {tab === 'new' && <NewShopForm agent={agent} onDone={() => setTab('mine')} />}
      {tab === 'mine' && (
        <ul className="flex flex-col gap-2">
          {shops.length === 0 && <p className="p-4 text-center text-muted">{t('agentShops.none')}</p>}
          {shops.map((r) => (
            <li key={r.id} className="flex flex-col gap-1 rounded-2xl bg-white p-4 text-sm">
              <span className="flex items-start justify-between gap-2">
                <b className="text-base">{r.shopName}</b>
                <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${STATUS_STYLE[r.status]}`}>{t(`agentShops.status.${r.status}`)}</span>
              </span>
              <span>{r.ownerName} · {r.market}</span>
              <span>{r.planCode} · {t(`plan2.term.${r.term}`)} · {formatRupees(r.amount)} · {t(`agentShops.pay.${r.paymentMode}`)} · {t(`agentShops.payStatus.${r.paymentStatus}`)}</span>
              <span className="text-xs text-muted">{formatDateTime(r.submittedAt)}</span>
              {r.rejectReason && <span className="font-semibold text-red-800">{t('fieldAgent.reason', { reason: r.rejectReason })}</span>}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

/**
 * Signing a shop up at the shop: details, GPS, photos, the owner, the plan and how it's paid, then the owner reads out
 * the code sent to their phone. Nothing is sent without that code.
 */
function NewShopForm({ agent, onDone }: { agent: FieldAgent; onDone: () => void }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const plans = useQuery({ queryKey: ['agent', 'plans'], queryFn: () => api<PlanInfo[]>('/v1/agent/plans'), staleTime: 300_000 }).data ?? []
  const blank = {
    id: crypto.randomUUID(), shopName: '', type: 'kirana', address: '', market: agent.market, ownerName: '', ownerPhone: '', altPhone: '', gstin: '',
    planCode: 'silver', term: 'year1' as PlanTerm, paymentMode: 'cash' as RegistrationPayment, paymentRef: '', consentCode: '',
  }
  const [form, setForm] = useState(blank)
  const [gps, setGps] = useState<{ latitude: number; longitude: number; accuracyMetres: number } | null>(null)
  const [photos, setPhotos] = useState<Partial<Record<(typeof PHOTO_KINDS)[number], string>>>({})
  const [codeSent, setCodeSent] = useState(false)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)
  const set = (k: keyof typeof blank) => (e: { target: { value: string } }) => setForm({ ...form, [k]: e.target.value })
  const plan = plans.find((p) => p.code === form.planCode)
  const amount = plan ? priceFor(plan, form.term) : null

  async function run(action: () => Promise<void>) {
    setBusy(true)
    setMessage(null)
    try {
      await action()
    } catch (e) {
      setMessage({ ok: false, text: e instanceof Error ? e.message : String(e) })
    } finally {
      setBusy(false)
    }
  }

  const locate = () => {
    setMessage(null)
    navigator.geolocation.getCurrentPosition(
      (p) => setGps({ latitude: p.coords.latitude, longitude: p.coords.longitude, accuracyMetres: Math.round(p.coords.accuracy) }),
      () => setMessage({ ok: false, text: t('agentShops.gpsFailed') }),
      { enableHighAccuracy: true, timeout: 20_000 },
    )
  }

  const sendCode = () => run(async () => {
    await api('/v1/agent/consent-code', 'POST', { ownerPhone: form.ownerPhone.trim() })
    setCodeSent(true)
    setMessage({ ok: true, text: t('agentShops.codeSent') })
  })

  const submit = () => run(async () => {
    await api<Registration>('/v1/agent/shops', 'POST', {
      ...form, ...gps, altPhone: form.altPhone.trim() || null, gstin: form.gstin.trim() || null,
      paymentRef: form.paymentMode === 'later' ? null : form.paymentRef.trim() || null,
      photos: Object.entries(photos).map(([kind, dataUrl]) => ({ kind, dataUrl })),
    })
    await queryClient.invalidateQueries({ queryKey: ['agent', 'shops'] })
    setForm({ ...blank, id: crypto.randomUUID() })
    setGps(null)
    setPhotos({})
    setCodeSent(false)
    onDone()
  })

  const box = 'h-12 rounded-xl border border-line bg-white px-3 text-lg'
  const label = 'flex flex-col gap-1'
  const caption = 'text-sm font-medium text-muted'
  const ready = form.shopName.trim().length >= 2 && form.address.trim().length >= 5 && gps && photos.front && form.ownerName.trim().length >= 2
    && form.ownerPhone.trim().length >= 10 && amount !== null && (form.paymentMode !== 'upi' || form.paymentRef.trim().length >= 6) && form.consentCode.length === 6

  return (
    <section className="flex flex-col gap-3 rounded-[20px] bg-white p-4">
      <h2 className="font-bold">{t('agentShops.shop')}</h2>
      <label className={label}><span className={caption}>{t('agentShops.shopName')}</span><input value={form.shopName} onChange={set('shopName')} className={box} /></label>
      <label className={label}><span className={caption}>{t('shopType.label')}</span>
        <select value={form.type} onChange={set('type')} className={box}>{SHOP_TYPES.map((x) => <option key={x} value={x}>{t(`shopType.${x}`)}</option>)}</select></label>
      <label className={label}><span className={caption}>{t('agentShops.address')}</span><textarea value={form.address} onChange={set('address')} rows={2} className="rounded-xl border border-line p-3 text-lg" /></label>
      <label className={label}><span className={caption}>{t('fieldAgent.market')}</span><input value={form.market} onChange={set('market')} className={box} /></label>
      <button type="button" onClick={locate} className={`min-h-12 rounded-xl border-2 font-semibold ${gps ? 'border-teal-700 bg-teal-50 text-teal-900' : 'border-dashed border-line'}`}>
        {gps ? t('agentShops.gpsTaken', { accuracy: gps.accuracyMetres }) : t('agentShops.takeGps')}
      </button>

      <h2 className="mt-2 font-bold">{t('agentShops.photos')}</h2>
      <div className="grid grid-cols-3 gap-2">
        {PHOTO_KINDS.map((kind) => (
          <label key={kind} className={`flex aspect-square cursor-pointer flex-col items-center justify-center overflow-hidden rounded-xl border-2 text-center text-xs font-semibold ${photos[kind] ? 'border-teal-700' : 'border-dashed border-line'}`}>
            {photos[kind] ? <img src={photos[kind]} alt={t(`agentShops.photo.${kind}`)} className="h-full w-full object-cover" /> : <span className="p-1">{t(`agentShops.photo.${kind}`)}{kind === 'front' ? ' *' : ''}</span>}
            <input type="file" accept="image/*" capture="environment" className="sr-only"
              onChange={(e) => { const file = e.target.files?.[0]; if (file) void shrinkPhoto(file, 1024, 140_000, false).then((url) => setPhotos((p) => ({ ...p, [kind]: url }))) }} />
          </label>
        ))}
      </div>

      <h2 className="mt-2 font-bold">{t('agentShops.owner')}</h2>
      <label className={label}><span className={caption}>{t('agentShops.ownerName')}</span><input value={form.ownerName} onChange={set('ownerName')} className={box} /></label>
      <label className={label}><span className={caption}>{t('agentShops.ownerPhone')}</span>
        <input value={form.ownerPhone} onChange={(e) => { set('ownerPhone')(e); setCodeSent(false) }} inputMode="tel" className={box} /></label>
      <label className={label}><span className={caption}>{t('agentShops.altPhone')}</span><input value={form.altPhone} onChange={set('altPhone')} inputMode="tel" className={box} /></label>
      <label className={label}><span className={caption}>{t('agentShops.gstin')}</span><input value={form.gstin} onChange={(e) => setForm({ ...form, gstin: e.target.value.toUpperCase() })} className={`${box} uppercase`} /></label>

      <h2 className="mt-2 font-bold">{t('agentShops.plan')}</h2>
      <div className="grid grid-cols-3 gap-2">
        {plans.map((p) => (
          <button key={p.code} type="button" onClick={() => setForm({ ...form, planCode: p.code })} aria-pressed={form.planCode === p.code}
            className={`min-h-14 rounded-xl font-bold ${form.planCode === p.code ? 'bg-teal-700 text-white' : 'border border-line'}`}>{p.name}</button>
        ))}
      </div>
      <div className="grid grid-cols-3 gap-2">
        {TERMS.map((x) => {
          const price = plan ? priceFor(plan, x) : null
          return price === null ? null : (
            <button key={x} type="button" onClick={() => setForm({ ...form, term: x })} aria-pressed={form.term === x}
              className={`flex min-h-14 flex-col items-center justify-center rounded-xl text-sm font-semibold ${form.term === x ? 'bg-teal-700 text-white' : 'border border-line'}`}>
              <span>{t(`plan2.term.${x}`)}</span><span>{formatRupees(price)}{x === 'year5' ? `/${t('plan2.year')}` : ''}</span>
            </button>
          )
        })}
      </div>
      <div role="radiogroup" aria-label={t('agentShops.payHow')} className="grid grid-cols-3 gap-2">
        {(['cash', 'upi', 'later'] as const).map((m) => (
          <button key={m} type="button" role="radio" aria-checked={form.paymentMode === m} onClick={() => setForm({ ...form, paymentMode: m })}
            className={`min-h-12 rounded-xl text-sm font-semibold ${form.paymentMode === m ? 'bg-teal-700 text-white' : 'border border-line'}`}>{t(`agentShops.pay.${m}`)}</button>
        ))}
      </div>
      {form.paymentMode !== 'later' && (
        <label className={label}><span className={caption}>{form.paymentMode === 'upi' ? t('agentShops.utr') : t('agentShops.receipt')}</span>
          <input value={form.paymentRef} onChange={set('paymentRef')} className={box} /></label>
      )}
      {form.paymentMode === 'cash' && <p className="rounded-xl bg-amber-50 p-3 text-sm text-amber-900">{t('agentShops.cashNote', { amount: amount !== null ? formatRupees(amount) : '–' })}</p>}

      <h2 className="mt-2 font-bold">{t('agentShops.consent')}</h2>
      <p className="text-sm text-muted">{t('agentShops.consentHint')}</p>
      <button type="button" disabled={busy || form.ownerPhone.trim().length < 10} onClick={() => void sendCode()} className="min-h-12 rounded-xl border border-line font-semibold disabled:opacity-40">
        {codeSent ? t('agentShops.resend') : t('agentShops.sendCode')}
      </button>
      {codeSent && (
        <label className={label}><span className={caption}>{t('agentShops.ownerCode')}</span>
          <input value={form.consentCode} onChange={(e) => setForm({ ...form, consentCode: e.target.value.replace(/\D/g, '').slice(0, 6) })} inputMode="numeric" className={`${box} font-mono tracking-widest`} /></label>
      )}

      {message && <p className={`rounded-xl p-3 text-sm font-semibold ${message.ok ? 'bg-teal-50 text-teal-900' : 'bg-red-50 text-red-800'}`}>{message.text}</p>}
      <Button disabled={busy || !ready} onClick={() => void submit()}>{t('agentShops.submit', { amount: amount !== null ? formatRupees(amount) : '' })}</Button>
    </section>
  )
}

/** This month at a glance, the cash the agent holds for the company, and their commission. */
function AgentHome({ s }: { s: AgentSummary }) {
  const { t } = useTranslation()
  const m = s.thisMonth
  return (
    <>
      <section className="flex flex-col gap-2 rounded-[20px] bg-white p-4">
        <h2 className="font-bold">{t('agentHome.month')}</h2>
        <div className="grid grid-cols-3 gap-2 text-center">
          {([['submitted', m.submitted], ['approved', m.approved], ['paid', m.paid]] as const).map(([k, v]) => (
            <div key={k} className="rounded-xl bg-ground p-2"><div className="text-2xl font-extrabold">{v}</div><div className="text-xs text-muted">{t(`agentShops.count.${k}`)}</div></div>
          ))}
        </div>
        <p className="text-sm">
          {m.badge !== 'none' && <b>{BADGE_ICON[m.badge]} {t(`agentHome.badge.${m.badge}`)} · </b>}
          {m.nextBadgeAt !== null ? t('agentHome.next', { count: m.nextBadgeAt - m.paid }) : t('agentHome.top')}
        </p>
      </section>

      {s.cashToDeposit > 0 && (
        <section className="rounded-[20px] bg-amber-50 p-4 text-amber-900">
          <div className="text-sm font-semibold">{t('agentHome.cash')}</div>
          <div className="text-3xl font-extrabold">{formatRupees(s.cashToDeposit)}</div>
          <div className="text-xs">{t('agentHome.cashHint')}</div>
        </section>
      )}

      <section className="flex flex-col gap-2 rounded-[20px] bg-white p-4">
        <h2 className="font-bold">{t('agentHome.commission')}</h2>
        <div className="grid grid-cols-2 gap-2">
          <div className="rounded-xl bg-emerald-50 p-3 text-emerald-900"><div className="text-xs">{t('agentHome.due')}</div><div className="text-2xl font-extrabold">{formatRupees(s.commissionDue)}</div></div>
          <div className="rounded-xl bg-ground p-3"><div className="text-xs text-muted">{t('agentHome.waiting')}</div><div className="text-2xl font-extrabold">{formatRupees(s.commissionWaiting)}</div></div>
        </div>
        <p className="text-sm">{t('agentHome.totals', { earned: formatRupees(s.commissionEarned), paid: formatRupees(s.commissionPaid) })}</p>
        <p className="text-xs text-muted">{t('agentHome.rules', { first: s.firstPaymentPercent, renewal: s.renewalPercent, days: s.qualifyDays })}</p>
        {s.payouts.length > 0 && (
          <ul className="divide-y divide-[#edf2f1] text-sm">
            {s.payouts.map((p) => (
              <li key={p.id} className="flex justify-between py-2"><span>{formatDateTime(p.paidAt)}{p.reference ? ` · ${p.reference}` : ''}</span><b>{formatRupees(p.amount)}</b></li>
            ))}
          </ul>
        )}
      </section>
      <p className="text-center text-xs text-muted">{t('agentHome.allTime', { shops: s.shopsAllTime, paid: s.paidShopsAllTime })}</p>
    </>
  )
}

/** Where the agent stands this month: rank in their market and overall, the market's leaders, and the badges. */
function AgentAwards({ s }: { s: AgentSummary }) {
  const { t } = useTranslation()
  return (
    <>
      <section className="grid grid-cols-2 gap-2">
        <div className="rounded-[20px] bg-white p-4 text-center"><div className="text-3xl font-extrabold">{s.rankInMarket ? `#${s.rankInMarket}` : '–'}</div><div className="text-xs text-muted">{t('agentAwards.inMarket', { market: s.market, count: s.agentsInMarket })}</div></div>
        <div className="rounded-[20px] bg-white p-4 text-center"><div className="text-3xl font-extrabold">{s.rankOverall ? `#${s.rankOverall}` : '–'}</div><div className="text-xs text-muted">{t('agentAwards.overall')}</div></div>
      </section>
      <section className="flex flex-col gap-2 rounded-[20px] bg-white p-4">
        <h2 className="font-bold">{t('agentAwards.leaders', { market: s.market })}</h2>
        {s.marketLeaders.length === 0 && <p className="text-sm text-muted">{t('agentAwards.none')}</p>}
        <ol className="flex flex-col gap-1">
          {s.marketLeaders.map((l) => (
            <li key={l.code} className={`flex items-center justify-between rounded-xl px-3 py-2 ${l.code === s.code ? 'bg-teal-50 font-bold' : ''}`}>
              <span>{l.rank}. {l.name} <span className="font-mono text-xs text-muted">{l.code}</span></span>
              <span>{BADGE_ICON[l.badge]} {t('agentAwards.paid', { count: l.paid })}</span>
            </li>
          ))}
        </ol>
      </section>
      <section className="flex flex-col gap-1 rounded-[20px] bg-white p-4 text-sm">
        <h2 className="font-bold">{t('agentAwards.badges')}</h2>
        <span>🥉 {t('agentHome.badge.bronze')} · 🥈 {t('agentHome.badge.silver')} · 🥇 {t('agentHome.badge.gold')}</span>
        <span className="text-muted">{t('agentAwards.howBadges')}</span>
      </section>
    </>
  )
}

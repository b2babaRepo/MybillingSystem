import { SHOP_TYPES } from '../lib/reports'
import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, btn } from '../components/ui'
import { api } from '../lib/api'
import { usePlans } from '../lib/billing'
import { shrinkPhoto } from '../lib/images'
import { marketPlace, useMarkets } from '../lib/markets'

export function NewShop({ canBill }: { canBill: boolean }) {
  const { t } = useTranslation()
  const plans = usePlans().data ?? []
  // Empty plan: the shop starts on the usual free trial.
  const markets = useMarkets().data ?? []
  const [marketId, setMarketId] = useState('')
  const emptyIds = { gstin: '', shopNo: '', state: '', district: '', city: '', village: '' }
  const [ids, setIds] = useState(emptyIds)
  const [addItems, setAddItems] = useState(true)
  const [gps, setGps] = useState<{ latitude: number; longitude: number } | null>(null)
  const [photo, setPhoto] = useState<string | null>(null)
  const locate = () => navigator.geolocation.getCurrentPosition(
    (p) => {
      setGps({ latitude: p.coords.latitude, longitude: p.coords.longitude })
      // Fills what it finds; anything it can't find stays as typed.
      void api<Record<'state' | 'district' | 'city' | 'village', string | null>>(`/v1/platform/markets/place?lat=${p.coords.latitude}&lng=${p.coords.longitude}`)
        .then((f) => setIds((x) => ({ ...x, state: f.state ?? x.state, district: f.district ?? x.district, city: f.city ?? x.city, village: f.village ?? x.village })))
        .catch(() => undefined)
    },
    () => setMessage(t('markets.gpsFailed')),
    { enableHighAccuracy: true, timeout: 20_000 },
  )
  // Only the markets of the place entered: the village if given, else the city, else the district or state.
  const same = (a: string | null, b: string) => (a ?? '').trim().toLowerCase() === b.trim().toLowerCase()
  const placeMarkets = markets.filter((m) => m.isActive && (
    ids.village.trim() ? same(m.village, ids.village)
      : ids.city.trim() ? same(m.city, ids.city)
        : ids.district.trim() ? same(m.district, ids.district)
          : ids.state.trim() ? same(m.state, ids.state) : true))
  const chosenMarket = placeMarkets.some((m) => m.id === marketId) ? marketId : ''
  const [paid, setPaid] = useState({ planCode: '', term: 'year1', method: 'cash', reference: '' })
  const queryClient = useQueryClient()
  const empty = { shopName: '', ownerName: '', phone: '', area: '', type: 'kirana', language: 'hi' }
  const [people, setPeople] = useState<PersonDraft[]>([])
  const [form, setForm] = useState(empty)
  const [open, setOpen] = useState(false)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<string | null>(null)
  const [created, setCreated] = useState<{ name: string; phone: string; password: string | null; code: string | null } | null>(null)
  const set = (key: keyof typeof empty) => (e: { target: { value: string } }) => setForm({ ...form, [key]: e.target.value })

  async function create() {
    setBusy(true)
    setMessage(null)
    setCreated(null)
    try {
      const result = await api<{ shopId: string; newAccount: boolean; tempPassword: string | null; shopCode: string | null }>('/v1/platform/shops', 'POST', {
        ...form, shopName: form.shopName.trim(), ownerName: form.ownerName.trim(), phone: form.phone.trim(), area: form.area.trim() || null,
        people: people.filter((p) => p.name.trim() && p.phone.trim()).map(toPerson),
        gstin: ids.gstin.trim() || null, shopNo: ids.shopNo.trim() || null, addTradeItems: addItems,
        state: ids.state.trim() || null, district: ids.district.trim() || null, city: ids.city.trim() || null, village: ids.village.trim() || null,
        marketId: chosenMarket || null, latitude: gps?.latitude ?? null, longitude: gps?.longitude ?? null, photoDataUrl: photo,
      })
      setMarketId('')
      setIds(emptyIds)
      setGps(null)
      setPhoto(null)
      setCreated({ name: form.shopName.trim(), phone: form.phone.trim(), password: result.tempPassword, code: result.shopCode })
      if (paid.planCode) {
        // The shop exists either way: a failed payment can be recorded again from the shop's billing panel.
        await api(`/v1/platform/shops/${result.shopId}/payments`, 'POST', { ...paid, reference: paid.reference.trim() || null })
        setPaid({ ...paid, planCode: '', reference: '' })
      }
      setForm(empty)
      setPeople([])
      await queryClient.invalidateQueries({ queryKey: ['platform'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  const input = 'h-12 rounded-xl border border-line bg-white px-3'
  return (
    <section className={`flex flex-col gap-3 ${open ? 'rounded-2xl bg-white p-4 shadow-sm' : ''}`}>
      <button onClick={() => setOpen(!open)} aria-expanded={open} className={`flex min-h-11 items-center gap-2 self-start rounded-xl px-4 ${open ? btn.outline : btn.solid}`}>
        <span aria-hidden className="text-xl leading-none">{open ? '×' : '+'}</span>{t('openShop.title')}
      </button>
      {open && <>
        <p className="text-sm text-muted">{t('openShop.hint')}</p>
        {canBill && (
          <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('billing.plan')}</span>
            <select value={paid.planCode} onChange={(e) => setPaid({ ...paid, planCode: e.target.value })} className={input}>
              <option value="">{t('plan2.status.trial')}</option>
              {plans.filter((p) => p.code !== 'free').map((p) => <option key={p.code} value={p.code}>{p.name}</option>)}
            </select></label>
        )}
        {canBill && paid.planCode && (
          <div className="grid grid-cols-2 gap-2">
            <select aria-label={t('billing.plan')} value={paid.term} onChange={(e) => setPaid({ ...paid, term: e.target.value })} className={input}>
              {['year1', 'year3', 'year5'].map((term) => <option key={term} value={term}>{t(`plan2.term.${term}`)}</option>)}
            </select>
            <select aria-label={t('billing.plan')} value={paid.method} onChange={(e) => setPaid({ ...paid, method: e.target.value })} className={input}>
              {['cash', 'upi'].map((method) => <option key={method} value={method}>{t(`billing.methods.${method}`)}</option>)}
            </select>
          </div>
        )}
        <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('openShop.shopName')}</span>
          <input value={form.shopName} onChange={set('shopName')} className={input} /></label>
        <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('openShop.ownerName')}</span>
          <input value={form.ownerName} onChange={set('ownerName')} className={input} /></label>
        <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('openShop.phone')}</span>
          <input value={form.phone} onChange={set('phone')} inputMode="tel" className={input} /></label>
        <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('openShop.area')}</span>
          <input value={form.area} onChange={set('area')} className={input} /></label>
        <div className="grid grid-cols-2 gap-2">
          <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('shopType.label')}</span>
            <select value={form.type} onChange={set('type')} className={input}>
              {SHOP_TYPES.map((x) => <option key={x} value={x}>{t(`shopType.${x}`)}</option>)}
            </select></label>
          <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('openShop.language')}</span>
            <select value={form.language} onChange={set('language')} className={input}>
              <option value="hi">हिंदी</option><option value="en">English</option>
            </select></label>
        </div>
        <button type="button" onClick={locate} className="min-h-11 rounded-xl border border-line font-semibold">{gps ? t('markets.gpsDone') : t('markets.gps')}</button>
        <div className="grid grid-cols-2 gap-2">
          {(['state', 'district', 'city', 'village'] as const).map((key) => (
            <label key={key} className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t(`markets.${key}`)}</span>
              <input value={ids[key]} onChange={(e) => setIds({ ...ids, [key]: e.target.value })} maxLength={80} className={`${input} min-w-0`} /></label>
          ))}
        </div>
        <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('markets.market')}</span>
          <select value={chosenMarket} onChange={(e) => {
            setMarketId(e.target.value)
            const m = markets.find((x) => x.id === e.target.value)
            if (m) setIds((x) => ({ ...x, state: m.state ?? x.state, district: m.district ?? x.district, city: m.city ?? x.city, village: m.village ?? x.village }))
          }} className={input}>
            <option value="">{t(placeMarkets.length ? 'markets.none' : 'markets.noneHere')}</option>
            {placeMarkets.map((m) => <option key={m.id} value={m.id}>{m.name}{marketPlace(m) ? `, ${marketPlace(m)}` : ''}</option>)}
          </select></label>
        <div className="grid grid-cols-2 gap-2">
          <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('markets.shopNo')}</span>
            <input value={ids.shopNo} onChange={(e) => setIds({ ...ids, shopNo: e.target.value })} maxLength={30} className={`${input} min-w-0`} /></label>
          <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('markets.gstin')}</span>
            <input value={ids.gstin} onChange={(e) => setIds({ ...ids, gstin: e.target.value.toUpperCase() })} maxLength={15} className={`${input} min-w-0 uppercase`} /></label>
        </div>
        <label className="flex min-h-11 cursor-pointer items-center justify-center rounded-xl border border-line font-semibold">
          {photo ? t('markets.photoDone') : t('markets.photo')}
          <input type="file" accept="image/*" capture="environment" className="sr-only"
            onChange={(e) => { const file = e.target.files?.[0]; if (file) void shrinkPhoto(file, 1024, 140_000, false).then(setPhoto) }} />
        </label>
        <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={addItems} onChange={(e) => setAddItems(e.target.checked)} className="h-5 w-5" />{t('masterItems.addToShop')}</label>
        <PeopleRows people={people} onChange={setPeople} />
        <Button disabled={busy || form.shopName.trim().length < 2 || form.phone.trim().length < 10} onClick={() => void create()}>{t('openShop.create')}</Button>
        {created && (
          <div className="flex flex-col gap-1 rounded-xl bg-brand-soft p-3 text-sm text-brand-deep">
            <span className="font-semibold">{t('openShop.done', { name: created.name })}{created.code && <span className="ml-2 font-mono">{created.code}</span>}</span>
            {created.password
              ? <span>{t('openShop.login', { phone: created.phone })} <span className="font-mono text-base font-bold">{created.password}</span><br />{t('openShop.once')}</span>
              : <span>{t('openShop.existing')}</span>}
          </div>
        )}
        {message && <p className="rounded-xl bg-due-soft p-3 text-sm font-semibold text-red-800">{message}</p>}
      </>}
    </section>
  )
}

interface PersonDraft { name: string; phone: string; role: 'owner' | 'staff'; pin: string }
const toPerson = (p: PersonDraft) => ({ name: p.name.trim(), phone: p.phone.trim(), role: p.role, pin: p.role === 'staff' ? p.pin : null })

/** Extra owners and helpers typed in while opening a shop; the plan (trial: Gold) sets how many fit. */
function PeopleRows({ people, onChange }: { people: PersonDraft[]; onChange: (p: PersonDraft[]) => void }) {
  const { t } = useTranslation()
  const set = (i: number, patch: Partial<PersonDraft>) => onChange(people.map((p, j) => (j === i ? { ...p, ...patch } : p)))
  const box = 'h-11 min-w-0 rounded-xl border border-line bg-white px-2'
  return (
    <div className="flex flex-col gap-2">
      {people.map((p, i) => (
        <div key={i} className="grid grid-cols-2 gap-2 rounded-xl bg-ground p-2 text-sm">
          <select aria-label={t('staff.roleLabel')} value={p.role} onChange={(e) => set(i, { role: e.target.value as PersonDraft['role'] })} className={box}>
            <option value="owner">{t('staff.owner')}</option><option value="staff">{t('staff.helper')}</option>
          </select>
          <button type="button" onClick={() => onChange(people.filter((_, j) => j !== i))} className="min-h-11 rounded-xl border border-line font-semibold">{t('plans.cancel')}</button>
          <input aria-label={t('staff.name')} placeholder={t('staff.name')} value={p.name} onChange={(e) => set(i, { name: e.target.value })} className={box} />
          <input aria-label={t('staff.phone')} placeholder={t('staff.phone')} value={p.phone} inputMode="tel" onChange={(e) => set(i, { phone: e.target.value })} className={box} />
          {p.role === 'staff' && <input aria-label={t('staff.pin')} placeholder={t('staff.pin')} value={p.pin} inputMode="numeric"
            onChange={(e) => set(i, { pin: e.target.value.replace(/\D/g, '').slice(0, 6) })} className={`${box} col-span-2`} />}
        </div>
      ))}
      <button type="button" onClick={() => onChange([...people, { name: '', phone: '', role: 'owner', pin: '' }])}
        className={`min-h-11 self-start ${btn.add}`}>+ {t('people.addMore')}</button>
    </div>
  )
}

/** In a shop's admin detail: add an owner or a helper (with a PIN), within the shop's plan. */
export function AddPerson({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [draft, setDraft] = useState<PersonDraft[]>([])
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  async function save() {
    setBusy(true)
    setMessage(null)
    try {
      for (const p of draft.filter((x) => x.name.trim() && x.phone.trim())) await api(`/v1/platform/shops/${shopId}/people`, 'POST', toPerson(p))
      setDraft([])
      await queryClient.invalidateQueries({ queryKey: ['platform'] })
      setMessage(t('people.added'))
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }
  return (
    <div className="flex flex-col gap-2">
      <PeopleRows people={draft} onChange={setDraft} />
      {draft.length > 0 && <Button disabled={busy} onClick={() => void save()}>{t('people.save')}</Button>}
      {message && <p className="rounded-xl bg-brand-soft p-3 text-sm font-semibold text-brand-deep">{message}</p>}
    </div>
  )
}

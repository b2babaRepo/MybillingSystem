import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { btn } from '../lib/buttonStyles'
import { api, request } from '../lib/api'
import { shrinkPhoto } from '../lib/images'
import { VoiceButton } from '../components/VoiceButton'
import { formatRupees, whatsAppLink } from '../lib/format'
import { navigate } from '../router'
import { ask, confirmAsk } from '../lib/ask'
import { parseGrid } from '../lib/dairyGrid'
import { useNow } from '../lib/useNow'

type Kind = 'cow' | 'buffalo' | 'mixed'
type Shift = 'morning' | 'evening'
interface Farmer { id: string; code: number; name: string; phone: string | null; village: string | null; defaultKind: Kind; isActive: boolean; due: number }
interface Collection { id: string; farmerCode: number; farmerName: string; phone: string | null; kind: Kind; litres: number; fat: number; snf: number | null; rate: number; amount: number; belowStandard: boolean; rejected: boolean; edited: boolean }
interface ShiftDay { date: string; shift: Shift; collections: Collection[]; litres: number; amount: number; avgFat: number }
interface Plan { id: string; for: 'farmers' | 'dairy'; from: string; method: 'fat_snf' | 'fat_only' | 'flat' | 'grid'; gridCells?: number; gridMinFat?: number | null; gridMaxFat?: number | null; gridMinSnf?: number | null; gridMaxSnf?: number | null; rates: { kind: Kind; fatRate: number; snfRate: number; flatRate: number; minFat: number; minSnf: number }[] }
interface Bill { farmerId: string; code: number; name: string; phone: string | null; litres: number; avgFat: number; milkAmount: number; feed: number; advance: number; bonus: number; paidInPeriod: number; opening: number; due: number; collections: number }
interface Dispatch { id: string; date: string; shift: Shift; agency: string; kind: Kind; litres: number; fat: number; snf: number; acceptedLitres: number | null; acceptedFat: number | null; acceptedSnf: number | null; amount: number | null; paidOn: string | null }
interface Summary { collectedLitres: number; collectedFatKg: number; paidToFarmers: number; dispatchedLitres: number; dispatchedFatKg: number; acceptedLitres: number; acceptedFatKg: number; fromDairy: number; dairyUnpaid: number; margin: number; rejected: number; belowStandard: number; dispatchesAwaitingDairy: number }

const select = 'h-12 w-full rounded-xl border border-line bg-white px-3'
const iso = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
const nowShift = (): Shift => (new Date().getHours() < 14 ? 'morning' : 'evening')

interface Flag { kind: string; date: string; farmerName: string | null; detail: string }
interface Match { line: { date: string | null; shift: Shift | null; litres: number | null; fat: number | null; snf: number | null; amount: number | null }; dispatchId: string | null; sentLitres: number | null; sentFat: number | null; litresShort: number | null; fatKgShort: number | null }

/** Photo reading (testing slip, dairy statement) is an AI service: shown only when switched on, and after the person agrees. */
function useAiPhotos() {
  const queryClient = useQueryClient()
  const ai = useQuery({ queryKey: ['ai-status'], queryFn: () => api<{ consentedAt: string | null; canReadPhotos: boolean; canHearVoice: boolean }>('/v1/me/ai') }).data
  const agree = async () => { await request('/v1/me/ai/consent', 'POST', { agree: true }); await queryClient.invalidateQueries({ queryKey: ['ai-status'] }) }
  return { canRead: !!ai?.canReadPhotos, canHear: !!ai?.canHearVoice, consented: !!ai?.consentedAt, agree }
}

function PhotoButton({ label, busy, onFile }: { label: string; busy: boolean; onFile: (file: File) => void }) {
  const { t } = useTranslation()
  const ai = useAiPhotos()
  const [ask, setAsk] = useState(false)
  if (!ai.canRead) return null
  if (!ai.consented) return (
    <>
      <button onClick={() => setAsk(true)} className={`min-h-11 rounded-xl ${btn.outline}`}>📷 {label}</button>
      {ask && <div className="flex flex-col gap-2 rounded-xl bg-warn-soft p-2 text-sm"><p>{t('dairy.photoConsent')}</p><button onClick={() => void ai.agree()} className={`h-10 rounded-xl ${btn.solid}`}>{t('expense.consentYes')}</button></div>}
    </>
  )
  return (
    <label className={`flex min-h-11 cursor-pointer items-center justify-center rounded-xl border-2 border-brand font-semibold text-brand-strong ${busy ? 'opacity-50' : ''}`}>
      📷 {busy ? t('invoicePhoto.reading') : label}
      <input type="file" accept="image/*" capture="environment" className="sr-only" disabled={busy} onChange={(e) => { const f = e.target.files?.[0]; e.target.value = ''; if (f) onFile(f) }} />
    </label>
  )
}

/** On a dairy centre's home: the shift so far, and the way in. */
export function DairyHomeCard({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const day = useQuery({ queryKey: [shopId, 'dairy', 'shift', 'now'], queryFn: () => api<ShiftDay>('/v1/dairy/collections') }).data
  return (
    <button onClick={() => navigate('/dairy')} className="mx-4 mt-3 flex w-[calc(100%-2rem)] flex-col gap-1 rounded-card bg-white p-3 text-left">
      <span className="flex justify-between font-bold"><span>🥛 {t('dairy.homeTitle')}</span><span aria-hidden="true">→</span></span>
      {day && <span className="text-sm text-muted">{t(`dairy.shift_${day.shift}`)}: {t('dairy.shiftLine', { count: day.collections.length, litres: day.litres, fat: day.avgFat, amount: formatRupees(day.amount) })}</span>}
    </button>
  )
}

/** The milk collection centre: today's shift at the counter, farmers, rates, bills and milk sent to the dairy. */
export function DairyScreen({ shopId, shopName, isOwner }: { shopId: string; shopName: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const tabs = (['collect', 'farmers', 'bills', 'dispatch', 'rates', 'helper'] as const).filter((x) => isOwner || x === 'collect' || x === 'farmers' || x === 'dispatch')
  const [tab, setTab] = useState<(typeof tabs)[number]>('collect')
  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <div role="tablist" className="grid gap-1 rounded-2xl bg-white p-1 text-sm font-semibold" style={{ gridTemplateColumns: `repeat(${tabs.length}, 1fr)` }}>
        {tabs.map((x) => <button key={x} role="tab" aria-selected={tab === x} onClick={() => setTab(x)} className={`min-h-11 rounded-xl ${tab === x ? 'bg-brand text-white' : ''}`}>{t(`dairy.tab_${x}`)}</button>)}
      </div>
      {tab === 'collect' && <Collect shopId={shopId} shopName={shopName} isOwner={isOwner} />}
      {tab === 'farmers' && <Farmers shopId={shopId} />}
      {tab === 'bills' && <Bills shopId={shopId} shopName={shopName} />}
      {tab === 'dispatch' && <Dispatches shopId={shopId} isOwner={isOwner} />}
      {tab === 'rates' && <Rates shopId={shopId} />}
      {tab === 'helper' && <Helper shopId={shopId} shopName={shopName} />}
    </div>
  )
}

function useRun(shopId: string) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [message, setMessage] = useState('')
  const run = async (work: () => Promise<unknown>, done: string) => {
    setMessage('')
    try {
      await work()
      setMessage(done)
      void queryClient.invalidateQueries({ queryKey: [shopId, 'dairy'] })
      return true
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
      return false
    }
  }
  return { message, run }
}

/** The counter: farmer code, milk, litres, fat and SNF (or the lactometer reading). The amount shows before saving; then a WhatsApp slip. */
function Collect({ shopId, shopName, isOwner }: { shopId: string; shopName: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const [date, setDate] = useState(() => iso(new Date()))
  const [shift, setShift] = useState<Shift>(nowShift())
  const farmers = useQuery({ queryKey: [shopId, 'dairy', 'farmers'], queryFn: () => api<Farmer[]>('/v1/dairy/farmers') }).data ?? []
  const day = useQuery({ queryKey: [shopId, 'dairy', 'shift', date, shift], queryFn: () => api<ShiftDay>(`/v1/dairy/collections?date=${date}&shift=${shift}`) }).data
  const blank = { code: '', kind: '' as Kind | '', litres: '', fat: '', snf: '', clr: '', rejected: false }
  const [d, setD] = useState(blank)
  const farmer = farmers.find((f) => String(f.code) === d.code.trim() && f.isActive)
  const kind: Kind = d.kind || farmer?.defaultKind || 'buffalo'
  const [quote, setQuote] = useState<{ amount: number; perLitre: number; snf: number; belowStandard: boolean } | null>(null)
  const [quoteError, setQuoteError] = useState('')
  const { message, run } = useRun(shopId)
  const [last, setLast] = useState<Collection | null>(null)

  const quoteFor = [date, kind, d.litres, d.fat, d.snf, d.clr].join('|')
  const [quotedFor, setQuotedFor] = useState(quoteFor)
  if (quotedFor !== quoteFor) {
    setQuotedFor(quoteFor)
    setQuote(null)
    setQuoteError('')
  }
  useEffect(() => {
    if (!(Number(d.litres) > 0 && Number(d.fat) > 0)) return
    const q = new URLSearchParams({ date, kind, litres: d.litres, fat: d.fat, ...(d.snf ? { snf: d.snf } : d.clr ? { clr: d.clr } : {}) })
    const timer = setTimeout(() => { api<typeof quote>(`/v1/dairy/quote?${q}`).then(setQuote).catch((e: Error) => setQuoteError(e.message)) }, 300)
    return () => clearTimeout(timer)
  }, [date, kind, d.litres, d.fat, d.snf, d.clr])

  const ai = useAiPhotos()
  const [heard, setHeard] = useState('')
  // Said at the counter ("ek number das litre bhains fat saadhe chhe"): the fields fill, and what was heard stays on screen to check.
  const fromVoice = async (said: string) => {
    setHeard(said)
    try {
      const e = await api<{ farmerCode: number | null; kind: Kind | null; litres: number | null; fat: number | null; snf: number | null; clr: number | null }>('/v1/dairy/parse-entry', 'POST', { question: said })
      setD((cur) => ({
        ...cur, code: e.farmerCode ? String(e.farmerCode) : cur.code, kind: e.kind ?? cur.kind, litres: e.litres ? String(e.litres) : cur.litres,
        fat: e.fat ? String(e.fat) : cur.fat, snf: e.snf ? String(e.snf) : cur.snf, clr: e.clr ? String(e.clr) : cur.clr,
      }))
    } catch (err) {
      setQuoteError(err instanceof Error ? err.message : t('error'))
    }
  }
  const [reading, setReading] = useState(false)
  const readSlip = async (file: File) => {
    setReading(true)
    try {
      const s = await api<{ litres: number | null; fat: number | null; snf: number | null; clr: number | null; farmerCode: number | null }>('/v1/dairy/slip-photo', 'POST', { imageDataUrl: await shrinkPhoto(file, 1400, 1_000_000, false) })
      setD((cur) => ({ ...cur, code: s.farmerCode ? String(s.farmerCode) : cur.code, litres: s.litres ? String(s.litres) : cur.litres, fat: s.fat ? String(s.fat) : cur.fat, snf: s.snf ? String(s.snf) : cur.snf, clr: s.clr ? String(s.clr) : cur.clr }))
    } catch (e) {
      setQuoteError(e instanceof Error ? e.message : t('error'))
    } finally {
      setReading(false)
    }
  }

  const save = () => run(async () => {
    const saved = await api<Collection>('/v1/dairy/collections', 'POST', {
      id: crypto.randomUUID(), date, shift, farmerId: farmer!.id, kind, litres: Number(d.litres), fat: Number(d.fat),
      snf: d.snf ? Number(d.snf) : null, clr: !d.snf && d.clr ? Number(d.clr) : null, rejected: d.rejected,
    })
    setLast(saved)
    setD(blank)
    setHeard('')
  }, t('saved'))

  const slip = (c: Collection) => t(c.rejected ? 'dairy.slipRejected' : 'dairy.slip', {
    shop: shopName, name: c.farmerName, date, shift: t(`dairy.shift_${shift}`), kind: t(`dairy.kind_${c.kind}`), litres: c.litres, fat: c.fat, snf: c.snf ?? '-', rate: c.rate, amount: formatRupees(c.amount),
  })

  return (
    <>
      <section className="flex flex-col gap-2 rounded-card bg-white p-3">
        <div className="grid grid-cols-2 gap-2">
          <Field label={t('dairy.date')} type="date" value={date} onChange={(e) => setDate(e.target.value)} />
          <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('dairy.shift')}</span>
            <select value={shift} onChange={(e) => setShift(e.target.value as Shift)} className={select}><option value="morning">{t('dairy.shift_morning')}</option><option value="evening">{t('dairy.shift_evening')}</option></select>
          </label>
        </div>
        <div className="grid grid-cols-[1fr_2fr] gap-2">
          <Field label={t('dairy.code')} value={d.code} onChange={(e) => setD({ ...d, code: e.target.value })} inputMode="numeric" autoFocus />
          <div className="flex min-h-12 items-end pb-2 text-lg font-bold">{farmer ? `${farmer.name}${farmer.village ? ` · ${farmer.village}` : ''}` : d.code ? <span className="text-sm text-due">{t('dairy.noFarmer')}</span> : null}</div>
        </div>
        <div className="grid grid-cols-3 gap-1" role="radiogroup" aria-label={t('dairy.kind')}>
          {(['buffalo', 'cow', 'mixed'] as const).map((k) => <button key={k} role="radio" aria-checked={kind === k} onClick={() => setD({ ...d, kind: k })} className={`min-h-11 rounded-xl font-semibold ${kind === k ? 'bg-brand text-white' : 'border border-line'}`}>{t(`dairy.kind_${k}`)}</button>)}
        </div>
        <div className="grid grid-cols-2 gap-2">
          <Field label={t('dairy.litres')} value={d.litres} onChange={(e) => setD({ ...d, litres: e.target.value })} inputMode="decimal" />
          <Field label={t('dairy.fat')} value={d.fat} onChange={(e) => setD({ ...d, fat: e.target.value })} inputMode="decimal" />
          <Field label={t('dairy.snf')} value={d.snf} onChange={(e) => setD({ ...d, snf: e.target.value })} inputMode="decimal" />
          <Field label={t('dairy.clr')} value={d.clr} onChange={(e) => setD({ ...d, clr: e.target.value })} inputMode="decimal" placeholder={t('dairy.clrHint')} />
        </div>
        <PhotoButton label={t('dairy.readSlip')} busy={reading} onFile={(f) => void readSlip(f)} />
        {ai.canHear && ai.consented && <VoiceButton onText={(said) => void fromVoice(said)} onError={(m) => setQuoteError(m ?? '')} />}
        {heard && <p className="rounded-xl bg-ground p-2 text-sm">{t('dairy.heard', { text: heard })}</p>}
        <label className="flex min-h-11 items-center gap-2 text-sm"><input type="checkbox" checked={d.rejected} onChange={(e) => setD({ ...d, rejected: e.target.checked })} />{t('dairy.rejected')}</label>
        {quote && <p className={`rounded-xl p-2 text-lg font-bold ${quote.belowStandard ? 'bg-warn-soft' : 'bg-brand-soft text-brand-deep'}`}>{d.rejected ? t('dairy.notPaid') : `${formatRupees(quote.amount)} · ₹${quote.perLitre}/L`}{!d.snf && d.clr ? ` · SNF ${quote.snf}` : ''}{quote.belowStandard ? ` · ${t('dairy.below')}` : ''}</p>}
        {quoteError && <p className="text-sm text-due">{quoteError}</p>}
        <Button disabled={!farmer || !(Number(d.litres) > 0) || !(Number(d.fat) > 0)} onClick={() => void save()}>{t('dairy.save')}</Button>
        {message && <p className="text-sm font-semibold text-brand-deep">{message}</p>}
        {last && last.phone && <a href={whatsAppLink(last.phone, slip(last))} target="_blank" rel="noreferrer" className="flex h-11 items-center justify-center rounded-xl bg-[#15803d] font-semibold text-white">{t('dairy.sendSlip', { name: last.farmerName })}</a>}
      </section>

      {day && (
        <section className="flex flex-col gap-1 rounded-card bg-white p-3">
          <h3 className="flex justify-between font-bold"><span>{t(`dairy.shift_${day.shift}`)}</span><span>{day.litres} L · {t('dairy.avgFat', { fat: day.avgFat })} · {formatRupees(day.amount)}</span></h3>
          {day.collections.length === 0 && <p className="text-sm text-muted">{t('dairy.noneYet')}</p>}
          {[...day.collections].reverse().map((c) => (
            <div key={c.id} className={`flex items-center justify-between gap-2 border-b border-line py-1 text-sm ${c.rejected ? 'text-muted line-through' : ''}`}>
              <span>#{c.farmerCode} {c.farmerName} · {t(`dairy.kind_${c.kind}`)} {c.litres} L · {c.fat}/{c.snf ?? '-'}{c.belowStandard ? ' ⚠' : ''}{c.edited ? ` · ${t('dairy.edited')}` : ''}</span>
              <span className="flex shrink-0 items-center gap-1 font-semibold">{formatRupees(c.amount)}
                {c.phone && <a href={whatsAppLink(c.phone, slip(c))} target="_blank" rel="noreferrer" aria-label={t('dairy.sendSlip', { name: c.farmerName })} className="px-1 text-[#15803d]">✉</a>}
                {isOwner && <EditButton shopId={shopId} c={c} date={date} shift={shift} />}
              </span>
            </div>
          ))}
        </section>
      )}
    </>
  )
}

/** The owner corrects a reading; the server keeps the old figures in the edit log. */
function EditButton({ shopId, c, date, shift }: { shopId: string; c: Collection; date: string; shift: Shift }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const edit = async () => {
    const litres = (await ask(t('dairy.litres'), String(c.litres))); if (litres === null) return
    const fat = (await ask(t('dairy.fat'), String(c.fat))); if (fat === null) return
    const snf = (await ask(t('dairy.snf'), c.snf == null ? '' : String(c.snf))); if (snf === null) return
    try {
      await api(`/v1/dairy/collections/${c.id}`, 'PUT', { id: c.id, date, shift, farmerId: '00000000-0000-0000-0000-000000000000', kind: c.kind, litres: Number(litres), fat: Number(fat), snf: snf ? Number(snf) : null, clr: null, rejected: c.rejected })
      void queryClient.invalidateQueries({ queryKey: [shopId, 'dairy'] })
    } catch (e) {
      alert(e instanceof Error ? e.message : t('error'))
    }
  }
  return <button onClick={() => void edit()} aria-label={t('dairy.edit')} className="px-1 text-brand">✎</button>
}

function Farmers({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const farmers = useQuery({ queryKey: [shopId, 'dairy', 'farmers'], queryFn: () => api<Farmer[]>('/v1/dairy/farmers') }).data ?? []
  const blank = { name: '', phone: '', village: '', kind: 'buffalo' as Kind }
  const [d, setD] = useState(blank)
  const [entry, setEntry] = useState<{ id: string; kind: 'feed' | 'advance' | 'bonus'; amount: string; note: string } | null>(null)
  const { message, run } = useRun(shopId)
  return (
    <>
      <section className="flex flex-col gap-2 rounded-card bg-white p-3">
        <h3 className="font-bold">{t('dairy.addFarmer')}</h3>
        <p className="rounded-xl bg-brand-soft p-2 text-xs text-brand-deep">{t('dairy.botHint')}</p>
        <div className="grid grid-cols-2 gap-2">
          <Field label={t('dairy.name')} value={d.name} onChange={(e) => setD({ ...d, name: e.target.value })} />
          <Field label={t('phone')} value={d.phone} onChange={(e) => setD({ ...d, phone: e.target.value })} inputMode="tel" />
          <Field label={t('dairy.village')} value={d.village} onChange={(e) => setD({ ...d, village: e.target.value })} />
          <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('dairy.kind')}</span>
            <select value={d.kind} onChange={(e) => setD({ ...d, kind: e.target.value as Kind })} className={select}>{(['buffalo', 'cow', 'mixed'] as const).map((k) => <option key={k} value={k}>{t(`dairy.kind_${k}`)}</option>)}</select>
          </label>
        </div>
        <Button disabled={d.name.trim().length < 2} onClick={() => void run(async () => { await api('/v1/dairy/farmers', 'POST', { id: crypto.randomUUID(), name: d.name, phone: d.phone, village: d.village, defaultKind: d.kind }); setD(blank) }, t('saved'))}>{t('save')}</Button>
        {message && <p className="text-sm font-semibold text-brand-deep">{message}</p>}
      </section>
      <section className="flex flex-col gap-1 rounded-card bg-white p-3">
        {farmers.length === 0 && <p className="text-sm text-muted">{t('dairy.noFarmers')}</p>}
        {farmers.map((f) => (
          <div key={f.id} className="border-b border-line py-1">
            <p className="flex justify-between text-sm"><span><b>#{f.code} {f.name}</b>{f.village ? ` · ${f.village}` : ''} · {t(`dairy.kind_${f.defaultKind}`)}</span><span className={f.due < 0 ? 'text-due' : ''}>{t(f.due < 0 ? 'dairy.owesUs' : 'dairy.weOwe', { amount: formatRupees(Math.abs(f.due)) })}</span></p>
            {entry?.id === f.id ? (
              <div className="mt-1 grid grid-cols-[1fr_1fr_auto] items-end gap-2">
                <select value={entry.kind} onChange={(e) => setEntry({ ...entry, kind: e.target.value as 'feed' })} aria-label={t('dairy.entryKind')} className={select}>{(['feed', 'advance', 'bonus'] as const).map((k) => <option key={k} value={k}>{t(`dairy.entry_${k}`)}</option>)}</select>
                <Field label={t('bridal.amount')} value={entry.amount} onChange={(e) => setEntry({ ...entry, amount: e.target.value })} inputMode="decimal" />
                <Button className="h-12 px-3" disabled={!(Number(entry.amount) > 0)} onClick={() => void run(async () => { await api(`/v1/dairy/farmers/${f.id}/entries`, 'POST', { id: crypto.randomUUID(), kind: entry.kind, amount: Number(entry.amount), mode: 'cash', note: entry.note || null }); setEntry(null) }, t('saved'))}>{t('save')}</Button>
              </div>
            ) : <button onClick={() => setEntry({ id: f.id, kind: 'feed', amount: '', note: '' })} className="min-h-10 text-xs font-semibold text-brand underline">{t('dairy.addEntry')}</button>}
          </div>
        ))}
      </section>
    </>
  )
}

/** Bills for a cycle (10 days by default): each farmer's milk, deductions and what's due, with a WhatsApp statement and the payout. */
function Bills({ shopId, shopName }: { shopId: string; shopName: string }) {
  const nowMs = useNow()
  const { t } = useTranslation()
  const today = new Date(nowMs)
  const [from, setFrom] = useState(iso(new Date(today.getFullYear(), today.getMonth(), today.getDate() <= 10 ? 1 : today.getDate() <= 20 ? 11 : 21)))
  const [to, setTo] = useState(iso(today))
  const bills = useQuery({ queryKey: [shopId, 'dairy', 'bills', from, to], queryFn: () => api<Bill[]>(`/v1/dairy/bills?from=${from}&to=${to}`) }).data ?? []
  const [mode, setMode] = useState<'cash' | 'upi'>('upi')
  const { message, run } = useRun(shopId)
  const statement = (b: Bill) => t('dairy.statement', {
    shop: shopName, name: b.name, from, to, litres: b.litres, fat: b.avgFat, milk: formatRupees(b.milkAmount), feed: formatRupees(b.feed), advance: formatRupees(b.advance),
    bonus: formatRupees(b.bonus), paid: formatRupees(b.paidInPeriod), opening: formatRupees(b.opening), due: formatRupees(b.due),
  })
  return (
    <>
      <div className="grid grid-cols-[1fr_1fr_6rem] items-end gap-2">
        <Field label={t('rx.from')} type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
        <Field label={t('rx.to')} type="date" value={to} onChange={(e) => setTo(e.target.value)} />
        <select value={mode} onChange={(e) => setMode(e.target.value as 'upi')} aria-label={t('jobs.mode')} className={select}><option value="upi">UPI</option><option value="cash">{t('cash')}</option></select>
      </div>
      {message && <p className="text-sm font-semibold text-brand-deep">{message}</p>}
      <p className="text-sm font-semibold">{t('dairy.billsTotal', { count: bills.length, litres: bills.reduce((s, b) => s + b.litres, 0).toFixed(2), due: formatRupees(bills.reduce((s, b) => s + Math.max(0, b.due), 0)) })}</p>
      {bills.length === 0 && <p className="rounded-card bg-white p-3 text-sm text-muted">{t('dairy.noBills')}</p>}
      {bills.map((b) => (
        <section key={b.farmerId} className="flex flex-col gap-1 rounded-card bg-white p-3 text-sm">
          <p className="flex justify-between font-bold"><span>#{b.code} {b.name}</span><span className={b.due < 0 ? 'text-due' : ''}>{t(b.due < 0 ? 'dairy.owesUs' : 'dairy.weOwe', { amount: formatRupees(Math.abs(b.due)) })}</span></p>
          <p>{t('dairy.billLine', { count: b.collections, litres: b.litres, fat: b.avgFat, milk: formatRupees(b.milkAmount) })}</p>
          {(b.feed > 0 || b.advance > 0 || b.bonus > 0 || b.opening !== 0 || b.paidInPeriod > 0) && <p className="text-xs text-muted">{t('dairy.billDeductions', { feed: formatRupees(b.feed), advance: formatRupees(b.advance), bonus: formatRupees(b.bonus), opening: formatRupees(b.opening), paid: formatRupees(b.paidInPeriod) })}</p>}
          <div className="mt-1 flex gap-2">
            {b.phone && <a href={whatsAppLink(b.phone, statement(b))} target="_blank" rel="noreferrer" className="flex h-10 flex-1 items-center justify-center rounded-xl bg-[#15803d] font-semibold text-white">{t('dairy.sendStatement')}</a>}
            {b.due > 0 && <Button className="h-10 flex-1 text-sm" onClick={async () => { if ((await confirmAsk(t('dairy.payConfirm', { amount: formatRupees(b.due), name: b.name })))) void run(() => api(`/v1/dairy/farmers/${b.farmerId}/entries`, 'POST', { id: crypto.randomUUID(), kind: 'payout', amount: b.due, mode }), t('dairy.paid')) }}>{t('dairy.pay', { amount: formatRupees(b.due) })}</Button>}
          </div>
        </section>
      ))}
    </>
  )
}

/** Milk handed to Sudha or another dairy, the dairy's own figures from its statement, and the month's milk balance. */
function Dispatches({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const list = useQuery({ queryKey: [shopId, 'dairy', 'dispatches'], queryFn: () => api<Dispatch[]>('/v1/dairy/dispatches') }).data ?? []
  const summary = useQuery({ queryKey: [shopId, 'dairy', 'summary'], enabled: isOwner, queryFn: () => api<Summary>('/v1/dairy/summary') }).data
  const [d, setD] = useState({ agency: list[0]?.agency ?? 'Sudha (COMFED)', kind: 'mixed' as Kind, shift: nowShift(), litres: '', fat: '', snf: '' })
  const [acc, setAcc] = useState<{ id: string; litres: string; fat: string; snf: string; amount: string } | null>(null)
  const { message, run } = useRun(shopId)
  const flags = useQuery({ queryKey: [shopId, 'dairy', 'flags'], enabled: isOwner, queryFn: () => api<Flag[]>('/v1/dairy/flags') }).data ?? []
  const [matches, setMatches] = useState<Match[] | null>(null)
  const [reading, setReading] = useState(false)
  const readStatement = async (file: File) => {
    setReading(true)
    await run(async () => setMatches(await api<Match[]>('/v1/dairy/statement-photo', 'POST', { imageDataUrl: await shrinkPhoto(file, 1600, 1_200_000, false) })), t('dairy.statementRead'))
    setReading(false)
  }
  return (
    <>
      {isOwner && flags.length > 0 && (
        <section className="flex flex-col gap-1 rounded-card bg-warn-soft p-3 text-sm">
          <h3 className="font-bold">{t('dairy.flagsTitle', { count: flags.length })}</h3>
          <p className="text-xs text-muted">{t('dairy.flagsHint')}</p>
          {flags.map((f, i) => <p key={i}>⚠ {f.date} · {f.farmerName ? `${f.farmerName} · ` : ''}<b>{t(`dairy.flag_${f.kind}`)}</b>: {f.detail}</p>)}
        </section>
      )}
      {isOwner && (
        <section className="flex flex-col gap-2 rounded-card bg-white p-3">
          <h3 className="font-bold">{t('dairy.statementTitle')}</h3>
          <p className="text-xs text-muted">{t('dairy.statementHint')}</p>
          <PhotoButton label={t('dairy.readStatement')} busy={reading} onFile={(f) => void readStatement(f)} />
          {matches?.map((m, i) => (
            <div key={i} className={`flex flex-col gap-1 rounded-xl p-2 text-sm ${m.dispatchId ? 'bg-ground' : 'bg-due-soft'}`}>
              <p>{m.line.date ?? '?'} · {m.line.shift ? t(`dairy.shift_${m.line.shift}`) : '?'} · {m.line.litres ?? '?'} L · {m.line.fat ?? '?'}/{m.line.snf ?? '?'} · {m.line.amount != null ? formatRupees(m.line.amount) : '?'}</p>
              {m.dispatchId ? <p className={(m.litresShort ?? 0) > 0 ? 'text-due' : 'text-brand-strong'}>{t('dairy.matchLine', { sent: m.sentLitres, short: m.litresShort ?? '?', fat: m.fatKgShort ?? '?' })}</p> : <p className="text-due">{t('dairy.noMatch')}</p>}
              {m.dispatchId && m.line.litres != null && m.line.fat != null && m.line.snf != null && (
                <Button tone="plain" className="h-10 text-sm" onClick={() => void run(async () => {
                  await api(`/v1/dairy/dispatches/${m.dispatchId}/accepted`, 'PUT', { acceptedLitres: m.line.litres, acceptedFat: m.line.fat, acceptedSnf: m.line.snf, amount: m.line.amount })
                  setMatches((cur) => cur?.filter((_, j) => j !== i) ?? null)
                }, t('saved'))}>{t('dairy.useFigures')}</Button>
              )}
            </div>
          ))}
        </section>
      )}
      {summary && (
        <section className="flex flex-col gap-1 rounded-card bg-white p-3 text-sm">
          <h3 className="font-bold">{t('dairy.monthTitle')}</h3>
          <p className="flex justify-between"><span>{t('dairy.collected')}</span><span>{summary.collectedLitres} L · {summary.collectedFatKg} kg fat</span></p>
          <p className="flex justify-between"><span>{t('dairy.sent')}</span><span>{summary.dispatchedLitres} L · {summary.dispatchedFatKg} kg fat</span></p>
          <p className="flex justify-between"><span>{t('dairy.accepted')}</span><span>{summary.acceptedLitres} L · {summary.acceptedFatKg} kg fat</span></p>
          {summary.dispatchedLitres > 0 && <p className={`flex justify-between font-semibold ${summary.collectedLitres - summary.dispatchedLitres > summary.collectedLitres * 0.01 ? 'text-due' : ''}`}><span>{t('dairy.gap')}</span><span>{(summary.collectedLitres - summary.dispatchedLitres).toFixed(2)} L · {(summary.collectedFatKg - summary.dispatchedFatKg).toFixed(2)} kg fat</span></p>}
          <p className="flex justify-between"><span>{t('dairy.paidFarmers')}</span><span>{formatRupees(summary.paidToFarmers)}</span></p>
          <p className="flex justify-between"><span>{t('dairy.fromDairy')}</span><span>{formatRupees(summary.fromDairy)}</span></p>
          <p className="flex justify-between font-bold"><span>{t('dairy.margin')}</span><span>{formatRupees(summary.margin)}</span></p>
          {summary.dispatchesAwaitingDairy > 0 && <p className="text-xs text-muted">{t('dairy.awaiting', { count: summary.dispatchesAwaitingDairy })}</p>}
          {summary.dairyUnpaid > 0 && <p className="text-xs text-due">{t('dairy.unpaid', { amount: formatRupees(summary.dairyUnpaid) })}</p>}
        </section>
      )}
      <section className="flex flex-col gap-2 rounded-card bg-white p-3">
        <h3 className="font-bold">{t('dairy.sendMilk')}</h3>
        <div className="grid grid-cols-2 gap-2">
          <Field label={t('dairy.agency')} value={d.agency} onChange={(e) => setD({ ...d, agency: e.target.value })} />
          <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('dairy.shift')}</span>
            <select value={d.shift} onChange={(e) => setD({ ...d, shift: e.target.value as Shift })} className={select}><option value="morning">{t('dairy.shift_morning')}</option><option value="evening">{t('dairy.shift_evening')}</option></select>
          </label>
          <Field label={t('dairy.litres')} value={d.litres} onChange={(e) => setD({ ...d, litres: e.target.value })} inputMode="decimal" />
          <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('dairy.kind')}</span>
            <select value={d.kind} onChange={(e) => setD({ ...d, kind: e.target.value as Kind })} className={select}>{(['mixed', 'buffalo', 'cow'] as const).map((k) => <option key={k} value={k}>{t(`dairy.kind_${k}`)}</option>)}</select>
          </label>
          <Field label={t('dairy.fat')} value={d.fat} onChange={(e) => setD({ ...d, fat: e.target.value })} inputMode="decimal" />
          <Field label={t('dairy.snf')} value={d.snf} onChange={(e) => setD({ ...d, snf: e.target.value })} inputMode="decimal" />
        </div>
        <Button disabled={!(Number(d.litres) > 0 && Number(d.fat) > 0 && Number(d.snf) > 0)} onClick={() => void run(async () => {
          await api('/v1/dairy/dispatches', 'POST', { id: crypto.randomUUID(), date: iso(new Date()), shift: d.shift, agency: d.agency, kind: d.kind, litres: Number(d.litres), fat: Number(d.fat), snf: Number(d.snf) })
          setD({ ...d, litres: '', fat: '', snf: '' })
        }, t('saved'))}>{t('dairy.saveDispatch')}</Button>
        {message && <p className="text-sm font-semibold text-brand-deep">{message}</p>}
      </section>
      {list.map((x) => (
        <section key={x.id} className="flex flex-col gap-1 rounded-card bg-white p-3 text-sm">
          <p className="flex justify-between font-semibold"><span>{x.date} · {t(`dairy.shift_${x.shift}`)} · {x.agency}</span><span>{x.amount != null ? formatRupees(x.amount) : ''}</span></p>
          <p>{t('dairy.sentLine', { litres: x.litres, fat: x.fat, snf: x.snf })}{x.acceptedLitres != null ? ` → ${t('dairy.acceptedLine', { litres: x.acceptedLitres, fat: x.acceptedFat, snf: x.acceptedSnf })}` : ''}</p>
          {isOwner && x.acceptedLitres == null && (acc?.id === x.id ? (
            <div className="grid grid-cols-2 gap-2">
              <Field label={t('dairy.acceptedLitres')} value={acc.litres} onChange={(e) => setAcc({ ...acc, litres: e.target.value })} inputMode="decimal" />
              <Field label={t('dairy.fat')} value={acc.fat} onChange={(e) => setAcc({ ...acc, fat: e.target.value })} inputMode="decimal" />
              <Field label={t('dairy.snf')} value={acc.snf} onChange={(e) => setAcc({ ...acc, snf: e.target.value })} inputMode="decimal" />
              <Field label={t('dairy.statementAmount')} value={acc.amount} onChange={(e) => setAcc({ ...acc, amount: e.target.value })} inputMode="decimal" placeholder={t('dairy.statementAmountHint')} />
              <Button className="col-span-2" onClick={() => void run(async () => { await api(`/v1/dairy/dispatches/${x.id}/accepted`, 'PUT', { acceptedLitres: Number(acc.litres), acceptedFat: Number(acc.fat), acceptedSnf: Number(acc.snf), amount: acc.amount ? Number(acc.amount) : null }); setAcc(null) }, t('saved'))}>{t('save')}</Button>
            </div>
          ) : <button onClick={() => setAcc({ id: x.id, litres: String(x.litres), fat: String(x.fat), snf: String(x.snf), amount: '' })} className="min-h-10 self-start text-xs font-semibold text-brand underline">{t('dairy.enterAccepted')}</button>)}
          {isOwner && x.amount != null && !x.paidOn && <button onClick={() => void run(() => api(`/v1/dairy/dispatches/${x.id}/paid`, 'POST', {}), t('saved'))} className="min-h-10 self-start text-xs font-semibold text-brand underline">{t('dairy.markPaid')}</button>}
          {x.paidOn && <p className="text-xs text-brand-strong">{t('dairy.paidOn', { date: x.paidOn })}</p>}
        </section>
      ))}
    </>
  )
}

/** Rate plans: from a date, for farmers or for what the dairy pays. A change is a new plan; old milk keeps its rate. */
function Rates({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const plans = useQuery({ queryKey: [shopId, 'dairy', 'plans'], queryFn: () => api<Plan[]>('/v1/dairy/rate-plans') }).data ?? []
  const empty = { fatRate: '', snfRate: '', flatRate: '', minFat: '', minSnf: '' }
  const [d, setD] = useState(() => ({ for: 'farmers' as Plan['for'], from: iso(new Date()), method: 'fat_snf' as Plan['method'], rates: { buffalo: { ...empty, minFat: '5', minSnf: '9' }, cow: { ...empty, minFat: '3.2', minSnf: '8.3' }, mixed: { ...empty, minFat: '4.5', minSnf: '8.5' } } }))
  const { message, run } = useRun(shopId)
  const setRate = (k: Kind, key: keyof typeof empty, v: string) => setD({ ...d, rates: { ...d.rates, [k]: { ...d.rates[k], [key]: v } } })
  const [gridKind, setGridKind] = useState<Kind>('buffalo')
  const [gridText, setGridText] = useState('')
  const cells = d.method === 'grid' ? parseGrid(gridText) : []
  const filled = d.method === 'grid'
    ? (cells.length > 0 ? [[gridKind, d.rates[gridKind]] as [Kind, typeof empty]] : [])
    : (Object.entries(d.rates) as [Kind, typeof empty][]).filter(([, r]) => (d.method === 'flat' ? Number(r.flatRate) : Number(r.fatRate)) > 0)
  return (
    <>
      <section className="flex flex-col gap-2 rounded-card bg-white p-3">
        <h3 className="font-bold">{t('dairy.newPlan')}</h3>
        <p className="text-xs text-muted">{t('dairy.planHint')}</p>
        <div className="grid grid-cols-3 gap-2">
          <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('dairy.planFor')}</span>
            <select value={d.for} onChange={(e) => setD({ ...d, for: e.target.value as Plan['for'] })} className={select}><option value="farmers">{t('dairy.for_farmers')}</option><option value="dairy">{t('dairy.for_dairy')}</option></select>
          </label>
          <Field label={t('dairy.from')} type="date" value={d.from} onChange={(e) => setD({ ...d, from: e.target.value })} />
          <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('dairy.method')}</span>
            <select value={d.method} onChange={(e) => setD({ ...d, method: e.target.value as Plan['method'] })} className={select}>{(['fat_snf', 'fat_only', 'flat', 'grid'] as const).map((m) => <option key={m} value={m}>{t(`dairy.method_${m}`)}</option>)}</select>
          </label>
        </div>
        {d.method === 'grid' && (
          <div className="flex flex-col gap-2 rounded-xl bg-ground p-2">
            <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('dairy.kind')}</span>
              <select value={gridKind} onChange={(e) => setGridKind(e.target.value as Kind)} className={select}>{(['buffalo', 'cow', 'mixed'] as const).map((k) => <option key={k} value={k}>{t(`dairy.kind_${k}`)}</option>)}</select>
            </label>
            <label className="block"><span className="mb-1 block text-sm font-semibold text-muted">{t('dairy.gridPaste')}</span>
              <textarea value={gridText} onChange={(e) => setGridText(e.target.value)} rows={6} placeholder={'FAT\t8.9\t9.0\n6.0\t48.10\t48.60\n6.1\t48.70\t49.20'} className="w-full rounded-xl border border-line p-2 font-mono text-sm" />
            </label>
            <p className="text-xs text-muted">{cells.length > 0 ? t('dairy.gridRead', { count: cells.length }) : t('dairy.gridHint')}</p>
          </div>
        )}
        {d.method !== 'grid' && (['buffalo', 'cow', 'mixed'] as const).map((k) => (
          <div key={k} className="grid grid-cols-4 items-end gap-2 rounded-xl bg-ground p-2">
            <p className="col-span-4 text-sm font-bold">{t(`dairy.kind_${k}`)}</p>
            {d.method === 'flat' ? <Field label={t('dairy.flatRate')} value={d.rates[k].flatRate} onChange={(e) => setRate(k, 'flatRate', e.target.value)} inputMode="decimal" /> : <Field label={t('dairy.fatRate')} value={d.rates[k].fatRate} onChange={(e) => setRate(k, 'fatRate', e.target.value)} inputMode="decimal" />}
            {d.method === 'fat_snf' ? <Field label={t('dairy.snfRate')} value={d.rates[k].snfRate} onChange={(e) => setRate(k, 'snfRate', e.target.value)} inputMode="decimal" /> : <span />}
            <Field label={t('dairy.minFat')} value={d.rates[k].minFat} onChange={(e) => setRate(k, 'minFat', e.target.value)} inputMode="decimal" />
            <Field label={t('dairy.minSnf')} value={d.rates[k].minSnf} onChange={(e) => setRate(k, 'minSnf', e.target.value)} inputMode="decimal" />
          </div>
        ))}
        <Button disabled={filled.length === 0} onClick={() => void run(() => api('/v1/dairy/rate-plans', 'POST', {
          id: crypto.randomUUID(), for: d.for, from: d.from, method: d.method,
          rates: filled.map(([kind, r]) => ({ kind, fatRate: Number(r.fatRate) || 0, snfRate: Number(r.snfRate) || 0, flatRate: Number(r.flatRate) || 0, minFat: Number(r.minFat) || 0, minSnf: Number(r.minSnf) || 0 })),
          grid: d.method === 'grid' ? cells.map((c) => ({ kind: gridKind, ...c })) : null,
        }), t('saved'))}>{t('dairy.savePlan')}</Button>
        {message && <p className="text-sm font-semibold text-brand-deep">{message}</p>}
      </section>
      {plans.map((p) => (
        <section key={p.id} className="rounded-card bg-white p-3 text-sm">
          <p className="font-bold">{t(`dairy.for_${p.for}`)} · {t('dairy.fromDate', { date: p.from })} · {t(`dairy.method_${p.method}`)}</p>
          {p.method === 'grid' && <p>{t('dairy.gridSummary', { count: p.gridCells ?? 0, fat: `${p.gridMinFat}–${p.gridMaxFat}`, snf: `${p.gridMinSnf}–${p.gridMaxSnf}` })}</p>}
          {p.method !== 'grid' && p.rates.map((r) => <p key={r.kind}>{t(`dairy.kind_${r.kind}`)}: {p.method === 'flat' ? `₹${r.flatRate}/L` : `₹${r.fatRate}/kg fat${p.method === 'fat_snf' ? ` + ₹${r.snfRate}/kg SNF` : ''}`} · min {r.minFat}/{r.minSnf}</p>)}
        </section>
      ))}
    </>
  )
}

interface Draft { type: 'payout' | 'remind'; farmerId: string; farmerCode: number; farmerName: string; amount: number; why: string }

/**
 * Ask the helper about the centre ("aaj kitna doodh aaya?", "kisko paisa dena hai?"). It answers from the centre's own
 * records only, and any action it suggests is a draft: nothing happens until the owner taps it.
 */
function Helper({ shopId, shopName }: { shopId: string; shopName: string }) {
  const { t } = useTranslation()
  const ai = useAiPhotos()
  const farmers = useQuery({ queryKey: [shopId, 'dairy', 'farmers'], queryFn: () => api<Farmer[]>('/v1/dairy/farmers') }).data ?? []
  const [question, setQuestion] = useState('')
  const [answer, setAnswer] = useState<{ answer: string; drafts: Draft[] } | null>(null)
  const [busy, setBusy] = useState(false)
  const { message, run } = useRun(shopId)
  const ask = async (q: string) => {
    setBusy(true)
    setAnswer(null)
    await run(async () => setAnswer(await api<{ answer: string; drafts: Draft[] }>('/v1/dairy/ask', 'POST', { question: q })), '')
    setBusy(false)
  }
  const quick = ['dairy.q_today', 'dairy.q_pay', 'dairy.q_flags', 'dairy.q_dairy'] as const
  if (!ai.canRead) return <p className="rounded-card bg-white p-3 text-sm text-muted">{t('dairy.helperOff')}</p>
  if (!ai.consented) return (
    <section className="flex flex-col gap-2 rounded-card bg-warn-soft p-3 text-sm">
      <p>{t('dairy.helperConsent')}</p>
      <Button onClick={() => void ai.agree()}>{t('expense.consentYes')}</Button>
    </section>
  )
  return (
    <>
      <section className="flex flex-col gap-2 rounded-card bg-white p-3">
        <p className="text-xs text-muted">{t('dairy.helperHint')}</p>
        <div className="flex flex-wrap gap-2">{quick.map((k) => <button key={k} disabled={busy} onClick={() => { setQuestion(t(k)); void ask(t(k)) }} className="min-h-10 rounded-full border border-line px-3 text-sm">{t(k)}</button>)}</div>
        <textarea value={question} onChange={(e) => setQuestion(e.target.value)} rows={2} placeholder={t('dairy.askPlaceholder')} className="rounded-xl border border-line p-2" />
        <div className="grid grid-cols-[1fr_auto] gap-2">
          <Button disabled={busy || question.trim().length < 2} onClick={() => void ask(question.trim())}>{busy ? t('dairy.thinking') : t('dairy.ask')}</Button>
          {ai.canHear && <VoiceButton onText={(said) => { setQuestion(said); void ask(said) }} onError={() => undefined} />}
        </div>
        {message && <p className="text-sm text-due">{message}</p>}
      </section>
      {answer && (
        <section className="flex flex-col gap-2 rounded-card bg-white p-3">
          <p className="whitespace-pre-line">{answer.answer}</p>
          {answer.drafts.length > 0 && <p className="text-xs text-muted">{t('dairy.draftsHint')}</p>}
          {answer.drafts.map((d) => {
            const phone = farmers.find((f) => f.id === d.farmerId)?.phone ?? null
            return (
              <div key={d.type + d.farmerId} className="flex flex-col gap-1 rounded-xl bg-ground p-2 text-sm">
                <p><b>#{d.farmerCode} {d.farmerName}</b> · {d.why}</p>
                {d.type === 'payout'
                  ? <Button className="h-10 text-sm" onClick={async () => { if ((await confirmAsk(t('dairy.payConfirm', { amount: formatRupees(d.amount), name: d.farmerName })))) void run(() => api(`/v1/dairy/farmers/${d.farmerId}/entries`, 'POST', { id: crypto.randomUUID(), kind: 'payout', amount: d.amount, mode: 'upi' }), t('dairy.paid')) }}>{t('dairy.pay', { amount: formatRupees(d.amount) })}</Button>
                  : phone && <a href={whatsAppLink(phone, t('dairy.remindText', { shop: shopName, name: d.farmerName, due: formatRupees(d.amount) }))} target="_blank" rel="noreferrer" className="flex h-10 items-center justify-center rounded-xl bg-[#15803d] font-semibold text-white">{t('dairy.sendReminder')}</a>}
              </div>
            )
          })}
        </section>
      )}
    </>
  )
}

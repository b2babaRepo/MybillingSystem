import { btn } from '../../lib/buttonStyles'
import { CustIcon } from '../../components/CustIcons'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ItemCard } from '../../components/ItemCard'
import { api, request } from '../../lib/api'
import { uuidv7 } from '../../lib/ids'
import { formatDateTime } from '../../lib/format'
import { shrinkPhoto } from '../../lib/images'
import { saveItem, type ListKind } from '../../lib/planner'
import type { Unit } from '../../lib/types'
import { navigate } from '../../router'
import { VoiceButton } from '../../components/VoiceButton'
import { UnitSelect } from '../BillScreen'
import { confirmAsk } from '../../lib/ask'

type Confidence = 'high' | 'medium' | 'low'

interface DraftLine {
  said: string
  catalogueItemId: number | null
  name: string
  quantity: number | null
  unit: Unit | null
  confidence: Confidence
  reason: string | null
}

interface Draft {
  proposalId: string
  reader: string
  lines: DraftLine[]
}

/** A list that came in on WhatsApp and waits to be checked here. */
interface PendingDraft {
  proposalId: string
  receivedAt: string
  text: string | null
  hadImage: boolean
  lines: DraftLine[]
}

interface AiStatus {
  consentedAt: string | null
  canReadPhotos: boolean
  canHearVoice: boolean
  keepDays: number
}

/** Longest voice order: the speech service's quick path is for clips under 30 seconds. */

const STRIPE: Record<Confidence, string> = { high: 'border-l-teal-600', medium: 'border-l-amber-400', low: 'border-l-red-500' }

/**
 * "Write it, say it or snap it". The household types what they need in their own words ("do kilo aata, ek litre
 * sarson tel"), speaks it (the words land in the note box to check first), or adds a photo of a paper list or a
 * chat screenshot. The server reads it into lines; the household checks each one (green: sure, amber: check, red:
 * not understood), fixes or removes, then adds them to a list. Nothing is sent to a shop from here: the usual
 * send, the shop's packing and their OK still follow. Agreement is recorded on the server and can be withdrawn
 * here, which erases their AI records.
 */
export function AiOrderScreen({ userId }: { userId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const status = useQuery({ queryKey: ['ai-status'], queryFn: () => api<AiStatus>('/v1/me/ai') })
  const pending = useQuery({ queryKey: ['ai-drafts'], queryFn: () => api<PendingDraft[]>('/v1/me/drafts'), enabled: !!status.data?.consentedAt })
  const [text, setText] = useState('')
  const [photo, setPhoto] = useState<string | null>(null)
  const [draft, setDraft] = useState<Draft | null>(null)
  const [lines, setLines] = useState<DraftLine[]>([])
  const [kind, setKind] = useState<ListKind>('monthly')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function consent(agree: boolean) {
    await request('/v1/me/ai/consent', 'POST', { agree })
    await queryClient.invalidateQueries({ queryKey: ['ai-status'] })
    if (!agree) navigate('/')
  }

  if (!status.data) return <p className="p-6 text-center text-muted">{status.isError ? status.error.message : '…'}</p>
  const ai = status.data

  if (!ai.consentedAt) {
    return (
      <div className="flex flex-col gap-3 p-4">
        <section className="flex flex-col gap-3 rounded-card bg-white p-4">
          <h2 className="text-xl font-bold">✍️ {t('ai.title')}</h2>
          <p className="text-[15px] leading-relaxed">{t('ai.consentBody')}</p>
          <ul className="list-disc pl-5 text-sm text-muted">
            <li>{t('ai.consentPoint1')}</li>
            <li>{t('ai.consentVoice')}</li>
            <li>{t('ai.consentPoint2', { days: ai.keepDays })}</li>
            <li>{t('ai.consentPoint3')}</li>
          </ul>
          <button
            className={`h-12 rounded-xl ${btn.solid}`}
            onClick={() => void consent(true)}
          >
            {t('ai.consentAgree')}
          </button>
          <button className="min-h-11 text-sm text-muted underline" onClick={() => navigate('/')}>{t('ai.notNow')}</button>
        </section>
      </div>
    )
  }

  async function read() {
    setBusy(true)
    setError(null)
    try {
      const result = await api<Draft>('/v1/me/orders/draft', 'POST', { text: text.trim() || null, imageDataUrl: photo })
      setDraft(result)
      setLines(result.lines)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  /** Opens a WhatsApp draft for checking, exactly like one read here. */
  function open(waiting: PendingDraft) {
    setDraft({ proposalId: waiting.proposalId, reader: 'whatsapp', lines: waiting.lines })
    setLines(waiting.lines)
  }

  /** A WhatsApp draft is done once added to a list or set aside: it leaves the waiting list. */
  async function finishWhatsApp() {
    if (draft && pending.data?.some((p) => p.proposalId === draft.proposalId)) {
      await request(`/v1/me/drafts/${draft.proposalId}/done`, 'POST', {})
      await queryClient.invalidateQueries({ queryKey: ['ai-drafts'] })
    }
  }

  async function addToList() {
    for (const l of lines) {
      if (!l.name.trim()) continue
      await saveItem(userId, kind, {
        id: uuidv7(),
        name: l.name.trim(),
        quantity: l.quantity && l.quantity > 0 ? l.quantity : 1,
        unit: l.unit ?? 'piece',
        checked: false,
        note: t('ai.saidNote', { said: l.said }),
        catalogueItemId: l.catalogueItemId,
        aiConfidence: l.confidence,
      })
    }
    await finishWhatsApp()
    navigate(`/list/${kind}`)
  }

  const change = (index: number, patch: Partial<DraftLine>) => setLines((all) => all.map((l, i) => (i === index ? { ...l, ...patch } : l)))

  if (draft) {
    const unsure = lines.filter((l) => l.confidence !== 'high').length
    return (
      <div className="flex flex-col gap-3 p-4 pb-28">
        <section className="rounded-card bg-white p-3">
          <h2 className="text-lg font-bold">{t('ai.checkTitle', { count: lines.length })}</h2>
          <p className="text-sm text-muted">{unsure > 0 ? t('ai.checkHintUnsure', { count: unsure }) : t('ai.checkHintAllSure')}</p>
        </section>
        {lines.length === 0 && <p className="py-6 text-center text-muted">{t('ai.nothingFound')}</p>}
        <ul className="flex flex-col gap-2">
          {lines.map((l, i) => (
            <li key={i} className={`flex gap-3 rounded-[16px] border-l-[6px] bg-white p-3 ${STRIPE[l.confidence]}`}>
              <div className="w-16 shrink-0">
                <ItemCard item={{ catalogueItemId: l.catalogueItemId, name: l.name }} title="" compact />
              </div>
              <div className="flex min-w-0 flex-1 flex-col gap-1.5">
                <div className="flex items-start gap-2">
                  {l.catalogueItemId ? (
                    <span className="flex-1 font-bold">{l.name}</span>
                  ) : (
                    <input value={l.name} onChange={(e) => change(i, { name: e.target.value })} aria-label={t('ai.itemName')}
                      className="h-10 min-w-0 flex-1 rounded-lg border border-line px-2 font-semibold" />
                  )}
                  <button aria-label={t('remove')} onClick={() => setLines((all) => all.filter((_, j) => j !== i))} className="h-10 w-10 text-xl text-muted">×</button>
                </div>
                <span className="text-xs text-muted">{t('ai.said', { said: l.said })}</span>
                {l.reason && <span className={`text-xs font-semibold ${l.confidence === 'low' ? 'text-due' : 'text-warn'}`}>{l.reason}</span>}
                <div className="grid grid-cols-[1fr_1fr] items-end gap-2">
                  <label className="block">
                    <span className="mb-1 block text-xs font-semibold text-muted">{t('quantity')}</span>
                    <input value={l.quantity ?? ''} onChange={(e) => change(i, { quantity: Number(e.target.value.replace(/[^\d.]/g, '')) || null })}
                      inputMode="decimal" className="h-11 w-full rounded-xl border border-line px-2 text-lg" />
                  </label>
                  <UnitSelect value={l.unit ?? 'piece'} onChange={(unit) => change(i, { unit })} />
                </div>
              </div>
            </li>
          ))}
        </ul>
        <div className="flex justify-between">
          <button className="min-h-11 text-sm font-semibold text-brand-strong underline" onClick={() => setDraft(null)}>‹ {t('ai.again')}</button>
          {draft.reader === 'whatsapp' && (
            <button className="min-h-11 text-sm text-muted underline" onClick={() => void finishWhatsApp().then(() => setDraft(null))}>{t('ai.setAside')}</button>
          )}
        </div>
        <div className="fixed inset-x-0 bottom-[68px] z-10 mx-auto lg:bottom-0 lg:max-w-4xl flex max-w-xl flex-col gap-2 border-t border-line bg-white px-4 py-3">
          <div role="tablist" className="grid grid-cols-2 rounded-xl bg-[#edf2f1] p-1 text-sm">
            {(['monthly', 'weekly'] as const).map((k) => (
              <button key={k} role="tab" aria-selected={kind === k} onClick={() => setKind(k)}
                className={`h-9 rounded-lg font-semibold ${kind === k ? 'bg-white text-brand-strong shadow' : 'text-muted'}`}>
                {t(`plan.${k}`)}
              </button>
            ))}
          </div>
          <button disabled={lines.length === 0} onClick={() => void addToList()} className={`h-13 min-h-12 text-lg rounded-xl ${btn.solid}`}>
            {t('ai.addToList', { count: lines.length })}
          </button>
        </div>
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      {pending.data && pending.data.length > 0 && (
        <section className="flex flex-col gap-2 rounded-card bg-[#e7f8ee] p-3 ring-1 ring-[#25d366]/40">
          <h2 className="font-bold">💬 {t('ai.fromWhatsapp', { count: pending.data.length })}</h2>
          {pending.data.map((p) => (
            <button key={p.proposalId} onClick={() => open(p)} className="flex flex-col items-start rounded-xl bg-white p-3 text-left">
              <span className="text-xs text-muted">{formatDateTime(p.receivedAt)}{p.hadImage ? ' · 📷' : ''}</span>
              <span className="line-clamp-2 font-semibold">{p.text || t('ai.photoList')}</span>
              <span className="text-sm text-brand-strong">{t('ai.itemsToCheck', { count: p.lines.length })} ›</span>
            </button>
          ))}
        </section>
      )}
      <section className="flex flex-col gap-3 rounded-card bg-white p-4">
        <h2 className="text-lg font-bold">✍️ {t('ai.title')}</h2>
        <textarea value={text} onChange={(e) => setText(e.target.value)} maxLength={2000} rows={5} placeholder={t('ai.placeholder')}
          className="rounded-[14px] border border-line p-3 text-[17px] outline-none focus:border-brand-line" />
        {ai.canHearVoice ? (
          <VoiceButton onText={(heard) => setText((current) => (current.trim() ? `${current.trim()}, ${heard}` : heard))} onError={setError} />
        ) : (
          <p className="flex items-center gap-1.5 text-xs text-muted"><CustIcon name="mic" size={14} className="shrink-0" />{t('ai.voiceOff')}</p>
        )}
        {photo ? (
          <div className="flex items-center gap-3">
            <img src={photo} alt="" className="h-20 w-20 rounded-xl object-cover" />
            <button className="text-sm text-muted underline" onClick={() => setPhoto(null)}>{t('ai.removePhoto')}</button>
          </div>
        ) : ai.canReadPhotos && (
          <label className="flex min-h-12 cursor-pointer items-center justify-center gap-2 rounded-2xl border-2 border-dashed border-brand-line font-semibold text-brand-strong">
            📷 {t('ai.addPhoto')}
            <input type="file" accept="image/*" className="sr-only"
              onChange={async (e) => {
                const file = e.target.files?.[0]
                e.target.value = ''
                if (file) setPhoto(await shrinkPhoto(file, 1500, 1_200_000, false))
              }} />
          </label>
        )}
        <p className="text-xs text-muted">{ai.canReadPhotos ? t('ai.photoHint') : t('ai.photoOff')}</p>
        {error && <p className="rounded-lg bg-due-soft p-3 text-sm text-due">{error}</p>}
        <button disabled={busy || (!text.trim() && !photo)} onClick={() => void read()} className={`h-14 text-lg rounded-xl ${btn.solid}`}>
          {busy ? t('ai.reading') : t('ai.read')}
        </button>
      </section>
      <button className="min-h-11 self-center text-sm text-muted underline" onClick={async () => (await confirmAsk(t('ai.withdrawConfirm'))) && void consent(false)}>
        {t('ai.withdraw')}
      </button>
    </div>
  )
}

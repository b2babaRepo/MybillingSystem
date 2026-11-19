import { useQuery, useQueryClient } from '@tanstack/react-query'
import QRCode from 'qrcode'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { confirmAsk } from '../components/Ask'
import { shownLocale } from '../i18n'
import { api, request } from '../lib/api'
import { shrinkPhoto } from '../lib/images'
import { navigate } from '../router'
import { UPLOAD_DRAFT_KEY, type InvoiceDraft } from './SuppliersScreen'

interface Upload { id: string; source: 'computer' | 'phone'; createdAt: string; usedAt: string | null }
interface Link { slug: string; expires: number; token: string }
interface Supplier { id: string; name: string; isActive?: boolean }

const when = (iso: string) => new Date(iso).toLocaleString(shownLocale(), { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })

/** A waiting photo's picture, fetched only when it is on screen. */
function Thumb({ id }: { id: string }) {
  const image = useQuery({ queryKey: ['purchase-upload', id], queryFn: () => api<{ imageDataUrl: string }>(`/v1/suppliers/uploads/${id}`), staleTime: Infinity }).data
  return image ? <a href={image.imageDataUrl} target="_blank" rel="noreferrer"><img src={image.imageDataUrl} alt="" className="h-20 w-16 shrink-0 rounded-lg border border-line object-cover" /></a>
    : <span className="h-20 w-16 shrink-0 rounded-lg bg-[#edf2f1]" />
}

/**
 * Uploaded bills: a supplier's bill photo sent from this computer, or from a phone by scanning a QR code (no app or
 * sign-in on the phone). Each waits here; "Make purchase" reads it into the supplier's bill form, where the owner checks
 * the lines and saves: stock goes up, the supplier's khata gets the bill, as with any purchase.
 */
export function PurchaseUploadsScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [phoneOpen, setPhoneOpen] = useState(false)
  // While the QR is up, look for the phone's photos every few seconds.
  const list = useQuery({ queryKey: [shopId, 'purchase-uploads'], queryFn: () => api<Upload[]>('/v1/suppliers/uploads'), refetchInterval: phoneOpen ? 4000 : false })
  const suppliers = useQuery({ queryKey: [shopId, 'suppliers'], queryFn: () => api<Supplier[]>('/v1/suppliers') }).data ?? []
  const ai = useQuery({ queryKey: ['ai-status'], queryFn: () => api<{ consentedAt: string | null; canReadPhotos: boolean }>('/v1/me/ai') }).data
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState<string | null>(null)
  const [read, setRead] = useState<{ uploadId: string; draft: InvoiceDraft; supplierId: string } | null>(null)
  const [link, setLink] = useState<{ url: string; qr: string; until: number } | null>(null)
  const refresh = () => queryClient.invalidateQueries({ queryKey: [shopId, 'purchase-uploads'] })
  const waiting = (list.data ?? []).filter((u) => !u.usedAt)
  const done = (list.data ?? []).filter((u) => u.usedAt)

  async function fromComputer(files: FileList) {
    setMessage('')
    setBusy('upload')
    let sent = 0
    try {
      for (const file of [...files]) {
        await api('/v1/suppliers/uploads', 'POST', { imageDataUrl: await shrinkPhoto(file, 1600, 1_200_000, false) })
        sent++
      }
      setMessage(t('upload.sent', { count: sent }))
    } catch (e) {
      setMessage(`${t('upload.sent', { count: sent })} ${e instanceof Error ? e.message : t('error')}`)
    } finally {
      setBusy(null)
      await refresh()
    }
  }

  async function openPhone() {
    setMessage('')
    try {
      const l = await api<Link>('/v1/suppliers/uploads/link', 'POST', {})
      const url = `${location.origin}${location.pathname}#/up/${l.slug}?e=${l.expires}&t=${l.token}`
      setLink({ url, qr: await QRCode.toDataURL(url, { width: 360, margin: 2, color: { dark: '#10201d', light: '#ffffff' } }), until: l.expires * 1000 })
      setPhoneOpen(true)
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  // The link stops working after 30 minutes: stop looking then.
  useEffect(() => {
    if (!link) return
    const h = setTimeout(() => setPhoneOpen(false), Math.max(0, link.until - Date.now()))
    return () => clearTimeout(h)
  }, [link])

  async function makePurchase(u: Upload) {
    setMessage('')
    setBusy(u.id)
    try {
      const { imageDataUrl } = await api<{ imageDataUrl: string }>(`/v1/suppliers/uploads/${u.id}`)
      const draft = await api<InvoiceDraft>('/v1/suppliers/purchases/photo', 'POST', { imageDataUrl })
      if (draft.lines.length === 0) throw new Error(t('invoicePhoto.nothing'))
      setRead({ uploadId: u.id, draft, supplierId: draft.supplierId ?? '' })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    } finally {
      setBusy(null)
    }
  }

  async function newSupplier(name: string) {
    const id = crypto.randomUUID()
    await api('/v1/suppliers', 'POST', { id, name: name.trim(), phone: null })
    await queryClient.invalidateQueries({ queryKey: [shopId, 'suppliers'] })
    return id
  }

  async function goToBill() {
    if (!read) return
    try {
      const supplierId = read.supplierId === 'new' ? await newSupplier(read.draft.supplierName ?? t('upload.unknownSupplier')) : read.supplierId
      sessionStorage.setItem(UPLOAD_DRAFT_KEY, JSON.stringify({ uploadId: read.uploadId, supplierId, draft: read.draft }))
      sessionStorage.setItem('gk-open-supplier', supplierId)
      navigate('/suppliers')
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  async function remove(u: Upload) {
    if (!(await confirmAsk(t('upload.deleteConfirm')))) return
    await api(`/v1/suppliers/uploads/${u.id}`, 'DELETE').catch((e) => setMessage(e instanceof Error ? e.message : t('error')))
    await refresh()
  }

  const agree = async () => {
    await request('/v1/me/ai/consent', 'POST', { agree: true })
    await queryClient.invalidateQueries({ queryKey: ['ai-status'] })
  }

  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex items-center justify-between gap-2">
        <h1 className="text-lg font-extrabold">{t('upload.title')}</h1>
        <button onClick={() => navigate('/suppliers')} className="min-h-10 text-sm font-semibold text-brand underline">{t('side.suppliers')} ›</button>
      </div>
      <p className="text-sm text-muted">{t('upload.hint')}</p>

      <div className="grid gap-3 md:grid-cols-2">
        <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
          <h2 className="font-bold">💻 {t('upload.computer')}</h2>
          <p className="text-sm text-muted">{t('upload.computerHint')}</p>
          <label className={`mt-auto flex min-h-12 cursor-pointer items-center justify-center rounded-2xl border-2 border-brand px-4 font-semibold text-brand ${busy === 'upload' ? 'opacity-50' : ''}`}>
            {busy === 'upload' ? t('upload.sending') : t('upload.choose')}
            <input type="file" accept="image/*" multiple className="hidden" disabled={busy === 'upload'}
              onChange={(e) => { const files = e.target.files; if (files?.length) void fromComputer(files); e.target.value = '' }} />
          </label>
        </section>
        <section className="flex flex-col gap-2 rounded-card bg-brand-soft p-4 shadow-sm">
          <h2 className="font-bold">📱 {t('upload.phone')}</h2>
          {!phoneOpen || !link ? <>
            <p className="text-sm text-brand-deep">{t('upload.phoneHint')}</p>
            <button onClick={() => void openPhone()} className="mt-auto min-h-12 rounded-2xl bg-brand px-4 font-semibold text-white">{t('upload.showQr')}</button>
          </> : (
            <div className="flex flex-wrap items-center gap-3">
              <img src={link.qr} alt={t('upload.qrAlt')} className="h-44 w-44 rounded-xl bg-white p-1" />
              <ol className="min-w-0 flex-1 list-decimal pl-5 text-sm text-brand-deep">
                <li>{t('upload.step1')}</li>
                <li>{t('upload.step2')}</li>
                <li>{t('upload.step3')}</li>
                <li className="mt-1 list-none text-xs">{t('upload.linkLife')}</li>
                <li className="list-none"><button onClick={() => void navigator.clipboard?.writeText(link.url)} className="min-h-10 font-semibold text-brand underline">{t('upload.copyLink')}</button></li>
              </ol>
            </div>
          )}
        </section>
      </div>

      {ai && ai.canReadPhotos && !ai.consentedAt && (
        <div className="rounded-card bg-warn-soft p-3 text-sm">
          <p className="font-bold">{t('expense.consentTitle')}</p><p>{t('invoicePhoto.consent')}</p>
          <button onClick={() => void agree()} className="mt-2 min-h-11 rounded-xl bg-brand px-4 font-semibold text-white">{t('upload.agree')}</button>
        </div>
      )}
      {ai && !ai.canReadPhotos && <p className="rounded-card bg-warn-soft p-3 text-sm">{t('upload.noAi')}</p>}
      {message && <p role="status" className="text-sm font-semibold text-brand-deep">{message}</p>}

      {read && (
        <section className="flex flex-col gap-2 rounded-card border-2 border-brand bg-white p-4 shadow-sm">
          <h2 className="font-bold">{t('upload.readTitle', { count: read.draft.lines.length })}</h2>
          <p className="text-sm text-muted">{[read.draft.supplierName, read.draft.billNo && t('upload.billNo', { no: read.draft.billNo }), read.draft.grandTotal != null && `₹${read.draft.grandTotal}`].filter(Boolean).join(' · ')}</p>
          <label className="text-sm"><span className="mb-1 block font-semibold text-muted">{t('upload.whichSupplier')}</span>
            <select value={read.supplierId} onChange={(e) => setRead({ ...read, supplierId: e.target.value })} className="h-11 w-full rounded-xl border border-line bg-white px-2">
              <option value="">{t('upload.pickSupplier')}</option>
              {suppliers.filter((s) => s.isActive !== false).map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
              {read.draft.supplierName && <option value="new">{t('upload.addSupplier', { name: read.draft.supplierName })}</option>}
            </select></label>
          <div className="flex flex-wrap gap-2">
            <button disabled={!read.supplierId} onClick={() => void goToBill()} className="min-h-12 flex-1 rounded-2xl bg-brand px-4 font-semibold text-white disabled:opacity-40">{t('upload.checkAndSave')}</button>
            <button onClick={() => setRead(null)} className="min-h-12 rounded-2xl border border-line px-4 font-semibold">{t('cancel')}</button>
          </div>
          <p className="text-xs text-muted">{t('upload.whatHappens')}</p>
        </section>
      )}

      <h2 className="mt-2 font-bold">{t('upload.waiting', { count: waiting.length })}</h2>
      {waiting.length === 0 && <p className="rounded-card bg-white p-4 text-sm text-muted">{t('upload.none')}</p>}
      <div className="flex flex-col divide-y divide-line rounded-card bg-white shadow-sm">
        {waiting.map((u) => (
          <div key={u.id} className="flex items-center gap-3 p-3">
            <Thumb id={u.id} />
            <span className="flex min-w-0 flex-1 flex-col text-sm">
              <b>{u.source === 'phone' ? `📱 ${t('upload.fromPhone')}` : `💻 ${t('upload.fromComputer')}`}</b>
              <span className="text-muted">{when(u.createdAt)}</span>
            </span>
            <button disabled={!!busy || !ai?.consentedAt} onClick={() => void makePurchase(u)} className="min-h-11 rounded-xl bg-brand px-3 text-sm font-semibold text-white disabled:opacity-40">
              {busy === u.id ? t('invoicePhoto.reading') : t('upload.make')}</button>
            <button onClick={() => void remove(u)} aria-label={t('upload.delete')} className="flex h-11 w-11 items-center justify-center rounded-xl border border-line text-due">🗑</button>
          </div>
        ))}
      </div>
      {done.length > 0 && (
        <details className="rounded-card bg-white p-3 text-sm shadow-sm">
          <summary className="cursor-pointer font-semibold">{t('upload.done', { count: done.length })}</summary>
          <ul className="mt-2 flex flex-col gap-1 text-muted">
            {done.map((u) => <li key={u.id}>{u.source === 'phone' ? '📱' : '💻'} {when(u.createdAt)} → {t('upload.madeOn', { when: when(u.usedAt!) })}</li>)}
          </ul>
        </details>
      )}
    </div>
  )
}

/** #/up/{shop}?e=…&t=…: the page a phone opens from the QR code. Take or pick bill photos and send them; no sign-in. */
export function PhoneUploadPage({ path }: { path: string }) {
  const { t } = useTranslation()
  const match = path.match(/^\/up\/([^/?]+)\?e=(\d+)&t=([0-9a-f]+)/)
  const [sent, setSent] = useState(0)
  const [state, setState] = useState<'idle' | 'busy' | 'failed'>('idle')
  const expired = !match || Number(match[2]) * 1000 < Date.now()

  async function send(files: FileList) {
    setState('busy')
    try {
      for (const file of [...files]) {
        await api(`/v1/upload/${match![1]}?e=${match![2]}&t=${match![3]}`, 'POST', { imageDataUrl: await shrinkPhoto(file, 1600, 1_200_000, false) })
        setSent((n) => n + 1)
      }
      setState('idle')
    } catch {
      setState('failed')
    }
  }

  return (
    <div className="mx-auto flex min-h-dvh max-w-md flex-col gap-4 bg-white p-6 text-ink">
      <h1 className="text-2xl font-extrabold">{t('upload.phoneTitle')}</h1>
      {expired ? <p className="rounded-xl bg-due-soft p-3 text-due">{t('upload.expired')}</p> : <>
        <p className="text-muted">{t('upload.phonePageHint')}</p>
        <label className={`flex min-h-16 cursor-pointer items-center justify-center rounded-2xl bg-brand px-4 text-lg font-bold text-white ${state === 'busy' ? 'opacity-50' : ''}`}>
          📷 {state === 'busy' ? t('upload.sending') : t('upload.takePhoto')}
          <input type="file" accept="image/*" capture="environment" className="hidden" disabled={state === 'busy'} onChange={(e) => { if (e.target.files?.length) void send(e.target.files); e.target.value = '' }} />
        </label>
        <label className={`flex min-h-14 cursor-pointer items-center justify-center rounded-2xl border-2 border-brand px-4 font-semibold text-brand ${state === 'busy' ? 'opacity-50' : ''}`}>
          🖼 {t('upload.pickPhotos')}
          <input type="file" accept="image/*" multiple className="hidden" disabled={state === 'busy'} onChange={(e) => { if (e.target.files?.length) void send(e.target.files); e.target.value = '' }} />
        </label>
        {sent > 0 && <p role="status" className="rounded-xl bg-brand-soft p-3 font-semibold text-brand-deep">✓ {t('upload.phoneSent', { count: sent })}</p>}
        {state === 'failed' && <p role="status" className="rounded-xl bg-due-soft p-3 text-due">{t('upload.phoneFailed')}</p>}
      </>}
    </div>
  )
}

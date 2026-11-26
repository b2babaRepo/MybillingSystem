import { btn } from '../lib/buttonStyles'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { confirmAsk } from '../lib/ask'
import { api } from '../lib/api'

interface Config { enabled: boolean; appId: string | null; configId: string | null; graphVersion: string }
interface Connection { connected: boolean; displayNumber: string | null; verifiedName: string | null; quality: string | null; connectedAt: string | null; testSentAt: string | null }

declare global {
  interface Window {
    FB?: { init: (o: object) => void; login: (cb: (r: { authResponse?: { code?: string } }) => void, o: object) => void }
    fbAsyncInit?: () => void
  }
}

/** Meta's JavaScript SDK, loaded only when the owner presses Connect. */
function loadSdk(appId: string, version: string): Promise<NonNullable<Window['FB']>> {
  return new Promise((resolve, reject) => {
    if (window.FB) { window.FB.init({ appId, autoLogAppEvents: true, xfbml: false, version }); resolve(window.FB); return }
    window.fbAsyncInit = () => { window.FB!.init({ appId, autoLogAppEvents: true, xfbml: false, version }); resolve(window.FB!) }
    const script = document.createElement('script')
    script.src = 'https://connect.facebook.net/en_US/sdk.js'
    script.async = true
    script.crossOrigin = 'anonymous'
    script.onerror = () => reject(new Error('sdk'))
    document.body.appendChild(script)
  })
}

/**
 * The shop's own WhatsApp Business number, connected with Meta's Embedded Signup (the official way). Messages then go
 * from the shop's number through Meta's Cloud API: no linked WhatsApp Web device, no risk to the number.
 */
export function WhatsAppBusinessPanel() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const config = useQuery({ queryKey: ['wa-business-config'], queryFn: () => api<Config>('/v1/whatsapp/business/config'), staleTime: Infinity }).data
  const conn = useQuery({ queryKey: ['wa-business'], queryFn: () => api<Connection>('/v1/whatsapp/business') }).data
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)
  const refresh = () => queryClient.invalidateQueries({ queryKey: ['wa-business'] })

  async function connect() {
    if (!config?.enabled || !config.appId || !config.configId) return
    setMessage(null)
    setBusy(true)
    // The popup tells the page which account and number were chosen; the login callback brings the one-time code.
    let chosen: { waba_id?: string; phone_number_id?: string } | null = null
    const listen = (e: MessageEvent) => {
      if (!/(^|\.)facebook\.com$/.test(new URL(e.origin).hostname)) return
      try {
        const data = typeof e.data === 'string' ? JSON.parse(e.data) : e.data
        if (data?.type === 'WA_EMBEDDED_SIGNUP' && data.event?.startsWith('FINISH')) chosen = data.data
        if (data?.type === 'WA_EMBEDDED_SIGNUP' && data.event === 'CANCEL') setMessage({ ok: false, text: t('waBiz.cancelled') })
      } catch { /* other messages from Facebook */ }
    }
    window.addEventListener('message', listen)
    try {
      const FB = await loadSdk(config.appId, config.graphVersion)
      const code = await new Promise<string | null>((resolve) => FB.login((r) => resolve(r.authResponse?.code ?? null), {
        config_id: config.configId, response_type: 'code', override_default_response_type: true, extras: { setup: {}, sessionInfoVersion: '3' },
      }))
      // The number's details may arrive a moment after the login callback.
      for (let i = 0; i < 20 && !chosen && code; i++) await new Promise((r) => setTimeout(r, 150))
      const picked = chosen as { waba_id?: string; phone_number_id?: string } | null
      if (!code || !picked?.waba_id || !picked.phone_number_id) {
        setMessage((m) => m ?? { ok: false, text: t('waBiz.notFinished') })
        return
      }
      await api('/v1/whatsapp/business/connect', 'POST', { code, wabaId: picked.waba_id, phoneNumberId: picked.phone_number_id })
      setMessage({ ok: true, text: t('waBiz.connected') })
      await refresh()
    } catch (e) {
      setMessage({ ok: false, text: e instanceof Error && e.message !== 'sdk' ? e.message : t('waBiz.sdkFailed') })
    } finally {
      window.removeEventListener('message', listen)
      setBusy(false)
    }
  }

  async function test() {
    setMessage(null)
    setBusy(true)
    try {
      await api('/v1/whatsapp/business/test', 'POST', {})
      setMessage({ ok: true, text: t('waBiz.testSent') })
      await refresh()
    } catch (e) {
      setMessage({ ok: false, text: e instanceof Error ? e.message : t('error') })
    } finally {
      setBusy(false)
    }
  }

  async function disconnect() {
    if (!(await confirmAsk(t('waBiz.disconnectConfirm')))) return
    await api('/v1/whatsapp/business', 'DELETE')
    setMessage(null)
    await refresh()
  }

  if (!config) return null
  const tone = conn?.quality === 'GREEN' ? 'bg-green-50 text-green-800' : conn?.quality === 'YELLOW' ? 'bg-warn-soft text-warn' : conn?.quality === 'RED' ? 'bg-due-soft text-due' : 'bg-[#edf2f1] text-muted'
  return (
    <section className="flex flex-col gap-3 rounded-card bg-white p-4 shadow-sm">
      <div className="flex items-center gap-2">
        <span aria-hidden className="flex h-10 w-10 items-center justify-center rounded-xl bg-[#25D366] text-xl text-white">✆</span>
        <div><h2 className="text-lg font-bold">{t('waBiz.title')}</h2><p className="text-sm text-muted">{t('waBiz.hint')}</p></div>
      </div>
      {conn?.connected ? (
        <div className="flex flex-col gap-2 rounded-xl bg-[#f6faf9] p-3">
          <p className="flex flex-wrap items-center gap-2">
            <b className="text-lg">{conn.displayNumber}</b>
            {conn.verifiedName && <span className="text-muted">· {conn.verifiedName}</span>}
            {conn.quality && <span className={`rounded-full px-2 py-0.5 text-xs font-bold ${tone}`}>{t('waBiz.quality', { q: t(`waBiz.q.${conn.quality}`, { defaultValue: conn.quality }) })}</span>}
          </p>
          <p className="text-xs text-muted">{t('waBiz.since', { date: conn.connectedAt ? new Date(conn.connectedAt).toLocaleDateString() : '' })}{conn.testSentAt ? ` · ${t('waBiz.lastTest', { date: new Date(conn.testSentAt).toLocaleString() })}` : ''}</p>
          <div className="flex flex-wrap gap-2">
            <button disabled={busy} onClick={() => void test()} className="min-h-11 rounded-xl bg-[#15803d] px-4 font-semibold text-white disabled:opacity-50">{t('waBiz.test')}</button>
            <button disabled={busy} onClick={() => void connect()} className="min-h-11 rounded-xl border border-line px-4 font-semibold disabled:opacity-50">{t('waBiz.change')}</button>
            <button onClick={() => void disconnect()} className={`min-h-11 text-sm ${btn.dangerOutline}`}>{t('waBiz.disconnect')}</button>
          </div>
          <Templates />
        </div>
      ) : config.enabled ? (
        <>
          <ul className="list-inside list-disc text-sm text-muted">
            <li>{t('waBiz.step1')}</li><li>{t('waBiz.step2')}</li><li>{t('waBiz.step3')}</li>
          </ul>
          <button disabled={busy} onClick={() => void connect()} className="min-h-12 self-start rounded-xl bg-[#25D366] px-5 font-bold text-white shadow-sm disabled:opacity-50">
            {busy ? t('waBiz.connecting') : t('waBiz.connect')}</button>
          <p className="text-xs text-muted">{t('waBiz.cost')}</p>
        </>
      ) : <p className="rounded-xl bg-[#f6faf9] p-3 text-sm text-muted">{t('waBiz.notSetUp')}</p>}
      {message && <p role="status" className={`rounded-xl p-2 text-sm font-semibold ${message.ok ? 'bg-brand-soft text-brand-deep' : 'bg-due-soft text-due'}`}>{message.text}</p>}
    </section>
  )
}

interface TemplateStatus { key: string; name: string; status: string; reason: string | null }

/**
 * The shop's three message templates (bill, payment reminder, khata statement) and Meta's verdict on each. A message
 * the shop starts can only use an approved template, so this is what turns on "Send from shop WhatsApp".
 */
function Templates() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const list = useQuery({ queryKey: ['wa-templates'], queryFn: () => api<{ canCreate: boolean; templates: TemplateStatus[] }>('/v1/whatsapp/business/templates'), retry: false })
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const data = list.data
  const missing = data?.templates.some((x) => x.status === 'MISSING' || x.status === 'REJECTED')
  async function ask() {
    setBusy(true)
    setError('')
    try {
      await api('/v1/whatsapp/business/templates', 'POST', {})
      await queryClient.invalidateQueries({ queryKey: ['wa-templates'] })
    } catch (e) {
      setError(e instanceof Error ? e.message : t('error'))
    } finally {
      setBusy(false)
    }
  }
  const tone = (s: string) => (s === 'APPROVED' ? 'bg-green-50 text-green-800' : s === 'PENDING' ? 'bg-warn-soft text-warn' : s === 'MISSING' ? 'bg-[#edf2f1] text-muted' : 'bg-due-soft text-due')
  return (
    <div className="mt-2 flex flex-col gap-2 border-t border-line pt-2">
      <p className="text-sm font-bold">{t('waBiz.templates')}</p>
      {list.isError && <p className="text-xs text-due">{list.error.message}</p>}
      {data?.templates.map((x) => (
        <p key={x.name} className="flex flex-wrap items-center justify-between gap-2 text-sm">
          <span>{t(`waBiz.tpl.${x.key}`)}</span>
          <span className={`rounded-full px-2 py-0.5 text-xs font-bold ${tone(x.status)}`} title={x.reason ?? undefined}>{t(`waBiz.tplStatus.${x.status}`, { defaultValue: x.status })}</span>
        </p>
      ))}
      {data && missing && (data.canCreate
        ? <button disabled={busy} onClick={() => void ask()} className={`min-h-11 self-start px-4 text-sm rounded-xl ${btn.solid}`}>{busy ? t('waBiz.asking') : t('waBiz.ask')}</button>
        : <p className="text-xs text-muted">{t('waBiz.noPublicUrl')}</p>)}
      {data && !missing && data.templates.some((x) => x.status === 'PENDING') && <p className="text-xs text-muted">{t('waBiz.pendingHint')}</p>}
      {error && <p className="text-xs text-due">{error}</p>}
    </div>
  )
}

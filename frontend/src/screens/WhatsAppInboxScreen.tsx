import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { shownLocale } from '../i18n'
import { api } from '../lib/api'
import { navigate } from '../router'
import { useShopWhatsApp } from '../components/ShopWhatsApp'

interface Conversation { phone: string; customerId: string | null; name: string | null; lastBody: string; lastDirection: 'In' | 'Out'; lastAt: string; waiting: number; canReplyFreely: boolean }
interface Message { id: string; direction: 'In' | 'Out'; kind: string; body: string; status: string; error: string | null; at: string }
interface Thread { phone: string; customerId: string | null; name: string | null; canReplyFreely: boolean; windowEndsAt: string | null; messages: Message[] }

const when = (iso: string) => new Date(iso).toLocaleString(shownLocale(), { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })
const shown = (wa: string) => (wa.startsWith('91') && wa.length === 12 ? `+91 ${wa.slice(2, 7)} ${wa.slice(7)}` : `+${wa}`)
const TICK: Record<string, string> = { sent: '✓', delivered: '✓✓', read: '✓✓', failed: '⚠' }

/**
 * The shop's WhatsApp inbox: what customers wrote to the shop's own number, one conversation each (named from the
 * khata by phone), the bills and reminders sent with how far they got, and a reply box while WhatsApp allows one.
 */
export function WhatsAppInboxScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const connected = useShopWhatsApp()
  const list = useQuery({ queryKey: ['wa-inbox', shopId], queryFn: () => api<Conversation[]>('/v1/whatsapp/inbox'), refetchInterval: 20_000, enabled: connected })
  const [open, setOpen] = useState<string | null>(null)
  const thread = useQuery({ queryKey: ['wa-inbox', shopId, open], queryFn: () => api<Thread>(`/v1/whatsapp/inbox/${open}`), enabled: !!open, refetchInterval: 15_000 })
  const [text, setText] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const end = useRef<HTMLDivElement>(null)
  useEffect(() => { end.current?.scrollIntoView({ block: 'end' }) }, [thread.data?.messages.length])

  async function act(path: string, body: object) {
    setBusy(true)
    setError('')
    try {
      await api(path, 'POST', body)
      setText('')
      await queryClient.invalidateQueries({ queryKey: ['wa-inbox', shopId] })
    } catch (e) {
      setError(e instanceof Error ? e.message : t('error'))
    } finally {
      setBusy(false)
    }
  }

  if (!connected) {
    return (
      <div className="mx-auto flex max-w-2xl flex-col gap-3 p-4">
        <h1 className="text-lg font-extrabold">{t('waInbox.title')}</h1>
        <p className="rounded-card bg-white p-4 text-sm text-muted">{t('waInbox.notConnected')}</p>
        <button onClick={() => navigate('/settings')} className="min-h-11 self-start rounded-xl bg-brand px-4 font-semibold text-white">{t('waInbox.toSettings')}</button>
      </div>
    )
  }

  const t0 = thread.data
  return (
    <div className="flex flex-col gap-3 p-4 lg:mx-auto lg:w-full lg:max-w-6xl">
      <h1 className="text-lg font-extrabold">{t('waInbox.title')}</h1>
      <div className="grid gap-3 lg:grid-cols-[360px_1fr]">
        <ul className={`flex-col divide-y divide-line self-start rounded-card bg-white shadow-sm ${open ? 'hidden lg:flex' : 'flex'}`}>
          {list.data?.length === 0 && <li className="p-4 text-sm text-muted">{t('waInbox.empty')}</li>}
          {list.data?.map((c) => (
            <li key={c.phone}>
              <button onClick={() => setOpen(c.phone)} aria-current={open === c.phone ? 'true' : undefined}
                className={`flex w-full items-start gap-3 p-3 text-left hover:bg-[#f6faf9] ${open === c.phone ? 'bg-brand-soft' : ''}`}>
                <span aria-hidden className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[#25D366]/15 font-bold text-[#128C7E]">{(c.name ?? '#').slice(0, 1).toUpperCase()}</span>
                <span className="min-w-0 flex-1">
                  <span className="flex items-baseline justify-between gap-2"><b className="truncate">{c.name ?? shown(c.phone)}</b><span className="shrink-0 text-xs text-muted">{when(c.lastAt)}</span></span>
                  <span className="flex items-center gap-2"><span className="truncate text-sm text-muted">{c.lastDirection === 'Out' ? `${t('waInbox.you')}: ` : ''}{c.lastBody}</span>
                    {c.waiting > 0 && <span className="ml-auto shrink-0 rounded-full bg-[#25D366] px-1.5 text-xs font-bold text-white">{c.waiting}</span>}</span>
                </span>
              </button>
            </li>
          ))}
        </ul>

        {open && t0 ? (
          <section className="flex min-h-[60dvh] flex-col rounded-card bg-[#efeae2] shadow-sm">
            <header className="flex items-center gap-2 rounded-t-card bg-white p-3">
              <button onClick={() => setOpen(null)} aria-label={t('a11y.back')} className="h-10 w-10 rounded-xl text-xl lg:hidden">←</button>
              <span className="min-w-0 flex-1"><b className="block truncate">{t0.name ?? shown(t0.phone)}</b><span className="text-xs text-muted">{shown(t0.phone)}</span></span>
              {t0.customerId && <button onClick={() => navigate(`/c/${t0.customerId}`)} className="min-h-10 text-sm font-semibold text-brand underline">{t('waInbox.khata')}</button>}
            </header>
            <div className="flex flex-1 flex-col gap-2 overflow-y-auto p-3">
              {t0.messages.map((m) => (
                <div key={m.id} className={`max-w-[80%] rounded-xl px-3 py-2 text-sm shadow-sm ${m.direction === 'Out' ? 'self-end bg-[#d9fdd3]' : 'self-start bg-white'}`}>
                  {m.kind !== 'text' && m.kind !== 'reply' && <span className="mb-0.5 block text-[11px] font-bold uppercase text-[#128C7E]">{t(`waInbox.kind.${m.kind}`, { defaultValue: m.kind })}</span>}
                  <p className="whitespace-pre-line">{m.body}</p>
                  <span className="mt-0.5 flex items-center justify-end gap-1 text-[11px] text-muted">
                    {when(m.at)}{m.direction === 'Out' && <span className={m.status === 'read' ? 'text-sky-600' : m.status === 'failed' ? 'text-due' : ''} title={m.error ?? t(`waInbox.status.${m.status}`)}>{TICK[m.status] ?? ''}</span>}
                  </span>
                  {m.status === 'failed' && m.error && <span className="block text-[11px] text-due">{m.error}</span>}
                </div>
              ))}
              <div ref={end} />
            </div>
            <footer className="flex flex-col gap-2 rounded-b-card bg-white p-3">
              {t0.customerId && (
                <div className="flex flex-wrap gap-2 text-sm">
                  <button disabled={busy} onClick={() => void act(`/v1/whatsapp/inbox/${t0.phone}/last-bill`, {})} className="min-h-9 rounded-full border border-line px-3 font-semibold disabled:opacity-50">🧾 {t('waInbox.lastBill')}</button>
                  <button disabled={busy} onClick={() => void act('/v1/whatsapp/business/send', { kind: 'statement', customerId: t0.customerId })} className="min-h-9 rounded-full border border-line px-3 font-semibold disabled:opacity-50">📒 {t('waSend.statement')}</button>
                  <button disabled={busy} onClick={() => void act('/v1/whatsapp/business/send', { kind: 'reminder', customerId: t0.customerId })} className="min-h-9 rounded-full border border-line px-3 font-semibold disabled:opacity-50">🔔 {t('waSend.reminder')}</button>
                </div>
              )}
              {t0.canReplyFreely ? (
                <form onSubmit={(e) => { e.preventDefault(); if (text.trim()) void act(`/v1/whatsapp/inbox/${t0.phone}/reply`, { text }) }} className="flex gap-2">
                  <input value={text} onChange={(e) => setText(e.target.value)} maxLength={4000} placeholder={t('waInbox.type')} aria-label={t('waInbox.type')} className="h-11 min-w-0 flex-1 rounded-full border border-line px-4" />
                  <button disabled={busy || !text.trim()} className="h-11 rounded-full bg-[#25D366] px-5 font-bold text-white disabled:opacity-50">{t('waInbox.send')}</button>
                </form>
              ) : <p className="text-xs text-muted">{t('waInbox.windowClosed')}</p>}
              {t0.canReplyFreely && t0.windowEndsAt && <p className="text-[11px] text-muted">{t('waInbox.windowOpen', { time: when(t0.windowEndsAt) })}</p>}
              {error && <p className="text-sm text-due">{error}</p>}
            </footer>
          </section>
        ) : <p className="hidden rounded-card bg-white p-6 text-center text-muted lg:block">{t('waInbox.pick')}</p>}
      </div>
    </div>
  )
}

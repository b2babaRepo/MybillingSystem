import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'

/** Whether the shop's own WhatsApp Business number is connected (anyone at the counter may ask). */
export function useShopWhatsApp() {
  return useQuery({ queryKey: ['wa-business-on'], queryFn: () => api<{ connected: boolean }>('/v1/whatsapp/business/on'), staleTime: 5 * 60_000, retry: false }).data?.connected ?? false
}

/**
 * "Send from shop WhatsApp": a bill, a payment reminder or a khata statement, sent from the shop's own number with
 * its approved template. Shown only when the number is connected. Says what happened right under the button.
 */
export function SendFromShop({ kind, billId, customerId, label, className = '' }: { kind: 'bill' | 'reminder' | 'statement'; billId?: string; customerId?: string; label?: string; className?: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const connected = useShopWhatsApp()
  const [state, setState] = useState<{ ok: boolean; text: string } | null>(null)
  const [busy, setBusy] = useState(false)
  if (!connected) return null
  async function send() {
    setBusy(true)
    setState(null)
    try {
      await api('/v1/whatsapp/business/send', 'POST', { kind, billId: billId ?? null, customerId: customerId ?? null })
      setState({ ok: true, text: t('waSend.sent') })
      void queryClient.invalidateQueries({ queryKey: ['wa-inbox'] })
    } catch (e) {
      setState({ ok: false, text: e instanceof Error ? e.message : t('error') })
    } finally {
      setBusy(false)
    }
  }
  return (
    <span className={`inline-flex flex-col gap-1 print:hidden ${className}`}>
      <button type="button" disabled={busy} onClick={() => void send()}
        className="flex min-h-11 items-center justify-center gap-1.5 rounded-xl border-2 border-[#25D366] bg-white px-3 text-sm font-semibold text-[#128C7E] hover:bg-green-50 disabled:opacity-50">
        <span aria-hidden>✆</span>{busy ? t('waSend.sending') : label ?? t(`waSend.${kind}`)}</button>
      {state && <span role="status" className={`text-xs font-semibold ${state.ok ? 'text-jama' : 'text-due'}`}>{state.text}</span>}
    </span>
  )
}

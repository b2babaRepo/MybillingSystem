import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BackIcon } from '../components/Icons'
import { Button } from '../components/ui'
import { api } from '../lib/api'
import { isValidAadhaar, type FieldAgent } from '../lib/agents'
import { navigate } from '../router'

/**
 * Becoming a field agent: the application (address, market, Aadhaar, UPI for commission, the agreement), then where it
 * stands. Once approved it shows the agent code that every shop they register will carry.
 */
export function AgentScreen({ userName }: { userName: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const me = useQuery({ queryKey: ['agent', 'me'], queryFn: () => api<FieldAgent | null>('/v1/agents/me').then((a) => a ?? null) })
  const [form, setForm] = useState({ address: '', market: '', aadhaar: '', upiId: '', acceptAgreement: false })
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const agent = me.data
  const aadhaarOk = isValidAadhaar(form.aadhaar)

  async function apply() {
    setBusy(true)
    setError(null)
    try {
      const saved = await api<FieldAgent>('/v1/agents/apply', 'POST', { ...form, upiId: form.upiId.trim() || null })
      queryClient.setQueryData(['agent', 'me'], saved)
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  const box = 'h-12 rounded-xl border border-line bg-white px-3 text-lg'
  const showForm = !agent || agent.status === 'rejected'
  return (
    <div className="mx-auto min-h-dvh max-w-md pb-10">
      <header className="sticky top-0 z-10 flex items-center gap-2 bg-teal-800 px-4 py-3 text-white">
        <button onClick={() => navigate('/')} className="-ml-2 flex h-11 w-11 items-center justify-center" aria-label={t('a11y.back')}><BackIcon /></button>
        <h1 className="text-xl font-bold">{t('fieldAgent.title')}</h1>
      </header>

      <div className="flex flex-col gap-3 p-4">
        {me.isLoading && <p className="text-muted">…</p>}

        {agent && (
          <section className={`flex flex-col gap-1 rounded-[20px] p-4 ${agent.status === 'approved' ? 'bg-emerald-50 text-emerald-900' : agent.status === 'applied' ? 'bg-amber-50 text-amber-900' : 'bg-red-50 text-red-900'}`}>
            <span className="text-sm font-semibold">{t(`fieldAgent.status.${agent.status}`)}</span>
            {agent.code && <span className="font-mono text-3xl font-extrabold">{agent.code}</span>}
            <span>{agent.fullName} · {agent.market}</span>
            {agent.status === 'applied' && <span className="text-sm">{t('fieldAgent.waiting')}</span>}
            {agent.status === 'approved' && <span className="text-sm">{t('fieldAgent.approvedHint')}</span>}
            {agent.statusReason && <span className="text-sm font-semibold">{t('fieldAgent.reason', { reason: agent.statusReason })}</span>}
          </section>
        )}

        {showForm && me.isSuccess && (
          <section className="flex flex-col gap-3 rounded-[20px] bg-white p-4">
            <p className="text-sm text-muted">{t('fieldAgent.intro', { name: userName })}</p>
            <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('fieldAgent.address')}</span>
              <textarea value={form.address} onChange={(e) => setForm({ ...form, address: e.target.value })} rows={2} className="rounded-xl border border-line bg-white p-3 text-lg" /></label>
            <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('fieldAgent.market')}</span>
              <input value={form.market} onChange={(e) => setForm({ ...form, market: e.target.value })} placeholder="Lalpur market, Ranchi" className={box} /></label>
            <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('fieldAgent.aadhaar')}</span>
              <input value={form.aadhaar} onChange={(e) => setForm({ ...form, aadhaar: e.target.value.replace(/[^\d ]/g, '').slice(0, 14) })} inputMode="numeric" placeholder="1234 5678 9012" className={`${box} font-mono`} />
              <span className={`text-xs ${form.aadhaar.replace(/\s/g, '').length === 12 && !aadhaarOk ? 'font-semibold text-red-700' : 'text-muted'}`}>
                {form.aadhaar.replace(/\s/g, '').length === 12 && !aadhaarOk ? t('fieldAgent.aadhaarWrong') : t('fieldAgent.aadhaarHint')}
              </span></label>
            <label className="flex flex-col gap-1"><span className="text-sm font-medium text-muted">{t('fieldAgent.upi')}</span>
              <input value={form.upiId} onChange={(e) => setForm({ ...form, upiId: e.target.value.trim() })} placeholder="name@okaxis" className={box} /></label>
            <label className="flex items-start gap-3 rounded-xl bg-ground p-3 text-sm">
              <input type="checkbox" checked={form.acceptAgreement} onChange={(e) => setForm({ ...form, acceptAgreement: e.target.checked })} className="mt-0.5 h-5 w-5 shrink-0" />
              <span>{t('fieldAgent.agreement')}</span>
            </label>
            {error && <p className="rounded-lg bg-red-50 p-3 text-due">{error}</p>}
            <Button disabled={busy || !aadhaarOk || !form.acceptAgreement || form.address.trim().length < 5 || form.market.trim().length < 2} onClick={() => void apply()}>
              {agent?.status === 'rejected' ? t('fieldAgent.applyAgain') : t('fieldAgent.apply')}
            </Button>
          </section>
        )}
      </div>
    </div>
  )
}

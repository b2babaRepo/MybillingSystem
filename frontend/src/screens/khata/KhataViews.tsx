import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { KhataHistory } from '../../components/KhataHistory'
import { Balance, Button, Sheet, btn } from '../../components/ui'
import { nextLanguage, setLanguage } from '../../i18n'
import { api } from '../../lib/api'
import { formatPhone, formatRupees } from '../../lib/format'
import type { ItemProblem, StatementLine, Statement } from '../../lib/types'
import { navigate } from '../../router'

// Transparency step 2: the customer's own, read-only copy of their khata. The same ledger the shop sees,
// removed entries and their reasons included. Reached by the private link the shop shares (#/k/...), with no
// account, or inside the household app for every shop where the person's phone is a khata customer.

interface KhataView {
  shopName: string
  shopArea: string | null
  shopPhone: string | null
  statement: Statement
  claims?: { amount: number; reportedAt: string; status: 'waiting' | 'notReceived' }[] | null
}

interface MyKhata {
  shopId: string
  shopName: string
  shopArea: string | null
  customerId: string
  customerName: string
  balance: number
  confirmed: boolean
}

type Ticked = { problem: ItemProblem; received: string }

/**
 * What's wrong with an entry. On a bill the customer ticks the exact items (not received, got less, wrong) and
 * sees how much is in question; a note is optional. Other entries just need a note. The khata doesn't change:
 * the entry is flagged until the shop answers.
 */
function DisputeSheet({ entry, path, onClose, onSent }: { entry: StatementLine; path: string; onClose: () => void; onSent: () => void }) {
  const { t } = useTranslation()
  const items = entry.bill?.items ?? []
  const [ticked, setTicked] = useState<Record<string, Ticked>>({})
  const [message, setMessage] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const chosen = items.filter((i) => ticked[i.id])
  const shortInvalid = chosen.some((i) => ticked[i.id].problem === 'short' && !(Number(ticked[i.id].received) >= 0 && Number(ticked[i.id].received) < i.quantity && ticked[i.id].received !== ''))
  const inQuestion = chosen.reduce((sum, i) => {
    const tick = ticked[i.id]
    return sum + (tick.problem === 'short' ? Math.max(0, i.quantity - Number(tick.received || 0)) * i.unitPrice : i.amount)
  }, 0)
  const valid = !shortInvalid && (chosen.length > 0 || message.trim().length >= 3)

  const toggle = (id: string, problem: ItemProblem) =>
    setTicked((current) => {
      const next = { ...current }
      if (next[id]?.problem === problem) delete next[id]
      else next[id] = { problem, received: next[id]?.received ?? '' }
      return next
    })

  async function send() {
    setBusy(true)
    setError(null)
    try {
      await api(path, 'POST', {
        entryId: entry.id,
        message: message.trim(),
        items: chosen.map((i) => ({ billLineId: i.id, problem: ticked[i.id].problem, receivedQuantity: ticked[i.id].problem === 'short' ? Number(ticked[i.id].received) : null })),
      })
      onSent()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
      setBusy(false)
    }
  }

  return (
    <Sheet title={t('dispute.sheetTitle')} onClose={onClose}>
      <div className="flex max-h-[70dvh] flex-col gap-3 overflow-y-auto">
        {items.length > 0 && <p className="text-sm text-muted">{t('dispute.tickHint')}</p>}
        {items.map((item) => {
          const tick = ticked[item.id]
          return (
            <div key={item.id} className={`rounded-[14px] border bg-white p-3 ${tick ? 'border-amber-400' : 'border-line'}`}>
              <div className="flex justify-between gap-2 font-semibold">
                <span className="min-w-0 truncate">{item.name}</span>
                <span className="shrink-0 text-sm text-muted">{item.quantity} {t(`units.${item.unit}`, { count: item.quantity })} · {formatRupees(item.amount)}</span>
              </div>
              <div className="mt-2 grid grid-cols-3 gap-1.5 text-xs font-semibold">
                {(['not_received', 'short', 'wrong'] as const).map((problem) => (
                  <button
                    key={problem}
                    onClick={() => toggle(item.id, problem)}
                    aria-pressed={tick?.problem === problem}
                    className={`min-h-11 rounded-lg px-1 ${tick?.problem === problem ? 'bg-amber-500 text-white' : 'bg-[#edf2f1] text-ink'}`}
                  >
                    {t(`dispute.tick.${problem}`)}
                  </button>
                ))}
              </div>
              {tick?.problem === 'short' && (
                <label className="mt-2 flex items-center gap-2 text-sm">
                  {t('dispute.gotHowMuch')}
                  <input
                    value={tick.received}
                    onChange={(e) => setTicked({ ...ticked, [item.id]: { ...tick, received: e.target.value.replace(/[^\d.]/g, '') } })}
                    inputMode="decimal"
                    className="h-10 w-20 rounded-lg border border-line px-2 text-base font-bold"
                  />
                  {t(`units.${item.unit}`)} / {item.quantity}
                </label>
              )}
            </div>
          )
        })}
        <textarea
          value={message}
          onChange={(e) => setMessage(e.target.value)}
          maxLength={300}
          rows={2}
          placeholder={items.length > 0 ? t('dispute.notePlaceholder') : t('dispute.ask')}
          className="rounded-[14px] border border-line bg-white p-3 outline-none focus:border-brand-line"
        />
        {chosen.length > 0 && <p className="font-semibold">{t('dispute.inQuestion', { amount: formatRupees(Math.round(inQuestion * 100) / 100) })}</p>}
        {error && <p className="text-sm text-due">{error}</p>}
        <Button disabled={!valid || busy} onClick={() => void send()} className="h-14">
          {t('dispute.send')}
        </Button>
      </div>
    </Sheet>
  )
}

/** "I have paid": the customer's word to the shop. Their khata changes only when the shop confirms the money came. */
function PaidCard({ path, balance, queryKey }: { path: string; balance: number; queryKey: unknown[] }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [amount, setAmount] = useState(String(balance))
  const [utr, setUtr] = useState('')
  const [message, setMessage] = useState('')
  const [sent, setSent] = useState(false)
  async function send() {
    setMessage('')
    try {
      await api(path, 'POST', { id: crypto.randomUUID(), amount: Number(amount), utr: utr.trim() || null })
      setSent(true)
      await queryClient.invalidateQueries({ queryKey })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  if (sent) return <p className="rounded-[14px] bg-brand-soft p-3 text-sm font-semibold text-brand-deep">{t('receipts.paidSent')}</p>
  if (!open) return <button onClick={() => setOpen(true)} className={`flex h-12 items-center justify-center rounded-xl ${btn.outline}`}>{t('receipts.iPaid')}</button>
  return (
    <div className="flex flex-col gap-2 rounded-card bg-white p-4">
      <input aria-label={t('receipts.amount')} value={amount} onChange={(e) => setAmount(e.target.value)} inputMode="decimal" className="h-12 rounded-xl border border-line px-3 text-lg font-bold" />
      <input aria-label={t('receipts.utr')} placeholder={t('receipts.utr')} value={utr} onChange={(e) => setUtr(e.target.value)} maxLength={30} className="h-12 rounded-xl border border-line px-3" />
      <Button disabled={!(Number(amount) > 0)} onClick={() => void send()}>{t('receipts.tellShop')}</Button>
      {message && <p className="text-sm font-semibold text-due">{message}</p>}
    </div>
  )
}

function KhataBody({ view, disputePath, paidPath, queryKey }: { view: KhataView; disputePath: string; paidPath: string; queryKey: unknown[] }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [disputing, setDisputing] = useState<StatementLine | null>(null)
  const { customer } = view.statement
  const open = view.statement.entries.filter((e) => e.dispute && !e.dispute.resolvedAt).length
  return (
    <div className="flex flex-col gap-3 p-4">
      <div className="flex items-start justify-between rounded-card bg-white p-4">
        <div className="min-w-0">
          <div className="text-sm text-muted">{[view.shopName, view.shopArea].filter(Boolean).join(' · ')}</div>
          <div className="text-2xl font-bold">{customer.name}</div>
        </div>
        <Balance amount={customer.balance} large />
      </div>
      <p className="rounded-[14px] bg-brand-soft p-3 text-sm text-brand-deep">{t('myKhata.readOnly', { shop: view.shopName })}</p>
      {view.shopPhone && (
        <a className="flex h-12 items-center justify-center rounded-2xl border border-line bg-white font-semibold" href={`tel:${view.shopPhone}`}>
          📞 {t('myKhata.callShop', { phone: formatPhone(view.shopPhone) })}
        </a>
      )}
      {view.claims?.map((c) => (
        <p key={c.reportedAt} className={`rounded-[14px] p-3 text-sm font-semibold ${c.status === 'waiting' ? 'bg-brand-soft text-brand-deep' : 'bg-warn-soft text-amber-950'}`}>
          {t(`receipts.claim_${c.status}`, { amount: formatRupees(c.amount) })}
        </p>
      ))}
      {customer.balance > 0 && <PaidCard path={paidPath} balance={customer.balance} queryKey={queryKey} />}
      {open > 0 && <p className="rounded-[14px] bg-warn-soft p-3 text-sm font-semibold text-amber-950">⚠ {t('dispute.openCount', { count: open })}</p>}
      <p className="text-sm text-muted">{t('dispute.hint')}</p>
      <KhataHistory
        entries={view.statement.entries}
        onDispute={setDisputing}
      />
      {disputing && (
        <DisputeSheet
          entry={disputing}
          path={disputePath}
          onClose={() => setDisputing(null)}
          onSent={() => {
            setDisputing(null)
            void queryClient.invalidateQueries({ queryKey })
          }}
        />
      )}
    </div>
  )
}

/** The page behind the shop's link: #/k/{shop}/{customer}?t={token}. Open to anyone holding the link. */
export function KhataLinkPage({ path }: { path: string }) {
  const { t, i18n } = useTranslation()
  const match = path.match(/^\/k\/([^/]+)\/([0-9a-f-]+)\?t=([\w-]+)/)
  const view = useQuery({
    queryKey: ['khata-link-view', path],
    queryFn: () => api<KhataView>(`/v1/khata/${match![1]}/${match![2]}?t=${match![3]}`),
    enabled: !!match,
    retry: false,
  })

  return (
    <div className="mx-auto min-h-dvh max-w-xl">
      <header className="sticky top-0 z-10 flex items-center gap-2 bg-brand px-4 py-3 text-white">
        <h1 className="flex-1 truncate text-xl font-bold">{t('myKhata.title')}</h1>
        <button onClick={() => setLanguage(nextLanguage(i18n.language).code)} className="h-11 rounded-full bg-white/20 px-3 text-sm font-semibold">
          {nextLanguage(i18n.language).short}
        </button>
      </header>
      {view.data ? (
        <KhataBody view={view.data} queryKey={['khata-link-view', path]} disputePath={`/v1/khata/${match![1]}/${match![2]}/disputes?t=${match![3]}`} paidPath={`/v1/khata/${match![1]}/${match![2]}/paid?t=${match![3]}`} />
      ) : view.isError || !match ? (
        <p className="p-6 text-center text-muted">{t('myKhata.linkInvalid')}</p>
      ) : (
        <p className="p-6 text-center text-muted">…</p>
      )}
    </div>
  )
}

/** A khata under my number that isn't proved mine yet: the shop's code opens it. Nothing about it shows until then. */
function ConfirmKhata({ khata }: { khata: MyKhata }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const [code, setCode] = useState('')
  const [message, setMessage] = useState('')
  async function confirm() {
    setMessage('')
    try {
      await api(`/v1/me/khata/${khata.shopId}/${khata.customerId}/confirm`, 'POST', { code: code.trim() })
      await queryClient.invalidateQueries({ queryKey: ['my-khata'] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  return (
    <div className="flex flex-col gap-2 px-3.5 py-3">
      <div className="font-bold">{khata.shopName}</div>
      <p className="text-sm text-muted">{t('myKhata.confirmHint')}</p>
      <div className="flex gap-2">
        <input aria-label={t('myKhata.confirmCode')} placeholder={t('myKhata.confirmCode')} value={code} onChange={(e) => setCode(e.target.value)} inputMode="numeric" maxLength={6} autoComplete="one-time-code" className="h-11 min-w-0 flex-1 rounded-xl border border-line px-3 text-lg" />
        <Button disabled={code.trim().length !== 6} onClick={() => void confirm()}>{t('myKhata.confirmNow')}</Button>
      </div>
      {message && <p className="text-sm text-due">{message}</p>}
    </div>
  )
}

/** Household app: every shop where my phone has a khata. */
export function MyKhataScreen() {
  const { t } = useTranslation()
  const mine = useQuery({ queryKey: ['my-khata'], queryFn: () => api<MyKhata[]>('/v1/me/khata') })
  return (
    <div className="flex flex-col gap-3 p-4">
      {mine.data?.length === 0 && <p className="py-8 text-center text-muted">{t('myKhata.none')}</p>}
      <ul className="divide-y divide-[#edf2f1] rounded-card bg-white">
        {mine.data?.map((k) => (
          <li key={k.customerId}>
            {!k.confirmed ? <ConfirmKhata khata={k} /> : <button className="flex min-h-16 w-full items-center gap-3 px-3.5 py-3 text-left" onClick={() => navigate(`/khata/${k.shopId}/${k.customerId}`)}>
              <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-[#e0efec] text-lg font-extrabold text-brand">{k.shopName.charAt(0)}</span>
              <div className="min-w-0 flex-1">
                <div className="truncate font-bold">{k.shopName}</div>
                {k.shopArea && <div className="truncate text-sm text-muted">{k.shopArea}</div>}
              </div>
              <div className={`text-right font-bold ${k.balance > 0 ? 'text-udhaar' : 'text-muted'}`}>
                {formatRupees(Math.abs(k.balance))}
                <div className="text-xs font-normal">{k.balance > 0 ? t('myKhata.youOwe') : t('settled')}</div>
              </div>
              <span aria-hidden className="text-xl text-muted">›</span>
            </button>}
          </li>
        ))}
      </ul>
    </div>
  )
}

/** Household app: my khata at one shop. */
export function MyKhataDetail({ shopId, customerId }: { shopId: string; customerId: string }) {
  const { t } = useTranslation()
  const view = useQuery({ queryKey: ['my-khata', shopId, customerId], queryFn: () => api<KhataView>(`/v1/me/khata/${shopId}/${customerId}`) })
  if (view.isError) return <p className="p-6 text-center text-muted">{t('myKhata.linkInvalid')}</p>
  return view.data ? (
    <KhataBody view={view.data} queryKey={['my-khata', shopId, customerId]} disputePath={`/v1/me/khata/${shopId}/${customerId}/disputes`} paidPath={`/v1/me/khata/${shopId}/${customerId}/paid`} />
  ) : (
    <p className="p-6 text-center text-muted">…</p>
  )
}

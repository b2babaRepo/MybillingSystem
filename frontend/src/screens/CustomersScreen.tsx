import { phoneOk } from '../lib/phone'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Balance, Button, Field, PendingDot, Sheet } from '../components/ui'
import { addCustomer, useCustomers } from '../lib/data'
import { notify } from '../lib/notify'
import { formatPhone, formatRupees } from '../lib/format'
import { navigate } from '../router'
import { OpenDisputesBanner } from '../components/OpenDisputesBanner'

/** Everyone on khata, most due first ("who owes me?"). */
export function CustomersScreen({ shopId, selectedId, isOwner = false }: { shopId: string; selectedId?: string; isOwner?: boolean }) {
  const { t } = useTranslation()
  const { customers, isLoading } = useCustomers(shopId)
  const [search, setSearch] = useState('')
  const [adding, setAdding] = useState(false)

  const [sort, setSort] = useState<'due' | 'old' | 'az'>('due')
  const query = search.trim().toLowerCase()
  const matching = query
    ? customers.filter((c) => c.name.toLowerCase().includes(query) || c.phone?.includes(query))
    : customers
  // "Most due" first by default: the question a shopkeeper opens this screen with. "Owing" shows only those who owe.
  const shown =
    sort === 'az' ? matching : sort === 'due' ? [...matching].sort((a, b) => b.balance - a.balance) : matching.filter((c) => c.balance > 0)

  const toGet = customers.reduce((n, c) => n + Math.max(0, c.balance), 0)
  const advance = customers.reduce((n, c) => n + Math.max(0, -c.balance), 0)
  const owingCount = customers.filter((c) => c.balance > 0).length
  const card = (on: boolean) => `flex min-h-[80px] flex-col justify-between rounded-card p-3 text-left shadow-sm transition ${on ? 'ring-2 ring-brand' : 'ring-1 ring-black/5'}`
  const icon = (d: string) => <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="shrink-0"><path d={d} /></svg>
  const shortcuts = [
    { to: '/receipts', label: t('receipts.link'), d: 'M3 6h18v12H3zM12 9.5a2.5 2.5 0 100 5 2.5 2.5 0 000-5', tone: 'bg-[#e3f2ee] text-[#0b5e4f] hover:bg-[#d2ebe4]' },
    { to: '/dues', label: t('dues.link'), d: 'M5 4h4l2 5-2.5 1.5a11 11 0 005 5L15 13l5 2v4a2 2 0 01-2 2A16 16 0 013 6a2 2 0 012-2', tone: 'bg-[#fde8ec] text-[#be123c] hover:bg-[#fbd5dd]' },
    { to: '/reminders', label: t('menu.reminders'), d: 'M6 16V11a6 6 0 0112 0v5l2 2H4zM10 21h4', tone: 'bg-[#fdf1e3] text-[#a1460a] hover:bg-[#fbe6cc]' },
    ...(isOwner ? [{ to: '/khata/import', label: t('khata3.import'), d: 'M12 15V3M7 8l5-5 5 5M4 15v5h16v-5', tone: 'bg-[#f1eafd] text-[#6d28d9] hover:bg-[#e7dcfb]' }] : []),
  ]

  return (
    <div className="p-4">
      <OpenDisputesBanner />
      <div className="mb-3 flex flex-wrap items-center gap-2">
        <h1 className="mr-auto text-xl font-extrabold">{t('khata')}</h1>
        <Button className="hidden h-12 px-5 text-base lg:block" onClick={() => setAdding(true)}>+ {t('addCustomer')}</Button>
      </div>

      {/* What is out on khata, at a glance. Tapping "Owing" shows only those who owe. */}
      <div className="mb-3 grid grid-cols-2 gap-2 sm:grid-cols-4">
        <button onClick={() => setSort(sort === 'old' ? 'due' : 'old')} className={`${card(sort === 'old')} bg-due-soft`}>
          <span className="text-xs font-semibold text-due">{t('khata3.toGet')}</span><b className="text-xl text-due">{formatRupees(toGet)}</b>
        </button>
        <button onClick={() => setSort(sort === 'old' ? 'due' : 'old')} className={`${card(sort === 'old')} bg-white`}>
          <span className="text-xs font-semibold text-muted">{t('khata3.owing')}</span><b className="text-2xl">{owingCount}</b>
        </button>
        <div className={`${card(false)} bg-green-50`}>
          <span className="text-xs font-semibold text-emerald-800">{t('khata3.advance')}</span><b className="text-xl text-jama">{formatRupees(advance)}</b>
        </div>
        <div className={`${card(false)} bg-white`}>
          <span className="text-xs font-semibold text-muted">{t('khata3.all')}</span><b className="text-2xl">{customers.length}</b>
        </div>
      </div>

      {/* Shortcuts, each in its own colour with a line icon on a white dot, as on the customer page. */}
      <div className="mb-3 grid grid-cols-2 gap-2 sm:grid-cols-4">
        {shortcuts.map((x) => (
          <button key={x.to} onClick={() => navigate(x.to)} className={`flex min-h-[52px] items-center gap-2.5 rounded-2xl px-3 text-left text-sm font-bold transition active:scale-[0.98] ${x.tone}`}>
            <span aria-hidden className="flex h-[30px] w-[30px] shrink-0 items-center justify-center rounded-full bg-white">{icon(x.d)}</span>
            <span className="leading-tight">{x.label}</span>
          </button>
        ))}
      </div>

      <div className="mb-3 flex flex-col gap-3 rounded-card bg-white p-3 shadow-sm">
        <span className="relative block">
          <span aria-hidden className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-muted">{icon('M11 4a7 7 0 100 14 7 7 0 000-14zM20 20l-3.5-3.5')}</span>
          <input
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t('search')}
            aria-label={t('search')}
            className="h-12 w-full rounded-2xl border border-line bg-[#f6faf9] pl-11 pr-4 text-[17px] outline-none transition focus:border-brand-line focus:bg-white focus:ring-4 focus:ring-brand-line/10"
          />
        </span>
        {/* Sort: one segmented control. */}
        <div className="grid grid-cols-3 gap-1 rounded-2xl bg-[#edf2f1] p-1">
          {(['due', 'old', 'az'] as const).map((key) => (
            <button
              key={key}
              onClick={() => setSort(key)}
              aria-pressed={sort === key}
              className={`h-10 rounded-xl text-[15px] font-semibold transition ${sort === key ? 'bg-brand text-white shadow-md shadow-teal-900/20' : 'text-muted hover:text-ink'}`}
            >
              {t(`sort.${key}`)}
            </button>
          ))}
        </div>
      </div>
      {!isLoading && customers.length === 0 && <p className="py-8 text-center text-muted">{t('noCustomers')}</p>}
      {/* A computer: a register-style table, Party | Amount, sorted by clicking a head, with what is owed in all at the foot. */}
      {shown.length > 0 && (
        <table className="hidden w-full border-collapse overflow-hidden rounded-card bg-white text-sm shadow-sm lg:table">
          <thead className="bg-brand-soft text-left text-xs uppercase tracking-wide text-ink">
            <tr>
              <th className="px-3 py-2"><button onClick={() => setSort('az')} className="flex items-center gap-1 font-bold uppercase" aria-label={t('custTable.sortName')}>{t('custTable.party')}<span aria-hidden className="text-muted">{sort === 'az' ? '▲' : '↕'}</span></button></th>
              <th className="px-3 py-2 text-right"><button onClick={() => setSort('due')} className="ml-auto flex items-center gap-1 font-bold uppercase" aria-label={t('custTable.sortAmount')}>{t('custTable.amount')}<span aria-hidden className="text-muted">{sort === 'due' ? '▼' : '↕'}</span></button></th>
            </tr>
          </thead>
          <tbody className="divide-y divide-line">
            {shown.map((c) => (
              <tr key={c.id} onClick={() => navigate(`/c/${c.id}`)} aria-current={selectedId === c.id ? 'true' : undefined}
                className={`cursor-pointer hover:bg-[#f6faf9] ${selectedId === c.id ? 'bg-brand-soft' : ''}`}>
                <td className="px-3 py-2.5">
                  <span className="flex items-center gap-1.5 font-semibold">{c.name}{c.pending > 0 && <PendingDot />}</span>
                  {c.phone && <span className="block text-xs text-muted">{formatPhone(c.phone)}</span>}
                </td>
                <td className={`whitespace-nowrap px-3 py-2.5 text-right font-bold ${c.balance > 0 ? 'text-due' : c.balance < 0 ? 'text-jama' : 'text-muted'}`}>
                  {formatRupees(Math.abs(c.balance))}
                  {c.balance !== 0 && <span className="block text-[11px] font-normal">{c.balance > 0 ? t('custTable.owes') : t('custTable.advance')}</span>}
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-t-2 border-ink font-bold">
              <td className="px-3 py-2">{t('custTable.total', { count: shown.length })}</td>
              <td className="px-3 py-2 text-right text-due">{formatRupees(shown.reduce((n, c) => n + Math.max(0, c.balance), 0))}</td>
            </tr>
          </tfoot>
        </table>
      )}
      <ul className="divide-y divide-[#edf2f1] rounded-card bg-white shadow-sm lg:hidden">
        {shown.map((c) => (
          <li key={c.id}>
            <button aria-current={selectedId === c.id ? 'true' : undefined} className={`flex min-h-16 w-full items-center gap-3 px-3.5 py-3 text-left active:bg-slate-50 lg:hover:bg-[#f6faf9] ${selectedId === c.id ? 'lg:bg-brand-soft' : ''}`} onClick={() => navigate(`/c/${c.id}`)}>
              <span className={`flex h-11 w-11 shrink-0 items-center justify-center rounded-full font-bold ${c.balance > 0 ? 'bg-due-soft text-due' : c.balance < 0 ? 'bg-green-50 text-jama' : 'bg-[#e0efec] text-brand'}`}>{initials(c.name)}</span>
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2 text-[17px] font-semibold">
                  {c.name} {c.pending > 0 && <PendingDot />}
                </div>
                {c.phone && <div className="text-[13px] text-muted">{formatPhone(c.phone)}</div>}
              </div>
              <Balance amount={c.balance} />
              <span aria-hidden className="text-xl text-muted">›</span>
            </button>
          </li>
        ))}
      </ul>
      <Button className="fixed right-[max(1rem,calc(50vw-17rem))] bottom-24 h-14 lg:hidden rounded-full! px-6 shadow-[0_6px_16px_rgba(15,118,110,0.35)]" onClick={() => setAdding(true)}>
        + {t('addCustomer')}
      </Button>
      {adding && <AddCustomerSheet shopId={shopId} onClose={() => setAdding(false)} onAdded={(id) => navigate(`/c/${id}`)} />}
    </div>
  )
}

export function AddCustomerSheet({ shopId, onClose, onAdded }: { shopId: string; onClose: () => void; onAdded: (id: string) => void }) {
  const { t } = useTranslation()
  const [name, setName] = useState('')
  const [phone, setPhone] = useState('')

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (!name.trim() || !phoneOk(phone)) return
    const id = await addCustomer(shopId, name.trim(), phone.trim())
    notify.success(t('notice.customerAdded', { name: name.trim() }))
    onClose()
    onAdded(id)
  }

  return (
    <Sheet title={t('addCustomer')} onClose={onClose}>
      <form onSubmit={submit} className="space-y-3">
        <Field label={t('customerName')} value={name} onChange={(e) => setName(e.target.value)} required autoFocus />
        <Field label={t('phoneOptional')} value={phone} onChange={(e) => setPhone(e.target.value)} inputMode="tel" />
        {!phoneOk(phone) && <p role="alert" className="text-sm font-semibold text-due">{t('hwp2.badPhone')}</p>}
        <Button type="submit" className="w-full" disabled={!name.trim() || !phoneOk(phone)}>
          {t('save')}
        </Button>
      </form>
    </Sheet>
  )
}

function initials(name: string) {
  return name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0]).join('').toUpperCase()
}

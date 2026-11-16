import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Balance, Button, Field, PendingDot, Sheet } from '../components/ui'
import { addCustomer, useCustomers } from '../lib/data'
import { formatPhone } from '../lib/format'
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

  return (
    <div className="p-4">
      <OpenDisputesBanner />
      <Button className="mb-3 hidden h-14 w-full text-lg shadow-md shadow-teal-900/15 lg:block" onClick={() => setAdding(true)}>+ {t('addCustomer')}</Button>
      <div className="mb-3 flex flex-col gap-3 rounded-card bg-white p-3 shadow-sm">
        <span className="relative block">
          <span aria-hidden className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-muted">🔍</span>
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
              className={`h-10 rounded-xl text-[15px] font-semibold transition ${sort === key ? 'bg-white text-brand-strong shadow-sm ring-1 ring-brand/20' : 'text-muted hover:text-ink'}`}
            >
              {t(`sort.${key}`)}
            </button>
          ))}
        </div>
      </div>
      {/* Shortcuts as cards, not bare links. */}
      <div className={`mb-3 grid gap-2 ${isOwner ? 'grid-cols-2' : 'grid-cols-1'}`}>
        <button onClick={() => navigate('/receipts')} className="flex min-h-14 items-center gap-2.5 rounded-2xl bg-white p-3 text-left text-sm font-semibold shadow-sm hover:bg-[#f6faf9]">
          <span aria-hidden className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-green-50 text-lg">💰</span>
          <span className="min-w-0 flex-1">{t('receipts.link')}</span><span aria-hidden className="text-muted">›</span>
        </button>
        {isOwner && (
          <button onClick={() => navigate('/khata/import')} className="flex min-h-14 items-center gap-2.5 rounded-2xl bg-white p-3 text-left text-sm font-semibold shadow-sm hover:bg-[#f6faf9]">
            <span aria-hidden className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-brand-soft text-lg">📥</span>
            <span className="min-w-0 flex-1 leading-snug">{t('partyImport.link')}</span><span aria-hidden className="text-muted">›</span>
          </button>
        )}
      </div>
      {!isLoading && customers.length === 0 && <p className="py-8 text-center text-muted">{t('noCustomers')}</p>}
      <ul className="divide-y divide-[#edf2f1] rounded-card bg-white">
        {shown.map((c) => (
          <li key={c.id}>
            <button aria-current={selectedId === c.id ? 'true' : undefined} className={`flex min-h-16 w-full items-center gap-3 px-3.5 py-3 text-left active:bg-slate-50 lg:hover:bg-[#f6faf9] ${selectedId === c.id ? 'lg:bg-brand-soft' : ''}`} onClick={() => navigate(`/c/${c.id}`)}>
              <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-[#e0efec] font-bold text-brand">{initials(c.name)}</span>
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
    const id = await addCustomer(shopId, name.trim(), phone.trim())
    onClose()
    onAdded(id)
  }

  return (
    <Sheet title={t('addCustomer')} onClose={onClose}>
      <form onSubmit={submit} className="space-y-3">
        <Field label={t('customerName')} value={name} onChange={(e) => setName(e.target.value)} required autoFocus />
        <Field label={t('phoneOptional')} value={phone} onChange={(e) => setPhone(e.target.value)} inputMode="tel" />
        <Button type="submit" className="w-full" disabled={!name.trim()}>
          {t('save')}
        </Button>
      </form>
    </Sheet>
  )
}

function initials(name: string) {
  return name.split(/\s+/).filter(Boolean).slice(0, 2).map((w) => w[0]).join('').toUpperCase()
}

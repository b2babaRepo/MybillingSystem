import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Balance, Button, Field, PendingDot, Sheet } from '../components/ui'
import { addCustomer, useCustomers } from '../lib/data'
import { formatPhone } from '../lib/format'
import { navigate } from '../router'
import { OpenDisputesBanner } from '../components/OpenDisputesBanner'

/** Everyone on khata, most due first ("who owes me?"). */
export function CustomersScreen({ shopId }: { shopId: string }) {
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
      <input
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder={t('search')}
        aria-label={t('search')}
        className="mb-3 h-12 w-full rounded-[14px] border border-line bg-white px-4 text-[17px] outline-none focus:border-teal-600"
      />
      <div className="mb-3 flex gap-2">
        {(['due', 'old', 'az'] as const).map((key) => (
          <button
            key={key}
            onClick={() => setSort(key)}
            className={`h-11 rounded-full px-4 text-[15px] font-semibold ${sort === key ? 'bg-ink text-white' : 'border border-line bg-white text-ink'}`}
          >
            {t(`sort.${key}`)}
          </button>
        ))}
      </div>
      {!isLoading && customers.length === 0 && <p className="py-8 text-center text-muted">{t('noCustomers')}</p>}
      <ul className="divide-y divide-[#edf2f1] rounded-[18px] bg-white">
        {shown.map((c) => (
          <li key={c.id}>
            <button className="flex min-h-16 w-full items-center gap-3 px-3.5 py-3 text-left active:bg-slate-50" onClick={() => navigate(`/c/${c.id}`)}>
              <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-full bg-[#e0efec] font-bold text-teal-700">{initials(c.name)}</span>
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
      <Button className="fixed right-4 bottom-24 h-14 rounded-full! px-6 shadow-[0_6px_16px_rgba(15,118,110,0.35)]" onClick={() => setAdding(true)}>
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

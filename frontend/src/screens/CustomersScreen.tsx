import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Balance, Button, Field, PendingDot, Sheet } from '../components/ui'
import { addCustomer, useCustomers } from '../lib/data'
import { navigate } from '../router'

/** Home screen: everyone on khata, biggest question first ("who owes me?"). */
export function CustomersScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const { customers, isLoading } = useCustomers(shopId)
  const [search, setSearch] = useState('')
  const [adding, setAdding] = useState(false)

  const query = search.trim().toLowerCase()
  const shown = query
    ? customers.filter((c) => c.name.toLowerCase().includes(query) || c.phone?.includes(query))
    : customers

  return (
    <div className="p-4">
      <input
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder={t('search')}
        className="mb-3 h-12 w-full rounded-xl border border-slate-300 bg-white px-3 text-lg"
      />
      {!isLoading && customers.length === 0 && <p className="py-8 text-center text-slate-500">{t('noCustomers')}</p>}
      <ul className="divide-y divide-slate-200 rounded-xl bg-white shadow-sm">
        {shown.map((c) => (
          <li key={c.id}>
            <button className="flex w-full items-center gap-3 p-4 text-left active:bg-slate-100" onClick={() => navigate(`/c/${c.id}`)}>
              <div className="flex-1">
                <div className="flex items-center gap-2 text-lg font-semibold">
                  {c.name} {c.pending > 0 && <PendingDot />}
                </div>
                {c.phone && <div className="text-sm text-slate-500">{c.phone}</div>}
              </div>
              <Balance amount={c.balance} />
            </button>
          </li>
        ))}
      </ul>
      <Button className="fixed right-4 bottom-24 shadow-lg" onClick={() => setAdding(true)}>
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

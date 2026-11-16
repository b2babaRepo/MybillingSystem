import { shownLocale } from '../i18n'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { useCustomers, useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'

interface Card {
  customerId: string; hairType: string | null; skinType: string | null; allergies: string | null; colourFormula: string | null; patchTestOn: string | null
  patchTestReaction: boolean; notes: string | null; patchTestDue: boolean; visits: { date: string; services: string; stylistName: string | null; total: number }[]
}
interface Recipe { serviceId: string; products: { productId: string; name: string; unit: string; quantity: number; costPrice: number | null; stock: number }[]; cost: number; price: number }

const day = (iso: string) => new Date(`${iso}T00:00`).toLocaleDateString(shownLocale(), { day: 'numeric', month: 'short', year: 'numeric' })
const select = 'h-12 w-full rounded-xl border border-line bg-white px-3'

/** Client cards (hair, skin, allergies, colour formula, patch test, past visits) and, for the owner, what each service uses from stock. */
export function ClientCardsScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const { customers } = useCustomers(shopId)
  const [customerId, setCustomerId] = useState(() => new URLSearchParams(location.hash.split('?')[1] ?? '').get('customer') ?? '')
  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
        <h3 className="font-bold">{t('cards.title')}</h3>
        <select value={customerId} onChange={(e) => setCustomerId(e.target.value)} aria-label={t('jobs.customer')} className={select}>
          <option value="">{t('packages.chooseCustomer')}</option>{customers.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </select>
        {customerId && <ClientCard key={customerId} shopId={shopId} customerId={customerId} />}
      </section>
      {isOwner && <ServiceProducts shopId={shopId} />}
    </div>
  )
}

function ClientCard({ shopId, customerId }: { shopId: string; customerId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const card = useQuery({ queryKey: [shopId, 'client-card', customerId], queryFn: () => api<Card>(`/v1/customers/${customerId}/card`) }).data
  const [d, setD] = useState({ hairType: '', skinType: '', allergies: '', colourFormula: '', patchTestOn: '', patchTestReaction: false, notes: '' })
  const [message, setMessage] = useState('')
  useEffect(() => {
    if (card) setD({ hairType: card.hairType ?? '', skinType: card.skinType ?? '', allergies: card.allergies ?? '', colourFormula: card.colourFormula ?? '', patchTestOn: card.patchTestOn ?? '', patchTestReaction: card.patchTestReaction, notes: card.notes ?? '' })
  }, [card])
  const set = (key: keyof typeof d) => (e: { target: { value: string } }) => setD({ ...d, [key]: e.target.value })
  async function save() {
    setMessage('')
    try {
      await api(`/v1/customers/${customerId}/card`, 'PUT', { ...d, patchTestOn: d.patchTestOn || null })
      setMessage(t('saved'))
      void queryClient.invalidateQueries({ queryKey: [shopId, 'client-card', customerId] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  if (!card) return null
  return (
    <div className="flex flex-col gap-2">
      {card.patchTestReaction ? <p className="rounded-xl bg-red-50 p-2 text-sm font-semibold text-due">{t('cards.reaction')}</p>
        : card.patchTestDue && <p className="rounded-xl bg-amber-50 p-2 text-sm font-semibold">{t('cards.patchDue')}</p>}
      <div className="grid grid-cols-2 gap-2">
        <Field label={t('cards.hair')} value={d.hairType} onChange={set('hairType')} placeholder={t('cards.hairHint')} />
        <Field label={t('cards.skin')} value={d.skinType} onChange={set('skinType')} placeholder={t('cards.skinHint')} />
      </div>
      <Field label={t('cards.allergies')} value={d.allergies} onChange={set('allergies')} />
      <Field label={t('cards.formula')} value={d.colourFormula} onChange={set('colourFormula')} placeholder={t('cards.formulaHint')} />
      <div className="grid grid-cols-2 gap-2">
        <Field label={t('cards.patchOn')} type="date" value={d.patchTestOn} onChange={set('patchTestOn')} />
        <label className="flex min-h-11 items-end gap-2 pb-3 text-sm"><input type="checkbox" checked={d.patchTestReaction} onChange={(e) => setD({ ...d, patchTestReaction: e.target.checked })} />{t('cards.hadReaction')}</label>
      </div>
      <Field label={t('cards.notes')} value={d.notes} onChange={set('notes')} />
      {message && <p className="text-sm font-semibold text-teal-900">{message}</p>}
      <Button onClick={() => void save()}>{t('save')}</Button>
      <h4 className="mt-2 font-semibold">{t('cards.visits')}</h4>
      {card.visits.length === 0 && <p className="text-sm text-muted">{t('cards.noVisits')}</p>}
      {card.visits.map((v, i) => <p key={i} className="flex justify-between text-sm"><span>{day(v.date)} · {v.services}{v.stylistName ? ` · ${v.stylistName}` : ''}</span><span>{formatRupees(v.total)}</span></p>)}
    </div>
  )
}

function ServiceProducts({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const all = useProducts(shopId).products
  const services = all.filter((p) => p.isService)
  const stock = all.filter((p) => !p.isService)
  const [serviceId, setServiceId] = useState('')
  const recipe = useQuery({ queryKey: [shopId, 'service-products', serviceId], enabled: !!serviceId, queryFn: () => api<Recipe>(`/v1/services/${serviceId}/products`) }).data
  const [rows, setRows] = useState<{ productId: string; quantity: string }[]>([])
  const [message, setMessage] = useState('')
  useEffect(() => { setRows(recipe?.products.map((p) => ({ productId: p.productId, quantity: String(p.quantity) })) ?? []) }, [recipe])
  async function save() {
    setMessage('')
    try {
      await api(`/v1/services/${serviceId}/products`, 'PUT', { products: rows.filter((r) => r.productId).map((r) => ({ productId: r.productId, quantity: Number(r.quantity) || 0 })) })
      setMessage(t('saved'))
      void queryClient.invalidateQueries({ queryKey: [shopId, 'service-products', serviceId] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  return (
    <section className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
      <h3 className="font-bold">{t('cards.usesTitle')}</h3>
      <p className="text-xs text-muted">{t('cards.usesHint')}</p>
      <select value={serviceId} onChange={(e) => setServiceId(e.target.value)} aria-label={t('packages.service')} className={select}>
        <option value="">{t('cards.chooseService')}</option>{services.map((s) => <option key={s.id} value={s.id}>{s.name}</option>)}
      </select>
      {serviceId && recipe && (
        <>
          {rows.map((r, i) => (
            <div key={i} className="grid grid-cols-[2fr_1fr_auto] items-end gap-2">
              <select value={r.productId} onChange={(e) => setRows(rows.map((x, j) => (j === i ? { ...x, productId: e.target.value } : x)))} aria-label={t('cards.product')} className={select}>
                <option value="">—</option>{stock.map((p) => <option key={p.id} value={p.id}>{p.name} ({p.unit})</option>)}
              </select>
              <Field label={t('cards.quantity')} value={r.quantity} onChange={(e) => setRows(rows.map((x, j) => (j === i ? { ...x, quantity: e.target.value } : x)))} inputMode="decimal" />
              <Button tone="plain" className="h-12 px-3" aria-label={t('cards.remove')} onClick={() => setRows(rows.filter((_, j) => j !== i))}>×</Button>
            </div>
          ))}
          <Button tone="plain" onClick={() => setRows([...rows, { productId: '', quantity: '1' }])}>{t('cards.addProduct')}</Button>
          <p className="text-sm">{t('cards.costLine', { cost: formatRupees(recipe.cost), price: formatRupees(recipe.price) })}</p>
          {recipe.products.some((p) => p.costPrice == null) && <p className="text-xs text-due">{t('cards.noCost', { names: recipe.products.filter((p) => p.costPrice == null).map((p) => p.name).join(', ') })}</p>}
          {message && <p className="text-sm font-semibold text-teal-900">{message}</p>}
          <Button onClick={() => void save()}>{t('save')}</Button>
        </>
      )}
    </section>
  )
}

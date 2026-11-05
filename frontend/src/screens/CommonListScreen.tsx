import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { AisleTiles, type Aisle } from '../components/AisleTiles'
import { ItemCard } from '../components/ItemCard'
import { imageUrl } from '../lib/images'
import { Button } from '../components/ui'
import { api, request } from '../lib/api'
import { useProducts } from '../lib/data'
import { formatRupees } from '../lib/format'
import type { CatalogueItem } from '../lib/planner'
import type { Product, Unit } from '../lib/types'
import { navigate } from '../router'

type Pick = { price: string; stock: string }

/**
 * The shop fills its shelf from the common list every shop and household shares: tap an item, type your own
 * price (and how many you have), add. Names, units and categories come from the list, so a household's
 * "आटा" finds this shop's atta with this shop's price. Items already sold here show your price instead.
 * Anything not on the list (a local brand, loose goods) is still added by hand on the Items screen.
 */
export function CommonListScreen({ shopId }: { shopId: string }) {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const catalogue = useQuery({ queryKey: ['catalogue'], queryFn: () => api<CatalogueItem[]>('/v1/catalogue'), staleTime: 3_600_000 })
  const { products } = useProducts(shopId)
  const [aisle, setAisle] = useState<Aisle | null>(null)
  const [picked, setPicked] = useState<Record<number, Pick>>({})
  const [editing, setEditing] = useState<CatalogueItem | null>(null)
  const [managing, setManaging] = useState<CatalogueItem | null>(null)
  const [busy, setBusy] = useState(false)
  const [result, setResult] = useState<string | null>(null)

  const items = catalogue.data ?? []
  const mine = new Map(products.filter((p) => p.catalogueItemId).map((p) => [p.catalogueItemId!, p]))
  const shown = items.filter((i) => i.aisle === aisle?.code)
  const sellingPerAisle = new Map<string, number>()
  for (const item of items) if (mine.has(item.id)) sellingPerAisle.set(item.aisle, (sellingPerAisle.get(item.aisle) ?? 0) + 1)
  const ready = Object.entries(picked).filter(([, p]) => Number(p.price) > 0)

  async function addAll() {
    setBusy(true)
    try {
      const done = await api<{ created: number; skipped: number }>('/v1/products/from-catalogue', 'POST', {
        items: ready.map(([id, p]) => ({ catalogueItemId: Number(id), sellingPrice: Number(p.price), openingStock: p.stock ? Number(p.stock) : null })),
      })
      setPicked({})
      setResult(t('commonList.added', { count: done.created }))
      await queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })
    } catch (e) {
      setResult(e instanceof Error ? e.message : String(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <p className="text-sm text-muted">{t('commonList.hint')}</p>
      {result && <p className="rounded-[14px] bg-teal-50 p-3 font-semibold text-teal-900">{result}</p>}
      {aisle ? (
        <button onClick={() => setAisle(null)} className="flex min-h-11 items-center gap-1 self-start text-lg font-bold text-teal-800">
          ‹ {i18n.language === 'hi' ? aisle.name : aisle.nameEn}
        </button>
      ) : (
        <div className="rounded-[18px] bg-white p-3">
          <AisleTiles counts={sellingPerAisle} onPick={setAisle} />
        </div>
      )}

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        {shown.map((item) => {
          const sold = mine.get(item.id)
          const pick = picked[item.id]
          return (
            <ItemCard
              key={item.id}
              item={{ catalogueItemId: item.id, aisle: item.aisle }}
              title={item.name}
              subtitle={sold ? t('commonList.yourPrice', { price: formatRupees(sold.sellingPrice), unit: t(`units.${item.unit}`) }) : `${item.nameEn} · ~${formatRupees(item.typicalPrice)}/${t(`units.${item.unit}`)}`}
              badge={sold ? `✓ ${t('commonList.selling')}` : pick && Number(pick.price) > 0 ? formatRupees(Number(pick.price)) : undefined}
              selected={!!pick}
              imageUrl={sold ? imageUrl(shopId, sold.id, sold.imageAt, 'thumb') : undefined}
              onClick={sold ? () => setManaging(item) : () => setEditing(item)}
            />
          )
        })}
      </div>

      {managing && (
        <OptionsSheet
          shopId={shopId}
          item={managing}
          options={products.filter((p) => p.catalogueItemId === managing.id)}
          onClose={() => setManaging(null)}
          onChanged={() => void queryClient.invalidateQueries({ queryKey: [shopId, 'products'] })}
        />
      )}

      {editing && (
        <PriceSheet
          item={editing}
          value={picked[editing.id]}
          onClose={() => setEditing(null)}
          onSave={(pick) => {
            setPicked((current) => {
              const next = { ...current }
              if (pick) next[editing.id] = pick
              else delete next[editing.id]
              return next
            })
            setEditing(null)
          }}
        />
      )}

      {ready.length > 0 && (
        <div className="fixed inset-x-0 bottom-[68px] z-10 mx-auto max-w-xl border-t border-line bg-white px-4 py-3">
          <Button className="h-14 w-full" disabled={busy} onClick={() => void addAll()}>
            {t('commonList.addN', { count: ready.length })}
          </Button>
        </div>
      )}
    </div>
  )
}

const UNITS: Unit[] = ['packet', 'piece', 'kg', 'g', 'litre', 'ml']

/**
 * An item the shop already sells: its options (brands, pack sizes, loose), which one is the usual (given when a
 * household leaves the choice to the shop), and a form to add another option at the shop's own price.
 */
/** Price per kg or litre of a pack, so packs compare with each other and with loose ("₹49.9/kg"). */
function perBase(o: Product): { price: number; unit: Unit } | null {
  if (!o.packSize || !o.packUnit || o.sellingPrice <= 0) return null
  const [base, factor]: [Unit, number] | [null, 0] = o.packUnit === 'kg' ? ['kg', 1] : o.packUnit === 'g' ? ['kg', 0.001] : o.packUnit === 'litre' ? ['litre', 1] : o.packUnit === 'ml' ? ['litre', 0.001] : [null, 0]
  return base ? { price: Math.round((o.sellingPrice / (o.packSize * factor)) * 10) / 10, unit: base } : null
}

function OptionsSheet({ shopId, item, options, onClose, onChanged }: { shopId: string; item: CatalogueItem; options: Product[]; onClose: () => void; onChanged: () => void }) {
  const { t } = useTranslation()
  const [name, setName] = useState(`${item.nameEn} `)
  const [unit, setUnit] = useState<Unit>('packet')
  const [packSize, setPackSize] = useState('')
  const [packUnit, setPackUnit] = useState<Unit>(item.unit)
  const [price, setPrice] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [adding, setAdding] = useState(options.length === 0)
  const usual = options.find((o) => o.isDefaultOption)?.id ?? [...options].sort((a, b) => b.stock - a.stock)[0]?.id
  const usualOption = options.find((o) => o.id === usual)
  const others = options.filter((o) => o.id !== usual)
  const isPacked = (o: Product) => !!o.packSize || o.unit === 'packet' || o.unit === 'piece'
  const groups = [
    { key: 'packed', label: t('options.groupPacked'), items: others.filter(isPacked) },
    { key: 'loose', label: t('options.groupLoose'), items: others.filter((o) => !isPacked(o)) },
  ].filter((g) => g.items.length > 0)
  const packed = unit === 'packet' || unit === 'piece'
  const valid = name.trim().length >= 2 && Number(price) > 0 && (!packed || !packSize || Number(packSize) > 0)

  async function run(action: () => Promise<unknown>) {
    setError(null)
    try {
      await action()
      onChanged()
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e))
    }
  }

  const add = () => run(async () => {
    await api('/v1/products/from-catalogue', 'POST', {
      items: [{ catalogueItemId: item.id, sellingPrice: Number(price), name: name.trim(), unit, packSize: packed && packSize ? Number(packSize) : null, packUnit: packed && packSize ? packUnit : null }],
    })
    setName(`${item.nameEn} `)
    setPackSize('')
    setPrice('')
    setAdding(false)
  })

  const thumb = (o: Product, size: string) => {
    const url = imageUrl(shopId, o.id, o.imageAt, 'thumb')
    return url
      ? <img src={url} alt="" loading="lazy" className={`${size} shrink-0 rounded-[10px] bg-white object-contain`} />
      : <span aria-hidden className={`${size} shrink-0 rounded-[10px] bg-[#f3efe4]`} />
  }
  const details = (o: Product) => {
    const per = perBase(o)
    return (
      <span className="text-[13px] text-muted">
        <b className="text-ink">{formatRupees(o.sellingPrice)}</b>{o.packSize ? '' : `/${t(`units.${o.unit}`)}`}
        {per ? ` · ${formatRupees(per.price)}/${t(`units.${per.unit}`)}` : ''}
        {' · '}<span className={o.stock <= 0 ? 'font-semibold text-due' : ''}>{t('options.stockLeft', { qty: o.stock, unit: t(`units.${o.unit}`) })}</span>
      </span>
    )
  }

  return (
    <div className="fixed inset-0 z-20 flex items-end justify-center bg-black/40" onClick={onClose}>
      <div className="max-h-[90dvh] w-full max-w-xl overflow-y-auto rounded-t-3xl bg-ground p-4 pb-8" onClick={(e) => e.stopPropagation()}>
        <h2 className="text-xl font-bold">{item.name}</h2>
        <p className="mb-3 text-sm text-muted">{t('options.count', { count: options.length })}</p>

        {usualOption && (
          <button onClick={() => navigate(`/items/${usualOption.id}`)} className="mb-3 flex w-full flex-col gap-1.5 rounded-[18px] border-2 border-teal-700 bg-white p-3 text-left">
            <span className="text-xs font-bold text-teal-800">★ {t('options.usualHeading')}</span>
            <span className="flex items-center gap-3">
              {thumb(usualOption, 'h-12 w-12')}
              <span className="flex min-w-0 flex-1 flex-col">
                <span className="truncate text-[17px] font-bold">{usualOption.name}</span>
                {details(usualOption)}
              </span>
            </span>
            <span className="text-xs text-muted">{t('options.usualHint')}</span>
          </button>
        )}

        {groups.map((g) => (
          <section key={g.key} className="mb-3">
            <h3 className="mb-1.5 text-sm font-bold text-muted">{g.label}</h3>
            <ul className="overflow-hidden rounded-[18px] bg-white">
              {g.items.map((o) => (
                <li key={o.id} className="flex items-center border-b border-line last:border-b-0">
                  <button aria-label={`${t('options.makeUsual')}: ${o.name}`} onClick={() => void run(() => request(`/v1/products/${o.id}/default`, 'POST'))}
                    className="flex h-16 w-12 shrink-0 items-center justify-center">
                    <span aria-hidden className="h-5 w-5 rounded-full border-2 border-[#9aa8a5]" />
                  </button>
                  <button onClick={() => navigate(`/items/${o.id}`)} className="flex min-h-16 min-w-0 flex-1 items-center gap-3 py-2 pr-3 text-left">
                    {thumb(o, 'h-11 w-11')}
                    <span className="flex min-w-0 flex-1 flex-col">
                      <span className="truncate font-semibold">{o.name}</span>
                      {details(o)}
                    </span>
                    <span aria-hidden className="text-lg text-muted">›</span>
                  </button>
                </li>
              ))}
            </ul>
          </section>
        ))}
        {others.length > 0 && <p className="mb-3 text-center text-xs text-muted">{t('options.legend')}</p>}

        {!adding && <Button onClick={() => setAdding(true)} className="h-12 w-full">+ {t('options.addTitle')}</Button>}
        {adding && <h3 className="mt-2 mb-2 font-bold">{t('options.addTitle')}</h3>}
        {adding && <div className="flex flex-col gap-2 rounded-[14px] bg-white p-3">
          <input value={name} onChange={(e) => setName(e.target.value)} maxLength={120} placeholder={t('options.namePlaceholder')}
            className="h-12 rounded-xl border border-line px-3 outline-none focus:border-teal-600" />
          <div className="flex flex-wrap gap-1.5">
            {UNITS.map((u) => (
              <button key={u} onClick={() => setUnit(u)} aria-pressed={unit === u}
                className={`min-h-10 rounded-full px-3 text-sm font-semibold ${unit === u ? 'bg-teal-700 text-white' : 'bg-[#edf2f1]'}`}>
                {t('options.soldPer', { unit: t(`units.${u}`) })}
              </button>
            ))}
          </div>
          {packed && (
            <label className="flex items-center gap-2 text-sm">
              {t('options.onePackHolds')}
              <input value={packSize} onChange={(e) => setPackSize(e.target.value.replace(/[^\d.]/g, ''))} inputMode="decimal" className="h-10 w-20 rounded-lg border border-line px-2 font-bold" />
              <select value={packUnit} onChange={(e) => setPackUnit(e.target.value as Unit)} className="h-10 rounded-lg border border-line bg-white px-2">
                {(['kg', 'g', 'litre', 'ml', 'piece'] as Unit[]).map((u) => <option key={u} value={u}>{t(`units.${u}`)}</option>)}
              </select>
            </label>
          )}
          <label className="flex items-center gap-2 text-sm font-semibold">
            {t('options.yourPricePer', { unit: t(`units.${unit}`) })}
            <input value={price} onChange={(e) => setPrice(e.target.value.replace(/[^\d.]/g, ''))} inputMode="decimal" placeholder="₹" className="h-11 w-28 rounded-lg border border-line px-2 text-lg font-bold" />
          </label>
          {error && <p className="text-sm text-due">{error}</p>}
          <Button disabled={!valid} onClick={() => void add()} className="h-12">{t('options.add')}</Button>
        </div>}
      </div>
    </div>
  )
}

function PriceSheet({ item, value, onClose, onSave }: { item: CatalogueItem; value?: Pick; onClose: () => void; onSave: (pick: Pick | null) => void }) {
  const { t } = useTranslation()
  const [price, setPrice] = useState(value?.price ?? '')
  const [stock, setStock] = useState(value?.stock ?? '')
  const unit = t(`units.${item.unit}`)
  return (
    <div className="fixed inset-0 z-20 flex items-end justify-center bg-black/40" onClick={onClose}>
      <div className="w-full max-w-xl rounded-t-3xl bg-ground p-4 pb-8" onClick={(e) => e.stopPropagation()}>
        <div className="mb-3 flex items-center gap-3">
          <div className="w-24 shrink-0">
            <ItemCard item={{ catalogueItemId: item.id, aisle: item.aisle }} title={item.name} compact />
          </div>
          <div>
            <h2 className="text-xl font-bold">{item.name}</h2>
            <p className="text-sm text-muted">{t('commonList.typical', { price: formatRupees(item.typicalPrice), unit })}</p>
          </div>
        </div>
        <label className="block">
          <span className="mb-1 block font-semibold">{t('commonList.priceLabel', { unit })}</span>
          <input autoFocus value={price} onChange={(e) => setPrice(e.target.value.replace(/[^\d.]/g, ''))} inputMode="decimal" placeholder={String(item.typicalPrice)}
            className="h-14 w-full rounded-[14px] border border-line bg-white px-4 text-2xl font-bold outline-none focus:border-teal-600" />
        </label>
        <label className="mt-3 block">
          <span className="mb-1 block text-sm font-semibold text-muted">{t('commonList.stockLabel', { unit })}</span>
          <input value={stock} onChange={(e) => setStock(e.target.value.replace(/[^\d.]/g, ''))} inputMode="decimal"
            className="h-12 w-full rounded-[14px] border border-line bg-white px-4 text-lg outline-none focus:border-teal-600" />
        </label>
        <div className="mt-4 grid grid-cols-2 gap-2">
          <Button tone="plain" className="h-12" onClick={() => onSave(null)}>{value ? t('remove') : t('cancel')}</Button>
          <Button className="h-12" disabled={!(Number(price) > 0)} onClick={() => onSave({ price, stock })}>{t('commonList.choose')}</Button>
        </div>
      </div>
    </div>
  )
}

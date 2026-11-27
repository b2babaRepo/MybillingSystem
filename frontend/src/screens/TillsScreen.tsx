import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { btn, controlSm } from '../components/ui'
import { shownLocale } from '../i18n'
import { api } from '../lib/api'
import { setCounterName } from '../lib/data'
import { uuidv7 } from '../lib/ids'
import { setThisTill, thisTill, type Till } from '../lib/terminal'

interface Terminal extends Till { mallMachineId: string | null; isActive: boolean; registeredAt: string; lastBillAt: string | null }

/**
 * The shop's registered tills: each a real device record (T01, T02…) whose bills carry it, with the machine ID a
 * mall may ask for. Register this device as a till, rename one, or turn one off (a lost or retired device).
 */
export function TillsScreen({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const list = useQuery({ queryKey: [shopId, 'terminals'], queryFn: () => api<Terminal[]>('/v1/terminals'), retry: false })
  const [mine, setMine] = useState(() => thisTill(shopId))
  const [name, setName] = useState('')
  const [message, setMessage] = useState('')
  const [editing, setEditing] = useState<Terminal | null>(null)

  async function run(work: () => Promise<void>) {
    setMessage('')
    try {
      await work()
      await list.refetch()
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }
  const register = () => run(async () => {
    const till = await api<Terminal>('/v1/terminals', 'POST', { id: uuidv7(), name: name.trim() })
    setThisTill(shopId, till)
    setCounterName(till.name)
    setMine(till)
    setName('')
  })
  const save = (till: Terminal) => run(async () => {
    await api(`/v1/terminals/${till.id}`, 'PUT', { name: till.name.trim(), mallMachineId: till.mallMachineId?.trim() || null, isActive: till.isActive })
    if (mine?.id === till.id) { setThisTill(shopId, till); setMine(till) }
    setEditing(null)
  })

  return (
    <div className="flex flex-col gap-3 p-4">
      <p className="text-sm text-muted">{t('tills.hint')}</p>
      <section className="flex flex-col gap-2 rounded-card bg-white p-4">
        {mine ? (
          <p className="font-semibold">{t('tills.thisIs', { code: mine.code, name: mine.name })}</p>
        ) : (
          <>
            <p className="font-semibold">{t('tills.register')}</p>
            <div className="flex gap-2">
              <input value={name} onChange={(e) => setName(e.target.value)} maxLength={30} placeholder="Counter 1" aria-label={t('tills.name')} className={`${controlSm} flex-1`} />
              <button type="button" disabled={!name.trim()} onClick={() => void register()} className={`min-h-11 rounded-xl px-4 ${btn.solid}`}>{t('tills.registerButton')}</button>
            </div>
          </>
        )}
      </section>
      {message && <p className="text-sm text-due">{message}</p>}
      <ul className="flex flex-col gap-2">
        {list.data?.map((till) => (
          <li key={till.id} className="flex flex-col gap-2 rounded-card bg-white p-3 text-sm">
            {editing?.id === till.id ? (
              <>
                <input value={editing.name} onChange={(e) => setEditing({ ...editing, name: e.target.value })} maxLength={30} aria-label={t('tills.name')} className={controlSm} />
                <input value={editing.mallMachineId ?? ''} onChange={(e) => setEditing({ ...editing, mallMachineId: e.target.value })} maxLength={40} placeholder={t('tills.mallId')} aria-label={t('tills.mallId')} className={controlSm} />
                <label className="flex min-h-11 items-center gap-2"><input type="checkbox" checked={editing.isActive} onChange={(e) => setEditing({ ...editing, isActive: e.target.checked })} className="h-5 w-5" />{t('tills.active')}</label>
                <div className="flex gap-2">
                  <button type="button" onClick={() => void save(editing)} className={`min-h-11 rounded-xl px-4 ${btn.solid}`}>{t('save')}</button>
                  <button type="button" onClick={() => setEditing(null)} className={`min-h-11 rounded-xl px-4 ${btn.outline}`}>{t('cancel')}</button>
                </div>
              </>
            ) : (
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span><b>{till.code}</b> · {till.name}{till.mallMachineId ? ` · ${t('tills.mallId')}: ${till.mallMachineId}` : ''}{mine?.id === till.id ? ` · ${t('tills.thisDevice')}` : ''}</span>
                <span className={till.isActive ? 'text-muted' : 'font-semibold text-due'}>
                  {till.isActive ? (till.lastBillAt ? t('tills.lastBill', { when: new Date(till.lastBillAt).toLocaleString(shownLocale(), { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' }) }) : t('tills.noBills')) : t('tills.off')}
                </span>
                <button type="button" onClick={() => setEditing(till)} className={`min-h-9 rounded-lg px-3 ${btn.outline}`}>{t('tills.edit')}</button>
              </div>
            )}
          </li>
        ))}
      </ul>
    </div>
  )
}

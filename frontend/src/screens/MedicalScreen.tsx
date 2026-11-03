import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Field } from '../components/ui'
import { api } from '../lib/api'
import { navigate } from '../router'

interface Profile {
  licenceForm20: string | null; licenceForm21: string | null; licenceIssuedOn: string | null; pharmacistName: string | null; pharmacistRegNo: string | null
  gstin: string | null; fssaiNo: string | null; retentionDueOn: string | null; retentionSoon: boolean; complete: boolean
}

const day = (iso: string) => new Date(`${iso}T00:00`).toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' })
const useProfile = (shopId: string) => useQuery({ queryKey: [shopId, 'pharmacy-profile'], queryFn: () => api<Profile>('/v1/pharmacy/profile') })

/** On a medical shop's home: what setup is left, and the licence retention fee when it is near. */
export function MedicalHomeCard({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const profile = useProfile(shopId).data
  const row = 'flex min-h-12 w-full items-center justify-between rounded-xl px-1 text-left font-semibold'
  return (
    <section className="mx-4 mt-3 flex flex-col gap-1 rounded-[18px] bg-white p-3">
      <h3 className="font-bold">{t('medical.homeTitle')}</h3>
      {profile && !profile.complete && <p className="rounded-xl bg-amber-50 p-2 text-sm">{t('medical.finishSetup')}</p>}
      {profile?.retentionSoon && profile.retentionDueOn && <p className="rounded-xl bg-red-50 p-2 text-sm font-semibold text-due">{t('medical.retentionDue', { date: day(profile.retentionDueOn) })}</p>}
      <button onClick={() => navigate('/medical/stock')} className={row}><span>{t('batches.link')}</span><span aria-hidden="true">→</span></button>
      <button onClick={() => navigate('/medical/h1')} className={row}><span>{t('rx.register')}</span><span aria-hidden="true">→</span></button>
      <button onClick={() => navigate('/medical/setup')} className={row}><span>{t('medical.setupLink')}</span><span aria-hidden="true">→</span></button>
    </section>
  )
}

/** A new medical shop's first steps: licences and pharmacist (shown on bills), then its common medicines. */
export function MedicalSetupScreen({ shopId, isOwner }: { shopId: string; isOwner: boolean }) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const profile = useProfile(shopId).data
  const [d, setD] = useState({ licenceForm20: '', licenceForm21: '', licenceIssuedOn: '', pharmacistName: '', pharmacistRegNo: '', gstin: '', fssaiNo: '' })
  const [message, setMessage] = useState('')
  useEffect(() => {
    if (profile) setD({
      licenceForm20: profile.licenceForm20 ?? '', licenceForm21: profile.licenceForm21 ?? '', licenceIssuedOn: profile.licenceIssuedOn ?? '', pharmacistName: profile.pharmacistName ?? '',
      pharmacistRegNo: profile.pharmacistRegNo ?? '', gstin: profile.gstin ?? '', fssaiNo: profile.fssaiNo ?? '',
    })
  }, [profile])
  const set = (key: keyof typeof d) => (e: { target: { value: string } }) => setD({ ...d, [key]: e.target.value })

  async function run(work: () => Promise<unknown>, done: string) {
    setMessage('')
    try {
      await work()
      setMessage(done)
      void queryClient.invalidateQueries({ queryKey: [shopId] })
    } catch (e) {
      setMessage(e instanceof Error ? e.message : t('error'))
    }
  }

  return (
    <div className="flex flex-col gap-3 p-4 pb-28">
      <h2 className="text-lg font-bold">{t('medical.setupTitle')}</h2>
      {message && <p className="rounded-xl bg-teal-50 p-3 text-sm font-semibold text-teal-900">{message}</p>}

      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
        <h3 className="font-bold">1 · {t('medical.licences')}</h3>
        <p className="text-xs text-muted">{t('medical.licencesHint')}</p>
        <div className="grid grid-cols-2 gap-2">
          <Field label={t('medical.form20')} value={d.licenceForm20} onChange={set('licenceForm20')} disabled={!isOwner} />
          <Field label={t('medical.form21')} value={d.licenceForm21} onChange={set('licenceForm21')} disabled={!isOwner} />
        </div>
        <Field label={t('medical.issuedOn')} type="date" value={d.licenceIssuedOn} onChange={set('licenceIssuedOn')} disabled={!isOwner} />
        {profile?.retentionDueOn && <p className={`text-sm ${profile.retentionSoon ? 'font-semibold text-due' : 'text-muted'}`}>{t('medical.retentionNext', { date: day(profile.retentionDueOn) })}</p>}
        <div className="grid grid-cols-2 gap-2">
          <Field label={t('medical.pharmacist')} value={d.pharmacistName} onChange={set('pharmacistName')} disabled={!isOwner} />
          <Field label={t('medical.regNo')} value={d.pharmacistRegNo} onChange={set('pharmacistRegNo')} disabled={!isOwner} />
        </div>
        <div className="grid grid-cols-2 gap-2">
          <Field label="GSTIN" value={d.gstin} onChange={set('gstin')} disabled={!isOwner} />
          <Field label={t('medical.fssai')} value={d.fssaiNo} onChange={set('fssaiNo')} disabled={!isOwner} />
        </div>
        {isOwner && <Button onClick={() => void run(() => api('/v1/pharmacy/profile', 'PUT', { ...d, licenceIssuedOn: d.licenceIssuedOn || null }), t('saved'))}>{t('save')}</Button>}
      </section>

      <section className="flex flex-col gap-2 rounded-[18px] bg-white p-3">
        <h3 className="font-bold">2 · {t('medical.medicines')}</h3>
        <p className="text-xs text-muted">{t('medical.medicinesHint')}</p>
        {isOwner && <Button tone="plain" onClick={() => void run(() => api('/v1/products/starter', 'POST', {}), t('medical.added'))}>{t('medical.addCommon')}</Button>}
        <Button tone="plain" onClick={() => navigate('/items')}>{t('medical.openItems')}</Button>
      </section>
    </div>
  )
}

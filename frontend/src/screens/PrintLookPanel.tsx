import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type ChangeEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, btn } from '../components/ui'
import { api } from '../lib/api'
import type { BillPrint, PrintTheme } from '../lib/types'

/** A picture shrunk on the phone to fit `max` px and kept as PNG (clear for logos and signatures, transparent where it was). */
async function shrink(file: File, max = 320): Promise<string> {
  const bitmap = await createImageBitmap(file)
  const scale = Math.min(1, max / Math.max(bitmap.width, bitmap.height))
  const canvas = document.createElement('canvas')
  canvas.width = Math.round(bitmap.width * scale)
  canvas.height = Math.round(bitmap.height * scale)
  canvas.getContext('2d')!.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
  const png = canvas.toDataURL('image/png')
  // A photo of a signature on paper can be large as PNG: WebP then, which stays small.
  return png.length * 0.75 < 55_000 ? png : canvas.toDataURL('image/webp', 0.85)
}

/**
 * How bills look on paper: the shop's logo, the owner's signature, a style and terms printed at the foot. Owner only.
 * Shows on every A4 bill and on the bill link customers open.
 */
export function PrintLookPanel() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const current = useQuery({ queryKey: ['print-look'], queryFn: () => api<BillPrint>('/v1/shop/print') })
  if (!current.data) return null
  return <PrintLookForm initial={current.data} onSaved={() => { void queryClient.invalidateQueries({ queryKey: ['print-look'] }); void queryClient.invalidateQueries({ queryKey: ['print'] }) }} t={t} />
}

function PrintLookForm({ initial, onSaved, t }: { initial: BillPrint; onSaved: () => void; t: (key: string) => string }) {
  const [logo, setLogo] = useState(initial.logo)
  const [signature, setSignature] = useState(initial.signature)
  const [theme, setTheme] = useState<PrintTheme>(initial.theme)
  const [terms, setTerms] = useState(initial.terms ?? '')
  const [message, setMessage] = useState('')

  const pick = (set: (v: string) => void) => async (e: ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    e.target.value = ''
    if (file) set(await shrink(file))
  }

  async function save() {
    setMessage('')
    try {
      await api('/v1/shop/print', 'PUT', { logo, signature, theme, terms })
      setMessage(t('printLook.saved'))
      onSaved()
    } catch (err) {
      setMessage(err instanceof Error ? err.message : t('error'))
    }
  }

  const picture = (label: string, value: string | null, set: (v: string | null) => void) => (
    <div className="flex items-center gap-3">
      <div className="flex h-16 w-24 shrink-0 items-center justify-center rounded-xl border border-dashed border-line bg-white">
        {value ? <img src={value} alt={label} className="max-h-14 max-w-22 object-contain" /> : <span className="text-xs text-muted">{t('printLook.none')}</span>}
      </div>
      <div className="flex flex-1 flex-wrap gap-2">
        <label className="flex min-h-11 cursor-pointer items-center rounded-xl border border-line bg-white px-3 text-sm font-semibold">
          {label}<input type="file" accept="image/*" className="sr-only" onChange={(e) => void pick((v) => set(v))(e)} />
        </label>
        {value && <button onClick={() => set(null)} className={`min-h-11 px-3 text-sm ${btn.dangerOutline}`}>{t('printLook.remove')}</button>}
      </div>
    </div>
  )

  return (
    <section className="mt-4 flex flex-col gap-3 rounded-card bg-white p-4">
      <h2 className="text-lg font-bold">{t('printLook.title')}</h2>
      <p className="text-sm text-muted">{t('printLook.hint')}</p>
      {picture(t('printLook.logo'), logo, setLogo)}
      {picture(t('printLook.signature'), signature, setSignature)}
      <fieldset className="flex flex-col gap-1">
        <legend className="mb-1 text-sm font-semibold text-muted">{t('printLook.theme')}</legend>
        {(['classic', 'modern', 'compact'] as const).map((k) => (
          <label key={k} className="flex min-h-11 items-center gap-2"><input type="radio" name="print-theme" checked={theme === k} onChange={() => setTheme(k)} className="h-5 w-5" />
            <span><b>{t(`printLook.themes.${k}`)}</b> <span className="text-sm text-muted">{t(`printLook.themeHint.${k}`)}</span></span></label>
        ))}
      </fieldset>
      <label className="block">
        <span className="mb-1 block text-sm font-semibold text-muted">{t('printLook.terms')}</span>
        <textarea value={terms} onChange={(e) => setTerms(e.target.value)} rows={3} maxLength={400} placeholder={t('printLook.termsPlaceholder')}
          className="w-full rounded-[14px] border-[1.5px] border-line bg-white p-3 text-base outline-none transition focus:border-brand-line focus:ring-4 focus:ring-brand-line/10" />
      </label>
      <Button onClick={() => void save()}>{t('save')}</Button>
      {message && <p role="status" className="text-sm font-semibold">{message}</p>}
    </section>
  )
}

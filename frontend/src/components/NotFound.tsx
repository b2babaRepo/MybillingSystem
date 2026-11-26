import { useTranslation } from 'react-i18next'
import { navigate } from '../router'
import { btn } from '../lib/buttonStyles'

/** A link to something that isn't there (a wrong id, a bad month): say so and offer the way back. */
export function NotFound({ text, back }: { text: string; back: string }) {
  const { t } = useTranslation()
  return (
    <div className="flex flex-col items-start gap-3 p-6">
      <p role="alert" className="font-semibold text-due">{text}</p>
      <button onClick={() => navigate(back)} className={`min-h-11 rounded-xl px-5 ${btn.outline}`}>{t('hwp2.back')}</button>
    </div>
  )
}

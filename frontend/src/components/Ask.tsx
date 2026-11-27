import { btn } from '../lib/buttonStyles'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { setAskHost, type Question } from '../lib/ask'

/** Shows the questions from lib/ask (ask, confirmAsk, askSecret, tell). One at the root of the page. */
export function AskHost() {
  const { t } = useTranslation()
  const [question, setQuestion] = useState<Question | null>(null)
  const [text, setText] = useState('')
  const input = useRef<HTMLInputElement>(null)

  useEffect(() => {
    setAskHost((q) => { setText(q.value ?? ''); setQuestion(q) })
    return () => setAskHost(null)
  }, [])
  useEffect(() => { if (question?.kind === 'text' || question?.kind === 'secret') input.current?.focus() }, [question])

  if (!question) return null
  const finish = (answer: string | null) => { question.done(answer); setQuestion(null) }
  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center bg-black/40 sm:items-center" onClick={() => finish(null)}>
      <form role="dialog" aria-modal="true" aria-label={question.message} onClick={(e) => e.stopPropagation()}
        onSubmit={(e) => { e.preventDefault(); finish(question.kind === 'confirm' ? 'yes' : text) }}
        className="w-full max-w-md rounded-t-3xl bg-white p-5 pb-[calc(1.25rem+env(safe-area-inset-bottom))] sm:rounded-3xl">
        <p className="whitespace-pre-line text-[17px] font-semibold leading-snug">{question.message}</p>
        {question.kind === 'text' && (
          <input ref={input} value={text} onChange={(e) => setText(e.target.value)} className="mt-3 h-12 w-full rounded-xl border border-line px-3 text-[17px] outline-none focus:border-brand-line" />
        )}
        {question.kind === 'secret' && (
          <input ref={input} type="password" inputMode="numeric" autoComplete="off" maxLength={6} value={text} aria-label={question.message}
            onChange={(e) => setText(e.target.value.replace(/\D/g, ''))} className="mt-3 h-14 w-full rounded-xl border border-line px-3 text-center text-2xl tracking-[0.5em] outline-none focus:border-brand-line" />
        )}
        <div className={`mt-4 grid gap-2 ${question.kind === 'tell' ? 'grid-cols-1' : 'grid-cols-2'}`}>
          {question.kind !== 'tell' && <button type="button" onClick={() => finish(null)} className="h-12 rounded-xl border border-line font-semibold">{t('cancel')}</button>}
          <button type="submit" className={`h-12 rounded-xl ${btn.solid}`}>{question.kind === 'confirm' ? t('ask.yes') : t('ask.ok')}</button>
        </div>
      </form>
    </div>
  )
}

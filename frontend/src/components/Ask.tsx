import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'

/**
 * The app's own question box, in place of the browser's prompt() and confirm(): some phone and in-app browsers
 * refuse those pop-ups ("prompt() is not supported"), which silently stopped whatever asked. Call
 * `await ask('Why?')` for a typed answer (null when cancelled), `await confirmAsk('Delete it?')` for yes/no, or
 * `tell('Saved')` for a message with just OK (in place of alert()).
 * One <AskHost /> at the root of the page shows them.
 */
interface Question { message: string; value?: string; kind: 'text' | 'confirm' | 'tell'; done: (answer: string | null) => void }

let show: ((q: Question) => void) | null = null

export function ask(message: string, value?: string): Promise<string | null> {
  return new Promise((done) => (show ? show({ message, value, kind: 'text', done }) : done(null)))
}

export function confirmAsk(message: string): Promise<boolean> {
  return new Promise((done) => (show ? show({ message, kind: 'confirm', done: (a) => done(a !== null) }) : done(false)))
}

/** A message with just OK, in place of the browser's alert(). */
export function tell(message: string): Promise<void> {
  return new Promise((done) => (show ? show({ message, kind: 'tell', done: () => done() }) : done()))
}

export function AskHost() {
  const { t } = useTranslation()
  const [question, setQuestion] = useState<Question | null>(null)
  const [text, setText] = useState('')
  const input = useRef<HTMLInputElement>(null)

  useEffect(() => {
    show = (q) => { setText(q.value ?? ''); setQuestion(q) }
    return () => { show = null }
  }, [])
  useEffect(() => { if (question?.kind === 'text') input.current?.focus() }, [question])

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
        <div className={`mt-4 grid gap-2 ${question.kind === 'tell' ? 'grid-cols-1' : 'grid-cols-2'}`}>
          {question.kind !== 'tell' && <button type="button" onClick={() => finish(null)} className="h-12 rounded-xl border border-line font-semibold">{t('cancel')}</button>}
          <button type="submit" className="h-12 rounded-xl bg-brand font-bold text-white">{question.kind === 'confirm' ? t('ask.yes') : t('ask.ok')}</button>
        </div>
      </form>
    </div>
  )
}

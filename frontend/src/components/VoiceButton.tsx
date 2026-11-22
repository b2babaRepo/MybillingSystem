import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { request } from '../lib/api'
import { CustIcon } from './CustIcons'

/** Voice notes stop by themselves here: the speech service is meant for clips under 30 seconds. */
const MAX_SECONDS = 25

/**
 * Tap to speak, tap again to stop (it stops by itself at 25 seconds). The recording goes to the server once, is
 * turned into words there and is not kept; the words are added to the note box for the person to check (a grocery list, or a kharcha line).
 */
export function VoiceButton({ onText, onError }: { onText: (text: string) => void; onError: (message: string | null) => void }) {
  const { t } = useTranslation()
  const [recording, setRecording] = useState(false)
  const [seconds, setSeconds] = useState(0)
  const [sending, setSending] = useState(false)
  const recorder = useRef<MediaRecorder | null>(null)

  useEffect(() => {
    if (!recording) return
    const timer = setInterval(() => setSeconds((s) => {
      if (s + 1 >= MAX_SECONDS) recorder.current?.stop()
      return s + 1
    }), 1000)
    return () => clearInterval(timer)
  }, [recording])

  useEffect(() => () => recorder.current?.stream.getTracks().forEach((track) => track.stop()), [])

  async function start() {
    onError(null)
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
      const type = ['audio/webm;codecs=opus', 'audio/ogg;codecs=opus', 'audio/webm'].find((m) => MediaRecorder.isTypeSupported(m)) ?? ''
      const rec = new MediaRecorder(stream, type ? { mimeType: type, audioBitsPerSecond: 24_000 } : undefined)
      const chunks: Blob[] = []
      rec.ondataavailable = (e) => e.data.size > 0 && chunks.push(e.data)
      rec.onstop = async () => {
        stream.getTracks().forEach((track) => track.stop())
        setRecording(false)
        const blob = new Blob(chunks, { type: rec.mimeType || 'audio/webm' })
        if (blob.size === 0) return
        setSending(true)
        try {
          const form = new FormData()
          form.append('audio', blob, blob.type.includes('ogg') ? 'order.ogg' : 'order.webm')
          const response = await request('/v1/me/orders/transcribe', 'POST', form)
          const body = (await response.json()) as { transcript?: string; title?: string; errors?: Record<string, string[]> }
          if (!response.ok) throw new Error(body.errors ? Object.values(body.errors)[0][0] : (body.title ?? t('error')))
          if (body.transcript) onText(body.transcript)
          else onError(t('ai.heardNothing'))
        } catch (e) {
          onError(e instanceof Error ? e.message : String(e))
        } finally {
          setSending(false)
        }
      }
      recorder.current = rec
      setSeconds(0)
      setRecording(true)
      rec.start()
    } catch {
      onError(t('ai.micBlocked'))
    }
  }

  return (
    <button
      disabled={sending}
      onClick={() => (recording ? recorder.current?.stop() : void start())}
      className={`flex min-h-14 items-center justify-center gap-2 rounded-2xl font-bold disabled:opacity-50 ${
        recording ? 'animate-pulse bg-red-600 text-white' : 'bg-gradient-to-r from-rose-500 to-orange-400 text-white'
      }`}
    >
      <CustIcon name="mic" size={20} /> {sending ? t('ai.hearing') : recording ? t('ai.stop', { seconds: MAX_SECONDS - seconds }) : t('ai.speak')}
    </button>
  )
}

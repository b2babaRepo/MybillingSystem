import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Sheet } from './ui'

const FORMATS = ['ean_13', 'ean_8', 'upc_a', 'upc_e', 'code_128', 'qr_code']

type Detector = { detect: (source: HTMLVideoElement) => Promise<{ rawValue: string }[]> }

/**
 * The phone's own barcode reader where it has one (Android Chrome); everywhere else (iPhone, Windows, Firefox) the
 * backup reader, loaded only when first needed. Its WebAssembly file ships with the app, so it scans offline too.
 */
async function makeDetector(): Promise<Detector> {
  const native = (window as unknown as { BarcodeDetector?: { new (o: { formats: string[] }): Detector; getSupportedFormats?: () => Promise<string[]> } }).BarcodeDetector
  if (native) {
    const supported = await native.getSupportedFormats?.().catch(() => [] as string[]) ?? []
    if (supported.includes('ean_13')) return new native({ formats: FORMATS.filter((f) => supported.includes(f)) })
  }
  const [{ BarcodeDetector, prepareZXingModule }, { default: wasmUrl }] = await Promise.all([
    import('barcode-detector/ponyfill'),
    import('zxing-wasm/reader/zxing_reader.wasm?url'),
  ])
  prepareZXingModule({ overrides: { locateFile: (path: string, prefix: string) => (path.endsWith('.wasm') ? wasmUrl : prefix + path) } })
  return new BarcodeDetector({ formats: FORMATS as never })
}

/** A short beep and a buzz (where the phone allows), so the counter knows the item went in without looking. */
function signal(ok: boolean) {
  try {
    const audio = new AudioContext()
    const tone = audio.createOscillator()
    const gain = audio.createGain()
    tone.frequency.value = ok ? 1500 : 400
    gain.gain.value = 0.15
    tone.connect(gain).connect(audio.destination)
    tone.start()
    tone.stop(audio.currentTime + (ok ? 0.08 : 0.25))
    tone.onended = () => void audio.close()
  } catch { /* no sound: the screen still shows it */ }
  try { navigator.vibrate?.(ok ? 60 : [80, 60, 80]) } catch { /* iPhone can't vibrate from a web page */ }
}

/**
 * Reads barcodes with the back camera. One code (closes after it), or many in a row for billing: the camera stays
 * open, each new code beeps, and the same code is ignored for two seconds so one pack isn't added twice.
 * `onCode` says whether the code was known, for the beep and the line under the picture.
 */
export function ScanSheet({ onCode, onClose, continuous = false }: { onCode: (code: string) => boolean | void; onClose: () => void; continuous?: boolean }) {
  const { t } = useTranslation()
  const video = useRef<HTMLVideoElement>(null)
  const handler = useRef(onCode)
  handler.current = onCode
  const [error, setError] = useState('')
  const [last, setLast] = useState<{ code: string; ok: boolean } | null>(null)
  const [torch, setTorch] = useState<{ on: boolean; track: MediaStreamTrack } | null>(null)

  useEffect(() => {
    let stream: MediaStream | null = null
    let stopped = false
    let timer = 0
    const seen = new Map<string, number>()
    ;(async () => {
      if (!window.isSecureContext) { setError(t('scan.needsHttps')); return }
      try {
        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: 'environment', width: { ideal: 1280 }, height: { ideal: 720 } } })
      } catch (e) {
        const name = e instanceof DOMException ? e.name : ''
        setError(t(name === 'NotAllowedError' ? 'scan.denied' : name === 'NotFoundError' || name === 'OverconstrainedError' ? 'scan.noCamera' : 'scan.failed'))
        return
      }
      if (stopped || !video.current) { stream.getTracks().forEach((track) => track.stop()); return }
      const track = stream.getVideoTracks()[0]
      const caps = (track.getCapabilities?.() ?? {}) as { torch?: boolean }
      if (caps.torch) setTorch({ on: false, track })
      video.current.srcObject = stream
      await video.current.play().catch(() => undefined)
      let detector: Detector
      try {
        detector = await makeDetector()
      } catch {
        setError(t('scan.failed'))
        return
      }
      // One read at a time: a slow phone never stacks reads up.
      const tick = async () => {
        if (stopped || !video.current) return
        if (video.current.readyState >= 2) {
          const found = await detector.detect(video.current).catch(() => [])
          const code = found[0]?.rawValue?.trim()
          const now = Date.now()
          if (code && !stopped && now - (seen.get(code) ?? 0) > 2000) {
            seen.set(code, now)
            const ok = handler.current(code) !== false
            signal(ok)
            setLast({ code, ok })
            if (!continuous) { stopped = true; return }
          }
        }
        timer = window.setTimeout(() => void tick(), 150)
      }
      void tick()
    })()
    return () => { stopped = true; window.clearTimeout(timer); stream?.getTracks().forEach((track) => track.stop()) }
  }, [continuous, t])

  async function toggleTorch() {
    if (!torch) return
    try {
      await torch.track.applyConstraints({ advanced: [{ torch: !torch.on } as MediaTrackConstraintSet] })
      setTorch({ ...torch, on: !torch.on })
    } catch { setTorch(null) }
  }

  return (
    <Sheet title={t('barcodeBill.scan')} onClose={onClose}>
      {error ? (
        <p className="rounded-xl bg-due-soft p-3 text-sm text-due">{error}</p>
      ) : (
        <div className="flex flex-col gap-2">
          <div className="relative">
            <video ref={video} muted playsInline className="w-full rounded-xl bg-black" />
            <div className="pointer-events-none absolute inset-x-[12%] top-1/2 h-0.5 -translate-y-1/2 bg-red-500/80" />
          </div>
          <p className="text-sm text-muted">{continuous ? t('scan.keepGoing') : t('scan.hint')}</p>
          {last && <p className={`text-sm font-semibold ${last.ok ? 'text-brand-strong' : 'text-due'}`}>{last.ok ? t('scan.added', { code: last.code }) : t('scan.unknown', { code: last.code })}</p>}
          <div className="flex gap-2">
            {torch && <button type="button" onClick={() => void toggleTorch()} className="min-h-11 flex-1 rounded-xl border border-line font-semibold">{torch.on ? t('scan.torchOff') : t('scan.torchOn')}</button>}
            {continuous && <button type="button" onClick={onClose} className="min-h-11 flex-1 rounded-xl bg-brand font-semibold text-white">{t('scan.done')}</button>}
          </div>
        </div>
      )}
    </Sheet>
  )
}

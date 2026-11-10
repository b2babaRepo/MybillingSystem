import { useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { whatsAppLink } from '../lib/format'
import { getPaper, pageRule, setPaper, type Paper } from '../lib/paper'
import type { ReceiptRow } from '../lib/escpos'

/** A print page: back, print, WhatsApp and the paper choice (A4, 80 mm, 58 mm roll), then the shop's header and the content. */
export function Page({ title, subtitle, shareText, phone, children, receipt }: { title: string; subtitle: string; shareText: string; phone: string | null; children: ReactNode; receipt?: () => ReceiptRow[] }) {
  const { t } = useTranslation()
  const [btState, setBtState] = useState<string | null>(null)
  // Straight to a Bluetooth thermal printer, where the browser can (Chrome on Android and computers; not iPhone).
  const bt = !!receipt && typeof navigator !== 'undefined' && 'bluetooth' in navigator
  async function btPrint(choose: boolean) {
    setBtState(t('btPrint.sending'))
    try {
      const { bluetoothPrint } = await import('../lib/btPrinter')
      const name = await bluetoothPrint(receipt!(), paper === '58' ? '58' : '80', choose)
      setBtState(t('btPrint.done', { name }))
    } catch (e) {
      // Closing the printer list isn't a problem worth showing.
      setBtState(e instanceof Error && e.name === 'NotFoundError' ? null : t('btPrint.failed', { error: e instanceof Error ? e.message : String(e) }))
    }
  }
  const [paper, choosePaper] = useState<Paper>(getPaper)
  // How long the printed part is: the shop-only buttons below it don't print, so they don't count.
  const roll = useRef<HTMLDivElement>(null)
  const [lengthMm, setLengthMm] = useState(297)
  useEffect(() => {
    const box = roll.current
    if (!box || paper === 'a4') return
    const measure = () => {
      const printed = [...box.children].filter((c) => !c.classList.contains('print:hidden'))
      const bottom = Math.max(...printed.map((c) => c.getBoundingClientRect().bottom))
      setLengthMm(((bottom - box.getBoundingClientRect().top) / 96) * 25.4 + 8)
    }
    measure()
    const watch = new ResizeObserver(measure)
    watch.observe(box)
    return () => watch.disconnect()
  }, [paper])
  const rule = pageRule(paper, lengthMm)
  // Opened with "Print" from the bill screen: show the print dialog straight away, once.
  useEffect(() => {
    let auto = false
    try { auto = sessionStorage.getItem('gk-autoprint') === '1'; sessionStorage.removeItem('gk-autoprint') } catch { /* no storage */ }
    if (auto) setTimeout(() => window.print(), 300)
  }, [])
  return (
    <div className="mx-auto min-h-dvh max-w-2xl bg-white p-6 text-black print:m-0 print:min-h-0 print:max-w-none print:p-0">
      {rule && <style>{rule}</style>}
      <div className="mb-4 flex flex-wrap gap-2 print:hidden">
        <button onClick={() => history.back()} className="h-11 rounded-xl border border-line px-4 font-semibold" aria-label={t('a11y.back')}>←</button>
        <button onClick={() => window.print()} className="h-11 rounded-xl bg-teal-700 px-4 font-semibold text-white">{t('print.printPdf')}</button>
        <a href={whatsAppLink(phone, shareText)} target="_blank" rel="noreferrer" className="flex h-11 items-center rounded-xl bg-[#15803d] px-4 font-semibold text-white">{t('print.share')}</a>
        {bt && <button onClick={() => void btPrint(false)} className="h-11 rounded-xl bg-slate-800 px-4 font-semibold text-white">{t('btPrint.button')}</button>}
        <select aria-label={t('paper.label')} value={paper} onChange={(e) => { const next = e.target.value as Paper; setPaper(next); choosePaper(next) }} className="h-11 rounded-xl border border-line bg-white px-2 font-semibold">
          <option value="a4">{t('paper.a4')}</option>
          <option value="80">{t('paper.r80')}</option>
          <option value="58">{t('paper.r58')}</option>
        </select>
      </div>
      {bt && btState && (
        <p role="status" className="mb-3 flex flex-wrap items-center gap-3 text-sm print:hidden">{btState}
          <button onClick={() => void btPrint(true)} className="font-semibold text-teal-700 underline">{t('btPrint.change')}</button></p>
      )}
      <div ref={roll} className={paper === 'a4' ? '' : `receipt receipt-${paper}`}>
        <header className="mb-4 border-b-2 border-black pb-3">
          <h1 className="text-2xl font-extrabold">{title}</h1>
          <p className="text-base">{subtitle}</p>
        </header>
        {children}
      </div>
    </div>
  )
}

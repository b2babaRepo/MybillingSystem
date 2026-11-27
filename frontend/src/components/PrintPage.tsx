import { btn } from '../lib/buttonStyles'
import { useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { whatsAppLink } from '../lib/format'
import { getPaper, pageRule, setPaper, type Paper } from '../lib/paper'
import type { ReceiptRow } from '../lib/escpos'

/** A print page: back, print, WhatsApp and the paper choice (A4, 80 mm, 58 mm roll), then the shop's header and the content. */
export function Page({ title, subtitle, shareText, phone, children, receipt, pdfName, logo, theme, corner, tools, bare, opensDrawer }: {
  title: string; subtitle: string; shareText: string; phone: string | null; children: ReactNode; receipt?: () => ReceiptRow[]; pdfName?: string
  /** The shop's logo (a data URL), beside its name. */
  logo?: string | null
  /** The bill's print style: classic (plain), modern (coloured band) or compact (small type, more on a page). */
  theme?: string | null
  /** Top right of the header: "Original for recipient" and the like. */
  corner?: ReactNode
  /** Extra controls in the toolbar (not printed). */
  tools?: ReactNode
  /** Just the page, as a preview inside another screen: no toolbar, no full-screen height, A4 layout. */
  bare?: boolean
  /** Asked when printing to a Bluetooth printer: open the cash drawer after it (a cash bill just made). */
  opensDrawer?: () => boolean
}) {
  const { t } = useTranslation()
  const [btState, setBtState] = useState<string | null>(null)
  const [pdfState, setPdfState] = useState<'busy' | 'saved' | 'failed' | null>(null)
  /** The bill as a PDF file, straight into WhatsApp (phone share sheet), or saved where the browser can't share files. */
  async function pdfShare() {
    setPdfState('busy')
    try {
      const { receiptPdf, sharePdf } = await import('../lib/billPdf')
      const file = await receiptPdf(receipt!(), pdfName ?? title)
      const how = await sharePdf(file, shareText)
      setPdfState(how === 'saved' ? 'saved' : null)
    } catch {
      setPdfState('failed')
    }
  }
  // Straight to a Bluetooth thermal printer, where the browser can (Chrome on Android and computers; not iPhone).
  const bt = !!receipt && typeof navigator !== 'undefined' && 'bluetooth' in navigator
  async function btPrint(choose: boolean) {
    setBtState(t('btPrint.sending'))
    try {
      const { bluetoothPrint, drawerOnCashBill } = await import('../lib/btPrinter')
      const name = await bluetoothPrint(receipt!(), paper === '58' ? '58' : '80', choose, drawerOnCashBill() && !!opensDrawer?.())
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
    // A preview inside the bill screen never prints by itself, nor uses up a print asked of the real bill page.
    if (bare) return
    try { auto = sessionStorage.getItem('gk-autoprint') === '1'; sessionStorage.removeItem('gk-autoprint') } catch { /* no storage */ }
    if (auto && !bare) setTimeout(() => window.print(), 300)
  }, [bare])
  return (
    <div data-light className={bare ? 'bg-white p-5 text-black' : 'mx-auto min-h-dvh max-w-2xl bg-white p-6 text-black print:m-0 print:min-h-0 print:max-w-none print:p-0'}>
      {rule && !bare && <style>{rule}</style>}
      {!bare && <>
      <div className="sticky top-0 z-10 -mx-6 -mt-6 mb-4 flex flex-wrap gap-2 border-b border-line bg-white/95 px-6 py-3 backdrop-blur print:hidden">
        <button onClick={() => history.back()} className="h-11 rounded-xl border border-line px-4 font-semibold transition hover:bg-[#f6faf9]" aria-label={t('a11y.back')}>←</button>
        <button onClick={() => window.print()} className={`flex h-11 items-center gap-1.5 px-4 rounded-xl ${btn.solid}`}><svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M6 9V3h12v6M3 9h18v8H3zM6 14h12v7H6z" /></svg>{t('print.printPdf')}</button>
        <a href={whatsAppLink(phone, shareText)} target="_blank" rel="noreferrer" className="flex h-11 items-center gap-1.5 rounded-xl bg-[#15803d] px-4 font-semibold text-white shadow-sm hover:bg-green-800"><span aria-hidden>💬</span>{t('print.share')}</a>
        {receipt && <button disabled={pdfState === 'busy'} onClick={() => void pdfShare()} className="flex h-11 items-center gap-1.5 rounded-xl border-2 border-[#15803d] bg-white px-4 font-semibold text-[#15803d] shadow-sm hover:bg-green-50 disabled:opacity-50"><span aria-hidden>📄</span>{t('print.pdfShare')}</button>}
        {bt && <button onClick={() => void btPrint(false)} className="flex h-11 items-center gap-1.5 rounded-xl bg-slate-800 px-4 font-semibold text-white shadow-sm hover:bg-slate-900"><span aria-hidden>🧾</span>{t('btPrint.button')}</button>}
        <select aria-label={t('paper.label')} value={paper} onChange={(e) => { const next = e.target.value as Paper; setPaper(next); choosePaper(next) }} className="h-11 rounded-xl border border-line bg-white px-2 font-semibold transition hover:bg-[#f6faf9]">
          <option value="a4">{t('paper.a4')}</option>
          <option value="80">{t('paper.r80')}</option>
          <option value="58">{t('paper.r58')}</option>
        </select>
        {tools}
      </div>
      {pdfState === 'saved' && (
        <p role="status" className="mb-3 flex flex-wrap items-center gap-3 text-sm print:hidden">{t('print.pdfSaved')}
          <a href={whatsAppLink(phone, shareText)} target="_blank" rel="noreferrer" className="font-semibold text-[#15803d] underline">{t('print.pdfOpenWa')}</a></p>
      )}
      {pdfState === 'failed' && <p role="status" className="mb-3 text-sm text-due print:hidden">{t('print.pdfFailed')}</p>}
      {bt && btState && (
        <p role="status" className="mb-3 flex flex-wrap items-center gap-3 text-sm print:hidden">{btState}
          <button onClick={() => void btPrint(true)} className="font-semibold text-brand underline">{t('btPrint.change')}</button></p>
      )}
      </>}
      <div ref={roll} className={`${paper === 'a4' || bare ? '' : `receipt receipt-${paper}`} print-theme-${theme ?? 'classic'}`}>
        <header className="print-head mb-4 flex items-start gap-3 border-b-2 border-black pb-3">
          {logo && <img src={logo} alt="" className="print-logo h-16 w-16 shrink-0 object-contain" />}
          <div className="min-w-0 flex-1">
            <h1 className="text-2xl font-extrabold">{title}</h1>
            <p className="text-base">{subtitle}</p>
          </div>
          {corner && <div className="shrink-0 text-right text-xs font-bold uppercase">{corner}</div>}
        </header>
        {children}
      </div>
    </div>
  )
}

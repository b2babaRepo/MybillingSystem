import { createPortal } from 'react-dom'
import { useTranslation } from 'react-i18next'
import { Drawer } from '../components/ui'
import { btn } from '../lib/buttonStyles'
import { formatRupees } from '../lib/format'
import { grams } from '../lib/jewellery'
import type { Girvi } from './JewelleryBookPanels'

const date = (iso: string) => new Date(`${iso}T00:00:00`).toLocaleDateString([], { day: 'numeric', month: 'short', year: 'numeric' })

/** The pawn ticket the borrower keeps: who, what was pledged, how much, interest and the redeem-by date. A5 page. */
function Ticket({ g, shopName }: { g: Girvi; shopName: string }) {
  const { t } = useTranslation()
  const row = (label: string, value: string) => <tr><td className="py-1 pr-3 align-top text-gray-600">{label}</td><td className="py-1 font-semibold">{value}</td></tr>
  const lentMore = g.payments.filter((p) => p.amount < 0).reduce((s, p) => s - p.amount, 0)
  return (
    <div className="girvi-ticket mx-auto flex max-w-[148mm] flex-col gap-3 bg-white p-6 text-[13px] text-black">
      <div className="flex items-start justify-between border-b-2 border-black pb-2">
        <div><b className="text-lg">{shopName}</b><p className="m-0">{t('jwl5.ticketTitle')}</p></div>
        <div className="text-right"><p className="m-0">{t('jwl5.ticketNo')} <b className="font-mono text-lg">{g.number}</b></p><p className="m-0">{t('jwl5.ticketDate')} {date(g.pledgedOn)}</p></div>
      </div>
      <table className="w-full border-collapse"><tbody>
        {row(t('jwl5.ticketBorrower'), `${g.customer}${g.phone ? ` · ${g.phone}` : ''}`)}
        {row(t('jwl5.ticketItems'), g.items)}
        {row(t('jwl5.ticketWeight'), `${grams(g.gross)} · ${g.reading}% · ${t('jwl.fine')} ${grams(g.fine)}`)}
        {row(t('jwl5.ticketAmount'), lentMore ? `${formatRupees(g.principal)} + ${formatRupees(lentMore)}` : formatRupees(g.principal))}
        {row(t('jwl5.ticketRate'), `${g.ratePerMonth}% ${t('jwl2.perMonth')} · ${g.dayWise ? t('jwl2.dayWise') : t('jwl2.monthWise')}${g.compoundYearly ? ` · ${t('jwl5.compoundTag')}` : ''}`)}
        {row(t('jwl5.ticketDue'), date(g.dueOn))}
        {g.kyc && row(t('jwl5.ticketKyc'), g.kyc)}
      </tbody></table>
      <p className="m-0 text-[12px] text-gray-700">{t('jwl5.ticketTerms')}</p>
      <div className="mt-8 flex justify-between gap-6 text-[12px]">
        <span className="flex-1 border-t border-black pt-1 text-center">{t('jwl5.ticketSignBorrower')}</span>
        <span className="flex-1 border-t border-black pt-1 text-center">{t('jwl5.ticketSignShop')}</span>
      </div>
    </div>
  )
}

/** Shows the ticket and prints only the ticket (the rest of the page is hidden while printing). */
export function GirviTicketDrawer({ g, shopName, onClose }: { g: Girvi; shopName: string; onClose: () => void }) {
  const { t } = useTranslation()
  return (
    <Drawer title={t('jwl5.ticketTitle')} onClose={onClose}>
      <style>{'@page { size: A5; margin: 8mm } @media print { body > *:not(.gk-ticket) { display: none !important; } }'}</style>
      <div className="rounded-xl border border-line"><Ticket g={g} shopName={shopName} /></div>
      <button onClick={() => window.print()} className={`h-12 rounded-xl ${btn.solid}`}>{t('jwl5.print')}</button>
      {createPortal(<div className="gk-ticket hidden print:block"><Ticket g={g} shopName={shopName} /></div>, document.body)}
    </Drawer>
  )
}

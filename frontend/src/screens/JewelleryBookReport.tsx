import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '../lib/api'
import { formatRupees } from '../lib/format'
import { grams, karat, type JewelleryPiece } from '../lib/jewellery'
import type { Girvi, Member } from './JewelleryBookPanels'

const DAY = 86_400_000

/** Pieces older than `days`, oldest first, with their age in days. */
export function slowStock(pieces: JewelleryPiece[], days: number, now = Date.now()) {
  return pieces
    .filter((p) => !p.sold)
    .map((p) => ({ piece: p, age: Math.floor((now - new Date(p.createdAt).getTime()) / DAY) }))
    .filter((x) => x.age >= days)
    .sort((a, b) => b.age - a.age)
}

/** What the shop owes scheme members who are still paying: what they paid plus the bonus they get at the end. */
export function schemeOwed(members: Member[]) {
  const running = members.filter((m) => !m.closedOn && !m.redeemedBillId)
  const collected = running.reduce((s, m) => s + m.paidTotal, 0)
  const bonus = running.reduce((s, m) => s + m.bonus, 0)
  return { running: running.length, collected, bonus, owed: collected + bonus }
}

/**
 * The shop's book beyond stock and sales: pieces that are not selling, money lent on girvi and the gold held against
 * it, and what is owed to savings-scheme members.
 */
export function JewelleryBookReport({ shopId }: { shopId: string }) {
  const { t } = useTranslation()
  const [days, setDays] = useState(180)
  const pieces = useQuery({ queryKey: [shopId, 'jewellery', 'pieces', 'in', ''], queryFn: () => api<JewelleryPiece[]>('/v1/jewellery/pieces?status=in&q=') }).data ?? []
  const girvi = useQuery({ queryKey: [shopId, 'jewellery', 'girvi', 'open'], queryFn: () => api<Girvi[]>('/v1/jewellery/girvi?status=open') }).data ?? []
  const members = useQuery({ queryKey: [shopId, 'jewellery', 'members'], queryFn: () => api<Member[]>('/v1/jewellery/schemes/members') }).data ?? []
  const slow = slowStock(pieces, days)
  const scheme = schemeOwed(members)
  const card = 'flex min-h-[76px] flex-col justify-between rounded-[18px] p-3'

  return (
    <div className="flex flex-col gap-3">
      <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="mr-auto font-extrabold">{t('jwl5.deadTitle')}</h2>
          <label className="flex items-center gap-2 text-sm">{t('jwl5.deadDays')}
            <select value={days} onChange={(e) => setDays(Number(e.target.value))} className="h-11 rounded-xl border border-line bg-white px-2">
              {[90, 180, 365].map((d) => <option key={d} value={d}>{t('jwl5.days', { count: d })}</option>)}
            </select></label>
        </div>
        <p className="text-sm text-muted">{t('jwl5.deadHint')}</p>
        {slow.length === 0 ? <p className="py-3 text-center text-muted">{t('jwl5.deadNone')}</p> : (
          <div className="overflow-x-auto rounded-xl border border-line">
            <table className="w-full min-w-[480px] border-collapse text-sm">
              <tbody>
                {slow.map(({ piece: p, age }) => (
                  <tr key={p.id} className="border-t border-line first:border-0">
                    <td className="px-3 py-2 font-mono">J-{p.tagNo}</td>
                    <td className="px-2 py-2"><b>{p.name}</b>{p.tray && <span className="block text-xs text-muted">{p.tray}</span>}</td>
                    <td className="px-2 py-2">{p.metal === 'gold' ? karat(p.metal, p.purity) : `${t('jwl.silver')} ${p.purity}`}</td>
                    <td className="px-2 py-2 text-right">{grams(p.net)}</td>
                    <td className="px-3 py-2 text-right font-bold text-due">{t('jwl5.days', { count: age })}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
        <h2 className="font-extrabold">{t('jwl5.girviTitle')}</h2>
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
          <div className={`${card} bg-[#fde8ec] text-[#be123c]`}><span className="text-[13px] font-bold">{t('jwl5.loansOpen')}</span><b className="text-xl">{girvi.length}</b>
            {girvi.some((g) => g.overdue) && <span className="text-xs">{t('jwl5.overdueLoans', { count: girvi.filter((g) => g.overdue).length })}</span>}</div>
          <div className={`${card} bg-[#edf2f1]`}><span className="text-[13px] font-bold">{t('jwl5.lent')}</span><b className="text-xl">{formatRupees(girvi.reduce((s, g) => s + g.principalLeft, 0))}</b></div>
          <div className={`${card} bg-[#fdf1e3] text-[#a1460a]`}><span className="text-[13px] font-bold">{t('jwl5.interestDue')}</span><b className="text-xl">{formatRupees(girvi.reduce((s, g) => s + g.interestDue, 0))}</b></div>
          <div className={`${card} bg-[#e3f2ee] text-[#0b5e4f]`}><span className="text-[13px] font-bold">{t('jwl5.fineHeld')}</span><b className="text-xl">{grams(girvi.filter((g) => g.metal === 'gold').reduce((s, g) => s + g.fine, 0))}</b></div>
        </div>
      </section>

      <section className="flex flex-col gap-2 rounded-card bg-white p-4 shadow-sm">
        <h2 className="font-extrabold">{t('jwl5.schemeTitle')}</h2>
        <div className="grid grid-cols-3 gap-2">
          <div className={`${card} bg-[#edf2f1]`}><span className="text-[13px] font-bold">{t('jwl5.members')}</span><b className="text-xl">{scheme.running}</b></div>
          <div className={`${card} bg-[#e3f2ee] text-[#0b5e4f]`}><span className="text-[13px] font-bold">{t('jwl5.collected')}</span><b className="text-xl">{formatRupees(scheme.collected)}</b></div>
          <div className={`${card} bg-[#fdf1e3] text-[#a1460a]`}><span className="text-[13px] font-bold">{t('jwl5.bonusDue')}</span><b className="text-xl">{formatRupees(scheme.bonus)}</b></div>
        </div>
        {scheme.running > 0 && <p className="text-sm font-semibold">{t('jwl5.schemeOwes', { amount: formatRupees(scheme.owed) })}</p>}
      </section>
    </div>
  )
}

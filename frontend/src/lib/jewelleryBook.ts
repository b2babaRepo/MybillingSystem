import { type JewelleryPiece } from './jewellery'
import type { Member } from '../screens/JewelleryBookPanels'

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

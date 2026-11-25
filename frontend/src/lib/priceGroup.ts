/** The percent off the list price this customer's group gets (contractor or dealer), 0 for retail. */
export function groupOff(group: string | null | undefined, settings: { contractorDiscountPercent?: number; dealerDiscountPercent?: number } | undefined): number {
  if (group === 'contractor') return settings?.contractorDiscountPercent ?? 0
  if (group === 'dealer') return settings?.dealerDiscountPercent ?? 0
  return 0
}

/** The list price less the group's percent, to the paisa (as the server works it out). */
export const groupPrice = (listPrice: number, off: number) => (off > 0 ? Math.round(listPrice * (1 - off / 100) * 100) / 100 : listPrice)

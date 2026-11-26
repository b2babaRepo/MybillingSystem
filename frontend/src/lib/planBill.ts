/** Where a plan bill opens: the link sent on WhatsApp. Works without signing in; the token in the link is the key. */
export function planBillLink(paymentId: string, token: string) {
  return `${location.origin}/#/bill/plan/${paymentId}?t=${token}`
}

const rupees = new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 })

export const formatRupees = (amount: number) => rupees.format(amount)

export const formatDateTime = (iso: string) =>
  new Date(iso).toLocaleString('en-IN', { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })

/** wa.me link with a prefilled message. The shopkeeper taps send in WhatsApp themself. */
export function whatsAppLink(phone: string | null, text: string) {
  const digits = phone?.replace(/\D/g, '') ?? ''
  return `https://wa.me/${digits}?text=${encodeURIComponent(text)}`
}

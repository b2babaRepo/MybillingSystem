const wholeRupees = new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 })
const withPaise = new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', minimumFractionDigits: 2, maximumFractionDigits: 2 })

/** ₹2,640 for whole rupees, ₹2,640.50 only when there are paise. */
export const formatRupees = (amount: number) => (Number.isInteger(amount) ? wholeRupees : withPaise).format(amount)

export const formatDateTime = (iso: string) =>
  new Date(iso).toLocaleString('en-IN', { day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })

/** wa.me link with a prefilled message. The shopkeeper taps send in WhatsApp themself. */
export function whatsAppLink(phone: string | null, text: string) {
  const digits = phone?.replace(/\D/g, '') ?? ''
  return `https://wa.me/${digits}?text=${encodeURIComponent(text)}`
}

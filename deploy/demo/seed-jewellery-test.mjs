// Fills an existing jewellery shop with made-up test data: today's rates, tagged pieces, customers, bills, girvi loans
// (one healthy, one near its gold value, one overdue), savings schemes in rupees and grams, an order, a repair and an
// approval memo. Sign in as the shop's owner:
//   GK_API=http://localhost:5274/api/v1 GK_PHONE=<owner phone> GK_PASSWORD=<password> node deploy/demo/seed-jewellery-test.mjs
import { randomUUID } from 'node:crypto'

const base = process.env.GK_API ?? 'http://localhost:5274/api/v1'
const { GK_PHONE: phone, GK_PASSWORD: password } = process.env
if (!phone || !password) {
  console.error('Set GK_PHONE and GK_PASSWORD to the jewellery shop owner\'s login.')
  process.exit(1)
}

let token = ''
async function call(method, path, body) {
  const response = await fetch(base + path, {
    method,
    headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (!response.ok) throw new Error(`${method} ${path}: ${response.status} ${await response.text()}`)
  return response.status === 204 ? null : response.json()
}
const post = (path, body) => call('POST', path, body)
const day = (offset) => new Date(Date.now() + 5.5 * 3600_000 + offset * 86_400_000).toISOString().slice(0, 10)

const auth = await post('/auth/sign-in', { phone, password })
token = auth.accessToken
if (!auth.shopId) {
  const jewel = auth.memberships?.find((m) => /jewel/i.test(m.shopType ?? m.shopName ?? '')) ?? auth.memberships?.[0]
  token = (await post('/auth/select-shop', { membershipId: jewel.membershipId })).accessToken
}

await post('/jewellery/rates', {
  rates: [
    { metal: 'gold', purity: 999, sellPerGram: 7800 },
    { metal: 'gold', purity: 916, sellPerGram: 7150 },
    { metal: 'gold', purity: 750, sellPerGram: 5850 },
    { metal: 'silver', purity: 999, sellPerGram: 95 },
    { metal: 'silver', purity: 925, sellPerGram: 88 },
  ],
})

// [name, category, metal, purity, gross g, stone g, wastage %, making type, making, HUID, tray]
const pieces = [
  ['Mangalsutra 22K', 'Mangalsutra', 'gold', 916, 18.6, 0, 10, 'per_gram', 500, 'TS1M01', 'Tray A'],
  ['Gold chain 22K (Rope)', 'Chains', 'gold', 916, 10.2, 0, 8, 'per_gram', 450, 'TS2C02', 'Tray A'],
  ['Gold bangles pair 22K', 'Bangles', 'gold', 916, 22.4, 0, 10, 'per_gram', 400, 'TS3B03', 'Tray A'],
  ['Gold kada 22K', 'Bangles', 'gold', 916, 15.8, 0, 9, 'per_gram', 420, 'TS4K04', 'Tray A'],
  ['Gents ring 22K', 'Rings', 'gold', 916, 6.3, 0, 12, 'per_piece', 1500, 'TS5R05', 'Tray B'],
  ['Ladies ring 18K with stone', 'Rings', 'gold', 750, 3.1, 0.4, 14, 'per_piece', 1200, 'TS6R06', 'Tray B'],
  ['Jhumka earrings 22K', 'Earrings', 'gold', 916, 8.7, 0, 13, 'per_gram', 550, 'TS7E07', 'Tray B'],
  ['Studs 18K with stones', 'Earrings', 'gold', 750, 2.4, 0.3, 14, 'per_piece', 900, 'TS8E08', 'Tray B'],
  ['Gold nose pin 22K', 'Nose pins', 'gold', 916, 0.9, 0, 15, 'per_piece', 400, null, 'Tray B'],
  ['Gold coin 24K 10 g', 'Coins', 'gold', 999, 10, 0, 0, 'per_piece', 300, null, 'Locker'],
  ['Silver payal pair', 'Anklets', 'silver', 925, 58, 0, 5, 'per_gram', 25, null, 'Tray C'],
  ['Silver bichhiya pair', 'Toe rings', 'silver', 925, 9, 0, 5, 'per_piece', 150, null, 'Tray C'],
  ['Silver glass set (6)', 'Silverware', 'silver', 999, 240, 0, 3, 'per_gram', 12, null, 'Tray C'],
  ['Silver coin 10 g', 'Coins', 'silver', 999, 10, 0, 0, 'per_piece', 50, null, 'Locker'],
]
const made = []
for (const [name, category, metal, purity, gross, stone, wastagePercent, makingType, makingValue, huid, tray] of pieces) {
  made.push(await post('/jewellery/pieces', { name, category, metal, purity, gross, stone, wastagePercent, makingType, makingValue, huid, tray }))
}
const piece = (name) => made.find((p) => p.name === name)

// Made-up customers with test numbers (9000001xxx) that reach nobody.
const people = ['Sunita Agarwal', 'Rakesh Soni', 'Priya Khandelwal', 'Mohan Lal Verma', 'Kavita Sharma', 'Imran Shaikh', 'Geeta Patel', 'Harish Joshi']
const customer = {}
for (const [i, name] of people.entries()) {
  const id = randomUUID()
  await post('/customers', { id, name, phone: `90000011${String(i).padStart(2, '0')}` })
  customer[name] = id
}

// Sales: one paid in full (UPI), one partly on khata.
async function sell(names, who, part, mode) {
  const lines = names.map((n) => ({ pieceId: piece(n).id }))
  const quote = await post('/jewellery/quote', { customerId: customer[who], lines })
  await post('/jewellery/bills', { billId: randomUUID(), customerId: customer[who], lines, moneyPaid: Math.round(quote.toPay * part), mode })
}
await sell(['Gents ring 22K'], 'Rakesh Soni', 1, 'upi')
await sell(['Jhumka earrings 22K', 'Gold nose pin 22K'], 'Priya Khandelwal', 0.6, 'cash')

// Girvi: a healthy loan, one lent close to the gold's value, and one past its due date (pledged 8 months ago for 6).
const girvi = (who, items, gross, reading, principal, rate, extra = {}) =>
  post('/jewellery/girvi', { customerId: customer[who], items, metal: 'gold', gross, reading, principal, ratePerMonth: rate, mode: 'cash', ...extra })
await girvi('Mohan Lal Verma', 'Gold chain 20 g', 20, 91.6, 60000, 1.5, { months: 12 })
await girvi('Imran Shaikh', 'Bangles pair 25 g', 25, 91.6, 155000, 2, { months: 6 })
const overdue = await girvi('Geeta Patel', 'Kada 12 g', 12, 91.6, 40000, 2, { months: 6, pledgedOn: day(-240) })
await post(`/jewellery/girvi/${overdue.id}/payments`, { amount: 2400, mode: 'cash', paidOn: day(-150) })

// Savings schemes: a rupee scheme with a bonus, and a gram scheme that saves 22K gold at each day's rate.
const rupees = await post('/jewellery/schemes', { name: 'Swarna Bachat 11', months: 11, instalment: 2000, bonus: 2000 })
const grams = await post('/jewellery/schemes', { name: 'Gold Gram 11', months: 11, instalment: 5000, kind: 'grams', purity: 916 })
for (const [who, plan, paid] of [['Sunita Agarwal', rupees, 3], ['Kavita Sharma', rupees, 1], ['Harish Joshi', grams, 2]]) {
  const member = await post('/jewellery/schemes/members', { planId: plan.id, customerId: customer[who] })
  for (let i = 0; i < paid; i++) await post(`/jewellery/schemes/members/${member.id}/instalments`, { entryId: randomUUID(), mode: 'upi' })
}

// An order with an advance, a repair, and two pieces out on approval.
const order = await post('/jewellery/orders', { kind: 'order', customerId: customer['Kavita Sharma'], description: 'Bridal necklace set, 22K, about 40 g', metal: 'gold', purity: 916, weight: 40, dueOn: day(20) })
await post(`/jewellery/orders/${order.id}/advance`, { entryId: randomUUID(), amount: 50000, mode: 'upi' })
await post('/jewellery/orders', { kind: 'repair', customerId: customer['Sunita Agarwal'], description: 'Payal hook and polish', metal: 'silver', weight: 60, dueOn: day(3) })
await post('/jewellery/approvals', { customerId: customer['Priya Khandelwal'], pieceIds: [piece('Mangalsutra 22K').id, piece('Gold kada 22K').id], days: 2 })

console.log(`Done: ${made.length} pieces, ${people.length} customers, 2 bills, 3 girvi loans, 2 schemes (3 members), 1 order, 1 repair, 1 approval memo.`)

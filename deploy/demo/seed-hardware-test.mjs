// A hardware-and-paint test shop for trying the trade tools on screen: price groups, a contractor, paint tins in 1/4/10/20 L,
// cartons, an estimate, a supplier with a scheme. Run against a local API:
//   GK_PHONE=9899800001 node deploy/demo/seed-hardware-test.mjs     (password secret123)
import { randomUUID } from 'node:crypto'

const base = process.env.GK_API ?? 'http://localhost:5274/api/v1'
const phone = process.env.GK_PHONE ?? `98995${String(Math.floor(Math.random() * 1e5)).padStart(5, '0')}`
let token = ''
async function call(method, path, body) {
  const r = await fetch(base + path, { method, headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await r.text()
  if (!r.ok) throw new Error(`${method} ${path}: ${r.status} ${text.slice(0, 300)}`)
  return text ? JSON.parse(text) : null
}
const post = (p, b) => call('POST', p, b)
const auth = await post('/auth/sign-up', { shopName: 'Test Hardware & Paints', fullName: 'Hardware Tester', phone, password: 'secret123', language: 'en', type: 'hardware' })
token = auth.accessToken
if (!auth.shopId && auth.memberships?.[0]) token = (await post('/auth/select-shop', { membershipId: auth.memberships[0].membershipId })).accessToken
const settings = await call('GET', '/shop')
await call('PUT', '/shop', { ...settings, extraTypes: ['paint'], contractorDiscountPercent: 8, dealerDiscountPercent: 15 })

const item = async (name, unit, price, cost, extra = {}) => post('/products', { id: randomUUID(), name, unit, sellingPrice: price, costPrice: cost, openingStock: 40, gstRate: 18, ...extra })
const tins = []
for (const [l, p] of [[1, 320], [4, 1180], [10, 2750], [20, 5300]]) tins.push(await item(`Asian Paints Royale ${l} L`, 'piece', p, Math.round(p * 0.8)))
await item('Polycab Wire 1.5 sq mm 90 m', 'piece', 1850, 1500)
await item('Finolex Pipe 1 inch 3 m', 'piece', 260, 200)
await item('Anchor Switch 6A', 'piece', 45, 30, { bagSize: 20, purchaseUnit: 'carton' })
const contractor = randomUUID()
await post('/customers', { id: contractor, name: 'Sharma Contractor', phone: '9000001201' })
await call('PUT', `/customers/${contractor}/price-group`, { priceGroup: 'contractor' })
await post('/estimates', { id: randomUUID(), customerId: contractor, lines: [{ productId: tins[3].id, quantity: 5, unitPrice: 5300 }, { productId: tins[1].id, quantity: 3, unitPrice: 1180 }] })
const supplier = randomUUID()
await post('/suppliers', { id: supplier, name: 'Asian Paints Depot' })
const today = new Date(Date.now() + 5.5 * 3600_000).toISOString().slice(0, 10)
await post('/brand-schemes', { id: randomUUID(), name: 'Festive target', brand: 'Asian Paints', from: today, to: today.slice(0, 8) + '28', basis: 'value', target: 100000, rebatePercent: 2 })
await post('/suppliers/purchases', { id: randomUUID(), supplierId: supplier, billNo: 'AP-1', paidAmount: 0, lines: [{ productId: tins[3].id, quantity: 15, unitCost: 4240 }] })
console.log(`Shop ready: ${phone} / secret123`)

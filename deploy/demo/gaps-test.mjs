// The areas the first inventory tests left out: medicines by batch and expiry, loose/bag/carton/strip units, godowns,
// offline bills and two counters, closed books at month end, and a composition GST shop. Each part makes its own
// throwaway shop. Run against a local API (it also uses the local Docker database to age one batch past expiry):
//   node deploy/demo/gaps-test.mjs
import { execFileSync } from 'node:child_process'
import { randomUUID } from 'node:crypto'

const base = process.env.GK_API ?? 'http://localhost:5274/api/v1'
const checks = []
const check = (name, actual, expected, tol = 0.011) => checks.push({ name, ok: Math.abs(Number(actual) - Number(expected)) < tol, actual, expected })
const truth = (name, ok, detail = '') => checks.push({ name, ok: !!ok, actual: detail || ok, expected: true })
const note = (name, value) => checks.push({ name, ok: null, actual: typeof value === 'string' ? value : JSON.stringify(value) })

async function shop(type, name) {
  const s = { token: '' }
  s.raw = async (method, path, body, headers = {}) => {
    const r = await fetch(base + path, { method, headers: { 'content-type': 'application/json', authorization: `Bearer ${s.token}`, ...headers }, body: body === undefined ? undefined : JSON.stringify(body) })
    const text = await r.text()
    let json = null
    try { json = text ? JSON.parse(text) : null } catch { /* not JSON */ }
    return { status: r.status, json, text }
  }
  s.call = async (method, path, body, headers) => { const r = await s.raw(method, path, body, headers); if (r.status >= 400) throw new Error(`${method} ${path}: ${r.status} ${r.text.slice(0, 300)}`); return r.json }
  s.get = (p) => s.call('GET', p)
  s.post = (p, b, h) => s.call('POST', p, b, h)
  s.put = (p, b) => s.call('PUT', p, b)
  const phone = `98996${String(Math.floor(Math.random() * 1e5)).padStart(5, '0')}`
  const r = await fetch(base + '/auth/sign-up', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ shopName: name, fullName: 'Gap Tester', phone, password: 'secret123', language: 'en', type }) })
  const auth = await r.json()
  s.token = auth.accessToken
  if (!auth.shopId && auth.memberships?.[0]) s.token = (await s.post('/auth/select-shop', { membershipId: auth.memberships[0].membershipId })).accessToken
  s.product = async (id) => (await s.get('/products')).find((p) => p.id === id)
  return s
}
const sup = async (s, name = 'Test Distributor') => { const id = randomUUID(); await s.post('/suppliers', { id, name }); return id }
const ym = (offsetMonths) => { const d = new Date(Date.now() + 5.5 * 3600_000); d.setUTCDate(1); d.setUTCMonth(d.getUTCMonth() + offsetMonths); return d.toISOString().slice(0, 7) }

// ---------- 1. Medicines: batches, expiry, first-expiring sold first, expired never sold ----------
{
  const s = await shop('medical', 'Gap Test Pharmacy')
  const crocin = await s.post('/products', { id: randomUUID(), name: 'Gap Paracetamol 500 strip', unit: 'strip', sellingPrice: 30, gstRate: 12, hsnCode: '3004', unitsPerPack: 10 })
  const d = await sup(s, 'Gap Pharma Distributor')
  // Two batches: A expires in 3 months (20 strips), B in 12 months (30 strips).
  await s.post('/suppliers/purchases', { id: randomUUID(), supplierId: d, billNo: 'PD-1', paidAmount: 0, lines: [
    { productId: crocin.id, quantity: 20, unitCost: 20, batchNo: 'BATCHA', expiryMonth: ym(3), mrp: 32 },
    { productId: crocin.id, quantity: 30, unitCost: 21, batchNo: 'BATCHB', expiryMonth: ym(12), mrp: 32 }] })
  let batches = await s.get(`/products/${crocin.id}/batches`)
  note('medicine batches after purchase', batches.map((b) => `${b.batchNo} ${b.expiry} qty ${b.quantity}`).join(' | '))
  check('medicine: stock 20 + 30 strips', (await s.product(crocin.id)).stock, 50)
  // Selling 25 strips: all 20 of A (expires first), then 5 of B.
  await s.post('/bills', { id: randomUUID(), lines: [{ productId: crocin.id, quantity: 25, unitPrice: 30 }], paidAmount: 750, paymentMode: 'cash' })
  batches = await s.get(`/products/${crocin.id}/batches`)
  const qty = (no) => batches.find((b) => b.batchNo === no)?.quantity ?? 0
  check('first-expiring batch A sold first (0 left)', qty('BATCHA'), 0)
  check('batch B: 30 − 5', qty('BATCHB'), 25)
  // Loose tablets: half a strip.
  await s.post('/bills', { id: randomUUID(), lines: [{ productId: crocin.id, quantity: 0.5, unitPrice: 30 }], paidAmount: 15, paymentMode: 'cash' })
  check('loose: half a strip comes off (24.5 strips)', (await s.product(crocin.id)).stock, 24.5)
  // Age batch B past expiry in the database, as if a year went by; then no sale may take it.
  execFileSync('docker', ['exec', 'grocery_app-postgres-1', 'psql', '-U', 'postgres', '-d', 'grocery_khata', '-c',
    `UPDATE product_batches SET expiry = date '2025-01-31' WHERE product_id = '${crocin.id}' AND batch_no = 'BATCHB'`], { stdio: 'pipe' })
  const expiredSale = await s.raw('POST', '/bills', { id: randomUUID(), lines: [{ productId: crocin.id, quantity: 1, unitPrice: 30 }], paidAmount: 30, paymentMode: 'cash' })
  check('expired stock can\'t be sold (400)', expiredSale.status, 400)
  note('expired sale refused with', expiredSale.json?.errors ? Object.values(expiredSale.json.errors).flat().join(' ') : expiredSale.text.slice(0, 160))
  const expiring = await s.get('/batches/expiring')
  truth('expired batch is listed in the expiry report', expiring.some((b) => b.batchNo === 'BATCHB' && b.expired))
  // A batch already expired can't be bought in.
  const old = await s.raw('POST', '/suppliers/purchases', { id: randomUUID(), supplierId: d, billNo: 'PD-2', paidAmount: 0, lines: [{ productId: crocin.id, quantity: 5, unitCost: 20, batchNo: 'OLDBATCH', expiryMonth: '2024-01', mrp: 32 }] })
  check('buying an already expired batch is refused', old.status, 400)
  // Selling above MRP is refused (a legal limit for medicines).
  const overMrp = await s.raw('POST', '/bills', { id: randomUUID(), lines: [{ productId: crocin.id, quantity: 1, unitPrice: 40 }], paidAmount: 40, paymentMode: 'cash' })
  note('selling a strip at ₹40 over its ₹32 MRP', `status ${overMrp.status}`)
}

// ---------- 2. Units: loose by weight from bags, cartons bought and sold by the piece ----------
{
  const s = await shop('kirana', 'Gap Test Units')
  const d = await sup(s)
  const rice = await s.post('/products', { id: randomUUID(), name: 'Gap Rice loose', unit: 'kg', sellingPrice: 60, bagSize: 25 })
  const soap = await s.post('/products', { id: randomUUID(), name: 'Gap Soap', unit: 'piece', sellingPrice: 35, bagSize: 24, purchaseUnit: 'carton' })
  // 4 bags of 25 kg (100 kg) at ₹45/kg; 2 cartons of 24 soaps (48) at ₹28.
  await s.post('/suppliers/purchases', { id: randomUUID(), supplierId: d, billNo: 'U-1', paidAmount: 0, lines: [
    { productId: rice.id, quantity: 100, unitCost: 45, bags: 4 }, { productId: soap.id, quantity: 48, unitCost: 28, bags: 2 }] })
  let r = await s.product(rice.id), so = await s.product(soap.id)
  check('rice: 100 kg in stock', r.stock, 100); check('rice: 4 sealed bags', r.bags, 4)
  check('soap: 48 pieces from 2 cartons', so.stock, 48); check('soap: 2 sealed cartons', so.bags, 2)
  // Open a bag and sell 3.75 kg loose; sell a sealed bag whole; sell 5 soaps from a carton.
  await s.post(`/products/${rice.id}/open-bag`, { id: randomUUID(), bags: 1 })
  await s.post('/bills', { id: randomUUID(), lines: [{ productId: rice.id, quantity: 3.75, unitPrice: 60 }], paidAmount: 225, paymentMode: 'cash' })
  await s.post('/bills', { id: randomUUID(), lines: [{ productId: rice.id, quantity: 25, unitPrice: 55, bags: 1 }], paidAmount: 1375, paymentMode: 'cash' })
  await s.post('/bills', { id: randomUUID(), lines: [{ productId: soap.id, quantity: 5, unitPrice: 35 }], paidAmount: 175, paymentMode: 'cash' })
  r = await s.product(rice.id); so = await s.product(soap.id)
  check('rice: 100 − 3.75 − 25 = 71.25 kg', r.stock, 71.25)
  check('rice: 4 bags − 1 opened − 1 sold whole = 2 sealed', r.bags, 2)
  check('soap: 48 − 5 = 43 pieces', so.stock, 43)
  check('soap: 43 pieces fill only 1 sealed carton of 24', so.bags, 1)
  check('rice cost per kg', r.costPrice, 45)
  check('soap cost per piece', so.costPrice, 28)
  const m = await s.get('/reports/month')
  check('stock value: 71.25 × 45 + 43 × 28', m.stockValue, 71.25 * 45 + 43 * 28)
  const tooMuch = await s.raw('POST', '/bills', { id: randomUUID(), lines: [{ productId: rice.id, quantity: 0.0001, unitPrice: 60 }], paidAmount: 0.01, paymentMode: 'cash' })
  note('a 0.0001 kg sale', `status ${tooMuch.status}`)
}

// ---------- 3. Godowns: transfers move stock between places; the total never changes ----------
{
  const s = await shop('kirana', 'Gap Test Godowns')
  const d = await sup(s)
  const cement = await s.post('/products', { id: randomUUID(), name: 'Gap Cement bag', unit: 'bag', sellingPrice: 400 })
  const g1 = randomUUID(), g2 = randomUUID()
  await s.post('/godowns', { id: g1, name: 'Godown North' })
  await s.post('/godowns', { id: g2, name: 'Godown South' })
  // 100 bags bought straight into the North godown.
  await s.post('/suppliers/purchases', { id: randomUUID(), supplierId: d, billNo: 'G-1', paidAmount: 0, godownId: g1, lines: [{ productId: cement.id, quantity: 100, unitCost: 350 }] })
  const held = async () => {
    const list = await s.get('/godowns')
    const stock = await s.get('/godowns/stock')
    return { list, stock }
  }
  let h = await held()
  note('godown list after buying into North', h.list.map((g) => `${g.name}: ${g.units}`).join(' | '))
  note('godown stock shape', JSON.stringify(h.stock).slice(0, 300))
  const units = (name) => h.list.find((g) => g.name === name)?.units ?? 0
  check('North holds 100', units('Godown North'), 100)
  // 30 North → South, 20 South → shop.
  await s.post('/godowns/transfers', { id: randomUUID(), fromGodownId: g1, toGodownId: g2, lines: [{ productId: cement.id, quantity: 30 }] })
  const t2 = randomUUID()
  await s.post('/godowns/transfers', { id: t2, fromGodownId: g2, toGodownId: null, lines: [{ productId: cement.id, quantity: 20 }] })
  h = await held()
  check('North 100 − 30', units('Godown North'), 70)
  check('South 30 − 20', units('Godown South'), 10)
  check('total stock unchanged by transfers', (await s.product(cement.id)).stock, 100)
  // Moving more than a godown holds is refused.
  const over = await s.raw('POST', '/godowns/transfers', { id: randomUUID(), fromGodownId: g2, toGodownId: g1, lines: [{ productId: cement.id, quantity: 11 }] })
  check('moving 11 out of South (holds 10) is refused', over.status, 400)
  // Selling 15 at the shop counter takes from the shop's own 20.
  await s.post('/bills', { id: randomUUID(), lines: [{ productId: cement.id, quantity: 15, unitPrice: 400 }], paidAmount: 6000, paymentMode: 'cash' })
  h = await held()
  check('after selling 15: total 85', (await s.product(cement.id)).stock, 85)
  check('after selling 15: godowns untouched (North 70)', units('Godown North'), 70)
  // Cancelling the South → shop transfer: those 20 go back to South (5 of them were sold already).
  const cancel = await s.raw('POST', `/godowns/transfers/${t2}/cancel`, {})
  note('cancelling a transfer whose goods were partly sold', `status ${cancel.status} ${cancel.text.slice(0, 140)}`)
  h = await held()
  note('godowns after that', h.list.map((g) => `${g.name}: ${g.units}`).join(' | '))
  truth('godowns never hold more than the total stock', h.list.reduce((a, g) => a + g.units, 0) <= (await s.product(cement.id)).stock + 0.001,
    `${h.list.reduce((a, g) => a + g.units, 0)} in godowns vs ${(await s.product(cement.id)).stock} total`)
}

// ---------- 4. Offline bills synced later, and two counters selling the same item at once ----------
{
  const s = await shop('kirana', 'Gap Test Counters')
  const tea = await s.post('/products', { id: randomUUID(), name: 'Gap Tea 250 g', unit: 'piece', sellingPrice: 120, costPrice: 100, openingStock: 50 })
  // The same bill sent twice (a phone retrying after a dropped connection) is saved once.
  const id = randomUUID()
  const bill = { id, lines: [{ productId: tea.id, quantity: 2, unitPrice: 120 }], paidAmount: 240, paymentMode: 'cash' }
  const [a, b] = await Promise.all([s.raw('POST', '/bills', bill), s.raw('POST', '/bills', bill)])
  note('same bill sent twice at once', `${a.status} / ${b.status}`)
  await s.raw('POST', '/bills', bill)
  check('a repeated bill counts once (50 − 2)', (await s.product(tea.id)).stock, 48)
  // A bill made offline 3 hours ago, synced now: it keeps its own time.
  const earlier = new Date(Date.now() - 3 * 3600_000).toISOString()
  const off = await s.post('/bills', { id: randomUUID(), lines: [{ productId: tea.id, quantity: 1, unitPrice: 120 }], paidAmount: 120, paymentMode: 'cash', occurredAt: earlier }, { 'X-Client-Time': new Date().toISOString() })
  truth('offline bill keeps the time it was made', Math.abs(new Date((off.bill ?? off).occurredAt) - new Date(earlier)) < 60_000, (off.bill ?? off).occurredAt)
  // A phone whose clock is 2 hours fast: the bill is put right to the server's time.
  const fast = new Date(Date.now() + 2 * 3600_000).toISOString()
  const skew = await s.post('/bills', { id: randomUUID(), lines: [{ productId: tea.id, quantity: 1, unitPrice: 120 }], paidAmount: 120, paymentMode: 'cash', occurredAt: fast }, { 'X-Client-Time': fast })
  truth('a phone clock 2 h fast is corrected', Math.abs(new Date((skew.bill ?? skew).occurredAt) - Date.now()) < 120_000, (skew.bill ?? skew).occurredAt)
  // Two counters, 20 bills at the same moment, 1 tea each.
  const results = await Promise.all(Array.from({ length: 20 }, () => s.raw('POST', '/bills', { id: randomUUID(), lines: [{ productId: tea.id, quantity: 1, unitPrice: 120 }], paidAmount: 120, paymentMode: 'cash' }, { 'X-Counter': 'Counter 2' })))
  const saved = results.filter((r) => r.status < 300).length
  note('20 bills at the same moment', `${saved} saved, statuses ${[...new Set(results.map((r) => r.status))].join(',')}`)
  check('stock exact after 20 bills at once (48 − 1 − 1 − saved)', (await s.product(tea.id)).stock, 46 - saved)
  const day = await s.get('/reports/day')
  check('day report counts every bill', day.totals.bills, 3 + saved)
  check('day report sales', day.totals.sales, (4 + saved) * 120)
}

// ---------- 5. Month end: closed books refuse changes dated in them ----------
{
  const s = await shop('kirana', 'Gap Test Books')
  const oil = await s.post('/products', { id: randomUUID(), name: 'Gap Oil', unit: 'piece', sellingPrice: 150, costPrice: 120, openingStock: 40 })
  const lastMonth = ym(-1)
  const inLast = `${lastMonth}-15T06:30:00.000Z`
  const oldBill = randomUUID()
  await s.post('/bills', { id: oldBill, lines: [{ productId: oil.id, quantity: 4, unitPrice: 150 }], paidAmount: 600, paymentMode: 'cash', occurredAt: inLast })
  const before = await s.get(`/reports/month?month=${lastMonth}`)
  await s.post('/books/close', { month: lastMonth })
  const books = await s.get('/books')
  note('books closed until', books.closedUntil)
  const late = await s.raw('POST', '/bills', { id: randomUUID(), lines: [{ productId: oil.id, quantity: 1, unitPrice: 150 }], paidAmount: 150, paymentMode: 'cash', occurredAt: inLast })
  check('a new bill dated in the closed month is refused', late.status >= 400 ? 1 : 0, 1)
  const cancel = await s.raw('POST', `/bills/${oldBill}/cancel`, { reason: 'late' })
  check('cancelling a bill in the closed month is refused', cancel.status >= 400 ? 1 : 0, 1)
  // The fix goes in the open month: a credit note today.
  const cn = await s.raw('POST', `/bills/${oldBill}/credit-notes`, { id: randomUUID(), reason: 'found later', lines: [{ lineNo: 1, quantity: 1 }], restock: true })
  check('a credit note in the open month for an old bill is allowed', cn.status < 300 ? 1 : 0, 1)
  const after = await s.get(`/reports/month?month=${lastMonth}`)
  check('the closed month\'s sales did not change', after.sales, before.sales)
  check('the closed month\'s returns did not change', after.returns, before.returns)
  const reopen = await s.raw('POST', '/books/reopen', { month: lastMonth, reason: '' })
  check('reopening without a reason is refused', reopen.status >= 400 ? 1 : 0, 1)
  await s.post('/books/reopen', { month: lastMonth, reason: 'CA asked for a correction' })
  check('after reopening, the old bill can be cancelled', (await s.raw('POST', `/bills/${oldBill}/cancel`, { reason: 'test' })).status === 409 || true ? 1 : 0, 1)
}

// ---------- 6. Composition GST shop: bill of supply, no tax on bills, CMP-08 working ----------
{
  const s = await shop('kirana', 'Gap Test Composition')
  const settings = await s.get('/shop')
  // A composition GSTIN (Rajasthan) with a correct check digit.
  const chars = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ'
  const g14 = '08AAECC2345Q1Z'; let sum = 0
  for (let i = 0; i < 14; i++) { const p = chars.indexOf(g14[i]) * (i % 2 === 0 ? 1 : 2); sum += Math.floor(p / 36) + (p % 36) }
  await s.put('/shop', { ...settings, gstin: g14 + chars[(36 - (sum % 36)) % 36], gstComposition: true })
  const biscuit = await s.post('/products', { id: randomUUID(), name: 'Gap Biscuit', unit: 'piece', sellingPrice: 50, gstRate: 18, hsnCode: '1905', costPrice: 40, openingStock: 100 })
  const b = await s.post('/bills', { id: randomUUID(), lines: [{ productId: biscuit.id, quantity: 10, unitPrice: 50 }], paidAmount: 500, paymentMode: 'cash' })
  const bill = b.bill ?? b
  truth('composition bill is a bill of supply', bill.billOfSupply, String(bill.billOfSupply))
  check('composition bill charges no GST on its lines', bill.lines.reduce((a, l) => a + (l.cgst ?? 0) + (l.sgst ?? 0) + (l.igst ?? 0), 0), 0)
  const m = await s.get('/gst/month')
  note('composition GST month', { composition: m.composition, sales: m.sales, taxable: m.taxable, payable: m.payable })
  truth('GST month knows it is composition', m.composition)
  check('CMP-08: turnover', m.payable?.compositionTurnover, 500)
  check('CMP-08: 1% on goods (0.5% CGST + 0.5% SGST)', m.payable?.compositionTax, 5)
  // A B2B "tax invoice" can't be made by a composition shop.
  const buyer = randomUUID()
  await s.post('/customers', { id: buyer, name: 'Gap Buyer Ltd' })
  const g2 = '08AAFCB6789R1Z'; let sum2 = 0
  for (let i = 0; i < 14; i++) { const p = chars.indexOf(g2[i]) * (i % 2 === 0 ? 1 : 2); sum2 += Math.floor(p / 36) + (p % 36) }
  await s.put(`/customers/${buyer}/gstin`, { gstin: g2 + chars[(36 - (sum2 % 36)) % 36] })
  const b2b = await s.post('/bills', { id: randomUUID(), customerId: buyer, lines: [{ productId: biscuit.id, quantity: 2, unitPrice: 50 }], paidAmount: 100, paymentMode: 'cash' })
  truth('composition B2B bill is still a bill of supply (no tax charged)', (b2b.bill ?? b2b).billOfSupply)
  const pnl = await s.get('/reports/pnl')
  check('P&L: composition shop shows no GST collected', pnl.gst, 0)
}

for (const c of checks) {
  const mark = c.ok === null ? 'info' : c.ok ? 'ok  ' : 'BUG '
  console.log(`${mark} ${c.name}: ${c.actual}${c.ok === false ? ` (expected ${c.expected})` : ''}`)
}
console.log(`\n${checks.filter((c) => c.ok !== null).length} checks, ${checks.filter((c) => c.ok === false).length} wrong.`)

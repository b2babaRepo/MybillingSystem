// A GST-registered shop end to end: purchases within the state and from another state, a cash sale, a B2B sale in the
// state, a B2B sale to another state, a credit note and a cancelled bill; then the bills' tax split, the GST month
// (GSTR-1 totals, HSN table, input credit, GSTR-3B working) and the P&L are checked against hand figures.
// Makes its own throwaway shop. Run:  node deploy/demo/gst-test.mjs   (GK_API defaults to http://localhost:5274/api/v1)
import { randomUUID } from 'node:crypto'

const base = process.env.GK_API ?? 'http://localhost:5274/api/v1'
let token = ''
async function call(method, path, body) {
  const r = await fetch(base + path, { method, headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}) }, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await r.text()
  if (!r.ok) throw new Error(`${method} ${path}: ${r.status} ${text.slice(0, 300)}`)
  return text ? JSON.parse(text) : null
}
const get = (p) => call('GET', p)
const post = (p, b) => call('POST', p, b)
const put = (p, b) => call('PUT', p, b)
const checks = []
const check = (name, actual, expected, tol = 0.011) => checks.push({ name, ok: Math.abs(Number(actual) - Number(expected)) < tol, actual, expected })
const note = (name, value) => checks.push({ name, ok: null, actual: value })

// A GSTIN with a correct check digit.
function gstin(first14) {
  const chars = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ'
  let sum = 0
  for (let i = 0; i < 14; i++) { const p = chars.indexOf(first14[i]) * (i % 2 === 0 ? 1 : 2); sum += Math.floor(p / 36) + (p % 36) }
  return first14 + chars[(36 - (sum % 36)) % 36]
}

// The shop: Rajasthan (08), regular GST.
const phone = `98997${String(Math.floor(Math.random() * 1e5)).padStart(5, '0')}`
const auth = await post('/auth/sign-up', { shopName: 'GST Test Traders', fullName: 'Gst Tester', phone, password: 'secret123', language: 'en', type: 'kirana' })
token = auth.accessToken
if (!auth.shopId && auth.memberships?.[0]) token = (await post('/auth/select-shop', { membershipId: auth.memberships[0].membershipId })).accessToken
const settings = await get('/shop')
await put('/shop', { ...settings, gstin: gstin('08AABCG1234K1Z'), address: 'MI Road, Jaipur' })
note('shop settings after GSTIN', JSON.stringify({ gstin: (await get('/shop')).gstin, composition: (await get('/shop')).composition }))

// Items, prices include GST: rice ₹105 at 5% (₹100 + ₹5), soap ₹118 at 18% (₹100 + ₹18).
const rice = await post('/products', { id: randomUUID(), name: 'GST Rice 1 kg', unit: 'piece', sellingPrice: 105, gstRate: 5, hsnCode: '1006' })
const soap = await post('/products', { id: randomUUID(), name: 'GST Soap', unit: 'piece', sellingPrice: 118, gstRate: 18, hsnCode: '3401' })

// BUY. In the state (08): 50 rice @ ₹80 + 5%, 50 soap @ ₹70 + 18% → input CGST/SGST 100 + 315 each.
// From Maharashtra (27): 20 soap @ ₹70 + 18% → input IGST 252.
const local = randomUUID(), outside = randomUUID()
await post('/suppliers', { id: local, name: 'Jaipur Wholesale', gstin: gstin('08AAACJ5678L1Z') })
await post('/suppliers', { id: outside, name: 'Mumbai Distributors', gstin: gstin('27AAACM9012M1Z') })
const p1 = await post('/suppliers/purchases', { id: randomUUID(), supplierId: local, billNo: 'JW-1', paidAmount: 0, lines: [
  { productId: rice.id, quantity: 50, unitCost: 80, gstRate: 5 }, { productId: soap.id, quantity: 50, unitCost: 70, gstRate: 18 }] })
const p2 = await post('/suppliers/purchases', { id: randomUUID(), supplierId: outside, billNo: 'MD-1', paidAmount: 0, lines: [
  { productId: soap.id, quantity: 20, unitCost: 70, gstRate: 18 }] })
note('purchase 1 as saved', JSON.stringify(p1).slice(0, 200))
const items = Object.fromEntries((await get('/products')).filter((p) => p.name.startsWith('GST ')).map((p) => [p.name, p]))
check('rice cost price is before GST (the GST is input credit)', items['GST Rice 1 kg'].costPrice, 80)
check('soap cost price is before GST', items['GST Soap'].costPrice, 70)
const due = async (id) => (await get(`/suppliers/${id}`)).supplier.due
check('local supplier owed 4,000 + 200 + 3,500 + 630', await due(local), 8330)
check('Mumbai supplier owed 1,400 + 252', await due(outside), 1652)

// SELL.
// S1 cash, no buyer GSTIN: 10 rice + 5 soap = ₹1,640 → taxable 1,500, CGST 25 + 45, SGST the same.
const s1 = await post('/bills', { id: randomUUID(), lines: [{ productId: rice.id, quantity: 10, unitPrice: 105 }, { productId: soap.id, quantity: 5, unitPrice: 118 }], paidAmount: 1640, paymentMode: 'cash' })
// S2 B2B in Rajasthan: 20 soap = ₹2,360 → taxable 2,000, CGST 180, SGST 180.
const b2b = randomUUID()
await post('/customers', { id: b2b, name: 'Jaipur Hotel Pvt Ltd' })
await put(`/customers/${b2b}/gstin`, { gstin: gstin('08AAFCH3456N1Z') })
const s2 = await post('/bills', { id: randomUUID(), customerId: b2b, lines: [{ productId: soap.id, quantity: 20, unitPrice: 118 }], paidAmount: 2360, paymentMode: 'upi' })
// S3 B2B to Gujarat (24): 10 rice = ₹1,050 → taxable 1,000, IGST 50.
const guj = randomUUID()
await post('/customers', { id: guj, name: 'Ahmedabad Caterers' })
await put(`/customers/${guj}/gstin`, { gstin: gstin('24AAGCA7890P1Z') })
await put(`/customers/${guj}/state`, { state: '24' }).catch(() => null)
const s3 = await post('/bills', { id: randomUUID(), customerId: guj, lines: [{ productId: rice.id, quantity: 10, unitPrice: 105 }], paidAmount: 1050, paymentMode: 'upi' })
// S4 cancelled: must not count.
const s4id = randomUUID()
await post('/bills', { id: s4id, lines: [{ productId: rice.id, quantity: 5, unitPrice: 105 }], paidAmount: 525, paymentMode: 'cash' })
await post(`/bills/${s4id}/cancel`, { reason: 'test' })
// Credit note: 2 soap back from S2 → taxable 200, CGST 18, SGST 18.
const cn = await post(`/bills/${s2.bill?.id ?? s2.id}/credit-notes`, { id: randomUUID(), reason: 'damaged pack', lines: [{ lineNo: 1, quantity: 2 }], restock: true })

const tax = (b) => { const bill = b.bill ?? b; return bill.lines.reduce((a, l) => ({ t: a.t + (l.taxableValue ?? 0), c: a.c + (l.cgst ?? 0), s: a.s + (l.sgst ?? 0), i: a.i + (l.igst ?? 0) }), { t: 0, c: 0, s: 0, i: 0 }) }
const t1 = tax(s1), t2 = tax(s2), t3 = tax(s3)
check('S1 cash: taxable', t1.t, 1500); check('S1 cash: CGST', t1.c, 70); check('S1 cash: SGST', t1.s, 70); check('S1 cash: no IGST', t1.i, 0)
check('S2 B2B in state: taxable', t2.t, 2000); check('S2: CGST', t2.c, 180); check('S2: SGST', t2.s, 180)
check('S3 B2B to Gujarat: taxable', t3.t, 1000); check('S3: IGST', t3.i, 50); check('S3: no CGST', t3.c, 0)
note('S3 bill as saved', JSON.stringify({ placeOfSupply: (s3.bill ?? s3).placeOfSupply, buyerGstin: (s3.bill ?? s3).buyerGstin, billNo: (s3.bill ?? s3).billNo }))
check('credit note: CGST', cn.lines.reduce((a, l) => a + l.cgst, 0), 18)
note('credit note number', cn.noteNo)

// THE MONTH: GSTR-1 totals, HSN table, input credit and the GSTR-3B working.
const m = await get('/gst/month')
note('GST month', JSON.stringify({ bills: m.bills, cancelled: m.cancelled, sales: m.sales, taxable: m.taxable, cgst: m.cgst, sgst: m.sgst, igst: m.igst, b2b: m.b2bSales, creditNotes: m.creditNotes, cnValue: m.creditNoteValue, cnTax: m.creditNoteTax, purchaseBills: m.purchaseBills, inputTaxable: m.inputTaxable, inputGst: m.inputGst, checks: m.checks }))
check('GSTR-1: bills (cancelled left out)', m.bills, 3)
check('GSTR-1: cancelled', m.cancelled, 1)
check('GSTR-1: taxable before credit notes', m.taxable, 4500)
check('GSTR-1: CGST before credit notes', m.cgst, 250)
check('GSTR-1: SGST before credit notes', m.sgst, 250)
check('GSTR-1: IGST', m.igst, 50)
check('GSTR-1: B2B sales (with GST)', m.b2bSales, 3410)
check('credit notes: count', m.creditNotes, 1)
check('credit notes: tax', m.creditNoteTax, 36)
check('input: purchase bills', m.purchaseBills, 2)
check('input: taxable 4,000 + 3,500 + 1,400', m.inputTaxable, 8900)
check('input: GST 200 + 630 + 252', m.inputGst, 1082)

const pay = m.payable
note('GSTR-3B working', JSON.stringify(pay))
check('3B: output taxable net of the credit note', pay.outputTaxable, 4300)
check('3B: output CGST 250 − 18', pay.outputCgst, 232)
check('3B: output SGST', pay.outputSgst, 232)
check('3B: output IGST', pay.outputIgst, 50)
check('3B: input CGST', pay.inputCgst, 415)
check('3B: input SGST', pay.inputSgst, 415)
check('3B: input IGST', pay.inputIgst, 252)
check('3B: nothing to pay in cash (credit 1,082 > tax 514)', pay.payCgst + pay.paySgst + pay.payIgst, 0)
check('3B: credit carried forward = 1,082 − 514', pay.carryCgst + pay.carrySgst + pay.carryIgst, 568)
// IGST credit must be used first, on IGST then CGST/SGST, before CGST or SGST credit (sec. 49, rule 88A).
check('3B: IGST credit used up before CGST/SGST credit (none of it carried)', pay.carryIgst, 0)

const hsn = m.hsn ?? []
note('HSN table', JSON.stringify(hsn))
const row = (code) => hsn.filter((h) => h.hsn === code).reduce((a, h) => ({ q: a.q + h.quantity, t: a.t + h.taxable, tax: a.tax + h.cgst + h.sgst + h.igst }), { q: 0, t: 0, tax: 0 })
check('HSN 1006 rice: quantity 10 + 10', row('1006').q, 20)
check('HSN 1006 rice: taxable', row('1006').t, 2000)
check('HSN 3401 soap: quantity 5 + 20 − 2 returned', row('3401').q, 23)
check('HSN 3401 soap: taxable 500 + 2,000 − 200', row('3401').t, 2300)
check('HSN 3401 soap: tax 90 + 360 − 36', row('3401').tax, 414)

const pnl = await get('/reports/pnl')
note('P&L', JSON.stringify({ sales: pnl.sales, turnover: pnl.turnover, gst: pnl.gst, goodsProfit: pnl.goodsProfit, returns: pnl.returns }))
check('P&L: sales with GST 1,640 + 2,360 + 1,050, less the ₹236 credit note', pnl.sales, 4814)
check('P&L: GST in sales, net of the credit note (514)', pnl.gst, 514)
check('P&L: turnover without GST (4,300)', pnl.turnover, 4300)
// Profit before GST: rice 20 × (100 − 80) = 400; soap 25 × (100 − 70) = 750; less 2 soap returned × 30 = 60.
check('P&L: profit on goods less returns', pnl.goodsProfit - pnl.returns, 1090)

for (const c of checks) {
  const mark = c.ok === null ? 'info' : c.ok ? 'ok  ' : 'BUG '
  console.log(`${mark} ${c.name}: ${typeof c.actual === 'string' ? c.actual : `${c.actual}${c.ok === false ? ` (expected ${c.expected})` : ''}`}`)
}
console.log(`\n${checks.filter((c) => c.ok !== null).length} checks, ${checks.filter((c) => c.ok === false).length} wrong.`)

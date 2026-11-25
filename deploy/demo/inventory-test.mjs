// Inventory end to end: buy, sell, cancel, return, send back to the supplier, write off damage; then check stock and
// every report against numbers worked out by hand. Makes its own throwaway kirana shop. Run against a local API:
//   node deploy/demo/inventory-test.mjs            (GK_API defaults to http://localhost:5274/api/v1)
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
const checks = []
const near = (a, b) => Math.abs(Number(a) - Number(b)) < 0.011
function check(name, actual, expected) { checks.push({ name, ok: near(actual, expected), actual, expected }) }
function note(name, value) { checks.push({ name, ok: null, actual: value, expected: '' }) }

// A fresh shop.
const phone = `98998${String(Math.floor(Math.random() * 1e5)).padStart(5, '0')}`
const auth = await post('/auth/sign-up', { shopName: 'Inventory Test Kirana', fullName: 'Inv Tester', phone, password: 'secret123', language: 'en', type: 'kirana' })
token = auth.accessToken
if (!auth.shopId && auth.memberships?.[0]) token = (await post('/auth/select-shop', { membershipId: auth.memberships[0].membershipId })).accessToken

// Three items, no opening stock. Prices include GST (the app's rule).
const item = async (name, price, gst) => (await post('/products', { id: randomUUID(), name, unit: 'piece', sellingPrice: price, gstRate: gst }))
const sugar = await item('Test Sugar 1 kg', 50, 5)
const oil = await item('Test Oil 1 L', 180, 5)
const soap = await item('Test Soap', 40, 18)
const supplier = randomUUID()
await post('/suppliers', { id: supplier, name: 'Test Wholesaler' })

// BUY: 20 sugar @40, 10 oil @150, 30 soap @30 = ₹3,200 on credit.
const purchase = randomUUID()
const bought = await post('/suppliers/purchases', { id: purchase, supplierId: supplier, billNo: 'W-1', paidAmount: 0, lines: [
  { productId: sugar.id, quantity: 20, unitCost: 40 }, { productId: oil.id, quantity: 10, unitCost: 150 }, { productId: soap.id, quantity: 30, unitCost: 30 }] })
note('purchase total as saved', bought.total ?? bought.purchase?.total ?? JSON.stringify(bought).slice(0, 120))

const stockOf = async () => Object.fromEntries((await get('/products')).filter((p) => p.name.startsWith('Test ')).map((p) => [p.name, p]))
let s = await stockOf()
check('after buying: sugar stock', s['Test Sugar 1 kg'].stock, 20)
check('after buying: oil stock', s['Test Oil 1 L'].stock, 10)
check('after buying: soap stock', s['Test Soap'].stock, 30)
check('after buying: sugar cost price', s['Test Sugar 1 kg'].costPrice, 40)

// SELL: A cash 5 sugar + 10 soap = ₹650. B on khata 4 oil = ₹720, ₹200 paid. C 2 sugar, then cancelled.
const customer = randomUUID()
await post('/customers', { id: customer, name: 'Test Customer' })
const billA = randomUUID()
await post('/bills', { id: billA, lines: [{ productId: sugar.id, quantity: 5, unitPrice: 50 }, { productId: soap.id, quantity: 10, unitPrice: 40 }], paidAmount: 650, paymentMode: 'cash' })
await post('/bills', { id: randomUUID(), customerId: customer, lines: [{ productId: oil.id, quantity: 4, unitPrice: 180 }], paidAmount: 200, paymentMode: 'cash' })
const billC = randomUUID()
await post('/bills', { id: billC, lines: [{ productId: sugar.id, quantity: 2, unitPrice: 50 }], paidAmount: 100, paymentMode: 'upi' })
await post(`/bills/${billC}/cancel`, { reason: 'test cancel' })

// RETURN: 2 soap from bill A back on the shelf (₹80 refunded in cash).
const view = await get(`/bills/${billA}`)
const soapLineNo = (view.bill ?? view).lines.findIndex((l) => l.name.includes('Soap')) + 1 // lines are numbered 1, 2, 3… in bill order
await post(`/bills/${billA}/credit-notes`, { id: randomUUID(), reason: 'test return', lines: [{ lineNo: soapLineNo, quantity: 2 }], restock: true, refundMode: 'cash' })

// SEND BACK: 1 oil to the supplier. DAMAGE: 1 sugar written off.
await post('/suppliers/returns', { id: randomUUID(), supplierId: supplier, lines: [{ productId: oil.id, quantity: 1, batchId: null }] })
await post('/stock-movements', { id: randomUUID(), productId: sugar.id, kind: 'adjustment', quantity: -1, reason: 'damage', note: 'test damage' })

s = await stockOf()
check('stock: sugar 20 − 5 sold − 1 damaged (cancelled bill put back)', s['Test Sugar 1 kg'].stock, 14)
check('stock: oil 10 − 4 sold − 1 sent back', s['Test Oil 1 L'].stock, 5)
check('stock: soap 30 − 10 sold + 2 returned', s['Test Soap'].stock, 22)

// Money: sales ₹650 + ₹720 = ₹1,370, less the ₹80 return. Cash in ₹650 + ₹200, out ₹80. Udhaar ₹520.
const day = await get('/reports/day')
note('day report totals', JSON.stringify(day.totals))
check('day: sales (cancelled bill left out)', day.totals.sales, 1370)
check('day: bills counted', day.totals.bills, 2)
check('day: cancelled bills listed', day.cancelled.length, 1)
const statement = await get(`/customers/${customer}/statement`)
check('customer owes 720 − 200', statement.customer.balance, 520)

const cash = await get('/cash-count')
note('cash count', JSON.stringify(cash).slice(0, 300))
check('drawer: cash in − out = 650 + 200 − 80', cash.cashIn - cash.cashOut, 770)

const month = await get('/reports/month')
note('month report', JSON.stringify({ sales: month.sales, bills: month.bills, cash: month.cash, upi: month.upi, udhaar: month.udhaarGiven, returns: month.returns, stockLoss: month.stockLoss, purchases: month.stockPurchases, stockValue: month.stockValue, supplierDue: month.supplierDue, profit: month.estimatedProfit }))
check('month: sales', month.sales, 1370)
check('month: UPI (the cancelled bill took none)', month.upi, 0)
check('month: returns = profit given back on 2 soap (₹80 − ₹60 cost)', month.returns, 20)
check('month: stock bought from the drawer as kharcha (none; supplier bills are separate)', month.stockPurchases, 0)
check('month: stock value at cost 14×40 + 5×150 + 22×30', month.stockValue, 1970)
check('month: supplier due stays 3,200 until their credit note', month.supplierDue, 3200)
check('month: damage written off at cost', month.stockLoss, 40)
// Gross margin on what stayed sold: sugar 5×10, oil 4×30, soap 8×10 = ₹250.
check('month: profit = 270 on goods − 20 returns − 40 damaged', month.estimatedProfit, 210)

const items = await get('/reports/items')
note('items report', JSON.stringify(items).slice(0, 600))

const pnl = await get('/reports/pnl')
note('P&L', JSON.stringify({ sales: pnl.sales, turnover: pnl.turnover, gst: pnl.gst, goodsProfit: pnl.goodsProfit, returns: pnl.returns, stockLoss: pnl.stockLoss, purchases: pnl.purchases, opening: pnl.openingStock, closing: pnl.closingStock, net: pnl.netProfit, supplierDue: pnl.supplierDue, customerDue: pnl.customerDue }))
check('P&L: closing stock at cost', pnl.closingStock, 1970)
check('P&L: supplier bills (gross)', pnl.purchases, 3200)
check('P&L: customer due', pnl.customerDue, 520)
check('P&L: supplier due before their credit note', pnl.supplierDue, 3200)
// Books must balance: opening + purchases − closing = cost of goods that left (sold net + damaged).
check('P&L: opening + bills − closing = cost sold 1,040 + damaged 40 + 150 sent back', pnl.openingStock + pnl.purchases - pnl.closingStock, 1230)
// GST inside sales: sugar 250 @5%, oil 720 @5%, soap 320 @18% (after the return) = 11.90 + 34.29 + 48.81.
check('P&L: GST is 0 (the shop has no GSTIN, so its bills carry none)', pnl.gst, 0)

const supp = await get(`/suppliers/${supplier}`)
check('supplier due before their credit note', supp.supplier.due, 3200)


// ---------- Part 2: the awkward cases ----------
// The supplier's credit note for the oil sent back: ₹150 comes off what is owed.
const returnsList = await get('/suppliers/returns')
const openReturn = (returnsList.returns ?? returnsList).find((r) => r.supplierId === supplier)
await post(`/suppliers/returns/${openReturn.id}/credit-note`, { creditNoteNo: 'SCN-1', amount: 150 })
check('supplier balance after their credit note', (await get(`/suppliers/${supplier}`)).supplier.due, 3050)

// A second purchase of sugar at a new cost (₹44), then cancelled: stock, due and cost go back.
const p2 = randomUUID()
await post('/suppliers/purchases', { id: p2, supplierId: supplier, billNo: 'W-2', paidAmount: 0, lines: [{ productId: sugar.id, quantity: 10, unitCost: 44 }] })
s = await stockOf()
check('2nd purchase: sugar stock 14 + 10', s['Test Sugar 1 kg'].stock, 24)
note('2nd purchase: sugar cost price now (moving average or last cost?)', s['Test Sugar 1 kg'].costPrice)
await post(`/suppliers/purchases/${p2}/cancel`, {})
s = await stockOf()
check('purchase cancelled: sugar stock back to 14', s['Test Sugar 1 kg'].stock, 14)
check('purchase cancelled: sugar cost back to 40 (to the rupee)', Math.round(s['Test Sugar 1 kg'].costPrice), 40)
check('purchase cancelled: supplier balance back to 3,050', (await get(`/suppliers/${supplier}`)).supplier.due, 3050)

// Stock count: 12 sugar on the shelf where the books say 14.
await post('/stocktakes', { id: randomUUID(), counts: [{ productId: sugar.id, counted: 12 }] })
s = await stockOf()
check('stock count: sugar set to 12', s['Test Sugar 1 kg'].stock, 12)

// Cancelling bill A after 2 of its 10 soaps came back must be refused (the rest goes back on a credit note instead).
{
  const r = await fetch(`${base}/bills/${billA}/cancel`, { method: 'POST', headers: { 'content-type': 'application/json', authorization: `Bearer ${token}` }, body: JSON.stringify({ reason: 'after return' }) })
  check('cancelling a bill after a return is refused (409)', r.status, 409)
}
s = await stockOf()
check('refused cancel changed nothing: soap still 22', s['Test Soap'].stock, 22)

// Selling more than in stock: 8 oil where 5 are left.
let oversell
try { oversell = await post('/bills', { id: randomUUID(), lines: [{ productId: oil.id, quantity: 8, unitPrice: 180 }], paidAmount: 1440, paymentMode: 'cash' }); note('oversell 8 oil with 5 in stock', 'allowed') } catch (e) { note('oversell 8 oil with 5 in stock', `refused: ${String(e.message).slice(0, 120)}`) }
s = await stockOf()
note('oil stock after the oversell', s['Test Oil 1 L'].stock)
const month2 = await get('/reports/month')
note('month: stock value with oil below zero', month2.stockValue)
check('month: negative stock is not counted as value (12×40 + 22×30, to the rupee: the average cost keeps 2 decimals)', Math.round(month2.stockValue), 12 * 40 + 22 * 30)

// An item with no cost price: sold, so profit coverage drops below 100%.
const loose = await item('Test Loose Item', 100, 0)
await post('/bills', { id: randomUUID(), lines: [{ productId: loose.id, quantity: 1, unitPrice: 100 }], paidAmount: 100, paymentMode: 'cash' })
const items2 = await get('/reports/items')
note('items report: loose item (no cost)', JSON.stringify(items2.items.find((i) => i.name === 'Test Loose Item')))
note('items report: profit coverage', items2.profitCoverage)
const pnl2 = await get('/reports/pnl')
note('P&L after part 2', JSON.stringify({ sales: pnl2.sales, goodsProfit: pnl2.goodsProfit, returns: pnl2.returns, stockLoss: pnl2.stockLoss, purchases: pnl2.purchases, closing: pnl2.closingStock, net: pnl2.netProfit, itemsWithoutCost: pnl2.itemsWithoutCost, coverage: pnl2.profitCoverage, supplierDue: pnl2.supplierDue }))
check('P&L: stock loss = 1 damaged + 2 short in the count, at ₹40 (to the rupee)', Math.round(pnl2.stockLoss), 120)
check('P&L: supplier due after credit note', pnl2.supplierDue, 3050)

for (const c of checks) {
  const mark = c.ok === null ? 'info' : c.ok ? 'ok  ' : 'BUG '
  console.log(`${mark} ${c.name}: ${typeof c.actual === 'string' ? c.actual : `${c.actual}${c.ok === false ? ` (expected ${c.expected})` : ''}`}`)
}
console.log(`\n${checks.filter((c) => c.ok !== null).length} checks, ${checks.filter((c) => c.ok === false).length} wrong.`)

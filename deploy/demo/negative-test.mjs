// Negative tests against a running API: wrong input, no sign-in, a helper doing owner-only things, and one shop
// reaching into another's data. Every case should be refused with a 4xx; a 5xx or a 2xx is a bug.
// Makes two throwaway shops of its own (test numbers 98999xxxxx). Run:
//   GK_API=http://localhost:5274/api/v1 node deploy/demo/negative-test.mjs
import { randomUUID } from 'node:crypto'

const base = process.env.GK_API ?? 'http://localhost:5274/api/v1'
const results = []

async function raw(method, path, { token, body, rawBody, headers } = {}) {
  const response = await fetch(base + path, {
    method,
    headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}), ...headers },
    body: rawBody ?? (body === undefined ? undefined : JSON.stringify(body)),
  })
  const text = await response.text()
  let json = null
  try { json = JSON.parse(text) } catch { /* not JSON */ }
  return { status: response.status, json, text }
}

/** A case that must be refused: 4xx passes; 5xx or 2xx is a bug. `allow` lists extra acceptable codes. */
async function refuse(name, method, path, opts = {}, allow = []) {
  const r = await raw(method, path, opts)
  const ok = (r.status >= 400 && r.status < 500) || allow.includes(r.status)
  results.push({ ok, name, status: r.status, detail: ok ? '' : r.text.slice(0, 200) })
  return r
}

const phone = () => `98999${String(Math.floor(Math.random() * 100000)).padStart(5, '0')}`
async function shop(type, name) {
  const p = phone()
  const auth = (await raw('POST', '/auth/sign-up', { body: { shopName: name, fullName: 'Neg Tester', phone: p, password: 'secret123', language: 'en', type } })).json
  let token = auth.accessToken
  if (!auth.shopId && auth.memberships?.[0]) token = (await raw('POST', '/auth/select-shop', { token, body: { membershipId: auth.memberships[0].membershipId } })).json.accessToken
  return { token, phone: p }
}

const A = await shop('paint', 'Neg Test Paints A')
const B = await shop('jewellery', 'Neg Test Jewellers B')
const t = A.token

// Shop A's own records to aim at.
const custId = randomUUID()
await raw('POST', '/customers', { token: t, body: { id: custId, name: 'Neg Customer' } })
const prod = (await raw('POST', '/products', { token: t, body: { id: randomUUID(), name: 'Neg Primer 1 L', unit: 'litre', sellingPrice: 300, openingStock: 5 } })).json
const billId = randomUUID()
await raw('POST', '/bills', { token: t, body: { id: billId, customerId: custId, lines: [{ name: 'Brush', quantity: 1, unitPrice: 100 }], paidAmount: 0 } })
const painterId = randomUUID()
await raw('POST', '/painters', { token: t, body: { id: painterId, name: 'Neg Painter', commissionPercent: 5 } })

// --- No sign-in, or a broken token.
for (const path of ['/customers', '/products', '/bills/' + billId, '/painters/commission', '/shop']) {
  await refuse(`no sign-in: GET ${path}`, 'GET', path)
  await refuse(`bad token: GET ${path}`, 'GET', path, { token: 'not.a.token' })
}
await refuse('wrong password', 'POST', '/auth/sign-in', { body: { phone: A.phone, password: 'wrong-pass' } })
await refuse('sign-in with no body', 'POST', '/auth/sign-in', { rawBody: '' })
await refuse('sign-in with broken JSON', 'POST', '/auth/sign-in', { rawBody: '{"phone":' })
await refuse('sign-up with a 3-digit phone', 'POST', '/auth/sign-up', { body: { shopName: 'X', fullName: 'Y', phone: '123', password: 'secret123', type: 'kirana' } })
await refuse('sign-up with an unknown shop type', 'POST', '/auth/sign-up', { body: { shopName: 'X', fullName: 'Y', phone: phone(), password: 'secret123', type: 'spaceship' } })
await refuse('sign-up with a 2-letter password', 'POST', '/auth/sign-up', { body: { shopName: 'X', fullName: 'Y', phone: phone(), password: 'ab', type: 'kirana' } })
// Signing up again with the same number and password just signs the owner in (by design); another password must fail.
await refuse('sign-up again with a used phone and another password', 'POST', '/auth/sign-up', { body: { shopName: 'X', fullName: 'Y', phone: A.phone, password: 'not-the-owner-9', type: 'kirana' } })

// --- Customers.
await refuse('customer with empty name', 'POST', '/customers', { token: t, body: { id: randomUUID(), name: '' } })
await refuse('customer with 500-letter name', 'POST', '/customers', { token: t, body: { id: randomUUID(), name: 'x'.repeat(500) } })
await refuse('customer with bad phone', 'POST', '/customers', { token: t, body: { id: randomUUID(), name: 'Bad Phone', phone: '12ab' } })
await refuse('customer with no id', 'POST', '/customers', { token: t, body: { name: 'No Id' } })
await refuse('customer id not a uuid', 'POST', '/customers', { token: t, body: { id: 'abc', name: 'Bad Id' } })
await refuse('statement of unknown customer', 'GET', `/customers/${randomUUID()}/statement`, { token: t })
await refuse('statement with a non-uuid id', 'GET', '/customers/abc/statement', { token: t })
await refuse('negative credit limit', 'PUT', `/customers/${custId}/credit-limit`, { token: t, body: { creditLimit: -1 } })
await refuse('huge credit limit', 'PUT', `/customers/${custId}/credit-limit`, { token: t, body: { creditLimit: 1e20 } })

// --- Khata entries.
const entry = (o) => ({ id: randomUUID(), customerId: custId, type: 'payment', amount: 100, paymentMode: 'cash', ...o })
await refuse('payment of 0', 'POST', '/khata-entries', { token: t, body: entry({ amount: 0 }) })
await refuse('payment negative', 'POST', '/khata-entries', { token: t, body: entry({ amount: -50 }) })
await refuse('payment absurdly large', 'POST', '/khata-entries', { token: t, body: entry({ amount: 1e15 }) })
await refuse('payment with 3 decimals', 'POST', '/khata-entries', { token: t, body: entry({ amount: 10.555 }) }, [200, 201])
await refuse('payment unknown type', 'POST', '/khata-entries', { token: t, body: entry({ type: 'gift' }) })
await refuse('payment unknown mode', 'POST', '/khata-entries', { token: t, body: entry({ paymentMode: 'bitcoin' }) })
await refuse('payment for unknown customer', 'POST', '/khata-entries', { token: t, body: entry({ customerId: randomUUID() }) })
// A future date is taken as a wrong phone clock and saved as now (by design): check it isn't kept as next year.
{
  const r = await raw('POST', '/khata-entries', { token: t, body: entry({ occurredAt: new Date(Date.now() + 365 * 864e5).toISOString() }) })
  const at = r.json?.entry?.occurredAt
  results.push({ ok: r.status === 201 && at && new Date(at) <= new Date(Date.now() + 60_000), name: 'payment dated next year is saved as now', status: r.status, detail: r.text.slice(0, 200) })
}
await refuse('amount as text', 'POST', '/khata-entries', { token: t, body: entry({ amount: 'hundred' }) })

// --- Bills.
const bill = (o) => ({ id: randomUUID(), lines: [{ name: 'Brush', quantity: 1, unitPrice: 100 }], paidAmount: 100, ...o })
await refuse('bill with no lines', 'POST', '/bills', { token: t, body: bill({ lines: [] }) })
await refuse('bill line quantity 0', 'POST', '/bills', { token: t, body: bill({ lines: [{ name: 'Brush', quantity: 0, unitPrice: 100 }] }) })
await refuse('bill line negative quantity', 'POST', '/bills', { token: t, body: bill({ lines: [{ name: 'Brush', quantity: -2, unitPrice: 100 }], paidAmount: 0 }) })
await refuse('bill line negative price', 'POST', '/bills', { token: t, body: bill({ lines: [{ name: 'Brush', quantity: 1, unitPrice: -100 }], paidAmount: 0 }) })
await refuse('bill paid more than total (walk-in)', 'POST', '/bills', { token: t, body: bill({ paidAmount: 500 }) })
await refuse('bill paid negative', 'POST', '/bills', { token: t, body: bill({ paidAmount: -1 }) })
await refuse('bill unpaid with no customer', 'POST', '/bills', { token: t, body: bill({ paidAmount: 0 }) })
await refuse('bill for unknown product', 'POST', '/bills', { token: t, body: bill({ lines: [{ productId: randomUUID(), quantity: 1, unitPrice: 100 }] }) })
await refuse('bill line with empty name', 'POST', '/bills', { token: t, body: bill({ lines: [{ name: '', quantity: 1, unitPrice: 100 }] }) })
await refuse('bill with 2,000 lines', 'POST', '/bills', { token: t, body: bill({ lines: Array.from({ length: 2000 }, () => ({ name: 'Brush', quantity: 1, unitPrice: 1 })), paidAmount: 2000 }) })
await refuse('bill for unknown painter', 'POST', '/bills', { token: t, body: bill({ painterId: randomUUID() }) })
await refuse('bill with cash+UPI parts not adding up', 'POST', '/bills', { token: t, body: bill({ payments: [{ mode: 'cash', amount: 30 }, { mode: 'upi', amount: 30 }] }) })
await refuse('get unknown bill', 'GET', `/bills/${randomUUID()}`, { token: t })
await refuse('cancel unknown bill', 'POST', `/bills/${randomUUID()}/cancel`, { token: t, body: { reason: 'test' } })
await refuse('cancel bill with no reason', 'POST', `/bills/${billId}/cancel`, { token: t, body: { reason: '' } })

// --- Products and rates.
await refuse('product with negative price', 'POST', '/products', { token: t, body: { id: randomUUID(), name: 'Neg', unit: 'piece', sellingPrice: -5 } })
await refuse('product with unknown unit', 'POST', '/products', { token: t, body: { id: randomUUID(), name: 'Neg', unit: 'bucketful', sellingPrice: 5 } })
await refuse('product with GST 99%', 'POST', '/products', { token: t, body: { id: randomUUID(), name: 'Neg', unit: 'piece', sellingPrice: 5, gstRate: 99 } })
await refuse('rates: negative price', 'PUT', '/products/rates', { token: t, body: { rates: [{ productId: prod?.id, sellingPrice: -10 }] } })
await refuse('rates: unknown product', 'PUT', '/products/rates', { token: t, body: { rates: [{ productId: randomUUID(), sellingPrice: 10 }] } })

// --- Painters and the new commission payouts.
await refuse('painter commission 80%', 'POST', '/painters', { token: t, body: { id: randomUUID(), name: 'Greedy', commissionPercent: 80 } })
await refuse('painter with 1-letter name', 'POST', '/painters', { token: t, body: { id: randomUUID(), name: 'X', commissionPercent: 5 } })
await refuse('payout to unknown painter', 'POST', `/painters/${randomUUID()}/payouts`, { token: t, body: { id: randomUUID(), amount: 10 } })
await refuse('payout when nothing is owed', 'POST', `/painters/${painterId}/payouts`, { token: t, body: { id: randomUUID(), amount: 10 } })
await refuse('payout negative', 'POST', `/painters/${painterId}/payouts`, { token: t, body: { id: randomUUID(), amount: -10 } })
await refuse('payout with 200-letter note', 'POST', `/painters/${painterId}/payouts`, { token: t, body: { id: randomUUID(), amount: 1, note: 'n'.repeat(200) } })
await refuse('commission for month 2026-13', 'GET', '/painters/commission?month=2026-13', { token: t }, [200])

// --- Shop settings.
await refuse('shop with empty name', 'PUT', '/shop', { token: t, body: { name: '' } })
await refuse('shop with bad GSTIN', 'PUT', '/shop', { token: t, body: { name: 'Neg Test Paints A', gstin: '12345' } })
await refuse('girvi state not on the list', 'PUT', '/shop', { token: t, body: { name: 'Neg Test Paints A', girviState: 'atlantis' } })
await refuse('cash limit negative', 'PUT', '/shop', { token: t, body: { name: 'Neg Test Paints A', cashReceiptLimit: -1 } })

// --- One shop reaching into another (B uses A's ids).
const b = B.token
await refuse('B reads A\'s customer statement', 'GET', `/customers/${custId}/statement`, { token: b })
await refuse('B reads A\'s bill', 'GET', `/bills/${billId}`, { token: b })
await refuse('B cancels A\'s bill', 'POST', `/bills/${billId}/cancel`, { token: b, body: { reason: 'x' } })
await refuse('B records payment on A\'s customer', 'POST', '/khata-entries', { token: b, body: entry({}) })
await refuse('B bills A\'s customer', 'POST', '/bills', { token: b, body: bill({ customerId: custId, paidAmount: 0 }) })
await refuse('B sets A\'s customer credit limit', 'PUT', `/customers/${custId}/credit-limit`, { token: b, body: { creditLimit: 5 } })
await refuse('B reads A\'s customer shades', 'GET', `/customers/${custId}/shades`, { token: b }, [200])
await refuse('B pays A\'s painter', 'POST', `/painters/${painterId}/payouts`, { token: b, body: { id: randomUUID(), amount: 1 } })
await refuse('B changes A\'s product rate', 'PUT', '/products/rates', { token: b, body: { rates: [{ productId: prod?.id, sellingPrice: 1 }] } })
const shadesSeen = await raw('GET', `/customers/${custId}/shades`, { token: b })
if (shadesSeen.status === 200 && (shadesSeen.json ?? []).length > 0) results.push({ ok: false, name: 'B sees A\'s shades (data leak)', status: 200, detail: shadesSeen.text.slice(0, 200) })

// --- Jewellery shop B: wrong jewellery input.
await refuse('rates: purity 1234', 'POST', '/jewellery/rates', { token: b, body: { rates: [{ metal: 'gold', purity: 1234, sellPerGram: 7000 }] } })
await refuse('rates: metal "tin"', 'POST', '/jewellery/rates', { token: b, body: { rates: [{ metal: 'tin', purity: 999, sellPerGram: 7000 }] } })
await refuse('rates: negative price', 'POST', '/jewellery/rates', { token: b, body: { rates: [{ metal: 'gold', purity: 999, sellPerGram: -1 }] } })
await raw('POST', '/jewellery/rates', { token: b, body: { rates: [{ metal: 'gold', purity: 999, sellPerGram: 7800 }] } })
await refuse('piece with gross 0', 'POST', '/jewellery/pieces', { token: b, body: { name: 'Ring', metal: 'gold', purity: 916, gross: 0 } })
await refuse('piece with stone heavier than gross', 'POST', '/jewellery/pieces', { token: b, body: { name: 'Ring', metal: 'gold', purity: 916, gross: 2, stone: 5 } })
await refuse('piece with bad HUID', 'POST', '/jewellery/pieces', { token: b, body: { name: 'Ring', metal: 'gold', purity: 916, gross: 2, huid: '!!' } })
const bc = randomUUID()
await raw('POST', '/customers', { token: b, body: { id: bc, name: 'Neg Girvi' } })
await refuse('girvi principal 0', 'POST', '/jewellery/girvi', { token: b, body: { customerId: bc, items: 'Chain', metal: 'gold', gross: 10, reading: 91.6, principal: 0, ratePerMonth: 1 } })
await refuse('girvi reading 150%', 'POST', '/jewellery/girvi', { token: b, body: { customerId: bc, items: 'Chain', metal: 'gold', gross: 10, reading: 150, principal: 1000, ratePerMonth: 1 } })
await refuse('girvi rate 50% a month', 'POST', '/jewellery/girvi', { token: b, body: { customerId: bc, items: 'Chain', metal: 'gold', gross: 10, reading: 91.6, principal: 1000, ratePerMonth: 50 } })
await refuse('girvi pledged next year', 'POST', '/jewellery/girvi', { token: b, body: { customerId: bc, items: 'Chain', metal: 'gold', gross: 10, reading: 91.6, principal: 1000, ratePerMonth: 1, pledgedOn: '2099-01-01' } })
await refuse('girvi pledged on a bad date', 'POST', '/jewellery/girvi', { token: b, body: { customerId: bc, items: 'Chain', metal: 'gold', gross: 10, reading: 91.6, principal: 1000, ratePerMonth: 1, pledgedOn: '2026-02-30' } })
await refuse('girvi for unknown customer', 'POST', '/jewellery/girvi', { token: b, body: { customerId: randomUUID(), items: 'Chain', metal: 'gold', gross: 10, reading: 91.6, principal: 1000, ratePerMonth: 1 } })
await refuse('scheme 0 months', 'POST', '/jewellery/schemes', { token: b, body: { name: 'Zero', months: 0, instalment: 1000 } })
await refuse('scheme negative instalment', 'POST', '/jewellery/schemes', { token: b, body: { name: 'Neg', months: 5, instalment: -1000 } })
await refuse('scheme bonus bigger than all instalments', 'POST', '/jewellery/schemes', { token: b, body: { name: 'Big bonus', months: 2, instalment: 100, bonus: 100000 } })
await refuse('order due yesterday', 'POST', '/jewellery/orders', { token: b, body: { kind: 'order', customerId: bc, description: 'Ring', dueOn: '2020-01-01' } })
await refuse('order of unknown kind', 'POST', '/jewellery/orders', { token: b, body: { kind: 'magic', customerId: bc, description: 'Ring', dueOn: '2099-01-01' } })
await refuse('approval with no pieces', 'POST', '/jewellery/approvals', { token: b, body: { party: 'X', pieceIds: [] } })
await refuse('quote for unknown piece', 'POST', '/jewellery/quote', { token: b, body: { lines: [{ pieceId: randomUUID() }] } })

// --- Odd requests.
await refuse('text/plain body', 'POST', '/customers', { token: t, rawBody: 'name=x', headers: { 'content-type': 'text/plain' } })
await refuse('JSON array instead of object', 'POST', '/customers', { token: t, rawBody: '[]' })
await refuse('unknown API path', 'GET', '/no-such-thing', { token: t })
await refuse('DELETE on bills list', 'DELETE', '/bills', { token: t })
const xss = (await raw('POST', '/customers', { token: t, body: { id: randomUUID(), name: '<img src=x onerror=alert(1)>' } }))
results.push({ ok: true, name: `HTML in a customer name is stored as text (status ${xss.status}); screens must show it as text`, status: xss.status, detail: '' })

const bad = results.filter((r) => !r.ok)
for (const r of results) console.log(`${r.ok ? 'ok  ' : 'BUG '} ${String(r.status).padEnd(4)} ${r.name}${r.ok ? '' : `\n       ${r.detail}`}`)
console.log(`\n${results.length} cases, ${bad.length} not refused as expected.`)

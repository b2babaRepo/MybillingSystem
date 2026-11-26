// Supermarket rush load test: ONE shop, many tills billing at the same moment.
//
//   node loadtest/supermarket-rush.mjs [--base http://localhost:5274] [--tills 20] [--items 20000] [--seconds 60]
//                                      [--min-lines 20] [--max-lines 60] [--think 0]
//
// Unlike evening-rush.mjs (many small shops), this is the supermarket case: every till sells from one shop's stock
// and takes the next number from one bill series. The shop gets --items products (imported 2,000 at a time); then
// each till bills back to back for --seconds, with --think ms between bills (0 = a worst case with no pause), each
// bill 20-60 lines. It also times downloading the whole item list, as each till does when it opens.
//
// Pass mark: bill p95 under 1,000 ms and no errors. Reports bills per minute so a counter's real pace (one bill every
// 30-60 s) can be compared: 20 tills at one bill a minute each is 20 bills a minute.
//
// Run against a test API with its sign-up and global rate limits raised:
//   Auth__AuthRequestsPerMinutePerIp=100000  Security__RequestsPerMinutePerIp=10000000

const args = Object.fromEntries(process.argv.slice(2).reduce((pairs, value, i, all) =>
  value.startsWith('--') ? [...pairs, [value.slice(2), all[i + 1]]] : pairs, []))
const BASE = args.base ?? 'http://localhost:5274'
const TILLS = Number(args.tills ?? 20)
const ITEMS = Number(args.items ?? 20000)
const SECONDS = Number(args.seconds ?? 60)
const MIN_LINES = Number(args['min-lines'] ?? 20)
const MAX_LINES = Number(args['max-lines'] ?? 60)
const THINK_MS = Number(args.think ?? 0)
const P95_TARGET_MS = 1000

const timings = new Map()
const errors = new Map()
const samples = []

async function call(action, token, method, path, body) {
  const started = performance.now()
  let ok = false
  try {
    const response = await fetch(BASE + path, {
      method,
      headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
    ok = response.ok
    const text = await response.text()
    if (!ok) {
      if (samples.length < 5) samples.push(`${action} ${response.status} ${text.slice(0, 160)}`)
      throw new Error(`${action} ${response.status}`)
    }
    return { body: text ? JSON.parse(text) : null, bytes: text.length }
  } finally {
    const list = timings.get(action) ?? []
    list.push(performance.now() - started)
    timings.set(action, list)
    if (!ok) errors.set(action, (errors.get(action) ?? 0) + 1)
  }
}

const id = () => crypto.randomUUID()
const pick = (list) => list[Math.floor(Math.random() * list.length)]
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms))
const phone = () => '9' + String(Math.floor(Math.random() * 1e9)).padStart(9, '0')
const pct = (sorted, p) => sorted.length ? sorted[Math.min(sorted.length - 1, Math.floor(sorted.length * p))] : 0

async function setUp() {
  const owner = await call('setup', null, 'POST', '/api/v1/auth/sign-up', {
    shopName: 'Load Supermarket', fullName: 'Owner', phone: phone(), password: crypto.randomUUID(), type: 'kirana',
  })
  const token = owner.body.accessToken
  for (let start = 0; start < ITEMS; start += 2000) {
    const rows = Array.from({ length: Math.min(2000, ITEMS - start) }, (_, k) => {
      const i = start + k
      return { name: `Item ${String(i).padStart(5, '0')}`, unit: i % 4 === 0 ? 'kg' : 'packet', sellingPrice: 10 + (i % 300), costPrice: 8 + (i % 250), openingStock: 100000 }
    })
    await call('setup', token, 'POST', '/api/v1/products/import', { rows })
  }
  const list = await call('catalogue', token, 'GET', '/api/v1/products')
  console.log(`Shop ready: ${list.body.length} items; item list ${(list.bytes / 1024 / 1024).toFixed(1)} MB in ${Math.round(timings.get('catalogue')[0])} ms`)
  // Every till uses the owner's sign-in: a trial shop's plan caps helpers, and the server's work per bill is the same.
  const tills = Array.from({ length: TILLS }, () => token)
  return { token, products: list.body, tills }
}

async function till(shop, token, until, counts) {
  while (Date.now() < until) {
    const n = MIN_LINES + Math.floor(Math.random() * (MAX_LINES - MIN_LINES + 1))
    const lines = Array.from({ length: n }, () => {
      const p = pick(shop.products)
      return { productId: p.id, quantity: 1 + Math.floor(Math.random() * 3), unitPrice: p.sellingPrice }
    })
    const total = lines.reduce((sum, l) => sum + Math.round(l.quantity * l.unitPrice * 100) / 100, 0)
    try {
      await call('bill', token, 'POST', '/api/v1/bills', { id: id(), lines, paidAmount: Math.round(total * 100) / 100, paymentMode: 'cash' })
      counts.bills++
    } catch { /* counted in errors */ }
    if (THINK_MS) await sleep(THINK_MS)
  }
}

const shop = await setUp()
console.log(`Billing: ${TILLS} tills, ${MIN_LINES}-${MAX_LINES} lines a bill, ${THINK_MS} ms between bills, ${SECONDS} s`)
const counts = { bills: 0 }
const until = Date.now() + SECONDS * 1000
await Promise.all(shop.tills.map((t) => till(shop, t, until, counts)))

// Catalogue download while tills are idle again: what a till opening the app waits for.
for (let i = 0; i < 3; i++) await call('catalogue', shop.tills[0], 'GET', '/api/v1/products')

console.log('\naction       count   p50 ms   p95 ms   max ms   errors')
let failed = false
for (const [action, list] of timings) {
  const sorted = [...list].sort((a, b) => a - b)
  const e = errors.get(action) ?? 0
  console.log(`${action.padEnd(10)} ${String(list.length).padStart(7)} ${String(Math.round(pct(sorted, 0.5))).padStart(8)} ${String(Math.round(pct(sorted, 0.95))).padStart(8)} ${String(Math.round(sorted.at(-1))).padStart(8)} ${String(e).padStart(8)}`)
  if (action === 'bill' && (pct(sorted, 0.95) > P95_TARGET_MS || e > 0)) failed = true
}
console.log(`\nBills saved: ${counts.bills} in ${SECONDS} s = ${Math.round(counts.bills / SECONDS * 60)} a minute (${(counts.bills / SECONDS / TILLS * 60).toFixed(1)} per till per minute)`)
if (samples.length) console.log('First errors:\n  ' + samples.join('\n  '))
console.log(failed ? `FAIL: bill p95 over ${P95_TARGET_MS} ms or errors` : 'PASS')
process.exit(failed ? 1 : 0)

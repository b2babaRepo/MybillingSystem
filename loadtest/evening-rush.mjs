// Evening-rush load test (project plan, Phase 7): many shops billing at once.
//
//   node loadtest/evening-rush.mjs [--base http://localhost:5274] [--shops 25] [--cashiers 2] [--seconds 60]
//
// Each shop gets products and customers; then every cashier loops for the duration with 1-2 s of thinking time,
// doing what a counter does in the evening: bills (some on khata), udhaar/jama entries, customer and item lists,
// statements. Pass mark: p95 under 500 ms and under 1% errors. Needs Node 22+, nothing else.
//
// Run against a test API with its sign-up and global rate limits raised (a real shop never signs up 25 shops a
// minute from one IP):  Auth__AuthRequestsPerMinutePerIp=100000  Security__RequestsPerMinutePerIp=10000000

const args = Object.fromEntries(process.argv.slice(2).reduce((pairs, value, i, all) =>
  value.startsWith('--') ? [...pairs, [value.slice(2), all[i + 1]]] : pairs, []))
const BASE = args.base ?? 'http://localhost:5274'
const SHOPS = Number(args.shops ?? 25)
const CASHIERS = Number(args.cashiers ?? 2)
const SECONDS = Number(args.seconds ?? 60)
const P95_TARGET_MS = 500

const timings = new Map() // action -> durations (ms)
const errors = new Map() // action -> count

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
    if (!ok) throw new Error(`${action} ${response.status} ${text.slice(0, 120)}`)
    return text ? JSON.parse(text) : null
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

async function setUpShop(n) {
  const auth = await call('setup', null, 'POST', '/api/v1/auth/sign-up', {
    shopName: `Load Shop ${n}`, fullName: `Owner ${n}`, phone: phone(), password: crypto.randomUUID(),
  })
  const token = auth.accessToken
  const rows = Array.from({ length: 40 }, (_, i) => ({
    name: `Item ${i}`, unit: i % 3 === 0 ? 'kg' : 'packet', sellingPrice: 10 + i * 5, costPrice: 8 + i * 4, openingStock: 500,
  }))
  await call('setup', token, 'POST', '/api/v1/products/import', { rows })
  const products = await call('setup', token, 'GET', '/api/v1/products')
  const customers = []
  for (let i = 0; i < 25; i++) {
    const customerId = id()
    await call('setup', token, 'POST', '/api/v1/customers', { id: customerId, name: `Customer ${i}`, phone: phone() })
    customers.push(customerId)
  }
  return { token, products, customers }
}

async function cashier(shop, until) {
  while (Date.now() < until) {
    const roll = Math.random()
    if (roll < 0.45) {
      const onKhata = Math.random() < 0.4
      const lines = Array.from({ length: 1 + Math.floor(Math.random() * 5) }, () => {
        const p = pick(shop.products)
        return { productId: p.id, quantity: 1 + Math.floor(Math.random() * 3), unitPrice: p.sellingPrice }
      })
      const total = lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0)
      await call('bill', shop.token, 'POST', '/api/v1/bills', {
        id: id(), customerId: onKhata ? pick(shop.customers) : null, lines, paidAmount: onKhata ? 0 : total, paymentMode: 'cash',
      })
    } else if (roll < 0.65) {
      await call('khata entry', shop.token, 'POST', '/api/v1/khata-entries', {
        id: id(), customerId: pick(shop.customers), type: Math.random() < 0.6 ? 'credit' : 'payment', amount: 20 + Math.floor(Math.random() * 500),
      })
    } else if (roll < 0.8) {
      await call('customers', shop.token, 'GET', '/api/v1/customers')
    } else if (roll < 0.9) {
      await call('popular items', shop.token, 'GET', '/api/v1/products?sort=popular')
    } else if (roll < 0.95) {
      await call('statement', shop.token, 'GET', `/api/v1/customers/${pick(shop.customers)}/statement`)
    } else {
      await call('today', shop.token, 'GET', '/api/v1/dashboard/today')
    }
    await sleep(1000 + Math.random() * 1000)
  }
}

const percentile = (sorted, p) => sorted[Math.min(sorted.length - 1, Math.floor((p / 100) * sorted.length))] ?? 0

console.log(`Setting up ${SHOPS} shops at ${BASE} …`)
const shops = []
for (let i = 0; i < SHOPS; i += 5) {
  shops.push(...await Promise.all(Array.from({ length: Math.min(5, SHOPS - i) }, (_, k) => setUpShop(i + k + 1))))
}
timings.delete('setup')

console.log(`Evening rush: ${SHOPS} shops × ${CASHIERS} cashiers for ${SECONDS} s …`)
const started = Date.now()
const until = started + SECONDS * 1000
await Promise.all(shops.flatMap((shop) => Array.from({ length: CASHIERS }, () => cashier(shop, until).catch((e) => {
  console.error(e.message)
}))))
const elapsed = (Date.now() - started) / 1000

let all = []
const rows = [...timings.entries()].map(([action, list]) => {
  all = all.concat(list)
  const sorted = [...list].sort((a, b) => a - b)
  return { action, requests: list.length, p50: percentile(sorted, 50), p95: percentile(sorted, 95), p99: percentile(sorted, 99), errors: errors.get(action) ?? 0 }
})
const sortedAll = all.sort((a, b) => a - b)
const totalErrors = [...errors.entries()].filter(([a]) => a !== 'setup').reduce((sum, [, n]) => sum + n, 0)
rows.push({ action: 'ALL', requests: sortedAll.length, p50: percentile(sortedAll, 50), p95: percentile(sortedAll, 95), p99: percentile(sortedAll, 99), errors: totalErrors })

console.log('\naction           requests   p50 ms   p95 ms   p99 ms   errors')
for (const r of rows) {
  console.log(`${r.action.padEnd(16)} ${String(r.requests).padStart(8)} ${r.p50.toFixed(0).padStart(8)} ${r.p95.toFixed(0).padStart(8)} ${r.p99.toFixed(0).padStart(8)} ${String(r.errors).padStart(8)}`)
}
const all95 = percentile(sortedAll, 95)
const errorRate = totalErrors / Math.max(1, sortedAll.length)
console.log(`\n${(sortedAll.length / elapsed).toFixed(1)} requests/s · p95 ${all95.toFixed(0)} ms (target < ${P95_TARGET_MS}) · errors ${(errorRate * 100).toFixed(2)}% (target < 1%)`)
const pass = all95 < P95_TARGET_MS && errorRate < 0.01
console.log(pass ? 'PASS' : 'FAIL')
process.exit(pass ? 0 : 1)

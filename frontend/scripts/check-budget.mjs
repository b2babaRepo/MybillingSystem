// Performance budget from the project plan: the JavaScript a phone must download before the first screen
// shows (the entry script plus everything index.html preloads), gzipped. Run after `npm run build`.
// Screens loaded on demand don't count: they arrive later and are then cached by the service worker.
import { readdirSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { gzipSync } from 'node:zlib'

const BUDGET_KB = 130
const dist = fileURLToPath(new URL('../dist/', import.meta.url))
const html = readFileSync(join(dist, 'index.html'), 'utf8')
const files = [...html.matchAll(/(?:src|href)="\/(assets\/[^"]+\.js)"/g)].map((m) => m[1])
// The app waits for the shown language's text before its first screen, so the larger language file counts too.
const languages = readdirSync(join(dist, 'assets')).filter((f) => /^(en|hi)-[^.]+\.js$/.test(f))
const largest = languages.sort((a, b) => gzipSync(readFileSync(join(dist, 'assets', b))).length - gzipSync(readFileSync(join(dist, 'assets', a))).length)[0]
if (largest) files.push(`assets/${largest}`)

let total = 0
for (const file of new Set(files)) {
  const size = gzipSync(readFileSync(join(dist, file))).length
  total += size
  console.log(`${(size / 1024).toFixed(1).padStart(7)} KB  ${file}`)
}

const kb = total / 1024
console.log(`${kb.toFixed(1).padStart(7)} KB  first-load JavaScript (gzip), budget ${BUDGET_KB} KB`)
if (kb > BUDGET_KB) {
  console.error(`Over budget by ${(kb - BUDGET_KB).toFixed(1)} KB. Lazy-load a screen or drop a dependency.`)
  process.exit(1)
}

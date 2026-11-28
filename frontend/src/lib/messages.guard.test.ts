import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative } from 'node:path'
import { describe, expect, it } from 'vitest'

/**
 * Keeps every screen on the shared done/failed messages (lib/notify): a new screen that prints a raw error into its
 * own line, or uses the browser's alert/confirm/prompt, fails here with the file and line to fix.
 */
const SRC = join(__dirname, '..')

function sources(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name)
    if (statSync(path).isDirectory()) return sources(path)
    return /\.tsx?$/.test(name) && !/\.test\.tsx?$/.test(name) ? [path] : []
  })
}

const files = [...sources(join(SRC, 'screens')), ...sources(join(SRC, 'components'))]
  .map((path) => ({ path: relative(SRC, path).replace(/\\/g, '/'), lines: readFileSync(path, 'utf8').split(/\r?\n/) }))

describe('success and failure messages', () => {
  it('no screen shows a raw error only in its own line: shownError(e) or notify.error(e) shows it where it can be seen', () => {
    const raw = /\b(\w+) instanceof Error \? \1\.message/
    // A dialog (ask/tell) is already in front of the person; inside t(...) the error only picks the translated words.
    const dialog = /\b(ask|tell|askSecret|confirmAsk|t)\(/
    const offenders = files.flatMap((f) => f.lines.flatMap((line, i) => (raw.test(line) && !dialog.test(line) ? [`${f.path}:${i + 1}`] : [])))
    expect(offenders, 'use shownError(e) from lib/notify').toEqual([])
  })

  it('no browser pop-ups: alert, confirm and prompt are blocked in some phone browsers', () => {
    // window.alert/confirm/prompt, and a bare alert() (a screen's own function may well be called confirm()).
    const popup = /\bwindow\.(alert|confirm|prompt)\(|(^|[^\w.])alert\(/
    const offenders = files.flatMap((f) => f.lines.flatMap((line, i) => (popup.test(line) && !/^\s*(\/\/|\*)/.test(line) ? [`${f.path}:${i + 1}`] : [])))
    expect(offenders, 'use ask/confirmAsk/tell from lib/ask, or notify').toEqual([])
  })
})

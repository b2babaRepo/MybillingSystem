/**
 * The app's own question box, in place of the browser's prompt() and confirm(): some phone and in-app browsers
 * refuse those pop-ups ("prompt() is not supported"), which silently stopped whatever asked. Call
 * `await ask('Why?')` for a typed answer (null when cancelled), `await confirmAsk('Delete it?')` for yes/no, or
 * `tell('Saved')` for a message with just OK (in place of alert()).
 * One <AskHost /> (components/Ask) at the root of the page shows them.
 */
export interface Question { message: string; value?: string; kind: 'text' | 'confirm' | 'tell' | 'secret'; done: (answer: string | null) => void }

let show: ((q: Question) => void) | null = null

/** AskHost registers how it shows a question; null when it goes away. */
export function setAskHost(host: ((q: Question) => void) | null) { show = host }

export function ask(message: string, value?: string): Promise<string | null> {
  return new Promise((done) => (show ? show({ message, value, kind: 'text', done }) : done(null)))
}

export function confirmAsk(message: string): Promise<boolean> {
  return new Promise((done) => (show ? show({ message, kind: 'confirm', done: (a) => done(a !== null) }) : done(false)))
}

/** A passcode: digits only, hidden as they are typed. Null when cancelled. */
export function askSecret(message: string): Promise<string | null> {
  return new Promise((done) => (show ? show({ message, kind: 'secret', done }) : done(null)))
}

/** A message with just OK, in place of the browser's alert(). */
export function tell(message: string): Promise<void> {
  return new Promise((done) => (show ? show({ message, kind: 'tell', done: () => done() }) : done()))
}

import i18n from '../i18n'
import { askSecret } from './ask'
import { withBillPasscode } from './api'

/**
 * Sends an action that undoes money (cancel a bill, take goods back…), asking for the shop's bill passcode when the
 * server wants it: "Enter the bill passcode", then "Wrong passcode, try again".
 */
export function guarded<T>(run: () => Promise<T>): Promise<T> {
  return withBillPasscode(run, (wrongBefore, supervisor) => askSecret(i18n.t(supervisor ? (wrongBefore ? 'super.wrong' : 'super.ask') : wrongBefore ? 'passcode.wrong' : 'passcode.billAsk')))
}

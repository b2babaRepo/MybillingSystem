import i18n from 'i18next'
import { notify } from './notify'
import { setOutboxEventHandler } from './outbox'
import { navigate } from '../router'

/**
 * Says out loud what happens to writes kept on the phone, when it happens: "saved on the phone, will send when the
 * internet is back", "N entries sent", or "not sent: <why>" with a button to the Sync screen. A save is never shown
 * as done on the server while it is only waiting on the phone.
 */
export function installOutboxNotices() {
  setOutboxEventHandler((event) => {
    if (event.kind === 'queued') {
      notify.info(i18n.t('outboxNotice.queued'))
    } else if (event.kind === 'sent') {
      notify.success(i18n.t('outboxNotice.sent', { count: event.count }))
    } else {
      notify.error(i18n.t('outboxNotice.failed', { what: event.op.label, why: event.error }), {
        action: { label: i18n.t('outboxNotice.see'), run: () => navigate('/sync') },
      })
    }
  })
}

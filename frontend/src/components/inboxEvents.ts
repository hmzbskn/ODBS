/** Okundu durumu değişince menüdeki okunmamış sayacının yenilenmesi için küçük bir olay. */
export const INBOX_CHANGED = 'inbox-changed'

export function notifyInboxChanged() {
  window.dispatchEvent(new Event(INBOX_CHANGED))
}

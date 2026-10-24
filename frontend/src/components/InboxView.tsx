import { useState } from 'react'
import { api, ApiError } from '../api/client'
import type { InboxItem } from '../api/types'
import { EmptyState, ErrorState, Loading } from './Feedback'
import { formatDateTime } from './format'
import { notifyInboxChanged } from './inboxEvents'
import { useAsync } from './useAsync'

/** Öğrencinin bildirim kutusu: duyurular ve sistem bildirimleri; açılınca okundu olur. */
export function InboxView() {
  const { state, retry } = useAsync(() => api.get<InboxItem[]>('/api/inbox'), [])
  const [error, setError] = useState<string | null>(null)
  const [openKey, setOpenKey] = useState<string | null>(null)

  if (state.status === 'loading') return <Loading />
  if (state.status === 'error') return <ErrorState message={state.message} onRetry={retry} />

  const items = state.data
  const unread = items.filter((i) => !i.read).length

  async function open(item: InboxItem) {
    const key = `${item.kind}-${item.id}`
    setOpenKey(openKey === key ? null : key)
    if (item.read || openKey === key) return
    try {
      await api.post(`/api/inbox/${item.kind.toLowerCase()}/${item.id}/read`)
      notifyInboxChanged()
      retry()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Okundu olarak işaretlenemedi.')
    }
  }

  async function readAll() {
    try {
      await api.post('/api/inbox/read-all')
      notifyInboxChanged()
      retry()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'İşlem başarısız oldu.')
    }
  }

  if (items.length === 0) {
    return (
      <div className="card">
        <EmptyState title="Bildirim kutunuz boş">Danışmanınızın duyuruları ve talepleriniz hakkındaki güncellemeler burada görünür.</EmptyState>
      </div>
    )
  }

  return (
    <>
      <div className="page-head">
        <p className="muted">{unread > 0 ? `${unread} okunmamış` : 'Tümü okundu'}</p>
        <button type="button" className="btn btn-small" disabled={unread === 0} onClick={() => void readAll()}>
          Tümünü okundu say
        </button>
      </div>
      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}
      {items.map((item) => {
        const key = `${item.kind}-${item.id}`
        const isOpen = openKey === key
        return (
          <article className={`card inbox-item${item.read ? '' : ' unread'}`} key={key}>
            <button type="button" className="inbox-head" aria-expanded={isOpen} onClick={() => void open(item)}>
              <span className="inbox-title">
                {!item.read && <span className="dot" aria-label="okunmadı" />}
                <strong>{item.title}</strong>
                <span className="badge">{item.kind === 'Announcement' ? 'Duyuru' : 'Bildirim'}</span>
              </span>
              <span className="muted">{formatDateTime(item.createdAt)}</span>
            </button>
            {item.sender && <div className="muted">{item.sender}</div>}
            <p className={isOpen ? 'inbox-body' : 'inbox-body clamp'}>{item.body}</p>
          </article>
        )
      })}
    </>
  )
}

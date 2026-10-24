import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api, ApiError } from '../api/client'
import type { Petition, PetitionStatus } from '../api/types'
import { EmptyState, ErrorState, Loading } from './Feedback'
import { PETITION_STATUS_LABELS } from './labels'
import { PetitionCard } from './PetitionCard'
import { useAsync } from './useAsync'

type Filter = 'all' | PetitionStatus

/** Danışman/yönetici: talepleri inceler; "İncelemeye al", "Onayla" ya da (gerekçeyle) "Reddet". */
export function StaffPetitions() {
  const { state, retry } = useAsync(() => api.get<Petition[]>('/api/petitions'), [])
  const [filter, setFilter] = useState<Filter>('all')
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null)

  async function setStatus(p: Petition, status: PetitionStatus, note: string) {
    setMessage(null)
    try {
      await api.post(`/api/petitions/${p.id}/status`, { status, note: note.trim() || null })
      setMessage({ text: `Talep "${PETITION_STATUS_LABELS[status]}" durumuna alındı; öğrenciye bildirim gönderildi.`, ok: true })
      retry()
    } catch (e) {
      setMessage({ text: e instanceof ApiError ? e.message : 'İşlem başarısız oldu.', ok: false })
    }
  }

  if (state.status === 'loading') return <Loading />
  if (state.status === 'error') return <ErrorState message={state.message} onRetry={retry} />

  const open = state.data.filter((p) => p.status === 'Beklemede' || p.status === 'Inceleniyor').length
  const rows = state.data.filter((p) => filter === 'all' || p.status === filter)

  return (
    <>
      <div className="card toolbar">
        <label className="field">
          <span>Durum</span>
          <select value={filter} onChange={(e) => setFilter(e.target.value as Filter)}>
            <option value="all">Tümü ({state.data.length})</option>
            {(Object.keys(PETITION_STATUS_LABELS) as PetitionStatus[]).map((s) => (
              <option key={s} value={s}>
                {PETITION_STATUS_LABELS[s]}
              </option>
            ))}
          </select>
        </label>
        <p className="muted">{open} açık talep</p>
      </div>

      {message && (
        <p className={message.ok ? 'form-ok' : 'form-error'} role={message.ok ? 'status' : 'alert'}>
          {message.text}
        </p>
      )}

      {rows.length === 0 ? (
        <div className="card">
          <EmptyState title="Gösterilecek talep yok" />
        </div>
      ) : (
        rows.map((p) => (
          <PetitionCard
            key={p.id}
            petition={p}
            heading={
              <>
                · <Link to={`/ogrenci/${p.studentNo}`}>{p.studentName}</Link> <span className="muted">({p.studentNo})</span>
              </>
            }
            actions={p.status === 'Onaylandi' || p.status === 'Reddedildi' ? null : <ReviewActions petition={p} onSet={setStatus} />}
          />
        ))
      )}
    </>
  )
}

function ReviewActions({ petition, onSet }: { petition: Petition; onSet: (p: Petition, s: PetitionStatus, note: string) => Promise<void> }) {
  const [note, setNote] = useState('')
  const [busy, setBusy] = useState(false)

  async function run(status: PetitionStatus) {
    setBusy(true)
    await onSet(petition, status, note)
    setBusy(false)
  }

  return (
    <div className="review-actions">
      <label className="field">
        <span>Öğrenciye yanıt notu (reddederken zorunlu)</span>
        <textarea aria-label="Yanıt notu" rows={2} value={note} maxLength={1000} onChange={(e) => setNote(e.target.value)} />
      </label>
      <div className="btn-row">
        {petition.status === 'Beklemede' && (
          <button type="button" className="btn btn-small" disabled={busy} onClick={() => void run('Inceleniyor')}>
            İncelemeye al
          </button>
        )}
        <button type="button" className="btn btn-small btn-primary" disabled={busy} onClick={() => void run('Onaylandi')}>
          Onayla
        </button>
        <button type="button" className="btn btn-small btn-danger" disabled={busy || note.trim() === ''} onClick={() => void run('Reddedildi')}>
          Reddet
        </button>
      </div>
    </div>
  )
}

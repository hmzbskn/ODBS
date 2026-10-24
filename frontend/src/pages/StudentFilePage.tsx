import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, ApiError } from '../api/client'
import type { StudentFile, TimelineItem } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { ErrorState, Loading } from '../components/Feedback'
import { formatDateTime, formatGpa } from '../components/format'
import { useAsync } from '../components/useAsync'

const KIND_LABELS: Record<TimelineItem['kind'], string> = {
  Note: 'Not',
  Appointment: 'Randevu',
  Petition: 'Talep',
  Announcement: 'Duyuru',
}

/** Öğrenci dosyası: görüşme notları ve zaman çizelgesi. Yalnızca danışman/yönetici görür; notlar öğrenciye kapalıdır. */
export function StudentFilePage() {
  const { studentNo = '' } = useParams()
  const { user } = useAuth()
  const { state, retry } = useAsync(() => api.get<StudentFile>(`/api/student-files/${studentNo}`), [studentNo])
  const [content, setContent] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (state.status === 'loading') return <Loading />
  if (state.status === 'error') return <ErrorState message={state.message} onRetry={retry} />

  const file = state.data
  const canWrite = user?.role === 'Danisman'

  async function addNote(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await api.post(`/api/student-files/${studentNo}/notes`, { content })
      setContent('')
      retry()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Not kaydedilemedi.')
    } finally {
      setBusy(false)
    }
  }

  async function deleteNote(id: string) {
    if (!window.confirm('Bu notu silmek istediğinizden emin misiniz?')) return
    try {
      await api.delete(`/api/student-files/notes/${id}`)
      retry()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Not silinemedi.')
    }
  }

  return (
    <>
      <h1>
        {file.fullName} <span className="muted">({file.studentNo})</span>
      </h1>
      <p className="muted">
        {file.classYear}. sınıf · GANO {formatGpa(file.gpa)} · <Link to={`/kayitlar/${file.studentNo}`}>Ders kaydı</Link> ·{' '}
        <Link to={`/risk/${file.studentNo}`}>Risk ve mezuniyet</Link>
      </p>

      <section className="card note-form">
        <h2>Görüşme notu</h2>
        <p className="muted">Notlar yalnızca size ve yöneticiye görünür; öğrenci göremez.</p>
        {canWrite ? (
          <form onSubmit={addNote}>
            <textarea
              aria-label="Görüşme notu"
              rows={4}
              value={content}
              maxLength={2000}
              placeholder="Görüşmede konuşulanları yazın…"
              onChange={(e) => setContent(e.target.value)}
            />
            <div className="btn-row submit-row">
              <button type="submit" className="btn btn-primary" disabled={busy || content.trim() === ''}>
                {busy ? 'Kaydediliyor…' : 'Notu kaydet'}
              </button>
            </div>
          </form>
        ) : (
          <p className="muted">Yönetici hesabı not ekleyemez; yalnızca görüntüleyebilir.</p>
        )}
        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}
        {file.notes.map((n) => (
          <div className="note-item" key={n.id}>
            <span className="muted">
              {formatDateTime(n.createdAt)} · {n.advisorId}
            </span>
            <p>{n.content}</p>
            <button type="button" className="btn btn-small" onClick={() => void deleteNote(n.id)}>
              Sil
            </button>
          </div>
        ))}
      </section>

      <section className="card">
        <h2>Zaman çizelgesi</h2>
        {file.timeline.length === 0 ? (
          <p className="muted">Henüz kayıt yok.</p>
        ) : (
          <ul className="timeline">
            {file.timeline.map((t, i) => (
              <li key={`${t.kind}-${t.refId ?? i}`}>
                <span className="tl-date">
                  {formatDateTime(t.date)} · <span className="badge">{KIND_LABELS[t.kind]}</span>
                </span>
                <strong> {t.title}</strong>
                {t.detail && <p>{t.detail}</p>}
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  )
}

import { useState, type FormEvent } from 'react'
import { api, ApiError } from '../api/client'
import type { RegistrationDetail } from '../api/types'
import { DECISION_LABELS } from './labels'

interface Props {
  detail: RegistrationDetail
  onSaved: () => void
}

/** Danışman karar paneli: onayla ya da (not yazarak) düzeltme iste. */
export function ReviewPanel({ detail, onSaved }: Props) {
  const [note, setNote] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [revising, setRevising] = useState(false)

  async function submit(decision: 'Approved' | 'RevisionRequested') {
    setError(null)
    setBusy(true)
    try {
      await api.post(`/api/registrations/${detail.studentNo}/review`, {
        term: detail.term,
        decision,
        note: decision === 'RevisionRequested' ? note : undefined,
      })
      setNote('')
      setRevising(false)
      onSaved()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Karar kaydedilemedi.')
    } finally {
      setBusy(false)
    }
  }

  function onRevisionSubmit(event: FormEvent) {
    event.preventDefault()
    void submit('RevisionRequested')
  }

  return (
    <section className="card">
      <h2>Danışman kararı</h2>
      <p>
        Mevcut durum: <strong>{DECISION_LABELS[detail.decision]}</strong>
      </p>
      {detail.errorCount > 0 && !revising && (
        <p className="form-warn">
          Bu kayıtta {detail.errorCount} hata var. Yine de onaylayabilirsiniz; karar kayda geçer.
        </p>
      )}

      {revising ? (
        <form className="review-form" onSubmit={onRevisionSubmit}>
          <label className="field">
            <span>Öğrenciye iletilecek düzeltme notu</span>
            <textarea
              value={note}
              onChange={(e) => setNote(e.target.value)}
              rows={4}
              maxLength={1000}
              required
              autoFocus
            />
          </label>
          <div className="btn-row">
            <button type="submit" className="btn btn-primary" disabled={busy || note.trim() === ''}>
              {busy ? 'Kaydediliyor…' : 'Düzeltme iste'}
            </button>
            <button type="button" className="btn" disabled={busy} onClick={() => setRevising(false)}>
              Vazgeç
            </button>
          </div>
        </form>
      ) : (
        <div className="btn-row">
          <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void submit('Approved')}>
            {busy ? 'Kaydediliyor…' : 'Onayla'}
          </button>
          <button type="button" className="btn" disabled={busy} onClick={() => setRevising(true)}>
            Düzeltme iste
          </button>
        </div>
      )}

      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}
    </section>
  )
}

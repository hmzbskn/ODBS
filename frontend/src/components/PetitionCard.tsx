import { useState, type ReactNode } from 'react'
import { api, ApiError } from '../api/client'
import type { Petition, PetitionStatus } from '../api/types'
import { formatDateTime } from './format'
import { PETITION_STATUS_LABELS, PETITION_TYPE_LABELS } from './labels'

const STATUS_CLASS: Record<PetitionStatus, string> = {
  Beklemede: 'badge badge-warn',
  Inceleniyor: 'badge',
  Onaylandi: 'badge badge-ok',
  Reddedildi: 'badge badge-danger',
}

export function PetitionStatusBadge({ status }: { status: PetitionStatus }) {
  return <span className={STATUS_CLASS[status]}>{PETITION_STATUS_LABELS[status]}</span>
}

/** Bir talebin ortak görünümü: tür, durum, açıklama, ek dosya ve danışman yanıtı. `actions` rol bazlı eklenir. */
export function PetitionCard({ petition: p, heading, actions }: { petition: Petition; heading?: ReactNode; actions?: ReactNode }) {
  const [error, setError] = useState<string | null>(null)

  async function download() {
    setError(null)
    try {
      await api.download(`/api/petitions/${p.id}/attachment`, p.attachmentFileName ?? 'ek')
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Dosya indirilemedi.')
    }
  }

  return (
    <article className="card petition">
      <div className="page-head">
        <div>
          <strong>{PETITION_TYPE_LABELS[p.type]}</strong> {heading}
          <div className="muted">{formatDateTime(p.createdAt)}</div>
        </div>
        <PetitionStatusBadge status={p.status} />
      </div>
      <p className="petition-text">{p.description}</p>
      {p.hasAttachment && (
        <button type="button" className="btn btn-small" onClick={() => void download()}>
          Ek: {p.attachmentFileName}
        </button>
      )}
      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}
      {p.advisorNote && (
        <div className="advisor-note">
          <strong>Danışman yanıtı:</strong> {p.advisorNote}
          {p.updatedAt && <span className="muted"> · {formatDateTime(p.updatedAt)}</span>}
        </div>
      )}
      {actions}
    </article>
  )
}

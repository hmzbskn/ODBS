import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { api, ApiError } from '../api/client'
import type { SurveyDefinition } from '../api/types'
import { ErrorState, Loading } from '../components/Feedback'
import { formatDate } from '../components/format'
import { StatusBadge } from '../components/StatusBadge'
import { SurveyImport } from '../components/SurveyImport'
import { SurveyResults } from '../components/SurveyResults'
import { SurveyScoringEditor } from '../components/SurveyScoringEditor'
import { SurveyStats } from '../components/SurveyStats'
import { useAsync } from '../components/useAsync'

type Tab = 'results' | 'scoring' | 'import' | 'stats'

const TABS: { id: Tab; label: string }[] = [
  { id: 'results', label: 'Sonuçlar' },
  { id: 'scoring', label: 'Puanlama' },
  { id: 'import', label: 'CSV içe aktar' },
  { id: 'stats', label: 'İstatistik' },
]

export function SurveyDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [tab, setTab] = useState<Tab>('results')
  // Puanlama ya da içe aktarma sonrası sonuç/istatistik sekmelerinin yeniden yüklenmesi için.
  const [version, setVersion] = useState(0)
  const [deleteError, setDeleteError] = useState<string | null>(null)
  const { state, retry } = useAsync(() => api.get<SurveyDefinition>(`/api/surveys/${id}`), [id])

  if (state.status === 'loading') return <Loading />
  if (state.status === 'error') return <ErrorState message={state.message} onRetry={retry} />

  const s = state.data
  const refresh = () => {
    setVersion((v) => v + 1)
    retry()
  }

  async function remove() {
    if (!window.confirm('Bu anketi silmek istediğinizden emin misiniz?')) return
    try {
      await api.delete(`/api/surveys/${s.id}`)
      navigate('/anketler')
    } catch (e) {
      setDeleteError(e instanceof ApiError ? e.message : 'Anket silinemedi.')
    }
  }

  return (
    <>
      <p>
        <Link to="/anketler">← Anketler</Link>
      </p>
      <div className="page-head">
        <h1>{s.title}</h1>
        <StatusBadge status={s.status} />
      </div>
      <p className="muted">
        {formatDate(s.startDate)} - {formatDate(s.endDate)} · {s.questions.length} soru · {s.responseCount} / {s.targetCount}{' '}
        yanıt{s.targetClassYear ? ` · yalnızca ${s.targetClassYear}. sınıf` : ''}
      </p>
      {s.description && <p>{s.description}</p>}

      <div className="tabs" role="tablist">
        {TABS.map((t) => (
          <button
            key={t.id}
            type="button"
            role="tab"
            aria-selected={tab === t.id}
            className={`tab${tab === t.id ? ' tab-active' : ''}`}
            onClick={() => setTab(t.id)}
          >
            {t.label}
          </button>
        ))}
      </div>

      <section className="card" role="tabpanel">
        {tab === 'results' && <SurveyResults key={version} surveyId={s.id} />}
        {tab === 'scoring' && <SurveyScoringEditor survey={s} onSaved={refresh} />}
        {tab === 'import' && <SurveyImport surveyId={s.id} onImported={refresh} />}
        {tab === 'stats' && <SurveyStats key={version} surveyId={s.id} />}
      </section>

      {s.responseCount === 0 && (
        <div className="danger-zone">
          <button type="button" className="btn btn-danger" onClick={() => void remove()}>
            Anketi sil
          </button>
          {deleteError && (
            <p className="form-error" role="alert">
              {deleteError}
            </p>
          )}
        </div>
      )}
    </>
  )
}

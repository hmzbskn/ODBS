import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { StudentSurveyItem } from '../api/types'
import { EmptyState, ErrorState, Loading } from '../components/Feedback'
import { formatDate } from '../components/format'
import { useAsync } from '../components/useAsync'

export function StudentSurveysPage() {
  const { state, retry } = useAsync(() => api.get<StudentSurveyItem[]>('/api/surveys/mine'), [])

  return (
    <>
      <h1>Anketlerim</h1>
      {state.status === 'loading' && <Loading />}
      {state.status === 'error' && <ErrorState message={state.message} onRetry={retry} />}
      {state.status === 'ready' &&
        (state.data.length === 0 ? (
          <div className="card">
            <EmptyState title="Yanıtlanacak anket yok">Danışmanınız bir anket açtığında burada görünür.</EmptyState>
          </div>
        ) : (
          state.data.map((s) => (
            <section className="card survey-item" key={s.id}>
              <div>
                <h2>{s.title}</h2>
                {s.description && <p>{s.description}</p>}
                <p className="muted">Son gün: {formatDate(s.endDate)}</p>
              </div>
              {s.submitted ? (
                <span className="badge badge-ok">Yanıtlandı</span>
              ) : (
                <Link className="btn btn-primary" to={`/anketlerim/${s.id}`}>
                  Yanıtla
                </Link>
              )}
            </section>
          ))
        ))}
    </>
  )
}

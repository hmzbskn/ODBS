import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { SurveySummary } from '../api/types'
import { EmptyState, ErrorState, Loading } from '../components/Feedback'
import { formatDate } from '../components/format'
import { StatusBadge } from '../components/StatusBadge'
import { useAsync } from '../components/useAsync'

export function SurveysPage() {
  const { state, retry } = useAsync(() => api.get<SurveySummary[]>('/api/surveys'), [])

  return (
    <>
      <div className="page-head">
        <h1>Anketler</h1>
        <Link to="/anketler/yeni" className="btn btn-primary">
          Yeni anket
        </Link>
      </div>

      {state.status === 'loading' && <Loading />}
      {state.status === 'error' && <ErrorState message={state.message} onRetry={retry} />}
      {state.status === 'ready' && (
        <div className="card">
          {state.data.length === 0 ? (
            <EmptyState title="Henüz anket oluşturmadınız">
              Öğrencilerin sosyoekonomik durumunu öğrenmek için ilk anketinizi oluşturun.
            </EmptyState>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Anket</th>
                    <th>Durum</th>
                    <th>Tarih aralığı</th>
                    <th>Soru</th>
                    <th>Yanıt</th>
                  </tr>
                </thead>
                <tbody>
                  {state.data.map((s) => (
                    <tr key={s.id}>
                      <td>
                        <Link to={`/anketler/${s.id}`}>{s.title}</Link>
                      </td>
                      <td>
                        <StatusBadge status={s.status} />
                      </td>
                      <td>
                        {formatDate(s.startDate)} - {formatDate(s.endDate)}
                      </td>
                      <td>{s.questionCount}</td>
                      <td>
                        {s.responseCount} / {s.targetCount}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </>
  )
}

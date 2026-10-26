import { Link, useParams } from 'react-router-dom'
import { api } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import type { RiskDetail } from '../api/types'
import { ErrorState, Loading } from '../components/Feedback'
import { formatGpa } from '../components/format'
import { GraduationView } from '../components/GraduationView'
import { RiskBadge } from '../components/RiskBadge'
import { useAsync } from '../components/useAsync'

export function RiskDetailPage() {
  const { studentNo = '' } = useParams()
  const { user } = useAuth()
  // Ders kaydı ve öğrenci dosyası yalnızca danışman/yönetici içindir; bölüm başkanı ve dekan analizi görür.
  const canOpenFile = user?.role === 'Admin' || user?.role === 'Danisman'
  const { state, retry } = useAsync(() => api.get<RiskDetail>(`/api/analytics/students/${studentNo}/risk`), [studentNo])

  if (state.status === 'loading') return <Loading />
  if (state.status === 'error') return <ErrorState message={state.message} onRetry={retry} />

  const r = state.data
  return (
    <>
      <p>
        <Link to="/risk">← Riskli öğrenciler</Link>
        {canOpenFile && (
          <>
            {' · '}
            <Link to={`/kayitlar/${r.studentNo}`}>Ders kaydı</Link>
            {' · '}
            <Link to={`/ogrenci/${r.studentNo}`}>Öğrenci dosyası</Link>
          </>
        )}
      </p>
      <h1>
        {r.fullName} <span className="muted">({r.studentNo})</span>
      </h1>

      <div className="stat-grid">
        <div className="card stat">
          <span className="muted">Risk puanı</span>
          <strong>{r.score} / 100</strong>
          <span className="score-track" aria-hidden="true">
            <span className={`score-fill score-${r.level.toLowerCase()}`} style={{ width: `${r.score}%` }} />
          </span>
        </div>
        <div className="card stat">
          <span className="muted">Seviye</span>
          <strong>
            <RiskBadge level={r.level} />
          </strong>
        </div>
        <div className="card stat">
          <span className="muted">Dönem</span>
          <strong>{r.term || '-'}</strong>
        </div>
        <div className="card stat">
          <span className="muted">GANO</span>
          <strong>{formatGpa(r.gpa)}</strong>
        </div>
      </div>

      <section className="card">
        <h2>Risk göstergeleri</h2>
        {r.indicators.length === 0 ? (
          <p className="muted">Bu öğrencide risk göstergesi bulunmuyor.</p>
        ) : (
          <ul className="issue-list">
            {r.indicators.map((i) => (
              <li key={i.code} className="issue issue-warning">
                <div className="issue-head">
                  <span className="badge badge-warn">+{i.points}</span>
                  <strong>{i.title}</strong>
                </div>
                <p>{i.explanation}</p>
              </li>
            ))}
          </ul>
        )}
      </section>

      <h2 className="section-title">Mezuniyet analizi</h2>
      <GraduationView studentNo={r.studentNo} />
    </>
  )
}

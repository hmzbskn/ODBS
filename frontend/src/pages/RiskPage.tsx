import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { ManagedAdvisor, RiskLevel, RiskRow } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { EmptyState, ErrorState, Loading } from '../components/Feedback'
import { formatGpa } from '../components/format'
import { RISK_LEVEL_LABELS } from '../components/labels'
import { MANAGEMENT_ROLES } from '../components/nav'
import { RiskBadge } from '../components/RiskBadge'
import { useAsync } from '../components/useAsync'

type Filter = 'all' | RiskLevel

/** Riskli öğrenciler: puana göre sıralı liste, seviyeye göre renkli; her gösterge ayrı ve açıklamalıdır. */
export function RiskPage() {
  const { user } = useAuth()
  const isManagement = !!user && MANAGEMENT_ROLES.includes(user.role)
  const [advisorId, setAdvisorId] = useState('')
  const advisors = useAsync(
    () => (isManagement ? api.get<ManagedAdvisor[]>('/api/management/advisors') : Promise.resolve([])),
    [isManagement],
  )
  const { state, retry } = useAsync(
    () => api.get<RiskRow[]>(`/api/analytics/risk${advisorId ? `?advisorId=${encodeURIComponent(advisorId)}` : ''}`),
    [advisorId],
  )
  const [filter, setFilter] = useState<Filter>('all')
  const [query, setQuery] = useState('')

  const rows = useMemo(() => {
    if (state.status !== 'ready') return []
    const q = query.trim().toLocaleLowerCase('tr-TR')
    return state.data.filter(
      (r) =>
        (filter === 'all' || r.level === filter) &&
        (!q || r.fullName.toLocaleLowerCase('tr-TR').includes(q) || r.studentNo.includes(q)),
    )
  }, [state, filter, query])

  return (
    <>
      <h1>Risk ve Mezuniyet</h1>
      <p className="muted">
        Risk puanı; GANO, dönem ortalaması düşüşü, kalınan dersler, devamsızlık ve öğrenim süresi göstergelerinden
        hesaplanır. Bir öğrenciye tıklayarak puanın gerekçesini ve mezuniyet analizini görün.
      </p>

      <div className="card toolbar">
        {isManagement && advisors.state.status === 'ready' && (
          <label className="field">
            <span>Danışman</span>
            <select value={advisorId} onChange={(e) => setAdvisorId(e.target.value)}>
              <option value="">Tüm danışmanlar</option>
              {advisors.state.data.map((a) => (
                <option key={a.advisorId} value={a.advisorId}>
                  {a.title} {a.fullName} ({a.studentCount})
                </option>
              ))}
            </select>
          </label>
        )}
        <label className="field">
          <span>Risk seviyesi</span>
          <select value={filter} onChange={(e) => setFilter(e.target.value as Filter)}>
            <option value="all">Tümü</option>
            {(['Yuksek', 'Orta', 'Dusuk'] as RiskLevel[]).map((l) => (
              <option key={l} value={l}>
                {RISK_LEVEL_LABELS[l]}
              </option>
            ))}
          </select>
        </label>
        <label className="field">
          <span>Öğrenci ara</span>
          <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Ad veya numara" />
        </label>
      </div>

      {state.status === 'loading' && <Loading label="Öğrenci verileri OBİS'ten alınıyor…" />}
      {state.status === 'error' && <ErrorState message={state.message} onRetry={retry} />}
      {state.status === 'ready' && (
        <div className="card">
          {rows.length === 0 ? (
            <EmptyState title="Gösterilecek öğrenci yok">Filtreleri değiştirmeyi deneyin.</EmptyState>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Öğrenci</th>
                    <th>Dönem</th>
                    <th>GANO</th>
                    <th>Risk puanı</th>
                    <th>Seviye</th>
                    <th>Göstergeler</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr key={r.studentNo}>
                      <td>
                        <Link to={`/risk/${r.studentNo}`}>{r.fullName}</Link> <span className="muted">({r.studentNo})</span>
                      </td>
                      <td>{r.term || '-'}</td>
                      <td>{formatGpa(r.gpa)}</td>
                      <td>
                        <div className="score-cell">
                          <span className="score-track" aria-hidden="true">
                            <span className={`score-fill score-${r.level.toLowerCase()}`} style={{ width: `${r.score}%` }} />
                          </span>
                          <strong>{r.score}</strong>
                        </div>
                      </td>
                      <td>
                        <RiskBadge level={r.level} />
                      </td>
                      <td className="wrap-cell">{r.indicatorTitles.length === 0 ? <span className="muted">-</span> : r.indicatorTitles.join(' · ')}</td>
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

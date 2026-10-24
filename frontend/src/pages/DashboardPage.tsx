import { lazy, Suspense } from 'react'
import { api } from '../api/client'
import type { DashboardResponse, RegistrationDetail, RiskRow } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { Link } from 'react-router-dom'
import { EmptyState, ErrorState, Loading } from '../components/Feedback'
import { DECISION_LABELS } from '../components/labels'
import { RiskBadge } from '../components/RiskBadge'
import { formatGpa } from '../components/format'
import { useAsync } from '../components/useAsync'

// Recharts büyük bir pakettir; yalnızca grafik gösteren danışman paneli yüklesin diye tembel yüklenir.
const RiskDistributionChart = lazy(() => import('../components/DashboardCharts').then((m) => ({ default: m.RiskDistributionChart })))
const GpaTrendChart = lazy(() => import('../components/DashboardCharts').then((m) => ({ default: m.GpaTrendChart })))
const CourseSuccessChart = lazy(() => import('../components/DashboardCharts').then((m) => ({ default: m.CourseSuccessChart })))

export function DashboardPage() {
  const { user } = useAuth()
  if (!user) return null

  return (
    <>
      <h1>Hoş geldiniz, {user.displayName}</h1>
      {user.role === 'Ogrenci' && user.linkedStudentNo ? (
        <StudentSummary studentNo={user.linkedStudentNo} />
      ) : (
        <StaffSummary />
      )}
    </>
  )
}

function StaffSummary() {
  const dash = useAsync(() => api.get<DashboardResponse>('/api/analytics/dashboard'), [])
  const risk = useAsync(() => api.get<RiskRow[]>('/api/analytics/risk'), [])

  if (dash.state.status === 'loading') return <Loading label="Öğrenci verileri OBİS'ten alınıyor…" />
  if (dash.state.status === 'error') return <ErrorState message={dash.state.message} onRetry={dash.retry} />

  const d = dash.state.data
  if (d.studentCount === 0) {
    return <EmptyState title="Öğrenci bulunamadı">Size bağlı öğrenci kaydı yok.</EmptyState>
  }

  const attention = risk.state.status === 'ready' ? risk.state.data.filter((r) => r.level !== 'Dusuk').slice(0, 5) : []

  return (
    <>
      <div className="stat-grid">
        <Stat label="Öğrenci" value={d.studentCount} />
        <Stat label="Ortalama GANO" value={formatGpa(d.averageGpa)} />
        <Stat label="Yüksek riskli" value={d.risk.high} tone={d.risk.high > 0 ? 'danger' : undefined} />
        <Stat label="Mezuniyete en çok 1 dönem kalan" value={d.graduationCandidates} />
      </div>

      <div className="chart-grid">
        <section className="card">
          <h2>Risk dağılımı</h2>
          <Suspense fallback={<Loading label="Grafik yükleniyor…" />}>
            <RiskDistributionChart risk={d.risk} />
          </Suspense>
        </section>
        <section className="card">
          <h2>Dönem bazlı ortalama GANO</h2>
          <Suspense fallback={<Loading label="Grafik yükleniyor…" />}>
            <GpaTrendChart trend={d.gpaTrend} />
          </Suspense>
        </section>
      </div>

      <section className="card">
        <h2>Başarı oranı en düşük dersler</h2>
        <Suspense fallback={<Loading label="Grafik yükleniyor…" />}>
          <CourseSuccessChart courses={d.courseSuccess} />
        </Suspense>
      </section>

      <section className="card">
        <div className="page-head">
          <h2>Önce bakılması gerekenler</h2>
          <Link to="/risk">Tüm liste</Link>
        </div>
        {risk.state.status === 'loading' && <Loading />}
        {risk.state.status === 'error' && <ErrorState message={risk.state.message} onRetry={risk.retry} />}
        {risk.state.status === 'ready' &&
          (attention.length === 0 ? (
            <p className="muted">Orta ya da yüksek riskli öğrenci yok.</p>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Öğrenci</th>
                    <th>Risk puanı</th>
                    <th>Seviye</th>
                    <th>Göstergeler</th>
                  </tr>
                </thead>
                <tbody>
                  {attention.map((r) => (
                    <tr key={r.studentNo}>
                      <td>
                        <Link to={`/risk/${r.studentNo}`}>{r.fullName}</Link> <span className="muted">({r.studentNo})</span>
                      </td>
                      <td>
                        <strong>{r.score}</strong>
                      </td>
                      <td>
                        <RiskBadge level={r.level} />
                      </td>
                      <td className="wrap-cell">{r.indicatorTitles.join(' · ')}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ))}
      </section>
    </>
  )
}

function StudentSummary({ studentNo }: { studentNo: string }) {
  const { state, retry } = useAsync(() => api.get<RegistrationDetail>(`/api/registrations/${studentNo}`), [studentNo])

  if (state.status === 'loading') return <Loading />
  if (state.status === 'error') return <ErrorState message={state.message} onRetry={retry} />

  const d = state.data
  return (
    <>
      <div className="stat-grid">
        <Stat label="Dönem" value={d.term} />
        <Stat label="GANO" value={formatGpa(d.gpa)} />
        <Stat
          label="AKTS"
          value={`${d.totalEcts} / ${d.maxEcts}`}
          tone={d.totalEcts > d.maxEcts ? 'danger' : undefined}
        />
      </div>
      <section className="card">
        <h2>Ders kaydı durumu</h2>
        <p>
          Danışman kararı: <strong>{DECISION_LABELS[d.decision]}</strong>
        </p>
        <p className="muted">
          {d.errorCount} hata, {d.warningCount} uyarı, {d.infoCount} bilgi bulgusu var.
        </p>
      </section>
    </>
  )
}

function Stat({ label, value, tone }: { label: string; value: string | number; tone?: 'danger' }) {
  return (
    <div className={`card stat${tone ? ` stat-${tone}` : ''}`}>
      <span className="muted">{label}</span>
      <strong>{value}</strong>
    </div>
  )
}

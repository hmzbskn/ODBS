import { api } from '../api/client'
import type { AdvisorStudentList, RegistrationDetail } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { EmptyState, ErrorState, Loading } from '../components/Feedback'
import { DECISION_LABELS } from '../components/labels'
import { formatGpa } from '../components/format'
import { useAsync } from '../components/useAsync'

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
  const { state, retry } = useAsync(() => api.get<AdvisorStudentList>('/api/registrations/students'), [])

  if (state.status === 'loading') return <Loading label="Öğrenci kayıtları OBİS'ten alınıyor…" />
  if (state.status === 'error') return <ErrorState message={state.message} onRetry={retry} />

  const students = state.data.students
  if (students.length === 0) {
    return <EmptyState title="Öğrenci bulunamadı">Size bağlı öğrenci kaydı yok.</EmptyState>
  }

  const withErrors = students.filter((s) => s.errorCount > 0)
  const pending = students.filter((s) => s.decision === 'Pending')
  const topIssues = [...students]
    .filter((s) => s.errorCount + s.warningCount > 0)
    .sort((a, b) => b.errorCount - a.errorCount || b.warningCount - a.warningCount)
    .slice(0, 5)

  return (
    <>
      <div className="stat-grid">
        <Stat label="Öğrenci" value={students.length} />
        <Stat label="Hatalı kayıt" value={withErrors.length} tone={withErrors.length > 0 ? 'danger' : undefined} />
        <Stat label="Karar bekleyen" value={pending.length} />
      </div>

      <section className="card">
        <h2>Önce bakılması gerekenler</h2>
        {topIssues.length === 0 ? (
          <p className="muted">Hata ya da uyarı içeren kayıt yok.</p>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Öğrenci</th>
                  <th>Dönem</th>
                  <th>Hata</th>
                  <th>Uyarı</th>
                  <th>Karar</th>
                </tr>
              </thead>
              <tbody>
                {topIssues.map((s) => (
                  <tr key={s.studentNo}>
                    <td>
                      {s.fullName} <span className="muted">({s.studentNo})</span>
                    </td>
                    <td>{s.term}</td>
                    <td>{s.errorCount > 0 ? <span className="badge badge-danger">{s.errorCount}</span> : '-'}</td>
                    <td>{s.warningCount > 0 ? <span className="badge badge-warn">{s.warningCount}</span> : '-'}</td>
                    <td>{DECISION_LABELS[s.decision]}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
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

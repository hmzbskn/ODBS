import { Link, useParams, useSearchParams } from 'react-router-dom'
import { api } from '../api/client'
import type { RegistrationDetail } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { EmptyState, ErrorState, Loading } from '../components/Feedback'
import { IssueList } from '../components/IssueList'
import { DECISION_LABELS } from '../components/labels'
import { ReviewPanel } from '../components/ReviewPanel'
import { formatGpa } from '../components/format'
import { useAsync } from '../components/useAsync'
import { WeeklySchedule } from '../components/WeeklySchedule'

const dateFormat = new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium', timeStyle: 'short' })

/** Tek öğrencinin dönem kaydı. Danışman/yönetici `/kayitlar/:studentNo`, öğrenci `/kayitlarim` ile açar. */
export function RegistrationDetailPage() {
  const { user } = useAuth()
  const params = useParams()
  const [search] = useSearchParams()
  const isStudent = user?.role === 'Ogrenci'
  const studentNo = isStudent ? user?.linkedStudentNo : params.studentNo
  const term = search.get('term')

  const { state, retry } = useAsync(
    () => api.get<RegistrationDetail>(`/api/registrations/${studentNo}${term ? `?term=${term}` : ''}`),
    [studentNo, term],
  )

  if (!studentNo) return <EmptyState title="Öğrenci bulunamadı">Hesabınıza bağlı bir öğrenci numarası yok.</EmptyState>
  if (state.status === 'loading') return <Loading label="Ders kaydı kontrol ediliyor…" />
  if (state.status === 'error') return <ErrorState message={state.message} onRetry={retry} />

  const d = state.data
  const overLimit = d.totalEcts > d.maxEcts
  const latest = d.reviews[0]

  return (
    <>
      {!isStudent && (
        <p>
          <Link to={term ? `/kayitlar?term=${term}` : '/kayitlar'}>← Öğrenci listesi</Link>
        </p>
      )}
      <h1>
        {d.fullName} <span className="muted">({d.studentNo})</span>
      </h1>

      <div className="stat-grid">
        <div className="card stat">
          <span className="muted">Dönem</span>
          <strong>{d.term}</strong>
        </div>
        <div className="card stat">
          <span className="muted">GANO</span>
          <strong>{formatGpa(d.gpa)}</strong>
        </div>
        <div className={`card stat${overLimit ? ' stat-danger' : ''}`}>
          <span className="muted">AKTS (üst sınır {d.maxEcts})</span>
          <strong>{d.totalEcts}</strong>
        </div>
        <div className="card stat">
          <span className="muted">Bulgular</span>
          <strong>
            <span className="count-error">{d.errorCount}</span> / <span className="count-warn">{d.warningCount}</span> /{' '}
            {d.infoCount}
          </strong>
          <span className="muted">hata / uyarı / bilgi</span>
        </div>
      </div>

      {isStudent && latest && (
        <section className="card">
          <h2>Danışman kararı: {DECISION_LABELS[d.decision]}</h2>
          {latest.note && <p>{latest.note}</p>}
          <p className="muted">{dateFormat.format(new Date(latest.reviewedAt))}</p>
        </section>
      )}

      <section className="card">
        <h2>Kural kontrolü</h2>
        <IssueList issues={d.issues} />
      </section>

      <section className="card">
        <h2>Haftalık ders programı</h2>
        <WeeklySchedule slots={d.weeklySchedule} />
      </section>

      <section className="card">
        <h2>Seçilen dersler</h2>
        {d.courses.length === 0 ? (
          <p className="muted">Bu dönem için ders kaydı yok.</p>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Kod</th>
                  <th>Ders</th>
                  <th>Şube</th>
                  <th>Ders dönemi</th>
                  <th>AKTS</th>
                </tr>
              </thead>
              <tbody>
                {d.courses.map((c) => (
                  <tr key={c.courseCode} className={c.inConflict ? 'row-conflict' : undefined}>
                    <td>{c.courseCode}</td>
                    <td>{c.courseName}</td>
                    <td>{c.section}</td>
                    <td>{c.courseTerm}</td>
                    <td>{c.ects}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {!isStudent && <ReviewPanel detail={d} onSaved={retry} />}

      {!isStudent && d.reviews.length > 0 && (
        <section className="card">
          <h2>Karar geçmişi</h2>
          <ul className="history">
            {d.reviews.map((r) => (
              <li key={r.id}>
                <strong>{DECISION_LABELS[r.decision]}</strong>{' '}
                <span className="muted">
                  · {r.reviewedByAdvisorId} · {dateFormat.format(new Date(r.reviewedAt))}
                </span>
                {r.note && <p>{r.note}</p>}
              </li>
            ))}
          </ul>
        </section>
      )}
    </>
  )
}

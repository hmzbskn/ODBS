import { api } from '../api/client'
import type { GraduationAnalysis } from '../api/types'
import { ErrorState, Loading } from './Feedback'
import { formatGpa } from './format'
import { MISSING_STATUS_LABELS } from './labels'
import { useAsync } from './useAsync'

function Progress({ value, max, label }: { value: number; max: number; label: string }) {
  const pct = max === 0 ? 100 : Math.min(100, Math.round((value / max) * 100))
  return (
    <div className="progress" role="progressbar" aria-label={label} aria-valuenow={pct} aria-valuemin={0} aria-valuemax={100}>
      <span className="progress-fill" style={{ width: `${pct}%` }} />
    </div>
  )
}

/** Müfredata göre mezuniyet analizi: koşullar, eksik zorunlu dersler ve "bu dönem geçilirse" projeksiyonu. */
export function GraduationView({ studentNo }: { studentNo: string }) {
  const { state, retry } = useAsync(() => api.get<GraduationAnalysis>(`/api/analytics/students/${studentNo}/graduation`), [studentNo])

  if (state.status === 'loading') return <Loading />
  if (state.status === 'error') return <ErrorState message={state.message} onRetry={retry} />

  const g = state.data
  const inProgress = g.missingMandatory.filter((m) => m.status === 'InProgress').length

  return (
    <>
      <div className={`verdict ${g.canGraduate ? 'verdict-ok' : g.projectedCanGraduate ? 'verdict-soon' : 'verdict-no'}`} role="status">
        {g.canGraduate ? (
          <strong>Tüm mezuniyet koşulları sağlanmış.</strong>
        ) : g.projectedCanGraduate ? (
          <>
            <strong>Bu dönem aldığı derslerin hepsini geçerse mezuniyet koşullarını tamamlayacak.</strong>
            <span> Tahmini kalan dönem: {g.estimatedRemainingTerms}.</span>
          </>
        ) : (
          <>
            <strong>Mezuniyet koşulları henüz tamamlanmadı.</strong>
            <span> Tahmini kalan dönem: {g.estimatedRemainingTerms}.</span>
          </>
        )}
      </div>

      <div className="grad-grid">
        <div className="card grad-card">
          <span className="muted">Toplam AKTS</span>
          <strong>
            {g.earnedEcts} / {g.requiredEcts}
          </strong>
          <Progress value={g.earnedEcts} max={g.requiredEcts} label="Toplam AKTS ilerlemesi" />
        </div>
        <div className="card grad-card">
          <span className="muted">Zorunlu dersler</span>
          <strong>
            {g.mandatoryPassed} / {g.mandatoryTotal}
          </strong>
          <Progress value={g.mandatoryPassed} max={g.mandatoryTotal} label="Zorunlu ders ilerlemesi" />
        </div>
        <div className="card grad-card">
          <span className="muted">Seçmeli kredi</span>
          <strong>
            {g.electiveEarnedCredits} / {g.electiveRequiredCredits}
          </strong>
          <Progress value={g.electiveEarnedCredits} max={g.electiveRequiredCredits} label="Seçmeli kredi ilerlemesi" />
        </div>
        <div className="card grad-card">
          <span className="muted">Staj (gün)</span>
          <strong>
            {g.internshipDays} / {g.internshipRequiredDays}
          </strong>
          <Progress value={g.internshipDays} max={g.internshipRequiredDays} label="Staj ilerlemesi" />
        </div>
        <div className="card grad-card">
          <span className="muted">GANO (en az {g.minGpa.toFixed(2)})</span>
          <strong className={g.gpaOk ? undefined : 'count-error'}>{formatGpa(g.gpa)}</strong>
        </div>
      </div>

      {g.blockers.length > 0 && (
        <section className="card">
          <h2>Eksik koşullar</h2>
          <ul className="blockers">
            {g.blockers.map((b) => (
              <li key={b}>{b}</li>
            ))}
          </ul>
          {!g.canGraduate && g.projectedBlockers.length > 0 && inProgress > 0 && (
            <>
              <p className="muted">Bu dönem alınan dersler geçilse bile kalacak eksikler:</p>
              <ul className="blockers">
                {g.projectedBlockers.map((b) => (
                  <li key={b}>{b}</li>
                ))}
              </ul>
            </>
          )}
        </section>
      )}

      <section className="card">
        <h2>Tamamlanmayan zorunlu dersler</h2>
        {g.missingMandatory.length === 0 ? (
          <p className="muted">Tüm zorunlu dersler tamamlanmış.</p>
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Kod</th>
                  <th>Ders</th>
                  <th>Dönem</th>
                  <th>AKTS</th>
                  <th>Durum</th>
                </tr>
              </thead>
              <tbody>
                {g.missingMandatory.map((m) => (
                  <tr key={m.courseCode}>
                    <td>{m.courseCode}</td>
                    <td>{m.courseName}</td>
                    <td>{m.term}</td>
                    <td>{m.ects}</td>
                    <td>
                      <span className={m.status === 'Failed' ? 'badge badge-danger' : m.status === 'InProgress' ? 'badge badge-ok' : 'badge'}>
                        {MISSING_STATUS_LABELS[m.status]}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="card">
        <h2>Tamamlanan seçmeli dersler</h2>
        {g.passedElectives.length === 0 ? (
          <p className="muted">Henüz tamamlanmış seçmeli ders yok.</p>
        ) : (
          <ul className="blockers">
            {g.passedElectives.map((e) => (
              <li key={e.courseCode}>
                {e.courseCode} · {e.courseName} <span className="muted">({e.credit} kredi)</span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  )
}

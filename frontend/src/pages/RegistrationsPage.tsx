import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '../api/client'
import type { AdvisorStudentList } from '../api/types'
import { EmptyState, ErrorState, Loading } from '../components/Feedback'
import { DECISION_LABELS } from '../components/labels'
import { formatGpa } from '../components/format'
import { useAsync } from '../components/useAsync'

/** Danışmanın öğrenci listesi: dönem seçimi, arama ve her öğrencinin yanında hata/uyarı rozetleri. */
export function RegistrationsPage() {
  const [search, setSearch] = useSearchParams()
  const term = search.get('term')
  const [query, setQuery] = useState('')
  const [onlyProblems, setOnlyProblems] = useState(false)

  const { state, retry } = useAsync(
    () => api.get<AdvisorStudentList>(`/api/registrations/students${term ? `?term=${term}` : ''}`),
    [term],
  )

  const rows = useMemo(() => {
    if (state.status !== 'ready') return []
    const q = query.trim().toLocaleLowerCase('tr-TR')
    return state.data.students.filter(
      (s) =>
        (!q || s.fullName.toLocaleLowerCase('tr-TR').includes(q) || s.studentNo.includes(q)) &&
        (!onlyProblems || s.errorCount + s.warningCount > 0),
    )
  }, [state, query, onlyProblems])

  // Dönem listesi ilk yüklemeden gelir; sonraki filtrelemede korunur.
  const [terms, setTerms] = useState<number[]>([])
  if (state.status === 'ready' && terms.length === 0 && state.data.availableTerms.length > 0) {
    setTerms(state.data.availableTerms)
  }

  return (
    <>
      <h1>Ders Kayıtları</h1>

      <div className="card toolbar">
        <label className="field">
          <span>Dönem</span>
          <select
            value={term ?? ''}
            onChange={(e) => setSearch(e.target.value ? { term: e.target.value } : {})}
          >
            <option value="">Her öğrencinin güncel dönemi</option>
            {terms.map((t) => (
              <option key={t} value={t}>
                {t}. dönem
              </option>
            ))}
          </select>
        </label>
        <label className="field">
          <span>Öğrenci ara</span>
          <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Ad veya numara" />
        </label>
        <label className="check">
          <input type="checkbox" checked={onlyProblems} onChange={(e) => setOnlyProblems(e.target.checked)} />
          Yalnızca bulgusu olanlar
        </label>
      </div>

      {state.status === 'loading' && <Loading label="Kayıtlar kontrol ediliyor…" />}
      {state.status === 'error' && <ErrorState message={state.message} onRetry={retry} />}
      {state.status === 'ready' && (
        <div className="card">
          {rows.length === 0 ? (
            <EmptyState title="Gösterilecek öğrenci yok">
              Filtreleri değiştirmeyi ya da başka bir dönem seçmeyi deneyin.
            </EmptyState>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Öğrenci</th>
                    <th>Sınıf</th>
                    <th>Dönem</th>
                    <th>GANO</th>
                    <th>AKTS</th>
                    <th>Hata</th>
                    <th>Uyarı</th>
                    <th>Bilgi</th>
                    <th>Karar</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((s) => (
                    <tr key={s.studentNo}>
                      <td>
                        <Link to={`/kayitlar/${s.studentNo}${term ? `?term=${term}` : ''}`}>{s.fullName}</Link>{' '}
                        <span className="muted">({s.studentNo})</span>
                      </td>
                      <td>{s.classYear}</td>
                      <td>{s.term}</td>
                      <td>{formatGpa(s.gpa)}</td>
                      <td>{s.totalEcts}</td>
                      <td>{s.errorCount > 0 ? <span className="badge badge-danger">{s.errorCount}</span> : '-'}</td>
                      <td>{s.warningCount > 0 ? <span className="badge badge-warn">{s.warningCount}</span> : '-'}</td>
                      <td>{s.infoCount > 0 ? <span className="badge">{s.infoCount}</span> : '-'}</td>
                      <td>{DECISION_LABELS[s.decision]}</td>
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

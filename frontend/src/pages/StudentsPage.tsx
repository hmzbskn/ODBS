import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api/client'
import type { StudentListRow } from '../api/types'
import { EmptyState, ErrorState, Loading } from '../components/Feedback'
import { formatGpa } from '../components/format'
import { useAsync } from '../components/useAsync'

/** Danışmanın (yönetimde kapsamındaki) öğrenci listesi; öğrenciye tıklayınca okul, iletişim ve akademik özet açılır. */
export function StudentsPage() {
  const { state, retry } = useAsync(() => api.get<StudentListRow[]>('/api/students'), [])
  const [query, setQuery] = useState('')
  const [classYear, setClassYear] = useState('')
  const [program, setProgram] = useState('')

  const all = state.status === 'ready' ? state.data : []
  const classYears = useMemo(() => [...new Set(all.map((s) => s.classYear))].sort((a, b) => a - b), [all])
  const programs = useMemo(() => [...new Set(all.map((s) => s.program))].sort((a, b) => a.localeCompare(b, 'tr-TR')), [all])

  const rows = useMemo(() => {
    const q = query.trim().toLocaleLowerCase('tr-TR')
    return all.filter(
      (s) =>
        (!q || s.fullName.toLocaleLowerCase('tr-TR').includes(q) || s.studentNo.includes(q)) &&
        (!classYear || s.classYear === Number(classYear)) &&
        (!program || s.program === program),
    )
  }, [all, query, classYear, program])

  return (
    <>
      <h1>Öğrenciler</h1>
      <p className="muted">Öğrenciye tıklayarak okul, iletişim ve akademik bilgilerini görün.</p>

      <div className="card toolbar">
        <label className="field">
          <span>Öğrenci ara</span>
          <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Ad veya numara" />
        </label>
        <label className="field">
          <span>Sınıf</span>
          <select value={classYear} onChange={(e) => setClassYear(e.target.value)}>
            <option value="">Tüm sınıflar</option>
            {classYears.map((y) => (
              <option key={y} value={y}>
                {y}. sınıf
              </option>
            ))}
          </select>
        </label>
        <label className="field">
          <span>Öğretim türü</span>
          <select value={program} onChange={(e) => setProgram(e.target.value)}>
            <option value="">Tümü</option>
            {programs.map((p) => (
              <option key={p} value={p}>
                {p}
              </option>
            ))}
          </select>
        </label>
      </div>

      {state.status === 'loading' && <Loading label="Öğrenciler yükleniyor…" />}
      {state.status === 'error' && <ErrorState message={state.message} onRetry={retry} />}
      {state.status === 'ready' && (
        <div className="card">
          {rows.length === 0 ? (
            <EmptyState title="Öğrenci bulunamadı">Filtreleri değiştirmeyi deneyin.</EmptyState>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Öğrenci</th>
                    <th>Sınıf</th>
                    <th>Öğretim</th>
                    <th>GANO</th>
                    <th>Danışman</th>
                    <th>Durum</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((s) => (
                    <tr key={s.studentNo}>
                      <td>
                        <Link to={`/ogrenciler/${s.studentNo}`}>{s.fullName}</Link> <span className="muted">({s.studentNo})</span>
                      </td>
                      <td>{s.classYear}</td>
                      <td>{s.program}</td>
                      <td>{s.gpa > 0 ? formatGpa(s.gpa) : '-'}</td>
                      <td>{s.advisorName}</td>
                      <td>{s.status}</td>
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

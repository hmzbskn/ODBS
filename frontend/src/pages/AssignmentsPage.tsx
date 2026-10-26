import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { api, ApiError } from '../api/client'
import type { AssignResult, ManagedAdvisor, ManagedStudent } from '../api/types'
import { EmptyState, ErrorState, Loading } from '../components/Feedback'
import { formatGpa } from '../components/format'
import { useAsync } from '../components/useAsync'

/** Bölüm başkanı / dekan / yönetici: öğrencileri danışmanlara atar. Atama OBİS'teki danışmanı ezer;
 * öğrenciyi OBİS'teki danışmanına geri atamak ezme kaydını kaldırır. */
export function AssignmentsPage() {
  const advisors = useAsync(() => api.get<ManagedAdvisor[]>('/api/management/advisors'), [])
  const [filterAdvisor, setFilterAdvisor] = useState('')
  const students = useAsync(
    () => api.get<ManagedStudent[]>(`/api/management/students${filterAdvisor ? `?advisorId=${encodeURIComponent(filterAdvisor)}` : ''}`),
    [filterAdvisor],
  )
  const [query, setQuery] = useState('')
  const [classYear, setClassYear] = useState('')
  const [department, setDepartment] = useState('')
  const [status, setStatus] = useState<'' | 'reassigned' | 'obis'>('')
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [target, setTarget] = useState('')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ tone: 'ok' | 'error'; text: string } | null>(null)

  const all = students.state.status === 'ready' ? students.state.data : []
  const classYears = useMemo(() => [...new Set(all.map((s) => s.classYear))].sort((a, b) => a - b), [all])
  const departments = useMemo(() => [...new Set(all.map((s) => s.department))].sort((a, b) => a.localeCompare(b, 'tr-TR')), [all])

  const rows = useMemo(() => {
    const q = query.trim().toLocaleLowerCase('tr-TR')
    return all.filter(
      (s) =>
        (!q || s.fullName.toLocaleLowerCase('tr-TR').includes(q) || s.studentNo.includes(q)) &&
        (!classYear || s.classYear === Number(classYear)) &&
        (!department || s.department === department) &&
        (!status || (status === 'reassigned') === s.isReassigned),
    )
  }, [all, query, classYear, department, status])

  const filtersActive = Boolean(query || classYear || department || status)
  const clearFilters = () => {
    setQuery('')
    setClassYear('')
    setDepartment('')
    setStatus('')
  }

  const allSelected = rows.length > 0 && rows.every((s) => selected.has(s.studentNo))

  const toggle = (no: string) =>
    setSelected((prev) => {
      const next = new Set(prev)
      if (!next.delete(no)) next.add(no)
      return next
    })

  const toggleAll = () =>
    setSelected((prev) => {
      const next = new Set(prev)
      if (allSelected) rows.forEach((s) => next.delete(s.studentNo))
      else rows.forEach((s) => next.add(s.studentNo))
      return next
    })

  const assign = async () => {
    setBusy(true)
    setMessage(null)
    try {
      const r = await api.post<AssignResult>('/api/management/assignments', {
        advisorId: target,
        studentNos: [...selected],
      })
      const parts = [
        r.assigned > 0 && `${r.assigned} öğrenci atandı`,
        r.restoredToObis > 0 && `${r.restoredToObis} öğrenci OBİS'teki danışmanına döndürüldü`,
        r.unchanged > 0 && `${r.unchanged} öğrenci zaten bu danışmanda`,
      ].filter(Boolean)
      setMessage({ tone: 'ok', text: `${parts.join(', ')}.` })
      setSelected(new Set())
      students.retry()
      advisors.retry()
    } catch (e) {
      setMessage({ tone: 'error', text: e instanceof ApiError ? e.message : 'Atama yapılamadı.' })
    } finally {
      setBusy(false)
    }
  }

  if (advisors.state.status === 'loading') return <Loading />
  if (advisors.state.status === 'error') return <ErrorState message={advisors.state.message} onRetry={advisors.retry} />
  const advisorList = advisors.state.data

  return (
    <>
      <h1>Danışman Atamaları</h1>
      <p className="muted">
        Öğrencileri seçip bir danışmana atayın. Atama hemen geçerli olur: öğrenci yeni danışmanın listesine ve
        analizlerine geçer, eski danışman erişimini kaybeder. Mevcut randevular ve notlar silinmez.
      </p>

      <div className="card toolbar">
        <label className="field">
          <span>Mevcut danışman</span>
          <select
            value={filterAdvisor}
            onChange={(e) => {
              setFilterAdvisor(e.target.value)
              setSelected(new Set())
            }}
          >
            <option value="">Tüm öğrenciler</option>
            {advisorList.map((a) => (
              <option key={a.advisorId} value={a.advisorId}>
                {a.title} {a.fullName} ({a.studentCount})
              </option>
            ))}
          </select>
        </label>
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
        {departments.length > 1 && (
          <label className="field">
            <span>Bölüm</span>
            <select value={department} onChange={(e) => setDepartment(e.target.value)}>
              <option value="">Tüm bölümler</option>
              {departments.map((d) => (
                <option key={d} value={d}>
                  {d}
                </option>
              ))}
            </select>
          </label>
        )}
        <label className="field">
          <span>Atama durumu</span>
          <select value={status} onChange={(e) => setStatus(e.target.value as '' | 'reassigned' | 'obis')}>
            <option value="">Tümü</option>
            <option value="reassigned">Yeniden atananlar</option>
            <option value="obis">OBİS'teki danışmanında</option>
          </select>
        </label>
        {filtersActive && (
          <button type="button" className="btn" onClick={clearFilters}>
            Filtreleri temizle
          </button>
        )}
      </div>

      <div className="card assign-bar">
        <label className="field">
          <span>Atanacak danışman</span>
          <select value={target} onChange={(e) => setTarget(e.target.value)}>
            <option value="">Seçin…</option>
            {advisorList.map((a) => (
              <option key={a.advisorId} value={a.advisorId}>
                {a.title} {a.fullName}
              </option>
            ))}
          </select>
        </label>
        <button type="button" className="btn btn-primary" disabled={busy || !target || selected.size === 0} onClick={assign}>
          {busy ? 'Atanıyor…' : `Seçilenleri ata (${selected.size})`}
        </button>
        {message && (
          <p className={`notice grow${message.tone === 'error' ? ' state-error' : ''}`} role={message.tone === 'error' ? 'alert' : 'status'}>
            {message.text}
          </p>
        )}
      </div>

      {students.state.status === 'loading' && <Loading label="Öğrenciler yükleniyor…" />}
      {students.state.status === 'error' && <ErrorState message={students.state.message} onRetry={students.retry} />}
      {students.state.status === 'ready' && (
        <div className="card">
          <div className="select-bar">
            <span className="muted">
              {rows.length} öğrenci listeleniyor, {selected.size} seçili
            </span>
            <button type="button" className="btn btn-small" disabled={rows.length === 0 || allSelected} onClick={toggleAll}>
              {classYear ? `${classYear}. sınıfın listelenenlerini seç (${rows.length})` : `Listelenenlerin tümünü seç (${rows.length})`}
            </button>
            <button type="button" className="btn btn-small" disabled={selected.size === 0} onClick={() => setSelected(new Set())}>
              Seçimi temizle
            </button>
          </div>
          {rows.length === 0 ? (
            <EmptyState title="Öğrenci bulunamadı">Filtreleri değiştirmeyi deneyin.</EmptyState>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th className="check-cell">
                      <input type="checkbox" checked={allSelected} onChange={toggleAll} aria-label="Listedeki tüm öğrencileri seç" />
                    </th>
                    <th>Öğrenci</th>
                    <th>Sınıf</th>
                    <th>GANO</th>
                    <th>Danışman</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((s) => (
                    <tr key={s.studentNo} className={selected.has(s.studentNo) ? 'row-selected' : undefined}>
                      <td className="check-cell">
                        <input
                          type="checkbox"
                          checked={selected.has(s.studentNo)}
                          onChange={() => toggle(s.studentNo)}
                          aria-label={`${s.fullName} seç`}
                        />
                      </td>
                      <td>
                        <Link to={`/risk/${s.studentNo}`}>{s.fullName}</Link> <span className="muted">({s.studentNo})</span>
                      </td>
                      <td>{s.classYear}</td>
                      <td>{s.gpa > 0 ? formatGpa(s.gpa) : '-'}</td>
                      <td>
                        {s.advisorName} {s.isReassigned && <span className="badge badge-warn">Yeniden atandı</span>}
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

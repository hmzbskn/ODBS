import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api } from '../api/client'
import type { StudentProfileDetail } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { ErrorState, Loading } from '../components/Feedback'
import { formatGpa } from '../components/format'
import { useAsync } from '../components/useAsync'

// Selçuk öğrenci postası (Zimbra). Alıcıyı önceden doldurmak giriş gerektirdiğinden adres panoya kopyalanır.
const ZIMBRA_URL = 'https://ogrposta.selcuk.edu.tr/'

/** Tek öğrencinin okul, iletişim ve akademik özeti (danışman / yönetim görünümü). */
export function StudentProfilePage() {
  const { studentNo = '' } = useParams()
  const { user } = useAuth()
  const { state, retry } = useAsync(() => api.get<StudentProfileDetail>(`/api/students/${studentNo}`), [studentNo])
  const [copied, setCopied] = useState(false)

  if (state.status === 'loading') return <Loading />
  if (state.status === 'error') return <ErrorState message={state.message} onRetry={retry} />

  const { school, contact, academic, advisor } = state.data
  const canOpenFile = user?.role === 'Admin' || user?.role === 'Danisman'

  const mail = () => {
    if (contact.email) {
      navigator.clipboard?.writeText(contact.email).then(
        () => setCopied(true),
        () => setCopied(false),
      )
    }
    window.open(ZIMBRA_URL, '_blank', 'noopener')
  }

  return (
    <>
      <p>
        <Link to="/ogrenciler">← Öğrenciler</Link>
      </p>
      <h1>
        {school.fullName} <span className="muted">({school.studentNo})</span>
      </h1>

      <div className="kpi-grid">
        <div className="card stat">
          <span className="muted">GANO</span>
          <strong>{academic.gpa > 0 ? formatGpa(academic.gpa) : '-'}</strong>
        </div>
        <div className="card stat">
          <span className="muted">Alınan AKTS</span>
          <strong>{academic.earnedEcts}</strong>
        </div>
        <div className="card stat">
          <span className="muted">Kalan AKTS</span>
          <strong>{academic.remainingEcts}</strong>
        </div>
        <div className="card stat">
          <span className="muted">Bu dönem aldığı ders</span>
          <strong>{academic.currentTermCourseCount}</strong>
        </div>
      </div>

      <div className="profile-grid">
        <section className="card">
          <h2>Okul bilgileri</h2>
          <dl className="info-list">
            <dt>Numara</dt>
            <dd>{school.studentNo}</dd>
            <dt>Bölüm</dt>
            <dd>{school.department}</dd>
            <dt>Sınıf / dönem</dt>
            <dd>
              {school.classYear}. sınıf, {school.term}. dönem
            </dd>
            <dt>Öğretim türü</dt>
            <dd>{school.program}</dd>
            <dt>Durum</dt>
            <dd>{school.status}</dd>
            <dt>Danışman</dt>
            <dd>{advisor ? `${advisor.title} ${advisor.name}` : 'Atanmamış'}</dd>
          </dl>
        </section>

        <section className="card">
          <h2>İletişim</h2>
          <dl className="info-list">
            <dt>E-posta</dt>
            <dd>{contact.email ?? '-'}</dd>
            <dt>Telefon</dt>
            <dd>{contact.phone ?? '-'}</dd>
            <dt>İkamet yeri</dt>
            <dd>{contact.residence ?? '-'}</dd>
          </dl>
          <div className="advisor-card-action">
            <button type="button" className="btn btn-primary" onClick={mail} disabled={!contact.email}>
              E-posta gönder (Zimbra)
            </button>
            {copied && <span className="muted">Adres kopyalandı, yeni e-postada yapıştırın.</span>}
          </div>
        </section>
      </div>

      <div className="card toolbar">
        <Link className="btn" to={`/kayitlar/${school.studentNo}`}>
          Ders kaydı
        </Link>
        <Link className="btn" to={`/risk/${school.studentNo}`}>
          Risk ve mezuniyet
        </Link>
        {canOpenFile && (
          <Link className="btn" to={`/ogrenci/${school.studentNo}`}>
            Görüşme notları ve dosya
          </Link>
        )}
      </div>
    </>
  )
}

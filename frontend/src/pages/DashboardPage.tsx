import { lazy, Suspense, useState } from 'react'
import { api } from '../api/client'
import type { AdvisorContact, DashboardResponse, ManagementOverview, RegistrationDetail, RiskRow } from '../api/types'
import { useAuth } from '../auth/AuthContext'
import { Link } from 'react-router-dom'
import { EmptyState, ErrorState, Loading } from '../components/Feedback'
import { DECISION_LABELS } from '../components/labels'
import { MANAGEMENT_ROLES } from '../components/nav'
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
      <header className="dash-head">
        <p className="eyebrow">Genel bakış</p>
        <h1>Hoş geldiniz, {user.displayName}</h1>
      </header>
      {user.role === 'Ogrenci' && user.linkedStudentNo ? (
        <StudentSummary studentNo={user.linkedStudentNo} />
      ) : MANAGEMENT_ROLES.includes(user.role) ? (
        <ManagementSummary />
      ) : (
        <StaffSummary />
      )}
    </>
  )
}

/** Bölüm başkanı / dekan / yönetici: tüm kapsamın özeti, danışman karşılaştırma tablosu ve seçilen
 * danışmanın öğrencileri için danışmanın kendi panelindeki analizlerin aynısı. */
function ManagementSummary() {
  const overview = useAsync(() => api.get<ManagementOverview>('/api/management/overview'), [])
  const [advisorId, setAdvisorId] = useState('')

  if (overview.state.status === 'loading') return <Loading label="Danışman özetleri hazırlanıyor…" />
  if (overview.state.status === 'error') return <ErrorState message={overview.state.message} onRetry={overview.retry} />

  const { advisors } = overview.state.data
  const selected = advisors.find((a) => a.advisorId === advisorId)

  return (
    <>
      <section className="card">
        <div className="page-head">
          <h2>Danışmanlar</h2>
          <Link to="/atamalar">Öğrenci ata</Link>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Danışman</th>
                <th>Öğrenci</th>
                <th>Ort. GANO</th>
                <th>Yüksek risk</th>
                <th>Orta risk</th>
                <th>Mezuniyete ≤ 1 dönem</th>
                <th>
                  <span className="sr-only">İşlem</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {advisors.map((a) => (
                <tr key={a.advisorId} className={a.advisorId === advisorId ? 'row-selected' : undefined}>
                  <td>
                    {a.title} {a.fullName}
                  </td>
                  <td>{a.studentCount}</td>
                  <td>{a.studentCount === 0 ? '-' : formatGpa(a.averageGpa)}</td>
                  <td>{a.risk.high > 0 ? <span className="badge badge-danger">{a.risk.high}</span> : 0}</td>
                  <td>{a.risk.medium}</td>
                  <td>{a.graduationCandidates}</td>
                  <td>
                    <button
                      type="button"
                      className="btn btn-small"
                      aria-pressed={a.advisorId === advisorId}
                      onClick={() => setAdvisorId(a.advisorId === advisorId ? '' : a.advisorId)}
                    >
                      {a.advisorId === advisorId ? 'Tümüne dön' : 'Analizleri aç'}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <div className="toolbar card">
        <label className="field">
          <span>Analizlerin kapsamı</span>
          <select value={advisorId} onChange={(e) => setAdvisorId(e.target.value)}>
            <option value="">Tüm öğrenciler</option>
            {advisors.map((a) => (
              <option key={a.advisorId} value={a.advisorId}>
                {a.title} {a.fullName} ({a.studentCount})
              </option>
            ))}
          </select>
        </label>
        <p className="muted">
          {selected
            ? `${selected.title} ${selected.fullName} danışmanlığındaki öğrencilerin analizleri gösteriliyor.`
            : 'Kapsamınızdaki tüm öğrencilerin analizleri gösteriliyor.'}
        </p>
      </div>

      <StaffSummary advisorId={advisorId || undefined} />
    </>
  )
}

function StaffSummary({ advisorId }: { advisorId?: string }) {
  const query = advisorId ? `?advisorId=${encodeURIComponent(advisorId)}` : ''
  const dash = useAsync(() => api.get<DashboardResponse>(`/api/analytics/dashboard${query}`), [advisorId])
  const risk = useAsync(() => api.get<RiskRow[]>(`/api/analytics/risk${query}`), [advisorId])

  if (dash.state.status === 'loading') return <Loading label="Öğrenci verileri OBİS'ten alınıyor…" />
  if (dash.state.status === 'error') return <ErrorState message={dash.state.message} onRetry={dash.retry} />

  const d = dash.state.data
  if (d.studentCount === 0) {
    return <EmptyState title="Öğrenci bulunamadı">{advisorId ? 'Bu danışmana bağlı öğrenci yok.' : 'Size bağlı öğrenci kaydı yok.'}</EmptyState>
  }

  const attention = risk.state.status === 'ready' ? risk.state.data.filter((r) => r.level !== 'Dusuk').slice(0, 5) : []

  return (
    <>
      <div className="kpi-grid">
        <Stat label="Öğrenci" value={d.studentCount} />
        <Stat label="Ortalama GANO" value={formatGpa(d.averageGpa)} />
        <Stat label="Yüksek riskli" value={d.risk.high} tone={d.risk.high > 0 ? 'danger' : undefined} />
        <Stat label="Mezuniyete en çok 1 dönem kalan" value={d.graduationCandidates} />
      </div>

      <div className="dash-grid">
        <section className="card dash-main">
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
        <section className="card dash-side">
          <h2>Risk dağılımı</h2>
          <Suspense fallback={<Loading label="Grafik yükleniyor…" />}>
            <RiskDistributionChart risk={d.risk} />
          </Suspense>
        </section>
        <section className="card dash-half">
          <h2>Dönem bazlı ortalama GANO</h2>
          <Suspense fallback={<Loading label="Grafik yükleniyor…" />}>
            <GpaTrendChart trend={d.gpaTrend} />
          </Suspense>
        </section>
        <section className="card dash-half">
          <h2>Başarı oranı en düşük dersler</h2>
          <Suspense fallback={<Loading label="Grafik yükleniyor…" />}>
            <CourseSuccessChart courses={d.courseSuccess} />
          </Suspense>
        </section>
      </div>
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
      <div className="kpi-grid">
        <Stat label="Dönem" value={`${d.term}. dönem`} />
        <Stat label="GANO" value={formatGpa(d.gpa)} />
        <Stat
          label="AKTS"
          value={`${d.totalEcts} / ${d.maxEcts}`}
          tone={d.totalEcts > d.maxEcts ? 'danger' : undefined}
        />
        <Stat label="Bu dönem aldığı ders" value={d.courses.length} />
      </div>
      <AdvisorCard advisor={d.advisor} />
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

// Selçuk öğrenci postası (Zimbra). Alıcıyı önceden doldurmak için giriş gerektiğinden adres panoya kopyalanır.
const ZIMBRA_URL = 'https://ogrposta.selcuk.edu.tr/'

function AdvisorCard({ advisor }: { advisor: AdvisorContact | null }) {
  const [copied, setCopied] = useState(false)
  if (!advisor) {
    return (
      <section className="card">
        <h2>Danışmanım</h2>
        <p className="muted">Henüz bir danışman atanmamış.</p>
      </section>
    )
  }

  const openMail = () => {
    navigator.clipboard?.writeText(advisor.email).then(
      () => setCopied(true),
      () => setCopied(false),
    )
    window.open(ZIMBRA_URL, '_blank', 'noopener')
  }

  return (
    <section className="card advisor-card">
      <div>
        <p className="muted">Danışmanım</p>
        <h2>
          {advisor.title} {advisor.name}
        </h2>
        <p className="muted">{advisor.email}</p>
      </div>
      <div className="advisor-card-action">
        <button type="button" className="btn btn-primary" onClick={openMail}>
          E-posta gönder (Zimbra)
        </button>
        {copied && <span className="muted">Adres kopyalandı, yeni e-postada yapıştırın.</span>}
      </div>
    </section>
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

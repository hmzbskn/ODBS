import { useMemo, useState, type FormEvent } from 'react'
import { api, ApiError } from '../api/client'
import type { AnnouncementDetail, AnnouncementFilter, AnnouncementSummary, AudiencePreview, RiskLevel } from '../api/types'
import { EmptyState, ErrorState, Loading } from './Feedback'
import { formatDateTime, formatGpa } from './format'
import { RISK_LEVEL_LABELS } from './labels'
import { RiskBadge } from './RiskBadge'
import { useAsync } from './useAsync'

interface Draft {
  classYear: string
  minGpa: string
  maxGpa: string
  risk: '' | RiskLevel
  internship: '' | 'done' | 'notdone'
}

const EMPTY: Draft = { classYear: '', minGpa: '', maxGpa: '', risk: '', internship: '' }

const num = (v: string): number | null => (v.trim() === '' ? null : Number(v.trim().replace(',', '.')))

function toFilter(d: Draft): AnnouncementFilter {
  return {
    classYear: d.classYear ? Number(d.classYear) : null,
    minGpa: num(d.minGpa),
    maxGpa: num(d.maxGpa),
    riskLevel: d.risk || null,
    internshipComplete: d.internship === '' ? null : d.internship === 'done',
  }
}

function describe(f: AnnouncementFilter): string {
  const parts: string[] = []
  if (f.classYear) parts.push(`${f.classYear}. sınıf`)
  if (f.minGpa !== null) parts.push(`GANO ≥ ${f.minGpa}`)
  if (f.maxGpa !== null) parts.push(`GANO ≤ ${f.maxGpa}`)
  if (f.riskLevel) parts.push(`risk: ${RISK_LEVEL_LABELS[f.riskLevel]}`)
  if (f.internshipComplete !== null) parts.push(f.internshipComplete ? 'stajı tamamlayanlar' : 'stajı tamamlamayanlar')
  return parts.length === 0 ? 'Tüm öğrenciler' : parts.join(' · ')
}

export function StaffAnnouncements() {
  const list = useAsync(() => api.get<AnnouncementSummary[]>('/api/announcements'), [])
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')
  const [draft, setDraft] = useState<Draft>(EMPTY)
  const [preview, setPreview] = useState<{ key: string; data: AudiencePreview } | null>(null)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null)
  const [openId, setOpenId] = useState<string | null>(null)

  const filter = useMemo(() => toFilter(draft), [draft])
  const filterKey = JSON.stringify(filter)
  // Filtre değişince önizleme geçersiz olur: göndermeden önce güncel hedef kitle görülmeli.
  const previewFresh = preview !== null && preview.key === filterKey

  const patch = (p: Partial<Draft>) => setDraft((d) => ({ ...d, ...p }))

  async function showAudience() {
    setBusy(true)
    setMessage(null)
    try {
      setPreview({ key: filterKey, data: await api.post<AudiencePreview>('/api/announcements/preview', filter) })
    } catch (e) {
      setPreview(null)
      setMessage({ text: e instanceof ApiError ? e.message : 'Hedef kitle hesaplanamadı.', ok: false })
    } finally {
      setBusy(false)
    }
  }

  async function send(event: FormEvent) {
    event.preventDefault()
    if (!previewFresh) return
    setBusy(true)
    setMessage(null)
    try {
      const sent = await api.post<AnnouncementSummary>('/api/announcements', { title, body, filter })
      setMessage({ text: `Duyuru ${sent.recipientCount} öğrenciye gönderildi.`, ok: true })
      setTitle('')
      setBody('')
      setDraft(EMPTY)
      setPreview(null)
      list.retry()
    } catch (e) {
      setMessage({ text: e instanceof ApiError ? e.message : 'Duyuru gönderilemedi.', ok: false })
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <form className="card" onSubmit={send}>
        <h2>Yeni duyuru</h2>
        <div className="form-grid">
          <label className="field span-2">
            <span>Başlık</span>
            <input value={title} onChange={(e) => setTitle(e.target.value)} maxLength={150} required />
          </label>
          <label className="field span-2">
            <span>Metin</span>
            <textarea value={body} onChange={(e) => setBody(e.target.value)} rows={4} maxLength={2000} required />
          </label>
        </div>

        <h3 className="filter-title">Hedef kitle</h3>
        <div className="form-grid">
          <label className="field">
            <span>Sınıf</span>
            <select value={draft.classYear} onChange={(e) => patch({ classYear: e.target.value })}>
              <option value="">Tümü</option>
              {[1, 2, 3, 4].map((y) => (
                <option key={y} value={y}>
                  {y}. sınıf
                </option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>En düşük GANO</span>
            <input inputMode="decimal" value={draft.minGpa} onChange={(e) => patch({ minGpa: e.target.value })} placeholder="ör. 2.0" />
          </label>
          <label className="field">
            <span>En yüksek GANO</span>
            <input inputMode="decimal" value={draft.maxGpa} onChange={(e) => patch({ maxGpa: e.target.value })} placeholder="ör. 3.5" />
          </label>
          <label className="field">
            <span>Risk seviyesi</span>
            <select value={draft.risk} onChange={(e) => patch({ risk: e.target.value as Draft['risk'] })}>
              <option value="">Fark etmez</option>
              {(['Yuksek', 'Orta', 'Dusuk'] as RiskLevel[]).map((l) => (
                <option key={l} value={l}>
                  {RISK_LEVEL_LABELS[l]}
                </option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>Staj durumu</span>
            <select value={draft.internship} onChange={(e) => patch({ internship: e.target.value as Draft['internship'] })}>
              <option value="">Fark etmez</option>
              <option value="done">Stajı tamamlayanlar</option>
              <option value="notdone">Stajı tamamlamayanlar</option>
            </select>
          </label>
        </div>

        <div className="btn-row submit-row">
          <button type="button" className="btn" disabled={busy} onClick={() => void showAudience()}>
            Hedef kitleyi göster
          </button>
          <button type="submit" className="btn btn-primary" disabled={busy || !previewFresh || preview.data.count === 0 || !title.trim() || !body.trim()}>
            {previewFresh ? `Gönder (${preview.data.count} öğrenci)` : 'Gönder'}
          </button>
          {!previewFresh && <span className="muted">Göndermeden önce hedef kitleyi görün.</span>}
        </div>

        {message && (
          <p className={message.ok ? 'form-ok' : 'form-error'} role={message.ok ? 'status' : 'alert'}>
            {message.text}
          </p>
        )}

        {previewFresh && (
          <div className="audience">
            <p>
              <strong>{preview.data.count}</strong> öğrenci eşleşti <span className="muted">({describe(filter)})</span>
            </p>
            {preview.data.count > 0 && (
              <div className="table-wrap audience-table">
                <table>
                  <thead>
                    <tr>
                      <th>Öğrenci</th>
                      <th>Sınıf</th>
                      <th>GANO</th>
                      <th>Risk</th>
                      <th>Staj (gün)</th>
                    </tr>
                  </thead>
                  <tbody>
                    {preview.data.students.map((s) => (
                      <tr key={s.studentNo}>
                        <td>
                          {s.fullName} <span className="muted">({s.studentNo})</span>
                        </td>
                        <td>{s.classYear}</td>
                        <td>{s.gpa === null ? '-' : formatGpa(s.gpa)}</td>
                        <td>
                          <RiskBadge level={s.risk} />
                        </td>
                        <td>{s.internshipDays}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}
      </form>

      <section className="card">
        <h2>Gönderilen duyurular</h2>
        {list.state.status === 'loading' && <Loading />}
        {list.state.status === 'error' && <ErrorState message={list.state.message} onRetry={list.retry} />}
        {list.state.status === 'ready' &&
          (list.state.data.length === 0 ? (
            <EmptyState title="Henüz duyuru göndermediniz" />
          ) : (
            list.state.data.map((a) => (
              <div className="announcement-item" key={a.id}>
                <div className="page-head">
                  <div>
                    <strong>{a.title}</strong>
                    <div className="muted">
                      {formatDateTime(a.createdAt)} · {describe(a.filter)}
                    </div>
                  </div>
                  <button type="button" className="btn btn-small" aria-expanded={openId === a.id} onClick={() => setOpenId(openId === a.id ? null : a.id)}>
                    {a.readCount} / {a.recipientCount} okudu
                  </button>
                </div>
                {openId === a.id && <AnnouncementDetailView id={a.id} />}
              </div>
            ))
          ))}
      </section>
    </>
  )
}

function AnnouncementDetailView({ id }: { id: string }) {
  const { state, retry } = useAsync(() => api.get<AnnouncementDetail>(`/api/announcements/${id}`), [id])

  if (state.status === 'loading') return <Loading />
  if (state.status === 'error') return <ErrorState message={state.message} onRetry={retry} />

  return (
    <>
      <p className="announcement-body">{state.data.summary.body}</p>
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Öğrenci</th>
              <th>Durum</th>
            </tr>
          </thead>
          <tbody>
            {state.data.recipients.map((r) => (
              <tr key={r.studentNo}>
                <td>
                  {r.fullName} <span className="muted">({r.studentNo})</span>
                </td>
                <td>
                  {r.readAt ? <span className="badge badge-ok">Okudu · {formatDateTime(r.readAt)}</span> : <span className="badge">Okumadı</span>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

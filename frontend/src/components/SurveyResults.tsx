import { Fragment, useState } from 'react'
import { api, ApiError } from '../api/client'
import type { ResultsResponse } from '../api/types'
import { EmptyState, ErrorState, Loading } from './Feedback'
import { formatScore } from './format'
import { useAsync } from './useAsync'

const SUPPORT_SUGGESTIONS = ['Yemek kartı', 'Burs', 'Barınma desteği', 'Kırtasiye desteği']

/** Sıralı ihtiyaç listesi: seçim (ilk N / eşik), destek ataması, CSV dışa aktarma ve puan kırılımı. */
export function SurveyResults({ surveyId }: { surveyId: string }) {
  const { state, retry } = useAsync(() => api.get<ResultsResponse>(`/api/surveys/${surveyId}/results`), [surveyId])
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [expanded, setExpanded] = useState<string | null>(null)
  const [topN, setTopN] = useState('10')
  const [threshold, setThreshold] = useState('')
  const [support, setSupport] = useState('Yemek kartı')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null)

  if (state.status === 'loading') return <Loading />
  if (state.status === 'error') return <ErrorState message={state.message} onRetry={retry} />

  const { rows, responseCount, targetCount } = state.data
  if (rows.length === 0) {
    return (
      <EmptyState title="Henüz yanıt yok">
        Öğrenciler anketi doldurdukça ya da CSV ile sonuç içe aktardıkça burada puana göre sıralanır.
      </EmptyState>
    )
  }

  const toggle = (no: string) =>
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(no)) next.delete(no)
      else next.add(no)
      return next
    })

  function selectTop() {
    const n = Math.floor(Number(topN))
    if (!Number.isFinite(n) || n < 1) return setMessage({ text: 'Geçerli bir sayı girin.', ok: false })
    setSelected(new Set(rows.slice(0, n).map((r) => r.studentNo)))
    setMessage(null)
  }

  function selectAbove() {
    const t = Number(threshold.replace(',', '.'))
    if (threshold.trim() === '' || !Number.isFinite(t)) return setMessage({ text: 'Geçerli bir eşik puanı girin.', ok: false })
    setSelected(new Set(rows.filter((r) => r.totalScore >= t).map((r) => r.studentNo)))
    setMessage(null)
  }

  async function assign(clear: boolean) {
    if (selected.size === 0) return setMessage({ text: 'Önce öğrenci seçin.', ok: false })
    if (!clear && support.trim() === '') return setMessage({ text: 'Destek türünü yazın.', ok: false })
    setBusy(true)
    setMessage(null)
    try {
      await api.post(`/api/surveys/${surveyId}/support`, { studentNos: [...selected], supportType: clear ? '' : support })
      setMessage({ text: clear ? 'Atamalar kaldırıldı.' : `${selected.size} öğrenciye "${support.trim()}" atandı.`, ok: true })
      setSelected(new Set())
      retry()
    } catch (e) {
      setMessage({ text: e instanceof ApiError ? e.message : 'İşlem başarısız oldu.', ok: false })
    } finally {
      setBusy(false)
    }
  }

  async function exportCsv() {
    try {
      await api.download(`/api/surveys/${surveyId}/export`, 'ihtiyac-puanlari.csv')
    } catch (e) {
      setMessage({ text: e instanceof ApiError ? e.message : 'Dosya indirilemedi.', ok: false })
    }
  }

  return (
    <>
      <p className="muted">
        {responseCount} yanıt / {targetCount} hedef öğrenci. Puana tıklayarak puanın hangi cevaplardan oluştuğunu görün.
      </p>

      <div className="toolbar select-toolbar">
        <div className="inline-field">
          <label htmlFor="topn">İlk</label>
          <input id="topn" className="narrow" inputMode="numeric" value={topN} onChange={(e) => setTopN(e.target.value)} />
          <button type="button" className="btn btn-small" onClick={selectTop}>
            öğrenciyi seç
          </button>
        </div>
        <div className="inline-field">
          <label htmlFor="thr">Puanı</label>
          <input id="thr" className="narrow" inputMode="decimal" value={threshold} onChange={(e) => setThreshold(e.target.value)} />
          <button type="button" className="btn btn-small" onClick={selectAbove}>
            ve üstündekileri seç
          </button>
        </div>
        <button type="button" className="btn btn-small" onClick={() => setSelected(new Set())} disabled={selected.size === 0}>
          Seçimi temizle
        </button>
      </div>

      <div className="toolbar select-toolbar">
        <span className="muted">{selected.size} seçili</span>
        <input
          aria-label="Destek türü"
          list="support-types"
          value={support}
          onChange={(e) => setSupport(e.target.value)}
          maxLength={100}
        />
        <datalist id="support-types">
          {SUPPORT_SUGGESTIONS.map((s) => (
            <option key={s} value={s} />
          ))}
        </datalist>
        <button type="button" className="btn btn-primary btn-small" disabled={busy} onClick={() => void assign(false)}>
          Destek ata
        </button>
        <button type="button" className="btn btn-small" disabled={busy} onClick={() => void assign(true)}>
          Atamayı kaldır
        </button>
        <button type="button" className="btn btn-small push-right" onClick={() => void exportCsv()}>
          CSV indir
        </button>
      </div>

      {message && (
        <p className={message.ok ? 'form-ok' : 'form-error'} role={message.ok ? 'status' : 'alert'}>
          {message.text}
        </p>
      )}

      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th aria-label="Seç" />
              <th>Sıra</th>
              <th>Öğrenci</th>
              <th>Sınıf</th>
              <th>Puan</th>
              <th>Destek</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <Fragment key={r.studentNo}>
                <tr className={selected.has(r.studentNo) ? 'row-selected' : undefined}>
                  <td>
                    <input
                      type="checkbox"
                      aria-label={`${r.fullName} seç`}
                      checked={selected.has(r.studentNo)}
                      onChange={() => toggle(r.studentNo)}
                    />
                  </td>
                  <td>{r.rank}</td>
                  <td>
                    {r.fullName} <span className="muted">({r.studentNo})</span>
                    {r.importedExternally && <span className="badge badge-muted-dark">CSV</span>}
                  </td>
                  <td>{r.classYear || '-'}</td>
                  <td>
                    <button
                      type="button"
                      className="link-btn"
                      aria-expanded={expanded === r.studentNo}
                      onClick={() => setExpanded(expanded === r.studentNo ? null : r.studentNo)}
                    >
                      <strong>{formatScore(r.totalScore)}</strong>
                    </button>
                  </td>
                  <td>{r.support ? <span className="badge badge-ok">{r.support}</span> : '-'}</td>
                </tr>
                {expanded === r.studentNo && (
                  <tr className="breakdown-row">
                    <td colSpan={6}>
                      <table className="breakdown">
                        <thead>
                          <tr>
                            <th>Soru</th>
                            <th>Cevap</th>
                            <th>Puan</th>
                          </tr>
                        </thead>
                        <tbody>
                          {r.breakdown.map((b) => (
                            <tr key={b.questionId}>
                              <td>{b.questionText}</td>
                              <td>{b.answerText}</td>
                              <td>{formatScore(b.points)}</td>
                            </tr>
                          ))}
                          <tr>
                            <td colSpan={2}>
                              <strong>Toplam</strong>
                            </td>
                            <td>
                              <strong>{formatScore(r.totalScore)}</strong>
                            </td>
                          </tr>
                        </tbody>
                      </table>
                    </td>
                  </tr>
                )}
              </Fragment>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}

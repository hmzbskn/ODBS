import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, ApiError } from '../api/client'
import type { SurveyForm } from '../api/types'
import { EmptyState, ErrorState, Loading } from '../components/Feedback'
import { formatDate } from '../components/format'
import { useAsync } from '../components/useAsync'

interface Draft {
  options: string[]
  number: string
  text: string
}

const emptyDraft = (): Draft => ({ options: [], number: '', text: '' })

export function StudentSurveyFormPage() {
  const { id } = useParams()
  const { state, retry } = useAsync(() => api.get<SurveyForm>(`/api/surveys/${id}/form`), [id])
  const [drafts, setDrafts] = useState<Record<string, Draft>>({})
  const [consent, setConsent] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)

  if (done) {
    return (
      <div className="card">
        <EmptyState title="Yanıtınız kaydedildi">
          Teşekkürler. Yanıtlarınızı yalnızca danışmanınız görebilir. <Link to="/anketlerim">Anketlerime dön</Link>
        </EmptyState>
      </div>
    )
  }
  if (state.status === 'loading') return <Loading />
  if (state.status === 'error') {
    return (
      <>
        <p>
          <Link to="/anketlerim">← Anketlerim</Link>
        </p>
        <ErrorState message={state.message} onRetry={retry} />
      </>
    )
  }

  const form = state.data
  const draftOf = (qid: string) => drafts[qid] ?? emptyDraft()
  const patch = (qid: string, p: Partial<Draft>) => setDrafts((d) => ({ ...d, [qid]: { ...draftOf(qid), ...p } }))

  function toggleMulti(qid: string, optionId: string) {
    const current = draftOf(qid).options
    patch(qid, { options: current.includes(optionId) ? current.filter((o) => o !== optionId) : [...current, optionId] })
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    for (const q of form.questions) {
      const d = draftOf(q.id)
      const answered =
        q.type === 'Number' ? d.number.trim() !== '' : q.type === 'Text' ? d.text.trim() !== '' : d.options.length > 0
      if (q.required && !answered) return setError(`"${q.text}" sorusunu yanıtlamalısınız.`)
      if (q.type === 'Number' && answered && !Number.isFinite(Number(d.number.replace(',', '.'))))
        return setError(`"${q.text}" sorusu için geçerli bir sayı girin.`)
    }

    setBusy(true)
    try {
      await api.post(`/api/surveys/${form.id}/submit`, {
        consent,
        answers: form.questions.map((q) => {
          const d = draftOf(q.id)
          return {
            questionId: q.id,
            optionIds: q.type === 'SingleChoice' || q.type === 'MultiChoice' ? d.options : undefined,
            number: q.type === 'Number' && d.number.trim() !== '' ? Number(d.number.replace(',', '.')) : undefined,
            text: q.type === 'Text' ? d.text : undefined,
          }
        }),
      })
      setDone(true)
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Yanıt gönderilemedi.')
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <p>
        <Link to="/anketlerim">← Anketlerim</Link>
      </p>
      <h1>{form.title}</h1>
      {form.description && <p>{form.description}</p>}
      <p className="muted">Son gün: {formatDate(form.endDate)}</p>

      <form onSubmit={onSubmit}>
        <section className="card">
          <h2>Kişisel verilerin korunması</h2>
          <div className="consent-box" tabIndex={0} aria-label="KVKK aydınlatma metni">
            {form.consentText}
          </div>
          <label className="check consent-check">
            <input type="checkbox" checked={consent} onChange={(e) => setConsent(e.target.checked)} />
            {form.consentStatement}
          </label>
        </section>

        {form.questions.map((q, i) => (
          <fieldset className="card question-fieldset" key={q.id}>
            <legend>
              {i + 1}. {q.text}
              {q.required && <span className="req" aria-label="zorunlu"> *</span>}
            </legend>

            {q.type === 'SingleChoice' &&
              q.options.map((o) => (
                <label className="check" key={o.id}>
                  <input
                    type="radio"
                    name={q.id}
                    checked={draftOf(q.id).options[0] === o.id}
                    onChange={() => patch(q.id, { options: [o.id] })}
                  />
                  {o.text}
                </label>
              ))}

            {q.type === 'MultiChoice' &&
              q.options.map((o) => (
                <label className="check" key={o.id}>
                  <input type="checkbox" checked={draftOf(q.id).options.includes(o.id)} onChange={() => toggleMulti(q.id, o.id)} />
                  {o.text}
                </label>
              ))}

            {q.type === 'Number' && (
              <input
                className="narrow"
                inputMode="decimal"
                aria-label={q.text}
                value={draftOf(q.id).number}
                onChange={(e) => patch(q.id, { number: e.target.value })}
              />
            )}

            {q.type === 'Text' && (
              <textarea
                rows={3}
                maxLength={1000}
                aria-label={q.text}
                value={draftOf(q.id).text}
                onChange={(e) => patch(q.id, { text: e.target.value })}
              />
            )}
          </fieldset>
        ))}

        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}

        <div className="btn-row submit-row">
          <button type="submit" className="btn btn-primary" disabled={busy || !consent}>
            {busy ? 'Gönderiliyor…' : 'Yanıtları gönder'}
          </button>
          {!consent && <span className="muted">Göndermek için açık rıza onayı gerekir.</span>}
        </div>
      </form>
    </>
  )
}

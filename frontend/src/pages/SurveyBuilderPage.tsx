import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { api, ApiError } from '../api/client'
import type { CreateSurveyPayload, QuestionType, SurveyDefinition } from '../api/types'
import { QUESTION_TYPE_LABELS } from '../components/labels'

interface DraftOption {
  key: number
  text: string
  score: string
}

interface DraftQuestion {
  key: number
  text: string
  type: QuestionType
  required: boolean
  weight: string
  options: DraftOption[]
}

let nextKey = 1
const newOption = (): DraftOption => ({ key: nextKey++, text: '', score: '0' })
const newQuestion = (): DraftQuestion => ({
  key: nextKey++,
  text: '',
  type: 'SingleChoice',
  required: true,
  weight: '1',
  options: [newOption(), newOption()],
})

const isChoice = (t: QuestionType) => t === 'SingleChoice' || t === 'MultiChoice'

function toNumber(value: string): number {
  return Number(value.trim().replace(',', '.'))
}

function today(): string {
  return new Date().toISOString().slice(0, 10)
}

export function SurveyBuilderPage() {
  const navigate = useNavigate()
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [startDate, setStartDate] = useState(today())
  const [endDate, setEndDate] = useState('')
  const [targetClass, setTargetClass] = useState('')
  const [questions, setQuestions] = useState<DraftQuestion[]>([newQuestion()])
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const updateQuestion = (key: number, patch: Partial<DraftQuestion>) =>
    setQuestions((qs) => qs.map((q) => (q.key === key ? { ...q, ...patch } : q)))

  const updateOption = (qKey: number, oKey: number, patch: Partial<DraftOption>) =>
    setQuestions((qs) =>
      qs.map((q) =>
        q.key === qKey ? { ...q, options: q.options.map((o) => (o.key === oKey ? { ...o, ...patch } : o)) } : q,
      ),
    )

  function move(index: number, delta: number) {
    setQuestions((qs) => {
      const target = index + delta
      if (target < 0 || target >= qs.length) return qs
      const copy = [...qs]
      ;[copy[index], copy[target]] = [copy[target], copy[index]]
      return copy
    })
  }

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)

    for (const [i, q] of questions.entries()) {
      if (!Number.isFinite(toNumber(q.weight))) return setError(`${i + 1}. sorunun ağırlığı geçerli bir sayı olmalı.`)
      if (isChoice(q.type) && q.options.some((o) => !Number.isFinite(toNumber(o.score))))
        return setError(`${i + 1}. sorudaki seçenek puanları geçerli birer sayı olmalı.`)
    }

    const payload: CreateSurveyPayload = {
      title,
      description: description.trim() || null,
      startDate,
      endDate,
      targetClassYear: targetClass ? Number(targetClass) : null,
      questions: questions.map((q) => ({
        text: q.text,
        type: q.type,
        weight: toNumber(q.weight),
        required: q.required,
        options: isChoice(q.type) ? q.options.map((o) => ({ text: o.text, score: toNumber(o.score) })) : null,
      })),
    }

    setBusy(true)
    try {
      const created = await api.post<SurveyDefinition>('/api/surveys', payload)
      navigate(`/anketler/${created.id}`)
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'Anket oluşturulamadı.')
      setBusy(false)
    }
  }

  return (
    <>
      <p>
        <Link to="/anketler">← Anketler</Link>
      </p>
      <h1>Yeni anket</h1>

      <form onSubmit={onSubmit}>
        <section className="card form-grid">
          <label className="field span-2">
            <span>Başlık</span>
            <input value={title} onChange={(e) => setTitle(e.target.value)} maxLength={200} required />
          </label>
          <label className="field span-2">
            <span>Açıklama (isteğe bağlı)</span>
            <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={2} maxLength={2000} />
          </label>
          <label className="field">
            <span>Başlangıç</span>
            <input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} required />
          </label>
          <label className="field">
            <span>Bitiş</span>
            <input type="date" value={endDate} min={startDate} onChange={(e) => setEndDate(e.target.value)} required />
          </label>
          <label className="field">
            <span>Hedef öğrenciler</span>
            <select value={targetClass} onChange={(e) => setTargetClass(e.target.value)}>
              <option value="">Tüm öğrencilerim</option>
              {[1, 2, 3, 4].map((y) => (
                <option key={y} value={y}>
                  Yalnızca {y}. sınıf
                </option>
              ))}
            </select>
          </label>
        </section>

        <p className="muted hint">
          Puanlama: seçim sorularında seçilen seçeneklerin puanı, sayı sorularında girilen değer, sorunun ağırlığıyla
          çarpılır. Ağırlık ya da puan negatif olabilir (ör. gelir arttıkça ihtiyaç azalır). Puanları anket açıldıktan
          sonra da değiştirebilirsiniz.
        </p>

        {questions.map((q, index) => (
          <section className="card question-card" key={q.key}>
            <div className="question-head">
              <strong>Soru {index + 1}</strong>
              <div className="btn-row">
                <button type="button" className="btn btn-small" onClick={() => move(index, -1)} disabled={index === 0}>
                  ↑
                </button>
                <button
                  type="button"
                  className="btn btn-small"
                  onClick={() => move(index, 1)}
                  disabled={index === questions.length - 1}
                >
                  ↓
                </button>
                <button
                  type="button"
                  className="btn btn-small"
                  onClick={() => setQuestions((qs) => qs.filter((x) => x.key !== q.key))}
                  disabled={questions.length === 1}
                >
                  Sil
                </button>
              </div>
            </div>

            <div className="form-grid">
              <label className="field span-2">
                <span>Soru metni</span>
                <input value={q.text} onChange={(e) => updateQuestion(q.key, { text: e.target.value })} maxLength={300} required />
              </label>
              <label className="field">
                <span>Soru tipi</span>
                <select value={q.type} onChange={(e) => updateQuestion(q.key, { type: e.target.value as QuestionType })}>
                  {(Object.keys(QUESTION_TYPE_LABELS) as QuestionType[]).map((t) => (
                    <option key={t} value={t}>
                      {QUESTION_TYPE_LABELS[t]}
                    </option>
                  ))}
                </select>
              </label>
              {q.type !== 'Text' && (
                <label className="field">
                  <span>Ağırlık</span>
                  <input
                    inputMode="decimal"
                    value={q.weight}
                    onChange={(e) => updateQuestion(q.key, { weight: e.target.value })}
                  />
                </label>
              )}
              <label className="check span-2">
                <input
                  type="checkbox"
                  checked={q.required}
                  onChange={(e) => updateQuestion(q.key, { required: e.target.checked })}
                />
                Zorunlu soru
              </label>
            </div>

            {isChoice(q.type) && (
              <div className="options">
                <div className="option-row option-header muted">
                  <span>Seçenek</span>
                  <span>Puan</span>
                  <span />
                </div>
                {q.options.map((o) => (
                  <div className="option-row" key={o.key}>
                    <input
                      aria-label="Seçenek metni"
                      value={o.text}
                      onChange={(e) => updateOption(q.key, o.key, { text: e.target.value })}
                      maxLength={200}
                      required
                    />
                    <input
                      aria-label="Seçenek puanı"
                      inputMode="decimal"
                      value={o.score}
                      onChange={(e) => updateOption(q.key, o.key, { score: e.target.value })}
                    />
                    <button
                      type="button"
                      className="btn btn-small"
                      onClick={() => updateQuestion(q.key, { options: q.options.filter((x) => x.key !== o.key) })}
                      disabled={q.options.length <= 2}
                      aria-label="Seçeneği sil"
                    >
                      ✕
                    </button>
                  </div>
                ))}
                <button
                  type="button"
                  className="btn btn-small"
                  onClick={() => updateQuestion(q.key, { options: [...q.options, newOption()] })}
                  disabled={q.options.length >= 20}
                >
                  + Seçenek ekle
                </button>
              </div>
            )}
          </section>
        ))}

        <div className="btn-row">
          <button type="button" className="btn" onClick={() => setQuestions((qs) => [...qs, newQuestion()])} disabled={questions.length >= 30}>
            + Soru ekle
          </button>
        </div>

        {error && (
          <p className="form-error" role="alert">
            {error}
          </p>
        )}

        <div className="btn-row submit-row">
          <button type="submit" className="btn btn-primary" disabled={busy}>
            {busy ? 'Oluşturuluyor…' : 'Anketi oluştur'}
          </button>
          <Link to="/anketler" className="btn">
            Vazgeç
          </Link>
        </div>
      </form>
    </>
  )
}

import { useState } from 'react'
import { api, ApiError } from '../api/client'
import type { SurveyDefinition } from '../api/types'
import { QUESTION_TYPE_LABELS } from './labels'

interface Props {
  survey: SurveyDefinition
  onSaved: () => void
}

const toNumber = (v: string) => Number(v.trim().replace(',', '.'))

/** Soru ağırlıkları ve seçenek puanları. Kayıt, mevcut yanıtların puanlarına da hemen yansır. */
export function SurveyScoringEditor({ survey, onSaved }: Props) {
  const [weights, setWeights] = useState<Record<string, string>>(
    Object.fromEntries(survey.questions.map((q) => [q.id, String(q.weight)])),
  )
  const [scores, setScores] = useState<Record<string, string>>(
    Object.fromEntries(survey.questions.flatMap((q) => q.options.map((o) => [o.id, String(o.score)]))),
  )
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null)

  async function save() {
    const all = [...Object.values(weights), ...Object.values(scores)]
    if (all.some((v) => !Number.isFinite(toNumber(v)) || v.trim() === '')) {
      return setMessage({ text: 'Tüm ağırlık ve puanlar geçerli birer sayı olmalı.', ok: false })
    }

    setBusy(true)
    setMessage(null)
    try {
      await api.put(`/api/surveys/${survey.id}/scoring`, {
        questions: survey.questions
          .filter((q) => q.type !== 'Text')
          .map((q) => ({
            questionId: q.id,
            weight: toNumber(weights[q.id]),
            options: q.options.map((o) => ({ optionId: o.id, score: toNumber(scores[o.id]) })),
          })),
      })
      setMessage({ text: 'Puanlama kaydedildi; sonuçlar yeniden hesaplandı.', ok: true })
      onSaved()
    } catch (e) {
      setMessage({ text: e instanceof ApiError ? e.message : 'Kaydedilemedi.', ok: false })
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <p className="muted">
        Soru puanı = (seçilen seçeneklerin puanı toplamı ya da girilen sayı) x ağırlık. Negatif değerler kullanılabilir.
      </p>

      {survey.questions.map((q) => (
        <div className="scoring-q" key={q.id}>
          <div className="scoring-head">
            <div>
              <strong>{q.text}</strong> <span className="badge">{QUESTION_TYPE_LABELS[q.type]}</span>
            </div>
            {q.type !== 'Text' && (
              <label className="inline-field">
                <span className="muted">Ağırlık</span>
                <input
                  className="narrow"
                  inputMode="decimal"
                  value={weights[q.id]}
                  onChange={(e) => setWeights((w) => ({ ...w, [q.id]: e.target.value }))}
                />
              </label>
            )}
          </div>
          {q.options.length > 0 && (
            <div className="options">
              {q.options.map((o) => (
                <div className="option-row" key={o.id}>
                  <span>{o.text}</span>
                  <input
                    aria-label={`${o.text} puanı`}
                    className="narrow"
                    inputMode="decimal"
                    value={scores[o.id]}
                    onChange={(e) => setScores((s) => ({ ...s, [o.id]: e.target.value }))}
                  />
                </div>
              ))}
            </div>
          )}
          {q.type === 'Text' && <p className="muted">Metin soruları puanlanmaz.</p>}
        </div>
      ))}

      {message && (
        <p className={message.ok ? 'form-ok' : 'form-error'} role={message.ok ? 'status' : 'alert'}>
          {message.text}
        </p>
      )}
      <button type="button" className="btn btn-primary" disabled={busy} onClick={() => void save()}>
        {busy ? 'Kaydediliyor…' : 'Puanlamayı kaydet'}
      </button>
    </>
  )
}

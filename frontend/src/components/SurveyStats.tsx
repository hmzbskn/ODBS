import { api } from '../api/client'
import type { StatsResponse } from '../api/types'
import { ErrorState, Loading } from './Feedback'
import { formatScore } from './format'
import { useAsync } from './useAsync'

/** Anonim özet: kimlik ve serbest metin içermez; seçenek dağılımı ve sayı özetleri. */
export function SurveyStats({ surveyId }: { surveyId: string }) {
  const { state, retry } = useAsync(() => api.get<StatsResponse>(`/api/surveys/${surveyId}/stats`), [surveyId])

  if (state.status === 'loading') return <Loading />
  if (state.status === 'error') return <ErrorState message={state.message} onRetry={retry} />

  const { responseCount, targetCount, questions } = state.data
  const rate = targetCount > 0 ? Math.round((responseCount / targetCount) * 100) : 0

  return (
    <>
      <p>
        Katılım: <strong>{responseCount}</strong> / {targetCount} öğrenci (%{rate})
      </p>
      <p className="muted">Bu özet kimlik bilgisi içermez.</p>

      {questions.map((q) => {
        const max = Math.max(1, ...(q.options?.map((o) => o.count) ?? [0]))
        return (
          <div className="stat-q" key={q.questionId}>
            <h3>{q.text}</h3>
            {q.options && (
              <ul className="bars">
                {q.options.map((o) => (
                  <li key={o.text}>
                    <span className="bar-label">{o.text}</span>
                    <span className="bar-track" aria-hidden="true">
                      <span className="bar-fill" style={{ width: `${(o.count / max) * 100}%` }} />
                    </span>
                    <span className="bar-count">{o.count}</span>
                  </li>
                ))}
              </ul>
            )}
            {q.number && (
              <p>
                En az {formatScore(q.number.min)} · ortalama {formatScore(q.number.average)} · en çok{' '}
                {formatScore(q.number.max)} <span className="muted">({q.number.count} yanıt)</span>
              </p>
            )}
            {q.type === 'Number' && !q.number && <p className="muted">Henüz yanıt yok.</p>}
            {q.type === 'Text' && <p className="muted">{q.answeredCount} yanıt (serbest metinler bu özette gösterilmez).</p>}
          </div>
        )
      })}
    </>
  )
}

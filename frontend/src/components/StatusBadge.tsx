import type { SurveyStatus } from '../api/types'
import { SURVEY_STATUS_LABELS } from './labels'

export function StatusBadge({ status }: { status: SurveyStatus }) {
  const cls = status === 'Open' ? 'badge badge-ok' : status === 'Planned' ? 'badge badge-warn' : 'badge'
  return <span className={cls}>{SURVEY_STATUS_LABELS[status]}</span>
}
